// -----------------------------------------------------------------------------
// IppRelay.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Moves print job bytes between a client and the printer, and does nothing
//   else with them.
//
// This is the part the README's first disclosure is about:
//   Print job data passes through this machine. That is inherent to proxying
//   and cannot be engineered away. What CAN be controlled is what happens to
//   the data while it is here, and the answer is: it is copied from one socket
//   to another and forgotten.
//
//     - The payload is never parsed, inspected or modified (REQ-PXY-003).
//       There is no IPP parser in this project, and no branch anywhere that
//       depends on the content of a byte.
//     - The payload is never written to disk (REQ-PXY-004). The whole project
//       contains no reference to any file-writing type, and a test inspects the
//       compiled assembly's metadata to keep it that way.
//     - The payload is never logged. The observer interface below has no
//       parameter capable of carrying job content - only endpoints, counts and
//       durations (REQ-PXY-009).
//     - The payload is streamed through a pooled fixed-size buffer, so a large
//       job does not become a large allocation (REQ-PXY-005).
//
// This project knows nothing about mDNS, printers or discovery. It is handed a
// delegate that returns where to connect, and that is the entirety of what it
// knows about the world.
// -----------------------------------------------------------------------------

using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SecretPrinter.Spec;

namespace SecretPrinter.Proxy;

/// <summary>
/// Receives notifications about relayed connections.
/// </summary>
/// <remarks>
/// Every method takes endpoints, counts, durations or reasons. None takes a
/// byte array, a stream, or anything else capable of carrying job content. That
/// is deliberate and is enforced by a test: an implementation cannot log a
/// document because it is never given one (REQ-PXY-009, REQ-OBS-004).
/// </remarks>
[Requirement("REQ-OBS-004",
    "No method on this interface accepts a byte array, stream or span, so an implementation cannot log job content: it is never given any.")]
public interface IRelayObserver
{
    void ConnectionAccepted(EndPoint? client);

    void ConnectionRefused(EndPoint? client, string reason);

    void RelayStarted(EndPoint? client, IPEndPoint printer);

    void RelayCompleted(
        EndPoint? client, IPEndPoint printer, long bytesToPrinter, long bytesToClient, TimeSpan duration);

    void RelayFailed(EndPoint? client, string reason);
}

/// <summary>How a single relayed connection ended.</summary>
public sealed record RelayOutcome(
    bool Succeeded,
    long BytesToPrinter,
    long BytesToClient,
    TimeSpan Duration,
    string? Failure);

/// <summary>Settings for the relay. All explicit; none changes what is sent.</summary>
public sealed class RelayOptions
{
    /// <summary>
    /// Size of the pooled copy buffer, in bytes. Bounded on purpose: a print job
    /// may be tens of megabytes and must never be held whole in memory.
    /// </summary>
    public int BufferSize { get; init; } = 64 * 1024;

    /// <summary>How long to wait for the printer to accept a connection.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Networks a client connection may originate from. Empty means no
    /// restriction, which the service must never configure; the service builds
    /// this from the client interfaces it was configured for.
    /// </summary>
    public IReadOnlyList<IPNetwork> AllowedClientNetworks { get; init; } = [];
}

/// <summary>Streams IPP bytes between a client and the printer.</summary>
public sealed class IppRelay
{
    private readonly IConnectionListener _listener;
    private readonly IConnectionFactory _factory;
    private readonly Func<CancellationToken, Task<IPEndPoint>> _resolvePrinter;
    private readonly RelayOptions _options;
    private readonly IRelayObserver _observer;

    /// <param name="listener">Where client connections arrive.</param>
    /// <param name="factory">How connections to the printer are opened.</param>
    /// <param name="resolvePrinter">
    /// Returns the printer's current endpoint. A delegate rather than a
    /// dependency, so this project stays free of any knowledge of mDNS - and so
    /// the address is fetched per connection rather than remembered.
    /// </param>
    /// <param name="options">Buffer size, timeout and permitted client networks.</param>
    /// <param name="observer">Receives endpoints, counts and durations. Never content.</param>
    public IppRelay(
        IConnectionListener listener,
        IConnectionFactory factory,
        Func<CancellationToken, Task<IPEndPoint>> resolvePrinter,
        RelayOptions options,
        IRelayObserver observer)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(resolvePrinter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(observer);

        if (options.BufferSize is < 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Buffer size {options.BufferSize} is outside 1 KiB - 1 MiB. Too small wastes syscalls; "
                + "too large defeats the point of streaming.");
        }

        _listener = listener;
        _factory = factory;
        _resolvePrinter = resolvePrinter;
        _options = options;
        _observer = observer;
    }

    /// <summary>
    /// Accepts connections until cancelled, relaying each one independently.
    /// </summary>
    [Requirement("REQ-PXY-001",
        "Accepts connections from the listener, which the service binds to configured client interfaces only.")]
    [Requirement("REQ-PXY-008",
        "Each accepted connection is relayed on its own task, so one slow or stalled job cannot block another.")]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var running = new List<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
            IDuplexConnection client;
            try
            {
                client = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                // A failed accept is not a reason to stop accepting.
                _observer.RelayFailed(null, $"Accept failed: {ex.SocketErrorCode}");
                continue;
            }

            running.RemoveAll(t => t.IsCompleted);
            running.Add(Task.Run(() => RelayOneAsync(client, cancellationToken), cancellationToken));
        }

        // Let in-flight jobs finish rather than cutting a document in half.
        await Task.WhenAll(running).ConfigureAwait(false);
    }

    /// <summary>
    /// Relays one client connection to the printer and back, then closes both.
    /// </summary>
    [Requirement("REQ-PXY-002",
        "Opens a connection to the resolved printer endpoint and joins it to the accepted client connection.")]
    [Requirement("REQ-PXY-003",
        "Copies bytes verbatim in both directions. Nothing here reads, branches on, or alters payload content.")]
    [Requirement("REQ-PXY-006",
        "Both connections are disposed when either direction ends, so a half-open pair cannot linger.")]
    [Requirement("REQ-PXY-007",
        "A printer that cannot be reached or resolved produces a prompt, reported failure rather than a hang.")]
    [Requirement("REQ-SEC-012",
        "A client whose address is outside the permitted networks is refused and reported before any connection to the printer is opened.")]
    [Requirement("REQ-PXY-009",
        "Reports endpoints, byte counts and elapsed time to the observer, and has no means of reporting content.")]
    [Requirement("REQ-SEC-011",
        "The destination comes solely from the resolvePrinter delegate supplied at construction. Nothing a client sends can influence where the relay connects, so this cannot be used as a general-purpose proxy.")]
    public async Task<RelayOutcome> RelayOneAsync(IDuplexConnection client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var stopwatch = Stopwatch.StartNew();
        _observer.ConnectionAccepted(client.RemoteEndPoint);

        try
        {
            if (!IsPermitted(client.RemoteEndPoint, out string? refusal))
            {
                _observer.ConnectionRefused(client.RemoteEndPoint, refusal);
                return new RelayOutcome(false, 0, 0, stopwatch.Elapsed, refusal);
            }

            IPEndPoint printer;
            try
            {
                printer = await _resolvePrinter(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string reason = $"Could not locate the printer: {ex.Message}";
                _observer.RelayFailed(client.RemoteEndPoint, reason);
                return new RelayOutcome(false, 0, 0, stopwatch.Elapsed, reason);
            }

            IDuplexConnection upstream;
            try
            {
                upstream = await _factory
                    .ConnectAsync(printer, _options.ConnectTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string reason = $"Could not connect to the printer at {printer}: {ex.Message}";
                _observer.RelayFailed(client.RemoteEndPoint, reason);
                return new RelayOutcome(false, 0, 0, stopwatch.Elapsed, reason);
            }

            await using (upstream.ConfigureAwait(false))
            {
                _observer.RelayStarted(client.RemoteEndPoint, printer);

                // One direction finishing means the conversation is over; the
                // linked source stops the other rather than leaving it waiting
                // on a socket nobody will write to.
                using var bothDirections = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Counts are held outside the tasks so they survive a direction
                // being cancelled. Reading them from the Task results only works
                // when a task ran to completion, and in practice one direction
                // almost always ends by being stopped rather than by reaching
                // end-of-stream.
                var counters = new RelayCounters();

                Task toPrinter = PumpAsync(client.Stream, upstream.Stream, bothDirections, counters.AddSent);
                Task toClient = PumpAsync(upstream.Stream, client.Stream, bothDirections, counters.AddReceived);

                string? failure = null;

                try
                {
                    await Task.WhenAll(toPrinter, toClient).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Expected, and the normal way a relay ends. One direction
                    // reached end-of-stream and stopped the other, which
                    // surfaces here as a cancellation.
                    //
                    // This was originally treated as an unexpected failure and
                    // allowed to escape, which meant no relayed connection was
                    // ever reported as completed or failed. It was invisible in
                    // unit tests, where both sides close together and usually
                    // reach end-of-stream before either cancels the other; it
                    // showed up immediately against a real printer, which holds
                    // its side open.
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failure = $"Relay ended early: {ex.Message}";
                }

                long sent = counters.Sent;
                long received = counters.Received;

                stopwatch.Stop();

                if (failure is null)
                {
                    _observer.RelayCompleted(
                        client.RemoteEndPoint, printer, sent, received, stopwatch.Elapsed);
                    return new RelayOutcome(true, sent, received, stopwatch.Elapsed, null);
                }

                _observer.RelayFailed(client.RemoteEndPoint, failure);
                return new RelayOutcome(false, sent, received, stopwatch.Elapsed, failure);
            }
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies one direction until the source ends, then signals the other
    /// direction to stop.
    /// </summary>
    /// <remarks>
    /// The buffer is rented from the shared pool and is a fixed size regardless
    /// of job size. A fifty-megabyte photo print moves through in
    /// buffer-sized pieces and is never held whole (REQ-PXY-005).
    /// </remarks>
    [Requirement("REQ-PXY-005",
        "Streams through a pooled fixed-size buffer; job size does not affect memory held.")]
    [Requirement("REQ-PXY-004",
        "The only destination for bytes read here is the opposite stream. Nothing is written to disk, and this project references no file-writing type.")]
    private async Task PumpAsync(Stream from, Stream to, CancellationTokenSource stopBoth, Action<int> count)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(_options.BufferSize);

        try
        {
            while (true)
            {
                int read = await from
                    .ReadAsync(buffer.AsMemory(0, _options.BufferSize), stopBoth.Token)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                await to.WriteAsync(buffer.AsMemory(0, read), stopBoth.Token).ConfigureAwait(false);
                await to.FlushAsync(stopBoth.Token).ConfigureAwait(false);

                count(read);
            }
        }
        finally
        {
            // Cleared before returning: a pooled buffer handed to unrelated code
            // must not still contain a fragment of somebody's document.
            Array.Clear(buffer);
            ArrayPool<byte>.Shared.Return(buffer);

            if (!stopBoth.IsCancellationRequested)
            {
                await stopBoth.CancelAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Byte counts for one relayed connection, updated as bytes move rather
    /// than read from a task result, so a cancelled direction still reports what
    /// it carried.
    /// </summary>
    private sealed class RelayCounters
    {
        private long _sent;
        private long _received;

        public long Sent => Interlocked.Read(ref _sent);

        public long Received => Interlocked.Read(ref _received);

        public void AddSent(int bytes) => Interlocked.Add(ref _sent, bytes);

        public void AddReceived(int bytes) => Interlocked.Add(ref _received, bytes);
    }

    private bool IsPermitted(EndPoint? remote, out string refusal)
    {
        if (_options.AllowedClientNetworks.Count == 0)
        {
            refusal = string.Empty;
            return true;
        }

        if (remote is not IPEndPoint endpoint)
        {
            refusal = "Connection has no IP endpoint, so it cannot be checked against the permitted networks.";
            return false;
        }

        foreach (IPNetwork network in _options.AllowedClientNetworks)
        {
            if (network.Contains(endpoint.Address))
            {
                refusal = string.Empty;
                return true;
            }
        }

        refusal =
            $"{endpoint.Address} is not on a permitted client network "
            + $"({string.Join(", ", _options.AllowedClientNetworks)}).";
        return false;
    }
}

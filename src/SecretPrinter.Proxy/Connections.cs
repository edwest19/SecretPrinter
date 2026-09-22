// -----------------------------------------------------------------------------
// Connections.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// TcpConnectionFactory's connect deadline changed by Claude (Anthropic model,
// Claude Opus 5) at the direction of Edwin West, 2026-09-21: the factory's own
// deadline now fails as a TimeoutException instead of a cancellation, so the
// relay reports it. See docs/findings/2026-09-17-connect-timeout-is-not-reported.md.
// Reviewed by a human before merge.
//
// IDuplexConnection.Encryption added by Claude (Anthropic model, Claude Opus
// 5) at the direction of Edwin West, 2026-09-22, for REQ-OBS-008. Reviewed by
// a human before merge.
//
// Purpose:
//   The narrowest possible view of TCP, so the relay can be tested with
//   in-memory streams.
//
//   TcpListener and TcpClient are concrete. A relay written against them
//   directly could only be tested with real sockets, real ports and real
//   timing - which means slow, flaky tests that eventually get skipped. The
//   print path is the most security-sensitive code in this project and is the
//   last place that should happen.
//
// Note on what is NOT here:
//   Nothing in this file, or anywhere in SecretPrinter.Proxy, opens a file. The
//   project deliberately contains no other responsibility, so a reviewer
//   checking REQ-PXY-004 - that no part of a print job is ever written to disk -
//   reads one small project rather than the whole solution. There is a test
//   that inspects the compiled assembly's type references to enforce it.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace SecretPrinter.Proxy;

/// <summary>A two-way byte stream to some peer.</summary>
public interface IDuplexConnection : IAsyncDisposable
{
    /// <summary>The stream carrying bytes in both directions.</summary>
    Stream Stream { get; }

    /// <summary>Who is at the other end, for logging. Null when unknown.</summary>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// How this connection is protected, for logging: <c>none</c> for plain
    /// TCP, or the negotiated protocol for a TLS connection.
    /// </summary>
    /// <remarks>
    /// A description of the connection, fixed when it was opened. It is never
    /// derived from anything the connection carries. It exists so the relay can
    /// report how each connection to the printer was protected (REQ-OBS-008)
    /// while knowing nothing about TLS itself: the relay passes this text on and
    /// never reads it.
    /// </remarks>
    string Encryption { get; }
}

/// <summary>Accepts incoming connections.</summary>
public interface IConnectionListener : IAsyncDisposable
{
    /// <summary>Where this listener is bound.</summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>Waits for the next incoming connection.</summary>
    Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken);
}

/// <summary>Opens outgoing connections.</summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Connects to a destination, failing rather than hanging when it cannot be
    /// reached.
    /// </summary>
    Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>A real TCP connection.</summary>
public sealed class TcpConnection : IDuplexConnection
{
    private readonly TcpClient _client;

    public TcpConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;

        // Print jobs are streamed, so Nagle buffering only adds latency between
        // the client and the printer without reducing packet count meaningfully.
        _client.NoDelay = true;

        Stream = client.GetStream();
        RemoteEndPoint = client.Client.RemoteEndPoint;
    }

    public Stream Stream { get; }

    public EndPoint? RemoteEndPoint { get; }

    /// <summary>Always <c>none</c>: this is TCP with nothing on top of it.</summary>
    public string Encryption => "none";

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Listens for real TCP connections on one address and port.</summary>
public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly TcpListener _listener;

    /// <summary>
    /// Binds to one specific address, never to the wildcard. Binding every
    /// interface would accept print jobs on the printer network too, which
    /// REQ-PXY-001 forbids.
    /// </summary>
    public TcpConnectionListener(IPAddress address, ushort port)
    {
        ArgumentNullException.ThrowIfNull(address);

        _listener = new TcpListener(address, port);
        _listener.Start();
        LocalEndPoint = (IPEndPoint)_listener.LocalEndpoint;
    }

    public IPEndPoint LocalEndPoint { get; }

    public async Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        return new TcpConnection(client);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Opens real TCP connections to the printer.</summary>
/// <remarks>
/// Two ways a connect can stop early, and they mean different things. The
/// caller cancelling - the service stopping, or the printer being withdrawn -
/// is a cancellation and stays one. This factory's own deadline running out
/// means the printer did not accept a connection in the time allowed; that is
/// a failed job, and it is reported as a <see cref="TimeoutException"/>
/// because <see cref="IppRelay"/> reports failures and deliberately does not
/// report cancellations. Until 2026-09-21 both came out as a cancellation, so a
/// printer that never answered produced a job that ended with nothing logged.
/// That shape was seen on FIOS-STB-01 on 2026-09-21 - ten connections accepted
/// and located, then no further line for any of them - while its printer-side
/// WLAN was failing. The finding records what that run does and does not show.
/// </remarks>
public sealed class TcpConnectionFactory : IConnectionFactory
{
    private readonly Func<TcpClient, IPEndPoint, CancellationToken, ValueTask> _connect;

    /// <summary>Connects using the operating system's TCP stack.</summary>
    public TcpConnectionFactory()
        : this(static (client, destination, token) => client.ConnectAsync(destination, token))
    {
    }

    /// <summary>
    /// Replaces only the socket connect itself, so the deadline and the way it
    /// is reported can be tested without a network or a timing race. The
    /// deadline, its translation and the disposal are the same code the service
    /// runs. Internal: only this assembly and its test project can reach it.
    /// </summary>
    internal TcpConnectionFactory(Func<TcpClient, IPEndPoint, CancellationToken, ValueTask> connect)
    {
        ArgumentNullException.ThrowIfNull(connect);
        _connect = connect;
    }

    public async Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var client = new TcpClient();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);

            try
            {
                await _connect(client, destination, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The caller did not cancel, so the deadline did. The original
                // is kept as the inner exception so nothing about it is lost.
                throw new TimeoutException(
                    $"No TCP connection to {destination} was established within "
                    + $"{timeout.TotalSeconds:0.#}s. Nothing is claimed about why.",
                    ex);
            }

            return new TcpConnection(client);
        }
        catch
        {
            // A half-open client left behind would hold a socket handle for a
            // job that is already failing.
            client.Dispose();
            throw;
        }
    }
}

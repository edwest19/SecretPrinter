// -----------------------------------------------------------------------------
// PrinterResolver.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Interface matching widened to compare address family by Claude (Anthropic
// model, Claude Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed
// by a human before merge.
//
// Continuous reading, so that only replies arriving during a lookup are
// considered, added by Claude (Anthropic model, Claude Opus 5) at the direction
// of Edwin West, 2026-09-16. Reviewed by a human before merge.
//
// The timeout message stopped asserting anything about the printer, by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-19.
// Reviewed by a human before merge. See "On what a timeout entitles us to say"
// below.
//
// Lookups made at the same time share one query, including when it goes
// unanswered, by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-25. Reviewed by a human before merge. See "On sharing one
// query" below.
//
// Purpose:
//   Finds where the real printer currently is, by asking the printer network,
//   at the moment the answer is needed.
//
// Why it works this way:
//   Printers move. During development the target printer was observed at
//   192.168.12.186 and later at 192.168.12.180, within one session, because it
//   is on DHCP like everything else on a home network. Any design that stores
//   the address breaks silently at the next lease renewal - the printer appears
//   in the client's list, and every job fails at TCP connect.
//
//   So the printer is identified by its DNS-SD instance name, which is stable
//   across address changes, and the address is looked up on demand. That is
//   REQ-RES-001 through REQ-RES-003, and it is the reason this class takes an
//   instance name and offers no way to pass an address in.
//
// On caching:
//   REQ-RES-004 permits a cache, bounded by the TTL of the record the answer
//   came from. Caching is exactly the optimisation that quietly reintroduces
//   the stale-address bug, so the bound here is the record's own TTL, taken as
//   the minimum across the records used, and nothing extends it. When a cached
//   entry expires and a fresh lookup fails, the expired value is discarded
//   rather than returned: a stale address that once worked is more dangerous
//   than an honest failure, because it produces a job that vanishes instead of
//   an error somebody can act on (REQ-RES-005).
//
// On reading the transport:
//   Since 2026-09-16 the resolver has a socket of its own rather than sharing
//   the responder's (docs/findings/2026-09-14-shared-receive-loop.md). A socket
//   nobody reads still queues what arrives, so a resolver that read only while
//   looking up could find replies waiting that arrived long before - the
//   printer's answers to other devices' queries, for instance - and take the
//   first one naming our instance as the answer, with its TTL counted from now.
//   That is a remembered address by another route. This was reasoned from the
//   code before the separate socket shipped, not observed. So the transport is read continuously from construction,
//   and a datagram is kept only if a lookup is waiting when it is read;
//   everything else is discarded. A datagram that is already being read at the
//   instant a lookup begins can still reach it. One that sat waiting between
//   lookups cannot.
//
// On what a timeout entitles us to say:
//   Until 2026-09-19 a timeout produced "The printer may be asleep, off, or on
//   a different network." That sentence was measured false thirty-eight times
//   across two evenings, on two different adapters, while the printer was awake
//   and answering probes from this same machine. A timeout is the absence of an
//   answer; it carries no information about the device that did not send one.
//
//   So the resolver now examines the only thing it can examine - its own
//   interface - and says what it found. When the local side accounts for the
//   failure, it says so. When it does not, it says that the reason was not
//   established and names both possibilities, rather than picking the one that
//   blames someone else. See PrinterInterfaceReport.cs.
//
// On sharing one query:
//   REQ-RES-008 says concurrent jobs share one in-flight query rather than
//   issuing one apiece. Until 2026-09-25 lookups were only serialised: a lookup
//   that arrived while another was running waited for it, and then used its
//   answer from the cache - but if that query went unanswered, the waiting
//   lookup asked again and waited out its own timeout. On 2026-09-18 that is
//   how print jobs queued behind one another's failures. See
//   docs/findings/2026-09-25-two-clauses-of-req-res-008-were-never-built.md.
//
//   Now a lookup that arrives while a query for the same instance is running
//   joins it, and every caller receives that query's outcome: its answer, or
//   its failure. The query belongs to all of them, so it runs with the
//   resolver's own lifetime rather than any caller's cancellation token. A
//   caller that is cancelled stops waiting; the query goes on for the others,
//   and ends at its own timeout or when the resolver is disposed. A caller that
//   joins takes the running query's timeout, not the one it passed.
//
//   The resolver listens on behalf of one query at a time, so a lookup for a
//   different instance cannot join and must not run beside it. It waits for the
//   running query to end, whatever its outcome, and then starts its own.
//
//   A query's outcome is handed only to the callers that were waiting on it. It
//   stops being joinable before it is delivered, so a lookup that arrives
//   afterwards asks again rather than receiving an old failure.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Resolution;

/// <summary>Where the printer was found, and for how long that answer holds.</summary>
/// <param name="Instance">The DNS-SD instance that was resolved.</param>
/// <param name="Host">The host name the instance's SRV record named.</param>
/// <param name="Address">The address that host resolved to.</param>
/// <param name="Port">The port the SRV record named.</param>
/// <param name="TxtStrings">
/// The instance's TXT entries, carried out so the advertisement can be built
/// from a live observation rather than from anything stored (REQ-ADV-010).
/// </param>
/// <param name="ResolvedAt">When the answer was obtained.</param>
/// <param name="Ttl">Shortest TTL among the records used, in seconds.</param>
public sealed record ResolvedPrinter(
    DnsName Instance,
    DnsName Host,
    IPAddress Address,
    ushort Port,
    IReadOnlyList<string> TxtStrings,
    DateTimeOffset ResolvedAt,
    uint Ttl)
{
    public DateTimeOffset ExpiresAt => ResolvedAt + TimeSpan.FromSeconds(Ttl);

    public bool IsFreshAt(DateTimeOffset now) => now < ExpiresAt;

    public override string ToString() => $"{Instance} at {Address}:{Port} (host {Host}, TTL {Ttl}s)";
}

/// <summary>Thrown when the printer could not be located. Never swallowed.</summary>
public sealed class PrinterResolutionException(string message) : Exception(message);

/// <summary>Locates the printer on the printer-side network, on demand.</summary>
public sealed class PrinterResolver : IDisposable
{
    private readonly IMdnsTransport _transport;
    private readonly MdnsInterface _printerInterface;
    private readonly TimeProvider _clock;
    private readonly IInterfaceInventory _inventory;
    private readonly CancellationTokenSource _readerLifetime = new();
    private readonly CancellationToken _lifetime;
    private readonly Task _reader;

    // Guards _cached, _inFlight and _disposed. Never held across an await.
    private readonly object _sync = new();

    private ResolvedPrinter? _cached;

    // The query now running, which lookups for the same instance join, or null
    // when none is running. See "On sharing one query" above.
    private SharedLookup? _inFlight;

    // The inbox of the query that is running, or null when none is. Only one
    // query runs at a time, so there is at most one. Read and written with
    // Volatile because the reader loop runs on another thread.
    private Channel<MdnsDatagram>? _listening;

    // Why the reader loop stopped, if it has. A lookup that starts after this is
    // set fails at once with the reason, rather than waiting out its timeout.
    private string? _readerStopped;

    private bool _disposed;

    /// <param name="transport">The mDNS transport to query through.</param>
    /// <param name="printerInterface">
    /// The interface facing the printer. Queries are sent only here, and
    /// replies arriving anywhere else are ignored.
    /// </param>
    /// <param name="clock">
    /// Supplied so cache expiry can be tested without waiting. Defaults to the
    /// system clock.
    /// </param>
    /// <param name="inventory">
    /// Where the printer-side interface is examined when a lookup times out.
    /// Supplied so that every interface state can be tested on a machine which
    /// has none of them. Defaults to the adapters actually present.
    /// </param>
    public PrinterResolver(
        IMdnsTransport transport,
        MdnsInterface printerInterface,
        TimeProvider? clock = null,
        IInterfaceInventory? inventory = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(printerInterface);

        // Matches() rather than an index comparison: the transport's interface
        // list can now hold one adapter twice, once per address family, and the
        // two families number indexes separately. An index-only test could
        // accept an IPv6 entry as proof that the IPv4 printer interface is held.
        if (!transport.Interfaces.Any(i => i.Matches(printerInterface)))
        {
            throw new ArgumentException(
                $"Interface {printerInterface} is not held by the transport. Resolution would send "
                + "nothing and time out.",
                nameof(printerInterface));
        }

        _transport = transport;
        _printerInterface = printerInterface;
        _clock = clock ?? TimeProvider.System;
        _inventory = inventory ?? SystemInterfaceInventory.Instance;

        // Captured once. A query that is running when the resolver is disposed
        // is ended through this token; reading Token from the source after it
        // has been disposed would throw.
        _lifetime = _readerLifetime.Token;

        // Started last, once nothing above can throw.
        _reader = Task.Run(() => ReadContinuouslyAsync(_lifetime));
    }

    /// <summary>The cached answer, if any. Exposed for logging and tests.</summary>
    public ResolvedPrinter? Cached => _cached;

    /// <summary>
    /// Resolves the printer, using a cached answer only while it is still
    /// within the TTL it was given.
    /// </summary>
    /// <remarks>
    /// A lookup made while a query for the same instance is running joins that
    /// query and receives its outcome, answer or failure, with that query's
    /// timeout. Cancelling <paramref name="cancellationToken"/> stops this
    /// caller's wait only; the query goes on for any other caller waiting on it.
    /// See "On sharing one query" at the top of this file.
    /// </remarks>
    /// <exception cref="PrinterResolutionException">
    /// No usable answer arrived before the timeout. The caller must fail the
    /// print job; there is deliberately no fallback.
    /// </exception>
    [Requirement("REQ-RES-001",
        "Queries the printer network by mDNS at the moment the address is needed, rather than reading a stored one.")]
    [Requirement("REQ-RES-002",
        "Takes only an instance name. There is no parameter, field or configuration path by which an address could be supplied instead.")]
    [Requirement("REQ-RES-003",
        "Identifies the printer by DNS-SD instance name, which survives the DHCP address changes that were observed on the real device.")]
    [Requirement("REQ-RES-004",
        "Reuses a cached answer only while the clock is before the expiry derived from the shortest record TTL received.")]
    [Requirement("REQ-RES-005",
        "On failure, throws with a specific message and discards any expired cache entry rather than returning a stale address.")]
    public async Task<ResolvedPrinter> ResolveAsync(
        DnsName instance, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Resolution timeout must be positive.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SharedLookup? started = null;
            SharedLookup? joined = null;
            SharedLookup? ahead = null;

            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_cached is { } cached && cached.Instance.Equals(instance) && cached.IsFreshAt(_clock.GetUtcNow()))
                {
                    return cached;
                }

                if (_inFlight is null)
                {
                    // An expired entry is dropped before the lookup, so a failure
                    // below cannot silently fall back to it.
                    _cached = null;
                    started = new SharedLookup(instance, timeout);
                    _inFlight = started;
                }
                else if (_inFlight.Instance.Equals(instance))
                {
                    joined = _inFlight;
                }
                else
                {
                    ahead = _inFlight;
                }
            }

            if (ahead is not null)
            {
                await WaitForTurnAsync(ahead, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (started is not null)
            {
                // Not awaited here. The query belongs to every caller that joins
                // it, so this caller waits on its outcome like the others.
                _ = RunAsync(started);
            }

            SharedLookup lookup = started ?? joined!;
            return await lookup.Outcome.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Discards any cached answer, forcing the next resolution to query.</summary>
    public void Invalidate()
    {
        lock (_sync)
        {
            _cached = null;
        }
    }

    /// <summary>
    /// Stops reading the transport and ends any query that is running, whose
    /// callers then receive its failure. New lookups are refused. The transport
    /// is not disposed here: this class was handed it and does not own it.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cached = null;
        }

        _readerLifetime.Cancel();

        // The reader stops as soon as its pending read observes the
        // cancellation. The wait is bounded so a transport that ignores
        // cancellation cannot hang shutdown; if it does not stop in time, the
        // token source is left for the garbage collector rather than disposed
        // under a read that may still be using it.
        bool stopped = _reader.Wait(TimeSpan.FromSeconds(2));
        if (stopped)
        {
            _readerLifetime.Dispose();
        }
    }

    /// <summary>
    /// Runs one query on behalf of every caller that joins it, and hands its
    /// outcome to them. Never throws: every outcome, failure included, is
    /// delivered through <see cref="SharedLookup.Outcome"/>.
    /// </summary>
    private async Task RunAsync(SharedLookup lookup)
    {
        ResolvedPrinter? resolved = null;
        Exception? failure = null;

        try
        {
            resolved = await QueryAsync(lookup.Instance, lookup.Timeout, _lifetime).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Not handled here: delivered below to every caller waiting on this
            // query, exactly as it would have reached a single caller.
            failure = ex;
        }

        lock (_sync)
        {
            if (resolved is not null)
            {
                _cached = resolved;
            }

            _inFlight = null;
        }

        // Delivered only after the query has stopped being joinable, so a
        // lookup that arrives from here on asks again rather than being handed
        // this outcome.
        if (failure is null)
        {
            lookup.Outcome.SetResult(resolved!);
        }
        else
        {
            lookup.Outcome.SetException(failure);
        }
    }

    /// <summary>
    /// Waits for a query for another instance to end. Its outcome belongs to
    /// the callers who asked for that instance, so it is not reported here;
    /// only this caller's own cancellation is.
    /// </summary>
    private static async Task WaitForTurnAsync(SharedLookup ahead, CancellationToken cancellationToken)
    {
        try
        {
            await ahead.Outcome.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // That query failed. It was not this caller's question.
        }
    }

    /// <summary>
    /// Reads the transport for as long as the resolver exists, passing each
    /// datagram to the lookup that is waiting, or discarding it when none is.
    /// </summary>
    private async Task ReadContinuouslyAsync(CancellationToken lifetime)
    {
        while (true)
        {
            MdnsDatagram datagram;
            try
            {
                datagram = await _transport.ReceiveAsync(lifetime).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                StopReading("the resolver was disposed");
                return;
            }
            catch (SocketException)
            {
                // Transient network trouble, handled as the responder handles
                // it: one lost datagram is not a reason to stop.
                continue;
            }
            catch (Exception ex)
            {
                // Anything else - the transport disposed under us, for one - is
                // not transient. Stop, and make lookups say why.
                StopReading($"reading {_printerInterface} failed: {ex.Message}");
                return;
            }

            // Kept only if a lookup is waiting now. Otherwise it arrived between
            // lookups, is not a reply to anything we asked, and is dropped.
            Volatile.Read(ref _listening)?.Writer.TryWrite(datagram);
        }
    }

    private void StopReading(string reason)
    {
        Volatile.Write(ref _readerStopped, reason);
        Volatile.Read(ref _listening)?.Writer.TryComplete();
    }

    private async Task<ResolvedPrinter> QueryAsync(
        DnsName instance, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        // Listening starts before the query is sent, so a reply that comes back
        // at once is not missed, and ends when this lookup does, so nothing read
        // afterwards is kept for the next one.
        Channel<MdnsDatagram> inbox = Channel.CreateUnbounded<MdnsDatagram>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        Volatile.Write(ref _listening, inbox);

        try
        {
            return await ReceiveAnswerAsync(instance, timeout, inbox.Reader, deadline.Token, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _listening, null);
        }
    }

    [Requirement("REQ-RES-006",
        "Sends the query out of the printer-side interface only, and ignores replies that arrived on any other.")]
    private async Task<ResolvedPrinter> ReceiveAnswerAsync(
        DnsName instance,
        TimeSpan timeout,
        ChannelReader<MdnsDatagram> inbox,
        CancellationToken deadline,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _readerStopped) is { } stopped)
        {
            throw new PrinterResolutionException(
                $"'{instance}' cannot be resolved: the resolver has stopped reading, because {stopped}. "
                + "No address is assumed.");
        }

        byte[] query = new DnsQueryBuilder(0)
            .AddQuestion(instance, DnsRecordType.Srv, requestUnicastResponse: false)
            .AddQuestion(instance, DnsRecordType.Txt, requestUnicastResponse: false)
            .Build();

        await _transport.SendMulticastAsync(query, _printerInterface, deadline).ConfigureAwait(false);

        DnsName? host = null;
        ushort port = 0;
        IPAddress? address = null;
        IReadOnlyList<string>? txt = null;
        uint ttl = uint.MaxValue;
        bool askedForAddress = false;

        while (true)
        {
            MdnsDatagram datagram;
            try
            {
                datagram = await inbox.ReadAsync(deadline).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                throw new PrinterResolutionException(
                    $"'{instance}' cannot be resolved: the resolver stopped reading during the lookup, because "
                    + $"{Volatile.Read(ref _readerStopped)}. No address is assumed.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Nothing arrived. Before saying anything, look at the one side
                // of this that can actually be looked at.
                PrinterInterfaceReport local = PrinterInterfaceInspector.Inspect(_printerInterface, _inventory);

                throw new PrinterResolutionException(local.LocalFaultFound
                    ? $"'{instance}' could not be asked on {_printerInterface}: {local.Description} "
                      + "The printer was not reached, and nothing is claimed about it. No address is assumed."
                    : $"'{instance}' did not answer within {timeout.TotalSeconds:0.#}s on "
                      + $"{_printerInterface}. Nothing wrong was found locally: {local.Description} "
                      + "Why no answer arrived was not established - the printer may not have answered, "
                      + "or this host may no longer be receiving multicast on this interface. "
                      + "No address is assumed.");
            }

            // Replies from any other interface are not about our printer.
            if (datagram.InterfaceIndex != _printerInterface.Index)
            {
                continue;
            }

            DnsMessage message;
            try
            {
                message = DnsMessage.Parse(datagram.Payload, datagram.Payload.Length);
            }
            catch (InvalidDataException)
            {
                continue;
            }

            if (!message.IsResponse)
            {
                continue;
            }

            foreach (DnsRecord record in message.AllRecords)
            {
                switch (record.Type)
                {
                    case DnsRecordType.Srv when record.Name.Equals(instance) && record.SrvTarget is { } target:
                        host = target;
                        port = record.SrvPort;
                        ttl = Math.Min(ttl, record.Ttl);
                        break;

                    case DnsRecordType.Txt when record.Name.Equals(instance) && record.TxtStrings is { } strings:
                        txt = strings;
                        break;

                    case DnsRecordType.A when host is not null && record.Name.Equals(host)
                                              && record.Address is { } found:
                        address = found;
                        ttl = Math.Min(ttl, record.Ttl);
                        break;
                }
            }

            if (host is not null && address is not null)
            {
                return new ResolvedPrinter(
                    instance, host, address, port, txt ?? [], _clock.GetUtcNow(),
                    ttl == uint.MaxValue ? 0 : ttl);
            }

            // The SRV arrived without an address alongside it. Ask for the host
            // directly, once, rather than waiting for something that may never
            // come.
            if (host is not null && !askedForAddress)
            {
                askedForAddress = true;

                byte[] addressQuery = new DnsQueryBuilder(0)
                    .AddQuestion(host, DnsRecordType.A, requestUnicastResponse: false)
                    .Build();

                await _transport.SendMulticastAsync(addressQuery, _printerInterface, deadline)
                                .ConfigureAwait(false);
            }
        }
    }

    /// <summary>One query, and the outcome every caller waiting on it receives.</summary>
    private sealed class SharedLookup
    {
        public SharedLookup(DnsName instance, TimeSpan timeout)
        {
            Instance = instance;
            Timeout = timeout;

            // If every caller stops waiting before the query ends, nobody reads
            // its failure. Observing it here keeps that from being reported
            // later as an unobserved task exception. It changes nothing a
            // waiting caller sees.
            _ = Outcome.Task.ContinueWith(
                static finished => _ = finished.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public DnsName Instance { get; }

        public TimeSpan Timeout { get; }

        // Continuations run asynchronously, so completing this never runs a
        // waiting caller's code on the thread that ran the query.
        public TaskCompletionSource<ResolvedPrinter> Outcome { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

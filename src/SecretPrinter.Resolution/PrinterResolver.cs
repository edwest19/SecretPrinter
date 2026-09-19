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
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _readerLifetime = new();
    private readonly Task _reader;

    private ResolvedPrinter? _cached;

    // The inbox of the lookup that is waiting, or null when none is. Lookups are
    // serialised by _gate, so there is at most one. Read and written with
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

        // Started last, once nothing above can throw.
        _reader = Task.Run(() => ReadContinuouslyAsync(_readerLifetime.Token));
    }

    /// <summary>The cached answer, if any. Exposed for logging and tests.</summary>
    public ResolvedPrinter? Cached => _cached;

    /// <summary>
    /// Resolves the printer, using a cached answer only while it is still
    /// within the TTL it was given.
    /// </summary>
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

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _clock.GetUtcNow();

            if (_cached is { } cached && cached.Instance.Equals(instance) && cached.IsFreshAt(now))
            {
                return cached;
            }

            // An expired entry is dropped before the lookup, so a failure below
            // cannot silently fall back to it.
            _cached = null;

            ResolvedPrinter resolved = await QueryAsync(instance, timeout, cancellationToken)
                .ConfigureAwait(false);

            _cached = resolved;
            return resolved;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Discards any cached answer, forcing the next resolution to query.</summary>
    public void Invalidate() => _cached = null;

    /// <summary>
    /// Stops reading the transport and releases the lock that serialises
    /// lookups. The transport is not disposed here: this class was handed it and
    /// does not own it.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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

        _cached = null;
        _gate.Dispose();
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
}

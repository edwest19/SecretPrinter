// -----------------------------------------------------------------------------
// PrinterResolver.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
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
// -----------------------------------------------------------------------------

using System.Net;
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
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ResolvedPrinter? _cached;

    /// <param name="transport">The mDNS transport to query through.</param>
    /// <param name="printerInterface">
    /// The interface facing the printer. Queries are sent only here, and
    /// replies arriving anywhere else are ignored.
    /// </param>
    /// <param name="clock">
    /// Supplied so cache expiry can be tested without waiting. Defaults to the
    /// system clock.
    /// </param>
    public PrinterResolver(IMdnsTransport transport, MdnsInterface printerInterface, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(printerInterface);

        if (!transport.Interfaces.Any(i => i.Index == printerInterface.Index))
        {
            throw new ArgumentException(
                $"Interface {printerInterface} is not held by the transport. Resolution would send "
                + "nothing and time out.",
                nameof(printerInterface));
        }

        _transport = transport;
        _printerInterface = printerInterface;
        _clock = clock ?? TimeProvider.System;
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
    /// Releases the lock that serialises lookups. The transport is not disposed
    /// here: this class was handed it and does not own it.
    /// </summary>
    public void Dispose()
    {
        _cached = null;
        _gate.Dispose();
    }

    [Requirement("REQ-RES-006",
        "Sends the query out of the printer-side interface only, and ignores replies that arrived on any other.")]
    private async Task<ResolvedPrinter> QueryAsync(
        DnsName instance, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        byte[] query = new DnsQueryBuilder(0)
            .AddQuestion(instance, DnsRecordType.Srv, requestUnicastResponse: false)
            .AddQuestion(instance, DnsRecordType.Txt, requestUnicastResponse: false)
            .Build();

        await _transport.SendMulticastAsync(query, _printerInterface, deadline.Token).ConfigureAwait(false);

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
                datagram = await _transport.ReceiveAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new PrinterResolutionException(
                    $"'{instance}' did not answer within {timeout.TotalSeconds:0.#}s on "
                    + $"{_printerInterface}. The printer may be asleep, off, or on a different network. "
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

                await _transport.SendMulticastAsync(addressQuery, _printerInterface, deadline.Token)
                                .ConfigureAwait(false);
            }
        }
    }
}

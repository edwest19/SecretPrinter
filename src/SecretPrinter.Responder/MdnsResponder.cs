// -----------------------------------------------------------------------------
// MdnsResponder.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Interface matching widened to compare address family by Claude (Anthropic
// model, Claude Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed
// by a human before merge.
//
// Purpose:
//   The loop that joins the two halves the service already has:
//   AdvertisementBuilder decides WHAT to publish, MdnsSocket moves the bytes,
//   and this decides WHEN and TO WHOM.
//
//   It announces on startup, answers queries for names it owns, ignores
//   everything else, and retracts its advertisement on shutdown.
//
// The rule that keeps this narrow:
//   The responder holds no names of its own. Every name it will answer for is
//   read out of the Advertisement it was handed. There is no list of service
//   types in this file. A query is answered only if some record in that
//   advertisement is about exactly the name asked for, which is what makes
//   REQ-ADV-012 a structural property rather than a filter somebody has to
//   maintain.
//
// Behaviour measured, not assumed:
//   Answering by multicast, from a socket sharing port 5353 with the Windows
//   DNS Client, was accepted by a real iPhone on 2026-09-02. See
//   docs/findings/2026-09-02-ios-accepts-advertisement.md. This code reproduces
//   that behaviour rather than improving on it.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Responder;

/// <summary>One interface, and what is advertised on it.</summary>
/// <remarks>
/// Advertisements are per-interface because the A record must carry the
/// proxy's address on the interface the answer leaves by. One advertisement
/// reused across interfaces would publish the wrong address on all but one.
/// </remarks>
public sealed record AdvertisedInterface(MdnsInterface Interface, Advertisement Advertisement);

/// <summary>What a responder did, for logging and for tests.</summary>
public sealed record ResponderActivity(
    int AnnouncementsSent,
    int QueriesSeen,
    int QueriesAnswered,
    int IgnoredWrongInterface,
    int IgnoredNotOurs,
    int Unparseable,
    int GoodbyesSent);

/// <summary>Answers mDNS queries for the names the proxy advertises, and nothing else.</summary>
public sealed class MdnsResponder
{
    /// <summary>
    /// TTL ceiling for answers to legacy unicast queriers (RFC 6762 §6.7). Such
    /// clients do not understand cache-flush semantics, so their answers must
    /// expire quickly.
    /// </summary>
    public const uint LegacyUnicastTtl = 10;

    /// <summary>
    /// Unsolicited announcements sent at startup. RFC 6762 §8.3 asks for at
    /// least two, a second apart; three gives margin against loss on wireless.
    /// </summary>
    public const int AnnouncementCount = 3;

    private readonly IMdnsTransport _transport;
    private readonly IReadOnlyList<AdvertisedInterface> _advertised;
    private readonly Dictionary<int, AdvertisedInterface> _byIndex;

    private int _announcements;
    private int _queriesSeen;
    private int _queriesAnswered;
    private int _ignoredWrongInterface;
    private int _ignoredNotOurs;
    private int _unparseable;
    private int _goodbyes;

    public MdnsResponder(IMdnsTransport transport, IReadOnlyList<AdvertisedInterface> advertised)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(advertised);

        if (advertised.Count == 0)
        {
            throw new ArgumentException(
                "A responder with nothing to advertise would bind a port and answer nobody.",
                nameof(advertised));
        }

        foreach (AdvertisedInterface entry in advertised)
        {
            // Matches() rather than an index comparison: see the same change in
            // PrinterResolver. One adapter can appear in the transport's list
            // once per address family, and the indexes are numbered separately.
            if (!transport.Interfaces.Any(i => i.Matches(entry.Interface)))
            {
                throw new ArgumentException(
                    $"Advertisement is for {entry.Interface}, which the transport does not hold. "
                    + "Advertising on an interface the socket cannot send from would silently produce "
                    + "nothing.",
                    nameof(advertised));
            }
        }

        _transport = transport;
        _advertised = advertised;
        _byIndex = advertised.ToDictionary(entry => entry.Interface.Index);
    }

    public ResponderActivity Activity => new(
        _announcements, _queriesSeen, _queriesAnswered,
        _ignoredWrongInterface, _ignoredNotOurs, _unparseable, _goodbyes);

    /// <summary>
    /// Sends unsolicited announcements on every advertised interface, so clients
    /// learn about the printer without having to ask first.
    /// </summary>
    /// <param name="delayBetween">
    /// Gap between announcements. Injected so tests need not wait real seconds.
    /// </param>
    [Requirement("REQ-ADV-001",
        "Announces the advertised IPP service on each configured interface at startup.")]
    [Requirement("REQ-ADV-002",
        "The announcement carries every record in the advertisement, which includes the AirPrint subtype PTR that iOS queries for.")]
    public async Task AnnounceAsync(TimeSpan delayBetween, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < AnnouncementCount; attempt++)
        {
            foreach (AdvertisedInterface entry in _advertised)
            {
                var builder = new DnsResponseBuilder();
                foreach (OutgoingRecord record in entry.Advertisement.Records)
                {
                    builder.AddAnswer(record);
                }

                await _transport
                    .SendMulticastAsync(builder.Build(), entry.Interface, cancellationToken)
                    .ConfigureAwait(false);

                _announcements++;
            }

            if (attempt < AnnouncementCount - 1 && delayBetween > TimeSpan.Zero)
            {
                await Task.Delay(delayBetween, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles one received datagram. Returns true when an answer was sent.
    /// </summary>
    /// <remarks>
    /// Separated from the receive loop so the decisions - answer or ignore,
    /// unicast or multicast, which records - can be tested directly, with no
    /// socket and no timing.
    /// </remarks>
    [Requirement("REQ-ADV-012",
        "Answers only when a record in the advertisement is about exactly the name asked for; every other query is counted and ignored.")]
    [Requirement("REQ-ADV-011",
        "Ignores any datagram that arrived on an interface the service is not configured for.")]
    [Requirement("REQ-ADV-017",
        "Answers a legacy unicast querier by unicast, echoing its query identifier and capping TTLs.")]
    [Requirement("REQ-SEC-001",
        "Every record sent comes from this responder's own Advertisement. Received datagrams are read for their questions only; no byte of a received packet is ever re-emitted, so nothing is forwarded or reflected between networks.")]
    [Requirement("REQ-SEC-002",
        "Answers are drawn only from the advertisement, which contains printing service types alone. A service seen on the printer network cannot become answerable on the client network, because seeing it changes nothing about what this responder holds.")]
    public async Task<bool> HandleAsync(MdnsDatagram datagram, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        if (datagram.ArrivedOn is null || !_byIndex.TryGetValue(datagram.InterfaceIndex, out AdvertisedInterface? entry))
        {
            _ignoredWrongInterface++;
            return false;
        }

        DnsMessage query;
        try
        {
            query = DnsMessage.Parse(datagram.Payload, datagram.Payload.Length);
        }
        catch (InvalidDataException)
        {
            _unparseable++;
            return false;
        }

        if (query.IsResponse)
        {
            // Somebody else's answer, or our own multicast looping back.
            return false;
        }

        _queriesSeen++;

        bool legacyUnicast = datagram.IsLegacyUnicastQuerier;
        var builder = new DnsResponseBuilder(legacyUnicast ? query.Id : (ushort)0);
        var addedAnswers = new HashSet<string>(StringComparer.Ordinal);
        var addedAdditionals = new HashSet<string>(StringComparer.Ordinal);

        foreach ((DnsName name, DnsRecordType type, _) in query.Questions)
        {
            List<OutgoingRecord> answers = MatchingRecords(entry.Advertisement, name, type);
            if (answers.Count == 0)
            {
                continue;
            }

            if (legacyUnicast)
            {
                builder.AddQuestion(name, type);
            }

            foreach (OutgoingRecord record in answers)
            {
                if (addedAnswers.Add(Key(record)))
                {
                    builder.AddAnswer(Adjust(record, legacyUnicast));
                }
            }

            foreach (OutgoingRecord extra in AdditionalsFor(entry.Advertisement, answers))
            {
                if (!addedAnswers.Contains(Key(extra)) && addedAdditionals.Add(Key(extra)))
                {
                    builder.AddAdditional(Adjust(extra, legacyUnicast));
                }
            }
        }

        if (builder.AnswerCount == 0)
        {
            _ignoredNotOurs++;
            return false;
        }

        byte[] response = builder.Build();

        if (legacyUnicast)
        {
            await _transport
                .SendUnicastAsync(response, datagram.Source, entry.Interface, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _transport
                .SendMulticastAsync(response, entry.Interface, cancellationToken)
                .ConfigureAwait(false);
        }

        _queriesAnswered++;
        return true;
    }

    /// <summary>Receives and handles datagrams until cancelled.</summary>
    [Requirement("REQ-LIF-005",
        "A transient socket error is counted and the loop continues; only cancellation ends it.")]
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            MdnsDatagram datagram;
            try
            {
                datagram = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Transient network trouble - an adapter flapping, a buffer
                // overrun. Losing one datagram is not a reason to stop serving.
                _unparseable++;
                continue;
            }

            await HandleAsync(datagram, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retracts the advertisement by sending every record with TTL 0, so clients
    /// drop the printer immediately rather than waiting for it to expire.
    /// </summary>
    [Requirement("REQ-LIF-003",
        "Sends goodbye records for everything advertised, on every advertised interface, at shutdown.")]
    public async Task SendGoodbyeAsync(CancellationToken cancellationToken)
    {
        foreach (AdvertisedInterface entry in _advertised)
        {
            var builder = new DnsResponseBuilder();
            foreach (OutgoingRecord record in AdvertisementBuilder.ToGoodbye(entry.Advertisement.Records))
            {
                builder.AddAnswer(record);
            }

            await _transport
                .SendMulticastAsync(builder.Build(), entry.Interface, cancellationToken)
                .ConfigureAwait(false);

            _goodbyes++;
        }
    }

    /// <summary>
    /// Records in the advertisement that answer a question, matched on exact
    /// name and type. This is the only place a query becomes an answer, and it
    /// consults nothing but the advertisement.
    /// </summary>
    private static List<OutgoingRecord> MatchingRecords(
        Advertisement advertisement, DnsName question, DnsRecordType type)
    {
        var matches = new List<OutgoingRecord>();

        foreach (OutgoingRecord record in advertisement.Records)
        {
            if (!record.Name.Equals(question))
            {
                continue;
            }

            if (type == DnsRecordType.Any || record.Type == type)
            {
                matches.Add(record);
            }
        }

        return matches;
    }

    /// <summary>
    /// Records the querier will need next: SRV, TXT and A behind a PTR answer,
    /// and A behind an SRV answer. Supplying them saves the client further round
    /// trips, as RFC 6763 §12 recommends.
    /// </summary>
    private static List<OutgoingRecord> AdditionalsFor(
        Advertisement advertisement, List<OutgoingRecord> answers)
    {
        var additionals = new List<OutgoingRecord>();

        foreach (OutgoingRecord answer in answers)
        {
            switch (answer.Payload)
            {
                case PtrPayload ptr:
                    additionals.AddRange(advertisement.Records.Where(
                        r => r.Name.Equals(ptr.Target)
                             && r.Type is DnsRecordType.Srv or DnsRecordType.Txt));

                    foreach (OutgoingRecord srv in advertisement.Records.Where(
                        r => r.Name.Equals(ptr.Target) && r.Type == DnsRecordType.Srv))
                    {
                        additionals.AddRange(AddressRecordsFor(advertisement, srv));
                    }

                    break;

                case SrvPayload:
                    additionals.AddRange(AddressRecordsFor(advertisement, answer));
                    break;
            }
        }

        return additionals;
    }

    private static IEnumerable<OutgoingRecord> AddressRecordsFor(
        Advertisement advertisement, OutgoingRecord srvRecord) =>
        srvRecord.Payload is SrvPayload srv
            ? advertisement.Records.Where(
                r => r.Name.Equals(srv.Target) && r.Type is DnsRecordType.A or DnsRecordType.Aaaa)
            : [];

    /// <summary>Caps TTLs for legacy unicast answers; leaves multicast answers as built.</summary>
    private static OutgoingRecord Adjust(OutgoingRecord record, bool legacyUnicast) =>
        legacyUnicast
            ? record with { Ttl = Math.Min(record.Ttl, LegacyUnicastTtl), CacheFlush = false }
            : record;

    private static string Key(OutgoingRecord record) => $"{record.Name}|{record.Type}";
}

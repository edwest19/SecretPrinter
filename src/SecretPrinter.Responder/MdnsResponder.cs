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
// Per-family index claim corrected by Claude (Anthropic model, Claude Opus 5)
// at the direction of Edwin West, 2026-09-13. Comment only; no behaviour
// changed. Reviewed by a human before merge.
//
// Answering keyed by address family, with an explicit IPv6 companion, by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-14.
// Reviewed by a human before merge.
//
// Query and answer counts broken down by transport by Claude (Anthropic model,
// Claude Opus 5) at the direction of Edwin West, 2026-09-14, for REQ-OBS-007.
// Reviewed by a human before merge.
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
using System.Net.Sockets;
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
/// <param name="Interface">
/// The IPv4 entry for the adapter. Announcements and goodbyes go out by this
/// one and only this one.
/// </param>
/// <param name="Advertisement">
/// What to publish here. Shared with <paramref name="IPv6Interface"/> on
/// purpose: the same records, delivered over whichever transport the query
/// arrived on, which is what REQ-ADV-018 asks for. The A record carries the
/// adapter's IPv4 address in both cases, and REQ-ADV-021 forbids an AAAA.
/// </param>
/// <param name="IPv6Interface">
/// The adapter's IPv6 entry, when this interface also answers over IPv6, or
/// null when it does not.
///
/// Stated explicitly rather than inferred by matching addresses at answer time.
/// The pairing is decided once, by the caller that knows which interface plays
/// which role, and is then checked in the constructor - so an answer cannot go
/// out over a transport nobody chose.
/// </param>
public sealed record AdvertisedInterface(
    MdnsInterface Interface,
    Advertisement Advertisement,
    MdnsInterface? IPv6Interface = null);

/// <summary>What a responder did, for logging and for tests.</summary>
/// <remarks>
/// Queries seen and answered are held per transport rather than as totals.
/// A single pair of totals cannot answer the question the IPv6 work exists to
/// settle - whether anything was actually served over IPv6 - because a client
/// that discovers the proxy over IPv4 produces an identical count. The totals
/// remain available below, computed from the parts, so they cannot drift.
/// </remarks>
public sealed record ResponderActivity(
    int AnnouncementsSent,
    int QueriesSeenOverIPv4,
    int QueriesSeenOverIPv6,
    int QueriesAnsweredOverIPv4,
    int QueriesAnsweredOverIPv6,
    int IgnoredWrongInterface,
    int IgnoredNotOurs,
    int Unparseable,
    int GoodbyesSent)
{
    /// <summary>Queries seen over either transport.</summary>
    public int QueriesSeen => QueriesSeenOverIPv4 + QueriesSeenOverIPv6;

    /// <summary>Queries answered over either transport.</summary>
    public int QueriesAnswered => QueriesAnsweredOverIPv4 + QueriesAnsweredOverIPv6;

    /// <summary>
    /// One line naming both transports and their counts, for the operator.
    /// </summary>
    /// <remarks>
    /// Both transports are always named, including when a count is zero. A line
    /// that mentioned IPv6 only when it had been used would leave silence and
    /// absence looking the same, which is the ambiguity this exists to remove.
    /// </remarks>
    [Requirement("REQ-OBS-007",
        "Reports queries seen and answered for each transport by name, including zeroes, so IPv6 having served nothing is distinguishable from IPv6 not being reported.")]
    public string DescribeByTransport() =>
        $"By transport: IPv4 answered {QueriesAnsweredOverIPv4} of {QueriesSeenOverIPv4} seen; "
        + $"IPv6 answered {QueriesAnsweredOverIPv6} of {QueriesSeenOverIPv6} seen.";
}

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

    // What to answer with, and which interface to answer through, for a query
    // that arrived on a given family and index.
    //
    // Keyed by family AND index. The platform numbers interfaces per address
    // family and guarantees no relationship between the two numbers, and on
    // every machine this project has measured they happen to be equal - so an
    // index-only key would find the IPv4 entry for an IPv6 arrival, HIT, and
    // answer over IPv4. That would look exactly like working IPv6 receive while
    // being an IPv4 answer to a question asked over IPv6. See
    // docs/findings/2026-09-13-interface-index-parity.md.
    //
    // Separate from _advertised on purpose. _advertised stays the source for
    // AnnounceAsync and SendGoodbyeAsync, which iterate entry.Interface only.
    // Adding IPv6 entries there would silently start announcing and sending
    // goodbyes over IPv6 - a change to REQ-ADV-001, REQ-ADV-002 and REQ-LIF-003,
    // all already marked covered, smuggled into a commit scoped to REQ-ADV-018
    // and REQ-ADV-020. IPv6 announcements may well be worth having; that is a
    // separate, measured decision.
    private readonly Dictionary<(AddressFamily Family, int Index), AnsweringInterface> _answering;

    private int _announcements;
    private int _queriesSeenOverIPv4;
    private int _queriesSeenOverIPv6;
    private int _queriesAnsweredOverIPv4;
    private int _queriesAnsweredOverIPv6;
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
            // once per address family, each carrying the index the platform
            // reports for that family, with no guarantee that the two values
            // relate. See docs/findings/2026-09-13-interface-index-parity.md.
            if (!transport.Interfaces.Any(i => i.Matches(entry.Interface)))
            {
                throw new ArgumentException(
                    $"Advertisement is for {entry.Interface}, which the transport does not hold. "
                    + "Advertising on an interface the socket cannot send from would silently produce "
                    + "nothing.",
                    nameof(advertised));
            }

            if (entry.IPv6Interface is not { } companion)
            {
                continue;
            }

            if (!transport.Interfaces.Any(i => i.Matches(companion)))
            {
                throw new ArgumentException(
                    $"IPv6 companion {companion} is not held by the transport. Answering over it "
                    + "would be refused by the socket at the moment a query arrived, which is a "
                    + "worse place to find out than startup.",
                    nameof(advertised));
            }

            if (!companion.IsIPv6)
            {
                throw new ArgumentException(
                    $"IPv6 companion {companion} is not an IPv6 entry. Keying it by its stated "
                    + "family would file an IPv4 interface under IPv6 and answer IPv6 queries "
                    + "over IPv4.",
                    nameof(advertised));
            }

            if (!companion.Address.Equals(entry.Interface.Address))
            {
                // MdnsInterfaceResolver.ResolveIPv6 builds the companion with the
                // adapter's IPv4 address and its IPv6 index - deliberately, since
                // the transport decides how a datagram travels and the A record
                // decides what it says. Asserted here, at the point that invariant
                // is relied on, rather than trusted silently across two projects.
                throw new ArgumentException(
                    $"IPv6 companion {companion} does not carry the same address as "
                    + $"{entry.Interface}. They must describe one adapter: the shared advertisement "
                    + "publishes that address in its A record whichever transport answers.",
                    nameof(advertised));
            }
        }

        _transport = transport;
        _advertised = advertised;
        _answering = BuildAnswering(advertised);
    }

    /// <summary>What to answer with, and which interface to answer through.</summary>
    private sealed record AnsweringInterface(Advertisement Advertisement, MdnsInterface Via);

    /// <summary>
    /// Builds the family-keyed answering table: the IPv4 entry answers through
    /// itself, and its IPv6 companion answers through the companion, both from
    /// the same advertisement.
    /// </summary>
    /// <remarks>
    /// An explicit loop rather than ToDictionary, whose ArgumentException on a
    /// duplicate key names neither the key nor the entries that collided. A
    /// collision here means two interfaces were configured with the same family
    /// and index, which is worth saying out loud.
    /// </remarks>
    private static Dictionary<(AddressFamily, int), AnsweringInterface> BuildAnswering(
        IReadOnlyList<AdvertisedInterface> advertised)
    {
        var answering = new Dictionary<(AddressFamily, int), AnsweringInterface>();

        foreach (AdvertisedInterface entry in advertised)
        {
            Add(answering, entry.Interface, entry.Advertisement);

            if (entry.IPv6Interface is { } companion)
            {
                Add(answering, companion, entry.Advertisement);
            }
        }

        return answering;

        static void Add(
            Dictionary<(AddressFamily, int), AnsweringInterface> into,
            MdnsInterface via,
            Advertisement advertisement)
        {
            if (!into.TryAdd((via.Transport, via.Index), new AnsweringInterface(advertisement, via)))
            {
                throw new ArgumentException(
                    $"Two advertised interfaces both claim {via}. An arriving query could not be "
                    + "attributed to one of them, so the answer would depend on ordering.",
                    nameof(advertised));
            }
        }
    }

    public ResponderActivity Activity => new(
        _announcements,
        _queriesSeenOverIPv4, _queriesSeenOverIPv6,
        _queriesAnsweredOverIPv4, _queriesAnsweredOverIPv6,
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
    [Requirement("REQ-ADV-018",
        "Answers over the transport the query arrived on: the advertisement is looked up by the arrival interface's address family as well as its index, and the answer is sent through the entry for that family. The receiving half of this requirement is MdnsSocket.ReceiveAsync.")]
    [Requirement("REQ-SEC-002",
        "Answers are drawn only from the advertisement, which contains printing service types alone. A service seen on the printer network cannot become answerable on the client network, because seeing it changes nothing about what this responder holds.")]
    [Requirement("REQ-OBS-007",
        "Counts a query against the transport it arrived on and an answer against the transport it left on, so the two can be compared rather than one being inferred from the other.")]
    public async Task<bool> HandleAsync(MdnsDatagram datagram, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        // Keyed off ArrivedOn, which already carries both the transport and the
        // index the socket attributed the datagram to, rather than off
        // InterfaceIndex alone. One lookup replaces a two-part check and cannot
        // lose the address family on the way.
        if (datagram.ArrivedOn is not { } arrivedOn
            || !_answering.TryGetValue((arrivedOn.Transport, arrivedOn.Index), out AnsweringInterface? entry))
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

        // Counted against the transport the query ARRIVED on. The answer is
        // counted separately, against the transport it LEFT on, further down.
        // Those are two different claims: one says IPv6 receive is working, the
        // other says an IPv6 answer actually went out. Deriving either from the
        // other would make the pair unable to disagree, and disagreeing is
        // exactly what they would need to do if the family keying were wrong.
        if (arrivedOn.IsIPv6)
        {
            _queriesSeenOverIPv6++;
        }
        else
        {
            _queriesSeenOverIPv4++;
        }

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
                .SendUnicastAsync(response, datagram.Source, entry.Via, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _transport
                .SendMulticastAsync(response, entry.Via, cancellationToken)
                .ConfigureAwait(false);
        }

        if (entry.Via.IsIPv6)
        {
            _queriesAnsweredOverIPv6++;
        }
        else
        {
            _queriesAnsweredOverIPv4++;
        }

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

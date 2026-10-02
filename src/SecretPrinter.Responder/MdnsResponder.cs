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
// Made partial, with probing in MdnsResponder.Probing.cs, and HandleAsync
// changed to read responses and other devices' probes for conflicts even while
// nothing is on offer, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-28, for REQ-ADV-023 and REQ-ADV-024. The
// REQ-SEC-001 note on HandleAsync corrected to match: it said received
// datagrams were read for their questions only. Reviewed by a human before
// merge.
//
// Once a conflict is held, answering and announcing stop, and the service is
// told through the new constructor parameter 'conflicted', by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-28, for REQ-ADV-024. Goodbyes are unaffected. Reviewed by a human
// before merge.
//
// Silent while probing, and the REQ-ADV-023 and REQ-ADV-024 markers placed now
// that the service wiring makes both requirements fully met, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-28. Reviewed by a human before merge.
//
// Records told apart by their data as well as their name and type, and an
// address answer given the host's other address type as additionals, by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-30. Before this, two AAAA records for the host - two link-local
// addresses - counted as one, and every answer would have dropped the second.
// Nothing published two records of one name and type until then, so nothing
// was lost in practice. Reviewed by a human before merge.
//
// Each question's class read, by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-10-01, for REQ-ADV-025: a question is
// answered only in class IN or ANY, and a legacy unicast answer repeats it in
// the class it was asked in. Before, the class was discarded as the questions
// were read, so a question in any class was answered with class IN records and
// echoed as class IN. See
// docs/findings/2026-10-01-the-responder-ignored-the-question-class.md.
// Reviewed by a human before merge.
//
// Every question answered before any additional is chosen, so a record that
// answers one question is never also sent as an additional, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-01. Before, the questions were taken one at a time, and a record
// that answered a later question had already gone into the Additional
// section for an earlier one. See
// docs/findings/2026-10-01-a-record-was-sent-as-an-answer-and-an-additional.md.
// Reviewed by a human before merge.
//
// NSEC records sent, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-01, for REQ-ADV-022: as the answer to a
// question for a type that one of the claimed names does not have (RFC 6762
// s6.1), as an additional beside an address record when the host has no
// address of the other family (s6.2), and in the goodbye. One is built for each
// claimed name when the responder is constructed. Before this, such a question
// was not answered, and no NSEC was sent. The REQ-SEC-001 note on HandleAsync
// and the REQ-LIF-003 note on SendGoodbyeAsync changed to say so. The method
// that builds the records, NsecRecordsFor, is public so that the service logs
// the same records at startup (REQ-OBS-003). No REQ-ADV-022 marker is placed:
// at the time of this change the behaviour had not been captured on hardware.
// Reviewed by a human before merge.
//
// REQ-ADV-022 markers placed by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-02, on NsecRecordsFor, HandleAsync,
// SendGoodbyeAsync and AdditionalsFor. A capture on FIOS-STB-01 that day shows
// an iPhone's type 65 question for the host name answered with the NSEC, and a
// page printed 63 seconds later. See
// docs/findings/2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md.
// No code changed. Reviewed by a human before merge.
//
// Purpose:
//   The loop that joins the two halves the service already has:
//   AdvertisementBuilder decides WHAT to publish, MdnsSocket moves the bytes,
//   and this decides WHEN and TO WHOM.
//
//   It announces on startup, answers queries for names it owns, ignores
//   everything else, and retracts its advertisement on shutdown.
//
//   For the names it claims as its own alone, its host name and its service
//   instance name, it also says what they do not have. Asked for a type such a
//   name lacks, it answers with an NSEC record listing the types the name does
//   have (RFC 6762 s6.1). The NSEC records are not in the Advertisement. They
//   are built from it, once, when the responder is constructed
//   (NsecRecordsFor), and they say nothing the Advertisement does not.
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
/// adapter's IPv4 address in both cases. AAAA records, when the advertisement
/// holds any, carry the adapter's link-local addresses, which the relay listens
/// on (REQ-ADV-021); the responder sends them as it sends every other record.
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
public sealed partial class MdnsResponder
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

    /// <summary>
    /// TTL, in seconds, of the NSEC record for a host name: a claimed name that
    /// has an address record. RFC 6762 s6.1 says an NSEC SHOULD carry the TTL
    /// the missing record would have had, and s10 recommends 120 seconds for a
    /// record whose name is a host name.
    /// </summary>
    public const uint HostNameNsecTtl = 120;

    /// <summary>
    /// TTL, in seconds, of the NSEC record for any other claimed name, which
    /// here is the service instance name. RFC 6762 s10 recommends 120 seconds
    /// for a record whose name or data holds a host name and 75 minutes for
    /// other records. What the data of a record that does not exist would hold
    /// cannot be known, so the value for other records is used.
    /// </summary>
    public const uint OtherNameNsecTtl = 4500;

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
    private readonly Func<bool> _advertising;
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

    /// <param name="advertising">
    /// Asked before every query is answered. While it returns false the
    /// responder answers nothing, because the service has withdrawn the
    /// advertisement (REQ-LIF-006) and answering would re-publish a printer it
    /// has established it cannot reach. Defaults to always advertising.
    /// </param>
    /// <param name="conflicted">
    /// Called once, with the first conflict heard for a name this responder
    /// claims (README REQ-ADV-024), whether heard while probing or after
    /// announcing. Called on the receive path, so it must return promptly and
    /// must not throw. The responder does not wait for it: from the moment the
    /// conflict is recorded it answers and announces nothing by itself.
    /// </param>
    public MdnsResponder(
        IMdnsTransport transport,
        IReadOnlyList<AdvertisedInterface> advertised,
        Func<bool>? advertising = null,
        Action<NameConflict>? conflicted = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(advertised);

        _advertising = advertising ?? (static () => true);
        _conflicted = conflicted;

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

    /// <summary>
    /// What to answer with, which interface to answer through, and the NSEC
    /// records that say which types the advertisement's claimed names have.
    /// </summary>
    private sealed record AnsweringInterface(
        Advertisement Advertisement, MdnsInterface Via, IReadOnlyList<OutgoingRecord> Nsecs);

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
    ///
    /// The NSEC records are built here, once for each advertisement, and shared
    /// by its IPv4 entry and its IPv6 companion, as the advertisement is. An
    /// advertisement no NSEC can be built for is therefore refused when the
    /// responder is constructed, at startup, and not when the first query for a
    /// missing type arrives.
    /// </remarks>
    private static Dictionary<(AddressFamily, int), AnsweringInterface> BuildAnswering(
        IReadOnlyList<AdvertisedInterface> advertised)
    {
        var answering = new Dictionary<(AddressFamily, int), AnsweringInterface>();

        foreach (AdvertisedInterface entry in advertised)
        {
            IReadOnlyList<OutgoingRecord> nsecs = NsecRecordsFor(entry.Advertisement);

            Add(answering, entry.Interface, entry.Advertisement, nsecs);

            if (entry.IPv6Interface is { } companion)
            {
                Add(answering, companion, entry.Advertisement, nsecs);
            }
        }

        return answering;

        static void Add(
            Dictionary<(AddressFamily, int), AnsweringInterface> into,
            MdnsInterface via,
            Advertisement advertisement,
            IReadOnlyList<OutgoingRecord> nsecs)
        {
            if (!into.TryAdd((via.Transport, via.Index), new AnsweringInterface(advertisement, via, nsecs)))
            {
                throw new ArgumentException(
                    $"Two advertised interfaces both claim {via}. An arriving query could not be "
                    + "attributed to one of them, so the answer would depend on ordering.",
                    nameof(advertised));
            }
        }
    }

    /// <summary>
    /// One NSEC record for each name the advertisement claims, listing the types
    /// of the records it claims there. It is what this responder sends to say
    /// that a claimed name has no record of some other type (RFC 6762 s6.1).
    /// </summary>
    /// <remarks>
    /// Only claimed names get one. The service probes for them with type ANY
    /// (<see cref="ProbeAsync"/>), which is what entitles it to say that a type
    /// does not exist there (s6.1). The shared names, the service types, get
    /// none: other responders hold records for them too, and s6 forbids a
    /// negative answer for a shared record.
    ///
    /// The types come from <see cref="TypesAt"/>, the same list a heard NSEC is
    /// judged against (<see cref="NoteConflicts"/>), so this responder's own
    /// NSEC, heard back, agrees with it by construction.
    ///
    /// The cache-flush bit is set, as on every record of a claimed name
    /// (s10.2). <see cref="Adjust"/> clears it for a legacy unicast answer.
    ///
    /// Each record's data is encoded once here, so a list of types the writer
    /// refuses is found now. The writer accepts types 1 to 127 only: the
    /// restricted form cannot list a type above 255, and s6.1 forbids sending
    /// it for a name that has one.
    ///
    /// Public so that the service can log, at startup, exactly the records
    /// built here (AdvertisementLog; README REQ-OBS-003). The responder's
    /// constructor calls this same method, so the log and the wire cannot
    /// differ.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The advertisement holds a shared record at a name it also claims, or
    /// claims a type an NSEC record in the restricted form cannot list.
    /// </exception>
    [Requirement("REQ-ADV-022",
        "Builds the NSEC record for each name the advertisement claims. It lists the types of the records claimed at that name, in the restricted form of RFC 6762 s6.1, with the cache-flush bit set and a TTL of 120 s for a host name and 4,500 s for any other name. A shared name gets none. An advertisement whose NSEC would say something false is refused.")]
    public static IReadOnlyList<OutgoingRecord> NsecRecordsFor(Advertisement advertisement)
    {
        ArgumentNullException.ThrowIfNull(advertisement);

        var nsecs = new List<OutgoingRecord>();
        List<OutgoingRecord> claimed = Claimed(advertisement);

        foreach (DnsName name in claimed.Select(record => record.Name).Distinct())
        {
            // A claimed name is claimed for every type. A record at it that is
            // not marked unique would be left out of the list below, and the
            // NSEC would then deny a record this responder publishes.
            if (advertisement.Records.FirstOrDefault(
                    record => record.Name.Equals(name) && !record.CacheFlush) is { } shared)
            {
                throw new ArgumentException(
                    $"The advertisement holds a shared {shared.Type} record at {name}, a name it also claims as "
                    + "unique. An NSEC record for that name lists the claimed types only, and would deny the "
                    + "shared one.",
                    nameof(advertisement));
            }

            List<OutgoingRecord> atName = [.. claimed.Where(record => record.Name.Equals(name))];

            // RFC 6762 s2: a host name is a name that has an address record.
            bool isHostName = atName.Any(record => record.Type is DnsRecordType.A or DnsRecordType.Aaaa);

            var nsec = new OutgoingRecord(
                name,
                DnsRecordType.Nsec,
                isHostName ? HostNameNsecTtl : OtherNameNsecTtl,
                CacheFlush: true,
                new NsecPayload(name, TypesAt(atName)));

            try
            {
                _ = DnsRecordWriter.EncodeRdata(nsec.Type, nsec.Payload);
            }
            catch (ArgumentException refused)
            {
                throw new ArgumentException(
                    $"No NSEC record can be built for {name}, which the advertisement claims: {refused.Message}",
                    nameof(advertisement),
                    refused);
            }

            nsecs.Add(nsec);
        }

        return nsecs;
    }

    /// <summary>The NSEC record for a name, or null when the name is not a claimed one.</summary>
    private static OutgoingRecord? NsecFor(IReadOnlyList<OutgoingRecord> nsecs, DnsName name) =>
        nsecs.FirstOrDefault(nsec => nsec.Name.Equals(name));

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
    [Requirement("REQ-ADV-024",
        "Announces nothing once a conflict is held, checking before every send, so a conflict heard between two announcements stops the rest.")]
    public async Task AnnounceAsync(TimeSpan delayBetween, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < AnnouncementCount; attempt++)
        {
            foreach (AdvertisedInterface entry in _advertised)
            {
                // Checked before every send, not once at the start: a conflict
                // heard between two announcements stops the rest (REQ-ADV-024).
                if (Conflict is not null)
                {
                    return;
                }

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
        "Every record sent comes from this responder's own Advertisement, or is an NSEC record built from that Advertisement when the responder is constructed, listing the types of the records it claims at one of its own names. Received datagrams are read for their questions and, to detect conflicts with the names this responder claims, for records about those names; no byte of a received packet is ever re-emitted, so nothing is forwarded or reflected between networks.")]
    [Requirement("REQ-ADV-018",
        "Answers over the transport the query arrived on: the advertisement is looked up by the arrival interface's address family as well as its index, and the answer is sent through the entry for that family. The receiving half of this requirement is MdnsSocket.ReceiveAsync.")]
    [Requirement("REQ-SEC-002",
        "Answers are drawn only from the advertisement, which contains printing service types alone. A service seen on the printer network cannot become answerable on the client network, because seeing it changes nothing about what this responder holds.")]
    [Requirement("REQ-OBS-007",
        "Counts a query against the transport it arrived on and an answer against the transport it left on, so the two can be compared rather than one being inferred from the other.")]
    [Requirement("REQ-LIF-006",
        "Answers nothing while the advertisement is withdrawn. A goodbye followed by an answer to the "
        + "next query would re-advertise the printer within milliseconds of retracting it.")]
    [Requirement("REQ-ADV-024",
        "Answers nothing once a conflict is held, whatever the gate says, and reads every response for records about the names this responder claims, even while nothing is on offer.")]
    [Requirement("REQ-ADV-023",
        "Answers nothing while probing: the names are not this responder's until the probe comes back clear.")]
    [Requirement("REQ-ADV-025",
        "Answers a question only when its class, read without the unicast-response bit, is IN or ANY, because every record it sends is in class IN; a legacy unicast answer repeats each question in the class it was asked in.")]
    [Requirement("REQ-ADV-022",
        "A question in class IN or ANY for a type that a claimed name has no record of is answered with that name's NSEC record, in the Answer section. A question for type ANY gets the records at the name and no NSEC. A shared name, or any name this responder does not claim, gets no negative answer. Nothing is answered while probing, while nothing is offered, or after a conflict.")]
    public async Task<bool> HandleAsync(MdnsDatagram datagram, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        // Read once, so everything below sees the same answer. What is heard
        // must be read even while nothing is on offer: probing happens exactly
        // then, before the first announcement and before a restore, and a
        // conflict heard then is the one that matters most (REQ-ADV-023,
        // REQ-ADV-024). Answering still waits for the gate, below.
        bool advertising = _advertising();

        // Keyed off ArrivedOn, which already carries both the transport and the
        // index the socket attributed the datagram to, rather than off
        // InterfaceIndex alone. One lookup replaces a two-part check and cannot
        // lose the address family on the way.
        if (datagram.ArrivedOn is not { } arrivedOn
            || !_answering.TryGetValue((arrivedOn.Transport, arrivedOn.Index), out AnsweringInterface? entry))
        {
            // Counted only while on offer, as before datagrams were read while
            // withdrawn, so the counter means what it meant.
            if (advertising)
            {
                _ignoredWrongInterface++;
            }

            return false;
        }

        DnsMessage query;
        try
        {
            query = DnsMessage.Parse(datagram.Payload, datagram.Payload.Length);
        }
        catch (InvalidDataException)
        {
            // As above: counted only while on offer.
            if (advertising)
            {
                _unparseable++;
            }

            return false;
        }

        if (query.IsResponse)
        {
            // Somebody else's answer, or our own multicast looping back. Never
            // answered and never re-emitted: read only for records about the
            // names this responder claims (REQ-ADV-024).
            NoteConflicts(query, datagram, arrivedOn, entry.Advertisement);
            return false;
        }

        // A query carrying proposed records is a probe. While this responder is
        // probing too, the two may want the same name (RFC 6762 s8.2).
        NoteSimultaneousProbe(query, entry.Advertisement);

        // A conflict silences the responder whatever the gate says: another
        // device holds one of its names, and answering would publish records
        // that contradict that device's (REQ-ADV-024). So does probing: the
        // names are not this responder's until the probe comes back clear
        // (REQ-ADV-023), and the gate alone should not be what ensures that.
        if (!advertising || Conflict is not null || Volatile.Read(ref _probing))
        {
            // Not counted as ignored-not-ours: the query was ours to answer, and
            // we chose not to. The withdrawal itself is logged once, by the
            // service, rather than once per query here.
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

        // Every question is answered first, and only then are the additionals
        // chosen. Taken a question at a time, a record that answers a later
        // question would already have gone into the Additional section for an
        // earlier one, and would be sent twice.
        var answered = new List<OutgoingRecord>();
        var answeredKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach ((DnsName name, DnsRecordType type, ushort rawClass) in query.Questions)
        {
            // A question in a class other than IN or ANY asks about records this
            // responder does not have, whatever their name and type (REQ-ADV-025).
            if (!AsksAboutClassIn(rawClass))
            {
                continue;
            }

            List<OutgoingRecord> answers = MatchingRecords(entry.Advertisement, name, type);
            if (answers.Count == 0)
            {
                // No record of that type at that name. When the name is one
                // this responder claims for every type, the answer is its NSEC
                // record, which lists the types the name does have (RFC 6762
                // s6.1). That includes a question for type NSEC itself. For any
                // other name, the shared service types included, there is
                // nothing to say (s6). A question for type ANY never comes
                // here for a claimed name, because a claimed name has records.
                if (NsecFor(entry.Nsecs, name) is not { } nsec)
                {
                    continue;
                }

                answers = [nsec];
            }

            if (legacyUnicast)
            {
                // Repeated as asked, class included (RFC 6762 s6.7).
                builder.AddQuestion(name, type, rawClass);
            }

            foreach (OutgoingRecord record in answers)
            {
                if (answeredKeys.Add(Key(record)))
                {
                    answered.Add(record);
                    builder.AddAnswer(Adjust(record, legacyUnicast));
                }
            }
        }

        var addedAdditionals = new HashSet<string>(StringComparer.Ordinal);
        foreach (OutgoingRecord extra in AdditionalsFor(entry.Advertisement, entry.Nsecs, answered))
        {
            if (!answeredKeys.Contains(Key(extra)) && addedAdditionals.Add(Key(extra)))
            {
                builder.AddAdditional(Adjust(extra, legacyUnicast));
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
    /// <remarks>
    /// The NSEC records for the advertisement's claimed names are retracted
    /// with it (RFC 6762 s10.1), whether or not one was ever sent. A client
    /// that was told the host has no record of some type would otherwise keep
    /// that for up to the NSEC's TTL after the names were given up.
    /// </remarks>
    [Requirement("REQ-LIF-003",
        "Sends goodbye records for everything advertised, and for the NSEC records built from it, on every advertised interface, at shutdown.")]
    [Requirement("REQ-ADV-022",
        "The goodbye retracts both NSEC records, with TTL 0.")]
    public async Task SendGoodbyeAsync(CancellationToken cancellationToken)
    {
        foreach (AdvertisedInterface entry in _advertised)
        {
            // BuildAnswering files every advertised interface under its own
            // family and index, so this lookup cannot miss.
            IReadOnlyList<OutgoingRecord> nsecs =
                _answering[(entry.Interface.Transport, entry.Interface.Index)].Nsecs;

            var builder = new DnsResponseBuilder();
            foreach (OutgoingRecord record in
                     AdvertisementBuilder.ToGoodbye([.. entry.Advertisement.Records, .. nsecs]))
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
    /// consults nothing but the advertisement. The question's class is checked
    /// before this is called (<see cref="AsksAboutClassIn"/>).
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
    /// Records the querier will need next: SRV, TXT and the host's address
    /// records behind a PTR answer; the address records behind an SRV answer;
    /// and behind an address answer, the host's address records of the other
    /// type. Supplying them saves the client further round trips, as RFC 6763
    /// §12 recommends, and the last is what RFC 6762 §6.2 asks for, so that a
    /// lost packet cannot leave a client holding one address type and not the
    /// other.
    /// </summary>
    /// <remarks>
    /// Last comes the NSEC record of a host whose address record is in the
    /// response, as an answer or as one of the additionals above, when the
    /// advertisement holds no address record of the other type for that host.
    /// RFC 6762 s6.2 says it SHOULD be there, so that the querier knows the
    /// host has no address of that kind and does not wait for one.
    ///
    /// s6.2 also says a responder that treats an adapter's IPv4 and IPv6 sides
    /// as two interfaces MUST NOT send such an NSEC. This responder treats them
    /// as one: one advertisement serves both transports, and the same records
    /// are sent over either (REQ-ADV-018).
    ///
    /// s6.1 says a responder MAY add an NSEC beside any unique answer, to say
    /// what else the name lacks. This one does not; it adds the one s6.2 asks
    /// for and no other.
    ///
    /// The caller leaves out anything listed here that is already an answer,
    /// or listed twice.
    /// </remarks>
    [Requirement("REQ-ADV-022",
        "Adds the host's NSEC record as an additional when the response carries an address record of the host and the advertisement holds no address record of the other type (RFC 6762 s6.2). Adds no NSEC beside any other answer.")]
    private static List<OutgoingRecord> AdditionalsFor(
        Advertisement advertisement, IReadOnlyList<OutgoingRecord> nsecs, List<OutgoingRecord> answers)
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

                case AddressPayload:
                    additionals.AddRange(advertisement.Records.Where(
                        r => r.Name.Equals(answer.Name)
                             && r.Type is DnsRecordType.A or DnsRecordType.Aaaa
                             && r.Type != answer.Type));
                    break;
            }
        }

        // The remarks above: the NSEC of a host with addresses of one type only.
        foreach (OutgoingRecord address in
                 answers.Concat(additionals).Where(r => r.Payload is AddressPayload).ToList())
        {
            DnsRecordType other = address.Type == DnsRecordType.A ? DnsRecordType.Aaaa : DnsRecordType.A;

            if (!advertisement.Records.Any(r => r.Name.Equals(address.Name) && r.Type == other)
                && NsecFor(nsecs, address.Name) is { } nsec)
            {
                additionals.Add(nsec);
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

    /// <summary>The DNS class every record this responder sends is in: IN.</summary>
    private const int ClassIn = 1;

    /// <summary>The class a question uses to ask about every class.</summary>
    private const int ClassAny = 255;

    /// <summary>
    /// True when a question asks about class IN, or about every class (ANY).
    /// Every record this responder sends is in class IN, because
    /// DnsRecordWriter writes no other, and RFC 6762 s6 lets a record answer a
    /// question only when their classes agree or the question asks for ANY.
    /// The top bit of the field asks for a unicast answer (RFC 6762 s5.4) and
    /// is not part of the class, so it is masked off (REQ-ADV-025).
    /// </summary>
    private static bool AsksAboutClassIn(ushort rawClass) =>
        (rawClass & 0x7FFF) is ClassIn or ClassAny;

    /// <summary>Caps TTLs for legacy unicast answers; leaves multicast answers as built.</summary>
    private static OutgoingRecord Adjust(OutgoingRecord record, bool legacyUnicast) =>
        legacyUnicast
            ? record with { Ttl = Math.Min(record.Ttl, LegacyUnicastTtl), CacheFlush = false }
            : record;

    /// <summary>
    /// What makes two records the same record: name, type and data (RFC 6762
    /// treats records that differ in data as different records). Name and type
    /// alone would make two AAAA records for the host - two link-local
    /// addresses - count as one, and the second would never be sent.
    /// </summary>
    private static string Key(OutgoingRecord record) =>
        $"{record.Name}|{record.Type}|{Convert.ToHexString(DnsRecordWriter.EncodeRdata(record.Type, record.Payload))}";
}

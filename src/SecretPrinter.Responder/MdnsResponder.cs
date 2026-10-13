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
// The REQ-SEC-001 note on HandleAsync corrected by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-10-07. It said "no byte
// of a received packet is ever re-emitted". A legacy unicast answer repeats
// the query's identifier and its question, as RFC 6762 s6.7 requires and as
// this method has done since REQ-ADV-017 was met; the note now says so. The
// text of one marker changed and no statement that runs. See
// docs/findings/2026-10-07-the-readme-was-read-against-the-code.md. Reviewed
// by a human before merge.
//
// A socket error while answering no longer ends the receive loop, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-07, for REQ-LIF-005. ServeAsync guarded the receive and not the
// answer, so a send that failed ended the loop, and the service then answered
// no query until it was restarted, with nothing in its log. That was recorded
// on 2026-09-18 as reasoned and not observed, and it still is not observed.
// A socket error in either half is now also reported to the service, once for
// each different error, through the new constructor parameter socketError.
// See docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.
// Reviewed by a human before merge.
//
// A query's Answer section read, and a record the query already carries left
// out of the answer, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-10, for REQ-ADV-026 (RFC 6762 s7.1,
// known-answer suppression). Before, a query's Answer section was never read,
// and every question was answered whatever the querier said it already held. A
// query whose every answer is left out sends nothing and is counted in
// ResponderActivity.SuppressedByKnownAnswers, not under IgnoredNotOurs, and the
// first line of the service's stop summary, written until then in
// ServiceHost.cs, moved here as ResponderActivity.DescribeServed so that the
// count it now gives can be tested. The REQ-SEC-001 note on HandleAsync now
// says the Answer section is read. See
// docs/findings/2026-10-10-a-question-that-carries-its-answer-is-no-longer-answered.md.
// Reviewed by a human before merge.
//
// A record is no longer multicast on an adapter less than a second after it was
// last multicast there, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-12, for REQ-ADV-027 (RFC 6762 s6). Before,
// the responder kept no record of what it had sent, and answered the same
// question as often as it was asked. Now it keeps, for each adapter, when it
// last multicast each record there. An answer leaves out every record
// multicast there less than a second before, and sends nothing when nothing is
// left; such a query is counted in ResponderActivity.HeldBackWithinASecond and
// in the stop summary's first line. An announcement and a goodbye wait until
// the second is up, and an answer to a probe until 250 ms are, so that each
// still carries every record. A unicast answer to a legacy querier is not a
// multicast and is not affected. The clock and the waits are new constructor
// parameters, so that tests need not wait real seconds; the wait between
// announcements now goes through the same wait. The REQ-SEC-001 note on
// HandleAsync now says that whether a query's Authority section holds a record
// is read too. See
// docs/findings/2026-10-12-a-record-is-multicast-at-most-once-a-second.md.
// Reviewed by a human before merge.
//
// An answer by unicast sent only to an address on the network the query
// arrived from, by Claude (Anthropic model, Claude Opus 5.5) at the direction
// of Edwin West, 2026-10-13, for REQ-SEC-017. Before, an answer to a legacy
// querier went to whatever source address the query gave. The socket sets the
// interface a multicast leaves by and not the one a unicast leaves by, which
// Windows chooses from its routing table, so a forged source address on the
// printer network, or on no local network, would have had the answer sent
// there. Over IPv4 the network is the relay's (ClientNetwork.Of, REQ-SEC-012);
// over IPv6 the address must be link-local with the arrival interface's scope.
// A legacy query from anywhere else is not answered, and is counted in
// ResponderActivity.RefusedOffNetwork and in the stop summary's first line.
// Measured against the code with a fake transport; not seen on a network. See
// docs/findings/2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md.
// Reviewed by a human before merge.
//
// A question that asks for a unicast answer is answered by unicast, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West, later on
// 2026-10-13, for REQ-ADV-028 (RFC 6762 s5.4). Before, the unicast-response
// bit was read only to be set aside from the class, and such a question, from
// port 5353, was answered by multicast. Now a record that only questions with
// the bit ask for goes to the querier by unicast when this adapter multicast it
// less than a quarter of its TTL before, and by multicast otherwise; a probe's
// such records go by unicast at once. The unicast answer is made as the
// multicast one would be, goes only to an address on the network the query
// arrived from (REQ-SEC-017), is neither limited by the one-second rule nor
// recorded as a multicast, and is counted in
// ResponderActivity.AnsweredByUnicastOnRequest and in the stop summary's
// first line. The REQ-SEC-001 note on HandleAsync now says the bit is read.
// See docs/findings/2026-10-13-a-question-asking-for-a-unicast-answer-gets-one.md.
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
///
/// SuppressedByKnownAnswers counts queries that would have been answered and
/// were not, because the query already carried every answer (RFC 6762 s7.1;
/// README REQ-ADV-026). They are counted as seen and not as answered, and not
/// under IgnoredNotOurs either, which is for queries about names this
/// responder does not hold. (Added 2026-10-10 by Claude, Claude Opus 5.5.)
///
/// HeldBackWithinASecond counts queries that would have been answered and
/// were not, because every record the answer would have carried had been
/// multicast on that adapter less than a second before (RFC 6762 s6; README
/// REQ-ADV-027). A query whose every answer it already carried is counted
/// under SuppressedByKnownAnswers instead, because it would have gone
/// unanswered without the one-second rule. (Added 2026-10-12 by Claude, Claude
/// Opus 5.5.)
///
/// RefusedOffNetwork counts queries that would have been answered by unicast
/// and were not, because the address the answer would have gone to is not on
/// the network the query arrived from (README REQ-SEC-017). A query is
/// counted here only when there was an answer to refuse; one about a name this
/// responder does not hold is counted under IgnoredNotOurs whatever its
/// address. (Added 2026-10-13 by Claude, Claude Opus 5.5.)
///
/// AnsweredByUnicastOnRequest counts queries from port 5353 that were sent an
/// answer by unicast because a question in them asked for one (RFC 6762 s5.4;
/// README REQ-ADV-028). Each is also counted as answered. A legacy querier's
/// answer is not counted here: it goes by unicast for another reason
/// (REQ-ADV-017). (Added later on 2026-10-13 by Claude, Claude Opus 5.5.)
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
    int GoodbyesSent,
    int SuppressedByKnownAnswers,
    int HeldBackWithinASecond,
    int RefusedOffNetwork,
    int AnsweredByUnicastOnRequest)
{
    /// <summary>Queries seen over either transport.</summary>
    public int QueriesSeen => QueriesSeenOverIPv4 + QueriesSeenOverIPv6;

    /// <summary>Queries answered over either transport.</summary>
    public int QueriesAnswered => QueriesAnsweredOverIPv4 + QueriesAnsweredOverIPv6;

    /// <summary>
    /// The first line of the service's stop summary: queries answered and seen,
    /// those about names this responder does not hold, those left unanswered
    /// because they carried every answer already, those left unanswered
    /// because the answer would only have repeated records multicast less than
    /// a second before, and those left unanswered because the answer would have
    /// gone by unicast to an address off the network they arrived from; and,
    /// of the queries served, how many were answered by unicast because they
    /// asked for it.
    /// </summary>
    /// <remarks>
    /// Written in ServiceHost.cs until 2026-10-10, without the count of
    /// queries that carried every answer, which was added then (REQ-ADV-026).
    /// It moved here, beside <see cref="DescribeByTransport"/>, so that a test
    /// can read it: no test runs the service's stop path. Each count is always
    /// given, zero included, for the reason given for the transports below.
    /// The count of queries held back within a second was added on 2026-10-12
    /// (REQ-ADV-027), and the count of queries refused for their address on
    /// 2026-10-13 (REQ-SEC-017). The queries answered and the four counts
    /// after them add up to the queries seen, except a query whose answer
    /// could not be sent, which is seen and not answered. (Corrected
    /// 2026-10-13 by Claude, Claude Opus 5.5: this said "without the last
    /// count", "The last count was added on 2026-10-12" and "the three counts
    /// after them", which a fourth count would have made ambiguous.) The count
    /// at the end, added later on 2026-10-13 (REQ-ADV-028), is not one of the
    /// four: it is a part of the queries served.
    /// </remarks>
    [Requirement("REQ-ADV-026",
        "Says at shutdown how many queries were left unanswered because they carried every answer already, apart from those about other names, zero included.")]
    [Requirement("REQ-ADV-027",
        "Says at shutdown how many queries were left unanswered because their answer would only have repeated records multicast less than a second before, apart from the other counts, zero included.")]
    [Requirement("REQ-SEC-017",
        "Says at shutdown how many queries were left unanswered because their answer would have gone by unicast to an address off the network they arrived from, apart from the other counts, zero included.")]
    [Requirement("REQ-ADV-028",
        "Says at shutdown how many of the queries served were answered by unicast because a question asked for it, zero included.")]
    public string DescribeServed() =>
        $"Served {QueriesAnswered} quer(ies) of {QueriesSeen} seen; "
        + $"{IgnoredNotOurs} were for other services and were ignored; "
        + $"{SuppressedByKnownAnswers} carried every answer already and were not answered (RFC 6762 s7.1); "
        + $"{HeldBackWithinASecond} would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); "
        + $"{RefusedOffNetwork} would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); "
        + $"{AnsweredByUnicastOnRequest} of those served were answered by unicast because they asked for it (RFC 6762 s5.4).";

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

    /// <summary>
    /// The least time between two multicasts of one record on one adapter
    /// (RFC 6762 s6; README REQ-ADV-027).
    /// </summary>
    public static readonly TimeSpan MulticastSpacing = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The least time between a multicast of a record and an answer to a probe
    /// that carries it again: RFC 6762 s6 relaxes the second to this "when
    /// responding via multicast to a probe".
    /// </summary>
    public static readonly TimeSpan ProbeAnswerSpacing = TimeSpan.FromMilliseconds(250);

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
    private int _suppressedByKnownAnswers;
    private int _heldBackWithinASecond;
    private int _refusedOffNetwork;
    private int _answeredByUnicastOnRequest;

    // When each record was last multicast on each adapter, as a timestamp of
    // _clock (REQ-ADV-027). Keyed by the adapter's IPv4 entry, which stands for
    // the adapter: its IPv6 companion is the same interface (see AdditionalsFor
    // on RFC 6762 s6.2). Within an adapter, keyed by Key(record), which leaves
    // out the TTL, so a goodbye counts as a multicast of the record it retracts.
    // Every record this responder sends is in its advertisements or built from
    // them, so the table holds at most that many entries for each adapter and
    // needs no clearing. Read and written only under _multicastLock, because
    // the receive loop, the announcements and the goodbyes run on different
    // tasks: deciding and recording is one step, so two of them cannot both
    // decide to send the same record.
    private readonly object _multicastLock = new();
    private readonly Dictionary<(AddressFamily Family, int Index), Dictionary<string, long>> _lastMulticast = [];

    // Elapsed time is read from GetTimestamp, which on the system clock cannot
    // go backwards, and not from the wall time: a wall clock set back an hour
    // would otherwise hold every record back for an hour.
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    // Told of a socket error in the receive loop, and the error last told of,
    // so that one error repeating is reported once. Touched only by ServeAsync.
    private readonly Action<string>? _socketError;
    private string? _reportedSocketError;

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
    /// <param name="socketError">
    /// Called from <see cref="ServeAsync"/> with a description of a socket error
    /// met while receiving a datagram or while answering one. Called when an
    /// error first appears and again only when the description changes, so an
    /// error that repeats on every datagram is reported once. Called on the
    /// receive path, so it must return promptly and must not throw. The loop
    /// goes on whether or not anything is listening here.
    /// </param>
    /// <param name="clock">
    /// Where elapsed time is read, from its timestamps, for the one-second rule
    /// (README REQ-ADV-027). Defaults to the system clock.
    /// </param>
    /// <param name="delay">
    /// How the responder waits: before an announcement, a goodbye or an answer
    /// to a probe that would come too soon after the same record, and between
    /// announcements. Defaults to Task.Delay. Injected so that tests need not
    /// wait real seconds.
    /// </param>
    public MdnsResponder(
        IMdnsTransport transport,
        IReadOnlyList<AdvertisedInterface> advertised,
        Func<bool>? advertising = null,
        Action<NameConflict>? conflicted = null,
        Action<string>? socketError = null,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(advertised);

        _clock = clock ?? TimeProvider.System;
        _delay = delay ?? (static (wait, token) => Task.Delay(wait, token));

        _advertising = advertising ?? (static () => true);
        _conflicted = conflicted;
        _socketError = socketError;

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
    /// What to answer with, which interface to answer through, the NSEC records
    /// that say which types the advertisement's claimed names have, and the
    /// adapter the answer leaves by: the IPv4 entry, for the IPv4 entry and for
    /// its IPv6 companion alike, so that the one-second rule (REQ-ADV-027)
    /// counts both as one interface.
    /// </summary>
    private sealed record AnsweringInterface(
        Advertisement Advertisement, MdnsInterface Via, IReadOnlyList<OutgoingRecord> Nsecs, MdnsInterface Adapter);

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

            Add(answering, entry.Interface, entry.Advertisement, nsecs, entry.Interface);

            if (entry.IPv6Interface is { } companion)
            {
                Add(answering, companion, entry.Advertisement, nsecs, entry.Interface);
            }
        }

        return answering;

        static void Add(
            Dictionary<(AddressFamily, int), AnsweringInterface> into,
            MdnsInterface via,
            Advertisement advertisement,
            IReadOnlyList<OutgoingRecord> nsecs,
            MdnsInterface adapter)
        {
            if (!into.TryAdd((via.Transport, via.Index), new AnsweringInterface(advertisement, via, nsecs, adapter)))
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
        _ignoredWrongInterface, _ignoredNotOurs, _unparseable, _goodbyes, _suppressedByKnownAnswers,
        _heldBackWithinASecond, _refusedOffNetwork, _answeredByUnicastOnRequest);

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
    [Requirement("REQ-ADV-027",
        "Waits before each announcement until every record in it was last multicast on that adapter at least a second before, so that an announcement carries every record and repeats none sooner than RFC 6762 s6 allows, whatever interval it is given.")]
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

                // An announcement carries every record, so it waits for the
                // second to be up rather than leave any out (REQ-ADV-027). The
                // conflict is read again after the wait, which can be up to a
                // second long.
                MulticastNote note = await WaitToMulticastAsync(
                        entry.Interface, entry.Advertisement.Records, MulticastSpacing, cancellationToken)
                    .ConfigureAwait(false);
                if (Conflict is not null)
                {
                    Unnote(note);
                    return;
                }

                var builder = new DnsResponseBuilder();
                foreach (OutgoingRecord record in entry.Advertisement.Records)
                {
                    builder.AddAnswer(record);
                }

                await SendNotedAsync(builder.Build(), entry.Interface, note, cancellationToken).ConfigureAwait(false);

                _announcements++;
            }

            if (attempt < AnnouncementCount - 1 && delayBetween > TimeSpan.Zero)
            {
                await _delay(delayBetween, cancellationToken).ConfigureAwait(false);
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
        "Answers a legacy unicast querier by unicast, echoing its query identifier and capping TTLs, when its address is on the network the query arrived from (REQ-SEC-017).")]
    [Requirement("REQ-SEC-001",
        "Every record sent comes from this responder's own Advertisement, or is an NSEC record built from that Advertisement when the responder is constructed, listing the types of the records it claims at one of its own names. Received datagrams are read for their questions; for records about the names this responder claims, to detect conflicts; and, in a query's Answer section, for the records the querier says it already holds, which can only keep a record out of an answer (REQ-ADV-026); and for whether a query's Authority section holds any record, which marks it as a probe and decides only how soon its answer may be multicast (REQ-ADV-027); and for whether each question's unicast-response bit is set, which decides only whether an answer goes by unicast to the querier (REQ-ADV-028). No record from a received packet is ever sent on, so nothing is forwarded or reflected between networks. Two things from a query are repeated, to the device that sent it and on the network it came from: a legacy unicast answer carries that query's identifier and its question, written again from the parsed values, as RFC 6762 s6.7 requires.")]
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
    [Requirement("REQ-ADV-026",
        "Leaves out of the answer every record the query's Answer section already holds with a TTL of at least half the record's own (RFC 6762 s7.1), for multicast and legacy unicast queriers alike, and sends nothing when every answer is left out, counting that query apart from queries about other names. Additionals are chosen from the answers sent, so a record left out brings none.")]
    [Requirement("REQ-ADV-027",
        "Leaves out of a multicast answer every record, answer or additional, multicast on that adapter less than a second before, over either transport (RFC 6762 s6), and sends nothing when no answer is left, counting that query apart from the others. An answer to a probe, a query with records in its Authority section, waits instead until 250 ms have passed since any record in it was last multicast there, and leaves nothing out. An answer sent by unicast, to a legacy querier or because a question asked for one (REQ-ADV-028), is neither limited nor recorded.")]
    [Requirement("REQ-SEC-017",
        "Sends an answer by unicast only to an address on the network the query arrived from. A legacy querier at any other address is sent nothing, and that query is counted apart from the others; the records a question from any other address asks for by unicast go by multicast instead.")]
    [Requirement("REQ-ADV-028",
        "Sends by unicast, to the querier's address and port through the interface the query arrived on, each record that only questions with the unicast-response bit ask for and that was multicast on that adapter, over either transport, less than a quarter of its TTL before; every other record goes by multicast. The unicast answer is made as the multicast one: identifier 0, no question, TTLs and cache-flush bits as published, its own additionals less what the multicast carries. It is neither limited by the one-second rule nor recorded as a multicast. A probe's records asked for by unicast go by unicast at once. Not for legacy queriers.")]
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

        // Set when a record that would have been an answer is left out because
        // the query already carries it (RFC 6762 s7.1, REQ-ADV-026). If that
        // leaves nothing to send, the query is counted as suppressed, and not
        // as a query about a name this responder does not hold.
        bool leftOutAsKnown = false;

        // The records that some question without the unicast-response bit
        // asks for (RFC 6762 s5.4, REQ-ADV-028). Such a record goes by
        // multicast whatever another question in the same query asks.
        var askedForByMulticast = new HashSet<string>(StringComparer.Ordinal);

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

            bool asksForUnicast = AsksForUnicast(rawClass);

            foreach (OutgoingRecord record in answers)
            {
                // The querier already holds this record, and holds it for long
                // enough (REQ-ADV-026).
                if (IsKnownAnswer(query, record))
                {
                    leftOutAsKnown = true;
                    continue;
                }

                string key = Key(record);
                if (!asksForUnicast)
                {
                    askedForByMulticast.Add(key);
                }

                if (answeredKeys.Add(key))
                {
                    answered.Add(record);
                }
            }
        }

        // RFC 6762 s5.4 (REQ-ADV-028). Records that only questions asking for
        // a unicast answer ask for may go to the querier by unicast, and only
        // when its address is on the network the query arrived from
        // (REQ-SEC-017); from anywhere else they go by multicast, like any
        // other record. Which of them do go by unicast is decided below, with
        // the one-second rule. None of this is for a legacy querier, whose
        // whole answer goes by unicast for another reason (REQ-ADV-017): the
        // path for it below does not read this.
        bool mayAnswerByUnicast = answered.Any(record => !askedForByMulticast.Contains(Key(record)))
                                  && IsOnArrivalNetwork(datagram.Source, entry);
        List<OutgoingRecord> unicastAnswers = [];

        // RFC 6762 s6 (REQ-ADV-027). A record multicast on this adapter less
        // than a second before is not multicast again: it is left out of the
        // answer, and an answer left with nothing is not sent. "A querier that
        // did not receive and cache the previous transmission will retry its
        // request and receive a subsequent response." Additionals are chosen
        // from the answers left, and are left out by the same rule. An answer
        // to a probe is the exception s6 makes: it waits instead, until 250 ms
        // have passed, and carries every record, so that a device about to
        // take one of these names hears them all. An answer sent by unicast, to
        // a legacy querier or because a question asked for one, is not a
        // multicast, so none of this applies to it, and it is not recorded as
        // one.
        //
        // RFC 6762 s5.4 (REQ-ADV-028). A record a question asks for by unicast
        // alone goes by unicast when this adapter multicast it less than a
        // quarter of its TTL before, and by multicast otherwise, "so as to keep
        // all the peer caches up to date". Every record this responder
        // publishes has a TTL of at least 120 s, a quarter of which is longer
        // than a second, so such a record is never held back by the
        // one-second rule: if it was multicast within the second, it goes by
        // unicast. An answer to a probe that asks for a unicast answer gets
        // one, at once and whatever was multicast when: s5.4, "the responder
        // should always generate the requested unicast response".
        bool heldBack = false;
        List<OutgoingRecord> additionals;
        MulticastNote? note = null;
        bool probe = query.Authorities.Count > 0;

        if (legacyUnicast)
        {
            additionals = AdditionalsLeft(entry, answered, answeredKeys);
        }
        else if (probe)
        {
            if (mayAnswerByUnicast)
            {
                unicastAnswers = TakeAll(answered, record => !askedForByMulticast.Contains(Key(record)));
            }

            // The wait for the multicast comes after the unicast answer is
            // sent, below, so that the unicast answer is not kept waiting.
            additionals = AdditionalsLeft(entry, answered, answeredKeys);
        }
        else
        {
            lock (_multicastLock)
            {
                long now = _clock.GetTimestamp();
                if (mayAnswerByUnicast)
                {
                    unicastAnswers = TakeAll(
                        answered,
                        record => !askedForByMulticast.Contains(Key(record))
                                  && SentWithin(entry.Adapter, record, QuarterOfTtl(record), now));
                }

                heldBack = answered.RemoveAll(record => SentWithin(entry.Adapter, record, MulticastSpacing, now)) > 0;
                additionals = AdditionalsLeft(entry, answered, answeredKeys);
                additionals.RemoveAll(record => SentWithin(entry.Adapter, record, MulticastSpacing, now));

                if (answered.Count > 0)
                {
                    note = NoteMulticast(entry.Adapter, [.. answered, .. additionals], now);
                }
            }
        }

        foreach (OutgoingRecord record in answered)
        {
            builder.AddAnswer(Adjust(record, legacyUnicast));
        }

        foreach (OutgoingRecord extra in additionals)
        {
            builder.AddAdditional(Adjust(extra, legacyUnicast));
        }

        // The unicast answer is made as the multicast one is (s5.4: "Unicast
        // replies are subject to all the same packet generation rules as
        // multicast replies"): identifier 0, no question, TTLs and cache-flush
        // bits as published. Its additionals are chosen from its own answers,
        // and leave out anything the multicast answer carries, which the
        // querier receives as well.
        var unicastBuilder = new DnsResponseBuilder(0);
        if (unicastAnswers.Count > 0)
        {
            var inMulticast = new HashSet<string>(answered.Concat(additionals).Select(Key), StringComparer.Ordinal);
            foreach (OutgoingRecord record in unicastAnswers)
            {
                unicastBuilder.AddAnswer(record);
            }

            foreach (OutgoingRecord extra in AdditionalsLeft(entry, unicastAnswers, answeredKeys))
            {
                if (!inMulticast.Contains(Key(extra)))
                {
                    unicastBuilder.AddAdditional(extra);
                }
            }
        }

        if (builder.AnswerCount == 0 && unicastBuilder.AnswerCount == 0)
        {
            if (heldBack)
            {
                // Something would have been sent but for the one-second rule.
                _heldBackWithinASecond++;
            }
            else if (leftOutAsKnown)
            {
                _suppressedByKnownAnswers++;
            }
            else
            {
                _ignoredNotOurs++;
            }

            return false;
        }

        // An answer by unicast goes only to an address on the network the
        // query arrived from (REQ-SEC-017). Windows chooses the adapter a
        // unicast datagram leaves by from its routing table, so an answer to
        // a forged source address could otherwise leave by the printer-side
        // adapter, or through a router off both networks. Such a query is
        // counted, apart from the others, only when there was an answer to
        // refuse.
        if (legacyUnicast && !IsOnArrivalNetwork(datagram.Source, entry))
        {
            _refusedOffNetwork++;
            return false;
        }

        // The unicast answer first: an answer to a probe is not kept waiting
        // for the multicast's wait. Its address passed IsOnArrivalNetwork above
        // (REQ-SEC-017). Should it fail, the multicast it would have gone with
        // is not sent, and what was noted for that multicast is taken back.
        bool answeredByUnicastOnRequest = unicastBuilder.AnswerCount > 0;
        if (answeredByUnicastOnRequest)
        {
            try
            {
                await _transport
                    .SendUnicastAsync(unicastBuilder.Build(), datagram.Source, entry.Via, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                if (note is not null)
                {
                    Unnote(note);
                }

                throw;
            }
        }

        if (builder.AnswerCount > 0)
        {
            byte[] response = builder.Build();

            if (legacyUnicast)
            {
                await _transport
                    .SendUnicastAsync(response, datagram.Source, entry.Via, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (probe)
                {
                    note = await WaitToMulticastAsync(
                            entry.Adapter, [.. answered, .. additionals], ProbeAnswerSpacing, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Every multicast path above that leaves an answer to send notes it.
                await SendNotedAsync(response, entry.Via, note!, cancellationToken).ConfigureAwait(false);
            }
        }

        if (answeredByUnicastOnRequest)
        {
            _answeredByUnicastOnRequest++;
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
    /// <remarks>
    /// A socket error does not end the loop, whether it comes while receiving a
    /// datagram or while answering one. Until 2026-10-07 only the receive was
    /// guarded: a send that failed inside <see cref="HandleAsync"/> ended this
    /// method, and no query was answered again until the service was restarted.
    /// An error while receiving is counted, as it always was, under
    /// <see cref="ResponderActivity.Unparseable"/>, a counter named for something
    /// else. An error while answering needs no counter of its own: the query was
    /// counted as seen and is not counted as answered.
    /// </remarks>
    [Requirement("REQ-LIF-005",
        "A socket error while receiving a datagram, or while answering one, is reported once for each different error and the loop continues; only cancellation ends it.")]
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
            catch (System.Net.Sockets.SocketException ex)
            {
                // Transient network trouble - an adapter flapping, a buffer
                // overrun. Losing one datagram is not a reason to stop serving.
                _unparseable++;
                ReportSocketError("receiving", ex);
                continue;
            }

            try
            {
                await HandleAsync(datagram, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                // The answer could not be sent. One lost answer is not a reason
                // to stop answering: the client asks again, and the next send
                // may succeed. A cancellation is not caught here and ends the
                // loop, as it did before.
                ReportSocketError("answering", ex);
            }
        }
    }

    /// <summary>
    /// Tells the service of a socket error, unless it is the one last told of.
    /// </summary>
    private void ReportSocketError(string doing, System.Net.Sockets.SocketException error)
    {
        string description = $"while {doing}: {error.SocketErrorCode}: {error.Message}";
        if (string.Equals(description, _reportedSocketError, StringComparison.Ordinal))
        {
            return;
        }

        _reportedSocketError = description;
        _socketError?.Invoke(description);
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
    [Requirement("REQ-ADV-027",
        "Waits before each goodbye until every record it retracts was last multicast on that adapter at least a second before, so that the goodbye still retracts every record and repeats none sooner than RFC 6762 s6 allows.")]
    public async Task SendGoodbyeAsync(CancellationToken cancellationToken)
    {
        foreach (AdvertisedInterface entry in _advertised)
        {
            // BuildAnswering files every advertised interface under its own
            // family and index, so this lookup cannot miss.
            IReadOnlyList<OutgoingRecord> nsecs =
                _answering[(entry.Interface.Transport, entry.Interface.Index)].Nsecs;

            IReadOnlyList<OutgoingRecord> goodbye =
                AdvertisementBuilder.ToGoodbye([.. entry.Advertisement.Records, .. nsecs]);

            // A goodbye sends the same records with TTL 0 (RFC 6762 s10.1), and
            // is a multicast of them like any other, so it waits for the
            // second to be up rather than leave any out (REQ-ADV-027).
            MulticastNote note = await WaitToMulticastAsync(entry.Interface, goodbye, MulticastSpacing, cancellationToken)
                .ConfigureAwait(false);

            var builder = new DnsResponseBuilder();
            foreach (OutgoingRecord record in goodbye)
            {
                builder.AddAnswer(record);
            }

            await SendNotedAsync(builder.Build(), entry.Interface, note, cancellationToken).ConfigureAwait(false);

            _goodbyes++;
        }
    }

    /// <summary>
    /// True when an answer may be sent by unicast to this address: when it is
    /// on the network of the interface the query arrived on.
    /// </summary>
    /// <remarks>
    /// Over IPv4, the address must be on the client network as the relay works
    /// it out (<see cref="ClientNetwork.Of"/>; README REQ-SEC-012), worked out
    /// from the adapter's IPv4 address. Over IPv6, it must be a link-local
    /// address whose scope is the index of the interface the query arrived on:
    /// a link-local address names a host only together with its scope, and the
    /// service deals in no other IPv6 address (REQ-ADV-021). A link-local
    /// address with no scope is refused, because which link it is on cannot be
    /// told.
    ///
    /// Why this is needed: the socket sets the interface a multicast leaves
    /// by, and not the one a unicast datagram leaves by, which Windows chooses
    /// from its routing table. Until 2026-10-13 an answer to a legacy querier
    /// went to whatever address the query gave, so a query arriving on the
    /// client adapter with a forged source address on the printer network, or
    /// on no local network, would have had its answer sent there. See
    /// docs/findings/2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md.
    /// RFC 6762 s5.5 asks the same check of a query sent by unicast.
    /// </remarks>
    [Requirement("REQ-SEC-017",
        "Decides whether an address may be sent an answer by unicast: over IPv4, only an address on the client network as the relay works it out from the arrival adapter's address; over IPv6, only a link-local address whose scope is the arrival interface's index.")]
    private static bool IsOnArrivalNetwork(IPEndPoint source, AnsweringInterface entry)
    {
        IPAddress address = source.Address;

        // The address family needs no test of its own. IsIPv6LinkLocal is false
        // for every IPv4 address, and an IPv4 network contains no IPv6 address
        // (read in Claude's workspace on 2026-10-13, runtime 10.0.12).
        if (entry.Via.IsIPv6)
        {
            // The IPv6 entry's own index: the platform numbers interfaces per
            // address family (docs/findings/2026-09-13-interface-index-parity.md).
            return address.IsIPv6LinkLocal && address.ScopeId == entry.Via.Index;
        }

        return ClientNetwork.Of(entry.Adapter.Address).Contains(address);
    }

    /// <summary>
    /// A quarter of the record's TTL: within it, RFC 6762 s5.4 counts the
    /// record as multicast "recently" (README REQ-ADV-028).
    /// </summary>
    private static TimeSpan QuarterOfTtl(OutgoingRecord record) =>
        TimeSpan.FromTicks(record.Ttl * TimeSpan.TicksPerSecond / 4);

    /// <summary>Removes from the list every record that matches, and returns them, in order.</summary>
    private static List<OutgoingRecord> TakeAll(List<OutgoingRecord> records, Predicate<OutgoingRecord> match)
    {
        List<OutgoingRecord> taken = records.FindAll(match);
        records.RemoveAll(match);
        return taken;
    }

    /// <summary>
    /// The additionals for these answers, leaving out any that is itself an
    /// answer or listed twice.
    /// </summary>
    /// <remarks>
    /// Chosen from the answers actually sent: a record left out as known
    /// (REQ-ADV-026), or as multicast less than a second before (REQ-ADV-027),
    /// brings no additionals of its own. Known answers are not looked for
    /// among the additionals; RFC 6762 s7.1 asks this of answers.
    /// </remarks>
    private static List<OutgoingRecord> AdditionalsLeft(
        AnsweringInterface entry, List<OutgoingRecord> answered, HashSet<string> answeredKeys)
    {
        var additionals = new List<OutgoingRecord>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (OutgoingRecord extra in AdditionalsFor(entry.Advertisement, entry.Nsecs, answered))
        {
            if (!answeredKeys.Contains(Key(extra)) && added.Add(Key(extra)))
            {
                additionals.Add(extra);
            }
        }

        return additionals;
    }

    /// <summary>
    /// Waits until every one of these records was last multicast on this
    /// adapter at least <paramref name="spacing"/> before, then records them
    /// as multicast now, for the caller to send with
    /// <see cref="SendNotedAsync"/>.
    /// </summary>
    /// <remarks>
    /// The deciding and the recording are one step under the lock, and the
    /// wait is taken outside it and then the records looked at again, because
    /// another task may have multicast one of them meanwhile. The records are
    /// recorded before they are sent, so that no other task can decide to send
    /// one of them in between; a send that fails takes the record back
    /// (<see cref="SendNotedAsync"/>).
    /// </remarks>
    [Requirement("REQ-ADV-027",
        "Decides and records under one lock, waiting outside it as long as any of the records was multicast on the adapter more recently than the spacing allows.")]
    private async Task<MulticastNote> WaitToMulticastAsync(
        MdnsInterface adapter, IReadOnlyList<OutgoingRecord> records, TimeSpan spacing, CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan wait;
            lock (_multicastLock)
            {
                long now = _clock.GetTimestamp();
                wait = WaitBefore(adapter, records, spacing, now);
                if (wait <= TimeSpan.Zero)
                {
                    return NoteMulticast(adapter, records, now);
                }
            }

            await _delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// What <see cref="NoteMulticast"/> recorded: the adapter, the time, and
    /// for each record the time it held before, or null, so that the record can
    /// be taken back.
    /// </summary>
    private sealed record MulticastNote(MdnsInterface Adapter, long At, IReadOnlyList<(string Key, long? Before)> Records);

    /// <summary>
    /// Sends a multicast whose records have been noted, and takes the note back
    /// if the send does not complete, cancellation included.
    /// </summary>
    /// <remarks>
    /// A send that throws is taken to have multicast nothing, and RFC 6762 s6
    /// counts from "the last time that record was multicast". Without this, a
    /// failed send would hold the same records back for a second, and a query
    /// they held back would be counted as one, for records that were not sent.
    /// </remarks>
    private async Task SendNotedAsync(byte[] payload, MdnsInterface via, MulticastNote note, CancellationToken cancellationToken)
    {
        try
        {
            await _transport.SendMulticastAsync(payload, via, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Unnote(note);
            throw;
        }
    }

    /// <summary>
    /// Takes a note back: each record goes back to the time it held before,
    /// unless another multicast has recorded it since.
    /// </summary>
    private void Unnote(MulticastNote note)
    {
        lock (_multicastLock)
        {
            if (!_lastMulticast.TryGetValue((note.Adapter.Transport, note.Adapter.Index), out Dictionary<string, long>? sent))
            {
                return;
            }

            foreach ((string key, long? before) in note.Records)
            {
                if (sent.TryGetValue(key, out long at) && at == note.At)
                {
                    if (before is { } earlier)
                    {
                        sent[key] = earlier;
                    }
                    else
                    {
                        sent.Remove(key);
                    }
                }
            }
        }
    }

    /// <summary>
    /// How long until every one of these records may be multicast on this
    /// adapter: zero when each was last multicast there at least
    /// <paramref name="spacing"/> before <paramref name="now"/>, or never.
    /// Called under _multicastLock.
    /// </summary>
    private TimeSpan WaitBefore(MdnsInterface adapter, IEnumerable<OutgoingRecord> records, TimeSpan spacing, long now)
    {
        TimeSpan wait = TimeSpan.Zero;
        if (!_lastMulticast.TryGetValue((adapter.Transport, adapter.Index), out Dictionary<string, long>? sent))
        {
            return wait;
        }

        foreach (OutgoingRecord record in records)
        {
            if (sent.TryGetValue(Key(record), out long at))
            {
                TimeSpan left = spacing - _clock.GetElapsedTime(at, now);
                if (left > wait)
                {
                    wait = left;
                }
            }
        }

        return wait;
    }

    /// <summary>True when this record was multicast on this adapter less than <paramref name="spacing"/> before <paramref name="now"/>. Called under _multicastLock.</summary>
    private bool SentWithin(MdnsInterface adapter, OutgoingRecord record, TimeSpan spacing, long now) =>
        WaitBefore(adapter, [record], spacing, now) > TimeSpan.Zero;

    /// <summary>Records these records as multicast on this adapter at <paramref name="now"/>. Called under _multicastLock.</summary>
    private MulticastNote NoteMulticast(MdnsInterface adapter, IEnumerable<OutgoingRecord> records, long now)
    {
        if (!_lastMulticast.TryGetValue((adapter.Transport, adapter.Index), out Dictionary<string, long>? sent))
        {
            sent = new Dictionary<string, long>(StringComparer.Ordinal);
            _lastMulticast[(adapter.Transport, adapter.Index)] = sent;
        }

        var noted = new List<(string Key, long? Before)>();
        foreach (OutgoingRecord record in records)
        {
            string key = Key(record);
            noted.Add((key, sent.TryGetValue(key, out long before) ? before : null));
            sent[key] = now;
        }

        return new MulticastNote(adapter, now, noted);
    }

    /// <summary>
    /// True when the query's Answer section already holds this record with a
    /// TTL at least half the record's own: a known answer, which RFC 6762 s7.1
    /// says a responder MUST NOT send.
    /// </summary>
    /// <remarks>
    /// The same record means the same name, the same type, class IN, and the
    /// same data, compared as the bytes this responder's writer produces, which
    /// is how conflict detection compares data (<see cref="RdataOf"/>). The
    /// cache-flush bit is not part of the class: RFC 6762 s10.2 says it MUST
    /// NOT be set in a known answer, and a known answer that has it set still
    /// names a record in class IN. A known answer in any other class is
    /// another record.
    ///
    /// Half the correct value is taken as half the TTL the record is published
    /// with. For a multicast answer that is the TTL sent. A legacy unicast
    /// answer goes with its TTL capped (REQ-ADV-017); the comparison still uses
    /// the published TTL, the one s7.1 calls "the true RR TTL as known by the
    /// Multicast DNS responder". At less than half, the record is sent, as
    /// s7.1 says it MUST be.
    ///
    /// Data whose names differ only in the case of a letter are not the same
    /// bytes, so such a known answer does not keep the record out: it is sent,
    /// as every record was before this rule existed.
    ///
    /// A query marked truncated, whose known answers go on in packets after it
    /// (RFC 6762 s7.2), is judged on the known answers in its own packet. Its
    /// answer is not held back to wait for the rest.
    /// </remarks>
    [Requirement("REQ-ADV-026",
        "Defines a known answer: a record in the query's Answer section with the same name, the same type, class IN read without the cache-flush bit, and the same data, whose TTL is at least half the TTL the record is published with. At less than half it is not one, and the record is sent.")]
    private static bool IsKnownAnswer(DnsMessage query, OutgoingRecord ours)
    {
        byte[]? ourData = null;

        foreach (DnsRecord known in query.Answers)
        {
            if ((known.RawClass & 0x7FFF) != ClassIn
                || known.Type != ours.Type
                || !known.Name.Equals(ours.Name))
            {
                continue;
            }

            // At least half: twice the TTL the querier holds, in a type that
            // cannot overflow, is no less than the record's own.
            if ((ulong)known.Ttl * 2 < ours.Ttl)
            {
                continue;
            }

            ourData ??= DnsRecordWriter.EncodeRdata(ours.Type, ours.Payload);
            if (KnownAnswerData(known) is { } theirData && theirData.AsSpan().SequenceEqual(ourData))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A known answer's data, uncompressed, as this responder's writer would
    /// write it; or null when it cannot be written back out.
    /// </summary>
    /// <remarks>
    /// An NSEC record in the restricted form is written again from what the
    /// reader decoded, so that a querier listing this responder's own NSEC is
    /// recognised. Every other record is read as conflict detection reads it
    /// (<see cref="RdataOf"/>), which is left as it is.
    /// </remarks>
    private static byte[]? KnownAnswerData(DnsRecord known)
    {
        if (known.Type == DnsRecordType.Nsec
            && known.NsecNextDomainName is { } next
            && known.NsecTypes is { } types)
        {
            try
            {
                return DnsRecordWriter.EncodeRdata(DnsRecordType.Nsec, new NsecPayload(next, types));
            }
            catch (ArgumentException)
            {
                // Something decoded that the writer refuses. It came from the
                // network, so it must not end the receive loop.
                return null;
            }
        }

        return RdataOf(known);
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

    /// <summary>
    /// True when the question's unicast-response bit, the top bit of its class
    /// field, is set: the querier is willing to take its answer by unicast
    /// (RFC 6762 s5.4; README REQ-ADV-028).
    /// </summary>
    private static bool AsksForUnicast(ushort rawClass) => (rawClass & 0x8000) != 0;

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

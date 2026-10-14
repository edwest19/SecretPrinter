// -----------------------------------------------------------------------------
// RandomAnswerDelayTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-13, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder waits a random 20 to 120 ms before an answer
//   that other responders may be sending too, and not before one it alone
//   sends (README REQ-ADV-029). RFC 6762 section 6: "In any case where there
//   may be multiple responses, such as queries where the answer is a member of
//   a shared resource record set, each responder SHOULD delay its response by
//   a random amount of time selected with uniform random distribution in the
//   range 20-120 ms." Section 6.3: "for query messages containing more than
//   one question, all (non-defensive) answers SHOULD be randomly delayed in
//   the range 20-120 ms". And for a responder that will be the only one to
//   answer, "because it is able to answer every question in the query
//   message" and every answer is its alone, section 6 says it "SHOULD NOT
//   impose any random delay before responding, and SHOULD normally generate
//   its response within at most 10 ms." Until 2026-10-13 nothing waited
//   (docs/findings/2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md,
//   item 3).
//
//   What these tests take as the rule, each part of it recorded in README
//   REQ-ADV-029:
//   - An answer waits when it holds a shared record, one the advertisement
//     holds without the cache-flush bit (the PTR records of the service
//     types), or when the query holds more than one question, whatever the
//     answer holds and however many of the questions are answered.
//   - The wait comes before the rest of the answer is decided, so that the
//     one-second rule (REQ-ADV-027) and the unicast choice (REQ-ADV-028) judge
//     the moment of sending.
//   - It applies to an answer by multicast, by unicast because a question
//     asked for one, and to a legacy querier; not to an answer to a probe,
//     which section 6 says a responder "MUST respond quickly" to.
//   - After the wait, and after the 250 ms wait of an answer to a probe, nothing
//     more is sent if the printer has been withdrawn or a conflict is held. A
//     probe's unicast answer, sent before its wait, has gone by then, and
//     still counts.
//
//   The tests that expect a wait, or nothing sent after one, failed before the
//   change. Those that expect no wait passed before and after: they are there
//   so that the rule cannot be made to pass by waiting before every answer.
//   The test of the one-second rule failed before only at its last line, the
//   count: a responder that never waits passes the rest of it, which is there
//   so that a wait put after the decision fails.
//
//   The file was first written the same day for shared records alone, as
//   SharedAnswerDelayTests. Section 6.3 was read before the change was made,
//   and the file was renamed and widened. It was never committed under the
//   first name.
//
// The clock and the draw:
//   ManualClock, as in OncePerSecondTests: its Delay records each wait, moves
//   the timestamps on by it and returns at once. Most tests fix the draw with
//   the responder's randomAnswerDelay parameter, so that each knows the wait
//   it should see; one uses the default draw, to see its range and its spread.
//
// Addresses:
//   The client adapter is MdnsResponderTests.ClientNic, 192.168.1.234. The
//   querier is 192.168.1.41, as in the other responder tests; the probing and
//   conflicting device is 192.168.1.77, made up.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class RandomAnswerDelayTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    private static readonly DnsName Subtype = DnsName.Parse("_universal._sub._ipp._tcp.local");
    private static readonly DnsName OtherService = DnsName.Parse("_airplay._tcp.local");

    private const string Querier = "192.168.1.41";
    private const string Prober = "192.168.1.77";

    private sealed class Setup
    {
        public required MdnsResponder Responder { get; init; }
        public required FakeTransport Transport { get; init; }
        public required ManualClock Clock { get; init; }
        public required Advertisement Advertisement { get; init; }
        public List<long> SentAt { get; } = [];
        public bool Advertising { get; set; } = true;

        /// <summary>Run inside each wait the responder takes, before the clock moves.</summary>
        public Action? DuringWait { get; set; }
    }

    /// <param name="draw">The random wait drawn for each answer that takes one; null for the default draw.</param>
    private static Setup Build(Func<TimeSpan>? draw)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var clock = new ManualClock();
        Setup? s = null;
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)],
            advertising: () => s!.Advertising,
            clock: clock,
            delay: (wait, token) =>
            {
                s!.DuringWait?.Invoke();
                return clock.Delay(wait, token);
            },
            randomAnswerDelay: draw);
        s = new Setup { Responder = responder, Transport = transport, Clock = clock, Advertisement = advertisement };
        transport.OnSend = _ => s.SentAt.Add(clock.GetTimestamp());
        return s;
    }

    private static Func<TimeSpan> Always(int milliseconds) => () => TimeSpan.FromMilliseconds(milliseconds);

    private static MdnsDatagram Ask(DnsName name, DnsRecordType type, bool unicast = false, int port = 5353)
    {
        byte[] payload = new DnsQueryBuilder(0x2A2A).AddQuestion(name, type, requestUnicastResponse: unicast).Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(Querier), port), ClientNic.Index, ClientNic);
    }

    /// <summary>One query of two questions, neither asking for a unicast answer.</summary>
    private static MdnsDatagram AskTwo(DnsName first, DnsRecordType firstType, DnsName second, DnsRecordType secondType)
    {
        byte[] payload = new DnsQueryBuilder(0x2A2A)
            .AddQuestion(first, firstType, requestUnicastResponse: false)
            .AddQuestion(second, secondType, requestUnicastResponse: false)
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(Querier), 5353), ClientNic.Index, ClientNic);
    }

    /// <summary>A record another device proposes, which makes a query carrying it a probe.</summary>
    private static OutgoingRecord Proposed(DnsName name) =>
        new(name, DnsRecordType.A, 120, CacheFlush: false, new AddressPayload(IPAddress.Parse(Prober)));

    /// <summary>A probe from another device: a question with a proposed record in its Authority section.</summary>
    private static MdnsDatagram ProbeFor(DnsName name, DnsRecordType type)
    {
        byte[] payload = new DnsQueryBuilder(0)
            .AddQuestion(name, type, requestUnicastResponse: false)
            .AddAuthority(Proposed(name))
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(Prober), 5353), ClientNic.Index, ClientNic);
    }

    /// <summary>Another device's response claiming the host name for its own address: a conflict (REQ-ADV-024).</summary>
    private static MdnsDatagram ConflictFor(DnsName host)
    {
        byte[] payload = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(host, DnsRecordType.A, 120, CacheFlush: true, new AddressPayload(IPAddress.Parse(Prober))))
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(Prober), 5353), ClientNic.Index, ClientNic);
    }

    private static bool Handle(Setup s, MdnsDatagram datagram) =>
        s.Responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static DnsName Instance(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.Srv).Name;

    // ---- Which answers wait --------------------------------------------------

    [TestCase("An answer holding a shared record waits the drawn time before it is sent")]
    [Requirement("REQ-ADV-029")]
    public static void An_answer_holding_a_shared_record_waits()
    {
        Setup s = Build(Always(77));
        long asked = s.Clock.GetTimestamp();

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr)), "the question is answered");
        Assert.Equal(1, s.Clock.Waits.Count, "one wait");
        Assert.Equal(TimeSpan.FromMilliseconds(77), s.Clock.Waits[0], "of the time drawn");
        Assert.Equal(TimeSpan.FromMilliseconds(77), s.Clock.GetElapsedTime(asked, s.SentAt[0]),
            "RFC 6762 s6: the PTR is shared, so the answer went 77 ms after the question");
        Assert.Equal(1, s.Responder.Activity.WaitedAtRandom, "counted as an answer that waited");
    }

    [TestCase("An answer to one question, holding only records the service claims alone, goes at once")]
    [Requirement("REQ-ADV-029")]
    public static void An_answer_of_unique_records_goes_at_once()
    {
        Setup s = Build(Always(77));

        Assert.True(Handle(s, Ask(Host(s.Advertisement), DnsRecordType.A)), "the question is answered");
        Assert.Equal(0, s.Clock.Waits.Count,
            "RFC 6762 s6: the only responder for the name \"SHOULD NOT impose any random delay\"");
        Assert.Equal(0, s.Responder.Activity.WaitedAtRandom, "nothing counted");
    }

    [TestCase("An answer to a query of two questions waits, even when every record in it is one the service claims alone")]
    [Requirement("REQ-ADV-029")]
    public static void A_query_of_two_questions_waits_for_records_held_alone()
    {
        Setup s = Build(Always(77));
        long asked = s.Clock.GetTimestamp();

        Assert.True(
            Handle(s, AskTwo(Host(s.Advertisement), DnsRecordType.A, Instance(s.Advertisement), DnsRecordType.Srv)),
            "both questions are answered");
        Assert.Equal(TimeSpan.FromMilliseconds(77), s.Clock.GetElapsedTime(asked, s.SentAt[0]),
            "RFC 6762 s6.3: \"for query messages containing more than one question, all (non-defensive) answers SHOULD be randomly delayed\"");
        Assert.Equal(1, s.Responder.Activity.WaitedAtRandom, "counted as an answer that waited");
    }

    [TestCase("An answer to a query of two questions waits when only one of them is about the service")]
    [Requirement("REQ-ADV-029")]
    public static void A_query_of_two_questions_waits_when_one_is_answered()
    {
        Setup s = Build(Always(77));
        long asked = s.Clock.GetTimestamp();

        Assert.True(
            Handle(s, AskTwo(Host(s.Advertisement), DnsRecordType.A, OtherService, DnsRecordType.Ptr)),
            "the question about the host is answered");
        Assert.Equal(TimeSpan.FromMilliseconds(77), s.Clock.GetElapsedTime(asked, s.SentAt[0]),
            "RFC 6762 s6.3: the questions in the query are counted, not those answered; "
            + "s6: the service is not \"able to answer every question in the query message\"");
    }

    [TestCase("A query of two questions with nothing to answer here does not wait")]
    [Requirement("REQ-ADV-029")]
    public static void A_query_of_two_questions_with_nothing_to_answer_does_not_wait()
    {
        Setup s = Build(Always(77));

        Assert.False(
            Handle(s, AskTwo(OtherService, DnsRecordType.Ptr, DnsName.Parse("_raop._tcp.local"), DnsRecordType.Ptr)),
            "neither question is about the service");
        Assert.Equal(0, s.Clock.Waits.Count, "there is no answer to delay");
        Assert.Equal(1, s.Responder.Activity.IgnoredNotOurs, "counted as about other services, as before");
    }

    [TestCase("The default draw is a whole number of milliseconds from 20 to 120, both included, each about as often as any other")]
    [Requirement("REQ-ADV-029")]
    public static void The_default_draw_covers_20_to_120_ms_evenly()
    {
        Setup s = Build(draw: null);

        // A second apart, so that the one-second rule never holds one back.
        const int Answers = 5000;
        for (int i = 0; i < Answers; i++)
        {
            Handle(s, Ask(Subtype, DnsRecordType.Ptr));
            s.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        List<double> waits = [.. s.Clock.Waits.Select(w => w.TotalMilliseconds)];
        Assert.Equal(Answers, waits.Count, "one wait for each answer");
        Assert.True(waits.All(w => w >= 20 && w <= 120),
            $"RFC 6762 s6: each in the range 20-120 ms; drawn {waits.Min()} to {waits.Max()} ms");
        Assert.True(waits.All(w => w == Math.Floor(w)), "each a whole number of milliseconds");

        // Uniform over the 101 whole milliseconds from 20 to 120, 5,000 draws
        // come to about 49.5 of each. The chance that a given one of them is
        // never drawn is (100/101)^5000, about 2.5 in 10^22, so the chance
        // that any is missing is below 3 in 10^20; the chance that any is
        // drawn more than 150 times is smaller still. So this fails by chance
        // too rarely to matter, and fails for a draw that leaves out either
        // end of the range, or that favours some part of it heavily.
        Assert.Equal(101, waits.Distinct().Count(), "every whole millisecond from 20 to 120 is drawn");
        int most = waits.GroupBy(w => w).Max(g => g.Count());
        Assert.True(most <= 150, $"none drawn far more often than the others; the most often, {most} times");
    }

    // ---- The wait comes before the decision ----------------------------------

    [TestCase("The one-second rule judges the moment of sending, after the wait")]
    [Requirement("REQ-ADV-029")]
    public static void The_one_second_rule_judges_the_moment_of_sending()
    {
        var draws = new Queue<int>([100, 20]);
        Setup s = Build(() => TimeSpan.FromMilliseconds(draws.Dequeue()));

        Handle(s, Ask(Subtype, DnsRecordType.Ptr));
        s.Clock.Advance(TimeSpan.FromMilliseconds(950));

        // The first answer went 100 ms after its question; this one would go
        // 20 ms after it, 970 ms after the first.
        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr)),
            "RFC 6762 s6: less than a second after the PTR was last multicast, counted from when it was sent");
        Assert.Equal(1, s.Transport.Sent.Count, "one answer in all");
        Assert.Equal(1, s.Responder.Activity.HeldBackWithinASecond, "the second held back");
        Assert.Equal(1, s.Responder.Activity.WaitedAtRandom, "only the answer sent is counted as having waited");
    }

    // ---- Every way of sending ------------------------------------------------

    [TestCase("An answer by unicast that a question asked for waits too, when it holds a shared record")]
    [Requirement("REQ-ADV-029")]
    public static void A_unicast_answer_asked_for_waits_too()
    {
        Setup s = Build(Always(50));

        Handle(s, Ask(Subtype, DnsRecordType.Ptr));
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        long asked = s.Clock.GetTimestamp();

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, unicast: true)), "answered");
        Assert.False(s.Transport.Sent[1].WasMulticast, "by unicast (REQ-ADV-028)");
        Assert.Equal(TimeSpan.FromMilliseconds(50), s.Clock.GetElapsedTime(asked, s.SentAt[1]),
            "RFC 6762 s5.4: unicast replies are subject to the same randomized delays");
    }

    [TestCase("A legacy answer holding a shared record waits too")]
    [Requirement("REQ-ADV-029")]
    public static void A_legacy_answer_waits_too()
    {
        Setup s = Build(Always(50));
        long asked = s.Clock.GetTimestamp();

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, port: 50000)), "answered");
        Assert.False(s.Transport.Sent[0].WasMulticast, "by unicast, to a legacy querier");
        Assert.Equal(TimeSpan.FromMilliseconds(50), s.Clock.GetElapsedTime(asked, s.SentAt[0]), "after the wait");
    }

    [TestCase("An answer to a probe does not take the random wait, even for a shared record")]
    [Requirement("REQ-ADV-029")]
    public static void An_answer_to_a_probe_does_not_wait()
    {
        Setup s = Build(Always(50));

        Assert.True(Handle(s, ProbeFor(Subtype, DnsRecordType.Ptr)), "answered");
        Assert.Equal(0, s.Clock.Waits.Count,
            "RFC 6762 s6: \"In the special case of answering probe queries ... a Multicast DNS responder MUST respond quickly\"");
    }

    // ---- What may change during the wait -------------------------------------

    [TestCase("If the printer is withdrawn while the answer waits, nothing is sent")]
    [Requirement("REQ-ADV-029")]
    public static void Withdrawn_while_waiting_sends_nothing()
    {
        Setup s = Build(Always(60));
        s.DuringWait = () => s.Advertising = false;

        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr)), "not answered");
        Assert.Equal(0, s.Transport.Sent.Count,
            "REQ-LIF-006: an answer after the goodbye would offer again a printer just withdrawn");
        Assert.Equal(0, s.Responder.Activity.QueriesAnswered, "not counted as answered");
    }

    [TestCase("If a conflict is heard while the answer waits, nothing is sent")]
    [Requirement("REQ-ADV-029")]
    public static void A_conflict_while_waiting_sends_nothing()
    {
        Setup s = Build(Always(60));
        DnsName host = Host(s.Advertisement);
        s.DuringWait = () =>
        {
            s.DuringWait = null;
            Handle(s, ConflictFor(host));
        };

        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr)), "not answered");
        Assert.NotNull(s.Responder.Conflict, "the conflict was heard during the wait");
        Assert.Equal(0, s.Transport.Sent.Count, "REQ-ADV-024: nothing is answered once a conflict is held");
    }

    [TestCase("If the printer is withdrawn during the 250 ms wait of an answer to a probe, nothing is multicast")]
    [Requirement("REQ-ADV-029")]
    public static void Withdrawn_during_a_probes_wait_sends_nothing()
    {
        Setup s = Build(Always(60));
        DnsName host = Host(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));
        s.DuringWait = () => s.Advertising = false;

        Assert.False(Handle(s, ProbeFor(host, DnsRecordType.Any)), "the probe is not answered");
        Assert.Equal(1, s.Clock.Waits.Count, "the wait to 250 ms was taken (REQ-ADV-027)");
        Assert.Equal(1, s.Transport.Sent.Count, "and after it nothing was sent: the printer had been withdrawn");

        // The record of what was multicast is as it was: the answer that was
        // not sent is not counted as one.
        s.Advertising = true;
        s.DuringWait = null;
        s.Clock.Advance(TimeSpan.FromMilliseconds(800));
        Assert.True(Handle(s, Ask(host, DnsRecordType.A)),
            "a second after the A record went out it is sent again; the answer not sent did not count");
    }

    [TestCase("If the printer is withdrawn during a probe's 250 ms wait after its unicast answer went, that answer counts and nothing is multicast")]
    [Requirement("REQ-ADV-029")]
    public static void Withdrawn_during_a_probes_wait_after_its_unicast_answer()
    {
        Setup s = Build(Always(60));
        DnsName host = Host(s.Advertisement);
        DnsName instance = Instance(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));
        s.DuringWait = () => s.Advertising = false;

        // The instance name asked about for a unicast answer, the host name for
        // a multicast one: the first goes at once, the second waits to 250 ms.
        byte[] payload = new DnsQueryBuilder(0)
            .AddQuestion(instance, DnsRecordType.Any, requestUnicastResponse: true)
            .AddQuestion(host, DnsRecordType.Any, requestUnicastResponse: false)
            .AddAuthority(Proposed(host))
            .Build();
        var probe = new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(Prober), 5353), ClientNic.Index, ClientNic);

        Assert.True(Handle(s, probe), "answered: the unicast answer went before the wait");
        Assert.Equal(1, s.Clock.Waits.Count, "the wait to 250 ms was taken (REQ-ADV-027)");
        Assert.Equal(2, s.Transport.Sent.Count, "the first answer and the probe's unicast answer, and no multicast after the wait");
        Assert.False(s.Transport.Sent[1].WasMulticast, "the second by unicast (REQ-ADV-028)");
        Assert.Equal(1, s.Responder.Activity.AnsweredByUnicastOnRequest, "counted as answered by unicast");
        Assert.Equal(2, s.Responder.Activity.QueriesAnswered, "and as answered");
    }

    // ---- The stop summary ----------------------------------------------------

    [TestCase("The stop summary's first line says how many of the queries served waited at random")]
    [Requirement("REQ-ADV-029")]
    public static void The_stop_summary_counts_the_answers_that_waited()
    {
        Setup s = Build(Always(30));
        DnsName host = Host(s.Advertisement);

        // Three answered, two of them after a wait, and one about another
        // service: different counts, so a line that gave one in place of
        // another would read differently. The A record first: asked for after
        // a PTR, it would be held back, having gone out with it.
        Handle(s, Ask(host, DnsRecordType.A));
        Handle(s, Ask(Subtype, DnsRecordType.Ptr));
        s.Clock.Advance(TimeSpan.FromSeconds(2));
        Handle(s, Ask(Subtype, DnsRecordType.Ptr));
        Handle(s, Ask(OtherService, DnsRecordType.Ptr));

        Assert.Equal(
            "Served 3 quer(ies) of 4 seen; 1 were for other services and were ignored; "
            + "0 carried every answer already and were not answered (RFC 6762 s7.1); "
            + "0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); "
            + "0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); "
            + "0 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); "
            + "2 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).",
            s.Responder.Activity.DescribeServed(),
            "each count where it belongs");
    }
}

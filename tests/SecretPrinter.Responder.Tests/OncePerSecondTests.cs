// -----------------------------------------------------------------------------
// OncePerSecondTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-12, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder does not multicast a record on an interface
//   again until a second has passed since it last multicast it there (README
//   REQ-ADV-027; RFC 6762 section 6). RFC 6762 section 6: "a Multicast DNS
//   responder MUST NOT (except in the one special case of answering probe
//   queries) multicast a record on a given interface until at least one second
//   has elapsed since the last time that record was multicast on that
//   particular interface", and, for that special case, "when responding via
//   multicast to a probe, a Multicast DNS responder is only required to delay
//   its transmission as necessary to ensure an interval of at least 250 ms
//   since the last time the record was multicast on that interface". Until
//   2026-10-12 the responder kept no record of what it had sent, and answered
//   the same question five times within a millisecond when asked five times
//   (docs/findings/2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md,
//   item 2).
//
//   What these tests take as the rule, each part of it a decision recorded in
//   README REQ-ADV-027:
//   - An answer leaves out every record multicast on that interface less than
//     a second before, additionals included, and sends nothing if nothing is
//     left. RFC 6762 section 6 gives the reason that is enough: "A querier that
//     did not receive and cache the previous transmission will retry its
//     request and receive a subsequent response."
//   - An announcement and a goodbye wait until the second is up, so that each
//     still carries every record.
//   - An answer to a probe waits until 250 ms have passed.
//   - An adapter's IPv4 entry and its IPv6 companion are one interface, as the
//     responder already treats them for RFC 6762 section 6.2 (the remarks on
//     AdditionalsFor in MdnsResponder.cs).
//   - A unicast answer to a legacy querier is not a multicast: it is neither
//     limited nor counted.
//
//   Each test says in its name what it expects. Those that expect a record to
//   be sent although it was multicast recently, or something sent that the
//   rule does not touch, are there so the rule cannot be made to pass by
//   holding back too much.
//
// The clock:
//   ManualClock (tests/SecretPrinter.TestKit) is a TimeProvider whose
//   timestamps move only when a test moves them, and whose Delay moves them by
//   the wait asked for and returns at once, recording the wait. The responder
//   reads elapsed time from GetTimestamp, which on the real clock cannot go
//   backwards; the clock's wall time is kept apart so that one test can move it
//   backwards and show that it is not what decides.
//
// Changed 2026-10-13 by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, for REQ-SEC-017: the two tests of a legacy unicast
// answer now ask from 192.168.1.41, on the client adapter's network, through a
// new source parameter of Ask. They asked from 192.0.2.41, which is not on it,
// and since that day a legacy querier off the network is not answered. The
// test of the stop summary expects the line's new last count. Reviewed by a
// human before merge.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class OncePerSecondTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>A second client-side adapter. The address is from RFC 5737's documentation range.</summary>
    private static readonly MdnsInterface OtherNic =
        new("Ethernet 3", IPAddress.Parse("192.0.2.10"), 21, AddressFamily.InterNetwork);

    private static readonly DnsName ServiceType = DnsName.Parse("_ipp._tcp.local");
    private static readonly DnsName Subtype = DnsName.Parse("_universal._sub._ipp._tcp.local");

    private sealed record Setup(
        MdnsResponder Responder, FakeTransport Transport, ManualClock Clock, Advertisement Advertisement,
        List<long> SentAt);

    /// <summary>A responder on the client adapter, answering over IPv4 and its IPv6 companion.</summary>
    private static Setup Build()
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6, OtherNic);
        var clock = new ManualClock();
        var sentAt = new List<long>();
        transport.OnSend = _ => sentAt.Add(clock.GetTimestamp());
        var responder = new MdnsResponder(
            transport,
            [
                new AdvertisedInterface(ClientNic, advertisement, ClientNicV6),
                new AdvertisedInterface(OtherNic, advertisement),
            ],
            clock: clock,
            delay: clock.Delay);
        return new Setup(responder, transport, clock, advertisement, sentAt);
    }

    /// <summary>A multicast query (source port 5353), or a legacy one from another port.</summary>
    private static MdnsDatagram Ask(
        DnsName name, DnsRecordType type, MdnsInterface arrivedOn, int sourcePort = 5353, string source = "192.0.2.41")
    {
        byte[] payload = new DnsQueryBuilder(0x2A2A)
            .AddQuestion(name, type, requestUnicastResponse: false)
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(source), sourcePort), arrivedOn.Index, arrivedOn);
    }

    /// <summary>A probe from another device for this name, proposing its own address for it.</summary>
    private static MdnsDatagram ProbeFor(DnsName name, MdnsInterface arrivedOn)
    {
        var proposed = new OutgoingRecord(
            name, DnsRecordType.A, 120, CacheFlush: false, new AddressPayload(IPAddress.Parse("192.0.2.77")));
        byte[] payload = new DnsQueryBuilder(0)
            .AddQuestion(name, DnsRecordType.Any, requestUnicastResponse: false)
            .AddAuthority(proposed)
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse("192.0.2.77"), 5353), arrivedOn.Index, arrivedOn);
    }

    /// <summary>A query for these names and types carrying this record as a known answer at its full TTL.</summary>
    private static MdnsDatagram AskCarrying(
        (DnsName Name, DnsRecordType Type)[] questions, OutgoingRecord known, MdnsInterface arrivedOn)
    {
        var builder = new DnsResponseBuilder(0);
        foreach ((DnsName name, DnsRecordType type) in questions)
        {
            builder.AddQuestion(name, type, 1);
        }

        byte[] message = builder.AddAnswer(known with { CacheFlush = false }).Build();

        // A query, not a response: the response and authoritative bits cleared,
        // as KnownAnswerTests does.
        message[2] = 0;
        message[3] = 0;

        DnsMessage parsed = DnsMessage.Parse(message, message.Length);
        Assert.False(parsed.IsResponse, "the helper made a query");
        Assert.Equal(1, parsed.Answers.Count, "carrying one known answer");

        return new MdnsDatagram(message, new IPEndPoint(IPAddress.Parse("192.0.2.41"), 5353), arrivedOn.Index, arrivedOn);
    }

    private static bool Handle(Setup s, MdnsDatagram datagram) =>
        s.Responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static string Names(SentDatagram sent) =>
        string.Join("|", sent.Parsed.AllRecords.Select(r => $"{r.Name} {r.Type}"));

    // ---- Answers -------------------------------------------------------------

    [TestCase("A question asked again within a second is not answered again by multicast")]
    [Requirement("REQ-ADV-027")]
    public static void A_record_multicast_less_than_a_second_ago_is_not_multicast_again()
    {
        Setup s = Build();

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)), "the first question is answered");
        s.Clock.Advance(TimeSpan.FromMilliseconds(500));
        bool second = Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));

        Assert.False(second, "RFC 6762 s6: the same records, half a second later, are not multicast again");
        Assert.Equal(1, s.Transport.Sent.Count, "one response in all");

        ResponderActivity activity = s.Responder.Activity;
        Assert.Equal(2, activity.QueriesSeen, "both questions were seen");
        Assert.Equal(1, activity.QueriesAnswered, "one was answered");
        Assert.Equal(1, activity.HeldBackWithinASecond, "and one is counted as held back");
        Assert.Equal(0, activity.IgnoredNotOurs, "not as a question about another name");
        Assert.Equal(0, activity.SuppressedByKnownAnswers, "nor as one that carried its answer");
    }

    [TestCase("At 999 ms the record is still held back")]
    [Requirement("REQ-ADV-027")]
    public static void At_999_ms_the_record_is_still_held_back()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(999));

        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "999 ms is less than the second RFC 6762 s6 asks for");
    }

    [TestCase("A second after the last multicast the record is sent again")]
    public static void A_second_after_the_last_multicast_the_record_is_sent_again()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "at least one second has elapsed, which is all RFC 6762 s6 asks");
        Assert.Equal(Names(s.Transport.Sent[0]), Names(s.Transport.Sent[1]), "with the same records as the first time");
        Assert.Equal(0, s.Responder.Activity.HeldBackWithinASecond, "nothing held back");
    }

    [TestCase("Only the records multicast within the second are left out of an answer")]
    [Requirement("REQ-ADV-027")]
    public static void Only_the_records_multicast_within_the_second_are_left_out()
    {
        Setup s = Build();

        // The subtype's answer is its PTR, with the instance's SRV and TXT, the
        // host's A and the host's NSEC as additionals.
        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(300));

        // The service type's answer is another PTR, with the same additionals.
        Assert.True(Handle(s, Ask(ServiceType, DnsRecordType.Ptr, ClientNic)),
            "the service type's PTR has not been multicast, so it is sent");

        DnsMessage second = s.Transport.Sent[1].Parsed;
        Assert.Equal($"{ServiceType} Ptr", string.Join("|", second.AllRecords.Select(r => $"{r.Name} {r.Type}")),
            "the PTR alone: the SRV, TXT, A and NSEC went out 300 ms before and are left out");
        Assert.Equal(0, s.Responder.Activity.HeldBackWithinASecond,
            "a query that was answered is not counted as held back");
    }

    [TestCase("An adapter's IPv4 entry and its IPv6 companion are one interface")]
    [Requirement("REQ-ADV-027")]
    public static void Held_back_over_ipv6_after_an_answer_over_ipv4_on_the_same_adapter()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(5));

        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNicV6)),
            "the same records were multicast on this adapter 5 ms before, over IPv4");
        Assert.Equal(1, s.Transport.Sent.Count, "nothing went out over IPv6");
    }

    [TestCase("Another adapter is another interface")]
    public static void Another_adapter_is_another_interface()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, OtherNic)),
            "RFC 6762 s6 limits a record on a given interface; this is another one");
        Assert.True(s.Transport.Sent[1].Via.Matches(OtherNic), "answered on the other adapter");
    }

    [TestCase("A legacy unicast answer is not limited by the one-second rule")]
    public static void A_legacy_unicast_answer_is_not_limited()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic, sourcePort: 50000, source: "192.168.1.41")),
            "a legacy querier is answered by unicast, which is not a multicast");
        Assert.False(s.Transport.Sent[1].WasMulticast, "by unicast");
        Assert.Equal(Names(s.Transport.Sent[0]), Names(s.Transport.Sent[1]), "with every record");
    }

    [TestCase("A legacy unicast answer does not count as a multicast")]
    public static void A_legacy_unicast_answer_does_not_count_as_a_multicast()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic, sourcePort: 50000, source: "192.168.1.41"));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "nothing has been multicast yet, so a multicast question is answered");
        Assert.True(s.Transport.Sent[1].WasMulticast, "by multicast");
    }

    [TestCase("A query whose every answer it carries is counted as that, not as held back")]
    public static void A_query_carrying_every_answer_is_counted_as_known_not_held_back()
    {
        Setup s = Build();
        OutgoingRecord ptr = s.Advertisement.Records.Single(r => r.Name.Equals(Subtype) && r.Type == DnsRecordType.Ptr);

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Handle(s, AskCarrying([(Subtype, DnsRecordType.Ptr)], ptr, ClientNic));

        ResponderActivity activity = s.Responder.Activity;
        Assert.Equal(1, activity.SuppressedByKnownAnswers,
            "RFC 6762 s7.1 alone would have sent nothing, so that is what it is counted as");
        Assert.Equal(0, activity.HeldBackWithinASecond, "and not as held back");
    }

    [TestCase("A query left unanswered partly by its known answers and partly by the second is counted as held back")]
    [Requirement("REQ-ADV-027")]
    public static void A_query_left_unanswered_by_both_rules_is_counted_as_held_back()
    {
        Setup s = Build();
        OutgoingRecord ptr = s.Advertisement.Records.Single(r => r.Name.Equals(Subtype) && r.Type == DnsRecordType.Ptr);
        DnsName host = Host(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        // The subtype's PTR is carried as known; the host's A went out 100 ms
        // before. Without the one-second rule the A would have been sent.
        Assert.False(Handle(s, AskCarrying([(Subtype, DnsRecordType.Ptr), (host, DnsRecordType.A)], ptr, ClientNic)),
            "nothing is left to send");

        ResponderActivity activity = s.Responder.Activity;
        Assert.Equal(1, activity.HeldBackWithinASecond, "counted as held back, because something would have gone out but for it");
        Assert.Equal(0, activity.SuppressedByKnownAnswers, "and not as a query that carried every answer");
    }

    [TestCase("The clock that decides is the one that cannot go backwards")]
    [Requirement("REQ-ADV-027")]
    public static void The_wall_clock_does_not_decide()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));

        // The wall clock is put an hour forward and the timestamps half a second.
        s.Clock.Wall += TimeSpan.FromHours(1);
        s.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "half a second has passed, whatever the wall clock says");

        // Then the wall clock is put two hours back and the timestamps a second on.
        s.Clock.Wall -= TimeSpan.FromHours(2);
        s.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "a second has passed, whatever the wall clock says");
    }

    [TestCase("The stop summary counts the queries held back")]
    [Requirement("REQ-ADV-027")]
    public static void The_stop_summary_counts_the_queries_held_back()
    {
        Setup s = Build();

        // One answered, two held back, three for other services: three different
        // counts, so a line that gave one in place of another would read
        // differently.
        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        for (int i = 0; i < 2; i++)
        {
            s.Clock.Advance(TimeSpan.FromMilliseconds(100));
            Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        }

        for (int i = 0; i < 3; i++)
        {
            Handle(s, Ask(DnsName.Parse("_airplay._tcp.local"), DnsRecordType.Ptr, ClientNic));
        }

        Assert.Equal(
            "Served 1 quer(ies) of 6 seen; 3 were for other services and were ignored; "
            + "0 carried every answer already and were not answered (RFC 6762 s7.1); "
            + "2 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); "
            + "0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5).",
            s.Responder.Activity.DescribeServed(),
            "each count where it belongs");
    }

    [TestCase("A send that fails takes back only what it recorded")]
    [Requirement("REQ-ADV-027")]
    public static void A_failed_send_takes_back_only_what_it_recorded()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(300));

        // An answer to a probe may follow the A record by 250 ms. This one
        // cannot be sent.
        Action<SentDatagram>? recordTimes = s.Transport.OnSend;
        s.Transport.OnSend = _ => throw new SocketException((int)SocketError.NetworkUnreachable);
        Assert.Throws<SocketException>(() => Handle(s, ProbeFor(host, ClientNic)), "the answer to the probe is not sent");
        s.Transport.OnSend = recordTimes;

        s.Clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.False(Handle(s, Ask(host, DnsRecordType.A, ClientNic)),
            "the A record went out 500 ms before, and a send that failed does not change when");

        s.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(Handle(s, Ask(host, DnsRecordType.A, ClientNic)),
            "a second after it went out it is sent again: the send that failed is not counted as one");
    }

    // ---- Answers to probes ---------------------------------------------------

    [TestCase("A probe is answered at once when 250 ms have passed")]
    public static void A_probe_is_answered_at_once_when_250_ms_have_passed()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(300));

        Assert.True(Handle(s, ProbeFor(host, ClientNic)),
            "RFC 6762 s6: an answer to a probe needs 250 ms since the last multicast, not a second");
        Assert.Equal(0, s.Clock.Waits.Count, "and nothing waited");
        Assert.True(s.Transport.Sent[1].Parsed.Answers.Any(r => r.Type == DnsRecordType.A),
            "the A record defends the name, although it went out 300 ms before");
    }

    [TestCase("A probe within 250 ms is answered when 250 ms have passed")]
    [Requirement("REQ-ADV-027")]
    public static void A_probe_within_250_ms_is_answered_when_250_ms_have_passed()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, Ask(host, DnsRecordType.A, ClientNic));
        long first = s.SentAt[0];
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, ProbeFor(host, ClientNic)), "the probe is answered");
        Assert.Equal(TimeSpan.FromMilliseconds(250),
            s.Clock.GetElapsedTime(first, s.SentAt[1]),
            "RFC 6762 s6: delayed until 250 ms after the A record was last multicast, and no longer");
        Assert.True(s.Transport.Sent[1].Parsed.Answers.Any(r => r.Type == DnsRecordType.A),
            "with the A record that defends the name");
    }

    // ---- Announcements and goodbyes -----------------------------------------

    [TestCase("Announcements are a second apart whatever interval they are given")]
    [Requirement("REQ-ADV-027")]
    public static void Announcements_are_a_second_apart_whatever_interval_they_are_given()
    {
        Setup s = Build();

        s.Responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        // Announcements go out on each advertised interface's IPv4 entry: two
        // adapters, three rounds.
        List<long> onClient = [.. s.Transport.Sent
            .Select((sent, i) => (sent, at: s.SentAt[i]))
            .Where(x => x.sent.Via.Matches(ClientNic))
            .Select(x => x.at)];
        Assert.Equal(MdnsResponder.AnnouncementCount, onClient.Count, "three announcements on the client adapter");
        Assert.Equal(TimeSpan.FromSeconds(1), s.Clock.GetElapsedTime(onClient[0], onClient[1]),
            "the second a second after the first");
        Assert.Equal(TimeSpan.FromSeconds(1), s.Clock.GetElapsedTime(onClient[1], onClient[2]),
            "the third a second after the second");
        Assert.True(s.Transport.Sent.All(sent => sent.Parsed.Answers.Count == s.Advertisement.Records.Count),
            "and each carries every record");
    }

    [TestCase("A conflict heard while an announcement waits stops it")]
    [Requirement("REQ-ADV-024")]
    public static void A_conflict_heard_while_an_announcement_waits_stops_it()
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var clock = new ManualClock();
        MdnsResponder? responder = null;
        DnsName host = Host(advertisement);

        // The wait: while the announcement waits, another device answers for
        // the host name with an address this responder would not publish.
        Task Delay(TimeSpan wait, CancellationToken token)
        {
            byte[] theirs = new DnsResponseBuilder()
                .AddAnswer(new OutgoingRecord(host, DnsRecordType.A, 120, CacheFlush: true,
                    new AddressPayload(IPAddress.Parse("192.0.2.77"))))
                .Build();
            responder!.HandleAsync(
                new MdnsDatagram(theirs, new IPEndPoint(IPAddress.Parse("192.0.2.77"), 5353), ClientNic.Index, ClientNic),
                token).GetAwaiter().GetResult();
            return clock.Delay(wait, token);
        }

        responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)], clock: clock, delay: Delay);

        responder.HandleAsync(Ask(Subtype, DnsRecordType.Ptr, ClientNic), CancellationToken.None).GetAwaiter().GetResult();
        responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        Assert.NotNull(responder.Conflict, "the other device's answer was heard as a conflict");
        Assert.Equal(1, transport.Sent.Count,
            "REQ-ADV-024: nothing is announced once a conflict is held, though it was heard during the wait REQ-ADV-027 makes");
    }

    [TestCase("An answer within a second of an announcement is held back")]
    [Requirement("REQ-ADV-027")]
    public static void An_answer_within_a_second_of_an_announcement_is_held_back()
    {
        Setup s = Build();

        s.Responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
        int announced = s.Transport.Sent.Count;
        s.Clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.False(Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic)),
            "every record was announced half a second before");
        Assert.Equal(announced, s.Transport.Sent.Count, "nothing more was sent");
    }

    [TestCase("An announcement waits until a second after an answer")]
    [Requirement("REQ-ADV-027")]
    public static void An_announcement_waits_until_a_second_after_an_answer()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        long answered = s.SentAt[0];
        s.Clock.Advance(TimeSpan.FromMilliseconds(400));

        s.Responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        int firstOnClient = s.Transport.Sent.FindIndex(1, sent => sent.Via.Matches(ClientNic));
        Assert.Equal(TimeSpan.FromSeconds(1), s.Clock.GetElapsedTime(answered, s.SentAt[firstOnClient]),
            "the first announcement on that adapter waited until a second after the answer");
        Assert.Equal(s.Advertisement.Records.Count, s.Transport.Sent[firstOnClient].Parsed.Answers.Count,
            "and still carries every record");
    }

    [TestCase("A goodbye waits until a second after the last multicast, and carries every record")]
    [Requirement("REQ-ADV-027")]
    public static void A_goodbye_waits_until_a_second_after_the_last_multicast()
    {
        Setup s = Build();

        Handle(s, Ask(Subtype, DnsRecordType.Ptr, ClientNic));
        long answered = s.SentAt[0];
        s.Clock.Advance(TimeSpan.FromMilliseconds(200));

        s.Responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

        int goodbyeOnClient = s.Transport.Sent.FindIndex(1, sent => sent.Via.Matches(ClientNic));
        Assert.Equal(TimeSpan.FromSeconds(1), s.Clock.GetElapsedTime(answered, s.SentAt[goodbyeOnClient]),
            "the goodbye on that adapter waited until a second after the answer");

        DnsMessage goodbye = s.Transport.Sent[goodbyeOnClient].Parsed;
        Assert.Equal(
            s.Advertisement.Records.Count + MdnsResponder.NsecRecordsFor(s.Advertisement).Count,
            goodbye.Answers.Count,
            "every record and both NSEC records, as REQ-LIF-003 asks");
        Assert.True(goodbye.Answers.All(r => r.Ttl == 0), "each with TTL 0");
    }
}

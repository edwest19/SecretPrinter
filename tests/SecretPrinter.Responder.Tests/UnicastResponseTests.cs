// -----------------------------------------------------------------------------
// UnicastResponseTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-13, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder answers by unicast a question that asks for a
//   unicast answer, as RFC 6762 section 5.4 says it should, and by multicast
//   when it has not multicast the record recently (README REQ-ADV-028). RFC
//   6762 section 5.4: "When receiving a question with the unicast-response bit
//   set, a responder SHOULD usually respond with a unicast packet directed back
//   to the querier. However, if the responder has not multicast that record
//   recently (within one quarter of its TTL), then the responder SHOULD instead
//   multicast the response so as to keep all the peer caches up to date, and to
//   permit passive conflict detection." Until 2026-10-13 such a question, from
//   port 5353, was answered by multicast whatever the bit said
//   (docs/findings/2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md,
//   item 4).
//
//   What these tests take as the rule, each part of it a decision recorded in
//   README REQ-ADV-028:
//   - The choice is made record by record. A record that a question with the
//     bit set asks for, and no question without it, goes by unicast when it was
//     multicast on that adapter, over either transport, less than a quarter of
//     its TTL before. Every other record goes by multicast, under the
//     one-second rule (REQ-ADV-027).
//   - The unicast packet is made as the multicast one would be: identifier 0,
//     no question, TTLs and cache-flush bits as published. RFC 6762 section
//     5.4: "Unicast replies are subject to all the same packet generation rules
//     as multicast replies".
//   - It goes to the querier's address and port, through the interface the
//     query arrived on, only when that address is on the network the query
//     arrived from (REQ-SEC-017). Otherwise its records go by multicast.
//   - It is not a multicast: it is not recorded as one, and the one-second rule
//     does not hold it back.
//   - An answer to another device's probe that asks for a unicast answer goes
//     by unicast at once, whether or not the record was multicast recently:
//     section 5.4 says "the responder should always generate the requested
//     unicast response".
//
//   The tests that expect a unicast answer, or the new count, failed before the
//   change. Those that expect a multicast passed before and after: they are
//   there so that the rule cannot be made to pass by answering everything by
//   unicast.
//
// The clock:
//   ManualClock, as in OncePerSecondTests: its timestamps move only when a test
//   moves them, and its Delay moves them by the wait asked for and returns at
//   once, recording the wait.
//
// Addresses:
//   The client adapter is MdnsResponderTests.ClientNic, 192.168.1.234, and its
//   IPv6 companion. The querier is 192.168.1.41, as in the other responder
//   tests, or fe80::41 over IPv6, made up. A second adapter, 192.0.2.10, and a
//   querier off every local network, 203.0.113.9, are from RFC 5737's
//   documentation ranges. The probing device is 192.168.1.77, made up.
//
// Changed later still on 2026-10-13 by Claude (Anthropic model, Claude Opus
// 5.5) at the direction of Edwin West, for REQ-ADV-029: the test of the stop
// summary expects the count added at the end of the line, of the queries
// answered after a random wait. All three answered here are for a PTR, so all
// three are counted. The responders here take the default random wait, which
// moves the clock by 20 to 120 ms before each such answer; every test in this
// file passed with that wait fixed at 20 ms, at 120 ms, and alternating
// between the two either way round. Reviewed by a human before merge.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class UnicastResponseTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>A second client-side adapter, from RFC 5737's documentation range.</summary>
    private static readonly MdnsInterface OtherNic =
        new("Ethernet 3", IPAddress.Parse("192.0.2.10"), 21, AddressFamily.InterNetwork);

    private static readonly DnsName Subtype = DnsName.Parse("_universal._sub._ipp._tcp.local");

    private const string Querier = "192.168.1.41";

    private sealed record Setup(MdnsResponder Responder, FakeTransport Transport, ManualClock Clock, Advertisement Advertisement);

    private static Setup Build()
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6, OtherNic);
        var clock = new ManualClock();
        var responder = new MdnsResponder(
            transport,
            [
                new AdvertisedInterface(ClientNic, advertisement, ClientNicV6),
                new AdvertisedInterface(OtherNic, advertisement),
            ],
            clock: clock,
            delay: clock.Delay);
        return new Setup(responder, transport, clock, advertisement);
    }

    /// <summary>A query of these questions, each with or without the unicast-response bit.</summary>
    private static MdnsDatagram Ask(
        (DnsName Name, DnsRecordType Type, bool Unicast)[] questions,
        MdnsInterface arrivedOn,
        string source = Querier,
        int port = 5353)
    {
        var builder = new DnsQueryBuilder(0x2A2A);
        foreach ((DnsName name, DnsRecordType type, bool unicast) in questions)
        {
            builder.AddQuestion(name, type, requestUnicastResponse: unicast);
        }

        return new MdnsDatagram(builder.Build(), new IPEndPoint(IPAddress.Parse(source), port), arrivedOn.Index, arrivedOn);
    }

    /// <summary>One question that asks for a unicast answer.</summary>
    private static MdnsDatagram AskUnicast(DnsName name, DnsRecordType type, MdnsInterface arrivedOn, string source = Querier) =>
        Ask([(name, type, true)], arrivedOn, source);

    /// <summary>One question that asks for a multicast answer.</summary>
    private static MdnsDatagram AskMulticast(DnsName name, DnsRecordType type, MdnsInterface arrivedOn) =>
        Ask([(name, type, false)], arrivedOn);

    /// <summary>A probe from another device for this name, proposing its own address for it.</summary>
    private static MdnsDatagram ProbeFor(DnsName name, MdnsInterface arrivedOn, bool unicast)
    {
        var proposed = new OutgoingRecord(
            name, DnsRecordType.A, 120, CacheFlush: false, new AddressPayload(IPAddress.Parse("192.168.1.77")));
        byte[] payload = new DnsQueryBuilder(0)
            .AddQuestion(name, DnsRecordType.Any, requestUnicastResponse: unicast)
            .AddAuthority(proposed)
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse("192.168.1.77"), 5353), arrivedOn.Index, arrivedOn);
    }

    private static bool Handle(Setup s, MdnsDatagram datagram) =>
        s.Responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static string Names(IEnumerable<DnsRecord> records) =>
        string.Join("|", records.Select(r => $"{r.Name} {r.Type}"));

    // ---- The choice ----------------------------------------------------------

    [TestCase("A question asking for a unicast answer, for a record never multicast, is answered by multicast")]
    public static void A_record_never_multicast_is_multicast()
    {
        Setup s = Build();

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic)), "the question is answered");
        Assert.True(s.Transport.Sent.Single().WasMulticast,
            "RFC 6762 s5.4: the record has not been multicast recently, so it is multicast, to keep the peer caches up to date");
        Assert.Equal(0, s.Responder.Activity.AnsweredByUnicastOnRequest, "nothing answered by unicast");
    }

    [TestCase("A question asking for a unicast answer, for a record multicast within a quarter of its TTL, is answered by unicast")]
    [Requirement("REQ-ADV-028")]
    public static void A_record_multicast_recently_is_sent_by_unicast()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic)), "the question is answered");
        Assert.Equal(2, s.Transport.Sent.Count, "one more datagram");
        SentDatagram sent = s.Transport.Sent[1];
        Assert.False(sent.WasMulticast, "RFC 6762 s5.4: by unicast, the PTR having been multicast 10 s before");
        Assert.Equal(new IPEndPoint(IPAddress.Parse(Querier), 5353), sent.Destination, "to the querier's address and port");
        Assert.True(sent.Via.Matches(ClientNic), "through the adapter the question arrived on");
        Assert.Equal(1, s.Responder.Activity.AnsweredByUnicastOnRequest, "counted as answered by unicast as asked");
        Assert.Equal(2, s.Responder.Activity.QueriesAnswered, "both queries answered");
    }

    [TestCase("The unicast answer is made as the multicast one: identifier 0, no question, TTLs and cache-flush bits as published, the same additionals")]
    [Requirement("REQ-ADV-028")]
    public static void The_unicast_answer_is_made_as_the_multicast_one()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));

        DnsMessage multicast = s.Transport.Sent[0].Parsed;
        DnsMessage unicast = s.Transport.Sent[1].Parsed;
        Assert.False(s.Transport.Sent[1].WasMulticast, "the second answer went by unicast");
        Assert.Equal((ushort)0, unicast.Id, "identifier 0, as in a multicast response, not the query's");
        Assert.Equal(0, unicast.Questions.Count, "no question section: RFC 6762 s6");
        Assert.Equal(Names(multicast.AllRecords), Names(unicast.AllRecords), "the same records in the same sections");
        foreach (DnsRecord record in unicast.AllRecords)
        {
            OutgoingRecord? published = s.Advertisement.Records.FirstOrDefault(r => r.Name.Equals(record.Name) && r.Type == record.Type);
            if (published is not null)
            {
                Assert.Equal(published.Ttl, record.Ttl, $"{record.Name} {record.Type} keeps its TTL; only a legacy answer caps it");
                Assert.Equal(published.CacheFlush, record.CacheFlush, $"{record.Name} {record.Type} keeps its cache-flush bit");
            }
        }
    }

    [TestCase("Just inside a quarter of the TTL a record goes by unicast")]
    [Requirement("REQ-ADV-028")]
    public static void Just_inside_a_quarter_of_the_ttl_it_goes_by_unicast()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, AskMulticast(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(30) - TimeSpan.FromMilliseconds(1));

        Handle(s, AskUnicast(host, DnsRecordType.A, ClientNic));
        Assert.False(s.Transport.Sent[1].WasMulticast, "the A record's TTL is 120 s; 29.999 s is within a quarter of it");
    }

    [TestCase("At a quarter of the TTL a record goes by multicast")]
    [Requirement("REQ-ADV-028")]
    public static void At_a_quarter_of_the_ttl_it_goes_by_multicast()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, AskMulticast(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(30));

        Handle(s, AskUnicast(host, DnsRecordType.A, ClientNic));
        Assert.True(s.Transport.Sent[1].WasMulticast, "30 s is a quarter of 120 s, and is no longer \"recently\"");
    }

    [TestCase("The quarter is of each record's own TTL: a PTR of 4,500 s goes by unicast 1,124 s after it was multicast")]
    [Requirement("REQ-ADV-028")]
    public static void The_quarter_is_of_each_records_own_ttl()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(1124));

        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));
        SentDatagram sent = s.Transport.Sent[1];
        Assert.False(sent.WasMulticast, "the PTR's quarter is 1,125 s");
        Assert.Equal($"{Subtype} Ptr", Names(sent.Parsed.Answers), "the PTR by unicast");

        // The SRV, TXT, A and NSEC go with the PTR as its additionals, in the
        // same packet, whatever their own TTLs: an additional goes with the
        // answer it belongs to.
        Assert.Equal(2, s.Transport.Sent.Count, "nothing by multicast");
        Assert.Equal(4, sent.Parsed.Additionals.Count, "the PTR's four additionals came with it by unicast");
    }

    [TestCase("A record multicast less than a second before goes by unicast, and is not held back")]
    [Requirement("REQ-ADV-028")]
    public static void Within_a_second_it_goes_by_unicast_and_is_not_held_back()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic)),
            "the one-second rule holds back a multicast; this answer is not one");
        Assert.False(s.Transport.Sent[1].WasMulticast, "by unicast");
        Assert.Equal(0, s.Responder.Activity.HeldBackWithinASecond, "nothing held back");
    }

    [TestCase("A unicast answer is not limited by the one-second rule")]
    [Requirement("REQ-ADV-028")]
    public static void A_unicast_answer_is_not_limited()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(2));
        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic)), "asked again 100 ms later");
        Assert.Equal(3, s.Transport.Sent.Count, "answered again");
        Assert.False(s.Transport.Sent[2].WasMulticast, "by unicast again");
    }

    [TestCase("A unicast answer does not count as a multicast")]
    [Requirement("REQ-ADV-028")]
    public static void A_unicast_answer_does_not_count_as_a_multicast()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.True(Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic)),
            "the last multicast of the PTR was 10.5 s before; the unicast 0.5 s before is not one");
        Assert.True(s.Transport.Sent[2].WasMulticast, "by multicast");
    }

    [TestCase("A record that a question without the bit also asks for goes by multicast")]
    [Requirement("REQ-ADV-028")]
    public static void A_record_also_asked_for_by_multicast_goes_by_multicast()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, Ask([(Subtype, DnsRecordType.Ptr, true), (Subtype, DnsRecordType.Ptr, false)], ClientNic)),
            "answered");
        Assert.Equal(2, s.Transport.Sent.Count, "one datagram");
        Assert.True(s.Transport.Sent[1].WasMulticast, "the second question asks for a multicast answer, and gets one");
        Assert.Equal(0, s.Responder.Activity.AnsweredByUnicastOnRequest, "nothing answered by unicast");
    }

    [TestCase("In one query, only the record asked for by unicast alone, and multicast recently, goes by unicast")]
    [Requirement("REQ-ADV-028")]
    public static void Only_the_record_asked_for_by_unicast_alone_goes_by_unicast()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, AskMulticast(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, Ask([(host, DnsRecordType.A, true), (Subtype, DnsRecordType.Ptr, false)], ClientNic)),
            "answered");

        SentDatagram unicast = s.Transport.Sent.Single(d => !d.WasMulticast);
        SentDatagram multicast = s.Transport.Sent.Skip(1).Single(d => d.WasMulticast);
        Assert.Equal($"{host} A", Names(unicast.Parsed.Answers), "the A record, multicast 10 s before, by unicast");
        Assert.Equal($"{Subtype} Ptr", Names(multicast.Parsed.Answers), "the PTR, asked for by multicast, by multicast");
        Assert.False(multicast.Parsed.AllRecords.Any(r => r.Type == DnsRecordType.A),
            "a record that answers the query goes once, in the packet that answers with it");
        Assert.False(unicast.Parsed.Additionals.Any(r => multicast.Parsed.AllRecords.Any(m => m.Name.Equals(r.Name) && m.Type == r.Type)),
            "the unicast packet leaves out what the multicast one carries");
        Assert.Equal(1, s.Responder.Activity.AnsweredByUnicastOnRequest, "counted once");
        Assert.Equal(2, s.Responder.Activity.QueriesAnswered, "and the query once as answered");
    }

    [TestCase("A unicast answer that cannot be sent takes back the multicast noted with it, which is not sent either")]
    [Requirement("REQ-ADV-028")]
    public static void A_unicast_answer_that_fails_takes_back_the_multicast_noted_with_it()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, AskMulticast(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        // The A record goes by unicast and the PTR by multicast; the unicast
        // send fails, and the multicast is never sent.
        s.Transport.OnSend = sent =>
        {
            if (!sent.WasMulticast)
            {
                throw new SocketException((int)SocketError.NetworkUnreachable);
            }
        };
        Assert.Throws<SocketException>(
            () => Handle(s, Ask([(host, DnsRecordType.A, true), (Subtype, DnsRecordType.Ptr, false)], ClientNic)),
            "the unicast answer is not sent");
        s.Transport.OnSend = null;
        Assert.False(s.Transport.Sent.Skip(1).Any(d => d.WasMulticast), "nor is the multicast that would have followed it");

        s.Clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic)),
            "the PTR was never multicast, so half a second later it is: the failed answer is not counted as one");
    }

    // ---- Where it goes -------------------------------------------------------

    [TestCase("A querier off the network it asked from gets the records by multicast instead")]
    [Requirement("REQ-ADV-028")]
    public static void A_querier_off_the_network_gets_a_multicast()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic, source: "203.0.113.9")), "answered");
        Assert.True(s.Transport.Sent[1].WasMulticast,
            "REQ-SEC-017: no answer by unicast to an address off the network, so the records go to the group");
        Assert.Equal(0, s.Responder.Activity.AnsweredByUnicastOnRequest, "nothing answered by unicast");
        Assert.Equal(0, s.Responder.Activity.RefusedOffNetwork, "and nothing refused: the query was answered");
    }

    [TestCase("Over IPv6, a link-local querier is answered by unicast through the IPv6 entry, a multicast over IPv4 counting for the adapter")]
    [Requirement("REQ-ADV-028")]
    public static void Over_ipv6_a_link_local_querier_is_answered_by_unicast()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNicV6, source: $"fe80::41%{ClientNicV6.Index}")),
            "answered");
        SentDatagram sent = s.Transport.Sent[1];
        Assert.False(sent.WasMulticast, "by unicast: the PTR was multicast on this adapter 10 s before, over IPv4");
        Assert.Equal(new IPEndPoint(IPAddress.Parse($"fe80::41%{ClientNicV6.Index}"), 5353), sent.Destination, "to the querier");
        Assert.True(sent.Via.Matches(ClientNicV6), "over IPv6, the transport the question came by");
    }

    [TestCase("Another adapter is another interface: a record multicast on one is not recent on the other")]
    [Requirement("REQ-ADV-028")]
    public static void Another_adapter_is_another_interface()
    {
        Setup s = Build();

        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.True(Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, OtherNic, source: "192.0.2.41")), "answered");
        Assert.True(s.Transport.Sent[1].WasMulticast, "RFC 6762 s5.4 asks about the record on that interface, never multicast there");
        Assert.True(s.Transport.Sent[1].Via.Matches(OtherNic), "on the other adapter");
    }

    [TestCase("A legacy querier whose question has the bit set is answered as a legacy querier")]
    public static void A_legacy_querier_with_the_bit_set_is_answered_as_one()
    {
        Setup s = Build();

        Assert.True(Handle(s, Ask([(Subtype, DnsRecordType.Ptr, true)], ClientNic, port: 50000)), "answered");
        DnsMessage reply = s.Transport.Sent.Single().Parsed;
        Assert.False(s.Transport.Sent.Single().WasMulticast, "by unicast (RFC 6762 s6.7)");
        Assert.Equal((ushort)0x2A2A, reply.Id, "with its identifier, as a legacy answer");
        Assert.True(reply.AllRecords.All(r => r.Ttl <= MdnsResponder.LegacyUnicastTtl), "with its TTLs capped");
        Assert.Equal(0, s.Responder.Activity.AnsweredByUnicastOnRequest, "a legacy answer is not counted as one asked for by the bit");
    }

    // ---- Probes --------------------------------------------------------------

    [TestCase("A probe asking for a unicast answer is answered by unicast at once, though the record was never multicast")]
    [Requirement("REQ-ADV-028")]
    public static void A_probe_asking_for_unicast_is_answered_by_unicast_at_once()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Assert.True(Handle(s, ProbeFor(host, ClientNic, unicast: true)), "the probe is answered");
        SentDatagram sent = s.Transport.Sent.Single();
        Assert.False(sent.WasMulticast, "RFC 6762 s5.4: \"the responder should always generate the requested unicast response\"");
        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.1.77"), 5353), sent.Destination, "to the probing device");
        Assert.True(sent.Parsed.Answers.Any(r => r.Type == DnsRecordType.A), "with the A record that defends the name");
        Assert.Equal(0, s.Clock.Waits.Count, "at once");
    }

    [TestCase("A probe asking for a unicast answer is answered by unicast within 250 ms of a multicast, without waiting")]
    [Requirement("REQ-ADV-028")]
    public static void A_probe_asking_for_unicast_does_not_wait()
    {
        Setup s = Build();
        DnsName host = Host(s.Advertisement);

        Handle(s, AskMulticast(host, DnsRecordType.A, ClientNic));
        s.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(Handle(s, ProbeFor(host, ClientNic, unicast: true)), "the probe is answered");
        Assert.False(s.Transport.Sent[1].WasMulticast, "by unicast");
        Assert.Equal(0, s.Clock.Waits.Count, "without the 250 ms wait, which is for a multicast");
    }

    [TestCase("A probe without the bit is still answered by multicast")]
    public static void A_probe_without_the_bit_is_answered_by_multicast()
    {
        Setup s = Build();

        Assert.True(Handle(s, ProbeFor(Host(s.Advertisement), ClientNic, unicast: false)), "the probe is answered");
        Assert.True(s.Transport.Sent.Single().WasMulticast, "by multicast, as before");
    }

    // ---- The stop summary ----------------------------------------------------

    [TestCase("The stop summary's first line says how many were answered by unicast as asked")]
    [Requirement("REQ-ADV-028")]
    public static void The_stop_summary_counts_the_unicast_answers()
    {
        Setup s = Build();

        // Three answered, two of them by unicast as asked, and one about
        // another service: different counts, so a line that gave one in place
        // of another would read differently.
        Handle(s, AskMulticast(Subtype, DnsRecordType.Ptr, ClientNic));
        s.Clock.Advance(TimeSpan.FromSeconds(10));
        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));
        Handle(s, AskUnicast(Subtype, DnsRecordType.Ptr, ClientNic));
        Handle(s, AskMulticast(DnsName.Parse("_airplay._tcp.local"), DnsRecordType.Ptr, ClientNic));

        Assert.Equal(
            "Served 3 quer(ies) of 4 seen; 1 were for other services and were ignored; "
            + "0 carried every answer already and were not answered (RFC 6762 s7.1); "
            + "0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); "
            + "0 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5); "
            + "2 of those served were answered by unicast because they asked for it (RFC 6762 s5.4); "
            + "3 of those served were answered after a random wait of 20 to 120 ms, because the answer held a shared record or the query asked more than one question (RFC 6762 s6, s6.3).",
            s.Responder.Activity.DescribeServed(),
            "each count where it belongs");
    }
}

// -----------------------------------------------------------------------------
// NsecAnsweringTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-01, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks when the responder sends an NSEC record, where in the message it
//   goes, and what it says (README REQ-ADV-022).
//
//   The service probes for two names with type ANY and so owns every type at
//   them: its host name, which has the A and AAAA records, and its service
//   instance name, which has the SRV and TXT records. RFC 6762 section 6.1
//   says a responder asked for a type that such a name does not have MUST say
//   so, with an NSEC record listing the types the name does have. Section 6.2
//   says a response that carries a host's address records of one family, when
//   the host has none of the other, SHOULD carry that NSEC as an additional.
//
//   The service-type names (the PTR records) are shared with every other
//   printer on the network. Section 6 forbids a negative answer for a shared
//   name, so none is sent.
//
// Type 65:
//   HTTPS, in IANA's registry. It is here because it is what was measured: on
//   2026-09-29, HTTPS and AAAA questions for the service's host name, from two
//   devices, went unanswered. See
//   docs/findings/2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md.
//   The reader has no name for type 65, so it is written as a number.
//
// Markers:
//   Placed by Claude (Anthropic model, Claude Opus 5.5) at the direction of
//   Edwin West, 2026-10-02, after a capture on FIOS-STB-01 showed an iPhone's
//   type 65 question for the host name answered with the NSEC and a page
//   printed 63 seconds later; see
//   docs/findings/2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md.
//   Until then this header said why the tests carried none. No test changed
//   what it checks. Twenty-five carry REQ-ADV-022. The test of the
//   responder's own NSEC records heard back carries REQ-ADV-024, which is the
//   requirement that says how a heard NSEC is judged. The two tests of what
//   is refused when the responder is built carry none: the README does not
//   state those refusals. Reviewed by a human before merge.
//
//   What the capture showed is the host's NSEC as an answer, and both NSEC
//   records in the goodbye. The instance's NSEC as an answer, the NSEC as an
//   additional and the legacy unicast answer have been seen only here, with a
//   fake transport.
//
// Addresses:
//   fe80::10 is made up, scoped to 13, the IPv6 index of the test adapter.
//   192.168.1.234 is the test adapter's own address, 192.168.1.41 a client's
//   and 192.168.1.50 another device's. No real address appears here.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class NsecAnsweringTests
{
    /// <summary>HTTPS, type 65: a type neither of the service's names has.</summary>
    private const DnsRecordType Https = (DnsRecordType)65;

    private const ushort ClassChaos = 3;
    private const ushort ClassAny = 255;

    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    private static readonly IPAddress LinkLocal = IPAddress.Parse("fe80::10%13");

    private static (MdnsResponder Responder, FakeTransport Transport, Advertisement Advertisement) Build(
        params IPAddress[] linkLocal) => Build(null, linkLocal);

    private static (MdnsResponder Responder, FakeTransport Transport, Advertisement Advertisement) Build(
        Func<bool>? advertising, params IPAddress[] linkLocal)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement(linkLocal);
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)], advertising);
        return (responder, transport, advertisement);
    }

    /// <summary>The proxy's host name, as the advertisement holds it.</summary>
    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    /// <summary>The proxy's service instance name, as the advertisement holds it.</summary>
    private static DnsName Instance(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.Srv).Name;

    /// <summary>A query from a client on the client network, by multicast unless a port is given.</summary>
    private static MdnsDatagram Query(
        MdnsInterface arrivedOn, int sourcePort, params (DnsName Name, DnsRecordType Type)[] questions)
    {
        var builder = new DnsQueryBuilder(0x1234);
        foreach ((DnsName name, DnsRecordType type) in questions)
        {
            builder.AddQuestion(name, type, requestUnicastResponse: false);
        }

        return new MdnsDatagram(
            builder.Build(), new IPEndPoint(IPAddress.Parse("192.168.1.41"), sourcePort), arrivedOn.Index, arrivedOn);
    }

    private static MdnsDatagram Query(params (DnsName Name, DnsRecordType Type)[] questions) =>
        Query(ClientNic, 5353, questions);

    /// <summary>
    /// A query with one question, in the class given. DnsQueryBuilder writes
    /// class IN only, so the class field, the last two bytes of a query with
    /// one question, is overwritten, and read back through the real parser
    /// before it is used.
    /// </summary>
    private static MdnsDatagram QueryInClass(DnsName name, DnsRecordType type, ushort rawClass)
    {
        byte[] message = new DnsQueryBuilder(0).AddQuestion(name, type, requestUnicastResponse: false).Build();
        message[^2] = (byte)(rawClass >> 8);
        message[^1] = (byte)(rawClass & 0xFF);

        Assert.Equal(rawClass, DnsMessage.Parse(message, message.Length).Questions.Single().RawClass,
            "the helper wrote the class where the parser reads it");

        return new MdnsDatagram(
            message, new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), ClientNic.Index, ClientNic);
    }

    private static bool Handle(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Handles a query that must be answered, and returns what was sent.</summary>
    private static SentDatagram Answer(MdnsResponder responder, FakeTransport transport, MdnsDatagram query)
    {
        int before = transport.Sent.Count;
        Assert.True(Handle(responder, query), "the query is about one of the proxy's own names, so it is answered");
        Assert.Equal(before + 1, transport.Sent.Count, "with one message");
        return transport.Sent[^1];
    }

    private static List<DnsRecord> Nsecs(IEnumerable<DnsRecord> records) =>
        [.. records.Where(r => r.Type == DnsRecordType.Nsec)];

    /// <summary>
    /// Checks one NSEC record in full: whose it is, that it is in the restricted
    /// form with its own name as the next domain name, the types it lists, its
    /// TTL and its cache-flush bit.
    /// </summary>
    private static void AssertNsec(
        DnsRecord record, DnsName name, DnsRecordType[] types, uint ttl, bool cacheFlush, string where)
    {
        Assert.Equal(DnsRecordType.Nsec, record.Type, $"{where}: the record is an NSEC");
        Assert.True(record.Name.Equals(name), $"{where}: it is about {name}, and was about {record.Name}");
        Assert.True(record.NsecNextDomainName is { } next && next.Equals(name),
            $"{where}: its next domain name is its own name (RFC 6762 section 6.1)");
        Assert.True(record.NsecTypes is not null, $"{where}: it reads back in the restricted form");
        Assert.Equal(
            string.Join(",", types.Select(t => (int)t)),
            string.Join(",", record.NsecTypes!.Select(t => (int)t)),
            $"{where}: the types it lists, by number");
        Assert.Equal(ttl, record.Ttl, $"{where}: its TTL");
        Assert.Equal(cacheFlush, record.CacheFlush, $"{where}: its cache-flush bit");
        Assert.Equal(1, record.RawClass & 0x7FFF, $"{where}: its class is IN");
    }

    // ---- The negative answer (RFC 6762 section 6.1) ---------------------------

    [TestCase("A type 65 (HTTPS) query for the host is answered by multicast with the host's NSEC: A and AAAA, TTL 120, cache-flush bit set")]
    [Requirement("REQ-ADV-022")]
    public static void A_type_the_host_lacks_gets_the_hosts_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        SentDatagram sent = Answer(responder, transport, Query((Host(advertisement), Https)));

        Assert.True(sent.WasMulticast, "a query from port 5353 is answered by multicast");
        Assert.Equal(0, sent.Parsed.Questions.Count, "a multicast response repeats no question (RFC 6762 section 6)");
        Assert.Equal(1, sent.Parsed.AllRecords.Count(), "the NSEC is the whole response");
        AssertNsec(sent.Parsed.Answers.Single(), Host(advertisement),
            [DnsRecordType.A, DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer");
    }

    [TestCase("With no link-local address, an AAAA query for the host is answered with an NSEC listing A alone")]
    [Requirement("REQ-ADV-022")]
    public static void Without_a_link_local_address_an_aaaa_query_gets_an_nsec_listing_a()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        SentDatagram sent = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.Aaaa)));

        Assert.Equal(1, sent.Parsed.AllRecords.Count(), "the NSEC is the whole response");
        AssertNsec(sent.Parsed.Answers.Single(), Host(advertisement),
            [DnsRecordType.A], 120, cacheFlush: true, "the answer");
    }

    [TestCase("A query for a type the instance lacks is answered with the instance's NSEC: TXT and SRV, TTL 4500")]
    [Requirement("REQ-ADV-022")]
    public static void A_type_the_instance_lacks_gets_the_instances_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        foreach (DnsRecordType type in new[] { DnsRecordType.A, Https })
        {
            SentDatagram sent = Answer(responder, transport, Query((Instance(advertisement), type)));

            Assert.Equal(1, sent.Parsed.AllRecords.Count(), $"type {(int)type}: the NSEC is the whole response");
            AssertNsec(sent.Parsed.Answers.Single(), Instance(advertisement),
                [DnsRecordType.Txt, DnsRecordType.Srv], 4500, cacheFlush: true, $"type {(int)type}: the answer");
        }
    }

    [TestCase("A query for type NSEC itself, for the host, is answered with the host's NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void A_question_for_type_nsec_is_answered_with_the_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        SentDatagram sent = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.Nsec)));

        AssertNsec(sent.Parsed.Answers.Single(), Host(advertisement),
            [DnsRecordType.A, DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer");
    }

    [TestCase("Two questions for types the host lacks are answered with its NSEC once")]
    [Requirement("REQ-ADV-022")]
    public static void Two_negative_questions_for_one_name_send_the_nsec_once()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        SentDatagram sent = Answer(responder, transport, Query((host, Https), (host, DnsRecordType.Txt)));

        Assert.Equal(1, sent.Parsed.AllRecords.Count(), "one NSEC says everything both questions asked");
        AssertNsec(sent.Parsed.Answers.Single(), host,
            [DnsRecordType.A, DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer");
    }

    [TestCase("A question the responder has records for and one it has none for are both answered, in the Answer section")]
    [Requirement("REQ-ADV-022")]
    public static void A_positive_and_a_negative_question_are_both_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        SentDatagram sent = Answer(responder, transport,
            Query((Instance(advertisement), DnsRecordType.Srv), (Host(advertisement), Https)));

        Assert.Equal(1, sent.Parsed.Answers.Count(r => r.Type == DnsRecordType.Srv), "the SRV record answers the first");
        AssertNsec(Nsecs(sent.Parsed.Answers).Single(), Host(advertisement),
            [DnsRecordType.A, DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer to the second");
        Assert.Equal(0, Nsecs(sent.Parsed.Additionals).Count, "and it is not sent again as an additional");
    }

    [TestCase("A type 65 query arriving over IPv6 is answered over IPv6, and counted there")]
    [Requirement("REQ-ADV-022")]
    public static void A_negative_answer_leaves_by_the_transport_the_query_arrived_on()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        SentDatagram sent = Answer(responder, transport, Query(ClientNicV6, 5353, (Host(advertisement), Https)));

        Assert.True(sent.Via.Matches(ClientNicV6), "the answer leaves by the IPv6 entry (REQ-ADV-018)");
        Assert.Equal(1, Nsecs(sent.Parsed.Answers).Count, "and it is the NSEC");
        Assert.Equal(1, responder.Activity.QueriesAnsweredOverIPv6, "a negative answer is an answer, and is counted as one");
        Assert.Equal(0, responder.Activity.QueriesAnsweredOverIPv4, "against the transport it left on only");
        Assert.Equal(0, responder.Activity.IgnoredNotOurs, "the query was not ignored");
    }

    [TestCase("A question in class ANY for a type the host lacks is answered with the NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void A_class_any_question_gets_the_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        SentDatagram sent = Answer(responder, transport, QueryInClass(Host(advertisement), Https, ClassAny));

        AssertNsec(sent.Parsed.Answers.Single(), Host(advertisement),
            [DnsRecordType.A, DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer");
    }

    // ---- No negative answer ---------------------------------------------------

    [TestCase("A shared service-type name gets no negative answer, whatever type is asked for")]
    [Requirement("REQ-ADV-022")]
    public static void A_shared_name_gets_no_negative_answer()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal);

        (string Name, DnsRecordType Type)[] questions =
        [
            ("_ipp._tcp.local", DnsRecordType.Txt),
            ("_ipp._tcp.local", Https),
            ("_universal._sub._ipp._tcp.local", DnsRecordType.A),
            ("_services._dns-sd._udp.local", DnsRecordType.Srv),
            ("_ipp._tcp.local", DnsRecordType.Nsec),
        ];

        foreach ((string name, DnsRecordType type) in questions)
        {
            Assert.False(Handle(responder, Query((DnsName.Parse(name), type))),
                $"{name} type {(int)type}: other printers answer for this name too, so what it lacks is not this responder's to say (RFC 6762 section 6)");
        }

        Assert.Equal(0, transport.Sent.Count, "nothing was sent");
        Assert.Equal(questions.Length, responder.Activity.IgnoredNotOurs, "each was counted as ignored");
    }

    [TestCase("Another host's name gets no negative answer")]
    [Requirement("REQ-ADV-022")]
    public static void Another_hosts_name_gets_no_negative_answer()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal);

        Assert.False(Handle(responder, Query((DnsName.Parse("epson000000.local"), Https))),
            "the printer's name is not the proxy's");
        Assert.False(Handle(responder, Query((DnsName.Parse("another-host.local"), DnsRecordType.Aaaa))),
            "nor is any other host's");
        Assert.False(Handle(responder, Query((DnsName.Parse("local"), Https))),
            "nor is a parent of the proxy's names");

        Assert.Equal(0, transport.Sent.Count, "nothing was sent");
    }

    [TestCase("An ANY question is answered with the records at the name and no NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void An_any_question_gets_no_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        DnsMessage forHost = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.Any))).Parsed;
        DnsMessage forInstance = Answer(responder, transport, Query((Instance(advertisement), DnsRecordType.Any))).Parsed;

        Assert.Equal(2, forHost.Answers.Count, "the host's A and AAAA records answer");
        Assert.Equal(2, forInstance.Answers.Count, "the instance's SRV and TXT records answer");
        Assert.Equal(0, Nsecs(forHost.AllRecords).Count + Nsecs(forInstance.AllRecords).Count,
            "the NSEC is not one of the records at the name; it is what is said when a type is missing");
    }

    [TestCase("A positive answer for the instance carries no NSEC for the instance")]
    [Requirement("REQ-ADV-022")]
    public static void A_positive_answer_carries_no_nsec_for_its_own_name()
    {
        // RFC 6762 section 6.1 says a responder MAY add one. This one does not.
        foreach (IPAddress[] linkLocal in new IPAddress[][] { [], [LinkLocal] })
        {
            (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(linkLocal);
            DnsName instance = Instance(advertisement);

            foreach (DnsRecordType type in new[] { DnsRecordType.Srv, DnsRecordType.Txt })
            {
                DnsMessage reply = Answer(responder, transport, Query((instance, type))).Parsed;

                Assert.False(Nsecs(reply.AllRecords).Any(r => r.Name.Equals(instance)),
                    $"{type}, {linkLocal.Length} link-local address(es): no NSEC for the instance");
            }
        }
    }

    [TestCase("While probing, a query for a type the host lacks is not answered")]
    [Requirement("REQ-ADV-022")]
    public static void Nothing_is_denied_while_probing()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);
        var answered = new List<bool>();
        int waits = 0;

        Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            // The second wait is the one after the first probe.
            if (waits++ == 1)
            {
                answered.Add(Handle(responder, Query((Host(advertisement), Https))));
            }

            return Task.CompletedTask;
        }

        responder.ProbeAsync(Delay, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(1, answered.Count, "the query was put to the responder during the probe");
        Assert.False(answered[0], "the name is not the responder's to speak for until the probe is clear (REQ-ADV-023)");
        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count, "only the probes went out");
        Assert.False(transport.Sent.Any(s => Nsecs(s.Parsed.AllRecords).Count > 0), "and none of them carries an NSEC");
    }

    [TestCase("While withdrawn, a query for a type the host lacks is not answered")]
    [Requirement("REQ-ADV-022")]
    public static void Nothing_is_denied_while_withdrawn()
    {
        bool advertising = false;
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) =
            Build(() => advertising, LinkLocal);

        Assert.False(Handle(responder, Query((Host(advertisement), Https))),
            "a withdrawn responder says nothing about its names, negative or positive (REQ-LIF-006)");
        Assert.Equal(0, transport.Sent.Count, "nothing was sent");

        advertising = true;
        Assert.True(Handle(responder, Query((Host(advertisement), Https))), "on offer again, the same query is answered");
    }

    [TestCase("After a conflict, a query for a type the host lacks is not answered")]
    [Requirement("REQ-ADV-022")]
    public static void Nothing_is_denied_after_a_conflict()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        Assert.True(Handle(responder, Query((host, Https))), "before the conflict the query is answered");
        int before = transport.Sent.Count;

        byte[] theirs = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(host, DnsRecordType.A, 120, CacheFlush: true,
                new AddressPayload(IPAddress.Parse("192.168.1.50"))))
            .Build();
        Handle(responder, new MdnsDatagram(
            theirs, new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353), ClientNic.Index, ClientNic));
        Assert.True(responder.Conflict is not null, "another device's address for the host is a conflict");

        Assert.False(Handle(responder, Query((host, Https))),
            "another device holds the name, so this responder cannot say what the name lacks (REQ-ADV-024)");
        Assert.Equal(before, transport.Sent.Count, "nothing more was sent");
    }

    [TestCase("A question in class CHAOS for a type the host lacks gets no NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void A_question_in_another_class_gets_no_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        Assert.False(Handle(responder, QueryInClass(Host(advertisement), Https, ClassChaos)),
            "the NSEC is in class IN and says nothing about class CHAOS (REQ-ADV-025)");
        Assert.Equal(0, transport.Sent.Count, "nothing was sent");
    }

    // ---- Beside an address record (RFC 6762 section 6.2) ----------------------

    [TestCase("With no link-local address, an A answer, an SRV answer and a PTR answer each carry the host's NSEC as an additional")]
    [Requirement("REQ-ADV-022")]
    public static void Without_a_link_local_address_an_address_record_brings_the_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        (DnsName Name, DnsRecordType Type, string Section)[] questions =
        [
            (host, DnsRecordType.A, "answer"),
            (Instance(advertisement), DnsRecordType.Srv, "additional"),
            (DnsName.Parse("_universal._sub._ipp._tcp.local"), DnsRecordType.Ptr, "additional"),
        ];

        foreach ((DnsName name, DnsRecordType type, string section) in questions)
        {
            DnsMessage reply = Answer(responder, transport, Query((name, type))).Parsed;
            string where = $"{type} query";

            Assert.Equal(1, reply.AllRecords.Count(r => r.Type == DnsRecordType.A),
                $"{where}: the response carries the host's A record");
            Assert.Equal(section == "answer" ? 1 : 0, reply.Answers.Count(r => r.Type == DnsRecordType.A),
                $"{where}: as an {section}");
            Assert.Equal(0, Nsecs(reply.Answers).Count, $"{where}: nobody asked for a type the host lacks");
            AssertNsec(Nsecs(reply.Additionals).Single(), host,
                [DnsRecordType.A], 120, cacheFlush: true, $"{where}: the additional");
        }
    }

    [TestCase("With a link-local address, no response to an A, AAAA, SRV or PTR query carries an NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void With_a_link_local_address_no_nsec_is_added()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);

        (DnsName Name, DnsRecordType Type)[] questions =
        [
            (Host(advertisement), DnsRecordType.A),
            (Host(advertisement), DnsRecordType.Aaaa),
            (Instance(advertisement), DnsRecordType.Srv),
            (DnsName.Parse("_universal._sub._ipp._tcp.local"), DnsRecordType.Ptr),
        ];

        foreach ((DnsName name, DnsRecordType type) in questions)
        {
            DnsMessage reply = Answer(responder, transport, Query((name, type))).Parsed;

            Assert.Equal(1, reply.AllRecords.Count(r => r.Type == DnsRecordType.A),
                $"{type} query: the response carries the A record");
            Assert.Equal(1, reply.AllRecords.Count(r => r.Type == DnsRecordType.Aaaa),
                $"{type} query: and the AAAA record, so both address types are there");
            Assert.Equal(0, Nsecs(reply.AllRecords).Count, $"{type} query: and there is no absence to state");
        }
    }

    [TestCase("With no link-local address, an A and an AAAA question in one query put the NSEC in the Answer section only, in either order")]
    [Requirement("REQ-ADV-022")]
    public static void An_nsec_that_answers_is_not_also_an_additional()
    {
        DnsRecordType[][] orders =
        [
            [DnsRecordType.A, DnsRecordType.Aaaa],
            [DnsRecordType.Aaaa, DnsRecordType.A],
        ];

        foreach (DnsRecordType[] order in orders)
        {
            (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
            DnsName host = Host(advertisement);
            string where = $"{order[0]} asked before {order[1]}";

            DnsMessage reply = Answer(responder, transport, Query((host, order[0]), (host, order[1]))).Parsed;

            Assert.Equal(1, reply.Answers.Count(r => r.Type == DnsRecordType.A), $"{where}: the A record answers the A question");
            AssertNsec(Nsecs(reply.Answers).Single(), host,
                [DnsRecordType.A], 120, cacheFlush: true, $"{where}: the answer to the AAAA question");
            Assert.Equal(0, Nsecs(reply.Additionals).Count, $"{where}: and it is not sent again as an additional");
        }
    }

    [TestCase("A host with an AAAA record and no A record gets the same treatment the other way round")]
    [Requirement("REQ-ADV-022")]
    public static void A_host_with_only_an_aaaa_record_brings_the_nsec_too()
    {
        // AdvertisementBuilder always publishes an A record, so the service
        // never sends this. RFC 6762 section 6.2 states the rule for both
        // address types, the responder is written for both, and this checks
        // the half the service's own advertisement cannot reach.
        Advertisement both = MdnsResponderTests.BuildAdvertisement(LinkLocal);
        Advertisement aaaaOnly = both with { Records = [.. both.Records.Where(r => r.Type != DnsRecordType.A)] };
        DnsName host = aaaaOnly.Records.Single(r => r.Type == DnsRecordType.Aaaa).Name;

        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(transport, [new AdvertisedInterface(ClientNic, aaaaOnly, ClientNicV6)]);

        DnsMessage positive = Answer(responder, transport, Query((host, DnsRecordType.Aaaa))).Parsed;
        Assert.Equal(1, positive.Answers.Count(r => r.Type == DnsRecordType.Aaaa), "the AAAA record answers");
        AssertNsec(Nsecs(positive.Additionals).Single(), host,
            [DnsRecordType.Aaaa], 120, cacheFlush: true, "beside the AAAA answer");

        DnsMessage negative = Answer(responder, transport, Query((host, DnsRecordType.A))).Parsed;
        AssertNsec(negative.Answers.Single(), host,
            [DnsRecordType.Aaaa], 120, cacheFlush: true, "the answer to the A question");
    }

    // ---- Legacy unicast queriers (RFC 6762 section 6.7) -----------------------

    [TestCase("A legacy querier asking for type 65 gets the NSEC by unicast: its question repeated, its identifier, TTL 10, no cache-flush bit")]
    [Requirement("REQ-ADV-022")]
    public static void A_legacy_querier_gets_the_nsec_by_unicast()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        SentDatagram sent = Answer(responder, transport, Query(ClientNic, 51234, (host, Https)));

        Assert.False(sent.WasMulticast, "a legacy querier is answered by unicast");
        Assert.Equal(51234, sent.Destination!.Port, "at the port it asked from");
        Assert.Equal((ushort)0x1234, sent.Parsed.Id, "with its query identifier");

        (DnsName name, DnsRecordType type, ushort rawClass) = sent.Parsed.Questions.Single();
        Assert.True(name.Equals(host), "the question is repeated: its name");
        Assert.Equal(65, (int)type, "its type");
        Assert.Equal((ushort)1, rawClass, "and its class, as asked");

        Assert.Equal(1, sent.Parsed.AllRecords.Count(), "the NSEC is the whole answer");
        AssertNsec(sent.Parsed.Answers.Single(), host,
            [DnsRecordType.A, DnsRecordType.Aaaa], 10, cacheFlush: false, "the answer");
    }

    [TestCase("With no link-local address, a legacy querier asking for the A record gets the NSEC as an additional, TTL 10, no cache-flush bit")]
    [Requirement("REQ-ADV-022")]
    public static void A_legacy_querier_gets_the_additional_nsec_capped()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        SentDatagram sent = Answer(responder, transport, Query(ClientNic, 51234, (host, DnsRecordType.A)));

        Assert.False(sent.WasMulticast, "a legacy querier is answered by unicast");
        AssertNsec(Nsecs(sent.Parsed.Additionals).Single(), host,
            [DnsRecordType.A], 10, cacheFlush: false, "the additional");
    }

    // ---- Announcing, probing and retracting -----------------------------------

    [TestCase("No announcement carries an NSEC")]
    [Requirement("REQ-ADV-022")]
    public static void Announcements_carry_no_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build();

        responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(MdnsResponder.AnnouncementCount, transport.Sent.Count, "the announcements went out");
        Assert.False(transport.Sent.Any(s => Nsecs(s.Parsed.AllRecords).Count > 0),
            "an NSEC is sent when a question or an address record calls for it, and not announced");
    }

    [TestCase("No probe proposes an NSEC or asks for one")]
    [Requirement("REQ-ADV-022")]
    public static void Probes_propose_no_nsec()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build();

        responder.ProbeAsync(static (_, _) => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count, "the probes went out");
        foreach (SentDatagram sent in transport.Sent)
        {
            Assert.Equal(0, Nsecs(sent.Parsed.AllRecords).Count,
                "the NSEC describes the claimed records; it is not one of them");
            Assert.True(sent.Parsed.Questions.All(q => q.Type == DnsRecordType.Any), "every question is for type ANY");
        }
    }

    [TestCase("The goodbye retracts both NSEC records with TTL zero, beside every advertised record")]
    [Requirement("REQ-ADV-022")]
    public static void The_goodbye_retracts_both_nsec_records()
    {
        foreach (IPAddress[] linkLocal in new IPAddress[][] { [], [LinkLocal] })
        {
            (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(linkLocal);
            string where = $"{linkLocal.Length} link-local address(es)";

            responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

            DnsMessage goodbye = transport.Sent.Single().Parsed;
            List<DnsRecord> nsecs = Nsecs(goodbye.Answers);

            Assert.Equal(2, nsecs.Count, $"{where}: one NSEC for the host and one for the instance");
            AssertNsec(nsecs.Single(r => r.Name.Equals(Host(advertisement))), Host(advertisement),
                linkLocal.Length == 0 ? [DnsRecordType.A] : [DnsRecordType.A, DnsRecordType.Aaaa],
                0, cacheFlush: true, $"{where}: the host's");
            AssertNsec(nsecs.Single(r => r.Name.Equals(Instance(advertisement))), Instance(advertisement),
                [DnsRecordType.Txt, DnsRecordType.Srv], 0, cacheFlush: true, $"{where}: the instance's");

            Assert.Equal(advertisement.Records.Count + 2, goodbye.Answers.Count,
                $"{where}: every advertised record is still retracted, and nothing else is");
            Assert.True(goodbye.Answers.All(r => r.Ttl == 0), $"{where}: all with TTL zero");
            Assert.Equal(0, goodbye.Additionals.Count + goodbye.Authorities.Count, $"{where}: all in the Answer section");
        }
    }

    // ---- Heard back -----------------------------------------------------------

    [TestCase("The responder's own NSEC records, heard back exactly as it sent them, are not a conflict")]
    [Requirement("REQ-ADV-024")]
    public static void What_the_responder_sent_is_not_a_conflict_when_heard_back()
    {
        // With and without an AAAA record: the NSEC says something different in
        // each case, and in each it must agree with what the responder holds.
        foreach (IPAddress[] linkLocal in new IPAddress[][] { [], [LinkLocal] })
        {
            (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(linkLocal);

            Answer(responder, transport, Query((Host(advertisement), Https)));
            Answer(responder, transport, Query((Instance(advertisement), Https)));
            Answer(responder, transport, Query((Host(advertisement), DnsRecordType.A)));
            responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(4, transport.Sent.Count, "two negative answers, one positive answer and the goodbye went out");
            Assert.True(transport.Sent.Count(s => Nsecs(s.Parsed.AllRecords).Count > 0) >= 3,
                "and at least three of them carry an NSEC, so there is something to hear back");

            foreach (SentDatagram sent in transport.Sent.ToList())
            {
                Handle(responder, new MdnsDatagram(
                    sent.Payload, new IPEndPoint(ClientNic.Address, 5353), ClientNic.Index, ClientNic));
            }

            Assert.True(responder.Conflict is null,
                $"{linkLocal.Length} link-local address(es): multicast is heard by its sender, and the responder must not take its own words for another device's");
        }
    }

    // ---- At construction ------------------------------------------------------

    [TestCase("An advertisement claiming a type an NSEC cannot list is refused when the responder is built")]
    public static void A_type_the_nsec_cannot_list_is_refused_at_construction()
    {
        Advertisement ours = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic);

        // Type 300 needs a bitmap block other than 0, which the restricted form
        // does not have (RFC 6762 section 6.1).
        Advertisement withType300 = ours with
        {
            Records =
            [
                .. ours.Records,
                new OutgoingRecord(Host(ours), (DnsRecordType)300, 120, CacheFlush: true, new TxtPayload(["x=1"])),
            ],
        };

        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(transport, [new AdvertisedInterface(ClientNic, withType300)]),
            "an NSEC for that name would say the name has no type above 255, which would be false; "
            + "found at startup, not when the first query arrives");

        Assert.True(refused.Message.Contains("secretprinter.local", StringComparison.Ordinal)
                    && refused.Message.Contains("300", StringComparison.Ordinal),
            "the refusal names the name and the type, so the operator can find them: " + refused.Message);

        // The same record as a shared one is not claimed, so no NSEC is built for it.
        Advertisement shared = ours with
        {
            Records =
            [
                .. ours.Records,
                new OutgoingRecord(DnsName.Parse("_ipp._tcp.local"), (DnsRecordType)300, 4500, CacheFlush: false,
                    new TxtPayload(["x=1"])),
            ],
        };

        _ = new MdnsResponder(transport, [new AdvertisedInterface(ClientNic, shared)]);
    }

    [TestCase("An advertisement holding a shared record at a name it also claims is refused when the responder is built")]
    public static void A_shared_record_at_a_claimed_name_is_refused_at_construction()
    {
        Advertisement ours = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic);

        // AdvertisementBuilder makes no such record. This one is a PTR record
        // at the host name with the cache-flush bit clear.
        Advertisement mixed = ours with
        {
            Records =
            [
                .. ours.Records,
                new OutgoingRecord(Host(ours), DnsRecordType.Ptr, 4500, CacheFlush: false,
                    new PtrPayload(DnsName.Parse("elsewhere.local"))),
            ],
        };

        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(transport, [new AdvertisedInterface(ClientNic, mixed)]),
            "the host's NSEC lists the claimed types only, so it would say the host has no PTR record "
            + "while the responder answers with one");

        Assert.True(refused.Message.Contains("secretprinter.local", StringComparison.Ordinal)
                    && refused.Message.Contains("Ptr", StringComparison.Ordinal),
            "the refusal names the name and the type: " + refused.Message);
    }
}

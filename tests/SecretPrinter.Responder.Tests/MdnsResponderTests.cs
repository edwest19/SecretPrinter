// -----------------------------------------------------------------------------
// MdnsResponderTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Per-transport counting tests added by Claude (Anthropic model, Claude Opus 5)
// at the direction of Edwin West, 2026-09-14, for REQ-OBS-007. Reviewed by a
// human before merge.
//
// ClientNic, ClientNicV6 and BuildAdvertisement made internal, so the probing
// tests in MdnsProbeTests.cs use the same fixtures, by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-09-28. Reviewed by a
// human before merge.
//
// The call to AdvertisementBuilder.Build given an empty link-local list by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-30, when Build began taking one. No test changed what it checks.
// Reviewed by a human before merge.
//
// BuildAdvertisement given an optional list of link-local addresses, for
// LinkLocalAnsweringTests, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-30. Called with none, it builds exactly what
// it built before. Reviewed by a human before merge.
//
// An_answer_is_never_also_an_additional added by Claude (Anthropic model, Claude
// Opus 5.5) at the direction of Edwin West, 2026-10-01. It failed before the
// change it came with: a record answering a later question was also sent as an
// additional of an earlier one. A_record_answering_two_questions_is_sent_once
// added with it, because breaking the de-duplication of answers on purpose
// failed no test; it passed before the change and after. See
// docs/findings/2026-10-01-a-record-was-sent-as-an-answer-and-an-additional.md.
// Reviewed by a human before merge.
//
// Nothing_sent_originates_from_a_received_packet changed by Claude (Anthropic
// model, Claude Opus 5.5) at the direction of Edwin West, 2026-10-01, when the
// responder began sending NSEC records (README REQ-ADV-022). It allowed only
// records that are in the advertisement, and an NSEC is not in it: the
// responder builds one from it for each name it claims. The test now also
// allows an NSEC, and only one that is about a claimed name and lists exactly
// the types the advertisement holds there. Its title changed to say so.
// Reviewed by a human before merge.
//
// A_failed_send_does_not_end_the_receive_loop and
// A_failed_receive_is_reported_and_the_loop_goes_on added by Claude (Anthropic
// model, Claude Opus 5.5) at the direction of Edwin West, 2026-10-07, for
// REQ-LIF-005. They are the first tests here to run ServeAsync itself. The
// first failed before the change it came with: a send that threw ended the
// loop. See
// docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.
// Reviewed by a human before merge.
//
// Purpose:
//   Verifies which queries get answered, which are ignored, what the answers
//   contain, and how they are addressed.
//
//   Every test here runs against a fake transport, so none needs hardware, a
//   network, or real time. That was the point of introducing the seam: the one
//   requirement still unverified in this repository, REQ-CFG-006, got that way
//   because its test needed a particular adapter arrangement.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class MdnsResponderTests
{
    internal static readonly MdnsInterface ClientNic =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetwork);

    private static readonly MdnsInterface PrinterNic =
        new("Wi-Fi", IPAddress.Parse("192.168.12.245"), 11, AddressFamily.InterNetwork);

    /// <summary>
    /// The IPv6 companion of <see cref="ClientNic"/>: the same adapter, the same
    /// IPv4 address, and the index the platform reports for IPv6.
    /// </summary>
    /// <remarks>
    /// The address is IPv4 and that is deliberate, not an oversight - the
    /// transport decides how a datagram travels, the A record decides what it
    /// says. MdnsInterfaceResolver.ResolveIPv6 builds the real ones the same way.
    ///
    /// The index is 13, equal to the IPv4 index, because that is what every
    /// adapter on both of this project's machines reports. Keeping it equal here
    /// is the point: a responder keyed on the index alone would pass these tests
    /// by finding the IPv4 entry.
    /// </remarks>
    internal static readonly MdnsInterface ClientNicV6 =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetworkV6);

    /// <summary>
    /// Real ET-3760 records. The low three bytes of the UUID are redacted to
    /// <c>000000</c>; see docs/findings/2026-09-04-pre-publication-audit.md.
    /// </summary>
    private static readonly string[] EpsonTxt =
    [
        "txtvers=1",
        "rp=ipp/print",
        "pdl=application/octet-stream,image/urf,image/jpeg",
        "URF=CP1,PQ4-5,OB9,RS300,SRGB24,W8,DM3,IS1,V1.4",
        "Color=T",
        "Scan=T",
        "UUID=cfe92100-67c4-11d4-a45f-f8d027000000",
    ];

    /// <summary>The advertisement the responder tests share, optionally with link-local addresses.</summary>
    /// <param name="linkLocal">
    /// Link-local addresses to publish as AAAA records. None, unless a test is
    /// about AAAA records, so the tests written before AAAA records existed
    /// still check exactly what they checked then.
    /// </param>
    internal static Advertisement BuildAdvertisement(params IPAddress[] linkLocal) =>
        AdvertisementBuilder.Build(
            new PrinterCapabilities(EpsonTxt, 631, CapabilitySource.ForTest("responder tests")),
            new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631),
            ClientNic.Address,
            linkLocalAddresses: linkLocal);

    private static (MdnsResponder Responder, FakeTransport Transport) Build()
    {
        var transport = new FakeTransport(ClientNic);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, BuildAdvertisement())]);
        return (responder, transport);
    }

    /// <summary>A responder whose client interface also answers over IPv6.</summary>
    private static (MdnsResponder Responder, FakeTransport Transport) BuildDualStack()
    {
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, BuildAdvertisement(), ClientNicV6)]);
        return (responder, transport);
    }

    /// <summary>Builds a query datagram as a client would send it.</summary>
    private static MdnsDatagram Query(
        string name,
        DnsRecordType type = DnsRecordType.Ptr,
        int interfaceIndex = 13,
        int sourcePort = 5353,
        MdnsInterface? arrivedOn = null)
    {
        byte[] payload = new DnsQueryBuilder(0x1234)
            .AddQuestion(DnsName.Parse(name), type, requestUnicastResponse: false)
            .Build();

        return new MdnsDatagram(
            payload,
            new IPEndPoint(IPAddress.Parse("192.168.1.41"), sourcePort),
            interfaceIndex,
            arrivedOn ?? (interfaceIndex == ClientNic.Index ? ClientNic : null));
    }

    private static bool Handle(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    // ---- Answering ----------------------------------------------------------

    [TestCase("A query for the AirPrint subtype is answered")]
    [Requirement("REQ-ADV-002")]
    public static void Airprint_subtype_query_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, Query("_universal._sub._ipp._tcp.local"));

        Assert.True(answered, "iOS queries this subtype exclusively; not answering means never discovered");
        Assert.Equal(1, transport.Sent.Count, "exactly one answer should be sent");

        DnsMessage reply = transport.Sent[0].Parsed;
        Assert.True(reply.IsResponse, "the reply must have the response bit set");
        Assert.True(reply.Answers.Any(r => r.Type == DnsRecordType.Ptr),
            "the answer section must contain the PTR that was asked for");
    }

    [TestCase("A query for the base service type is answered")]
    [Requirement("REQ-ADV-001")]
    public static void Service_type_query_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        Assert.True(Handle(responder, Query("_ipp._tcp.local")), "the IPP service must be discoverable");
        Assert.Equal(1, transport.Sent.Count, "one answer");
    }

    [TestCase("A PTR answer carries SRV, TXT and A as additionals")]
    [Requirement("REQ-ADV-001")]
    public static void Ptr_answer_includes_what_the_client_needs_next()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        Handle(responder, Query("_ipp._tcp.local"));

        DnsMessage reply = transport.Sent[0].Parsed;
        var types = reply.Additionals.Select(r => r.Type).ToList();

        Assert.True(types.Contains(DnsRecordType.Srv), "SRV saves the client a round trip");
        Assert.True(types.Contains(DnsRecordType.Txt), "TXT carries the capabilities");
        Assert.True(types.Contains(DnsRecordType.A), "A resolves the host the SRV names");
    }

    [TestCase("A record that answers one question is not also sent as an additional, whichever question comes first")]
    public static void An_answer_is_never_also_an_additional()
    {
        DnsName serviceType = DnsName.Parse("_ipp._tcp.local");
        DnsName instance = BuildAdvertisement().Records.Single(r => r.Type == DnsRecordType.Srv).Name;

        // Asked first, the PTR question brings in the SRV record as an
        // additional; the SRV question then answers with it. Asked the other
        // way round, the SRV record is an answer before the PTR question
        // brings it in. Either way it is an answer, and only an answer.
        (DnsName Name, DnsRecordType Type)[][] orders =
        [
            [(serviceType, DnsRecordType.Ptr), (instance, DnsRecordType.Srv)],
            [(instance, DnsRecordType.Srv), (serviceType, DnsRecordType.Ptr)],
        ];

        foreach ((DnsName Name, DnsRecordType Type)[] questions in orders)
        {
            (MdnsResponder responder, FakeTransport transport) = Build();
            var query = new DnsQueryBuilder(0);
            foreach ((DnsName name, DnsRecordType type) in questions)
            {
                query.AddQuestion(name, type, requestUnicastResponse: false);
            }

            Handle(responder, new MdnsDatagram(
                query.Build(), new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), ClientNic.Index, ClientNic));

            DnsMessage reply = transport.Sent.Single().Parsed;
            string order = $"{questions[0].Type} asked before {questions[1].Type}";
            Assert.Equal(1, reply.Answers.Count(r => r.Type == DnsRecordType.Srv),
                $"{order}: the SRV record answers its question");
            Assert.False(reply.Additionals.Any(r => r.Type == DnsRecordType.Srv),
                $"{order}: so it is not sent again as an additional");
            Assert.True(reply.Additionals.Any(r => r.Type == DnsRecordType.Txt),
                $"{order}: the TXT record the PTR answer leads to is still an additional");
        }
    }

    [TestCase("A record that answers two questions in one query is sent once")]
    public static void A_record_answering_two_questions_is_sent_once()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        DnsName host = BuildAdvertisement().Records.Single(r => r.Type == DnsRecordType.A).Name;

        // An A question and an ANY question for the host are both answered by
        // its A record.
        byte[] query = new DnsQueryBuilder(0)
            .AddQuestion(host, DnsRecordType.A, requestUnicastResponse: false)
            .AddQuestion(host, DnsRecordType.Any, requestUnicastResponse: false)
            .Build();

        Handle(responder, new MdnsDatagram(
            query, new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), ClientNic.Index, ClientNic));

        DnsMessage reply = transport.Sent.Single().Parsed;
        Assert.Equal(1, reply.Answers.Count(r => r.Type == DnsRecordType.A), "the A record is sent once");
    }

    [TestCase("The answer advertises the proxy's address, never the printer's")]
    [Requirement("REQ-ADV-004")]
    public static void Answer_carries_the_proxy_address()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        Handle(responder, Query("_ipp._tcp.local"));

        foreach (DnsRecord record in transport.Sent[0].Parsed.AllRecords)
        {
            if (record.Type == DnsRecordType.A)
            {
                Assert.Equal(ClientNic.Address, record.Address!,
                    "the only address on the wire must be the proxy's own");
            }
        }
    }

    // ---- Ignoring -----------------------------------------------------------

    [TestCase("A query for a service we do not advertise is ignored")]
    [Requirement("REQ-ADV-012")]
    public static void Unrelated_service_query_is_ignored()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        Assert.False(Handle(responder, Query("_airplay._tcp.local")), "we advertise no AirPlay service");
        Assert.False(Handle(responder, Query("_companion-link._tcp.local")), "nor companion-link");
        Assert.False(Handle(responder, Query("_raop._tcp.local")), "nor AirPlay audio");

        Assert.Equal(0, transport.Sent.Count,
            "answering for a service we do not provide would be a false claim on the network");
        Assert.Equal(3, responder.Activity.IgnoredNotOurs, "each ignored query should be counted");
    }

    [TestCase("A withdrawn advertisement answers nothing")]
    [Requirement("REQ-LIF-006")]
    public static void A_withdrawn_responder_answers_nothing()
    {
        var transport = new FakeTransport(ClientNic);
        bool advertising = true;

        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, BuildAdvertisement())], () => advertising);

        Assert.True(
            Handle(responder, Query("_ipp._tcp.local")),
            "while advertising, our own service type is answered");

        int answered = transport.Sent.Count;
        Assert.True(answered > 0, "the first query must have produced an answer to compare against");

        advertising = false;

        Assert.False(
            Handle(responder, Query("_ipp._tcp.local")),
            "a withdrawn advertisement must not be re-published by answering the next query");

        Assert.Equal(
            answered,
            transport.Sent.Count,
            "nothing may go onto the client network while the printer is not on offer");

        advertising = true;

        Assert.True(
            Handle(responder, Query("_ipp._tcp.local")),
            "answering resumes when the printer is offered again");
    }

    [TestCase("A query for another host's name is ignored")]
    [Requirement("REQ-ADV-012")]
    public static void Other_hostname_query_is_ignored()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        Assert.False(
            Handle(responder, Query("epson000000.local", DnsRecordType.A)),
            "the proxy must not answer for the real printer's name");
        Assert.Equal(0, transport.Sent.Count, "impersonating the printer's hostname would break routing");
    }

    [TestCase("A query arriving on an unconfigured interface is ignored")]
    [Requirement("REQ-ADV-011")]
    public static void Query_from_unconfigured_interface_is_ignored()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        MdnsDatagram fromPrinterSide = Query(
            "_ipp._tcp.local", interfaceIndex: PrinterNic.Index, arrivedOn: null);

        Assert.False(Handle(responder, fromPrinterSide),
            "answering on the printer network would advertise the proxy where it must not appear");
        Assert.Equal(0, transport.Sent.Count, "nothing may be sent");
        Assert.Equal(1, responder.Activity.IgnoredWrongInterface, "the drop should be counted");
    }

    [TestCase("A malformed datagram is counted, not fatal")]
    [Requirement("REQ-LIF-005")]
    public static void Malformed_datagram_does_not_throw()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        var rubbish = new MdnsDatagram(
            [0x00, 0x01, 0x02], new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), 13, ClientNic);

        Assert.False(Handle(responder, rubbish), "a truncated packet cannot be answered");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent in reply to nonsense");
    }

    /// <summary>Waits, for at most five seconds, until a condition holds.</summary>
    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > TimeSpan.FromSeconds(5))
            {
                return false;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        return true;
    }

    [TestCase("A send that fails while answering does not end the receive loop")]
    [Requirement("REQ-LIF-005")]
    public static async Task A_failed_send_does_not_end_the_receive_loop()
    {
        var transport = new FakeTransport(ClientNic);
        var reported = new List<string>();
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, BuildAdvertisement())],
            socketError: reported.Add);

        // The first two answers cannot be sent. The third can.
        int sends = 0;
        transport.OnSend = _ =>
        {
            if (++sends <= 2)
            {
                throw new SocketException((int)SocketError.NetworkUnreachable);
            }
        };

        using var stop = new CancellationTokenSource();
        Task serving = responder.ServeAsync(stop.Token);

        transport.Enqueue(Query("_universal._sub._ipp._tcp.local"));
        transport.Enqueue(Query("_universal._sub._ipp._tcp.local"));
        transport.Enqueue(Query("_universal._sub._ipp._tcp.local"));

        bool thirdAnswered = await EventuallyAsync(() => responder.Activity.QueriesAnswered == 1)
            .ConfigureAwait(false);

        Assert.True(thirdAnswered,
            "the third query must be answered: two sends that failed are not a reason to stop answering");
        Assert.False(serving.IsCompleted, "and the loop must still be running");
        Assert.Equal(3, responder.Activity.QueriesSeen, "all three queries were seen");
        Assert.Equal(1, responder.Activity.QueriesAnswered,
            "and only the one whose answer left is counted as answered");
        Assert.Equal(1, reported.Count, "the same error twice is reported once");
        Assert.True(reported[0].Contains("answering", StringComparison.Ordinal),
            $"and the report says what the responder was doing; it said: {reported[0]}");

        await stop.CancelAsync().ConfigureAwait(false);
        await serving.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    [TestCase("A receive that fails is reported, and the loop goes on")]
    [Requirement("REQ-LIF-005")]
    public static async Task A_failed_receive_is_reported_and_the_loop_goes_on()
    {
        var transport = new FakeTransport(ClientNic)
        {
            ThrowOnReceive = new SocketException((int)SocketError.ConnectionReset),
        };
        var reported = new List<string>();
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, BuildAdvertisement())],
            socketError: reported.Add);

        using var stop = new CancellationTokenSource();
        Task serving = responder.ServeAsync(stop.Token);

        transport.Enqueue(Query("_universal._sub._ipp._tcp.local"));

        bool answered = await EventuallyAsync(() => responder.Activity.QueriesAnswered == 1)
            .ConfigureAwait(false);

        Assert.True(answered, "the query that arrived after the failed receive must be answered");
        Assert.Equal(1, reported.Count, "the failed receive is reported");
        Assert.True(reported[0].Contains("receiving", StringComparison.Ordinal),
            $"and the report says what the responder was doing; it said: {reported[0]}");

        await stop.CancelAsync().ConfigureAwait(false);
        await serving.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    // ---- Nothing crosses between networks -----------------------------------

    /// <summary>A response as another device on the network would send it.</summary>
    private static MdnsDatagram ForeignResponse(string serviceType, int interfaceIndex = 13)
    {
        DnsName type = DnsName.Parse(serviceType);
        var instance = new DnsName(["Someone Elses Device", .. type.Labels]);

        byte[] payload = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(type, DnsRecordType.Ptr, 4500, false, new PtrPayload(instance)))
            .AddAnswer(new OutgoingRecord(instance, DnsRecordType.Srv, 120, true,
                new SrvPayload(0, 0, 445, new DnsName(["someone-else", "local"]))))
            .AddAnswer(new OutgoingRecord(new DnsName(["someone-else", "local"]), DnsRecordType.A, 120, true,
                new AddressPayload(IPAddress.Parse("192.168.12.77"))))
            .Build();

        return new MdnsDatagram(
            payload,
            new IPEndPoint(IPAddress.Parse("192.168.12.77"), 5353),
            interfaceIndex,
            interfaceIndex == ClientNic.Index ? ClientNic : null);
    }

    [TestCase("A response from another device is never re-emitted")]
    [Requirement("REQ-SEC-001")]
    public static void Foreign_response_is_not_forwarded()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        Assert.False(Handle(responder, ForeignResponse("_smb._tcp.local")),
            "another device's answer is not ours to repeat");
        Assert.False(Handle(responder, ForeignResponse("_ipp._tcp.local")),
            "not even when it concerns the same service type we advertise");

        Assert.Equal(0, transport.Sent.Count,
            "a reflector would have forwarded these; this service is not one");
    }

    [TestCase("Every record sent comes from our own advertisement, or is an NSEC listing exactly the types it holds at a name it claims")]
    [Requirement("REQ-SEC-001")]
    public static void Nothing_sent_originates_from_a_received_packet()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        Advertisement ours = BuildAdvertisement();

        var permitted = new HashSet<string>(
            ours.Records.Select(r => $"{r.Name}|{r.Type}"), StringComparer.Ordinal);

        // A realistic mixture: our own service, someone else's, answers we
        // never asked for, and a question for a type our host does not have.
        Handle(responder, ForeignResponse("_smb._tcp.local"));
        Handle(responder, Query("_ipp._tcp.local"));
        Handle(responder, ForeignResponse("_airplay._tcp.local"));
        Handle(responder, Query("_universal._sub._ipp._tcp.local"));
        Handle(responder, Query("_smb._tcp.local"));
        Handle(responder, Query("secretprinter.local", DnsRecordType.Txt));
        Handle(responder, Query("someone-else.local", DnsRecordType.Txt));

        Assert.True(transport.Sent.Count > 0, "our own service must still have been answered");

        int nsecs = 0;
        foreach (SentDatagram sent in transport.Sent)
        {
            foreach (DnsRecord record in sent.Parsed.AllRecords)
            {
                if (record.Type == DnsRecordType.Nsec)
                {
                    // An NSEC is not in the advertisement. It is built from it,
                    // and may say only what the advertisement says: which types
                    // it holds at a name it claims as unique.
                    nsecs++;
                    List<OutgoingRecord> held = [.. ours.Records.Where(r => r.Name.Equals(record.Name))];

                    Assert.True(held.Count > 0 && held.All(r => r.CacheFlush),
                        $"an NSEC for {record.Name} was emitted, and that is not a name our advertisement claims");
                    Assert.Equal(
                        string.Join(",", held.Select(r => (int)r.Type).Distinct().Order()),
                        string.Join(",", record.NsecTypes!.Select(t => (int)t)),
                        $"the NSEC for {record.Name} lists exactly the types our advertisement holds there");
                    continue;
                }

                Assert.True(permitted.Contains($"{record.Name}|{record.Type}"),
                    $"{record.Type} {record.Name} was emitted but is not in our advertisement, "
                    + "which means something received was passed on");
            }
        }

        Assert.True(nsecs > 0, "the mixture produced at least one NSEC, so the check above ran");
    }

    [TestCase("A service seen on one network does not become answerable on the other")]
    [Requirement("REQ-SEC-002")]
    public static void Seeing_a_service_does_not_expose_it()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        // The responder learns that an SMB share exists, then is asked about it.
        Handle(responder, ForeignResponse("_smb._tcp.local"));
        Assert.False(Handle(responder, Query("_smb._tcp.local")),
            "hearing about a service must not make the proxy able to answer for it");

        Handle(responder, ForeignResponse("_airplay._tcp.local"));
        Assert.False(Handle(responder, Query("_airplay._tcp.local")),
            "and the same for any other");

        Assert.Equal(0, transport.Sent.Count,
            "non-printing services on the printer network stay invisible from the client network");
    }

    // ---- Legacy unicast queriers --------------------------------------------

    [TestCase("A legacy unicast querier is answered directly, with capped TTLs")]
    [Requirement("REQ-ADV-017")]
    public static void Legacy_querier_gets_unicast_with_short_ttl()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        Assert.True(Handle(responder, Query("_ipp._tcp.local", sourcePort: 51234)),
            "a legacy querier still deserves an answer");

        SentDatagram sent = transport.Sent[0];
        Assert.False(sent.WasMulticast, "RFC 6762 s6.7 requires a unicast reply to a legacy querier");
        Assert.Equal(51234, sent.Destination!.Port, "the reply must go back to the querier's port");

        DnsMessage reply = sent.Parsed;
        Assert.Equal((ushort)0x1234, reply.Id, "a legacy querier matches replies by query identifier");
        Assert.True(reply.Questions.Count > 0, "legacy replies repeat the question section");

        foreach (DnsRecord record in reply.AllRecords)
        {
            Assert.True(record.Ttl <= MdnsResponder.LegacyUnicastTtl,
                $"{record.Name} {record.Type} has TTL {record.Ttl}; legacy answers must be capped");
            Assert.False(record.CacheFlush,
                "legacy queriers do not understand the cache-flush bit, so it must be clear");
        }
    }

    [TestCase("A normal multicast querier is answered by multicast, with full TTLs")]
    [Requirement("REQ-ADV-017")]
    public static void Multicast_querier_gets_multicast_with_full_ttl()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        Handle(responder, Query("_ipp._tcp.local", sourcePort: 5353));

        SentDatagram sent = transport.Sent[0];
        Assert.True(sent.WasMulticast, "a query from port 5353 gets a multicast answer");
        Assert.Equal((ushort)0, sent.Parsed.Id, "multicast responses use identifier 0 (RFC 6762 s18.1)");
        Assert.True(sent.Parsed.AllRecords.Any(r => r.Ttl > MdnsResponder.LegacyUnicastTtl),
            "multicast answers keep their full TTLs");
    }

    // ---- Answering over the arrival transport --------------------------------

    [TestCase("A query arriving over IPv6 is answered over IPv6")]
    [Requirement("REQ-ADV-018")]
    public static void IPv6_query_is_answered_over_ipv6()
    {
        (MdnsResponder responder, FakeTransport transport) = BuildDualStack();

        bool answered = Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNicV6));

        Assert.True(answered, "a query arriving over IPv6 must be answered, not ignored");

        SentDatagram sent = transport.Sent[0];
        Assert.Equal(AddressFamily.InterNetworkV6, sent.Via.Transport,
            "REQ-ADV-018: the answer goes out over the transport the query arrived on");
        Assert.True(sent.Via.Matches(ClientNicV6),
            "the answer must leave by the IPv6 entry, not the IPv4 entry that shares its index");
    }

    [TestCase("A query arriving over IPv4 is still answered over IPv4")]
    public static void IPv4_query_is_answered_over_ipv4()
    {
        (MdnsResponder responder, FakeTransport transport) = BuildDualStack();

        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNic));

        SentDatagram sent = transport.Sent[0];
        Assert.Equal(AddressFamily.InterNetwork, sent.Via.Transport,
            "adding an IPv6 companion must not divert IPv4 answers onto IPv6");
    }

    [TestCase("Both transports answer with the same advertisement")]
    public static void Both_transports_answer_with_the_same_records()
    {
        (MdnsResponder responder, FakeTransport transport) = BuildDualStack();

        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNic));
        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNicV6));

        string overIPv4 = string.Join(
            "|", transport.Sent[0].Parsed.AllRecords.Select(r => $"{r.Name} {r.Type}"));
        string overIPv6 = string.Join(
            "|", transport.Sent[1].Parsed.AllRecords.Select(r => $"{r.Name} {r.Type}"));

        Assert.Equal(overIPv4, overIPv6,
            "the same records are delivered over either transport; only the path differs");

        Assert.False(
            transport.Sent[1].Parsed.AllRecords.Any(r => r.Type == DnsRecordType.Aaaa),
            "REQ-ADV-021: answering over IPv6 must not publish an AAAA record");
    }

    // ---- Counting by transport ----------------------------------------------

    // The reason these exist: on FIOS-STB-01 the responder answered 8 of 24 and
    // then 10 of 41 real queries, and there was no way to tell from that whether
    // a single one of those answers went out over IPv6. A client discovering the
    // proxy over IPv4 produces the identical line.

    [TestCase("A query answered over IPv6 is counted against IPv6")]
    [Requirement("REQ-OBS-007")]
    public static void IPv6_answers_are_counted_separately()
    {
        (MdnsResponder responder, _) = BuildDualStack();

        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNicV6));

        ResponderActivity activity = responder.Activity;

        Assert.Equal(1, activity.QueriesSeenOverIPv6, "the query arrived over IPv6");
        Assert.Equal(1, activity.QueriesAnsweredOverIPv6, "and the answer left over IPv6");
        Assert.Equal(0, activity.QueriesSeenOverIPv4, "nothing arrived over IPv4");
        Assert.Equal(0, activity.QueriesAnsweredOverIPv4, "and nothing left over it either");
    }

    [TestCase("A query answered over IPv4 is counted against IPv4")]
    [Requirement("REQ-OBS-007")]
    public static void IPv4_answers_are_counted_separately()
    {
        (MdnsResponder responder, _) = BuildDualStack();

        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNic));

        ResponderActivity activity = responder.Activity;

        Assert.Equal(1, activity.QueriesAnsweredOverIPv4, "an IPv4 query answered over IPv4");
        Assert.Equal(0, activity.QueriesAnsweredOverIPv6,
            "an IPv6 companion being configured must not make IPv4 answers look like IPv6 ones");
        Assert.Equal(1, activity.QueriesAnswered, "and the total is still the total");
    }

    [TestCase("The summary names both transports, including one that served nothing")]
    [Requirement("REQ-OBS-007")]
    public static void Summary_names_both_transports()
    {
        (MdnsResponder responder, _) = BuildDualStack();

        Handle(responder, Query("_ipp._tcp.local", arrivedOn: ClientNic));
        Handle(responder, Query("_airplay._tcp.local", arrivedOn: ClientNic));

        string summary = responder.Activity.DescribeByTransport();

        Assert.True(summary.Contains("IPv4 answered 1 of 2 seen", StringComparison.Ordinal),
            $"the IPv4 counts must appear as counted; the line said: {summary}");
        Assert.True(summary.Contains("IPv6 answered 0 of 0 seen", StringComparison.Ordinal),
            "IPv6 must be named even when it served nothing - otherwise an operator "
            + $"cannot tell silence from a line that simply omits it; the line said: {summary}");
    }

    [TestCase("Announcements and goodbyes stay on IPv4 only")]
    public static void Announcements_do_not_follow_the_companion()
    {
        (MdnsResponder responder, FakeTransport transport) = BuildDualStack();

        responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();
        responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(
            transport.Sent.All(s => s.Via.Transport == AddressFamily.InterNetwork),
            "adding an IPv6 companion must not quietly start announcing over IPv6 - that would "
            + "change REQ-ADV-001, REQ-ADV-002 and REQ-LIF-003, which is a separate decision");
    }

    [TestCase("An IPv6 companion carrying a different address is rejected")]
    public static void Companion_with_a_foreign_address_is_rejected()
    {
        var foreign = new MdnsInterface(
            "Ethernet 2", IPAddress.Parse("192.168.1.99"), 13, AddressFamily.InterNetworkV6);
        var transport = new FakeTransport(ClientNic, foreign);

        Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(
                transport, [new AdvertisedInterface(ClientNic, BuildAdvertisement(), foreign)]),
            "the companion and its IPv4 entry must describe one adapter, since they share an "
            + "advertisement whose A record publishes that address");
    }

    [TestCase("An IPv4 interface offered as an IPv6 companion is rejected")]
    public static void Companion_that_is_not_ipv6_is_rejected()
    {
        var transport = new FakeTransport(ClientNic, PrinterNic);

        Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(
                transport, [new AdvertisedInterface(ClientNic, BuildAdvertisement(), PrinterNic)]),
            "filing an IPv4 interface under IPv6 would answer IPv6 queries over IPv4");
    }

    // ---- Announcing and goodbyes --------------------------------------------

    [TestCase("Startup announces on every advertised interface")]
    [Requirement("REQ-ADV-001")]
    public static void Announce_sends_on_each_interface()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(MdnsResponder.AnnouncementCount, transport.Sent.Count,
            "RFC 6762 s8.3 asks for repeated announcements against packet loss");
        Assert.True(transport.Sent.All(s => s.WasMulticast), "announcements are multicast");
        Assert.True(transport.Sent.All(s => s.Via.Index == ClientNic.Index),
            "announcements must leave only by the advertised interface");
    }

    [TestCase("Shutdown retracts everything with TTL zero")]
    [Requirement("REQ-LIF-003")]
    public static void Goodbye_retracts_the_advertisement()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(1, transport.Sent.Count, "one goodbye per advertised interface");

        DnsMessage goodbye = transport.Sent[0].Parsed;
        Assert.True(goodbye.Answers.Count > 0, "a goodbye must name what it retracts");

        foreach (DnsRecord record in goodbye.Answers)
        {
            Assert.Equal(0u, record.Ttl,
                $"{record.Name} {record.Type} must have TTL 0 so clients drop it at once");
        }
    }

    // ---- Construction -------------------------------------------------------

    [TestCase("A responder advertising on an interface the transport lacks is rejected")]
    [Requirement("REQ-ADV-011")]
    public static void Advertising_on_unheld_interface_is_rejected()
    {
        var transport = new FakeTransport(ClientNic);

        Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(transport, [new AdvertisedInterface(PrinterNic, BuildAdvertisement())]),
            "advertising on an interface the socket cannot send from would silently produce nothing");
    }

    [TestCase("A responder with nothing to advertise is rejected")]
    [Requirement("REQ-CFG-005")]
    public static void Empty_advertisement_set_is_rejected()
    {
        var transport = new FakeTransport(ClientNic);

        Assert.Throws<ArgumentException>(
            () => _ = new MdnsResponder(transport, []),
            "a responder that answers nobody would bind a port and do nothing");
    }
}

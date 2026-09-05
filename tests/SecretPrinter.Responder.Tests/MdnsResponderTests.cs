// -----------------------------------------------------------------------------
// MdnsResponderTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
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
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class MdnsResponderTests
{
    private static readonly MdnsInterface ClientNic =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13);

    private static readonly MdnsInterface PrinterNic =
        new("Wi-Fi", IPAddress.Parse("192.168.12.245"), 11);

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

    private static Advertisement BuildAdvertisement() =>
        AdvertisementBuilder.Build(
            new PrinterCapabilities(EpsonTxt, 631, CapabilitySource.ForTest("responder tests")),
            new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631),
            ClientNic.Address);

    private static (MdnsResponder Responder, FakeTransport Transport) Build()
    {
        var transport = new FakeTransport(ClientNic);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, BuildAdvertisement())]);
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

    [TestCase("Every record sent comes from our own advertisement")]
    [Requirement("REQ-SEC-001")]
    public static void Nothing_sent_originates_from_a_received_packet()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();
        Advertisement ours = BuildAdvertisement();

        var permitted = new HashSet<string>(
            ours.Records.Select(r => $"{r.Name}|{r.Type}"), StringComparer.Ordinal);

        // A realistic mixture: our own service, someone else's, and answers we
        // never asked for.
        Handle(responder, ForeignResponse("_smb._tcp.local"));
        Handle(responder, Query("_ipp._tcp.local"));
        Handle(responder, ForeignResponse("_airplay._tcp.local"));
        Handle(responder, Query("_universal._sub._ipp._tcp.local"));
        Handle(responder, Query("_smb._tcp.local"));

        Assert.True(transport.Sent.Count > 0, "our own service must still have been answered");

        foreach (SentDatagram sent in transport.Sent)
        {
            foreach (DnsRecord record in sent.Parsed.AllRecords)
            {
                Assert.True(permitted.Contains($"{record.Name}|{record.Type}"),
                    $"{record.Type} {record.Name} was emitted but is not in our advertisement, "
                    + "which means something received was passed on");
            }
        }
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

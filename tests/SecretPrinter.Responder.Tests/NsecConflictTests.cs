// -----------------------------------------------------------------------------
// NsecConflictTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-01, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks how the responder judges an NSEC record for one of the names it
//   claims, heard in a response. This comes before the responder sends NSEC
//   records itself (README REQ-ADV-022), because it will hear its own: a
//   responder that took its own NSEC for somebody else's claim would withdraw
//   the printer the first time it answered with one.
//
//   An NSEC says which types exist at its name. One that says exactly what the
//   responder's own records say, the types it publishes at that name, is not a
//   conflict, whoever sent it: identical records never are (RFC 6762 s9). One
//   that says anything else, another type present or one of the responder's
//   own absent, contradicts the responder's records, and is a conflict
//   (README REQ-ADV-024).
//
//   The next domain name is not compared. RFC 6762 s6.1 says a receiver SHOULD
//   ignore it when it is not the record's own name, and process the rest of the
//   record as usual. The rest of the record is the list of types.
//
// Why these carry no [Requirement] marker:
//   They check REQ-ADV-024's definition of a conflict as it applies to NSEC
//   records. README.md does not yet name the NSEC among the records the service
//   sends; that comes with the change that makes the responder send it, and
//   the markers come with it.
//
// Addresses:
//   fe80::10 is made up, scoped to 13, the IPv6 index of the test adapter.
//   192.168.1.234 is the test adapter's own address, 192.168.1.77 another
//   device's. No real address appears here.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class NsecConflictTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    private static readonly IPAddress LinkLocal = IPAddress.Parse("fe80::10%13");

    private static (MdnsResponder Responder, Advertisement Advertisement) Build(params IPAddress[] linkLocal)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement(linkLocal);
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)]);
        return (responder, advertisement);
    }

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static DnsName Instance(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.Srv).Name;

    private static OutgoingRecord Nsec(DnsName name, params DnsRecordType[] types) =>
        new(name, DnsRecordType.Nsec, 120, CacheFlush: true, new NsecPayload(name, types));

    /// <summary>Hands the responder a response carrying these records, from this address.</summary>
    private static void Hear(MdnsResponder responder, string source, params OutgoingRecord[] records)
    {
        var builder = new DnsResponseBuilder();
        foreach (OutgoingRecord record in records)
        {
            builder.AddAnswer(record);
        }

        Hear(responder, builder.Build(), source);
    }

    private static void Hear(MdnsResponder responder, byte[] payload, string source) =>
        responder.HandleAsync(
            new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(source), 5353), ClientNic.Index, ClientNic),
            CancellationToken.None).GetAwaiter().GetResult();

    // ---- Not a conflict -------------------------------------------------------

    [TestCase("The responder's own NSEC for the host, A and AAAA, heard back, is not a conflict")]
    public static void Own_host_nsec_heard_back_is_not_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);

        Hear(responder, "192.168.1.234", Nsec(Host(advertisement), DnsRecordType.A, DnsRecordType.Aaaa));

        Assert.True(responder.Conflict is null,
            "it says exactly what the responder's own records say: the host has an A and an AAAA record");
    }

    [TestCase("With no link-local address, the responder's own NSEC for the host, A alone, heard back, is not a conflict")]
    public static void Own_host_nsec_without_aaaa_heard_back_is_not_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build();

        Hear(responder, "192.168.1.234", Nsec(Host(advertisement), DnsRecordType.A));

        Assert.True(responder.Conflict is null, "the host has an A record and nothing else");
    }

    [TestCase("The responder's own NSEC for the instance, SRV and TXT, heard back, is not a conflict")]
    public static void Own_instance_nsec_heard_back_is_not_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);

        Hear(responder, "192.168.1.234", Nsec(Instance(advertisement), DnsRecordType.Txt, DnsRecordType.Srv));

        Assert.True(responder.Conflict is null, "the instance has an SRV and a TXT record and nothing else");
    }

    [TestCase("An NSEC listing the responder's own types, from another device, is not a conflict")]
    public static void Identical_nsec_from_another_device_is_not_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);

        Hear(responder, "192.168.1.77", Nsec(Host(advertisement), DnsRecordType.Aaaa, DnsRecordType.A));

        Assert.True(responder.Conflict is null,
            "RFC 6762 section 9: records with identical data are never inconsistent, whoever sends them");
    }

    [TestCase("An NSEC with a compressed next domain name, listing the responder's own types, is not a conflict")]
    public static void Compressed_nsec_listing_our_types_is_not_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        // Written out by hand: the record's own name starts at offset 12, so
        // its next domain name is the pointer 0xC0 0x0C. The bitmap lists A
        // (0x40 in byte 0) and AAAA (0x08 in byte 3).
        byte[] message = [.. Header(1), .. RecordBytes(host, [0xC0, 0x0C, 0x00, 0x04, 0x40, 0x00, 0x00, 0x08])];
        Assert.True(DnsMessage.Parse(message, message.Length).Answers.Single().NsecTypes!.Count == 2,
            "the hand-built record reads as an NSEC listing two types");

        Hear(responder, message, "192.168.1.234");

        Assert.True(responder.Conflict is null,
            "compression is how a name is written, not what it says (RFC 6762 section 8.2)");
    }

    [TestCase("An NSEC naming another next domain name, listing the responder's own types, is not a conflict")]
    public static void Next_domain_name_is_not_compared()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        // The writer refuses this form, so it is written out by hand.
        byte[] next = [.. NameBytes(DnsName.Parse("elsewhere.local")), 0x00, 0x04, 0x40, 0x00, 0x00, 0x08];
        byte[] message = [.. Header(1), .. RecordBytes(host, next)];

        Hear(responder, message, "192.168.1.77");

        Assert.True(responder.Conflict is null,
            "RFC 6762 section 6.1: a receiver should ignore a next domain name that is not the record's own");
    }

    // ---- A conflict -----------------------------------------------------------

    [TestCase("An NSEC for the host that leaves out its AAAA record is a conflict")]
    public static void Nsec_denying_our_aaaa_is_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);

        Hear(responder, "192.168.1.77", Nsec(Host(advertisement), DnsRecordType.A));

        Assert.True(responder.Conflict is { Type: DnsRecordType.Nsec },
            "it says the host has no AAAA record, and the responder publishes one");
    }

    [TestCase("An NSEC for the host that lists a type the responder does not publish is a conflict")]
    public static void Nsec_asserting_another_type_is_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);

        Hear(responder, "192.168.1.77",
            Nsec(Host(advertisement), DnsRecordType.A, DnsRecordType.Aaaa, DnsRecordType.Txt));

        Assert.True(responder.Conflict is { Type: DnsRecordType.Nsec },
            "it says the host has a TXT record, which only another device could hold");
    }

    [TestCase("An NSEC for one of the responder's names that is not in the restricted form is a conflict")]
    public static void Unreadable_nsec_for_our_name_is_a_conflict()
    {
        (MdnsResponder responder, Advertisement advertisement) = Build(LinkLocal);
        DnsName host = Host(advertisement);

        // Two bitmap blocks: the reader keeps this as raw bytes, so what it
        // says about the host cannot be checked against the responder's records.
        byte[] data = [.. NameBytes(host), 0x00, 0x04, 0x40, 0x00, 0x00, 0x08, 0x01, 0x01, 0x40];
        byte[] message = [.. Header(1), .. RecordBytes(host, data)];

        Hear(responder, message, "192.168.1.77");

        Assert.True(responder.Conflict is { Type: DnsRecordType.Nsec },
            "an NSEC for this name that cannot be shown to agree with the responder's records is treated as disagreeing");
    }

    [TestCase("An NSEC for a name the responder does not claim is not its concern")]
    public static void Nsec_for_another_name_is_not_a_conflict()
    {
        (MdnsResponder responder, _) = Build(LinkLocal);

        Hear(responder, "192.168.1.77", Nsec(DnsName.Parse("_ipp._tcp.local"), DnsRecordType.Ptr));
        Hear(responder, "192.168.1.77", Nsec(DnsName.Parse("another-host.local"), DnsRecordType.A));

        Assert.True(responder.Conflict is null,
            "the service-type name is shared and the other host's name is not the responder's");
    }

    // ---- Wire helpers ---------------------------------------------------------

    private static byte[] Header(int answers) =>
        [0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, (byte)answers, 0x00, 0x00, 0x00, 0x00];

    private static List<byte> NameBytes(DnsName name)
    {
        var bytes = new List<byte>();
        foreach (string label in name.Labels)
        {
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(label);
            bytes.Add((byte)encoded.Length);
            bytes.AddRange(encoded);
        }

        bytes.Add(0);
        return bytes;
    }

    /// <summary>An NSEC record for this name, with exactly this data, class IN, cache-flush bit set, TTL 120.</summary>
    private static List<byte> RecordBytes(DnsName name, byte[] data)
    {
        List<byte> bytes = NameBytes(name);
        bytes.AddRange([0x00, 0x2F, 0x80, 0x01, 0x00, 0x00, 0x00, 0x78, (byte)(data.Length >> 8), (byte)data.Length]);
        bytes.AddRange(data);
        return bytes;
    }
}

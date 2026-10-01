// -----------------------------------------------------------------------------
// NsecRecordTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-01, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks how SecretPrinter.Dns writes and reads NSEC records, the negative
//   answers README REQ-ADV-022 is about. Writing is limited to the restricted
//   form RFC 6762 s6.1 defines: the next domain name is the record's own name,
//   and there is one type bitmap, block 0, of 1 to 32 bytes, without the NSEC
//   bit. Reading decodes that form and keeps any other as raw bytes, because
//   RFC 6762 s6.1 says a message must not be ignored for holding an NSEC
//   record that cannot be parsed.
//
//   The expected bytes below were worked out by hand from RFC 4034 s4.1.2:
//   network bit order, the first bit being bit 0, so bit 1 of byte 0 is type
//   1 (A). For example, AAAA is type 28: byte 28 / 8 = 3, bit 28 % 8 = 4, so
//   0x80 >> 4 = 0x08 in the fourth byte.
//
// Why these tests live in this suite:
//   SecretPrinter.Dns has no suite of its own. Its writer and reader are
//   tested through the suites of the projects that use them, and the responder
//   is the only user of NSEC records.
//
// Why these carry no [Requirement] marker:
//   REQ-ADV-022 is about what the responder sends, and nothing sends an NSEC
//   record yet. Markers go on when the requirement is fully met.
// -----------------------------------------------------------------------------

using System.Text;
using SecretPrinter.Dns;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class NsecRecordTests
{
    private static readonly DnsName Host = DnsName.Parse("secretprinter.local");
    private static readonly DnsName Instance = DnsName.Parse("SecretPrinter (ET-3760)._ipp._tcp.local");

    private const DnsRecordType Mx = (DnsRecordType)15;

    /// <summary>A name as the writer emits it: uncompressed labels, then the root.</summary>
    private static List<byte> NameBytes(DnsName name)
    {
        var bytes = new List<byte>();
        foreach (string label in name.Labels)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(label);
            bytes.Add((byte)encoded.Length);
            bytes.AddRange(encoded);
        }

        bytes.Add(0);
        return bytes;
    }

    private static byte[] Rdata(DnsName next, params byte[] bitmapBlock) => [.. NameBytes(next), .. bitmapBlock];

    private static OutgoingRecord Nsec(DnsName name, DnsName next, params DnsRecordType[] types) =>
        new(name, DnsRecordType.Nsec, 120, CacheFlush: true, new NsecPayload(next, types));

    private static string Hex(IEnumerable<byte> bytes) => Convert.ToHexString([.. bytes]);

    /// <summary>One record as it appears on the wire, its data given exactly.</summary>
    private static List<byte> RawRecord(List<byte> owner, DnsRecordType type, byte[] rdata)
    {
        var bytes = new List<byte>(owner)
        {
            (byte)((ushort)type >> 8), (byte)type,
            0x80, 0x01,                    // class IN, cache-flush bit set
            0x00, 0x00, 0x00, 0x78,        // TTL 120
            (byte)(rdata.Length >> 8), (byte)rdata.Length,
        };
        bytes.AddRange(rdata);
        return bytes;
    }

    /// <summary>A response holding the given records in its Answer section.</summary>
    private static byte[] Response(params List<byte>[] records)
    {
        var bytes = new List<byte>
        {
            0x00, 0x00,                    // ID 0
            0x84, 0x00,                    // response, authoritative
            0x00, 0x00,                    // no questions
            0x00, (byte)records.Length,    // answers
            0x00, 0x00, 0x00, 0x00,        // no authority or additional records
        };
        foreach (List<byte> record in records)
        {
            bytes.AddRange(record);
        }

        return [.. bytes];
    }

    private static DnsMessage Parse(byte[] message) => DnsMessage.Parse(message, message.Length);

    private static List<byte> HostAddressRecord() =>
        RawRecord(NameBytes(Host), DnsRecordType.A, [192, 168, 1, 234]);

    // ---- Writing --------------------------------------------------------------

    [TestCase("The host's NSEC lists A and AAAA in one bitmap, block 0, four bytes long")]
    public static void Host_nsec_is_written_in_the_restricted_form()
    {
        byte[] rdata = DnsRecordWriter.EncodeRdata(
            DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.A, DnsRecordType.Aaaa]));

        Assert.Equal(Hex(Rdata(Host, 0x00, 0x04, 0x40, 0x00, 0x00, 0x08)), Hex(rdata),
            "the host name uncompressed, block 0, length 4, A as 0x40 in byte 0 and AAAA as 0x08 in byte 3");
    }

    [TestCase("The instance's NSEC lists TXT and SRV in a five-byte bitmap")]
    public static void Instance_nsec_is_written_in_the_restricted_form()
    {
        byte[] rdata = DnsRecordWriter.EncodeRdata(
            DnsRecordType.Nsec, new NsecPayload(Instance, [DnsRecordType.Txt, DnsRecordType.Srv]));

        Assert.Equal(Hex(Rdata(Instance, 0x00, 0x05, 0x00, 0x00, 0x80, 0x00, 0x40)), Hex(rdata),
            "TXT, type 16, is 0x80 in byte 2; SRV, type 33, is 0x40 in byte 4");
    }

    [TestCase("A and MX are written as RFC 4034's own example writes them")]
    public static void Bitmap_matches_the_rfc_4034_example()
    {
        byte[] rdata = DnsRecordWriter.EncodeRdata(DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.A, Mx]));

        // RFC 4034 s4.3 encodes A and MX as 0x40 0x01 in the first two bytes of
        // block 0. Its example also lists RRSIG, NSEC and TYPE1234, which the
        // restricted form cannot, so only those two bytes are compared.
        Assert.Equal(Hex(Rdata(Host, 0x00, 0x02, 0x40, 0x01)), Hex(rdata), "A is 0x40 and MX, type 15, is 0x01");
    }

    [TestCase("The order types are listed in does not change the bytes")]
    public static void Type_order_does_not_matter()
    {
        byte[] forwards = DnsRecordWriter.EncodeRdata(
            DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.A, DnsRecordType.Aaaa]));
        byte[] backwards = DnsRecordWriter.EncodeRdata(
            DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.Aaaa, DnsRecordType.A]));

        Assert.Equal(Hex(forwards), Hex(backwards), "a bitmap records which types are present, not their order");
    }

    [TestCase("An NSEC listing no type is refused")]
    public static void Empty_type_list_is_refused()
    {
        Assert.Throws<ArgumentException>(
            () => DnsRecordWriter.EncodeRdata(DnsRecordType.Nsec, new NsecPayload(Host, [])),
            "the restricted form's bitmap is 1 to 32 bytes, so it lists at least one type");
    }

    [TestCase("An NSEC listing NSEC itself is refused")]
    public static void Nsec_bit_is_refused()
    {
        Assert.Throws<ArgumentException>(
            () => DnsRecordWriter.EncodeRdata(
                DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.A, DnsRecordType.Nsec])),
            "RFC 6762 s6.1: a Multicast DNS NSEC record must not have the NSEC bit set");
    }

    [TestCase("An NSEC listing type 0, a question or meta type, or a type above 255 is refused")]
    public static void Types_outside_the_data_range_are_refused()
    {
        foreach (int value in new[] { 0, 128, 255, 256, 1234 })
        {
            Assert.Throws<ArgumentException>(
                () => DnsRecordWriter.EncodeRdata(
                    DnsRecordType.Nsec, new NsecPayload(Host, [DnsRecordType.A, (DnsRecordType)value])),
                $"type {value} is outside the data types 1 to 127 the restricted form lists");
        }
    }

    [TestCase("Types 1 and 127, the ends of the data range, are written")]
    public static void Ends_of_the_data_range_are_written()
    {
        byte[] rdata = DnsRecordWriter.EncodeRdata(
            DnsRecordType.Nsec, new NsecPayload(Host, [(DnsRecordType)1, (DnsRecordType)127]));

        byte[] expected = Rdata(Host, [0x00, 0x10, 0x40, .. new byte[14], 0x01]);
        Assert.Equal(Hex(expected), Hex(rdata), "type 127 is the last bit of byte 15, so the bitmap is 16 bytes");
    }

    [TestCase("An NSEC whose next domain name is not its own name is refused when written")]
    public static void Next_domain_name_must_be_the_record_name()
    {
        var builder = new DnsResponseBuilder().AddAnswer(Nsec(Host, Instance, DnsRecordType.A));

        Assert.Throws<ArgumentException>(() => builder.Build(),
            "RFC 6762 s6.1: the restricted form's next domain name is the record's own name");
    }

    [TestCase("An NSEC payload in a record of another type, or another payload in an NSEC record, is refused")]
    public static void Payload_and_type_must_agree()
    {
        Assert.Throws<ArgumentException>(
            () => DnsRecordWriter.EncodeRdata(DnsRecordType.Txt, new NsecPayload(Host, [DnsRecordType.A])),
            "NSEC data cannot be written as a TXT record");
        Assert.Throws<ArgumentException>(
            () => DnsRecordWriter.EncodeRdata(DnsRecordType.Nsec, new TxtPayload(["txtvers=1"])),
            "nor can TXT data be written as an NSEC record");
    }

    // ---- Reading --------------------------------------------------------------

    [TestCase("An NSEC written by the writer reads back as its name and types, cache-flush bit included")]
    public static void Written_nsec_reads_back()
    {
        byte[] message = new DnsResponseBuilder()
            .AddAnswer(Nsec(Host, Host, DnsRecordType.Aaaa, DnsRecordType.A))
            .Build();

        DnsRecord record = Parse(message).Answers.Single();

        Assert.Equal(DnsRecordType.Nsec, record.Type, "an NSEC record");
        Assert.True(Host.Equals(record.NsecNextDomainName), "its next domain name is its own name");
        Assert.Equal("A,Aaaa", string.Join(',', record.NsecTypes!), "its types, in ascending order");
        Assert.True(record.CacheFlush, "the cache-flush bit as written");
        Assert.Null(record.RawData, "decoded, so not kept as raw bytes");
    }

    [TestCase("A next domain name written as a compression pointer is read as the name it points to")]
    public static void Compressed_next_domain_name_is_read()
    {
        // The record's own name starts at offset 12, just after the header, so
        // the pointer 0xC0 0x0C names it.
        List<byte> nsec = RawRecord(NameBytes(Host), DnsRecordType.Nsec, [0xC0, 0x0C, 0x00, 0x01, 0x40]);

        DnsRecord record = Parse(Response(nsec)).Answers.Single();

        Assert.True(Host.Equals(record.NsecNextDomainName),
            "RFC 6762 s6.1 allows for a compressed next domain name; it reads as the record's own name");
        Assert.Equal("A", string.Join(',', record.NsecTypes!), "and the bitmap still reads");
    }

    [TestCase("An NSEC with a second bitmap block is kept as raw bytes, and the rest of the message is read")]
    public static void Nsec_with_a_second_block_is_kept_raw()
    {
        byte[] rdata = Rdata(Host, 0x00, 0x01, 0x40, 0x01, 0x01, 0x40);
        List<byte> nsec = RawRecord(NameBytes(Host), DnsRecordType.Nsec, rdata);

        DnsMessage message = Parse(Response(nsec, HostAddressRecord()));

        Assert.Null(message.Answers[0].NsecTypes, "two blocks are not the restricted form, so nothing is decoded");
        Assert.Equal(Hex(rdata), Hex(message.Answers[0].RawData!), "the data is kept exactly as it arrived");
        Assert.Equal(DnsRecordType.A, message.Answers[1].Type, "and the record after it is read");
    }

    [TestCase("An NSEC whose bitmap length is 0, or above 32, is kept as raw bytes")]
    public static void Nsec_with_a_bitmap_length_out_of_range_is_kept_raw()
    {
        List<byte> empty = RawRecord(NameBytes(Host), DnsRecordType.Nsec, Rdata(Host, 0x00, 0x00));
        List<byte> longer = RawRecord(NameBytes(Host), DnsRecordType.Nsec, Rdata(Host, [0x00, 33, .. new byte[33]]));

        DnsMessage message = Parse(Response(empty, longer));

        Assert.Null(message.Answers[0].NsecTypes, "a bitmap of 0 bytes is not the restricted form");
        Assert.Null(message.Answers[1].NsecTypes, "nor is one of 33");
        Assert.NotNull(message.Answers[1].RawData, "both are kept as raw bytes");
    }

    [TestCase("An NSEC whose next domain name cannot be read is kept as raw bytes, and the rest of the message is read")]
    public static void Unreadable_next_domain_name_does_not_fail_the_message()
    {
        // A pointer to itself. The NSEC's data starts after the owner name and
        // the ten bytes of type, class, TTL and length.
        int dataAt = 12 + NameBytes(Host).Count + 10;
        byte[] rdata = [(byte)(0xC0 | (dataAt >> 8)), (byte)dataAt, 0x00, 0x01, 0x40];
        List<byte> nsec = RawRecord(NameBytes(Host), DnsRecordType.Nsec, rdata);

        DnsMessage message = Parse(Response(nsec, HostAddressRecord()));

        Assert.Null(message.Answers[0].NsecNextDomainName, "a pointer loop is not a name");
        Assert.Equal(Hex(rdata), Hex(message.Answers[0].RawData!), "the data is kept exactly as it arrived");
        Assert.Equal(DnsRecordType.A, message.Answers[1].Type,
            "RFC 6762 s6.1: a message is not ignored because one of its NSEC records cannot be parsed");
    }

    [TestCase("Bits for type 0 and for types 128 to 255 are ignored when read")]
    public static void Pseudo_type_bits_are_ignored()
    {
        // Byte 0: type 0 (0x80) and A (0x40). Byte 16: type 128 (0x80).
        byte[] bitmap = [0xC0, .. new byte[15], 0x80];
        List<byte> nsec = RawRecord(NameBytes(Host), DnsRecordType.Nsec, Rdata(Host, [0x00, 17, .. bitmap]));

        DnsRecord record = Parse(Response(nsec)).Answers.Single();

        Assert.Equal("A", string.Join(',', record.NsecTypes!),
            "RFC 4034 s4.1.2: bits for pseudo-types are ignored upon being read");
    }
}

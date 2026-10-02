// -----------------------------------------------------------------------------
// DnsResponseBuilder.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Record writing moved into DnsRecordWriter, so that a query's Authority
// Section (an mDNS probe, RFC 6762 s8.2) is written by the same code as a
// response's records, and DnsRecordWriter.EncodeRdata added for comparing
// record data byte for byte (RFC 6762 s8.2, s9), by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-09-28. The bytes
// written for a response are unchanged. Reviewed by a human before merge.
//
// AddQuestion given the class to repeat, which it writes as given, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-01. Before, every repeated question was written in class IN, so a
// legacy querier that asked in class ANY was answered with a question it had
// not asked (docs/findings/2026-10-01-the-responder-ignored-the-question-class.md).
// Reviewed by a human before merge.
//
// NsecPayload added, written only in the restricted form RFC 6762 s6.1 defines
// for Multicast DNS, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-01, for REQ-ADV-022. Nothing sent one when
// this was written; MdnsResponder has since, from a later change the same day.
// (That sentence updated 2026-10-01 by Claude, Claude Opus 5.5; it said
// "Nothing sends one yet".) Reviewed by a human before merge.
//
// Purpose:
//   Builds DNS response messages: the records a responder sends back when
//   answering a Multicast DNS query.
//
//   The reading half of this library (DnsMessage.cs) turns bytes into objects.
//   This file does the reverse for responses, in the same style: no sockets, no
//   policy, no decisions about WHAT to advertise - only how to encode records
//   that something else has already decided on.
//
// Deliberate simplifications, stated so they are not mistaken for oversights:
//   - Name compression is not used when writing. RFC 1035 permits it and real
//     responders use it heavily, but the messages this project emits are small
//     enough to fit comfortably without it, and uncompressed output is far
//     easier to audit byte by byte. If message size ever becomes a constraint,
//     that is the point to revisit this - not before.
//   - Only the record types this project sends are supported: PTR, SRV, TXT,
//     A/AAAA, and NSEC in the restricted form of RFC 6762 s6.1. Anything else
//     must be added deliberately.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecretPrinter.Dns;

/// <summary>The payload of a record being written. One subtype per record type.</summary>
public abstract record DnsRecordPayload;

/// <summary>A PTR record's target: the name being pointed at.</summary>
public sealed record PtrPayload(DnsName Target) : DnsRecordPayload;

/// <summary>An SRV record: where a service instance actually lives.</summary>
public sealed record SrvPayload(ushort Priority, ushort Weight, ushort Port, DnsName Target) : DnsRecordPayload;

/// <summary>A TXT record: key=value strings, in order, exactly as supplied.</summary>
public sealed record TxtPayload(IReadOnlyList<string> Strings) : DnsRecordPayload;

/// <summary>An A or AAAA record's address.</summary>
public sealed record AddressPayload(IPAddress Address) : DnsRecordPayload;

/// <summary>
/// An NSEC record in the restricted form RFC 6762 s6.1 defines for Multicast
/// DNS. It lists the types that exist at its name, and so asserts that no other
/// type does. In Multicast DNS it carries none of its DNSSEC meaning.
/// </summary>
/// <remarks>
/// The writer enforces the restricted form. The next domain name must be the
/// record's own name, and it is written uncompressed, as RFC 4034 s4.1.1
/// requires. At least one type must be listed, so the bitmap is 1 to 32 bytes.
/// Each type must lie between 1 and 127. That keeps it inside the one bitmap
/// block, number 0, the restricted form allows; types above 255 would need
/// another. It also keeps it among IANA's data types: 0 is reserved, and 128 to
/// 255 are question and meta types, which no record at a name has, and
/// RFC 4034 s4.1.2 requires bits for pseudo-types to be clear. NSEC itself must
/// not be listed (RFC 6762 s6.1). Anything else is refused, so a record outside
/// the restricted form cannot be sent.
/// </remarks>
/// <param name="NextDomainName">The record's own name (RFC 6762 s6.1).</param>
/// <param name="Types">The types that exist at the name, in any order.</param>
public sealed record NsecPayload(DnsName NextDomainName, IReadOnlyList<DnsRecordType> Types) : DnsRecordPayload;

/// <summary>A resource record ready to be written to the wire.</summary>
/// <param name="Name">The name this record is about.</param>
/// <param name="Type">The record type.</param>
/// <param name="Ttl">Time to live in seconds. Zero signals a goodbye (RFC 6762 s10.1).</param>
/// <param name="CacheFlush">
/// Sets the mDNS cache-flush bit (RFC 6762 s10.2), telling receivers to replace
/// rather than add to any cached records for this name and type. Correct for
/// records the responder is authoritative for - SRV, TXT, A - and wrong for
/// shared records such as the PTR entries in a service-type listing, where
/// several responders legitimately contribute answers.
/// </param>
/// <param name="Payload">The record's data.</param>
public sealed record OutgoingRecord(
    DnsName Name,
    DnsRecordType Type,
    uint Ttl,
    bool CacheFlush,
    DnsRecordPayload Payload);

/// <summary>Builds a DNS response message. Emits uncompressed names only.</summary>
public sealed class DnsResponseBuilder
{
    private readonly List<byte> _bytes = [];
    private readonly List<(DnsName Name, DnsRecordType Type, ushort RawClass)> _questions = [];
    private readonly List<OutgoingRecord> _answers = [];
    private readonly List<OutgoingRecord> _additionals = [];
    private readonly ushort _id;

    /// <param name="id">
    /// Query identifier. Multicast responses use 0 (RFC 6762 s18.1). A response
    /// to a legacy unicast querier must echo that querier's identifier
    /// (RFC 6762 s6.7).
    /// </param>
    public DnsResponseBuilder(ushort id = 0) => _id = id;

    /// <summary>
    /// Echoes a question back. Multicast responses omit the question section;
    /// legacy unicast responses must repeat it, which is the only reason this
    /// exists.
    /// </summary>
    /// <param name="name">The name asked about.</param>
    /// <param name="type">The type asked for.</param>
    /// <param name="rawClass">
    /// The question's class field exactly as it arrived, written back exactly
    /// as given. RFC 6762 s6.7 requires a legacy unicast response to repeat the
    /// question given in the query, so this builder does not choose a class.
    /// </param>
    public DnsResponseBuilder AddQuestion(DnsName name, DnsRecordType type, ushort rawClass)
    {
        _questions.Add((name, type, rawClass));
        return this;
    }

    public DnsResponseBuilder AddAnswer(OutgoingRecord record)
    {
        _answers.Add(record);
        return this;
    }

    /// <summary>
    /// Adds a record to the additional section: data the querier has not asked
    /// for but will need next. Supplying the SRV, TXT and A records alongside a
    /// PTR answer saves the client three more round trips.
    /// </summary>
    public DnsResponseBuilder AddAdditional(OutgoingRecord record)
    {
        _additionals.Add(record);
        return this;
    }

    public int AnswerCount => _answers.Count;

    public byte[] Build()
    {
        _bytes.Clear();

        // Flags 0x8400: QR (this is a response) and AA (authoritative). mDNS
        // responses are always authoritative - a responder speaks only for
        // names it owns.
        DnsRecordWriter.WriteUInt16(_bytes, _id);
        DnsRecordWriter.WriteUInt16(_bytes, 0x8400);
        DnsRecordWriter.WriteUInt16(_bytes, (ushort)_questions.Count);
        DnsRecordWriter.WriteUInt16(_bytes, (ushort)_answers.Count);
        DnsRecordWriter.WriteUInt16(_bytes, 0); // NSCOUNT
        DnsRecordWriter.WriteUInt16(_bytes, (ushort)_additionals.Count);

        foreach ((DnsName name, DnsRecordType type, ushort rawClass) in _questions)
        {
            DnsRecordWriter.WriteName(_bytes, name);
            DnsRecordWriter.WriteUInt16(_bytes, (ushort)type);
            DnsRecordWriter.WriteUInt16(_bytes, rawClass);
        }

        foreach (OutgoingRecord record in _answers)
        {
            DnsRecordWriter.WriteRecord(_bytes, record);
        }

        foreach (OutgoingRecord record in _additionals)
        {
            DnsRecordWriter.WriteRecord(_bytes, record);
        }

        return [.. _bytes];
    }
}

/// <summary>
/// Writes resource records to the wire. The one record writer in this
/// repository: responses use it for their answers and additionals, and queries
/// for their Authority Section.
/// </summary>
public static class DnsRecordWriter
{
    /// <summary>
    /// The record data (RDATA) a record would be written with, uncompressed. Two
    /// records carry identical data exactly when these bytes are equal, which is
    /// the comparison RFC 6762 s8.2 (probe tiebreaking) and s9 (conflicts) make.
    /// </summary>
    public static byte[] EncodeRdata(DnsRecordType type, DnsRecordPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var bytes = new List<byte>();
        WritePayload(bytes, type, payload);
        return [.. bytes];
    }

    internal static void WriteRecord(List<byte> bytes, OutgoingRecord record)
    {
        // Checked here rather than in WritePayload, because only the record
        // knows its own name.
        if (record.Payload is NsecPayload nsec && !nsec.NextDomainName.Equals(record.Name))
        {
            throw new ArgumentException(
                $"The NSEC record for {record.Name} names {nsec.NextDomainName} as its next domain name. "
                + "RFC 6762 s6.1's restricted form names the record's own name.",
                nameof(record));
        }

        WriteName(bytes, record.Name);
        WriteUInt16(bytes, (ushort)record.Type);
        WriteUInt16(bytes, (ushort)(record.CacheFlush ? 0x8001 : 0x0001));
        WriteUInt32(bytes, record.Ttl);

        // RDLENGTH is not known until the payload is written, so reserve two
        // bytes and patch them afterwards.
        int lengthOffset = bytes.Count;
        WriteUInt16(bytes, 0);
        int start = bytes.Count;

        WritePayload(bytes, record.Type, record.Payload);

        int length = bytes.Count - start;
        if (length > ushort.MaxValue)
        {
            throw new InvalidOperationException($"Record data for {record.Name} is {length} bytes; maximum is 65535.");
        }

        bytes[lengthOffset] = (byte)(length >> 8);
        bytes[lengthOffset + 1] = (byte)(length & 0xFF);
    }

    private static void WritePayload(List<byte> bytes, DnsRecordType type, DnsRecordPayload payload)
    {
        if ((type == DnsRecordType.Nsec) != (payload is NsecPayload))
        {
            throw new ArgumentException(
                $"A {type} record cannot carry a {payload.GetType().Name}: NSEC data goes in NSEC records only.",
                nameof(payload));
        }

        switch (payload)
        {
            case PtrPayload ptr:
                WriteName(bytes, ptr.Target);
                break;

            case SrvPayload srv:
                WriteUInt16(bytes, srv.Priority);
                WriteUInt16(bytes, srv.Weight);
                WriteUInt16(bytes, srv.Port);
                WriteName(bytes, srv.Target);
                break;

            case TxtPayload txt:
                WriteTxt(bytes, txt.Strings);
                break;

            case AddressPayload address:
                WriteAddress(bytes, address.Address, type);
                break;

            case NsecPayload nsec:
                WriteNsec(bytes, nsec);
                break;

            default:
                throw new ArgumentException(
                    $"No writer for payload type {payload.GetType().Name}.", nameof(payload));
        }
    }

    private static void WriteTxt(List<byte> bytes, IReadOnlyList<string> strings)
    {
        // An empty TXT record is written as a single zero-length string rather
        // than as nothing at all: RFC 6763 s6.1 requires TXT records to contain
        // at least one string, and a truly empty RDATA is malformed.
        if (strings.Count == 0)
        {
            bytes.Add(0);
            return;
        }

        foreach (string entry in strings)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(entry);
            if (encoded.Length > 255)
            {
                throw new ArgumentException(
                    $"TXT entry encodes to {encoded.Length} bytes; each entry must be 255 bytes or fewer. "
                    + $"Entry begins: '{entry[..Math.Min(40, entry.Length)]}'",
                    nameof(strings));
            }

            bytes.Add((byte)encoded.Length);
            bytes.AddRange(encoded);
        }
    }

    /// <summary>
    /// Writes NSEC data in the restricted form of RFC 6762 s6.1: the next domain
    /// name, uncompressed like every name this writer emits, then one type
    /// bitmap, block 0, as short as the highest listed type allows.
    /// </summary>
    private static void WriteNsec(List<byte> bytes, NsecPayload nsec)
    {
        ArgumentNullException.ThrowIfNull(nsec.NextDomainName);
        ArgumentNullException.ThrowIfNull(nsec.Types);

        if (nsec.Types.Count == 0)
        {
            throw new ArgumentException(
                "An NSEC record in the restricted form lists at least one type: its bitmap is 1 to 32 bytes "
                + "(RFC 6762 s6.1).",
                nameof(nsec));
        }

        var bitmap = new byte[32];
        int length = 0;

        foreach (DnsRecordType type in nsec.Types)
        {
            if (type == DnsRecordType.Nsec)
            {
                throw new ArgumentException(
                    "A Multicast DNS NSEC record must not list NSEC itself (RFC 6762 s6.1).", nameof(nsec));
            }

            int value = (int)type;
            if (value is < 1 or > 127)
            {
                throw new ArgumentException(
                    $"Type {value} cannot be listed. The restricted form lists data types 1 to 127: type 0 is "
                    + "reserved, 128 to 255 are question and meta types that no record at a name has, and types "
                    + "above 255 need a bitmap block other than 0 (RFC 6762 s6.1, RFC 4034 s4.1.2).",
                    nameof(nsec));
            }

            // Network bit order, the first bit being bit 0: bit 1 of byte 0 is
            // type 1, A (RFC 4034 s4.1.2). Trailing zero bytes are not written,
            // as that section requires, because the length stops at the byte
            // holding the highest type.
            bitmap[value / 8] |= (byte)(0x80 >> (value % 8));
            length = Math.Max(length, (value / 8) + 1);
        }

        WriteName(bytes, nsec.NextDomainName);
        bytes.Add(0);             // window block number: 0
        bytes.Add((byte)length);  // bitmap length in bytes: 1 to 16, within the 1 to 32 allowed
        bytes.AddRange(bitmap.AsSpan(0, length).ToArray());
    }

    private static void WriteAddress(List<byte> bytes, IPAddress address, DnsRecordType type)
    {
        AddressFamily expected = type == DnsRecordType.Aaaa
            ? AddressFamily.InterNetworkV6
            : AddressFamily.InterNetwork;

        if (address.AddressFamily != expected)
        {
            throw new ArgumentException(
                $"Record type {type} requires an {expected} address, but got {address.AddressFamily}.",
                nameof(address));
        }

        bytes.AddRange(address.GetAddressBytes());
    }

    internal static void WriteUInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)(value & 0xFF));
    }

    private static void WriteUInt32(List<byte> bytes, uint value)
    {
        bytes.Add((byte)(value >> 24));
        bytes.Add((byte)((value >> 16) & 0xFF));
        bytes.Add((byte)((value >> 8) & 0xFF));
        bytes.Add((byte)(value & 0xFF));
    }

    internal static void WriteName(List<byte> bytes, DnsName name)
    {
        foreach (string label in name.Labels)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(label);
            if (encoded.Length is 0 or > 63)
            {
                throw new ArgumentException(
                    $"Label '{label}' encodes to {encoded.Length} bytes; DNS labels must be 1-63 bytes.",
                    nameof(name));
            }

            bytes.Add((byte)encoded.Length);
            bytes.AddRange(encoded);
        }

        bytes.Add(0); // root label
    }
}

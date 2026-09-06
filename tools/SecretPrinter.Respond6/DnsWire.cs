// DnsWire.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Minimal DNS wire-format reader/writer for the SecretPrinter.Respond6 experiment.
//
// WHY THIS IS SELF-CONTAINED AND DOES NOT USE SecretPrinter.Dns
// ------------------------------------------------------------
// SecretPrinter.Dns already implements DNS wire format for the shipping product,
// and reusing it would normally be correct. It is deliberately NOT used here for
// two reasons, both stated plainly so nobody has to guess at the motive:
//
//   1. Claude wrote this file without access to the SecretPrinter.Dns public API
//      surface, and inventing call signatures that may not exist would be a guess.
//      This project's rule is that we do not guess.
//
//   2. Keeping the experiment standalone means it cannot accidentally alter,
//      depend on, or be constrained by production code while we are still
//      deciding what the production answer should be.
//
// Consequence to be aware of: a bug here is a bug in the EXPERIMENT only, and
// proves nothing about SecretPrinter.Dns. If this experiment produces a
// surprising result, suspect this file before suspecting the product.
//
// KNOWN LIMITATIONS (deliberate, documented, experiment-scoped)
// -------------------------------------------------------------
//   * Names are written WITHOUT compression pointers. This is legal but produces
//     larger packets than Bonjour would. Reading DOES follow compression pointers,
//     because incoming queries from iOS use them.
//   * Names are split on '.' when written, so a label containing a literal dot
//     cannot be represented. No name used by this experiment contains one.
//   * No probing (RFC 6762 section 8.1) and no known-answer suppression
//     (section 7.1) are implemented. Neither is needed to answer the one
//     question this experiment exists to answer.

using System.Text;

namespace SecretPrinter.Respond6;

/// <summary>DNS resource record type codes used by this experiment.</summary>
internal static class RrType
{
    public const ushort A = 1;
    public const ushort PTR = 12;
    public const ushort TXT = 16;
    public const ushort AAAA = 28;
    public const ushort SRV = 33;
    public const ushort NSEC = 47;
    public const ushort HTTPS = 65;
    public const ushort ANY = 255;

    public static string Name(ushort t) => t switch
    {
        A => "A",
        PTR => "PTR",
        TXT => "TXT",
        AAAA => "AAAA",
        SRV => "SRV",
        NSEC => "NSEC",
        HTTPS => "HTTPS/SVCB",
        ANY => "ANY",
        _ => $"TYPE{t}",
    };
}

/// <summary>A single question parsed out of an incoming mDNS query.</summary>
/// <param name="Name">Fully qualified name, without a trailing dot.</param>
/// <param name="QType">Record type requested.</param>
/// <param name="QClass">Class with the top (unicast-response) bit already stripped.</param>
/// <param name="WantsUnicastReply">True if the QU bit was set on the question.</param>
internal readonly record struct DnsQuestion(
    string Name,
    ushort QType,
    ushort QClass,
    bool WantsUnicastReply);

/// <summary>Reads just enough of a DNS message to extract its questions.</summary>
internal static class DnsReader
{
    /// <summary>
    /// Parses the header and question section of a DNS message.
    /// Returns false if the message is malformed or is not a query.
    /// Answer/authority/additional sections are intentionally not parsed;
    /// this experiment does not implement known-answer suppression.
    /// </summary>
    public static bool TryReadQuestions(
        ReadOnlySpan<byte> datagram,
        out ushort transactionId,
        out List<DnsQuestion> questions)
    {
        transactionId = 0;
        questions = new List<DnsQuestion>();

        if (datagram.Length < 12)
        {
            return false;
        }

        byte[] buf = datagram.ToArray();

        transactionId = ReadU16(buf, 0);
        ushort flags = ReadU16(buf, 2);
        ushort qdCount = ReadU16(buf, 4);

        // QR bit set means this is a response, not a query. We answer queries only.
        bool isResponse = (flags & 0x8000) != 0;
        if (isResponse)
        {
            return false;
        }

        int pos = 12;
        for (int i = 0; i < qdCount; i++)
        {
            string name;
            try
            {
                name = ReadName(buf, ref pos);
            }
            catch (FormatException)
            {
                // Truncated or hostile name encoding. Abandon the whole message
                // rather than acting on a partial parse.
                return false;
            }

            if (pos + 4 > buf.Length)
            {
                return false;
            }

            ushort qtype = ReadU16(buf, pos);
            ushort qclassRaw = ReadU16(buf, pos + 2);
            pos += 4;

            bool qu = (qclassRaw & 0x8000) != 0;
            ushort qclass = (ushort)(qclassRaw & 0x7FFF);

            questions.Add(new DnsQuestion(name, qtype, qclass, qu));
        }

        return true;
    }

    /// <summary>
    /// Reads a DNS name, following compression pointers. Advances <paramref name="pos"/>
    /// past the name as it appears at the original offset (not past the pointer target).
    /// </summary>
    private static string ReadName(byte[] buf, ref int pos)
    {
        var sb = new StringBuilder();
        int p = pos;
        int jumps = 0;
        bool jumped = false;

        while (true)
        {
            if (p >= buf.Length)
            {
                throw new FormatException("Name runs past end of message.");
            }

            int len = buf[p];

            if (len == 0)
            {
                p++;
                break;
            }

            if ((len & 0xC0) == 0xC0)
            {
                if (p + 1 >= buf.Length)
                {
                    throw new FormatException("Truncated compression pointer.");
                }

                int target = ((len & 0x3F) << 8) | buf[p + 1];

                if (!jumped)
                {
                    pos = p + 2;
                    jumped = true;
                }

                // Bound the jump count so a pointer loop cannot hang the process.
                if (++jumps > 16)
                {
                    throw new FormatException("Too many compression pointers.");
                }

                p = target;
                continue;
            }

            if ((len & 0xC0) != 0)
            {
                throw new FormatException("Reserved label type in name.");
            }

            p++;

            if (p + len > buf.Length)
            {
                throw new FormatException("Label runs past end of message.");
            }

            if (sb.Length > 0)
            {
                sb.Append('.');
            }

            sb.Append(Encoding.UTF8.GetString(buf, p, len));
            p += len;
        }

        if (!jumped)
        {
            pos = p;
        }

        return sb.ToString();
    }

    private static ushort ReadU16(byte[] b, int off) =>
        (ushort)((b[off] << 8) | b[off + 1]);
}

/// <summary>Builds an mDNS response message.</summary>
internal sealed class DnsResponseBuilder
{
    // mDNS class IN, and the cache-flush bit that marks a record set as
    // authoritative and unique (RFC 6762 section 10.2).
    private const ushort ClassIn = 1;
    private const ushort CacheFlush = 0x8000;

    private readonly List<byte[]> _answers = new();
    private readonly List<byte[]> _additionals = new();

    public int AnswerCount => _answers.Count;

    public int AdditionalCount => _additionals.Count;

    public void AddPtr(string name, string target, uint ttl, bool additional = false)
    {
        var rdata = new List<byte>();
        WriteName(rdata, target);
        // PTR records pointing at service instances are SHARED, not unique,
        // so the cache-flush bit must NOT be set (RFC 6762 section 10.2).
        Add(BuildRecord(name, RrType.PTR, ClassIn, ttl, rdata.ToArray()), additional);
    }

    public void AddSrv(
        string name,
        ushort priority,
        ushort weight,
        ushort port,
        string target,
        uint ttl,
        bool additional = false)
    {
        var rdata = new List<byte>();
        WriteU16(rdata, priority);
        WriteU16(rdata, weight);
        WriteU16(rdata, port);
        WriteName(rdata, target);
        Add(BuildRecord(name, RrType.SRV, ClassIn | CacheFlush, ttl, rdata.ToArray()), additional);
    }

    public void AddTxt(string name, IReadOnlyList<string> entries, uint ttl, bool additional = false)
    {
        var rdata = new List<byte>();

        if (entries.Count == 0)
        {
            // An empty TXT record is a single zero-length string, not zero bytes
            // (RFC 6763 section 6.1).
            rdata.Add(0);
        }
        else
        {
            foreach (string entry in entries)
            {
                byte[] b = Encoding.UTF8.GetBytes(entry);
                if (b.Length > 255)
                {
                    throw new ArgumentException(
                        $"TXT entry exceeds 255 bytes: {entry}", nameof(entries));
                }

                rdata.Add((byte)b.Length);
                rdata.AddRange(b);
            }
        }

        Add(BuildRecord(name, RrType.TXT, ClassIn | CacheFlush, ttl, rdata.ToArray()), additional);
    }

    public void AddA(string name, byte[] ipv4, uint ttl, bool additional = false)
    {
        if (ipv4.Length != 4)
        {
            throw new ArgumentException("An A record needs exactly 4 bytes.", nameof(ipv4));
        }

        Add(BuildRecord(name, RrType.A, ClassIn | CacheFlush, ttl, ipv4), additional);
    }

    /// <summary>
    /// Adds an NSEC record asserting that <paramref name="name"/> has an A record
    /// and nothing else. This is how mDNS says "no, there is no AAAA here" rather
    /// than staying silent and leaving the client to retry until it times out
    /// (RFC 6762 section 6.1).
    /// </summary>
    public void AddNsecAOnly(string name, uint ttl, bool additional = false)
    {
        var rdata = new List<byte>();
        WriteName(rdata, name);

        // Type bitmap, window block 0. Bit for type 1 (A) is bit 1 of byte 0,
        // counting from the most significant bit: 0x80 >> 1 == 0x40.
        rdata.Add(0x00); // window block number
        rdata.Add(0x01); // bitmap length in bytes
        rdata.Add(0x40); // bit 1 set == type A present

        Add(BuildRecord(name, RrType.NSEC, ClassIn | CacheFlush, ttl, rdata.ToArray()), additional);
    }

    /// <summary>
    /// Serialises the message. Per RFC 6762 section 18.1 the transaction ID of a
    /// multicast response must be zero, so no ID is accepted here.
    /// </summary>
    public byte[] Build()
    {
        var o = new List<byte>();

        WriteU16(o, 0);       // Transaction ID: zero for multicast responses.
        WriteU16(o, 0x8400);  // QR=1 (response), AA=1 (authoritative).
        WriteU16(o, 0);       // QDCOUNT: responses carry no questions.
        WriteU16(o, (ushort)_answers.Count);
        WriteU16(o, 0);       // NSCOUNT.
        WriteU16(o, (ushort)_additionals.Count);

        foreach (byte[] rr in _answers)
        {
            o.AddRange(rr);
        }

        foreach (byte[] rr in _additionals)
        {
            o.AddRange(rr);
        }

        return o.ToArray();
    }

    private void Add(byte[] record, bool additional)
    {
        if (additional)
        {
            _additionals.Add(record);
        }
        else
        {
            _answers.Add(record);
        }
    }

    private static byte[] BuildRecord(
        string name,
        ushort type,
        ushort cls,
        uint ttl,
        byte[] rdata)
    {
        var o = new List<byte>();
        WriteName(o, name);
        WriteU16(o, type);
        WriteU16(o, cls);
        WriteU32(o, ttl);
        WriteU16(o, (ushort)rdata.Length);
        o.AddRange(rdata);
        return o.ToArray();
    }

    private static void WriteName(List<byte> o, string name)
    {
        foreach (string label in name.Split('.'))
        {
            if (label.Length == 0)
            {
                continue;
            }

            byte[] b = Encoding.UTF8.GetBytes(label);

            if (b.Length > 63)
            {
                throw new ArgumentException($"Label exceeds 63 bytes: {label}", nameof(name));
            }

            o.Add((byte)b.Length);
            o.AddRange(b);
        }

        o.Add(0); // Root label terminates the name.
    }

    private static void WriteU16(List<byte> o, ushort v)
    {
        o.Add((byte)(v >> 8));
        o.Add((byte)(v & 0xFF));
    }

    private static void WriteU32(List<byte> o, uint v)
    {
        o.Add((byte)(v >> 24));
        o.Add((byte)((v >> 16) & 0xFF));
        o.Add((byte)((v >> 8) & 0xFF));
        o.Add((byte)(v & 0xFF));
    }
}

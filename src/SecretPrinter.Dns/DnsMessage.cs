// -----------------------------------------------------------------------------
// DnsMessage.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   A deliberately small, dependency-free DNS wire-format reader and writer,
//   sufficient for the subset of DNS used by Multicast DNS (RFC 6762) and
//   DNS-Based Service Discovery (RFC 6763).
//
//   This library is shared by every SecretPrinter component so that there is
//   exactly one implementation of DNS parsing in this repository to audit.
//
// Scope and honesty notes:
//   - Nothing in this file opens a socket or sends a packet. It converts bytes
//     to objects and back, and does nothing else.
//   - It implements only the record types this project needs: A, AAAA, PTR,
//     SRV and TXT. Any other record type is preserved as raw bytes and
//     reported as such, never silently dropped.
//   - Name compression (RFC 1035 s4.1.4) is supported for reading, with an
//     explicit jump limit to prevent malicious pointer loops.
//   - Name compression is NOT used when writing. The queries this project
//     emits are small; emitting uncompressed names keeps the writer trivially
//     auditable.
//   - Parsing is intentionally strict: malformed input raises
//     InvalidDataException rather than being guessed at.
// -----------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SecretPrinter.Dns;

/// <summary>DNS resource record types used by this project.</summary>
/// <remarks>
/// Values carry the names assigned by IANA. "PTR" is the standard name of DNS
/// record type 12 and appears as such in RFC 1035 and RFC 6763; renaming it to
/// satisfy a naming analyzer would make this code harder to check against the
/// specifications it implements, which matters more here than the guideline.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "PTR is the IANA-assigned name of DNS record type 12. Matching the RFCs is worth more than avoiding the substring 'ptr'.")]
public enum DnsRecordType : ushort
{
    A = 1,
    Ptr = 12,
    Txt = 16,
    Aaaa = 28,
    Srv = 33,
    Nsec = 47,
    Any = 255,
}

/// <summary>
/// A DNS name held as its individual labels.
/// </summary>
/// <remarks>
/// Labels are kept separate rather than as a single dotted string because
/// DNS-SD instance names may legitimately contain '.' characters
/// (RFC 6763 s4.1.1). Collapsing to a dotted string on read and splitting on
/// '.' when writing would corrupt such names. <see cref="ToString"/> escapes
/// dots and backslashes for display only.
/// </remarks>
public sealed class DnsName : IEquatable<DnsName>
{
    public IReadOnlyList<string> Labels { get; }

    public DnsName(IReadOnlyList<string> labels) => Labels = labels;

    /// <summary>
    /// Parses a dotted textual name, honouring backslash escapes so that
    /// literal dots inside a label can be expressed as "\.".
    /// </summary>
    public static DnsName Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var labels = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\')
            {
                if (i + 1 >= text.Length)
                {
                    throw new FormatException($"Name '{text}' ends with a dangling escape character.");
                }

                current.Append(text[++i]);
            }
            else if (c == '.')
            {
                labels.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        // A trailing dot denotes the root and produces no extra label.
        if (current.Length > 0)
        {
            labels.Add(current.ToString());
        }

        return new DnsName(labels);
    }

    /// <summary>Escaped, human-readable form. Do not round-trip through this.</summary>
    public override string ToString()
    {
        var parts = new List<string>(Labels.Count);
        foreach (string label in Labels)
        {
            parts.Add(label.Replace("\\", "\\\\", StringComparison.Ordinal)
                           .Replace(".", "\\.", StringComparison.Ordinal));
        }

        return string.Join('.', parts);
    }

    public bool Equals(DnsName? other)
    {
        if (other is null || other.Labels.Count != Labels.Count)
        {
            return false;
        }

        for (int i = 0; i < Labels.Count; i++)
        {
            // DNS label comparison is case-insensitive (RFC 1035 s2.3.3).
            if (!string.Equals(Labels[i], other.Labels[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as DnsName);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (string label in Labels)
        {
            hash.Add(label.ToLowerInvariant());
        }

        return hash.ToHashCode();
    }
}

/// <summary>A parsed resource record. Exactly one of the payload properties is set.</summary>
public sealed class DnsRecord
{
    public required DnsName Name { get; init; }
    public required DnsRecordType Type { get; init; }

    /// <summary>Raw CLASS field, including the mDNS cache-flush bit (0x8000).</summary>
    public required ushort RawClass { get; init; }

    public required uint Ttl { get; init; }

    /// <summary>True when the mDNS cache-flush bit was set (RFC 6762 s10.2).</summary>
    public bool CacheFlush => (RawClass & 0x8000) != 0;

    public DnsName? PtrTarget { get; init; }
    public DnsName? SrvTarget { get; init; }
    public ushort SrvPort { get; init; }
    public ushort SrvPriority { get; init; }
    public ushort SrvWeight { get; init; }

    /// <summary>TXT key/value strings, exactly as sent, in order.</summary>
    public IReadOnlyList<string>? TxtStrings { get; init; }

    public System.Net.IPAddress? Address { get; init; }

    /// <summary>RDATA for record types this tool does not decode.</summary>
    public byte[]? RawData { get; init; }
}

/// <summary>A parsed DNS message.</summary>
public sealed class DnsMessage
{
    public required ushort Id { get; init; }
    public required ushort Flags { get; init; }
    public required IReadOnlyList<(DnsName Name, DnsRecordType Type, ushort RawClass)> Questions { get; init; }
    public required IReadOnlyList<DnsRecord> Answers { get; init; }
    public required IReadOnlyList<DnsRecord> Authorities { get; init; }
    public required IReadOnlyList<DnsRecord> Additionals { get; init; }

    /// <summary>All records from every section, in wire order.</summary>
    public IEnumerable<DnsRecord> AllRecords => Answers.Concat(Authorities).Concat(Additionals);

    public bool IsResponse => (Flags & 0x8000) != 0;

    public static DnsMessage Parse(byte[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var reader = new DnsReader(buffer, length);

        ushort id = reader.ReadUInt16();
        ushort flags = reader.ReadUInt16();
        int qdCount = reader.ReadUInt16();
        int anCount = reader.ReadUInt16();
        int nsCount = reader.ReadUInt16();
        int arCount = reader.ReadUInt16();

        var questions = new List<(DnsName, DnsRecordType, ushort)>(qdCount);
        for (int i = 0; i < qdCount; i++)
        {
            DnsName name = reader.ReadName();
            var type = (DnsRecordType)reader.ReadUInt16();
            ushort cls = reader.ReadUInt16();
            questions.Add((name, type, cls));
        }

        List<DnsRecord> ReadSection(int count)
        {
            var records = new List<DnsRecord>(count);
            for (int i = 0; i < count; i++)
            {
                records.Add(reader.ReadRecord());
            }

            return records;
        }

        return new DnsMessage
        {
            Id = id,
            Flags = flags,
            Questions = questions,
            Answers = ReadSection(anCount),
            Authorities = ReadSection(nsCount),
            Additionals = ReadSection(arCount),
        };
    }
}

/// <summary>Sequential reader over a DNS message buffer.</summary>
public sealed class DnsReader
{
    private const int MaxCompressionJumps = 64;

    private readonly byte[] _buffer;
    private readonly int _length;
    private int _position;

    public DnsReader(byte[] buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    private void Require(int count)
    {
        if (_position + count > _length)
        {
            throw new InvalidDataException(
                $"Message truncated: wanted {count} byte(s) at offset {_position}, message is {_length} byte(s).");
        }
    }

    public ushort ReadUInt16()
    {
        Require(2);
        ushort value = (ushort)((_buffer[_position] << 8) | _buffer[_position + 1]);
        _position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        Require(4);
        uint value = ((uint)_buffer[_position] << 24)
                   | ((uint)_buffer[_position + 1] << 16)
                   | ((uint)_buffer[_position + 2] << 8)
                   | _buffer[_position + 3];
        _position += 4;
        return value;
    }

    public byte[] ReadBytes(int count)
    {
        Require(count);
        var value = new byte[count];
        Array.Copy(_buffer, _position, value, 0, count);
        _position += count;
        return value;
    }

    /// <summary>Reads a name, following compression pointers with a jump limit.</summary>
    public DnsName ReadName()
    {
        var labels = new List<string>();
        int position = _position;
        int jumps = 0;
        bool jumped = false;

        while (true)
        {
            if (position >= _length)
            {
                throw new InvalidDataException($"Name runs past end of message at offset {position}.");
            }

            byte lengthByte = _buffer[position];

            if (lengthByte == 0)
            {
                position++;
                break;
            }

            if ((lengthByte & 0xC0) == 0xC0)
            {
                if (position + 1 >= _length)
                {
                    throw new InvalidDataException($"Truncated compression pointer at offset {position}.");
                }

                int target = ((lengthByte & 0x3F) << 8) | _buffer[position + 1];
                position += 2;

                // The first pointer encountered fixes where the caller resumes.
                if (!jumped)
                {
                    _position = position;
                    jumped = true;
                }

                if (++jumps > MaxCompressionJumps)
                {
                    throw new InvalidDataException("Compression pointer loop detected.");
                }

                position = target;
                continue;
            }

            if ((lengthByte & 0xC0) != 0)
            {
                throw new InvalidDataException(
                    $"Reserved label type 0x{lengthByte:X2} at offset {position}.");
            }

            position++;
            if (position + lengthByte > _length)
            {
                throw new InvalidDataException($"Label runs past end of message at offset {position}.");
            }

            labels.Add(Encoding.UTF8.GetString(_buffer, position, lengthByte));
            position += lengthByte;
        }

        if (!jumped)
        {
            _position = position;
        }

        return new DnsName(labels);
    }

    public DnsRecord ReadRecord()
    {
        DnsName name = ReadName();
        var type = (DnsRecordType)ReadUInt16();
        ushort rawClass = ReadUInt16();
        uint ttl = ReadUInt32();
        int rdLength = ReadUInt16();

        Require(rdLength);
        int rdataStart = _position;
        int rdataEnd = rdataStart + rdLength;

        DnsRecord record;

        switch (type)
        {
            case DnsRecordType.Ptr:
                record = new DnsRecord
                {
                    Name = name, Type = type, RawClass = rawClass, Ttl = ttl,
                    PtrTarget = ReadName(),
                };
                break;

            case DnsRecordType.Srv:
            {
                ushort priority = ReadUInt16();
                ushort weight = ReadUInt16();
                ushort port = ReadUInt16();
                DnsName target = ReadName();
                record = new DnsRecord
                {
                    Name = name, Type = type, RawClass = rawClass, Ttl = ttl,
                    SrvPriority = priority, SrvWeight = weight, SrvPort = port, SrvTarget = target,
                };
                break;
            }

            case DnsRecordType.Txt:
            {
                var strings = new List<string>();
                while (_position < rdataEnd)
                {
                    int stringLength = _buffer[_position++];
                    if (_position + stringLength > rdataEnd)
                    {
                        throw new InvalidDataException("TXT string runs past end of RDATA.");
                    }

                    strings.Add(Encoding.UTF8.GetString(_buffer, _position, stringLength));
                    _position += stringLength;
                }

                record = new DnsRecord
                {
                    Name = name, Type = type, RawClass = rawClass, Ttl = ttl,
                    TxtStrings = strings,
                };
                break;
            }

            case DnsRecordType.A when rdLength == 4:
            case DnsRecordType.Aaaa when rdLength == 16:
                record = new DnsRecord
                {
                    Name = name, Type = type, RawClass = rawClass, Ttl = ttl,
                    Address = new System.Net.IPAddress(ReadBytes(rdLength)),
                };
                break;

            default:
                record = new DnsRecord
                {
                    Name = name, Type = type, RawClass = rawClass, Ttl = ttl,
                    RawData = ReadBytes(rdLength),
                };
                break;
        }

        // Trust the declared RDLENGTH over our own consumption, so that one
        // record we decode imperfectly cannot desynchronise the whole message.
        _position = rdataEnd;
        return record;
    }
}

/// <summary>Builds DNS query messages. Emits uncompressed names only.</summary>
public sealed class DnsQueryBuilder
{
    private readonly List<byte> _bytes = new();
    private readonly List<(DnsName Name, DnsRecordType Type, bool UnicastResponse)> _questions = new();
    private readonly ushort _id;

    public DnsQueryBuilder(ushort id) => _id = id;

    public DnsQueryBuilder AddQuestion(DnsName name, DnsRecordType type, bool requestUnicastResponse)
    {
        _questions.Add((name, type, requestUnicastResponse));
        return this;
    }

    public byte[] Build()
    {
        _bytes.Clear();

        // Header. Flags = 0x0000: standard query, not truncated, recursion not desired.
        WriteUInt16(_id);
        WriteUInt16(0x0000);
        WriteUInt16((ushort)_questions.Count);
        WriteUInt16(0); // ANCOUNT
        WriteUInt16(0); // NSCOUNT
        WriteUInt16(0); // ARCOUNT

        foreach ((DnsName name, DnsRecordType type, bool unicast) in _questions)
        {
            WriteName(name);
            WriteUInt16((ushort)type);

            // QCLASS IN (1), with the mDNS unicast-response bit (0x8000) when
            // requested. See RFC 6762 s5.4.
            WriteUInt16((ushort)(unicast ? 0x8001 : 0x0001));
        }

        return _bytes.ToArray();
    }

    private void WriteUInt16(ushort value)
    {
        _bytes.Add((byte)(value >> 8));
        _bytes.Add((byte)(value & 0xFF));
    }

    private void WriteName(DnsName name)
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

            _bytes.Add((byte)encoded.Length);
            _bytes.AddRange(encoded);
        }

        _bytes.Add(0); // root label
    }
}

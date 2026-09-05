// -----------------------------------------------------------------------------
// Report.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Formats what the probe received. This file makes no network calls and
//   makes no decisions; it only presents.
//
// Presentation principle:
//   Nothing observed is hidden. Records that do not fit the tidy grouping are
//   still printed, under "Other records", so the report can never be more
//   flattering than reality.
// -----------------------------------------------------------------------------

using System.Net;
using System.Text;
using SecretPrinter.Dns;

namespace SecretPrinter.Probe;

internal static class Report
{
    public static void Print(IReadOnlyList<Reply> replies, IReadOnlyList<string> queriedTypes, bool showRaw)
    {
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("RESULTS");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();

        if (replies.Count == 0)
        {
            PrintNoReplyGuidance(queriedTypes);
            return;
        }

        // Flatten every record from every reply, remembering who sent it.
        //
        // Records are de-duplicated by CONTENT, not by object identity. Separate
        // query rounds routinely return the same record, and identity-based
        // comparison would let those duplicates fall through the grouping logic
        // and pile up in the "Other records" section, which is misleading.
        var records = new List<(IPAddress Source, DnsRecord Record)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Reply reply in replies)
        {
            foreach (DnsRecord record in reply.Message.AllRecords)
            {
                if (seen.Add(Signature(reply.Source.Address, record)))
                {
                    records.Add((reply.Source.Address, record));
                }
            }
        }

        // Index the SRV, TXT and A records by the name they describe.
        var srvByName = new Dictionary<string, DnsRecord>(StringComparer.OrdinalIgnoreCase);
        var txtByName = new Dictionary<string, DnsRecord>(StringComparer.OrdinalIgnoreCase);
        var addressesByHost = new Dictionary<string, List<IPAddress>>(StringComparer.OrdinalIgnoreCase);
        var consumed = new HashSet<DnsRecord>();

        foreach ((_, DnsRecord record) in records)
        {
            string key = record.Name.ToString();
            switch (record.Type)
            {
                case DnsRecordType.Srv:
                    srvByName.TryAdd(key, record);
                    break;
                case DnsRecordType.Txt:
                    txtByName.TryAdd(key, record);
                    break;
                case DnsRecordType.A or DnsRecordType.Aaaa when record.Address is { } address:
                    if (!addressesByHost.TryGetValue(key, out List<IPAddress>? list))
                    {
                        list = [];
                        addressesByHost[key] = list;
                    }

                    if (!list.Contains(address))
                    {
                        list.Add(address);
                    }

                    break;
            }
        }

        // Group PTR records by the service type they answer for.
        var byServiceType = new SortedDictionary<string, List<DnsRecord>>(StringComparer.OrdinalIgnoreCase);
        foreach ((_, DnsRecord record) in records)
        {
            if (record.Type != DnsRecordType.Ptr || record.PtrTarget is null)
            {
                continue;
            }

            string serviceType = record.Name.ToString();
            if (!byServiceType.TryGetValue(serviceType, out List<DnsRecord>? list))
            {
                list = [];
                byServiceType[serviceType] = list;
            }

            if (!list.Any(existing => existing.PtrTarget!.Equals(record.PtrTarget)))
            {
                list.Add(record);
            }

            consumed.Add(record);
        }

        if (byServiceType.Count == 0)
        {
            Console.WriteLine("Replies arrived, but none contained a PTR record naming a service instance.");
            Console.WriteLine();
        }

        foreach ((string serviceType, List<DnsRecord> ptrRecords) in byServiceType)
        {
            Console.WriteLine($"SERVICE TYPE  {serviceType}");

            foreach (DnsRecord ptr in ptrRecords)
            {
                string instance = ptr.PtrTarget!.ToString();
                Console.WriteLine($"  INSTANCE    {instance}");
                Console.WriteLine($"    PTR ttl   {ptr.Ttl}s");

                if (srvByName.TryGetValue(instance, out DnsRecord? srv) && srv.SrvTarget is { } srvTarget)
                {
                    consumed.Add(srv);
                    Console.WriteLine($"    SRV       host={srvTarget} port={srv.SrvPort} " +
                                      $"priority={srv.SrvPriority} weight={srv.SrvWeight} ttl={srv.Ttl}s");

                    string hostKey = srvTarget.ToString();
                    if (addressesByHost.TryGetValue(hostKey, out List<IPAddress>? addresses))
                    {
                        foreach (IPAddress address in addresses)
                        {
                            Console.WriteLine($"    ADDRESS   {srvTarget} -> {address}");
                        }

                        foreach ((_, DnsRecord record) in records)
                        {
                            if (record.Type is DnsRecordType.A or DnsRecordType.Aaaa
                                && record.Name.ToString().Equals(hostKey, StringComparison.OrdinalIgnoreCase))
                            {
                                consumed.Add(record);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    ADDRESS   {srvTarget} -> (not resolved in this run)");
                    }
                }
                else
                {
                    Console.WriteLine("    SRV       (not received)");
                }

                if (txtByName.TryGetValue(instance, out DnsRecord? txt) && txt.TxtStrings is { } strings)
                {
                    consumed.Add(txt);
                    Console.WriteLine($"    TXT       {strings.Count} entr(ies), ttl={txt.Ttl}s");
                    foreach (string entry in strings)
                    {
                        Console.WriteLine($"                {entry}");
                    }
                }
                else
                {
                    Console.WriteLine("    TXT       (not received)");
                }

                Console.WriteLine();
            }
        }

        // Anything not shown above, so the report cannot quietly omit findings.
        var leftovers = records.Where(entry => !consumed.Contains(entry.Record)).ToList();
        if (leftovers.Count > 0)
        {
            Console.WriteLine($"OTHER RECORDS ({leftovers.Count}) - received but not part of a service grouping:");
            foreach ((IPAddress source, DnsRecord record) in leftovers)
            {
                string detail = record.Type switch
                {
                    DnsRecordType.A or DnsRecordType.Aaaa => record.Address?.ToString() ?? "(no address)",
                    DnsRecordType.Ptr => record.PtrTarget?.ToString() ?? "(no target)",
                    DnsRecordType.Txt => $"{record.TxtStrings?.Count ?? 0} string(s)",
                    DnsRecordType.Srv => $"{record.SrvTarget} port {record.SrvPort}",
                    _ => $"{record.RawData?.Length ?? 0} raw byte(s)",
                };

                Console.WriteLine($"  from {source,-15} {record.Type,-5} {record.Name}  {detail}");
            }

            Console.WriteLine();
        }

        if (showRaw)
        {
            Console.WriteLine(new string('-', 72));
            Console.WriteLine("RAW REPLIES");
            Console.WriteLine(new string('-', 72));
            for (int i = 0; i < replies.Count; i++)
            {
                Console.WriteLine($"Reply {i + 1} from {replies[i].Source} ({replies[i].RawBytes.Length} bytes):");
                Console.WriteLine(HexDump(replies[i].RawBytes));
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Summary: {replies.Count} repl(ies), {records.Count} record(s), " +
                          $"{byServiceType.Count} service type(s) seen.");
    }

    /// <summary>
    /// A stable content key for a record, used to recognise the same record
    /// arriving in more than one reply. TTL is deliberately excluded: it counts
    /// down between rounds, and two records differing only in TTL are the same
    /// record.
    /// </summary>
    private static string Signature(IPAddress source, DnsRecord record)
    {
        string payload = record.Type switch
        {
            DnsRecordType.A or DnsRecordType.Aaaa => record.Address?.ToString() ?? string.Empty,
            DnsRecordType.Ptr => record.PtrTarget?.ToString() ?? string.Empty,
            DnsRecordType.Srv => $"{record.SrvTarget}:{record.SrvPort}:{record.SrvPriority}:{record.SrvWeight}",
            DnsRecordType.Txt => string.Join('\u001f', record.TxtStrings ?? []),
            _ => Convert.ToHexString(record.RawData ?? []),
        };

        return $"{source}|{record.Name}|{record.Type}|{payload}";
    }

    private static void PrintNoReplyGuidance(IReadOnlyList<string> queriedTypes)
    {
        Console.WriteLine("No replies were received.");
        Console.WriteLine();
        Console.WriteLine("This is a result, not necessarily a failure. Things worth checking, in order:");
        Console.WriteLine("  1. Is the --interface address on the same network as the device you expect?");
        Console.WriteLine("  2. Does the Windows Firewall permit inbound UDP replies for this program?");
        Console.WriteLine("     This tool does not create firewall rules; that is left to you deliberately.");
        Console.WriteLine("  3. Is the device powered on and finished with its own network startup?");
        Console.WriteLine("  4. Does the device advertise one of the types queried in this run?");
        foreach (string type in queriedTypes)
        {
            Console.WriteLine($"       {type}");
        }

        Console.WriteLine("     If not, name the right one with --service.");
    }

    private static string HexDump(byte[] data)
    {
        var builder = new StringBuilder();
        for (int offset = 0; offset < data.Length; offset += 16)
        {
            int count = Math.Min(16, data.Length - offset);
            builder.Append(CultureInfoInvariant(offset));

            for (int i = 0; i < 16; i++)
            {
                builder.Append(i < count ? $"{data[offset + i]:x2} " : "   ");
            }

            builder.Append(' ');
            for (int i = 0; i < count; i++)
            {
                byte b = data[offset + i];
                builder.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string CultureInfoInvariant(int offset) =>
        offset.ToString("x4", System.Globalization.CultureInfo.InvariantCulture) + "  ";
}

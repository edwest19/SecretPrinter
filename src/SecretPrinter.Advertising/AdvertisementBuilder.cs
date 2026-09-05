// -----------------------------------------------------------------------------
// AdvertisementBuilder.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Decides exactly what the proxy publishes, and produces the DNS records for
//   it. This is where Section 4 of the specification becomes code.
//
//   There are no sockets in this file, and no dependency on the socket layer.
//   The question "could this ever publish the printer's address?" is answerable
//   by reading this one file, which is the whole reason it is separate.
//
// The rule this file exists to enforce:
//
//     The proxy advertises what the PROXY does,
//     using the format capabilities of the PRINTER.
//
//   Format capabilities describe what will come out on paper, and are copied
//   because they are honest about that. Identity and capability claims belong
//   to whoever can actually deliver them, and are not copied.
//
// Default-deny, not a blacklist:
//   TXT keys are copied only if they appear in an explicit allow-list.
//   Everything else is dropped and reported. A blacklist would mean a future
//   printer firmware advertising some new key - `Fax2=T`, say - would sail
//   through unexamined. Default-deny makes new keys a decision somebody has to
//   make, rather than something that happens quietly.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Spec;

namespace SecretPrinter.Advertising;

/// <summary>A TXT entry that was not copied, with the reason.</summary>
public sealed record DroppedTxtEntry(string Entry, string Reason);

/// <summary>The advertisement: records to publish, plus a full account of decisions taken.</summary>
/// <param name="Records">Records to publish, with normal TTLs.</param>
/// <param name="TxtStrings">The TXT entries actually published, in order.</param>
/// <param name="Dropped">Entries from the printer that were not copied, and why.</param>
/// <param name="Source">Where the printer capabilities came from.</param>
public sealed record Advertisement(
    IReadOnlyList<OutgoingRecord> Records,
    IReadOnlyList<string> TxtStrings,
    IReadOnlyList<DroppedTxtEntry> Dropped,
    CapabilitySource Source);

/// <summary>Turns measured printer capabilities plus a proxy identity into records to publish.</summary>
public static class AdvertisementBuilder
{
    /// <summary>Service type for IPP printers (RFC 8010 / DNS-SD registration).</summary>
    public const string ServiceType = "_ipp._tcp.local";

    /// <summary>
    /// The AirPrint subtype. Measured to be the only name iOS queries for; a
    /// responder omitting it is never discovered. See
    /// docs/findings/2026-09-02-ios-accepts-advertisement.md.
    /// </summary>
    public const string AirPrintSubtype = "_universal._sub._ipp._tcp.local";

    /// <summary>The DNS-SD service-type enumeration name (RFC 6763 §9).</summary>
    public const string ServiceEnumeration = "_services._dns-sd._udp.local";

    // RFC 6762 §10: records that contain a host name (A, SRV) get a short TTL
    // so a moved host is noticed quickly; other records (PTR, TXT) get 75
    // minutes.
    private const uint SharedRecordTtl = 4500;
    private const uint HostRecordTtl = 120;

    /// <summary>
    /// TXT keys copied from the printer, because each describes what will
    /// actually be produced on paper by the device the job reaches.
    /// </summary>
    public static readonly IReadOnlySet<string> CopyableTxtKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "txtvers",    // TXT record version
            "rp",         // resource path, e.g. ipp/print
            "qtotal",     // number of queues
            "priority",   // client preference ordering
            "pdl",        // page description languages accepted
            "URF",        // AirPrint raster capabilities
            "Color",      // colour capability
            "Duplex",     // two-sided capability
            "PaperMax",   // largest paper size
            "kind",       // media kinds
            "Sides",      // sides supported
            "usb_MFG",    // manufacturer, used by clients to pick rendering
            "usb_MDL",    // model, likewise
            "product",    // product string, likewise
        };

    /// <summary>
    /// Keys the specification forbids outright, each with the reason. Kept even
    /// though the allow-list already excludes them, so that a dropped entry is
    /// reported with a specific explanation instead of a generic one.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ForbiddenTxtKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UUID"] = "REQ-ADV-005: identifies the printer. The proxy publishes its own.",
            ["Scan"] = "REQ-ADV-006: the proxy does not relay scanning.",
            ["Fax"] = "REQ-ADV-006: the proxy does not relay faxing.",
            ["mopria-certified"] = "REQ-ADV-007: the printer holds that certification; the proxy does not.",
            ["adminurl"] = "REQ-ADV-008: points at a host unreachable from the client network.",
            ["TLS"] = "REQ-ADV-006: the proxy does not terminate TLS.",
            ["air"] = "REQ-ADV-006: describes an authentication scheme the proxy does not implement.",
            ["printer-state"] = "REQ-ADV-006: live device state the proxy does not track.",
            ["printer-type"] = "REQ-ADV-006: live device state the proxy does not track.",
        };

    /// <summary>Builds the advertisement for one interface.</summary>
    /// <param name="printer">Measured capabilities of the real printer.</param>
    /// <param name="proxy">The proxy's own identity.</param>
    /// <param name="advertisedAddress">
    /// The proxy's own IPv4 address on the interface this advertisement is for.
    /// This is what goes in the A record.
    /// </param>
    [Requirement("REQ-ADV-003",
        "The SRV record names a host owned by the proxy, and the A record binds that host to the proxy's own address on the advertising interface.")]
    [Requirement("REQ-ADV-004",
        "No printer address, hostname or A record is ever emitted; the only address published is the one passed in for the proxy itself.")]
    [Requirement("REQ-ADV-005",
        "Rejects a proxy UUID equal to the printer's, and publishes only the proxy's own.")]
    [Requirement("REQ-ADV-006",
        "Copies TXT keys by allow-list, so capability claims the proxy does not relay cannot be republished.")]
    [Requirement("REQ-ADV-007",
        "mopria-certified is on the forbidden list and is never copied.")]
    [Requirement("REQ-ADV-008",
        "adminurl is never copied; no adminurl is published at all.")]
    [Requirement("REQ-ADV-009",
        "Format capability keys are copied through unchanged, because they describe what will actually print.")]
    [Requirement("REQ-ADV-010",
        "Takes capabilities as an argument carrying their source; no printer TXT value is written literally in this file.")]
    [Requirement("REQ-SEC-002",
        "Publishes exactly three names - the IPP service type, the AirPrint subtype and the service enumeration - plus the proxy's own instance and host. There is no path by which any other service type could be added to an advertisement.")]
    public static Advertisement Build(
        PrinterCapabilities printer, ProxyIdentity proxy, IPAddress advertisedAddress)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(advertisedAddress);

        if (advertisedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                $"{advertisedAddress} is not IPv4. The A record requires an IPv4 address.",
                nameof(advertisedAddress));
        }

        // A proxy sharing the printer's UUID would make two devices claim one
        // identity. Clients cache by UUID, and the result is undefined.
        string? printerUuid = printer.Value("UUID");
        if (printerUuid is not null
            && Guid.TryParse(printerUuid, out Guid parsed)
            && parsed == proxy.Uuid)
        {
            throw new ArgumentException(
                "The proxy UUID is identical to the printer's. Two devices claiming one identity breaks "
                + "clients that cache it (REQ-ADV-005). Generate a distinct UUID for the proxy.",
                nameof(proxy));
        }

        (List<string> txt, List<DroppedTxtEntry> dropped) = BuildTxt(printer, proxy);

        var instance = new DnsName([proxy.InstanceName, "_ipp", "_tcp", "local"]);
        var host = new DnsName([proxy.HostLabel, "local"]);
        DnsName serviceType = DnsName.Parse(ServiceType);
        DnsName subtype = DnsName.Parse(AirPrintSubtype);
        DnsName enumeration = DnsName.Parse(ServiceEnumeration);

        List<OutgoingRecord> records =
        [
            // Shared records: other responders may legitimately answer for these
            // names too, so the cache-flush bit stays clear.
            new(serviceType, DnsRecordType.Ptr, SharedRecordTtl, CacheFlush: false, new PtrPayload(instance)),
            new(subtype, DnsRecordType.Ptr, SharedRecordTtl, CacheFlush: false, new PtrPayload(instance)),
            new(enumeration, DnsRecordType.Ptr, SharedRecordTtl, CacheFlush: false, new PtrPayload(serviceType)),

            // Unique records: the proxy is authoritative for its own instance
            // and host, so receivers should replace rather than accumulate.
            new(instance, DnsRecordType.Srv, HostRecordTtl, CacheFlush: true,
                new SrvPayload(0, 0, proxy.Port, host)),
            new(instance, DnsRecordType.Txt, SharedRecordTtl, CacheFlush: true, new TxtPayload(txt)),
            new(host, DnsRecordType.A, HostRecordTtl, CacheFlush: true, new AddressPayload(advertisedAddress)),
        ];

        return new Advertisement(records, txt, dropped, printer.Source);
    }

    /// <summary>
    /// Produces goodbye records: the same set with TTL zero, so clients drop the
    /// advertisement immediately instead of waiting for it to expire.
    /// </summary>
    [Requirement("REQ-LIF-003",
        "Produces the same record set with TTL 0, which is the goodbye form defined by RFC 6762 s10.1.")]
    public static IReadOnlyList<OutgoingRecord> ToGoodbye(IReadOnlyList<OutgoingRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return [.. records.Select(record => record with { Ttl = 0 })];
    }

    [Requirement("REQ-ADV-016",
        "Publishes the proxy's own display name and a note identifying it as a proxy, rather than the printer's, so the printer list shows what this actually is.")]
    private static (List<string> Published, List<DroppedTxtEntry> Dropped) BuildTxt(
        PrinterCapabilities printer, ProxyIdentity proxy)
    {
        var published = new List<string>();
        var dropped = new List<DroppedTxtEntry>();

        foreach (string entry in printer.TxtStrings)
        {
            int split = entry.IndexOf('=', StringComparison.Ordinal);
            if (split <= 0)
            {
                dropped.Add(new DroppedTxtEntry(entry, "Not a key=value entry."));
                continue;
            }

            string key = entry[..split];

            if (ForbiddenTxtKeys.TryGetValue(key, out string? reason))
            {
                dropped.Add(new DroppedTxtEntry(entry, reason));
                continue;
            }

            if (!CopyableTxtKeys.Contains(key))
            {
                dropped.Add(new DroppedTxtEntry(
                    entry,
                    $"'{key}' is not in the copy allow-list. Keys are copied only when deliberately "
                    + "permitted, so an unrecognised claim is never republished."));
                continue;
            }

            published.Add(entry);
        }

        // The proxy's own identity, added after filtering so it can never be
        // displaced by a printer value of the same name.
        published.Add($"ty={proxy.InstanceName}");
        published.Add(FormatNote(printer.Source));
        published.Add($"UUID={proxy.Uuid.ToString("D", CultureInfo.InvariantCulture)}");

        return (published, dropped);
    }

    /// <summary>
    /// The published note. Says plainly that this is a proxy, so a person
    /// reading printer details is not misled into thinking it is the device
    /// itself (REQ-ADV-016).
    /// </summary>
    private static string FormatNote(CapabilitySource source) =>
        $"note=SecretPrinter proxy. Capabilities from: {source.Description}";
}

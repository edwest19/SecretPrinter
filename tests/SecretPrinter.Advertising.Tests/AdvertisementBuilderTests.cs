// -----------------------------------------------------------------------------
// AdvertisementBuilderTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Verifies the honesty rules in Section 4 of the specification.
//
//   The fixture is the ACTUAL TXT record set published by the Epson ET-3760, as
//   measured with SecretPrinter.Probe and recorded in
//   docs/findings/2026-09-01-printer-capabilities.md. Testing against a
//   convenient invention would prove the builder handles inputs that never
//   occur; testing against the real thing proves it handles the one that does.
//
//   These tests need no network and no hardware, so unlike REQ-CFG-006 they run
//   everywhere.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Advertising.Tests;

internal static class AdvertisementBuilderTests
{
    /// <summary>
    /// The real ET-3760 advertisement as measured, with one disclosed change.
    /// Do not tidy this: its value is that it is what a real printer sent.
    /// <para>
    /// The device derives its hostname and the tail of its UUID from its MAC
    /// address. Before this repository was published, the low three bytes of
    /// both were replaced with <c>000000</c> so that one household's hardware
    /// identifier is not published worldwide. Every key, every other value and
    /// the structure of both identifiers are exactly as captured, which is all
    /// these tests depend on. See docs/findings/2026-09-01-printer-capabilities.md
    /// and docs/findings/2026-09-04-pre-publication-audit.md.
    /// </para>
    /// </summary>
    private static readonly string[] EpsonTxtRecords =
    [
        "txtvers=1",
        "ty=EPSON ET-3760 Series",
        "usb_MFG=EPSON",
        "usb_MDL=ET-3760 Series",
        "product=(EPSON ET-3760 Series)",
        "pdl=application/octet-stream,image/pwg-raster,image/urf,image/jpeg",
        "rp=ipp/print",
        "qtotal=1",
        "Color=T",
        "Duplex=T",
        "Scan=T",
        "Fax=F",
        "kind=document,envelope,photo",
        "PaperMax=legal-A4",
        "URF=CP1,PQ4-5,OB9,OFU0,RS300,SRGB24,W8,DM3,IS1,V1.4,MT1-3-6-8-10-11-12",
        "mopria-certified=1.3",
        "priority=30",
        "adminurl=http://EPSON000000.local.:80/PRESENTATION/BONJOUR",
        "note=",
        "UUID=cfe92100-67c4-11d4-a45f-f8d027000000",
        "TLS=1.2",
    ];

    private static readonly IPAddress ProxyAddress = IPAddress.Parse("192.168.1.234");
    private static readonly Guid ProxyUuid = Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94");

    private static PrinterCapabilities Epson() =>
        new(EpsonTxtRecords, 631, CapabilitySource.ForTest("ET-3760 records from probe run 2026-09-01"));

    private static ProxyIdentity Proxy() =>
        new("SecretPrinter (ET-3760)", "secretprinter", ProxyUuid, 631);

    private static Advertisement Built() => AdvertisementBuilder.Build(Epson(), Proxy(), ProxyAddress);

    private static string? Value(Advertisement advertisement, string key)
    {
        foreach (string entry in advertisement.TxtStrings)
        {
            int split = entry.IndexOf('=', StringComparison.Ordinal);
            if (split > 0 && entry.AsSpan(0, split).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return entry[(split + 1)..];
            }
        }

        return null;
    }

    // ---- Provenance ---------------------------------------------------------

    [TestCase("Capabilities cannot be built without a stated source")]
    [Requirement("REQ-ADV-010")]
    public static void Capabilities_require_a_source()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new PrinterCapabilities(["txtvers=1"], 631, new CapabilitySource("   ", DateTimeOffset.UtcNow)),
            "an advertisement must trace to an observation, not to a literal in source");
    }

    [TestCase("The advertisement carries the source of its capabilities")]
    [Requirement("REQ-ADV-010")]
    public static void Advertisement_reports_its_source()
    {
        Advertisement advertisement = Built();

        Assert.True(advertisement.Source.Description.Length > 0,
            "the source must survive into the result so it can be logged");
        Assert.True(Value(advertisement, "note")!.Contains("Capabilities from", StringComparison.Ordinal),
            "the published note should say where the capabilities came from");
    }

    // ---- What must NOT be published -----------------------------------------

    [TestCase("The printer's UUID is never published")]
    [Requirement("REQ-ADV-005")]
    public static void Printer_uuid_is_not_published()
    {
        Advertisement advertisement = Built();

        Assert.Equal(ProxyUuid.ToString("D"), Value(advertisement, "UUID"),
            "the published UUID must be the proxy's own");

        foreach (string entry in advertisement.TxtStrings)
        {
            Assert.False(entry.Contains("cfe92100-67c4-11d4-a45f-f8d027000000", StringComparison.OrdinalIgnoreCase),
                $"the printer's UUID must appear nowhere in the advertisement, but was in: {entry}");
        }
    }

    [TestCase("A proxy UUID identical to the printer's is rejected")]
    [Requirement("REQ-ADV-005")]
    public static void Colliding_uuid_is_rejected()
    {
        var colliding = new ProxyIdentity(
            "SecretPrinter", "secretprinter", Guid.Parse("cfe92100-67c4-11d4-a45f-f8d027000000"), 631);

        Assert.Throws<ArgumentException>(
            () => AdvertisementBuilder.Build(Epson(), colliding, ProxyAddress),
            "two devices claiming one identity breaks clients that cache by UUID");
    }

    [TestCase("Scan and Fax are not republished")]
    [Requirement("REQ-ADV-006")]
    public static void Unrelayed_capabilities_are_not_published()
    {
        Advertisement advertisement = Built();

        Assert.Null(Value(advertisement, "Scan"),
            "the printer scans; the proxy does not relay scanning");
        Assert.Null(Value(advertisement, "Fax"),
            "faxing is likewise not relayed");
        Assert.Null(Value(advertisement, "TLS"),
            "the proxy does not terminate TLS, so it must not claim a TLS version");
    }

    [TestCase("The mopria certification claim is not republished")]
    [Requirement("REQ-ADV-007")]
    public static void Certification_claim_is_not_published()
    {
        Assert.Null(Value(Built(), "mopria-certified"),
            "the printer holds that certification; the proxy is not the certified device");
    }

    [TestCase("The printer's adminurl is not republished")]
    [Requirement("REQ-ADV-008")]
    public static void Adminurl_is_not_published()
    {
        Advertisement advertisement = Built();

        Assert.Null(Value(advertisement, "adminurl"),
            "the printer's adminurl names a host unreachable from the client network");

        foreach (string entry in advertisement.TxtStrings)
        {
            Assert.False(entry.Contains("EPSON000000", StringComparison.OrdinalIgnoreCase),
                $"the printer's hostname must not leak into any entry, but was in: {entry}");
        }
    }

    [TestCase("No printer address or hostname appears in any record")]
    [Requirement("REQ-ADV-004")]
    public static void Printer_address_is_never_published()
    {
        Advertisement advertisement = Built();

        foreach (OutgoingRecord record in advertisement.Records)
        {
            if (record.Payload is AddressPayload address)
            {
                Assert.Equal(ProxyAddress, address.Address,
                    "the only address published must be the proxy's own");
            }

            if (record.Payload is SrvPayload srv)
            {
                Assert.True(srv.Target.ToString().StartsWith("secretprinter.", StringComparison.Ordinal),
                    $"the SRV target must be a host the proxy owns, but was {srv.Target}");
            }
        }
    }

    [TestCase("An unrecognised TXT key is dropped rather than passed through")]
    [Requirement("REQ-ADV-006")]
    public static void Unknown_keys_are_dropped()
    {
        var withNovelKey = new PrinterCapabilities(
            [.. EpsonTxtRecords, "Fax2=T", "SomeFutureClaim=yes"],
            631,
            CapabilitySource.ForTest("ET-3760 plus hypothetical future keys"));

        Advertisement advertisement = AdvertisementBuilder.Build(withNovelKey, Proxy(), ProxyAddress);

        Assert.Null(Value(advertisement, "Fax2"),
            "default-deny means a key nobody has considered is not republished");
        Assert.Null(Value(advertisement, "SomeFutureClaim"),
            "a blacklist would have let this through; an allow-list does not");
    }

    // ---- What must be published ---------------------------------------------

    [TestCase("Format capabilities are copied through unchanged")]
    [Requirement("REQ-ADV-009")]
    public static void Format_capabilities_are_copied()
    {
        Advertisement advertisement = Built();

        Assert.Equal(
            "application/octet-stream,image/pwg-raster,image/urf,image/jpeg",
            Value(advertisement, "pdl"),
            "pdl describes what will actually print and must be copied verbatim");
        Assert.Equal(
            "CP1,PQ4-5,OB9,OFU0,RS300,SRGB24,W8,DM3,IS1,V1.4,MT1-3-6-8-10-11-12",
            Value(advertisement, "URF"),
            "URF drives iOS rasterisation and must not be altered");
        Assert.Equal("T", Value(advertisement, "Color"), "colour capability is a format fact");
        Assert.Equal("T", Value(advertisement, "Duplex"), "duplex capability is a format fact");
        Assert.Equal("legal-A4", Value(advertisement, "PaperMax"), "paper size is a format fact");
        Assert.Equal("ipp/print", Value(advertisement, "rp"), "the resource path must match the printer's");
    }

    [TestCase("The SRV record points at the proxy's host and port")]
    [Requirement("REQ-ADV-003")]
    public static void Srv_names_the_proxy()
    {
        Advertisement advertisement = Built();

        OutgoingRecord srv = advertisement.Records.Single(r => r.Type == DnsRecordType.Srv);
        var payload = (SrvPayload)srv.Payload;

        Assert.Equal("secretprinter.local", payload.Target.ToString(), "SRV must name a host the proxy owns");
        Assert.Equal((ushort)631, payload.Port, "SRV must name the port the proxy listens on");

        OutgoingRecord a = advertisement.Records.Single(r => r.Type == DnsRecordType.A);
        Assert.Equal("secretprinter.local", a.Name.ToString(), "the A record must be for the proxy's host");
        Assert.Equal(ProxyAddress, ((AddressPayload)a.Payload).Address,
            "the A record must carry the proxy's address on the advertising interface");
    }

    [TestCase("The published display name is the proxy's, not the printer's")]
    [Requirement("REQ-ADV-016")]
    public static void Display_name_is_the_proxy()
    {
        Advertisement advertisement = Built();

        Assert.Equal("SecretPrinter (ET-3760)", Value(advertisement, "ty"),
            "a person reading the printer list should see that this is the proxy");
        Assert.True(Value(advertisement, "note")!.Contains("proxy", StringComparison.OrdinalIgnoreCase),
            "the note should say plainly that this is a proxy");
    }

    // ---- Reporting ----------------------------------------------------------

    // Deliberately unmarked. REQ-OBS-002 is about the SERVICE logging every
    // advertisement it publishes. This only proves the builder hands back the
    // material to log; the logging itself belongs to the service host. Marking
    // it here would overstate what is verified.
    [TestCase("Every dropped entry is reported with a reason")]
    public static void Dropped_entries_are_reported()
    {
        Advertisement advertisement = Built();

        Assert.True(advertisement.Dropped.Count > 0, "the Epson set contains entries that must be dropped");

        foreach (DroppedTxtEntry entry in advertisement.Dropped)
        {
            Assert.True(entry.Reason.Length > 0, $"'{entry.Entry}' was dropped with no reason given");
        }

        Assert.True(advertisement.Dropped.Any(d => d.Entry.StartsWith("Scan=", StringComparison.Ordinal)),
            "Scan must appear in the dropped list, not vanish silently");
        Assert.True(
            advertisement.Dropped.Any(d => d.Reason.Contains("REQ-ADV-005", StringComparison.Ordinal)),
            "the UUID drop should cite the requirement that forbids it");
    }

    // ---- Only printing is ever published ------------------------------------

    [TestCase("Only printing service types appear in the advertisement")]
    [Requirement("REQ-SEC-002")]
    public static void Only_printing_services_are_published()
    {
        Advertisement advertisement = Built();

        var permitted = new HashSet<string>(StringComparer.Ordinal)
        {
            "_ipp._tcp.local",
            "_universal._sub._ipp._tcp.local",
            "_services._dns-sd._udp.local",
            "SecretPrinter (ET-3760)._ipp._tcp.local",
            "secretprinter.local",
        };

        foreach (OutgoingRecord record in advertisement.Records)
        {
            Assert.True(permitted.Contains(record.Name.ToString()),
                $"{record.Name} is published but is not a printing name or the proxy's own; "
                + "nothing else may appear on the client network");
        }

        // The service enumeration must name only the printing type, or a client
        // browsing all services would learn of something else.
        OutgoingRecord enumeration = advertisement.Records.Single(
            r => r.Name.ToString() == "_services._dns-sd._udp.local");

        Assert.Equal("_ipp._tcp.local", ((PtrPayload)enumeration.Payload).Target.ToString(),
            "service enumeration must offer printing and nothing else");
    }

    // ---- Goodbye ------------------------------------------------------------

    [TestCase("Goodbye records are the same set with TTL zero")]
    [Requirement("REQ-LIF-003")]
    public static void Goodbye_zeroes_every_ttl()
    {
        Advertisement advertisement = Built();
        IReadOnlyList<OutgoingRecord> goodbye = AdvertisementBuilder.ToGoodbye(advertisement.Records);

        Assert.Equal(advertisement.Records.Count, goodbye.Count,
            "a goodbye must retract everything that was announced");

        foreach (OutgoingRecord record in goodbye)
        {
            Assert.Equal(0u, record.Ttl, $"{record.Name} {record.Type} must have TTL 0 in a goodbye");
        }

        Assert.True(advertisement.Records.All(r => r.Ttl > 0),
            "building the goodbye must not mutate the original records");
    }

    // ---- Identity validation ------------------------------------------------

    [TestCase("A host label containing a dot is rejected")]
    [Requirement("REQ-ADV-003")]
    public static void Host_label_must_be_a_single_label()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new ProxyIdentity("SecretPrinter", "secretprinter.local", ProxyUuid, 631),
            "the host label is one DNS label; passing a dotted name would produce a malformed record");
    }

    [TestCase("An empty proxy UUID is rejected")]
    [Requirement("REQ-ADV-005")]
    public static void Empty_uuid_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Empty, 631),
            "every installation sharing an empty UUID defeats the point of having one");
    }

    [TestCase("An over-long instance name is rejected")]
    [Requirement("REQ-ADV-003")]
    public static void Overlong_instance_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new ProxyIdentity(new string('x', 64), "secretprinter", ProxyUuid, 631),
            "DNS labels are limited to 63 bytes");
    }
}

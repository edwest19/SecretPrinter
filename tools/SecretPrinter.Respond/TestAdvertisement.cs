// -----------------------------------------------------------------------------
// TestAdvertisement.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Defines exactly what the responder experiment advertises. One fake printer,
//   nothing else.
//
// This is NOT the service's advertisement builder. It is a fixed set of records
// for one experiment, hardcoded on purpose so that what goes on the wire can be
// read straight out of this file with no configuration to trace. The real
// implementation will derive its TXT records from a live query to the printer
// (REQ-ADV-010); this one cannot, because there is no printer behind it.
//
// Honesty rules applied here, which are the same rules Section 4 of the README
// places on the real service:
//   - The UUID is generated for this test, not copied from the Epson
//     (REQ-ADV-005). Two devices claiming one identity is a real failure mode.
//   - Scan and Fax are absent. A proxy that does not relay scanning must not
//     advertise it (REQ-ADV-006), and this test relays nothing at all.
//   - mopria-certified is absent. The printer holds that certification; this
//     does not (REQ-ADV-007).
//   - adminurl is absent. The printer's own points at a host unreachable from
//     the client network (REQ-ADV-008).
//   - The instance name says plainly that this is a test and should not be
//     used. Nothing here impersonates the Epson (REQ-ADV-016).
//   - pdl and URF ARE copied from what the Epson advertises, because they
//     describe formats - and if this test ever did print, those would be the
//     honest values. They are measured, not invented; see
//     docs/findings/2026-09-01-printer-capabilities.md.
//
// Known departure from RFC 6762, stated rather than hidden:
//   A conforming responder probes for name conflicts before claiming a name
//   (s8.1). This experiment does not probe. It runs briefly, under supervision,
//   with a name nothing else will plausibly hold. The real service must probe.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;

namespace SecretPrinter.Respond;

/// <summary>The fixed set of records this experiment advertises.</summary>
internal sealed class TestAdvertisement
{
    /// <summary>
    /// A UUID generated once for this test tool and then frozen, so repeated
    /// runs present a stable identity to clients that cache it. It is NOT the
    /// Epson's UUID, and it is not generated per-run, because a printer whose
    /// identity changes every minute confuses clients in ways that would muddy
    /// the experiment.
    /// </summary>
    private const string TestUuid = "b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94";

    private const uint RecordTtl = 120;
    private const uint HostTtl = 120;

    public TestAdvertisement(string instanceName, IPAddress address, ushort port, string hostLabel)
    {
        InstanceLabel = instanceName;
        Address = address;
        Port = port;

        ServiceType = DnsName.Parse("_ipp._tcp.local");
        AirPrintSubtype = DnsName.Parse("_universal._sub._ipp._tcp.local");
        ServiceEnumeration = DnsName.Parse("_services._dns-sd._udp.local");

        // The instance name is a single DNS label that may contain spaces and
        // dots (RFC 6763 s4.1.1), so it is placed as a label directly rather
        // than parsed from a dotted string.
        Instance = new DnsName([instanceName, "_ipp", "_tcp", "local"]);
        Host = new DnsName([hostLabel, "local"]);
    }

    public string InstanceLabel { get; }
    public IPAddress Address { get; }
    public ushort Port { get; }

    public DnsName ServiceType { get; }
    public DnsName AirPrintSubtype { get; }
    public DnsName ServiceEnumeration { get; }
    public DnsName Instance { get; }
    public DnsName Host { get; }

    /// <summary>
    /// TXT records for the advertised instance.
    /// </summary>
    /// <remarks>
    /// Format capabilities are the values the Epson ET-3760 actually
    /// advertises, read with SecretPrinter.Probe. Identity and capability
    /// claims are this tool's own, or absent.
    /// </remarks>
    public IReadOnlyList<string> TxtStrings =>
    [
        "txtvers=1",
        $"ty={InstanceLabel}",
        "note=SecretPrinter discovery experiment. Not a real printer.",
        "rp=ipp/print",
        "qtotal=1",
        "priority=30",

        // Measured from the printer. Honest about what would print, if anything
        // were listening behind this - which, in this experiment, nothing is.
        "pdl=application/octet-stream,image/pwg-raster,image/urf,image/jpeg",
        "URF=CP1,PQ4-5,OB9,OFU0,RS300,SRGB24,W8,DM3,IS1,V1.4,MT1-3-6-8-10-11-12",
        "Color=T",
        "Duplex=T",
        "kind=document",
        "PaperMax=legal-A4",

        // This tool's own identity, never the printer's.
        $"UUID={TestUuid}",
    ];

    /// <summary>
    /// Every record this responder owns, with the supplied TTL. A TTL of zero
    /// produces the goodbye set (RFC 6762 s10.1).
    /// </summary>
    public IReadOnlyList<OutgoingRecord> AllRecords(uint ttl) =>
    [
        // Shared records: several responders may legitimately answer for a
        // service type, so the cache-flush bit stays clear.
        new(ServiceType, DnsRecordType.Ptr, ttl, CacheFlush: false, new PtrPayload(Instance)),
        new(AirPrintSubtype, DnsRecordType.Ptr, ttl, CacheFlush: false, new PtrPayload(Instance)),
        new(ServiceEnumeration, DnsRecordType.Ptr, ttl, CacheFlush: false, new PtrPayload(ServiceType)),

        // Unique records: this responder is authoritative for its own instance
        // and host, so receivers should replace rather than accumulate.
        new(Instance, DnsRecordType.Srv, ttl, CacheFlush: true, new SrvPayload(0, 0, Port, Host)),
        new(Instance, DnsRecordType.Txt, ttl, CacheFlush: true, new TxtPayload(TxtStrings)),
        new(Host, DnsRecordType.A, ttl == 0 ? 0 : HostTtl, CacheFlush: true, new AddressPayload(Address)),
    ];

    public IReadOnlyList<OutgoingRecord> AnnouncementRecords() => AllRecords(RecordTtl);

    /// <summary>Goodbye records: identical, with TTL zero, so caches drop us immediately.</summary>
    public IReadOnlyList<OutgoingRecord> GoodbyeRecords() => AllRecords(0);

    /// <summary>
    /// Answers a single question, or returns empty when the question is about
    /// something this responder does not own.
    /// </summary>
    /// <remarks>
    /// Answering only for owned names is what keeps this from being a general
    /// mDNS responder. Everything else on the network is ignored.
    /// </remarks>
    public (IReadOnlyList<OutgoingRecord> Answers, IReadOnlyList<OutgoingRecord> Additionals) Answer(
        DnsName question, DnsRecordType type)
    {
        List<OutgoingRecord> all = [.. AnnouncementRecords()];

        OutgoingRecord ptrService = all[0];
        OutgoingRecord ptrSubtype = all[1];
        OutgoingRecord ptrEnumeration = all[2];
        OutgoingRecord srv = all[3];
        OutgoingRecord txt = all[4];
        OutgoingRecord a = all[5];

        bool Wants(DnsRecordType wanted) => type == wanted || type == DnsRecordType.Any;

        if (question.Equals(ServiceType) && Wants(DnsRecordType.Ptr))
        {
            return ([ptrService], [srv, txt, a]);
        }

        if (question.Equals(AirPrintSubtype) && Wants(DnsRecordType.Ptr))
        {
            return ([ptrSubtype], [srv, txt, a]);
        }

        if (question.Equals(ServiceEnumeration) && Wants(DnsRecordType.Ptr))
        {
            return ([ptrEnumeration], []);
        }

        if (question.Equals(Instance))
        {
            List<OutgoingRecord> answers = [];
            if (Wants(DnsRecordType.Srv))
            {
                answers.Add(srv);
            }

            if (Wants(DnsRecordType.Txt))
            {
                answers.Add(txt);
            }

            return answers.Count > 0 ? (answers, [a]) : ([], []);
        }

        if (question.Equals(Host) && Wants(DnsRecordType.A))
        {
            return ([a], []);
        }

        return ([], []);
    }
}

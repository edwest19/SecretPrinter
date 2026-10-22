// -----------------------------------------------------------------------------
// PrinterAnnouncementsTests.cs  (SecretPrinter.Resolution.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-10-22, for REQ-RES-011.
// Reviewed by a human before merge.
//
// Purpose:
//   Verifies that the printer is heard when it speaks without being asked, and
//   only then: a response on the printer-side interface, sent to the mDNS
//   group from port 5353, carrying the configured instance's SRV record and an
//   address record for the host it names, each within its TTL, in one
//   datagram or several. And that everything else is ignored: unicast, another
//   port, another interface, a question, another class, another instance or
//   host, a goodbye, a record whose TTL has run out.
//
//   The order of records in Announced_in_three_packets_is_heard is the order
//   measured on 2026-10-21: each of the printer's announcements came as three
//   datagrams, the first beginning with its address record and the SRV in a
//   later one (docs/findings/2026-10-21-the-printer-announces-itself-when-it-starts.md).
//
//   Every test runs against a fake clock and datagrams built here; none needs
//   a network or waits.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Resolution.Tests;

internal static class PrinterAnnouncementsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 22, 1, 39, TimeSpan.Zero);

    private static readonly MdnsInterface PrinterNic =
        new("Wi-Fi", IPAddress.Parse("192.168.12.245"), 11, AddressFamily.InterNetwork);

    private static readonly MdnsInterface ClientNic =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetwork);

    private static readonly DnsName Instance = DnsName.Parse("EPSON ET-3760 Series._ipps._tcp.local");

    private static readonly DnsName IppInstance = DnsName.Parse("EPSON ET-3760 Series._ipp._tcp.local");

    /// <summary>
    /// The printer's host name, its low three bytes redacted to 000000 as in
    /// docs/findings/2026-09-04-pre-publication-audit.md.
    /// </summary>
    private static readonly DnsName Host = DnsName.Parse("EPSON000000.local");

    private static readonly IPAddress PrinterAddress = IPAddress.Parse("192.168.12.180");

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static OutgoingRecord Srv(uint ttl = 120, DnsName? instance = null, DnsName? target = null) =>
        new(instance ?? Instance, DnsRecordType.Srv, ttl, true, new SrvPayload(0, 0, 631, target ?? Host));

    private static OutgoingRecord Txt(uint ttl = 4500) =>
        new(Instance, DnsRecordType.Txt, ttl, true, new TxtPayload(["txtvers=1", "rp=ipp/print"]));

    private static OutgoingRecord Address(uint ttl = 120, DnsName? host = null, string? address = null) =>
        new(host ?? Host, DnsRecordType.A, ttl, true, new AddressPayload(address is null ? PrinterAddress : IPAddress.Parse(address)));

    private static byte[] Response(params OutgoingRecord[] answers)
    {
        var builder = new DnsResponseBuilder();
        foreach (OutgoingRecord answer in answers)
        {
            builder.AddAnswer(answer);
        }

        return builder.Build();
    }

    /// <summary>
    /// A datagram as the resolver's socket delivers it: by default from the
    /// printer, from port 5353, sent to 224.0.0.251, arriving on the
    /// printer-side interface.
    /// </summary>
    private static MdnsDatagram Datagram(
        byte[] payload,
        IPAddress? destination = null,
        int sourcePort = 5353,
        MdnsInterface? arrivedOn = null,
        bool destinationUnknown = false)
    {
        MdnsInterface on = arrivedOn ?? PrinterNic;
        return new MdnsDatagram(
            payload,
            new IPEndPoint(PrinterAddress, sourcePort),
            on.Index,
            on,
            destinationUnknown ? null : destination ?? MdnsSocket.MulticastGroup);
    }

    private static (PrinterAnnouncements Announcements, List<ResolvedPrinter> Heard, TestClock Clock) Listening()
    {
        var heard = new List<ResolvedPrinter>();
        var clock = new TestClock(Start);
        return (new PrinterAnnouncements(Instance, heard.Add, clock), heard, clock);
    }

    // ---- Heard ---------------------------------------------------------------

    [TestCase("The printer is heard from one response holding its instance's SRV and its host's address")]
    [Requirement("REQ-RES-011")]
    public static void Heard_from_one_response()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Txt(), Address())), PrinterNic);

        Assert.Equal(1, heard.Count, "a response holding the SRV and the address it names is enough to reach the printer");
        Assert.Equal(Instance, heard[0].Instance, "the instance listened for");
        Assert.Equal(Host, heard[0].Host, "the host comes from the SRV target");
        Assert.Equal(PrinterAddress, heard[0].Address, "the address comes from the address record for that host");
        Assert.Equal((ushort)631, heard[0].Port, "the port comes from the SRV record");
        Assert.Equal(Start, heard[0].ResolvedAt, "heard now");
        Assert.Equal(120u, heard[0].Ttl, "the shorter of the two records' lifetimes");
        Assert.Equal(2, heard[0].TxtStrings.Count, "the TXT strings held are carried");
    }

    [TestCase("An announcement in three packets, the address first and the SRV later, is heard when the SRV arrives")]
    [Requirement("REQ-RES-011")]
    public static void Announced_in_three_packets_is_heard()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        // As measured on 2026-10-21: the first packet begins with the address
        // record; another instance's SRV and TXT come in the second; this
        // instance's SRV in the third.
        announcements.Hear(Datagram(Response(Address())), PrinterNic);
        Assert.Equal(0, heard.Count, "an address alone does not say which printer it is for");

        announcements.Hear(Datagram(Response(Srv(instance: IppInstance))), PrinterNic);
        Assert.Equal(0, heard.Count, "another instance's SRV does not complete it");

        announcements.Hear(Datagram(Response(Srv(), Txt())), PrinterNic);
        Assert.Equal(1, heard.Count, "the SRV completes it with the address already held");
        Assert.Equal(PrinterAddress, heard[0].Address, "the address held from the first packet");
    }

    [TestCase("An SRV heard before its address is completed when the address arrives")]
    [Requirement("REQ-RES-011")]
    public static void Srv_first_then_address_is_heard()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv())), PrinterNic);
        Assert.Equal(0, heard.Count, "an SRV without the address it names cannot be reached");

        announcements.Hear(Datagram(Response(Address())), PrinterNic);
        Assert.Equal(1, heard.Count, "the address for the host the SRV names completes it");
    }

    [TestCase("The lifetime reported is the shorter of the two records' lifetimes left, counted from now")]
    [Requirement("REQ-RES-011")]
    public static void Lifetime_is_the_shorter_left()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, TestClock clock) = Listening();

        announcements.Hear(Datagram(Response(Address(ttl: 120))), PrinterNic);
        clock.Advance(TimeSpan.FromSeconds(30));
        announcements.Hear(Datagram(Response(Srv(ttl: 4500))), PrinterNic);

        Assert.Equal(1, heard.Count, "both records are within their TTL");
        Assert.Equal(90u, heard[0].Ttl, "the address heard 30 s ago with a TTL of 120 has 90 s left, less than the SRV");
        Assert.Equal(Start + TimeSpan.FromSeconds(30), heard[0].ResolvedAt, "counted from now, not from when the address was heard");
    }

    [TestCase("Each response that brings the records again reports the printer again; the caller decides what that means")]
    [Requirement("REQ-RES-011")]
    public static void Repeated_announcements_are_each_reported()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address())), PrinterNic);
        announcements.Hear(Datagram(Response(Srv(), Address())), PrinterNic);

        Assert.Equal(2, heard.Count, "the printer announces itself up to eight times; each is reported");
    }

    // ---- Ignored -------------------------------------------------------------

    [TestCase("A response sent by unicast is ignored, as RFC 6762 s6 says of unsolicited unicast responses")]
    [Requirement("REQ-RES-011")]
    public static void Unicast_response_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address()), destination: PrinterNic.Address), PrinterNic);

        Assert.Equal(0, heard.Count, "RFC 6762 s6: a querier MUST silently ignore unicast responses it did not ask for");
    }

    [TestCase("A datagram whose destination is not known is ignored")]
    [Requirement("REQ-RES-011")]
    public static void Unknown_destination_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address()), destinationUnknown: true), PrinterNic);

        Assert.Equal(0, heard.Count, "a datagram that cannot be shown to have been sent to the group is not taken");
    }

    [TestCase("A response from a port other than 5353 is ignored, as RFC 6762 s6 says")]
    [Requirement("REQ-RES-011")]
    public static void Other_source_port_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address()), sourcePort: 49152), PrinterNic);

        Assert.Equal(0, heard.Count, "RFC 6762 s6: responses whose source port is not 5353 MUST be silently ignored");
    }

    [TestCase("A response that arrived on another interface, or on none configured, is ignored")]
    [Requirement("REQ-RES-011")]
    [Requirement("REQ-RES-006")]
    public static void Other_interface_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address()), arrivedOn: ClientNic), PrinterNic);
        announcements.Hear(
            new MdnsDatagram(
                Response(Srv(), Address()),
                new IPEndPoint(PrinterAddress, 5353),
                PrinterNic.Index,
                null,
                MdnsSocket.MulticastGroup),
            PrinterNic);

        Assert.Equal(0, heard.Count, "only what arrives on the printer-side interface is about the printer");
    }

    [TestCase("A question carrying the same records, as known answers or as a probe's proposal, is not the printer's word")]
    [Requirement("REQ-RES-011")]
    public static void Question_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        // Another device's question listing the records it already holds, in
        // its Answer Section (RFC 6762 s7.1: "A Multicast DNS querier MUST NOT
        // cache resource records observed in the Known-Answer Section of other
        // Multicast DNS queries."). Built as a response, then given the query
        // bit.
        byte[] knownAnswers = Response(Srv(), Address());
        knownAnswers[2] &= 0x7F;

        // A probe (RFC 6762 s8.2) carries the records it proposes in its
        // Authority Section.
        byte[] probe = new DnsQueryBuilder(0)
            .AddQuestion(Instance, DnsRecordType.Any, requestUnicastResponse: false)
            .AddAuthority(Srv())
            .AddAuthority(Address())
            .Build();

        announcements.Hear(Datagram(knownAnswers), PrinterNic);
        announcements.Hear(Datagram(probe), PrinterNic);

        Assert.Equal(0, heard.Count, "a question says nothing about who holds a name");
        Assert.Equal(0, announcements.HeldAddresses, "and nothing is held from it");
    }

    [TestCase("Records in a response's Authority Section are not taken")]
    [Requirement("REQ-RES-011")]
    public static void Authority_section_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        // The SRV and the address in the Authority Section of a message marked
        // as a response: built as a query, then given the response bit.
        byte[] payload = new DnsQueryBuilder(0)
            .AddQuestion(Instance, DnsRecordType.Any, requestUnicastResponse: false)
            .AddAuthority(Srv())
            .AddAuthority(Address())
            .Build();
        payload[2] |= 0x80;

        announcements.Hear(Datagram(payload), PrinterNic);

        Assert.Equal(0, heard.Count, "only the Answer and Additional sections carry what the printer says it holds");
    }

    [TestCase("A record of a class other than IN is not taken")]
    [Requirement("REQ-RES-011")]
    public static void Other_class_is_ignored()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        // One address record, its class changed from IN to CH (3) on the wire.
        // Names are written uncompressed, so its class field follows the
        // 12-byte header, the name and the 2-byte type.
        byte[] payload = Response(Address());
        int nameLength = Host.Labels.Sum(label => 1 + label.Length) + 1;
        int classAt = 12 + nameLength + 2;
        payload[classAt] = 0x80;
        payload[classAt + 1] = 0x03;

        announcements.Hear(Datagram(payload), PrinterNic);
        announcements.Hear(Datagram(Response(Srv())), PrinterNic);

        Assert.Equal(0, heard.Count, "an address record of another class is not the printer's address");
        Assert.Equal(0, announcements.HeldAddresses, "and it is not held");
    }

    [TestCase("An address for another host than the SRV names does not complete it")]
    [Requirement("REQ-RES-011")]
    public static void Address_for_another_host_does_not_complete()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(
            Datagram(Response(Srv(), Address(host: DnsName.Parse("other-device.local"), address: "192.168.12.50"))),
            PrinterNic);

        Assert.Equal(0, heard.Count, "the address must be for the host the SRV names");
    }

    [TestCase("Traffic about other devices, after the printer was heard, reports nothing more")]
    [Requirement("REQ-RES-011")]
    public static void Other_traffic_reports_nothing()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(), Address())), PrinterNic);
        announcements.Hear(
            Datagram(Response(Address(host: DnsName.Parse("other-device.local"), address: "192.168.12.50"))),
            PrinterNic);
        announcements.Hear(Datagram(Response(Srv(instance: IppInstance))), PrinterNic);

        Assert.Equal(1, heard.Count, "a datagram that brings neither the SRV nor its host's address reports nothing");
    }

    // ---- Goodbyes and lifetimes ---------------------------------------------

    [TestCase("A record with a TTL of 0 is a goodbye and is never taken")]
    [Requirement("REQ-RES-011")]
    public static void Goodbye_is_never_taken()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv(ttl: 0), Address(ttl: 0))), PrinterNic);

        Assert.Equal(0, heard.Count, "RFC 6762 s10.1: a TTL of 0 says the record is going away");
        Assert.Equal(0, announcements.HeldAddresses, "and nothing is held from it");
    }

    [TestCase("A goodbye forgets the address it names, so a later SRV does not complete it")]
    [Requirement("REQ-RES-011")]
    public static void Goodbye_forgets_the_address()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Address())), PrinterNic);
        announcements.Hear(Datagram(Response(Address(ttl: 0))), PrinterNic);
        announcements.Hear(Datagram(Response(Srv())), PrinterNic);

        Assert.Equal(0, heard.Count, "the address said goodbye before the SRV arrived");
    }

    [TestCase("A goodbye forgets the SRV it names, so a later address does not complete it")]
    [Requirement("REQ-RES-011")]
    public static void Goodbye_forgets_the_srv()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Srv())), PrinterNic);
        announcements.Hear(Datagram(Response(Srv(ttl: 0))), PrinterNic);
        announcements.Hear(Datagram(Response(Address())), PrinterNic);

        Assert.Equal(0, heard.Count, "the SRV said goodbye before the address arrived");
    }

    [TestCase("A goodbye for another address of the host leaves the address held")]
    [Requirement("REQ-RES-011")]
    public static void Goodbye_for_another_address_leaves_the_held_one()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, _) = Listening();

        announcements.Hear(Datagram(Response(Address())), PrinterNic);
        announcements.Hear(Datagram(Response(Address(ttl: 0, address: "192.168.12.99"))), PrinterNic);
        announcements.Hear(Datagram(Response(Srv())), PrinterNic);

        Assert.Equal(1, heard.Count, "RFC 6762 s10.1: a goodbye names the record's data, and this one names another address");
    }

    [TestCase("A record whose TTL has run out is not used")]
    [Requirement("REQ-RES-011")]
    public static void Expired_record_is_not_used()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, TestClock clock) = Listening();

        announcements.Hear(Datagram(Response(Address(ttl: 120))), PrinterNic);
        clock.Advance(TimeSpan.FromSeconds(120));
        announcements.Hear(Datagram(Response(Srv())), PrinterNic);

        Assert.Equal(0, heard.Count, "an address heard 120 s ago with a TTL of 120 is no longer valid");
    }

    [TestCase("A lifetime of less than a second left is not reported")]
    [Requirement("REQ-RES-011")]
    public static void Under_a_second_left_is_not_reported()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, TestClock clock) = Listening();

        announcements.Hear(Datagram(Response(Address(ttl: 120))), PrinterNic);
        clock.Advance(TimeSpan.FromSeconds(119.5));
        announcements.Hear(Datagram(Response(Srv())), PrinterNic);

        Assert.Equal(0, heard.Count, "half a second left rounds down to a lifetime of 0, which is no answer");
    }

    // ---- Bounded -------------------------------------------------------------

    [TestCase("At most 16 addresses are held, and the one heard longest ago is forgotten first")]
    [Requirement("REQ-RES-011")]
    public static void Addresses_held_are_bounded()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, TestClock clock) = Listening();

        announcements.Hear(Datagram(Response(Address())), PrinterNic);

        for (int i = 0; i < 40; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            announcements.Hear(
                Datagram(Response(Address(host: DnsName.Parse($"device-{i}.local"), address: $"192.168.12.{100 + i}"))),
                PrinterNic);
        }

        Assert.Equal(PrinterAnnouncements.MaximumHeldAddresses, announcements.HeldAddresses,
            "a device sending records for many names cannot make what is held grow");

        announcements.Hear(Datagram(Response(Srv())), PrinterNic);
        Assert.Equal(0, heard.Count, "the printer's address, heard longest ago, was forgotten first");
    }

    [TestCase("An address whose TTL has run out is forgotten before one still valid")]
    [Requirement("REQ-RES-011")]
    public static void Expired_addresses_go_first()
    {
        (PrinterAnnouncements announcements, List<ResolvedPrinter> heard, TestClock clock) = Listening();

        // The printer's address first, so it is the one heard longest ago; then
        // fifteen short-lived ones.
        announcements.Hear(Datagram(Response(Address())), PrinterNic);
        clock.Advance(TimeSpan.FromSeconds(1));

        for (int i = 0; i < 15; i++)
        {
            announcements.Hear(
                Datagram(Response(Address(ttl: 5, host: DnsName.Parse($"brief-{i}.local"), address: $"192.168.12.{100 + i}"))),
                PrinterNic);
        }

        // The fifteen run out; two more names arrive. Were the oldest forgotten
        // first, the printer's would go.
        clock.Advance(TimeSpan.FromSeconds(10));
        announcements.Hear(Datagram(Response(Address(host: DnsName.Parse("late-1.local"), address: "192.168.12.201"))), PrinterNic);
        announcements.Hear(Datagram(Response(Address(host: DnsName.Parse("late-2.local"), address: "192.168.12.202"))), PrinterNic);

        announcements.Hear(Datagram(Response(Srv())), PrinterNic);
        Assert.Equal(1, heard.Count, "the expired addresses made room, so the printer's, still valid, was kept");
    }
}

// -----------------------------------------------------------------------------
// ConflictSourceTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-27, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder reads a response for conflicts only when it was
//   sent to the mDNS group, 224.0.0.251 or ff02::fb, and came from UDP port
//   5353 (README REQ-ADV-030). RFC 6762 section 6: "Multicast DNS
//   implementations MUST silently ignore any Multicast DNS responses they
//   receive where the source UDP port is not 5353." And: "A Multicast DNS
//   querier MUST only accept unicast responses if they answer a recently sent
//   query (e.g., sent within the last two seconds) that explicitly requested
//   unicast responses. A Multicast DNS querier MUST silently ignore all other
//   unicast responses." The responder's only questions are its probes, and
//   they ask for multicast answers (MdnsResponder.Probing.cs says why), so no
//   unicast response answers a question of its own that asked for one.
//
//   Each test hands the responder the same response: another device's address
//   for the host name the responder claims, which is a conflict under
//   REQ-ADV-024 when it is read. Only where it came from, and where it was sent
//   to, change. Two of them are controls: the response sent to each group from
//   port 5353 is still a conflict. They passed before the change as well, and
//   are here so that a change that ignored every response, or read only one
//   family's group, would fail.
//
//   Written before the change and run against the code without it: the five
//   tests that a response is ignored failed, and the two controls passed. See
//   docs/findings/2026-10-27-the-conflict-check-takes-only-a-multicast-response-from-port-5353.md.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class ConflictSourceTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>The other device's address, and the address it claims for the responder's host name.</summary>
    private const string Other = "192.168.1.50";

    /// <summary>The other device's link-local address, when it sends over IPv6.</summary>
    private const string OtherV6 = "fe80::50";

    /// <summary>This machine's own IPv6 link-local address, the destination of a unicast over IPv6.</summary>
    private const string OwnV6 = "fe80::1234";

    private sealed class Setup
    {
        public required MdnsResponder Responder { get; init; }
        public required FakeTransport Transport { get; init; }
        public required Advertisement Advertisement { get; init; }
        public List<NameConflict> Reports { get; } = [];

        public DnsName Host => Advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;
    }

    /// <summary>
    /// A responder with nothing on offer, the state it probes in. What it
    /// hears is read for conflicts in that state too (REQ-ADV-024).
    /// </summary>
    private static Setup Build()
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        Setup? s = null;
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)],
            advertising: static () => false,
            conflicted: found => s!.Reports.Add(found));
        s = new Setup { Responder = responder, Transport = transport, Advertisement = advertisement };
        return s;
    }

    /// <summary>
    /// Another device's response giving its own address for <paramref name="host"/>,
    /// as it arrived on <paramref name="arrivedOn"/> from <paramref name="sourcePort"/>,
    /// sent to <paramref name="destination"/> (null: not known).
    /// </summary>
    private static MdnsDatagram TheirAddressFor(
        DnsName host, MdnsInterface arrivedOn, int sourcePort, IPAddress? destination)
    {
        byte[] payload = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(
                host, DnsRecordType.A, 120, CacheFlush: true, new AddressPayload(IPAddress.Parse(Other))))
            .Build();

        IPAddress source = IPAddress.Parse(arrivedOn.IsIPv6 ? OtherV6 : Other);
        return new MdnsDatagram(
            payload, new IPEndPoint(source, sourcePort), arrivedOn.Index, arrivedOn, destination);
    }

    private static void Hear(Setup s, MdnsDatagram datagram) =>
        s.Responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>The checks every ignored response must pass.</summary>
    private static void AssertIgnored(Setup s, string what)
    {
        Assert.True(s.Responder.Conflict is null, $"{what} is silently ignored: no conflict is held");
        Assert.Equal(0, s.Reports.Count, $"{what}: nothing is reported to the service");
        Assert.Equal(0, s.Transport.Sent.Count, $"{what}: nothing is sent");
    }

    // ---- Controls: read as before ---------------------------------------------

    [TestCase("A response sent to 224.0.0.251 from port 5353, with another address for the host, is a conflict")]
    [Requirement("REQ-ADV-030")]
    public static void A_response_to_the_IPv4_group_from_5353_is_read()
    {
        Setup s = Build();

        Hear(s, TheirAddressFor(s.Host, ClientNic, 5353, MdnsSocket.MulticastGroup));

        Assert.True(s.Responder.Conflict is { Type: DnsRecordType.A },
            "REQ-ADV-024: another device's address for the host is a conflict");
        Assert.Equal(1, s.Reports.Count, "reported once");
    }

    [TestCase("A response sent to ff02::fb from port 5353, with another address for the host, is a conflict")]
    [Requirement("REQ-ADV-030")]
    public static void A_response_to_the_IPv6_group_from_5353_is_read()
    {
        Setup s = Build();

        Hear(s, TheirAddressFor(s.Host, ClientNicV6, 5353, MdnsSocket.MulticastGroupV6));

        Assert.True(s.Responder.Conflict is { Type: DnsRecordType.A },
            "REQ-ADV-024: another device's address for the host is a conflict, over IPv6 too");
        Assert.True(s.Responder.Conflict!.ArrivedOn.Matches(ClientNicV6), "heard over IPv6");
    }

    // ---- Ignored ----------------------------------------------------------------

    [TestCase("The same response from a port other than 5353 is silently ignored, and a later one from 5353 is still read")]
    [Requirement("REQ-ADV-030")]
    public static void A_response_from_another_port_is_ignored()
    {
        // 53, the unicast DNS port; 5354, next to mDNS; and two that an
        // operating system hands out to a socket that names no port.
        foreach (int port in new[] { 53, 5354, 49152, 65535 })
        {
            Setup s = Build();

            Hear(s, TheirAddressFor(s.Host, ClientNic, port, MdnsSocket.MulticastGroup));
            AssertIgnored(s, $"a response from port {port}");

            // Nothing about the ignored response is kept: the same response
            // from port 5353 is read as it would have been.
            Hear(s, TheirAddressFor(s.Host, ClientNic, 5353, MdnsSocket.MulticastGroup));
            Assert.True(s.Responder.Conflict is not null, $"after port {port}, a response from 5353 is still read");
        }
    }

    [TestCase("The same response sent by unicast to this machine is silently ignored")]
    [Requirement("REQ-ADV-030")]
    public static void A_unicast_response_is_ignored()
    {
        Setup s = Build();

        Hear(s, TheirAddressFor(s.Host, ClientNic, 5353, ClientNic.Address));

        AssertIgnored(s, "a response sent by unicast to this machine's IPv4 address");
    }

    [TestCase("The same response sent by unicast over IPv6 is silently ignored")]
    [Requirement("REQ-ADV-030")]
    public static void A_unicast_response_over_IPv6_is_ignored()
    {
        Setup s = Build();

        Hear(s, TheirAddressFor(s.Host, ClientNicV6, 5353, IPAddress.Parse(OwnV6)));

        AssertIgnored(s, "a response sent by unicast to this machine's IPv6 link-local address");
    }

    [TestCase("The same response whose destination is not known is silently ignored")]
    [Requirement("REQ-ADV-030")]
    public static void A_response_whose_destination_is_not_known_is_ignored()
    {
        Setup s = Build();

        Hear(s, TheirAddressFor(s.Host, ClientNic, 5353, destination: null));

        AssertIgnored(s, "a response whose destination is not known");
    }

    [TestCase("While probing, a response from a port other than 5353 leaves the probe clear")]
    [Requirement("REQ-ADV-030")]
    public static void A_response_from_another_port_does_not_end_a_probe()
    {
        Setup s = Build();
        int waits = 0;

        // Heard during the wait after the first probe, as in
        // MdnsProbeTests.Conflict_found_while_probing_is_reported, which hears
        // the same records from port 5353 and is a conflict.
        Task Delay(TimeSpan wait, CancellationToken token)
        {
            if (waits++ == 1)
            {
                Hear(s, TheirAddressFor(s.Host, ClientNic, 5354, MdnsSocket.MulticastGroup));
            }

            return Task.CompletedTask;
        }

        ProbeResult result = s.Responder.ProbeAsync(Delay, CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(result.IsClear, "a response from another port is not an answer to the probe");
        Assert.Equal(0, s.Reports.Count, "nothing is reported to the service");
        Assert.Equal(6, s.Transport.Sent.Count, "all three probes go out, over IPv4 and IPv6");
    }
}

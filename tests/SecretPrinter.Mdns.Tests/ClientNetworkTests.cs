// -----------------------------------------------------------------------------
// ClientNetworkTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-13, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks the rule that says what the IPv4 network of a client interface is
//   (src/SecretPrinter.Mdns/ClientNetwork.cs). The relay accepts print
//   connections from that network (README REQ-SEC-012), and since 2026-10-13
//   the responder sends answers by unicast only to addresses on it (README
//   REQ-SEC-017). The rule moved that day out of ServiceHost.cs, where no test
//   read it, unchanged: /8 for an address in 10.0.0.0/8, /24 for any other,
//   worked out from the address and not read from a subnet mask.
//
// Addresses:
//   From RFC 5737's documentation ranges for any address outside 10.0.0.0/8
//   (192.0.2.0/24, and 198.51.100.0/24 for another network), and made up
//   inside 10.0.0.0/8 and 172.16.0.0/12, which the rule treats apart.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Mdns.Tests;

internal static class ClientNetworkTests
{
    [TestCase("An address outside 10.0.0.0/8 is on its /24")]
    [Requirement("REQ-SEC-017")]
    public static void An_address_outside_ten_is_on_its_slash_twenty_four()
    {
        IPNetwork network = ClientNetwork.Of(IPAddress.Parse("192.0.2.161"));

        Assert.Equal(IPNetwork.Parse("192.0.2.0/24"), network, "the address with its last octet cleared, /24");
        Assert.True(network.Contains(IPAddress.Parse("192.0.2.1")), "the first host is on it");
        Assert.True(network.Contains(IPAddress.Parse("192.0.2.254")), "and the last");
        Assert.False(network.Contains(IPAddress.Parse("198.51.100.1")), "an address on another network is not");
    }

    [TestCase("An address in 10.0.0.0/8 is on the whole /8")]
    [Requirement("REQ-SEC-017")]
    public static void An_address_in_ten_is_on_the_whole_slash_eight()
    {
        IPNetwork network = ClientNetwork.Of(IPAddress.Parse("10.20.30.40"));

        Assert.Equal(IPNetwork.Parse("10.0.0.0/8"), network, "10.0.0.0/8");
        Assert.True(network.Contains(IPAddress.Parse("10.200.1.2")), "an address far from the interface's own is on it");
        Assert.False(network.Contains(IPAddress.Parse("11.0.0.1")), "an address outside 10.0.0.0/8 is not");
    }

    [TestCase("Only the first octet is read: an address in 172.16.0.0/12 is on its /24, not on the /12")]
    [Requirement("REQ-SEC-017")]
    public static void Only_the_first_octet_is_read()
    {
        IPNetwork network = ClientNetwork.Of(IPAddress.Parse("172.16.5.4"));

        Assert.Equal(IPNetwork.Parse("172.16.5.0/24"), network,
            "172.16.0.0/12 is a private range too, and the rule gives it /24, as it always has");
    }

    [TestCase("An IPv6 address is refused: over IPv6 the service deals with link-local addresses and their scope")]
    public static void An_ipv6_address_is_refused()
    {
        bool refused = false;
        try
        {
            _ = ClientNetwork.Of(IPAddress.Parse("fe80::10"));
        }
        catch (ArgumentException)
        {
            refused = true;
        }

        Assert.True(refused, "a /24 or /8 of an IPv6 address would mean nothing");
    }
}

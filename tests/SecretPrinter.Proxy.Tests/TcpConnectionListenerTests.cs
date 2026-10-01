// -----------------------------------------------------------------------------
// TcpConnectionListenerTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-30, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the TCP listener refuses to bind anything but one specific
//   address. Its comment has said "never the wildcard" since it was written;
//   until these tests, nothing in it stopped a caller handing it one. A
//   wildcard bind would accept print jobs on every interface, the printer
//   network included (REQ-PXY-001).
//
//   The relay is about to listen on IPv6 link-local addresses as well as on
//   IPv4 (docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md), which
//   adds a second wildcard to refuse. An IPv4-mapped address is refused too:
//   it would make an IPv6 socket accept IPv4 connections, which is the IPv4
//   listener's job.
//
//   The one test that binds uses the loopback address and a port the system
//   chooses, so it touches no real network and cannot collide with a running
//   service.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Proxy.Tests;

internal static class TcpConnectionListenerTests
{
    [TestCase("The listener refuses the IPv4 wildcard address")]
    [Requirement("REQ-PXY-001")]
    public static void IPv4_wildcard_is_refused()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => _ = new TcpConnectionListener(IPAddress.Any, 0),
            "0.0.0.0 would accept connections on every interface, the printer network included");

        Assert.True(ex.Message.Contains("wildcard", StringComparison.Ordinal),
            "the message says the address is a wildcard");
    }

    [TestCase("The listener refuses the IPv6 wildcard address")]
    [Requirement("REQ-PXY-001")]
    public static void IPv6_wildcard_is_refused()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => _ = new TcpConnectionListener(IPAddress.IPv6Any, 0),
            ":: would accept connections on every interface, the printer network included");

        Assert.True(ex.Message.Contains("wildcard", StringComparison.Ordinal),
            "the message says the address is a wildcard");
    }

    [TestCase("The listener refuses an IPv4-mapped address")]
    [Requirement("REQ-PXY-001")]
    public static void IPv4_mapped_address_is_refused()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => _ = new TcpConnectionListener(IPAddress.Parse("::ffff:127.0.0.1"), 0),
            "an IPv4-mapped address would have an IPv6 socket take IPv4 connections");

        Assert.True(ex.Message.Contains("IPv4-mapped", StringComparison.Ordinal),
            "the message names the kind of address");
    }

    [TestCase("The listener binds the one address it is given")]
    [Requirement("REQ-PXY-001")]
    public static async Task Listener_binds_the_address_given()
    {
        await using var listener = new TcpConnectionListener(IPAddress.Loopback, 0);

        Assert.Equal(IPAddress.Loopback, listener.LocalEndPoint.Address,
            "the listener is bound to exactly the address it was given");
        Assert.True(listener.LocalEndPoint.Port != 0, "and to a real port");
    }
}

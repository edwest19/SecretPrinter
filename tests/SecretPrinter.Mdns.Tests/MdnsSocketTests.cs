// -----------------------------------------------------------------------------
// MdnsSocketTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Purpose:
//   Verifies that MdnsSocket is configured the way the specification requires,
//   by reading the options back from the operating system rather than trusting
//   what the code asked for.
//
// A note on what these tests are worth:
//   Asserting that SO_REUSEADDR is set proves the option is set. It does not
//   prove the service coexists with the Windows DNS Client, because that
//   depends on Windows behaviour a Linux CI runner cannot exercise. The
//   coexistence claim rests on a measured run recorded in
//   docs/findings/2026-09-01-port-5353-sharing.md. These tests guard against
//   regression - somebody removing the option - which is a real and likely
//   failure, and is exactly what a test can catch.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Mdns.Tests;

/// <summary>
/// Supplies a fixed set of adapters, so interface-resolution rules can be
/// tested against arrangements this machine does not have.
/// </summary>
internal sealed class FakeInventory(params LocalAdapter[] adapters) : IInterfaceInventory
{
    public IReadOnlyList<LocalAdapter> Adapters { get; } = adapters;
}

internal static class MdnsSocketTests
{
    /// <summary>
    /// Finds a usable IPv4 interface address on this machine, or skips.
    /// </summary>
    private static IPAddress? FindUsableInterface()
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || !adapter.SupportsMulticast)
            {
                continue;
            }

            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(unicast.Address))
                {
                    return unicast.Address;
                }
            }
        }

        return null;
    }

    private static MdnsSocket OpenOrSkip(out IPAddress address)
    {
        IPAddress? found = FindUsableInterface()
            ?? throw new SkipException("No non-loopback multicast-capable IPv4 interface on this machine.");

        address = found;

        try
        {
            return MdnsSocket.Open([found]);
        }
        catch (SocketException ex)
        {
            throw new SkipException($"Could not bind UDP 5353 in this environment: {ex.SocketErrorCode}.");
        }
    }

    // ---- Interface resolution -----------------------------------------------

    [TestCase("Resolve rejects an IPv6 address with a specific reason")]
    [Requirement("REQ-CFG-003")]
    public static void Resolve_rejects_ipv6()
    {
        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.Resolve(IPAddress.IPv6Loopback),
            "IPv6 addresses are not usable by this IPv4-only service");

        Assert.True(ex.Message.Contains("IPv4", StringComparison.Ordinal),
            "the message should name IPv4 as the requirement");
    }

    [TestCase("Resolve rejects an address that is not on this machine")]
    [Requirement("REQ-CFG-003")]
    public static void Resolve_rejects_foreign_address()
    {
        // 203.0.113.0/24 is TEST-NET-3 (RFC 5737), reserved for documentation
        // and guaranteed not to be a real local address.
        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.Resolve(IPAddress.Parse("203.0.113.7")),
            "an address not held by any adapter cannot be bound");

        Assert.True(ex.Message.Contains("not an address on any interface", StringComparison.Ordinal),
            "the message should say the address is not local");
    }

    [TestCase("ResolveAll rejects an empty interface list")]
    [Requirement("REQ-CFG-005")]
    public static void ResolveAll_rejects_empty()
    {
        Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveAll([]),
            "starting with no interfaces would be a silently useless service");
    }

    [TestCase("ResolveAll rejects the same address configured twice")]
    [Requirement("REQ-CFG-003")]
    public static void ResolveAll_rejects_duplicates()
    {
        IPAddress? address = FindUsableInterface();
        if (address is null)
        {
            Assert.Skip("No usable IPv4 interface on this machine.");
            return;
        }

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveAll([address, address]),
            "a duplicated interface is a configuration mistake, not a valid setup");

        Assert.True(ex.Message.Contains("more than once", StringComparison.Ordinal),
            "the message should say the address was repeated");
    }

    [TestCase("ResolveAll rejects two addresses that share one interface")]
    [Requirement("REQ-CFG-006")]
    public static void ResolveAll_rejects_shared_interface()
    {
        // Previously this needed a machine with an adapter holding two IPv4
        // addresses, so it skipped everywhere and the requirement sat
        // unverified. Supplying the adapters through IInterfaceInventory makes
        // it an ordinary test that runs in every environment.
        var inventory = new FakeInventory(
            new LocalAdapter(
                "Ethernet 2",
                13,
                IsUp: true,
                SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.234"), IPAddress.Parse("192.168.1.235")]));

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveAll(
                [IPAddress.Parse("192.168.1.234"), IPAddress.Parse("192.168.1.235")], inventory),
            "two roles on one interface would make arriving datagrams unattributable");

        Assert.True(ex.Message.Contains("both on interface", StringComparison.Ordinal),
            "the message should name the shared interface");
    }

    [TestCase("An interface that is down is rejected with that reason")]
    [Requirement("REQ-CFG-003")]
    public static void Down_interface_is_rejected()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet 2", 13, IsUp: false, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.234")]));

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.1.234"), inventory),
            "binding a down interface would produce a service that silently receives nothing");

        Assert.True(ex.Message.Contains("not up", StringComparison.Ordinal),
            "the operator needs to know the adapter is down, not merely that it failed");
    }

    [TestCase("An interface without multicast support is rejected with that reason")]
    [Requirement("REQ-CFG-003")]
    public static void Non_multicast_interface_is_rejected()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("VPN", 22, IsUp: true, SupportsMulticast: false,
                [IPAddress.Parse("10.8.0.2")]));

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.Resolve(IPAddress.Parse("10.8.0.2"), inventory),
            "mDNS cannot work on an interface that carries no multicast");

        Assert.True(ex.Message.Contains("multicast", StringComparison.Ordinal),
            "the reason must name multicast so the operator can act on it");
    }

    [TestCase("An interface with no IPv4 index is rejected with that reason")]
    [Requirement("REQ-CFG-003")]
    public static void Interface_without_ipv4_index_is_rejected()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("Odd", null, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.9.9")]));

        Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.9.9"), inventory),
            "without an index, arriving datagrams could not be attributed to this interface");
    }

    // ---- IPv6 companion resolution -------------------------------------------
    //
    // These carry no [Requirement] marker. REQ-ADV-018 is about receiving and
    // answering over IPv6, and none of that exists yet; marking preparatory
    // code would make the coverage matrix claim a behaviour the service does
    // not have. The marker goes on the socket work when the socket does it.

    [TestCase("A resolved IPv4 interface says it carries IPv4")]
    public static void Resolved_interface_states_its_transport()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", 13, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.98")], IPv6Index: 15));

        MdnsInterface resolved =
            MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.1.98"), inventory);

        Assert.Equal(AddressFamily.InterNetwork, resolved.Transport,
            "an entry that does not state its family cannot be used to choose one");
        Assert.False(resolved.IsIPv6, "this entry carries IPv4");
        Assert.True(resolved.ToString().Contains("IPv4", StringComparison.Ordinal),
            "a log line naming an interface must say which family it means, since the indexes differ");
    }

    [TestCase("The IPv6 companion keeps the IPv4 address and takes the IPv6 index")]
    public static void IPv6_companion_keeps_ipv4_address()
    {
        // The two indexes differ deliberately: Windows numbers the families
        // separately, and a companion that inherited the IPv4 index would
        // attribute arriving IPv6 datagrams to the wrong interface - or to no
        // interface at all, which is silent.
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", 13, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.98")], IPv6Index: 15));

        MdnsInterface ipv4 =
            MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.1.98"), inventory);
        MdnsInterface ipv6 = MdnsInterfaceResolver.ResolveIPv6(ipv4, inventory);

        Assert.True(ipv6.IsIPv6, "the companion carries IPv6");
        Assert.Equal(15, ipv6.Index, "the companion takes the adapter's IPv6 index, not its IPv4 one");
        Assert.Equal(ipv4.Address, ipv6.Address,
            "the address advertised over IPv6 is still the IPv4 address the relay listens on");
        Assert.Equal(ipv4.Name, ipv6.Name, "both entries describe one adapter");
    }

    [TestCase("An interface with IPv6 disabled is rejected, naming IPv6")]
    public static void Interface_without_ipv6_is_rejected()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", 13, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.98")]));

        MdnsInterface ipv4 =
            MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.1.98"), inventory);

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveIPv6(ipv4, inventory),
            "a client interface without IPv6 can never be discovered by iOS");

        Assert.True(ex.Message.Contains("IPv6", StringComparison.Ordinal),
            "the operator needs to know it is IPv6 that is missing, not merely that something failed");
    }

    [TestCase("An IPv6 entry has no IPv6 companion of its own")]
    public static void IPv6_entry_has_no_companion()
    {
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", 13, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.98")], IPv6Index: 15));

        MdnsInterface ipv6 = MdnsInterfaceResolver.ResolveIPv6(
            MdnsInterfaceResolver.Resolve(IPAddress.Parse("192.168.1.98"), inventory), inventory);

        Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveIPv6(ipv6, inventory),
            "deriving a companion from a companion would produce an entry nobody asked for");
    }

    // ---- Socket configuration -----------------------------------------------

    [TestCase("Open shares port 5353 rather than seizing it")]
    [RequiresNetwork]
    [Requirement("REQ-ADV-014")]
    public static void Open_sets_reuse_address()
    {
        using MdnsSocket socket = OpenOrSkip(out _);
        MdnsSocketConfiguration configuration = socket.ReadBackConfiguration();

        Assert.True(configuration.ReuseAddress,
            "SO_REUSEADDR must be set, or the bind fails against the Windows DNS Client");
        Assert.False(configuration.ExclusiveAddressUse,
            "exclusive use would defeat SO_REUSEADDR on Windows");
        Assert.Equal(MdnsSocket.MdnsPort, configuration.LocalEndPoint.Port,
            "the socket must be bound to the mDNS port");
        Assert.Equal(IPAddress.Any, configuration.LocalEndPoint.Address,
            "a wildcard bind is required for reliable multicast receipt on Windows");
    }

    [TestCase("Open sets multicast TTL to 255 as RFC 6762 requires")]
    [RequiresNetwork]
    [Requirement("REQ-ADV-013")]
    public static void Open_sets_ttl_255()
    {
        using MdnsSocket socket = OpenOrSkip(out _);
        MdnsSocketConfiguration configuration = socket.ReadBackConfiguration();

        Assert.Equal(255, configuration.MulticastTimeToLive,
            "RFC 6762 s11 requires TTL 255; a lower value invites silent drops");
        Assert.Equal(255, MdnsSocket.RequiredMulticastTtl,
            "the constant and the specification must not drift apart");
    }

    [TestCase("Open enables IP_PKTINFO so arrival interface is reported")]
    [RequiresNetwork]
    [Requirement("REQ-ADV-015")]
    public static void Open_enables_packet_information()
    {
        using MdnsSocket socket = OpenOrSkip(out _);
        MdnsSocketConfiguration configuration = socket.ReadBackConfiguration();

        Assert.True(configuration.PacketInformation,
            "without IP_PKTINFO the arrival interface would have to be guessed");
    }

    // Deliberately unmarked. REQ-OBS-001 is about the service LOGGING its
    // interfaces at startup; this only proves the socket exposes them for
    // something else to log. Marking it would overstate what is verified.
    [TestCase("Open reports the interfaces it joined")]
    [RequiresNetwork]
    public static void Open_reports_joined_interfaces()
    {
        using MdnsSocket socket = OpenOrSkip(out IPAddress address);

        Assert.Equal(1, socket.Interfaces.Count, "one interface was requested");
        Assert.Equal(address, socket.Interfaces[0].Address, "the joined address must be the one requested");
        Assert.True(socket.Interfaces[0].Index > 0, "a real interface has a positive index");
        Assert.True(socket.Interfaces[0].Name.Length > 0, "the interface should be named for logging");
    }

    // ---- Interface confinement ----------------------------------------------

    [TestCase("Sending out an interface the socket does not own is refused")]
    [RequiresNetwork]
    [Requirement("REQ-ADV-011")]
    public static void Send_refuses_unopened_interface()
    {
        using MdnsSocket socket = OpenOrSkip(out _);

        var foreign = new MdnsInterface(
            "not-ours", IPAddress.Parse("203.0.113.9"), 9999, AddressFamily.InterNetwork);

        Assert.Throws<ArgumentException>(
            () => socket.SendMulticastAsync(new byte[] { 0, 0 }, foreign, CancellationToken.None)
                        .GetAwaiter().GetResult(),
            "advertising out an unconfigured interface could leak onto the printer network");
    }

    [TestCase("Sending out an interface with a mismatched index is refused")]
    [RequiresNetwork]
    [Requirement("REQ-ADV-011")]
    public static void Send_refuses_index_mismatch()
    {
        using MdnsSocket socket = OpenOrSkip(out IPAddress address);

        // Right address, wrong index: the kind of thing a stale cached
        // descriptor would produce after an adapter change.
        var mismatched = new MdnsInterface(
            "stale", address, socket.Interfaces[0].Index + 1000, AddressFamily.InterNetwork);

        Assert.Throws<ArgumentException>(
            () => socket.SendMulticastAsync(new byte[] { 0, 0 }, mismatched, CancellationToken.None)
                        .GetAwaiter().GetResult(),
            "a descriptor whose index no longer matches must not be trusted");
    }

    [TestCase("Open fails loudly when an interface cannot be used")]
    [Requirement("REQ-LIF-004")]
    public static void Open_fails_fast_on_bad_interface()
    {
        Assert.Throws<MdnsInterfaceException>(
            () => MdnsSocket.Open([IPAddress.Parse("203.0.113.11")]),
            "a service that starts without a usable interface would receive nothing, silently");
    }

    // ---- Lifecycle ----------------------------------------------------------

    [TestCase("Dispose is safe to call more than once")]
    [RequiresNetwork]
    [Requirement("REQ-LIF-002")]
    public static void Dispose_is_idempotent()
    {
        MdnsSocket socket = OpenOrSkip(out _);
        socket.Dispose();
        socket.Dispose();
    }

    [TestCase("Using a disposed socket throws rather than failing quietly")]
    [RequiresNetwork]
    [Requirement("REQ-LIF-002")]
    public static void Disposed_socket_rejects_use()
    {
        MdnsSocket socket = OpenOrSkip(out _);
        socket.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => socket.ReadBackConfiguration(),
            "a disposed socket must not appear to still work");
    }

    // ---- Datagram classification --------------------------------------------

    [TestCase("A source port other than 5353 marks a legacy unicast querier")]
    [Requirement("REQ-ADV-017")]
    public static void Legacy_querier_detected_by_source_port()
    {
        var fromEphemeral = new MdnsDatagram(
            [], new IPEndPoint(IPAddress.Parse("192.0.2.10"), 51234), 1, null);

        var fromMdnsPort = new MdnsDatagram(
            [], new IPEndPoint(IPAddress.Parse("192.0.2.10"), MdnsSocket.MdnsPort), 1, null);

        Assert.True(fromEphemeral.IsLegacyUnicastQuerier,
            "RFC 6762 s6.7 identifies legacy queriers by a source port other than 5353");
        Assert.False(fromMdnsPort.IsLegacyUnicastQuerier,
            "a query from port 5353 is a normal multicast querier");
    }

    [TestCase("A datagram from an unconfigured interface has no ArrivedOn")]
    [Requirement("REQ-ADV-011")]
    public static void Unknown_interface_yields_null_arrived_on()
    {
        var datagram = new MdnsDatagram(
            [], new IPEndPoint(IPAddress.Parse("192.0.2.10"), MdnsSocket.MdnsPort), 4242, null);

        Assert.Null(datagram.ArrivedOn,
            "callers must be able to tell that a datagram is not from a configured network");
    }
}

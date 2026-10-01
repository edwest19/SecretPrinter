// -----------------------------------------------------------------------------
// ListenPlanTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-30, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks the rule REQ-ADV-021 rests on: the relay listens on exactly the
//   addresses the advertisement publishes, because the listeners are derived
//   from the published address records rather than worked out a second time.
//   An address published and not listened on would be a printer that is
//   discovered and cannot be reached.
//
//   Also checks that the listeners for one interface open together or not at
//   all. If the IPv4 listener opened and the link-local one failed, an AAAA
//   record would be published with nothing listening behind it.
//
// Addresses:
//   fe80::10 and fe80::20 are made up, scoped to 15, the made-up IPv6 index of
//   the client adapter here. 192.168.1.161 is the client address already used
//   throughout the repository.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Mdns;
using SecretPrinter.Proxy;
using SecretPrinter.Responder;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ListenPlanTests
{
    private static readonly MdnsInterface Client =
        new("Ethernet", IPAddress.Parse("192.168.1.161"), 15, AddressFamily.InterNetwork);

    private static readonly MdnsInterface ClientV6 =
        new("Ethernet", IPAddress.Parse("192.168.1.161"), 15, AddressFamily.InterNetworkV6);

    private static readonly IPNetwork ClientNetwork = IPNetwork.Parse("192.168.1.0/24");

    private static readonly IPAddress LinkLocal1 = IPAddress.Parse("fe80::10%15");
    private static readonly IPAddress LinkLocal2 = IPAddress.Parse("fe80::20%15");

    private static Advertisement Advertised(IPAddress address, params IPAddress[] linkLocal) =>
        AdvertisementBuilder.Build(
            new PrinterCapabilities(["txtvers=1", "rp=ipp/print"], 631, CapabilitySource.ForTest("listen plan tests")),
            new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631),
            address,
            linkLocal);

    // ---- What is listened on ------------------------------------------------

    [TestCase("The relay listens on exactly the addresses the advertisement publishes, in its order")]
    [Requirement("REQ-ADV-021")]
    public static void Listens_on_exactly_the_published_addresses()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1, LinkLocal2), ClientV6);

        IReadOnlyList<ListenEntry> plan = ListenPlan.From(advertised, ClientNetwork);

        Assert.Equal(3, plan.Count, "one listener for the A record and one for each AAAA record");
        Assert.Equal(Client.Address, plan[0].Address, "the IPv4 address the A record carries");
        Assert.Equal(LinkLocal1, plan[1].Address, "the first link-local address, scope included");
        Assert.Equal(LinkLocal2, plan[2].Address, "the second");
        Assert.True(plan[1].Address.ScopeId == 15 && plan[2].Address.ScopeId == 15,
            "the scope binds each link-local listener to this interface");
    }

    [TestCase("A link-local listener permits fe80::/10 on the interface's scope, and nothing else")]
    [Requirement("REQ-ADV-021")]
    [Requirement("REQ-SEC-012")]
    public static void Link_local_listener_permits_its_own_link_only()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1), ClientV6);

        ListenEntry entry = ListenPlan.From(advertised, ClientNetwork)[1];

        Assert.Equal(IPNetwork.Parse("fe80::/10"), entry.PermittedNetworks.Single(),
            "link-local clients only; the IPv4 network is the IPv4 listener's");
        Assert.Equal(15L, entry.PermittedLinkLocalScopes.Single(),
            "and only on this interface's scope, which the network check cannot see");
    }

    [TestCase("The IPv4 listener permits the interface's IPv4 network and no link-local scope")]
    [Requirement("REQ-SEC-012")]
    public static void IPv4_listener_permits_its_network_only()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1), ClientV6);

        ListenEntry entry = ListenPlan.From(advertised, ClientNetwork)[0];

        Assert.Equal(ClientNetwork, entry.PermittedNetworks.Single(), "the network of the client interface");
        Assert.Equal(0, entry.PermittedLinkLocalScopes.Count, "no link-local client arrives on an IPv4 listener");
    }

    [TestCase("Without link-local addresses, the relay listens on the IPv4 address alone")]
    [Requirement("REQ-ADV-021")]
    public static void Without_link_local_addresses_only_ipv4_is_listened_on()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address), ClientV6);

        IReadOnlyList<ListenEntry> plan = ListenPlan.From(advertised, ClientNetwork);

        Assert.Equal(1, plan.Count, "nothing published over IPv6, nothing listened on over IPv6");
        Assert.Equal(Client.Address, plan[0].Address, "the IPv4 address");
    }

    // ---- What is refused ----------------------------------------------------

    [TestCase("An advertisement whose A record is not the interface's address is refused")]
    public static void Foreign_a_record_is_refused()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(IPAddress.Parse("192.168.1.99")), ClientV6);

        Assert.Throws<InvalidOperationException>(
            () => ListenPlan.From(advertised, ClientNetwork),
            "an advertisement built for another address belongs to another interface");
    }

    [TestCase("An AAAA record on a scope other than the interface's is refused")]
    [Requirement("REQ-ADV-021")]
    public static void Aaaa_on_another_scope_is_refused()
    {
        var advertised = new AdvertisedInterface(
            Client, Advertised(Client.Address, IPAddress.Parse("fe80::10%16")), ClientV6);

        Assert.Throws<InvalidOperationException>(
            () => ListenPlan.From(advertised, ClientNetwork),
            "an address on another interface is not valid on this one, and a listener on it would be elsewhere");
    }

    [TestCase("An AAAA record with no IPv6 entry to check its scope against is refused")]
    public static void Aaaa_without_companion_is_refused()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1));

        Assert.Throws<InvalidOperationException>(
            () => ListenPlan.From(advertised, ClientNetwork),
            "without the interface's IPv6 index, nothing says which link the address is on");
    }

    [TestCase("An IPv4 network that does not hold the interface's address is refused")]
    public static void Foreign_ipv4_network_is_refused()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address), ClientV6);

        Assert.Throws<ArgumentException>(
            () => ListenPlan.From(advertised, IPNetwork.Parse("10.0.0.0/8")),
            "the permitted network is the client interface's own");
    }

    // ---- Opening --------------------------------------------------------------

    /// <summary>A listener that records being closed.</summary>
    private sealed class RecordingListener(IPAddress address) : IConnectionListener
    {
        public bool Disposed { get; private set; }

        public IPEndPoint LocalEndPoint { get; } = new(address, 631);

        public Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken) =>
            Task.FromException<IDuplexConnection>(new InvalidOperationException("not used here"));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [TestCase("Every listener in the plan is opened, in order")]
    [Requirement("REQ-ADV-021")]
    public static async Task Every_listener_is_opened()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1), ClientV6);
        IReadOnlyList<ListenEntry> plan = ListenPlan.From(advertised, ClientNetwork);

        IReadOnlyList<IConnectionListener> opened =
            await ListenPlan.OpenAllAsync(plan, entry => new RecordingListener(entry.Address));

        Assert.Equal(2, opened.Count, "one listener per entry");
        Assert.Equal(Client.Address, opened[0].LocalEndPoint.Address, "the IPv4 listener first");
        Assert.Equal(LinkLocal1, opened[1].LocalEndPoint.Address, "then the link-local one");
    }

    [TestCase("If one listener cannot be opened, those already open are closed and the failure is raised")]
    [Requirement("REQ-ADV-021")]
    public static async Task One_failure_closes_the_rest()
    {
        var advertised = new AdvertisedInterface(Client, Advertised(Client.Address, LinkLocal1), ClientV6);
        IReadOnlyList<ListenEntry> plan = ListenPlan.From(advertised, ClientNetwork);
        var openedBeforeFailure = new List<RecordingListener>();

        SocketException? raised = null;
        try
        {
            await ListenPlan.OpenAllAsync(plan, entry =>
            {
                if (entry.Address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // As when a published link-local address is gone by the
                    // time the listeners open.
                    throw new SocketException((int)SocketError.AddressNotAvailable);
                }

                var listener = new RecordingListener(entry.Address);
                openedBeforeFailure.Add(listener);
                return listener;
            });
        }
        catch (SocketException ex)
        {
            raised = ex;
        }

        Assert.True(raised is not null, "the failure reaches the caller, which stops the service");
        Assert.Equal(1, openedBeforeFailure.Count, "the IPv4 listener had been opened");
        Assert.True(openedBeforeFailure[0].Disposed,
            "and is closed again, so nothing listens while an AAAA record points at nothing");
    }
}

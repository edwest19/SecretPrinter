// -----------------------------------------------------------------------------
// LinkLocalAddressTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-30, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks how the IPv6 link-local addresses of a client interface are read
//   and chosen. Edwin decided on 2026-09-30 that the service publishes, and
//   the relay listens on, the client interface's link-local addresses and no
//   other IPv6 address. The measurements and the reasons are in
//   docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md.
//
// Why these carry no [Requirement] marker:
//   REQ-ADV-021 is being rewritten to say what is published, and nothing
//   publishes an AAAA record yet. A marker here would let the coverage matrix
//   claim a behaviour the service does not have. The markers go on the code
//   that publishes and listens, when it does.
//
// Addresses:
//   Every IPv6 address below is made up: fe80::10 and the like, and
//   2001:db8::1 from the documentation prefix (RFC 3849). No address of any
//   real machine appears in this file.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.TestKit;

namespace SecretPrinter.Mdns.Tests;

internal static class LinkLocalAddressTests
{
    private const int IPv4Index = 13;
    private const int IPv6Index = 15;
    private static readonly IPAddress ClientIPv4 = IPAddress.Parse("192.168.1.98");

    /// <summary>A client adapter holding the given link-local addresses.</summary>
    private static FakeInventory Adapter(params LocalLinkLocalAddress[] linkLocal) =>
        new(new LocalAdapter(
            "Ethernet",
            IPv4Index,
            IsUp: true,
            SupportsMulticast: true,
            [ClientIPv4],
            IPv6Index,
            IPv6LinkLocalAddresses: linkLocal));

    /// <summary>A link-local address scoped to the adapter above.</summary>
    private static LocalLinkLocalAddress Scoped(string address, LocalAddressCondition condition) =>
        new(IPAddress.Parse($"{address}%{IPv6Index}"), condition);

    /// <summary>The adapter's IPv6 entry, as the responder socket would hold it.</summary>
    private static MdnsInterface IPv6Entry(FakeInventory inventory) =>
        MdnsInterfaceResolver.ResolveIPv6(MdnsInterfaceResolver.Resolve(ClientIPv4, inventory), inventory);

    [TestCase("The client interface's preferred link-local addresses are resolved, each with its scope")]
    public static void Preferred_link_local_addresses_are_resolved()
    {
        FakeInventory inventory = Adapter(
            Scoped("fe80::20", LocalAddressCondition.Preferred),
            Scoped("fe80::10", LocalAddressCondition.Preferred));

        IReadOnlyList<IPAddress> resolved =
            MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory);

        Assert.Equal(2, resolved.Count, "both preferred link-local addresses are published and listened on");
        Assert.Equal(IPAddress.Parse($"fe80::10%{IPv6Index}"), resolved[0],
            "the addresses come back in a fixed order, so logs and records do not reorder between runs");
        Assert.Equal(IPAddress.Parse($"fe80::20%{IPv6Index}"), resolved[1], "the second address follows");
        Assert.True(resolved.All(address => address.ScopeId == IPv6Index),
            "each address keeps its scope, which is what binds a listener to this interface");
    }

    [TestCase("A link-local address that is not preferred is not resolved")]
    public static void Only_preferred_link_local_addresses_are_resolved()
    {
        // Tentative: duplicate address detection has not finished, so the
        // address is not yet usable. Deprecated: RFC 4862 says new
        // connections should avoid it. Invalid and Unknown: nothing about the
        // address may be relied on. Publishing any of them would invite a
        // connection the address may not accept.
        FakeInventory inventory = Adapter(
            Scoped("fe80::10", LocalAddressCondition.Preferred),
            Scoped("fe80::20", LocalAddressCondition.Tentative),
            Scoped("fe80::30", LocalAddressCondition.Deprecated),
            Scoped("fe80::40", LocalAddressCondition.Invalid),
            Scoped("fe80::50", LocalAddressCondition.Unknown));

        IReadOnlyList<IPAddress> resolved =
            MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory);

        Assert.Equal(1, resolved.Count, "only the preferred address may be published");
        Assert.Equal(IPAddress.Parse($"fe80::10%{IPv6Index}"), resolved[0], "and it is the preferred one");
    }

    [TestCase("An adapter with no preferred link-local address resolves to none, without failing")]
    public static void No_preferred_link_local_address_resolves_to_none()
    {
        // Every interface has a link-local address (RFC 4291 section 2.1), but
        // it may not be usable yet, for example while duplicate address
        // detection runs. That is a state to report, not a reason to stop:
        // the caller is to publish no AAAA record and listen on no IPv6
        // address, which is what the service did before any of this existed.
        FakeInventory inventory = Adapter(Scoped("fe80::10", LocalAddressCondition.Tentative));

        IReadOnlyList<IPAddress> resolved =
            MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory);

        Assert.Equal(0, resolved.Count, "an address that is not yet usable is not published");
    }

    [TestCase("Link-local resolution refuses an IPv4 entry, even when the two indexes are equal")]
    public static void Link_local_resolution_refuses_an_ipv4_entry()
    {
        // The IPv4 and IPv6 indexes are equal here on purpose. On every adapter
        // this project has measured they were equal, and with different ones
        // the stale-index check would refuse the entry anyway, so this test
        // could not tell whether the IPv4 entry itself was refused.
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", IPv6Index, IsUp: true, SupportsMulticast: true,
                [ClientIPv4], IPv6Index,
                IPv6LinkLocalAddresses: [Scoped("fe80::10", LocalAddressCondition.Preferred)]));
        MdnsInterface ipv4 = MdnsInterfaceResolver.Resolve(ClientIPv4, inventory);

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveLinkLocal(ipv4, inventory),
            "the IPv4 entry's index is an IPv4 index, and no scope may be checked against it");

        Assert.True(ex.Message.Contains("is not an IPv6 entry", StringComparison.Ordinal),
            "the refusal is for the kind of entry, not for anything else about the adapter");
    }

    [TestCase("An address that is not link-local, listed as link-local, is refused rather than skipped")]
    public static void Non_link_local_address_in_the_list_is_refused()
    {
        // The inventory is the only place that reads the machine's addresses,
        // and it reads link-local ones only, so that no global, temporary or
        // unique local address is ever in hand to be published. A global
        // address arriving here means that promise was broken. Skipping it
        // would hide the break; refusing makes it impossible to miss.
        FakeInventory inventory = Adapter(
            Scoped("fe80::10", LocalAddressCondition.Preferred),
            new LocalLinkLocalAddress(IPAddress.Parse("2001:db8::1"), LocalAddressCondition.Preferred));

        var ex = Assert.Throws<InvalidOperationException>(
            () => MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory),
            "a global address must never reach the list the service will publish from");

        Assert.True(ex.Message.Contains("2001:db8::1", StringComparison.Ordinal),
            "the message names the offending address");
    }

    [TestCase("A link-local address scoped to another interface is refused")]
    public static void Link_local_address_with_another_scope_is_refused()
    {
        // A link-local address means something only together with its scope.
        // The listener will be bound with this scope, and the relay will
        // compare each connection's scope with this interface's IPv6 index, so
        // an address reported with any other scope cannot be used here.
        FakeInventory inventory = Adapter(
            new LocalLinkLocalAddress(IPAddress.Parse("fe80::10%16"), LocalAddressCondition.Preferred));

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory),
            "an address scoped to another interface would bind the listener somewhere else");

        Assert.True(ex.Message.Contains("16", StringComparison.Ordinal)
                    && ex.Message.Contains("15", StringComparison.Ordinal),
            "the message gives both the address's scope and the interface's IPv6 index");
    }

    [TestCase("An adapter whose IPv6 index changed since its entry was resolved is refused")]
    public static void Adapter_with_a_changed_ipv6_index_is_refused()
    {
        // The entry was resolved when the adapter had IPv6 index 15; it now
        // reports 17 and, for the moment, no link-local address. Returning an
        // empty list would look like an adapter that has none, when in fact
        // the entry no longer describes it.
        FakeInventory before = Adapter(Scoped("fe80::10", LocalAddressCondition.Preferred));
        MdnsInterface entry = IPv6Entry(before);

        var after = new FakeInventory(
            new LocalAdapter("Ethernet", IPv4Index, IsUp: true, SupportsMulticast: true,
                [ClientIPv4], 17, IPv6LinkLocalAddresses: []));

        var ex = Assert.Throws<MdnsInterfaceException>(
            () => MdnsInterfaceResolver.ResolveLinkLocal(entry, after),
            "an entry that no longer describes its adapter must not yield an answer");

        Assert.True(ex.Message.Contains("17", StringComparison.Ordinal),
            "the message gives the index the adapter reports now");
    }

    [TestCase("An inventory that did not read link-local addresses is refused")]
    public static void Inventory_without_link_local_addresses_is_refused()
    {
        // Null means the addresses were not read, which is different from an
        // adapter that has none. Treating it as none would quietly publish no
        // AAAA record, and the operator would have no way to tell why.
        var inventory = new FakeInventory(
            new LocalAdapter("Ethernet", IPv4Index, IsUp: true, SupportsMulticast: true,
                [ClientIPv4], IPv6Index));

        Assert.Throws<InvalidOperationException>(
            () => MdnsInterfaceResolver.ResolveLinkLocal(IPv6Entry(inventory), inventory),
            "addresses that were never read must not be mistaken for an adapter that has none");
    }

    [TestCase("On this machine, the inventory reads the link-local addresses of every adapter, even when there are none")]
    [RequiresNetwork]
    public static void System_inventory_reads_link_local_addresses_of_every_adapter()
    {
        // Separate from the test below, which skips on a machine with no
        // link-local address. An inventory that stopped reading the list
        // would also produce no link-local addresses, and would hide behind
        // that skip. This runs on any machine with an adapter.
        IReadOnlyList<LocalAdapter> adapters = SystemInterfaceInventory.Instance.Adapters;

        if (adapters.Count == 0)
        {
            Assert.Skip("This machine reports no network adapters.");
        }

        Assert.True(adapters.All(adapter => adapter.IPv6LinkLocalAddresses is not null),
            "a list that was never read would look like an adapter without IPv6");
    }

    [TestCase("On this machine, the inventory reads only link-local IPv6 addresses, each scoped to its adapter's IPv6 index")]
    [RequiresNetwork]
    public static void System_inventory_reads_only_scoped_link_local_addresses()
    {
        // Reads the real machine. This is the test that shows, on Windows,
        // that the platform reports a link-local address with its adapter's
        // IPv6 index as the scope, which the relay's scope check will rely on,
        // and that the inventory keeps no global, temporary or unique local
        // address, although the machines this was built on hold all three.
        var linkLocal = SystemInterfaceInventory.Instance.Adapters
            .SelectMany(adapter => (adapter.IPv6LinkLocalAddresses ?? [])
                .Select(entry => (adapter, entry)))
            .ToList();

        if (linkLocal.Count == 0)
        {
            Assert.Skip("No adapter on this machine holds an IPv6 link-local address.");
        }

        foreach ((LocalAdapter adapter, LocalLinkLocalAddress entry) in linkLocal)
        {
            Assert.True(entry.Address.IsIPv6LinkLocal,
                $"'{adapter.Name}' listed an address outside fe80::/10 among its link-local addresses");
            Assert.Equal((long?)adapter.IPv6Index, (long?)entry.Address.ScopeId,
                $"a link-local address on '{adapter.Name}' is scoped to its adapter's IPv6 index");
        }
    }
}

// -----------------------------------------------------------------------------
// MdnsInterface.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Purpose:
//   Turns an IPv4 address from configuration into a fully identified network
//   interface: its friendly name, its operating-system index, and which address
//   family it carries.
//
//   One adapter yields one entry per family. The IPv4 entry comes from the
//   configured address directly; the IPv6 entry is derived from it, keeping the
//   same advertised IPv4 address and taking the adapter's separate IPv6 index.
//   That is what lets a caller pick a family by picking an entry, with no extra
//   parameter threaded through the transport.
//
//   The index matters more than it looks. Every datagram the service receives
//   is attributed to an interface by index (IP_PKTINFO), and deciding whether
//   to answer a query depends on comparing that index against the interfaces
//   the service was configured for. Getting this mapping wrong would mean
//   answering queries from the printer network on the client network, or the
//   reverse - exactly the confusion this project must not create.
//
// This file opens no sockets and sends nothing.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Spec;

namespace SecretPrinter.Mdns;

/// <summary>
/// A resolved local interface, for one address family: what it is called, the
/// address it advertises, its index in that family, and which family it carries.
/// </summary>
/// <param name="Name">Friendly adapter name, e.g. "Ethernet 2".</param>
/// <param name="Address">
/// The IPv4 address the service was configured with. This stays IPv4 even on an
/// entry whose <paramref name="Transport"/> is IPv6, and that is deliberate
/// rather than an oversight. The transport family decides how a datagram travels;
/// it does not decide what the datagram says. An iPhone that asks over IPv6 is
/// answered with this IPv4 address, because that is the address the relay
/// listens on - measured on 2026-09-06 and recorded in
/// docs/findings/2026-09-06-ipv6-mdns-transport.md.
/// </param>
/// <param name="Index">
/// Operating-system interface index for <paramref name="Transport"/>, as
/// reported by IP_PKTINFO or IPV6_PKTINFO. Windows numbers the two families
/// separately, so the same adapter has two different indexes and they must not
/// be compared across families.
/// </param>
/// <param name="Transport">
/// Which address family this entry sends and receives over. One adapter yields
/// one entry per family it can carry, so choosing an entry is the whole of
/// choosing a family: nothing downstream needs a second parameter to say
/// whether a reply goes to 224.0.0.251 or ff02::fb.
/// </param>
public sealed record MdnsInterface(string Name, IPAddress Address, int Index, AddressFamily Transport)
{
    /// <summary>True when this entry carries IPv6.</summary>
    public bool IsIPv6 => Transport == AddressFamily.InterNetworkV6;

    public override string ToString() =>
        $"{Name} ({Address}, {(IsIPv6 ? "IPv6" : "IPv4")} index {Index})";
}

/// <summary>Thrown when a configured interface cannot be used. Never swallowed.</summary>
public sealed class MdnsInterfaceException(string message) : Exception(message);

/// <summary>Resolves configured addresses to local interfaces.</summary>
public static class MdnsInterfaceResolver
{
    /// <summary>
    /// Resolves one IPv4 address to the interface that holds it, using the
    /// machine's real adapters.
    /// </summary>
    public static MdnsInterface Resolve(IPAddress address) =>
        Resolve(address, SystemInterfaceInventory.Instance);

    /// <summary>
    /// Resolves one IPv4 address against a supplied inventory.
    /// </summary>
    /// <exception cref="MdnsInterfaceException">
    /// The address is not IPv4, is not present, is on an interface that is
    /// down, or is on an interface that does not support multicast. Each is
    /// reported with its specific reason rather than a generic failure, because
    /// an operator debugging a silent service needs to know which it is.
    /// </exception>
    [Requirement("REQ-CFG-003",
        "Rejects an unusable interface with a message naming the specific reason: not IPv4, not present, down, or no multicast.")]
    public static MdnsInterface Resolve(IPAddress address, IInterfaceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(inventory);

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new MdnsInterfaceException(
                $"{address} is not an IPv4 address. This service binds IPv4 only.");
        }

        foreach (LocalAdapter adapter in inventory.Adapters)
        {
            if (!adapter.IPv4Addresses.Any(held => held.Equals(address)))
            {
                continue;
            }

            if (!adapter.IsUp)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' holds {address} but is not up.");
            }

            if (!adapter.SupportsMulticast)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' ({address}) does not support multicast, "
                    + "so mDNS cannot work on it.");
            }

            if (adapter.Index is not { } index)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' ({address}) has no usable IPv4 configuration.");
            }

            return new MdnsInterface(adapter.Name, address, index, AddressFamily.InterNetwork);
        }

        throw new MdnsInterfaceException(
            $"{address} is not an address on any interface of this machine. "
            + "On Windows, list local addresses with: Get-NetIPAddress -AddressFamily IPv4");
    }

    /// <summary>Resolves an adapter name using the machine's real adapters.</summary>
    public static MdnsInterface ResolveByName(string name) =>
        ResolveByName(name, SystemInterfaceInventory.Instance);

    /// <summary>
    /// Resolves a friendly adapter name, as written in configuration, to that
    /// adapter's address and index.
    /// </summary>
    /// <remarks>
    /// Configuration names interfaces the way the operator sees them in Windows
    /// - "Ethernet 2", "Wi-Fi" - rather than by address, because addresses on a
    /// DHCP network change and a configuration file that goes stale overnight is
    /// worse than useless. The resolution is returned so it can be logged.
    /// </remarks>
    /// <exception cref="MdnsInterfaceException">
    /// No adapter has that name, or the adapter holds no usable IPv4 address, or
    /// it holds more than one and the choice would be arbitrary.
    /// </exception>
    [Requirement("REQ-CFG-004",
        "Resolves a configured interface name to a concrete address and index at startup, returning both so the resolution can be logged.")]
    public static MdnsInterface ResolveByName(string name, IInterfaceInventory inventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(inventory);

        var known = new List<string>();

        foreach (LocalAdapter adapter in inventory.Adapters)
        {
            known.Add(adapter.Name);

            if (!string.Equals(adapter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (adapter.IPv4Addresses.Count == 0)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{name}' has no IPv4 address, so it cannot be used.");
            }

            if (adapter.IPv4Addresses.Count > 1)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{name}' holds several IPv4 addresses "
                    + $"({string.Join(", ", adapter.IPv4Addresses)}). Naming it would leave the choice to "
                    + "chance, so configure the address explicitly instead.");
            }

            return Resolve(adapter.IPv4Addresses[0], inventory);
        }

        throw new MdnsInterfaceException(
            $"No interface is named '{name}'. Interfaces on this machine: {string.Join(", ", known)}");
    }

    /// <summary>Resolves the IPv6 companion using the machine's real adapters.</summary>
    public static MdnsInterface ResolveIPv6(MdnsInterface ipv4Interface) =>
        ResolveIPv6(ipv4Interface, SystemInterfaceInventory.Instance);

    /// <summary>
    /// Produces the IPv6 entry for the adapter an IPv4 entry already names: same
    /// adapter, same advertised address, the adapter's IPv6 index, IPv6
    /// transport.
    /// </summary>
    /// <remarks>
    /// The IPv4 entry is the input rather than a bare address because
    /// configuration names interfaces by IPv4 address and nothing else. There is
    /// no separate IPv6 address to configure, and there should not be: an
    /// operator who has already said which adapter faces the client network has
    /// said everything the service needs. Asking again in another family would
    /// be a second chance to get it wrong.
    ///
    /// The returned entry keeps the IPv4 address. See the remarks on
    /// <see cref="MdnsInterface.Address"/> for why.
    /// </remarks>
    /// <exception cref="MdnsInterfaceException">
    /// The adapter is gone, is down, carries no multicast, or has no IPv6
    /// configuration. The last is the interesting one and is reported as such:
    /// an operator whose client interface has IPv6 disabled needs to be told
    /// that specifically, because the service will refuse to start and the
    /// reason is a single checkbox on the adapter.
    /// </exception>
    public static MdnsInterface ResolveIPv6(MdnsInterface ipv4Interface, IInterfaceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(ipv4Interface);
        ArgumentNullException.ThrowIfNull(inventory);

        if (ipv4Interface.Transport != AddressFamily.InterNetwork)
        {
            throw new MdnsInterfaceException(
                $"{ipv4Interface} is not an IPv4 entry, so it has no IPv6 companion to resolve.");
        }

        foreach (LocalAdapter adapter in inventory.Adapters)
        {
            if (!adapter.IPv4Addresses.Any(held => held.Equals(ipv4Interface.Address)))
            {
                continue;
            }

            if (!adapter.IsUp)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' holds {ipv4Interface.Address} but is not up.");
            }

            if (!adapter.SupportsMulticast)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' ({ipv4Interface.Address}) does not support multicast, "
                    + "so mDNS cannot work on it.");
            }

            if (adapter.IPv6Index is not { } ipv6Index)
            {
                throw new MdnsInterfaceException(
                    $"Interface '{adapter.Name}' ({ipv4Interface.Address}) has no IPv6 configuration, "
                    + "so it cannot receive the mDNS queries iOS sends. iOS was measured querying over "
                    + "IPv6 only, so a client interface without it is never discovered. Enable IPv6 on "
                    + "the adapter, or configure a different client interface. On Windows, check with: "
                    + "Get-NetAdapterBinding -Name '" + adapter.Name + "' -ComponentID ms_tcpip6");
            }

            return new MdnsInterface(
                adapter.Name, ipv4Interface.Address, ipv6Index, AddressFamily.InterNetworkV6);
        }

        throw new MdnsInterfaceException(
            $"{ipv4Interface.Address} is no longer an address on any interface of this machine.");
    }

    /// <summary>Resolves several addresses using the machine's real adapters.</summary>
    public static IReadOnlyList<MdnsInterface> ResolveAll(IReadOnlyList<IPAddress> addresses) =>
        ResolveAll(addresses, SystemInterfaceInventory.Instance);

    /// <summary>
    /// Resolves several addresses, rejecting duplicates and rejecting two
    /// addresses that land on the same interface.
    /// </summary>
    /// <remarks>
    /// Two configured roles on one interface would mean the service could not
    /// tell a client query from a printer query, since both would arrive with
    /// the same index. Refusing to start is the only safe response.
    /// </remarks>
    [Requirement("REQ-CFG-005",
        "Resolves every interface before any is used; one failure aborts the whole set rather than yielding a partial configuration.")]
    [Requirement("REQ-CFG-006",
        "Refuses two configured addresses that resolve to the same interface, since arriving datagrams could not then be attributed to a role.")]
    public static IReadOnlyList<MdnsInterface> ResolveAll(
        IReadOnlyList<IPAddress> addresses, IInterfaceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(inventory);

        if (addresses.Count == 0)
        {
            throw new MdnsInterfaceException("No interfaces were configured. At least one is required.");
        }

        var resolved = new List<MdnsInterface>(addresses.Count);
        var seenAddresses = new HashSet<string>(StringComparer.Ordinal);
        var seenIndexes = new Dictionary<int, MdnsInterface>();

        foreach (IPAddress address in addresses)
        {
            if (!seenAddresses.Add(address.ToString()))
            {
                throw new MdnsInterfaceException($"{address} was configured more than once.");
            }

            MdnsInterface resolvedInterface = Resolve(address, inventory);

            if (seenIndexes.TryGetValue(resolvedInterface.Index, out MdnsInterface? existing))
            {
                throw new MdnsInterfaceException(
                    $"{address} and {existing.Address} are both on interface '{resolvedInterface.Name}' "
                    + $"(index {resolvedInterface.Index}). Arriving datagrams could not be attributed to one "
                    + "role or the other, so the service will not start.");
            }

            seenIndexes[resolvedInterface.Index] = resolvedInterface;
            resolved.Add(resolvedInterface);
        }

        return resolved;
    }
}

// -----------------------------------------------------------------------------
// MdnsInterface.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Turns an IPv4 address from configuration into a fully identified network
//   interface: its friendly name and its operating-system index.
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

/// <summary>A resolved local interface: what it is called, its address, its index.</summary>
/// <param name="Name">Friendly adapter name, e.g. "Ethernet 2".</param>
/// <param name="Address">The IPv4 address the service was configured with.</param>
/// <param name="Index">Operating-system IPv4 interface index, as reported by IP_PKTINFO.</param>
public sealed record MdnsInterface(string Name, IPAddress Address, int Index)
{
    public override string ToString() => $"{Name} ({Address}, index {Index})";
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

            return new MdnsInterface(adapter.Name, address, index);
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

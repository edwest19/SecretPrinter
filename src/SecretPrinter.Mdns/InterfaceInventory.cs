// -----------------------------------------------------------------------------
// InterfaceInventory.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Purpose:
//   Describes the machine's network adapters, behind an interface, so the rules
//   in MdnsInterfaceResolver can be tested against any adapter arrangement
//   rather than only the one the test machine happens to have.
//
// Why this exists:
//   REQ-CFG-006 forbids two configured addresses landing on one interface. Its
//   test needs an adapter holding two IPv4 addresses. Neither the development
//   container nor the target machine has one, so the test skipped on both and
//   the requirement sat unverified while still carrying a marker - the exact
//   failure the results-file mechanism was added to expose.
//
//   Reading adapters through a seam turns that from a hardware problem into an
//   ordinary test. The production implementation still reads the real machine;
//   nothing about the service's behaviour changes.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SecretPrinter.Mdns;

/// <summary>One local network adapter, reduced to what interface resolution needs.</summary>
/// <param name="Name">Friendly adapter name, e.g. "Ethernet 2".</param>
/// <param name="Index">Operating-system IPv4 interface index, or null when it has none.</param>
/// <param name="IsUp">Whether the adapter is operationally up.</param>
/// <param name="SupportsMulticast">Whether the adapter can carry multicast.</param>
/// <param name="IPv4Addresses">IPv4 unicast addresses held by this adapter.</param>
/// <param name="IPv6Index">
/// Operating-system IPv6 interface index, or null when the adapter has no IPv6
/// configuration. Windows numbers the two families separately, so this is not
/// the same value as <paramref name="Index"/> for the same adapter.
///
/// It defaults to null - meaning "this adapter cannot carry IPv6" - so that a
/// test describing an adapter for some unrelated reason need not state an IPv6
/// identity it does not care about. The default is the restrictive case: an
/// adapter is never treated as IPv6-capable unless something said so.
/// </param>
public sealed record LocalAdapter(
    string Name,
    int? Index,
    bool IsUp,
    bool SupportsMulticast,
    IReadOnlyList<IPAddress> IPv4Addresses,
    int? IPv6Index = null);

/// <summary>Supplies the set of local adapters.</summary>
public interface IInterfaceInventory
{
    IReadOnlyList<LocalAdapter> Adapters { get; }
}

/// <summary>Reads the adapters actually present on this machine.</summary>
public sealed class SystemInterfaceInventory : IInterfaceInventory
{
    /// <summary>The inventory used unless a caller supplies another.</summary>
    public static SystemInterfaceInventory Instance { get; } = new();

    public IReadOnlyList<LocalAdapter> Adapters
    {
        get
        {
            var adapters = new List<LocalAdapter>();

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();

                int? index = null;
                try
                {
                    index = properties.GetIPv4Properties()?.Index;
                }
                catch (NetworkInformationException)
                {
                    // Adapter has no IPv4 configuration. Reported as a null
                    // index rather than dropped, so the resolver can explain
                    // precisely why an address on it cannot be used.
                }

                int? ipv6Index = null;
                try
                {
                    ipv6Index = properties.GetIPv6Properties()?.Index;
                }
                catch (NetworkInformationException)
                {
                    // Adapter has no IPv6 configuration - IPv6 disabled on the
                    // adapter, most often. Reported as null for the same reason
                    // as the IPv4 index above: the resolver can then say which
                    // family is missing rather than failing vaguely.
                }

                var addresses = new List<IPAddress>();
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(unicast.Address);
                    }
                }

                adapters.Add(new LocalAdapter(
                    adapter.Name,
                    index,
                    adapter.OperationalStatus == OperationalStatus.Up,
                    adapter.SupportsMulticast,
                    addresses,
                    ipv6Index));
            }

            return adapters;
        }
    }
}

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
// Per-family index claim corrected by Claude (Anthropic model, Claude Opus 5)
// at the direction of Edwin West, 2026-09-13. Comment only; no behaviour
// changed. Reviewed by a human before merge.
//
// IPv4 address condition added by Claude (Anthropic model, Claude Opus 5) at
// the direction of Edwin West, 2026-09-19. Reviewed by a human before merge.
// An adapter can be up and still hold an address the stack will not use, which
// is the state that produced the false failure message recorded in
// docs/findings/2026-09-19-the-printer-side-multicast-membership-is-lost.md.
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

/// <summary>Condition of one local IPv4 address, as the platform reports it.</summary>
/// <remarks>
/// A deliberate narrowing of <see cref="DuplicateAddressDetectionState"/>. The
/// platform's own enum is not carried out of here, so that nothing downstream
/// has to decide what an unfamiliar member means, and so that a platform which
/// declines to report the state has somewhere honest to land.
/// </remarks>
public enum LocalAddressCondition
{
    /// <summary>The platform did not report a condition. Nothing may be concluded from this.</summary>
    Unknown = 0,

    /// <summary>Usable as a source address.</summary>
    Preferred,

    /// <summary>Still held, but the stack will not choose it. Sends do not fail; replies do not arrive.</summary>
    Deprecated,

    /// <summary>Not yet usable; duplicate address detection has not finished.</summary>
    Tentative,

    /// <summary>Expired or found to be a duplicate.</summary>
    Invalid,
}

/// <summary>One IPv4 address held by an adapter, with the condition the platform reports for it.</summary>
public sealed record LocalIPv4Address(IPAddress Address, LocalAddressCondition Condition);

/// <summary>One local network adapter, reduced to what interface resolution needs.</summary>
/// <param name="Name">Friendly adapter name, e.g. "Ethernet 2".</param>
/// <param name="Index">Operating-system IPv4 interface index, or null when it has none.</param>
/// <param name="IsUp">Whether the adapter is operationally up.</param>
/// <param name="SupportsMulticast">Whether the adapter can carry multicast.</param>
/// <param name="IPv4Addresses">IPv4 unicast addresses held by this adapter.</param>
/// <param name="IPv6Index">
/// Operating-system IPv6 interface index, or null when the adapter has no IPv6
/// configuration. Read separately from <paramref name="Index"/> because the
/// platform exposes an index per address family and promises no relationship
/// between them - not because the two are known to differ. On every adapter
/// measured by this project they were equal; that is two machines, not a
/// guarantee, and neither equality nor difference may be assumed. See
/// docs/findings/2026-09-13-interface-index-parity.md.
///
/// It defaults to null - meaning "this adapter cannot carry IPv6" - so that a
/// test describing an adapter for some unrelated reason need not state an IPv6
/// identity it does not care about. The default is the restrictive case: an
/// adapter is never treated as IPv6-capable unless something said so.
/// </param>
/// <param name="IPv4AddressConditions">
/// The same addresses as <paramref name="IPv4Addresses"/>, each with the
/// condition the platform reports for it. Added alongside rather than folded
/// into <paramref name="IPv4Addresses"/> because every existing caller and test
/// treats that member as the list of addresses held, and this step is not the
/// place to move them all.
///
/// It defaults to null, meaning the conditions were not read. That is the
/// restrictive case: a caller must treat an absent list as "not known", never
/// as "all preferred". A test that does not care about conditions can leave it
/// out and will get no opinion from it.
/// </param>
public sealed record LocalAdapter(
    string Name,
    int? Index,
    bool IsUp,
    bool SupportsMulticast,
    IReadOnlyList<IPAddress> IPv4Addresses,
    int? IPv6Index = null,
    IReadOnlyList<LocalIPv4Address>? IPv4AddressConditions = null);

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
                var conditions = new List<LocalIPv4Address>();
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    addresses.Add(unicast.Address);
                    conditions.Add(new LocalIPv4Address(unicast.Address, ConditionOf(unicast)));
                }

                adapters.Add(new LocalAdapter(
                    adapter.Name,
                    index,
                    adapter.OperationalStatus == OperationalStatus.Up,
                    adapter.SupportsMulticast,
                    addresses,
                    ipv6Index,
                    conditions));
            }

            return adapters;
        }
    }

    /// <summary>
    /// Narrows the platform's duplicate-address-detection state to what this
    /// project acts on. An unrecognised member, or a platform that declines to
    /// report at all, becomes <see cref="LocalAddressCondition.Unknown"/> so
    /// that no caller can mistake silence for health.
    /// </summary>
    private static LocalAddressCondition ConditionOf(UnicastIPAddressInformation unicast)
    {
        // CA1416: DuplicateAddressDetectionState is Windows-only. The guard is
        // the honest answer as well as the analyzer's: on a platform that does
        // not report it, we do not know, and Unknown is what "we do not know"
        // is called here.
        if (!OperatingSystem.IsWindows())
        {
            return LocalAddressCondition.Unknown;
        }

        try
        {
            return unicast.DuplicateAddressDetectionState switch
            {
                DuplicateAddressDetectionState.Preferred => LocalAddressCondition.Preferred,
                DuplicateAddressDetectionState.Deprecated => LocalAddressCondition.Deprecated,
                DuplicateAddressDetectionState.Tentative => LocalAddressCondition.Tentative,
                DuplicateAddressDetectionState.Invalid => LocalAddressCondition.Invalid,
                DuplicateAddressDetectionState.Duplicate => LocalAddressCondition.Invalid,
                _ => LocalAddressCondition.Unknown,
            };
        }
        catch (PlatformNotSupportedException)
        {
            // Not every platform reports this. Windows does, which is the only
            // platform this service runs on, but the caller is told "unknown"
            // rather than given a guess.
            return LocalAddressCondition.Unknown;
        }
    }
}

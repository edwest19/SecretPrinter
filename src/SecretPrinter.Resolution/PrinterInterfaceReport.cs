// -----------------------------------------------------------------------------
// PrinterInterfaceReport.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-19. Reviewed by a human before
// merge.
//
// Purpose:
//   Answers one question, at the moment a lookup has timed out: is the
//   interface we sent the query from still in a state where an answer could
//   have arrived? The resolver uses the answer to decide what it is entitled to
//   say.
//
// Why this exists:
//   The resolver used to respond to every timeout with "The printer may be
//   asleep, off, or on a different network." On 2026-09-18 that sentence was
//   printed twenty-one times while the printer was awake and on the right
//   network; the adapter had left the network and kept its address. On
//   2026-09-19 it was printed seventeen more times while the printer answered a
//   probe from the same machine on the same interface seconds-to-hours either
//   side. See docs/findings/2026-09-18-the-printer-side-interface-goes-away.md
//   and docs/findings/2026-09-19-the-printer-side-multicast-membership-is-lost.md.
//
//   A timeout is the absence of evidence. It cannot, by itself, support a claim
//   about a remote device. What it can support is a claim about this machine,
//   because this machine can be examined. So that is what gets examined, and
//   anything not examined is not asserted.
//
// What this deliberately does NOT check:
//   Whether the socket is still a member of 224.0.0.251 on that interface -
//   which is the fault actually measured on 2026-09-19. The platform reports
//   the groups an interface has joined, but not how many sockets hold each
//   join, so with another process on the machine holding its own membership of
//   the same group the interface still lists it and the check would pass while
//   the service is deaf. A check that reads correct and answers wrongly is
//   worse than no check, so it is not written here. Repairing the membership,
//   rather than detecting its loss, is the next piece of work, and it does not
//   depend on this file.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Mdns;

namespace SecretPrinter.Resolution;

/// <summary>What was found when the printer-side interface was examined.</summary>
public enum PrinterInterfaceCondition
{
    /// <summary>
    /// Nothing wrong was found locally. This is not a statement that the
    /// interface works - only that the things checked were in order.
    /// </summary>
    NothingWrongFound = 0,

    /// <summary>No adapter on this machine carries the index that was bound.</summary>
    NotFound,

    /// <summary>The adapter is present but not operationally up.</summary>
    NotUp,

    /// <summary>The adapter is up but no longer holds the address that was bound.</summary>
    AddressGone,

    /// <summary>The adapter holds the address, but the stack will not use it.</summary>
    AddressUnusable,
}

/// <summary>The outcome of examining the printer-side interface.</summary>
/// <param name="Condition">What was found.</param>
/// <param name="Description">
/// One sentence naming what was found, written to be embedded in a failure
/// message an operator will read in a log.
/// </param>
public sealed record PrinterInterfaceReport(PrinterInterfaceCondition Condition, string Description)
{
    /// <summary>True when the local side accounts for the failure on its own.</summary>
    public bool LocalFaultFound => Condition != PrinterInterfaceCondition.NothingWrongFound;
}

/// <summary>Examines the interface a lookup was sent from.</summary>
public static class PrinterInterfaceInspector
{
    /// <summary>
    /// Reports on the interface as it stands now. Reads only; changes nothing,
    /// and never throws for an ordinary absence - a missing adapter is an
    /// answer, not an error.
    /// </summary>
    /// <param name="printerInterface">The interface the query was sent from.</param>
    /// <param name="inventory">
    /// Where the adapters are read from. Supplied so that every state below can
    /// be tested on a machine that has none of them.
    /// </param>
    public static PrinterInterfaceReport Inspect(
        MdnsInterface printerInterface, IInterfaceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(printerInterface);
        ArgumentNullException.ThrowIfNull(inventory);

        // The index is read per address family, and the two families promise no
        // relationship. See docs/findings/2026-09-13-interface-index-parity.md.
        LocalAdapter? adapter = null;
        foreach (LocalAdapter candidate in inventory.Adapters)
        {
            int? index = printerInterface.IsIPv6 ? candidate.IPv6Index : candidate.Index;
            if (index == printerInterface.Index)
            {
                adapter = candidate;
                break;
            }
        }

        if (adapter is null)
        {
            return new PrinterInterfaceReport(
                PrinterInterfaceCondition.NotFound,
                $"no adapter on this machine now has index {printerInterface.Index}, "
                + $"so the query had nowhere to leave from.");
        }

        if (!adapter.IsUp)
        {
            return new PrinterInterfaceReport(
                PrinterInterfaceCondition.NotUp,
                $"adapter '{adapter.Name}' is not up.");
        }

        if (!HoldsAddress(adapter, printerInterface.Address))
        {
            return new PrinterInterfaceReport(
                PrinterInterfaceCondition.AddressGone,
                $"adapter '{adapter.Name}' is up but no longer holds {printerInterface.Address}.");
        }

        LocalAddressCondition condition = ConditionOf(adapter, printerInterface.Address);

        // Deprecated is the state measured on 2026-09-18: the address is still
        // held, so sending does not fail, but it is no longer a source address
        // the stack will choose, and the answer has nowhere to come back to.
        if (condition is LocalAddressCondition.Deprecated
            or LocalAddressCondition.Tentative
            or LocalAddressCondition.Invalid)
        {
            return new PrinterInterfaceReport(
                PrinterInterfaceCondition.AddressUnusable,
                $"adapter '{adapter.Name}' is up and still holds {printerInterface.Address}, "
                + $"but the address is {condition.ToString().ToLowerInvariant()}, so a reply had no "
                + "usable path back.");
        }

        string qualifier = condition == LocalAddressCondition.Preferred
            ? "and the address is preferred"
            : "and the platform did not report the address condition";

        return new PrinterInterfaceReport(
            PrinterInterfaceCondition.NothingWrongFound,
            $"adapter '{adapter.Name}' is up, holds {printerInterface.Address}, {qualifier}.");
    }

    private static bool HoldsAddress(LocalAdapter adapter, IPAddress address)
    {
        foreach (IPAddress held in adapter.IPv4Addresses)
        {
            if (held.Equals(address))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The condition reported for one address, or <see
    /// cref="LocalAddressCondition.Unknown"/> when the inventory did not report
    /// conditions at all. Absence is never read as health.
    /// </summary>
    private static LocalAddressCondition ConditionOf(LocalAdapter adapter, IPAddress address)
    {
        if (adapter.IPv4AddressConditions is not { } conditions)
        {
            return LocalAddressCondition.Unknown;
        }

        foreach (LocalIPv4Address held in conditions)
        {
            if (held.Address.Equals(address))
            {
                return held.Condition;
            }
        }

        return LocalAddressCondition.Unknown;
    }
}

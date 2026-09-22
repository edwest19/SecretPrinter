// -----------------------------------------------------------------------------
// StartupWait.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-22, for REQ-LIF-008.
// Reviewed by a human before merge.
//
// Purpose:
//   The two things startup waits for before it offers anything to the client
//   network: a printer-side interface it can use, and a printer that answers.
//   Until both are there the service runs, withdrawn: it advertises nothing,
//   answers no query and accepts no connection, which is what REQ-LIF-006
//   already does when the printer goes away after startup.
//
// Why startup waits rather than refusing:
//   Until 2026-09-22 startup refused a printer-side adapter that was not up,
//   and a printer that did not answer. On FIOS-STB-01 the printer side is a
//   WLAN that drops and is not reconnected by Windows
//   (docs/findings/2026-09-20-the-wlan-drops-and-nothing-retries.md), so a
//   reboot during an outage left the service stopped, and nine such refusals
//   are recorded in
//   docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md.
//   Edwin decided on 2026-09-22 that the service should start withdrawn and
//   come up by itself when the adapter returns, and that a printer which does
//   not answer at startup should be waited for in the same way.
//
// What the wait costs, stated so it is not hidden:
//   A misspelled printerInstance or printerIppsInstance now waits instead of
//   failing, because a printer that is switched off and a name that matches
//   nothing look the same from here: no answer. The warning logged on the first
//   unanswered attempt says so. A misspelled printerInterface is still refused
//   when the configuration is loaded, because the adapter must exist by name
//   then (ConfigurationLoader).
//
// How each wait behaves:
//   The adapter is examined locally every AdapterCheckInterval. Examining it
//   reads Windows' own view of the adapter and sends nothing on any network.
//   The adapter counts as usable when MdnsInterfaceResolver accepts it (up,
//   multicast-capable, exactly one IPv4 address), the address is not in
//   169.254.0.0/16, and the address is not reported tentative, deprecated or
//   invalid. The link-local exclusion is a judgement: Windows assigns such an
//   address while no DHCP server has answered, and binding to it would leave
//   the service asking for the printer from an address the printer network
//   never gave out. A printer network that really is link-local only would
//   therefore wait forever, with the reason in the log.
//
//   The printer is asked on the continuous-querying schedule REQ-RES-008 uses
//   while the printer is unreachable: one second, then each wait double the
//   last, capped at PrinterReachability.MaximumRetryInterval (60 minutes). The
//   schedule's two constants are read from PrinterReachability rather than
//   restated, so the two cannot drift apart.
//
//   Both waits log when they begin and when they end, and nothing in between
//   unless the reason for waiting changes. The 2026-09-20 outage lasted nine
//   hours; a line per check would have buried it.
//
// What this does not handle:
//   Once the printer-side interface has been resolved, its address is fixed
//   for the life of the process, as it was before this file existed. If the
//   adapter goes away while the printer is being waited for, or later, and
//   returns with a different address, the service does not rebind. That is
//   part of docs/findings/2026-09-18-the-printer-side-interface-goes-away.md,
//   which stays open.
// -----------------------------------------------------------------------------

using System.Net.Sockets;
using SecretPrinter.Mdns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>
/// Waits, before anything is offered, for a usable printer-side interface and
/// for the printer to answer.
/// </summary>
public static class StartupWait
{
    /// <summary>How often a printer-side adapter that is not usable is examined again.</summary>
    public static readonly TimeSpan AdapterCheckInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Returns the printer-side interface once it is usable, examining it every
    /// <see cref="AdapterCheckInterval"/> until then.
    /// </summary>
    /// <param name="adapterName">The adapter named in configuration.</param>
    /// <param name="inventory">Where the adapter's state is read.</param>
    /// <param name="log">Where the wait, its reasons and the resolution are recorded.</param>
    /// <param name="delay">How the loop waits; supplied so tests need not sleep.</param>
    /// <param name="cancellationToken">Ends the wait; the service is stopping.</param>
    [Requirement("REQ-LIF-008",
        "Waits for the printer-side interface instead of refusing to start, examining it locally on an interval, logging when the wait begins, when its reason changes and when it ends, and returning the interface only once it is usable.")]
    [Requirement("REQ-OBS-001",
        "Logs the printer-side interface's configured name, the address and index it resolved to, and its role, once it has been resolved.")]
    public static async Task<MdnsInterface> ForPrinterInterfaceAsync(
        string adapterName,
        IInterfaceInventory inventory,
        IServiceLog log,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(delay);

        string? reported = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (MdnsInterface? usable, string? reason) = Examine(adapterName, inventory);

            if (usable is not null)
            {
                if (reported is not null)
                {
                    log.Info($"The printer-side interface '{adapterName}' is usable. No longer waiting for it.");
                }

                log.Info($"  {DescribePrinterInterface(usable)}");
                return usable;
            }

            if (!string.Equals(reason, reported, StringComparison.Ordinal))
            {
                log.Warn(
                    $"Waiting for the printer-side interface: {reason} Nothing is offered to the client "
                    + "network until it is usable and the printer answers. Checking it again every "
                    + $"{AdapterCheckInterval.TotalSeconds:0} s; this is logged again only if the reason changes.");
                reported = reason;
            }

            await delay(AdapterCheckInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether the named adapter can be used as the printer-side interface now,
    /// and if not, why not.
    /// </summary>
    /// <returns>
    /// The resolved interface and no reason, or no interface and the reason.
    /// </returns>
    [Requirement("REQ-LIF-008",
        "Decides whether the printer-side adapter is usable: resolvable by name, holding an address outside 169.254.0.0/16, and that address not reported tentative, deprecated or invalid.")]
    public static (MdnsInterface? Usable, string? Reason) Examine(string adapterName, IInterfaceInventory inventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        ArgumentNullException.ThrowIfNull(inventory);

        MdnsInterface resolved;
        try
        {
            resolved = MdnsInterfaceResolver.ResolveByName(adapterName, inventory);
        }
        catch (MdnsInterfaceException ex)
        {
            return (null, ex.Message);
        }

        byte[] octets = resolved.Address.GetAddressBytes();
        if (octets[0] == 169 && octets[1] == 254)
        {
            return (null,
                $"Interface '{resolved.Name}' holds only the link-local address {resolved.Address}. Windows "
                + "assigns an address in 169.254.0.0/16 when no DHCP server has answered, so this machine "
                + "does not yet have an address on the printer network.");
        }

        LocalAdapter? adapter = inventory.Adapters.FirstOrDefault(
            a => string.Equals(a.Name, resolved.Name, StringComparison.OrdinalIgnoreCase));

        // Conditions are optional: a null list means the platform did not say,
        // which is not the same as "preferred". Only a condition that was
        // reported, and is one of the unusable ones, holds the wait.
        LocalIPv4Address? held = adapter?.IPv4AddressConditions?.FirstOrDefault(
            c => c.Address.Equals(resolved.Address));

        if (held is not null
            && held.Condition is LocalAddressCondition.Tentative
                or LocalAddressCondition.Deprecated
                or LocalAddressCondition.Invalid)
        {
            return (null,
                $"Interface '{resolved.Name}' holds {resolved.Address}, but the address is "
                + $"{held.Condition.ToString().ToLowerInvariant()}.");
        }

        return (resolved, null);
    }

    /// <summary>
    /// Returns what <paramref name="resolve"/> finds once the printer answers,
    /// asking again on the REQ-RES-008 continuous-querying schedule until then.
    /// </summary>
    /// <param name="resolve">One attempt to resolve both of the printer's services.</param>
    /// <param name="log">Where the wait is recorded.</param>
    /// <param name="delay">How the loop waits; supplied so tests need not sleep.</param>
    /// <param name="cancellationToken">Ends the wait; the service is stopping.</param>
    [Requirement("REQ-LIF-008",
        "Waits for the printer to answer instead of refusing to start, asking again after one second and then after each wait doubled, capped at PrinterReachability.MaximumRetryInterval, and logging the first unanswered attempt and the answer.")]
    [Requirement("REQ-RES-007",
        "Keeps asking until both instances resolve, so nothing is advertised before they have, and logs the first failure with the instance that did not answer.")]
    public static async Task<PrinterAtStartup> ForPrinterAsync(
        Func<CancellationToken, Task<PrinterAtStartup>> resolve,
        IServiceLog log,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(delay);

        TimeSpan wait = PrinterReachability.FirstRetryInterval;
        int unanswered = 0;

        while (true)
        {
            try
            {
                PrinterAtStartup found = await resolve(cancellationToken).ConfigureAwait(false);

                if (unanswered > 0)
                {
                    log.Info($"The printer answered after {unanswered} unanswered attempt(s). No longer waiting for it.");
                }

                return found;
            }
            catch (Exception ex) when (ex is PrinterResolutionException or SocketException)
            {
                // SocketException as well as the resolver's own failure: a send
                // can fail before the printer is asked at all, as PrinterWatch
                // records (measured on FIOS-STB-01 on 2026-09-20). Either way the
                // printer has not answered, and nothing more is claimed.
                unanswered++;

                if (unanswered == 1)
                {
                    log.Warn(
                        $"The printer did not answer at startup: {ex.Message} Nothing is offered to the client "
                        + $"network until it does. Asking again after {wait.TotalSeconds:0} s, each wait double "
                        + "the last, up to once an hour; this is logged again only when it answers. A misspelled "
                        + "printerInstance or printerIppsInstance looks exactly like this.");
                }
            }

            await delay(wait, cancellationToken).ConfigureAwait(false);

            wait += wait;
            if (wait > PrinterReachability.MaximumRetryInterval)
            {
                wait = PrinterReachability.MaximumRetryInterval;
            }
        }
    }

    /// <summary>The startup line recording how the printer-side interface resolved.</summary>
    public static string DescribePrinterInterface(MdnsInterface printerInterface)
    {
        ArgumentNullException.ThrowIfNull(printerInterface);

        return $"printer interface: {printerInterface.Name} -> {printerInterface.Address} "
               + $"(index {printerInterface.Index})";
    }
}

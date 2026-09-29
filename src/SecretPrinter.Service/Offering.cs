// -----------------------------------------------------------------------------
// Offering.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-28, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Every decision about whether the printer is offered to the client network,
//   in one place, and in a form that can be tested without a socket:
//
//   - ClaimAndOfferAsync: probe for the advertised names first, and only if
//     no other device answers, open the gate and announce (REQ-ADV-023). Used
//     at startup and before every restore.
//   - OnReachabilityChangedAsync: what the watch calls when the printer comes
//     and goes (REQ-LIF-006). Coming back goes through ClaimAndOfferAsync;
//     going away is what ServiceHost.OfferAsync did until today, moved here
//     unchanged.
//   - WithdrawOnConflictAsync: on the first name conflict, close the gate, say
//     so in the log, and send goodbyes. Nothing reopens it: ClaimAndOfferAsync
//     refuses while a conflict is held, so the printer stays withdrawn until
//     the service is restarted (REQ-ADV-024; Edwin's decision of 2026-09-28,
//     rather than choosing a new name as RFC 6762 s9 suggests).
//
// What is not here:
//   Deciding what counts as a conflict, which is the responder's
//   (MdnsResponder.Probing.cs), and the order of startup, which is
//   ServiceHost.RunAsync's. RunAsync starts the receive loop before the
//   startup probe, because the answers to a probe arrive through it.
// -----------------------------------------------------------------------------

using System.Net.Sockets;
using SecretPrinter.Responder;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>When the printer is offered to the client network, and when not.</summary>
public static class Offering
{
    /// <summary>The gap between announcements (RFC 6762 s8.3).</summary>
    public static readonly TimeSpan AnnouncementInterval = TimeSpan.FromSeconds(1);

    /// <summary>How long goodbyes may take before they are given up on.</summary>
    public static readonly TimeSpan GoodbyeWindow = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Probes for the advertised names and, only if no other device answers,
    /// opens the gate and announces. Returns whether the printer is now offered.
    /// </summary>
    /// <remarks>
    /// The gate stays closed for the whole probe, so nothing is accepted under
    /// names not yet claimed; the responder is silent while probing as well.
    /// A conflict found by the probe is logged by
    /// <see cref="WithdrawOnConflictAsync"/>, which hears of it from the
    /// responder, so it is logged once, wherever it was found.
    /// Socket failures propagate: at startup they stop the service, as a failed
    /// announcement always has; from the watch, OnReachabilityChangedAsync
    /// catches and logs them.
    /// </remarks>
    /// <param name="delay">The probe's waits; null for real time.</param>
    [Requirement("REQ-ADV-023",
        "Probes before every offer - at startup and before each restore - and opens the gate and announces only when the probe comes back clear. The gate stays closed throughout the probe.")]
    [Requirement("REQ-ADV-024",
        "Offers nothing while a conflict is held, and opens the gate only through AvailabilityGate.OpenUnless, so a conflict heard between the probe and the opening cannot leave the gate open.")]
    public static async Task<bool> ClaimAndOfferAsync(
        MdnsResponder responder,
        AvailabilityGate offering,
        IServiceLog log,
        Func<TimeSpan, CancellationToken, Task>? delay,
        TimeSpan announcementInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(offering);
        ArgumentNullException.ThrowIfNull(log);

        if (responder.Conflict is not null)
        {
            log.Warn("The printer is answering, but it is not offered: another device uses a name this "
                     + "service advertises (see the name conflict above). It stays withdrawn until the service "
                     + "is restarted, with a different name in the configuration.");
            return false;
        }

        log.Info("Probing for the advertised names before offering them (RFC 6762 s8.1).");

        ProbeResult result = await responder.ProbeAsync(delay, cancellationToken).ConfigureAwait(false);
        if (!result.IsClear)
        {
            return false;
        }

        if (!offering.OpenUnless(() => responder.Conflict is not null))
        {
            return false;
        }

        log.Info("Probe clear: no other device answered for the advertised names.");
        await responder.AnnounceAsync(announcementInterval, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Starts or stops offering the printer as the watch reports it reachable or
    /// not, in the order that never leaves an advertised service with nothing
    /// listening behind it.
    /// </summary>
    /// <param name="delay">The probe's waits; null for real time.</param>
    [Requirement("REQ-LIF-006",
        "On loss: closes the gate first, then sends goodbye records, so nothing is accepted that cannot "
        + "be served and clients drop the entry promptly. On recovery: probes, then opens the gate, then "
        + "announces, so the printer is listening before it is offered.")]
    public static async Task OnReachabilityChangedAsync(
        MdnsResponder responder,
        AvailabilityGate offering,
        IServiceLog log,
        bool reachable,
        Func<TimeSpan, CancellationToken, Task>? delay,
        TimeSpan announcementInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(offering);
        ArgumentNullException.ThrowIfNull(log);

        try
        {
            if (reachable)
            {
                if (await ClaimAndOfferAsync(responder, offering, log, delay, announcementInterval, cancellationToken)
                        .ConfigureAwait(false))
                {
                    log.Info("Advertisement restored: the printer is on offer again.");
                }

                return;
            }

            offering.Close();

            using var goodbyeWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            goodbyeWindow.CancelAfter(GoodbyeWindow);

            await responder.SendGoodbyeAsync(goodbyeWindow.Token).ConfigureAwait(false);
            log.Warn("Advertisement withdrawn and the listener closed: "
                     + "the printer is no longer offered to the client network.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException)
        {
            // On loss the gate has already closed, which is the part that
            // matters. On recovery nothing has opened: the names were never
            // claimed, so nothing is offered, and the watch will not ask again
            // until the printer is lost and found once more. Said plainly.
            log.Warn(reachable
                ? $"The printer answers again, but it could not be offered: {ex.Message}. Nothing is offered. "
                  + "The watch tries again only if the printer is lost and found again; restarting the "
                  + "service tries at once."
                : $"The advertisement could not be withdrawn on the network: {ex.Message}. The gate is closed "
                  + "regardless, so nothing is accepted; clients fall back on the record TTL.");
        }
    }

    /// <summary>
    /// Waits for the first name conflict the responder reports, then withdraws
    /// the printer and says so. Returns quietly if the service stops first.
    /// </summary>
    /// <param name="conflict">Completed with the responder's first conflict.</param>
    [Requirement("REQ-ADV-024",
        "On the first conflict: closes the gate, logs the name, record type, address and transport with what ends it, and sends goodbyes. Nothing reopens the gate afterwards, because ClaimAndOfferAsync refuses while a conflict is held.")]
    public static async Task WithdrawOnConflictAsync(
        Task<NameConflict> conflict,
        MdnsResponder responder,
        AvailabilityGate offering,
        IServiceLog log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(offering);
        ArgumentNullException.ThrowIfNull(log);

        NameConflict found;
        try
        {
            found = await conflict.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        offering.Close();
        log.Error(DescribeConflict(found));

        using var goodbyeWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        goodbyeWindow.CancelAfter(GoodbyeWindow);

        try
        {
            await responder.SendGoodbyeAsync(goodbyeWindow.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException)
        {
            log.Warn($"Could not send goodbye records after the name conflict: {ex.Message}. "
                     + "Clients may show the printer until its TTL expires.");
        }
    }

    /// <summary>
    /// The log line for a name conflict: what was heard, from where, and what
    /// the operator has to do.
    /// </summary>
    /// <remarks>
    /// The name comes from the network, but only as a <c>DnsName</c> that
    /// matched one this service advertises, and the whole line passes through
    /// the log's escaping (REQ-OBS-010) like any other.
    /// </remarks>
    public static string DescribeConflict(NameConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return $"Name conflict: {conflict.From} answered for {conflict.Name} ({conflict.Type}) with a record "
               + $"this service would not publish, heard on {conflict.ArrivedOn}. Another device uses a name "
               + "this service advertises, so nothing is offered: no answers, no announcements, no listener. "
               + "This lasts until the service is restarted; choose a different name in the configuration "
               + "first.";
    }
}

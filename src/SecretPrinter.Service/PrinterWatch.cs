// -----------------------------------------------------------------------------
// PrinterWatch.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Purpose:
//   Keeps PrinterReachability fed. It waits until a query is due, asks, reports
//   the outcome, and logs the two moments that matter: the printer going quiet,
//   and the printer coming back.
//
//   PrinterReachability holds the schedule and the verdict. This class holds the
//   clock and the loop. Neither knows anything about adapters.
//
// What it logs, and what it does not:
//   Transitions only. On 2026-09-20 the printer-side WLAN was down for nine
//   hours and five minutes; a line per retry would have been nine hours of log
//   saying the same thing. Two lines describe that outage completely - when it
//   started and when it ended - and the retry stream stays silent in between.
//
// Why it invalidates before it asks:
//   PrinterResolver answers from its cache while the answer is inside its TTL
//   (REQ-RES-004), and every reconfirmation here falls at 80% of that TTL or
//   later - still inside it. A watch that simply called ResolveAsync would be
//   answered from memory every time, would never put a packet on the printer
//   network, and would go on reporting a printer that had been unreachable for
//   hours as healthy. The query this class performs has to be a real one, so
//   the caller's lookup delegate is expected to discard the cache first.
//
// It reports a change through onChanged before returning to the schedule. That
// is how REQ-LIF-006 closes the listener and withdraws the advertisement: this
// class decides WHEN the printer stopped being reachable, and the handler
// decides what the service stops doing about it.
// -----------------------------------------------------------------------------

using SecretPrinter.Resolution;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>
/// Watches the printer on the RFC 6762 schedule held by
/// <see cref="PrinterReachability"/>, and logs when reachability changes.
/// </summary>
public sealed class PrinterWatch
{
    private readonly PrinterReachability _reachability;
    private readonly Func<CancellationToken, Task<ResolvedPrinter>> _lookup;
    private readonly IServiceLog _log;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<bool, CancellationToken, Task>? _onChanged;

    /// <param name="reachability">The state and schedule this loop drives.</param>
    /// <param name="lookup">
    /// Performs one real query and returns the answer, or throws
    /// <see cref="PrinterResolutionException"/> if the printer did not reply.
    /// It is expected to discard any cached answer first; see the note at the
    /// top of this file for why that is not optional.
    /// </param>
    /// <param name="log">Where transitions are recorded.</param>
    /// <param name="clock">Supplied so the schedule can be tested without waiting.</param>
    /// <param name="delay">
    /// How the loop waits. Supplied for the same reason as the clock: a test
    /// advances its own clock instead of sleeping.
    /// </param>
    /// <param name="onChanged">
    /// Invoked with the new reachability whenever it changes, after the change
    /// has been logged. This is where the service stops and resumes offering
    /// the printer (REQ-LIF-006). A handler that throws would end the watch, so
    /// it is expected to deal with its own failures.
    /// </param>
    public PrinterWatch(
        PrinterReachability reachability,
        Func<CancellationToken, Task<ResolvedPrinter>> lookup,
        IServiceLog log,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<bool, CancellationToken, Task>? onChanged = null)
    {
        ArgumentNullException.ThrowIfNull(reachability);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(log);

        _reachability = reachability;
        _lookup = lookup;
        _log = log;
        _clock = clock ?? TimeProvider.System;
        _delay = delay ?? ((wait, token) => Task.Delay(wait, token));
        _onChanged = onChanged;
    }

    /// <summary>Whether the printer is currently held reachable.</summary>
    public bool IsReachable => _reachability.IsReachable;

    /// <summary>
    /// Queries whenever the schedule says to, until cancelled. Returns without
    /// throwing when cancelled; every other failure is the printer being quiet,
    /// which is data rather than an error.
    /// </summary>
    [Requirement("REQ-RES-008",
        "Drives the schedule: waits for whichever comes first of the next query and the record's expiry, "
        + "asks a real question at that point, and feeds the answer or the silence back into the state.")]
    public async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // While the printer is reachable the record can reach the end of its
            // lifetime with no query outstanding, and that moment is itself a
            // transition. Wake for whichever comes first.
            DateTimeOffset wakeAt = _reachability.IsReachable
                ? Earlier(_reachability.NextQueryDue, _reachability.RecordExpiresAt)
                : _reachability.NextQueryDue;

            TimeSpan wait = wakeAt - _clock.GetUtcNow();

            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await _delay(wait, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await ReportAsync(_reachability.Tick(), cancellationToken).ConfigureAwait(false);

            if (_clock.GetUtcNow() < _reachability.NextQueryDue)
            {
                // Woken by the expiry rather than by a query falling due. The
                // Tick above has already delivered the verdict.
                continue;
            }

            await AskAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AskAsync(CancellationToken cancellationToken)
    {
        try
        {
            ResolvedPrinter answer = await _lookup(cancellationToken).ConfigureAwait(false);
            await ReportAsync(_reachability.RecordAnswer(answer), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown. Not silence, and not a verdict about the printer.
        }
        catch (PrinterResolutionException)
        {
            await ReportAsync(_reachability.RecordSilence(), cancellationToken).ConfigureAwait(false);
        }
    }

    [Requirement("REQ-LIF-006",
        "Reports every change in reachability, once, to the handler that stops or resumes offering the "
        + "printer - so the listener and the advertisement follow the measurement rather than a timer.")]
    private async Task ReportAsync(ReachabilityTransition transition, CancellationToken cancellationToken)
    {
        switch (transition)
        {
            case ReachabilityTransition.BecameUnreachable:
                _log.Warn(
                    "Printer unreachable: it stopped answering on the printer-side interface and its "
                    + "records have expired. Nothing is claimed about the printer itself, or about why. "
                    + "Asking again on the RFC 6762 schedule, backing off to hourly.");
                break;

            case ReachabilityTransition.BecameReachable:
                _log.Info("Printer reachable again: it answered on the printer-side interface.");
                break;

            case ReachabilityTransition.None:
            default:
                return;
        }

        if (_onChanged is not null)
        {
            await _onChanged(
                transition == ReachabilityTransition.BecameReachable, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static DateTimeOffset Earlier(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}

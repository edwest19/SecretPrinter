// -----------------------------------------------------------------------------
// PrinterWatch.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Answers obtained by print jobs' lookups taken in, and the wait ended early
// for them, by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-25, for REQ-RES-008. See "Answers that jobs obtain"
// below. Reviewed by a human before merge.
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
// Why every failure is silence:
//   A lookup can fail without the printer ever having been asked. When the
//   printer-side interface loses its address the send itself throws
//   SocketException - "The requested address is not valid in its context",
//   measured on FIOS-STB-01 on 2026-09-20 - and that is not a
//   PrinterResolutionException. An earlier version of this class caught only
//   the latter, so such a failure would have faulted the watch, and a faulted
//   watch brings down the whole service through Task.WhenAll. That is a worse
//   outcome than the fault it was meant to handle: before this class existed,
//   the same error cost one print job.
//
//   So anything that is not cancellation counts as silence. For deciding
//   reachability the distinction does not matter - a question that could not
//   leave the machine and a question that was not answered both mean the
//   printer was not reached - and nothing here claims otherwise. A failure
//   that is not simply the printer staying quiet is logged with its type, so
//   it cannot be mistaken for an ordinary timeout.
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
//
// Answers that jobs obtain:
//   REQ-RES-008 says a resolution performed because a job arrived resets the
//   schedule. A job's lookup does not go through this class, so the service
//   hands its answer over with RecordJobAnswer, and this loop records it with
//   PrinterReachability.RecordDemandAnswer. Until 2026-09-25 nothing did; see
//   docs/findings/2026-09-25-two-clauses-of-req-res-008-were-never-built.md.
//
//   RecordJobAnswer only queues the answer. The job goes on at once, and the
//   state is still changed by this loop alone, so reachability is never
//   touched from two threads and changes are reported one at a time, in order.
//   The loop's wait ends early when an answer is queued, because the answer
//   moves the schedule and the wait was computed from the old one.
//
//   A job's lookup that goes unanswered is not handed over. See the note on
//   demand-driven queries in PrinterReachability.cs.
// -----------------------------------------------------------------------------

using System.Threading.Channels;
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

    // Answers that jobs' lookups obtained, waiting for the loop to record them.
    // Only the loop reads it, but it is not created as single-reader: that
    // variant supports fewer operations, and an unsupported one would throw on
    // the watch's loop.
    private readonly Channel<ResolvedPrinter> _jobAnswers = Channel.CreateUnbounded<ResolvedPrinter>();

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
    /// Hands the watch an answer that a print job's lookup obtained. The watch
    /// records it on its own loop, which it wakes for, and it resets the
    /// schedule unless it tells nothing new; see
    /// <see cref="PrinterReachability.RecordDemandAnswer"/>. Never blocks, so
    /// the job goes on at once.
    /// </summary>
    [Requirement("REQ-RES-008",
        "Takes the answer a job's lookup obtained and wakes the watch to record it, so that a resolution "
        + "performed because a job arrived resets the schedule.")]
    public void RecordJobAnswer(ResolvedPrinter answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        // Unbounded and never completed, so this always succeeds.
        _jobAnswers.Writer.TryWrite(answer);
    }

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
            // Answers that jobs obtained come first: each can move the schedule,
            // and the wait below is computed from it.
            await RecordJobAnswersAsync(cancellationToken).ConfigureAwait(false);

            // While the printer is reachable the record can reach the end of its
            // lifetime with no query outstanding, and that moment is itself a
            // transition. Wake for whichever comes first.
            DateTimeOffset wakeAt = _reachability.IsReachable
                ? Earlier(_reachability.NextQueryDue, _reachability.RecordExpiresAt)
                : _reachability.NextQueryDue;

            TimeSpan wait = wakeAt - _clock.GetUtcNow();

            if (wait > TimeSpan.Zero)
            {
                bool answerArrived;

                try
                {
                    answerArrived = await WaitAsync(wait, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (answerArrived && !cancellationToken.IsCancellationRequested)
                {
                    // A job's answer arrived during the wait. Record it and work
                    // the wait out again from the schedule it leaves.
                    continue;
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

    /// <summary>
    /// Waits for <paramref name="wait"/>, or until a job hands over an answer,
    /// whichever comes first.
    /// </summary>
    /// <returns>True when a job's answer is waiting to be recorded.</returns>
    private async Task<bool> WaitAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        using var woken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task elapsed = _delay(wait, woken.Token);
        Task<bool> arrived = _jobAnswers.Reader.WaitToReadAsync(woken.Token).AsTask();

        await Task.WhenAny(elapsed, arrived).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (elapsed.IsCompleted)
        {
            // Observed rather than left behind, so a delay that failed fails the
            // watch as it did before the wait could be ended early.
            await elapsed.ConfigureAwait(false);
        }

        bool answerArrived = arrived.IsCompletedSuccessfully && await arrived.ConfigureAwait(false);

        // Ends whichever of the two is still waiting.
        await woken.CancelAsync().ConfigureAwait(false);

        return answerArrived;
    }

    /// <summary>Records every answer that jobs have handed over, in order.</summary>
    private async Task RecordJobAnswersAsync(CancellationToken cancellationToken)
    {
        while (_jobAnswers.Reader.TryRead(out ResolvedPrinter? answer))
        {
            await ReportAsync(_reachability.RecordDemandAnswer(answer), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AskAsync(CancellationToken cancellationToken)
    {
        try
        {
            ResolvedPrinter answer = await _lookup(cancellationToken).ConfigureAwait(false);
            await ReportAsync(_reachability.RecordAnswer(answer), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown. Not silence, and not a verdict about the printer.
        }
        catch (PrinterResolutionException)
        {
            // The ordinary case: asked, and not answered. The transition, if
            // there is one, is the whole story; nothing more is logged.
            await ReportAsync(_reachability.RecordSilence(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Everything else, including a send that could not leave the
            // machine. The question went unanswered, so it is silence like any
            // other - but it is not the ordinary kind, so it is named rather
            // than folded in quietly.
            _log.Warn(
                $"Printer lookup failed before an answer could be expected: {ex.GetType().Name}: "
                + $"{ex.Message}. Treated as no answer. Nothing is claimed about the printer.");

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

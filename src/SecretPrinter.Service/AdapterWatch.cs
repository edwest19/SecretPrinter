// -----------------------------------------------------------------------------
// AdapterWatch.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-10, for the SecretPrinter project, for REQ-RES-010.
// Reviewed by a human before merge.
//
// Purpose:
//   Says when the printer-side adapter has become usable again, so that a
//   printer held unreachable is asked at once (PrinterWatch.StartQuestionsOver)
//   rather than at the next of questions that may by then be an hour apart
//   (REQ-RES-008).
//
// Why it exists:
//   On 2026-10-10 the record of release 0.1.0 on FIOS-STB-01 showed the printer
//   offered again 38 minutes and about 12 minutes after the printer-side Wi-Fi
//   had been reconnected and the printer's port answered, because the service
//   asks a printer it holds unreachable on a schedule that backs off to once an
//   hour. Edwin West decided that day that the questions start over when the
//   adapter is usable again. See
//   docs/findings/2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md.
//
// What it does:
//   Every CheckInterval, the five seconds the startup wait uses, it examines the
//   adapter by the startup wait's own rule (StartupWait.Examine, REQ-LIF-008):
//   up, holding exactly one IPv4 address, that address outside 169.254.0.0/16
//   and not reported tentative, deprecated or invalid. It reports the interface, as found, when
//   it is usable after having been seen not usable, and when it is usable with
//   a different address or index than when last seen. Nothing else is
//   reported, so an adapter that stays usable never starts anything over.
//
//   It runs from the end of the service's startup, when the printer watch
//   starts, until the service stops, whether or not the printer is held
//   reachable. The watch decides what a report means: while the printer is
//   held reachable, nothing.
//
// What it does not do:
//   It never says the printer is reachable. Only an answer does that
//   (REQ-RES-008). It does not reopen the printer-side socket; the watch's next
//   question does, as it does before every question to a printer held
//   unreachable (REQ-RES-009). A drop shorter than CheckInterval falls between
//   two examinations and is not seen.
//
//   It does not make every loss shorter. Starting the questions over moves
//   when every later question falls, so a printer that starts answering long
//   after its network is back can be found later than it would have been
//   without it. Worked out from the code against 0.1.0's record, one loss of
//   2026-10-09 would have ended 26 to 35 minutes later (see the finding).
//
// What it logs:
//   Only a failure to examine the adapter: once when a failure begins, and
//   again only when it changes, or when it ends and comes back. A failed
//   examination says nothing about the adapter, so it is not taken for a
//   change either way. The start over itself is logged by the watch, which
//   knows whether it happened.
// -----------------------------------------------------------------------------

using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>
/// Examines the printer-side adapter at a fixed interval and reports when it has
/// become usable again.
/// </summary>
public sealed class AdapterWatch
{
    private readonly Func<(MdnsInterface? Usable, string? Reason)> _examine;
    private readonly Action<MdnsInterface> _usableAgain;
    private readonly IServiceLog _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    // The interface as last seen usable, or null when it was last seen not
    // usable. Touched only by CheckOnce, which one loop calls.
    private MdnsInterface? _last;

    // The failure last logged, so that one that repeats is logged once. Cleared
    // by an examination that works, so that a failure that comes back is
    // logged again.
    private string? _reportedFailure;

    /// <summary>How often the adapter is examined: the interval the startup wait uses.</summary>
    public static TimeSpan CheckInterval => StartupWait.AdapterCheckInterval;

    /// <param name="atStart">The interface the service started with, usable then.</param>
    /// <param name="examine">
    /// Examines the adapter now. In the service this is
    /// <see cref="StartupWait.Examine"/>, as for <see cref="PrinterSide"/>.
    /// </param>
    /// <param name="usableAgain">Told the interface, as found, when it is usable again.</param>
    /// <param name="log">Where a failed examination is recorded.</param>
    /// <param name="delay">How the loop waits; supplied so tests need not sleep.</param>
    public AdapterWatch(
        MdnsInterface atStart,
        Func<(MdnsInterface? Usable, string? Reason)> examine,
        Action<MdnsInterface> usableAgain,
        IServiceLog log,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(atStart);
        ArgumentNullException.ThrowIfNull(examine);
        ArgumentNullException.ThrowIfNull(usableAgain);
        ArgumentNullException.ThrowIfNull(log);

        _last = atStart;
        _examine = examine;
        _usableAgain = usableAgain;
        _log = log;
        _delay = delay ?? ((wait, token) => Task.Delay(wait, token));
    }

    /// <summary>
    /// Examines the adapter every <see cref="CheckInterval"/> until cancelled.
    /// Returns without throwing when cancelled.
    /// </summary>
    [Requirement("REQ-RES-010",
        "Examines the printer-side adapter every five seconds, the startup wait's interval, from the end of startup until the service stops.")]
    public async Task WatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _delay(CheckInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            CheckOnce();
        }
    }

    /// <summary>
    /// Examines the adapter once, and reports the interface if it has become
    /// usable after being seen not usable, or is usable on a different address
    /// or index than when last seen.
    /// </summary>
    [Requirement("REQ-RES-010",
        "Reports the interface only when it is usable after being seen not usable, or usable with another address or index; an adapter that stays usable is never reported, and a failed examination is logged when it begins, changes, or comes back, and changes nothing.")]
    public void CheckOnce()
    {
        MdnsInterface? usable;
        try
        {
            usable = _examine().Usable;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failure says nothing about the adapter: no change either way.
            string reason = $"{ex.GetType().Name}: {ex.Message}";
            if (!string.Equals(reason, _reportedFailure, StringComparison.Ordinal))
            {
                _reportedFailure = reason;
                _log.Warn(
                    $"Could not examine the printer-side interface: {reason}"
                    + (reason.EndsWith('.') ? " " : ". ")
                    + "Questions to the printer go on as scheduled. This is logged again only if the failure changes, or ends and comes back.");
            }

            return;
        }

        _reportedFailure = null;

        if (usable is not null && !usable.Equals(_last))
        {
            _usableAgain(usable);
        }

        _last = usable;
    }
}

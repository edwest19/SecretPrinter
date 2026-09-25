// -----------------------------------------------------------------------------
// PrinterWatchTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Loss_claims_nothing_it_did_not_measure corrected by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-09-25. It forbade the
// word "off" anywhere in the log, and the watch's own line says "backing off to
// hourly", so it had failed since it was written - unseen, because until the
// same day the harness never waited for an async test. It now forbids the
// phrases that would claim the printer is off. See
// docs/findings/2026-09-25-the-test-harness-never-waited-for-an-async-test.md.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds the watch to two things: that it actually asks, and that it only
//   speaks when something changed.
//
//   The second matters more than it sounds. The outage measured on 2026-09-20
//   lasted nine hours and five minutes. A line per retry would have buried the
//   two lines that describe it. These tests assert the silence in between as
//   firmly as they assert the two lines.
//
//   The last two cases guard a defect found by reading this file on 2026-09-20,
//   after it had been written, tested and pushed: a lookup can fail without the
//   printer being asked at all, and the first version of AskAsync caught only
//   PrinterResolutionException. A SocketException would have faulted the watch
//   and taken the service down with it - worse than the fault it handles.
//
//   The clock and the waiting are both supplied, so nothing here sleeps.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Resolution;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class PrinterWatchTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly DnsName Instance =
        new(["EPSON ET-3760 Series", "_ipps", "_tcp", "local"]);

    private const uint Ttl = 120;

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _current = now;

        public override DateTimeOffset GetUtcNow() => _current;

        public void MoveTo(DateTimeOffset when) => _current = when;
    }

    /// <summary>Collects what the watch chose to say.</summary>
    private sealed class RecordingLog : IServiceLog
    {
        public List<string> Lines { get; } = [];

        public void Write(LogLevel level, string message) => Lines.Add($"{level}: {message}");
    }

    private static ResolvedPrinter Answer(DateTimeOffset at) => new(
        Instance,
        new DnsName(["EPSON3EA18A", "local"]),
        IPAddress.Parse("192.168.12.180"),
        631,
        [],
        at,
        Ttl);

    /// <summary>
    /// Runs the watch with a lookup scripted by <paramref name="answers"/>: true
    /// answers, false stays silent. The loop stops when the script runs out, so
    /// every case below terminates on its own.
    /// </summary>
    private static async Task<(RecordingLog Log, int Asked, PrinterReachability State)> RunAsync(
        params bool[] answers)
    {
        var clock = new TestClock(Start);
        var log = new RecordingLog();
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        int asked = 0;
        using var stop = new CancellationTokenSource();

        var watch = new PrinterWatch(
            reachability,
            _ =>
            {
                if (asked >= answers.Length)
                {
                    stop.Cancel();
                    throw new OperationCanceledException();
                }

                bool answered = answers[asked++];

                if (asked >= answers.Length)
                {
                    stop.CancelAfter(TimeSpan.Zero);
                }

                return answered
                    ? Task.FromResult(Answer(clock.GetUtcNow()))
                    : throw new PrinterResolutionException("silent, for the test");
            },
            log,
            clock,

            // No waiting: the clock simply moves to the moment the watch was
            // waiting for.
            (wait, _) =>
            {
                clock.MoveTo(clock.GetUtcNow() + wait);
                return Task.CompletedTask;
            });

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        return (log, asked, reachability);
    }

    [TestCase("A printer that keeps answering produces no log lines at all")]
    public static async Task A_healthy_printer_is_not_narrated()
    {
        (RecordingLog log, int asked, PrinterReachability state) = await RunAsync(true, true, true);

        Assert.Equal(3, asked, "the watch must actually query on each scheduled wake");
        Assert.True(state.IsReachable, "a printer that answered three times is reachable");
        Assert.Equal(0, log.Lines.Count, "steady state is silent; only transitions are logged");
    }

    [TestCase("The printer going quiet is logged once, when the record expires")]
    public static async Task Loss_is_logged_once()
    {
        // Four unanswered reconfirmations, then the expiry, then retries.
        (RecordingLog log, _, PrinterReachability state) =
            await RunAsync(false, false, false, false, false, false, false);

        Assert.False(state.IsReachable, "five silences past the record's lifetime means unreachable");

        List<string> warnings = log.Lines.FindAll(line => line.StartsWith("Warning: "));

        Assert.Equal(1, warnings.Count, "the loss is one event, however many retries follow it");
        Assert.True(
            warnings[0].Contains("Printer unreachable", StringComparison.Ordinal),
            "the line must name what happened");
    }

    [TestCase("The watch does not blame the printer for the silence")]
    public static async Task Loss_claims_nothing_it_did_not_measure()
    {
        (RecordingLog log, _, _) = await RunAsync(false, false, false, false, false);

        string all = string.Join(" ", log.Lines);

        // Phrases rather than the bare word "off", which the line uses in
        // "backing off to hourly" and which says nothing about the printer.
        foreach (string forbidden in new[]
                 {
                     "asleep", "is off", "turned off", "switched off", "powered", "broken", "adapter",
                 })
        {
            Assert.False(
                all.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"silence does not establish '{forbidden}'; nothing here may claim it");
        }
    }

    [TestCase("The printer answering again is logged as recovery")]
    public static async Task Recovery_is_logged()
    {
        (RecordingLog log, _, PrinterReachability state) =
            await RunAsync(false, false, false, false, false, true);

        Assert.True(state.IsReachable, "an answer restores reachability");

        List<string> recovered =
            log.Lines.FindAll(line => line.Contains("reachable again", StringComparison.Ordinal));

        Assert.Equal(1, recovered.Count, "recovery is reported exactly once");
    }

    [TestCase("The watch asks at the moment the schedule says, not before")]
    public static async Task The_first_query_waits_for_eighty_percent()
    {
        var clock = new TestClock(Start);
        var log = new RecordingLog();
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        DateTimeOffset? askedAt = null;
        using var stop = new CancellationTokenSource();

        var watch = new PrinterWatch(
            reachability,
            _ =>
            {
                askedAt = clock.GetUtcNow();
                stop.Cancel();
                throw new OperationCanceledException();
            },
            log,
            clock,
            (wait, _) =>
            {
                clock.MoveTo(clock.GetUtcNow() + wait);
                return Task.CompletedTask;
            });

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.Equal(
            Start + TimeSpan.FromSeconds(96),
            askedAt,
            "80% of a 120s lifetime is 96s, and the watch waits for it");
    }

    [TestCase("Cancellation ends the watch without throwing")]
    public static async Task Cancellation_returns_quietly()
    {
        var clock = new TestClock(Start);
        var log = new RecordingLog();
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        using var stop = new CancellationTokenSource();
        await stop.CancelAsync().ConfigureAwait(false);

        var watch = new PrinterWatch(
            reachability,
            _ => throw new InvalidOperationException("a cancelled watch must not query"),
            log,
            clock,
            (_, token) => Task.FromCanceled(token));

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.Equal(0, log.Lines.Count, "a watch that never ran has nothing to report");
    }

    [TestCase("A lookup that fails before the printer is asked counts as silence")]
    public static async Task A_socket_failure_is_silence_not_a_crash()
    {
        var clock = new TestClock(Start);
        var log = new RecordingLog();
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        int asked = 0;
        using var stop = new CancellationTokenSource();

        var watch = new PrinterWatch(
            reachability,
            _ =>
            {
                if (++asked >= 5)
                {
                    stop.CancelAfter(TimeSpan.Zero);
                }

                // Exactly what the send throws when the printer-side interface
                // has lost its address, measured on FIOS-STB-01 on 2026-09-20.
                throw new SocketException((int)SocketError.AddressNotAvailable);
            },
            log,
            clock,
            (wait, _) =>
            {
                clock.MoveTo(clock.GetUtcNow() + wait);
                return Task.CompletedTask;
            });

        // The failure must not escape: a faulted watch takes the service with it.
        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.False(
            reachability.IsReachable,
            "a lookup that could not leave the machine is no evidence the printer is there");

        Assert.True(
            log.Lines.Exists(line => line.Contains("SocketException", StringComparison.Ordinal)),
            "a failure that is not an ordinary timeout must be named, not folded in quietly");

        Assert.True(
            log.Lines.Exists(line => line.Contains("Printer unreachable", StringComparison.Ordinal)),
            "the printer still becomes unreachable, by the same schedule as any other silence");
    }

    [TestCase("An unexpected failure is named and does not end the watch")]
    public static async Task An_unexpected_failure_does_not_end_the_watch()
    {
        var clock = new TestClock(Start);
        var log = new RecordingLog();
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        int asked = 0;
        using var stop = new CancellationTokenSource();

        var watch = new PrinterWatch(
            reachability,
            _ =>
            {
                asked++;

                if (asked == 1)
                {
                    throw new InvalidOperationException("something nobody predicted");
                }

                stop.CancelAfter(TimeSpan.Zero);
                return Task.FromResult(Answer(clock.GetUtcNow()));
            },
            log,
            clock,
            (wait, _) =>
            {
                clock.MoveTo(clock.GetUtcNow() + wait);
                return Task.CompletedTask;
            });

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.True(asked >= 2, "the watch must keep asking after an unexpected failure");

        Assert.True(
            log.Lines.Exists(line => line.Contains("InvalidOperationException", StringComparison.Ordinal)),
            "an unexpected failure must be named in the log rather than swallowed");
    }
}

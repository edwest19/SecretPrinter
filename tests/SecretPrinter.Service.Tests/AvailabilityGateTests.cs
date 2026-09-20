// -----------------------------------------------------------------------------
// AvailabilityGateTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Purpose:
//   Holds the gate to the two properties the relay depends on: a waiter that is
//   already satisfied does not block, and a waiter that is not is released the
//   moment the gate moves.
//
//   The second is what closes the listener. If WaitForCloseAsync did not
//   complete promptly, the service would go on accepting jobs for a printer it
//   had established it could not reach, which is the whole defect this work
//   exists to remove.
//
//   The last case covers the join between the watch and the gate: the watch
//   must report a loss as a loss, and it is the only thing that moves it.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class AvailabilityGateTests
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

    private sealed class SilentLog : IServiceLog
    {
        public void Write(LogLevel level, string message)
        {
            // The log is exercised by PrinterWatchTests; here it is noise.
        }
    }

    private static ResolvedPrinter Answer(DateTimeOffset at) => new(
        Instance,
        new DnsName(["EPSON3EA18A", "local"]),
        IPAddress.Parse("192.168.12.180"),
        631,
        [],
        at,
        Ttl);

    [TestCase("An open gate satisfies a waiter for open without blocking")]
    [Requirement("REQ-LIF-006")]
    public static async Task An_open_gate_does_not_block_a_waiter_for_open()
    {
        var gate = new AvailabilityGate(open: true);

        Assert.True(gate.IsOpen, "the service starts having resolved the printer");

        await gate.WaitForOpenAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestCase("Closing the gate releases a waiter that was waiting for it")]
    [Requirement("REQ-LIF-006")]
    public static async Task Closing_releases_the_waiter_that_closes_the_listener()
    {
        var gate = new AvailabilityGate(open: true);

        Task withdrawn = gate.WaitForCloseAsync(CancellationToken.None);

        Assert.False(withdrawn.IsCompleted, "nothing has closed the gate yet");

        gate.Close();

        await withdrawn.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.False(gate.IsOpen, "the gate must report the state it was moved to");
    }

    [TestCase("Opening the gate releases a waiter that was waiting for it")]
    [Requirement("REQ-LIF-006")]
    public static async Task Opening_releases_the_waiter_that_reopens_the_listener()
    {
        var gate = new AvailabilityGate(open: false);

        Task serving = gate.WaitForOpenAsync(CancellationToken.None);

        Assert.False(serving.IsCompleted, "nothing has opened the gate yet");

        gate.Open();

        await serving.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.True(gate.IsOpen, "the gate must report the state it was moved to");
    }

    [TestCase("Moving the gate to where it already is changes nothing")]
    [Requirement("REQ-LIF-006")]
    public static async Task Repeating_a_move_is_harmless()
    {
        var gate = new AvailabilityGate(open: true);

        gate.Open();
        gate.Open();

        Assert.True(gate.IsOpen, "opening an open gate leaves it open");

        gate.Close();
        Task withdrawn = gate.WaitForCloseAsync(CancellationToken.None);
        gate.Close();

        await withdrawn.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.False(gate.IsOpen, "closing a closed gate leaves it closed");
    }

    [TestCase("A gate reopened after a close releases a fresh waiter")]
    [Requirement("REQ-LIF-006")]
    public static async Task The_gate_can_be_reused_across_an_outage()
    {
        var gate = new AvailabilityGate(open: true);

        gate.Close();
        gate.Open();

        // The outage is over; a relay starting its next round must not be held
        // by a signal left over from the previous one.
        await gate.WaitForOpenAsync(CancellationToken.None).ConfigureAwait(false);

        Task withdrawn = gate.WaitForCloseAsync(CancellationToken.None);
        Assert.False(withdrawn.IsCompleted, "the close from the previous outage must not still be signalled");

        gate.Close();
        await withdrawn.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    [TestCase("The watch closes the gate when the printer goes quiet, and opens it when it returns")]
    [Requirement("REQ-LIF-006")]
    public static async Task The_watch_drives_the_gate()
    {
        var clock = new TestClock(Start);
        var gate = new AvailabilityGate(open: true);
        var reachability = new PrinterReachability(Answer(Start), clock, () => 0.0);

        // Silent until the record expires, then answering again.
        bool[] script = [false, false, false, false, false, true];
        int asked = 0;

        using var stop = new CancellationTokenSource();
        var moves = new List<bool>();

        var watch = new PrinterWatch(
            reachability,
            _ =>
            {
                if (asked >= script.Length)
                {
                    stop.Cancel();
                    throw new OperationCanceledException();
                }

                bool answered = script[asked++];

                if (asked >= script.Length)
                {
                    stop.CancelAfter(TimeSpan.Zero);
                }

                return answered
                    ? Task.FromResult(Answer(clock.GetUtcNow()))
                    : throw new PrinterResolutionException("silent, for the test");
            },
            new SilentLog(),
            clock,
            (wait, _) =>
            {
                clock.MoveTo(clock.GetUtcNow() + wait);
                return Task.CompletedTask;
            },
            onChanged: (reachable, _) =>
            {
                moves.Add(reachable);

                if (reachable)
                {
                    gate.Open();
                }
                else
                {
                    gate.Close();
                }

                return Task.CompletedTask;
            });

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.Equal(2, moves.Count, "one loss and one recovery, and nothing in between");
        Assert.False(moves[0], "the printer going quiet must close the gate");
        Assert.True(moves[1], "the printer answering again must open it");
        Assert.True(gate.IsOpen, "the gate ends where the last transition left it");
    }
}

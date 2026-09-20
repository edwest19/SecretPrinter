// -----------------------------------------------------------------------------
// PrinterReachabilityTests.cs  (SecretPrinter.Resolution.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Purpose:
//   Pins the reachability schedule to RFC 6762 section 5.2 rather than to
//   whatever the implementation happens to do.
//
//   Two numbers matter to a person waiting on a print job, and both are
//   asserted here against the printer's real TTL of 120 seconds: the printer is
//   held reachable for the full 120s and not a moment less, and the retry
//   stream while unreachable starts at one second and settles at one query an
//   hour.
//
//   The clock and the jitter are both supplied, so every case below is exact
//   and none of them waits.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Resolution.Tests;

internal static class PrinterReachabilityTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly DnsName Instance =
        new(["EPSON ET-3760 Series", "_ipps", "_tcp", "local"]);

    /// <summary>The printer's real TTL, as logged on the dev box.</summary>
    private const uint Ttl = 120;

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;

        public void MoveTo(DateTimeOffset when) => _now = when;
    }

    private static ResolvedPrinter Answer(DateTimeOffset at) => new(
        Instance,
        new DnsName(["EPSON3EA18A", "local"]),
        IPAddress.Parse("192.168.12.180"),
        631,
        [],
        at,
        Ttl);

    /// <summary>No spread, so the schedule lands on exactly 80/85/90/95%.</summary>
    private static PrinterReachability Fresh(TestClock clock) =>
        new(Answer(clock.GetUtcNow()), clock, () => 0.0);

    [TestCase("A printer that has just answered is reachable")]
    [Requirement("REQ-RES-008")]
    public static void Fresh_answer_is_reachable()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        Assert.True(reachability.IsReachable, "a printer that just answered must be reachable");
        Assert.Equal(0, reachability.UnansweredQueries, "nothing has gone unanswered yet");
    }

    [TestCase("The first reconfirmation is due at 80% of the record lifetime")]
    [Requirement("REQ-RES-008")]
    public static void First_reconfirmation_is_at_eighty_percent()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        Assert.Equal(
            Start + TimeSpan.FromSeconds(96),
            reachability.NextQueryDue,
            "80% of a 120s lifetime is 96s");
    }

    [TestCase("The 2% spread never pushes a reconfirmation into the next step")]
    [Requirement("REQ-RES-008")]
    public static void Jitter_stays_inside_its_own_step()
    {
        var clock = new TestClock(Start);

        // Full spread: 82% of the lifetime, which must still fall short of the
        // 85% step. If these ever overlap the schedule would skip a query.
        var reachability = new PrinterReachability(Answer(Start), clock, () => 1.0);

        Assert.Equal(
            Start + TimeSpan.FromSeconds(98.4),
            reachability.NextQueryDue,
            "80% plus the full 2% spread of a 120s lifetime is 98.4s");

        Assert.True(
            reachability.NextQueryDue < Start + TimeSpan.FromSeconds(102),
            "the spread must not reach the 85% step at 102s");
    }

    [TestCase("Unanswered reconfirmations step through 85%, 90% and 95%")]
    [Requirement("REQ-RES-008")]
    public static void Silence_steps_through_the_schedule()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(96));
        reachability.RecordSilence();
        Assert.Equal(
            Start + TimeSpan.FromSeconds(102),
            reachability.NextQueryDue,
            "85% of a 120s lifetime is 102s");

        clock.MoveTo(Start + TimeSpan.FromSeconds(102));
        reachability.RecordSilence();
        Assert.Equal(
            Start + TimeSpan.FromSeconds(108),
            reachability.NextQueryDue,
            "90% of a 120s lifetime is 108s");

        clock.MoveTo(Start + TimeSpan.FromSeconds(108));
        reachability.RecordSilence();
        Assert.Equal(
            Start + TimeSpan.FromSeconds(114),
            reachability.NextQueryDue,
            "95% of a 120s lifetime is 114s");
    }

    [TestCase("Four unanswered reconfirmations do not condemn the printer early")]
    [Requirement("REQ-RES-008")]
    public static void Four_silences_wait_for_the_record_to_expire()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        foreach (int second in new[] { 96, 102, 108, 114 })
        {
            clock.MoveTo(Start + TimeSpan.FromSeconds(second));
            Assert.Equal(
                ReachabilityTransition.None,
                reachability.RecordSilence(),
                $"silence at {second}s must not condemn the printer before its record expires");
        }

        Assert.True(
            reachability.IsReachable,
            "the RFC deletes the record at 100% of its lifetime, not at the last query");

        Assert.Equal(
            Start + TimeSpan.FromSeconds(120),
            reachability.NextQueryDue,
            "nothing more is asked between the last reconfirmation and expiry");
    }

    [TestCase("The printer becomes unreachable when the record reaches 100% of its lifetime")]
    [Requirement("REQ-RES-008")]
    public static void Expiry_makes_the_printer_unreachable()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(119));
        Assert.Equal(
            ReachabilityTransition.None,
            reachability.Tick(),
            "one second before expiry the printer is still reachable");

        clock.MoveTo(Start + TimeSpan.FromSeconds(120));
        Assert.Equal(
            ReachabilityTransition.BecameUnreachable,
            reachability.Tick(),
            "at 100% of the lifetime the record is deleted and the printer is unreachable");

        Assert.False(reachability.IsReachable, "the state must follow the transition");
    }

    [TestCase("An answer part-way through the schedule starts a fresh lifetime")]
    [Requirement("REQ-RES-008")]
    public static void An_answer_resets_the_schedule()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(96));
        reachability.RecordSilence();

        DateTimeOffset answeredAt = Start + TimeSpan.FromSeconds(100);
        clock.MoveTo(answeredAt);

        Assert.Equal(
            ReachabilityTransition.None,
            reachability.RecordAnswer(Answer(answeredAt)),
            "a printer that was already reachable has not changed state");

        Assert.Equal(0, reachability.UnansweredQueries, "the answer clears the unanswered count");
        Assert.Equal(
            answeredAt + TimeSpan.FromSeconds(96),
            reachability.NextQueryDue,
            "the schedule restarts at 80% of the new lifetime");
    }

    [TestCase("The first retry after a loss comes one second later")]
    [Requirement("REQ-RES-008")]
    public static void First_retry_interval_is_one_second()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(120));
        reachability.Tick();

        Assert.Equal(
            clock.GetUtcNow(),
            reachability.NextQueryDue,
            "the first query of the retry stream goes out at once");

        reachability.RecordSilence();

        Assert.Equal(
            clock.GetUtcNow() + TimeSpan.FromSeconds(1),
            reachability.NextQueryDue,
            "the RFC requires at least one second between the first two queries");
    }

    [TestCase("Retry intervals double")]
    [Requirement("REQ-RES-008")]
    public static void Retry_intervals_double()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(120));
        reachability.Tick();

        foreach (int seconds in new[] { 1, 2, 4, 8, 16 })
        {
            DateTimeOffset asked = clock.GetUtcNow();
            reachability.RecordSilence();

            Assert.Equal(
                asked + TimeSpan.FromSeconds(seconds),
                reachability.NextQueryDue,
                $"the interval after {seconds}s must be double the one before it");

            clock.MoveTo(reachability.NextQueryDue);
        }
    }

    [TestCase("Retry intervals stop at one query an hour")]
    [Requirement("REQ-RES-008")]
    public static void Retry_intervals_are_capped_at_an_hour()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(120));
        reachability.Tick();

        for (int i = 0; i < 40; i++)
        {
            reachability.RecordSilence();
            clock.MoveTo(reachability.NextQueryDue);
        }

        DateTimeOffset asked = clock.GetUtcNow();
        reachability.RecordSilence();

        Assert.Equal(
            asked + TimeSpan.FromMinutes(60),
            reachability.NextQueryDue,
            "the RFC caps a continuous querier at one query per hour");
    }

    [TestCase("A printer that answers again is reported as recovered")]
    [Requirement("REQ-RES-008")]
    public static void An_answer_while_unreachable_reports_recovery()
    {
        var clock = new TestClock(Start);
        PrinterReachability reachability = Fresh(clock);

        clock.MoveTo(Start + TimeSpan.FromSeconds(120));
        reachability.Tick();

        clock.Advance(TimeSpan.FromMinutes(5));
        DateTimeOffset answeredAt = clock.GetUtcNow();

        Assert.Equal(
            ReachabilityTransition.BecameReachable,
            reachability.RecordAnswer(Answer(answeredAt)),
            "coming back is a transition the service must be able to log");

        Assert.True(reachability.IsReachable, "the state must follow the transition");
        Assert.Equal(
            answeredAt + TimeSpan.FromSeconds(96),
            reachability.NextQueryDue,
            "recovery returns to the cache-maintenance schedule");
    }
}

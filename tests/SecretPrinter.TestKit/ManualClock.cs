// -----------------------------------------------------------------------------
// ManualClock.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-12, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   A TimeProvider for tests whose timestamps move only when a test moves
//   them, for code that reads elapsed time from GetTimestamp. Added with the
//   responder's one-second rule (README REQ-ADV-027; RFC 6762 section 6), which
//   reads elapsed time that way so that a wall clock set back cannot hold
//   records back.
//
//   Two ways to use it:
//   - Moved by hand (Advance), for the tests of the rule itself, which need to
//     say exactly how far apart two messages are.
//   - A second apart (SecondApart), for tests about something else that send
//     the same records more than once: every reading of the timestamp is a
//     second after the one before, so the rule never holds a record back and
//     never waits, and those tests check what they checked before the rule
//     existed. Without it they would either fail, because a second answer with
//     the same records is now not sent, or wait real seconds.
//
//   Delay moves the timestamps by the wait asked for, records it, and returns
//   at once. The wall time (GetUtcNow) is kept apart from the timestamps, so
//   that a test can move one and not the other.
// -----------------------------------------------------------------------------

namespace SecretPrinter.TestKit;

/// <summary>A clock whose timestamps move only when a test moves them, or a second at each reading.</summary>
public sealed class ManualClock : TimeProvider
{
    private long _ticks = 5_000_000_000;

    /// <summary>How far the timestamps move each time one is read. Zero unless made by <see cref="SecondApart"/>.</summary>
    public TimeSpan StepEachReading { get; init; }

    /// <summary>A clock each of whose readings is a second after the one before.</summary>
    public static ManualClock SecondApart() => new() { StepEachReading = TimeSpan.FromSeconds(1) };

    /// <summary>Timestamps are in ticks of 100 ns.</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>The current timestamp; then moves on by <see cref="StepEachReading"/>.</summary>
    public override long GetTimestamp()
    {
        long now = _ticks;
        _ticks += StepEachReading.Ticks;
        return now;
    }

    /// <summary>The wall time, apart from the timestamps on purpose.</summary>
    public DateTimeOffset Wall { get; set; } = new(2026, 10, 12, 20, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Wall;

    /// <summary>Every wait asked of <see cref="Delay"/>, in order.</summary>
    public List<TimeSpan> Waits { get; } = [];

    /// <summary>Moves the timestamps on.</summary>
    public void Advance(TimeSpan by) => _ticks += by.Ticks;

    /// <summary>Records the wait, moves the timestamps on by it, and returns at once.</summary>
    public Task Delay(TimeSpan wait, CancellationToken cancellationToken)
    {
        Waits.Add(wait);
        Advance(wait);
        return Task.CompletedTask;
    }
}

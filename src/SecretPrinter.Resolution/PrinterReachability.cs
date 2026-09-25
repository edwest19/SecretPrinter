// -----------------------------------------------------------------------------
// PrinterReachability.cs  (SecretPrinter.Resolution)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// RecordDemandAnswer added, and the paragraph on lookups made because a print
// job arrived corrected, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-25. Until then that paragraph said such a
// lookup's outcome was recorded here, and nothing recorded it. See
// docs/findings/2026-09-25-two-clauses-of-req-res-008-were-never-built.md.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds the service's belief about whether the printer can be reached, and
//   says when the next query is due.
//
//   It is fed the outcome of real mDNS lookups on the printer-side interface,
//   and nothing else. It never reads an adapter, an address, or a multicast
//   membership. That is deliberate and measured: on 2026-09-19 an adapter that
//   had left 224.0.0.251 went on listing it at reference count 0, and an
//   adapter reported up with a working unicast path returned no multicast
//   answers at all. Interface state has been observed wrong in both directions.
//   Resolution has not.
//
// The schedule is RFC 6762 section 5.2, both halves of it:
//
//   Reachable   - cache maintenance. A record answered with TTL T is
//                 reconfirmed at 80%, 85%, 90% and 95% of T, each with up to
//                 the RFC's 2% anti-synchronisation spread. If every one goes
//                 unanswered the record is deleted at 100% of T, and only then
//                 is the printer held unreachable.
//
//   Unreachable - continuous querying. The first two queries are at least one
//                 second apart, each interval is at least double the last, and
//                 the interval is capped at 60 minutes.
//
//   Known-Answer Suppression does not interfere. Section 7.1 has a responder
//   answer anyway when the TTL in a query's Answer Section is less than half
//   the true TTL, and has the querier omit such records for that reason. Every
//   reconfirmation here fires at 80% of lifetime or later, so the record is
//   never a known answer when we ask, and a healthy printer is obliged to
//   reply. Silence therefore means something.
//
// A lookup performed because a print job arrived is a demand-driven query
// rather than part of either stream. An answer it obtains is recorded through
// RecordDemandAnswer and resets the schedule like any other answer. A job's
// lookup that goes unanswered is not recorded at all: the record is still
// within its lifetime, section 5.2 holds it until 100%, and the
// reconfirmations above already decide when it has gone. It could not be
// passed to RecordSilence in any case. That call advances the reconfirmation
// stream by one step, so a job that failed early in a lifetime would move the
// next reconfirmation from 80% to 85%, and the 80% query would never be sent.
//
// This type decides nothing and sends nothing. It holds no socket, starts no
// timer and performs no lookup; the caller queries when NextQueryDue arrives
// and reports what happened. That is what makes every branch below testable on
// a machine with no printer and no network.
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;

namespace SecretPrinter.Resolution;

/// <summary>A change in reachability worth logging. Steady state reports None.</summary>
public enum ReachabilityTransition
{
    /// <summary>No change. The state is the same as it was.</summary>
    None,

    /// <summary>The printer was reachable and is not any more.</summary>
    BecameUnreachable,

    /// <summary>The printer was unreachable and has answered again.</summary>
    BecameReachable,
}

/// <summary>
/// Tracks whether the printer is reachable, from the outcomes of mDNS lookups
/// alone, and schedules the next query per RFC 6762 section 5.2.
/// </summary>
[Requirement("REQ-RES-008",
    "Its only inputs are the outcomes of mDNS resolution on the printer-side interface. There is no "
    + "parameter, field or constructor argument by which adapter state, address state or multicast "
    + "membership could reach this type, so no schedule or verdict here can rest on one.")]
public sealed class PrinterReachability
{
    /// <summary>How many reconfirmations the RFC sends before the record dies.</summary>
    public const int ReconfirmAttempts = 4;

    /// <summary>The shortest interval permitted between the first two queries of a stream.</summary>
    public static readonly TimeSpan FirstRetryInterval = TimeSpan.FromSeconds(1);

    /// <summary>The steady-state rate the RFC permits a continuous querier to settle at.</summary>
    public static readonly TimeSpan MaximumRetryInterval = TimeSpan.FromMinutes(60);

    // 80%, 85%, 90%, 95% of the record lifetime, per RFC 6762 section 5.2.
    private static readonly double[] ReconfirmFractions = [0.80, 0.85, 0.90, 0.95];

    // The RFC's 2% random spread, so that several queriers on one link do not
    // fire together: 80-82%, 85-87%, 90-92%, 95-97%.
    private const double JitterSpan = 0.02;

    private readonly TimeProvider _clock;
    private readonly Func<double> _jitter;

    private bool _reachable;
    private DateTimeOffset _answeredAt;
    private TimeSpan _lifetime;
    private int _unanswered;
    private TimeSpan _retryInterval;
    private DateTimeOffset _nextQueryDue;

    /// <param name="first">
    /// The answer the service already holds. REQ-RES-007 resolves the printer
    /// at startup and refuses to start without it, so there is no state in
    /// which this type exists having never seen one, and none is modelled.
    /// </param>
    /// <param name="clock">
    /// Supplied so the whole schedule can be tested without waiting. Defaults
    /// to the system clock.
    /// </param>
    /// <param name="jitter">
    /// Returns a value in [0,1], scaled to the RFC's 2% spread. Supplied so
    /// tests can pin the schedule to exact fractions. Defaults to random.
    /// </param>
    public PrinterReachability(
        ResolvedPrinter first,
        TimeProvider? clock = null,
        Func<double>? jitter = null)
    {
        ArgumentNullException.ThrowIfNull(first);

        _clock = clock ?? TimeProvider.System;
        _jitter = jitter ?? Random.Shared.NextDouble;

        AcceptAnswer(first);
    }

    /// <summary>Whether the printer is currently held reachable.</summary>
    public bool IsReachable => _reachable;

    /// <summary>When the caller should next query. Never in the past by design.</summary>
    public DateTimeOffset NextQueryDue => _nextQueryDue;

    /// <summary>When the record the belief rests on reaches 100% of its lifetime.</summary>
    public DateTimeOffset RecordExpiresAt => _answeredAt + _lifetime;

    /// <summary>Reconfirmations sent without an answer in the current lifetime.</summary>
    public int UnansweredQueries => _unanswered;

    /// <summary>
    /// Records that the printer answered. Resets the schedule to a fresh
    /// cache-maintenance cycle against the TTL the answer carried.
    /// </summary>
    public ReachabilityTransition RecordAnswer(ResolvedPrinter answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        bool wasReachable = _reachable;
        AcceptAnswer(answer);

        return wasReachable
            ? ReachabilityTransition.None
            : ReachabilityTransition.BecameReachable;
    }

    /// <summary>
    /// Records an answer obtained on demand, because a print job arrived,
    /// rather than on this schedule. It resets the schedule exactly as
    /// <see cref="RecordAnswer"/> does, unless it tells nothing new.
    /// </summary>
    /// <remarks>
    /// Two kinds of answer tell nothing new, and change nothing. One obtained no
    /// later than the answer already held: a job and the watch can share one
    /// query and so receive the same answer, and a job's answer can arrive after
    /// the watch has recorded a later one of its own. And one whose lifetime is
    /// already over: the record it would create is already dead, and taking it
    /// would report the printer reachable only for the next tick to report it
    /// gone. A job's lookup that went unanswered has no counterpart here; see
    /// the note at the top of this file.
    /// </remarks>
    [Requirement("REQ-RES-008",
        "Takes an answer a job's lookup obtained and resets the schedule with it, as with any other answer, "
        + "unless it is no newer than the answer already held or its lifetime is already over.")]
    public ReachabilityTransition RecordDemandAnswer(ResolvedPrinter answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.ResolvedAt <= _answeredAt || !answer.IsFreshAt(_clock.GetUtcNow()))
        {
            return ReachabilityTransition.None;
        }

        return RecordAnswer(answer);
    }

    /// <summary>
    /// Records that a query went unanswered, and schedules the next one.
    /// </summary>
    [Requirement("REQ-RES-008",
        "Carries both schedules of RFC 6762 section 5.2: while reachable, the next reconfirmation steps "
        + "through 80/85/90/95% of the record lifetime and the verdict waits for 100%; while unreachable, "
        + "the interval starts at one second, doubles, and stops at sixty minutes.")]
    public ReachabilityTransition RecordSilence()
    {
        DateTimeOffset now = _clock.GetUtcNow();

        if (!_reachable)
        {
            // Continuous querying: at least a second, then at least double each
            // time, capped so the service never settles above one query an hour.
            _retryInterval = _retryInterval == TimeSpan.Zero
                ? FirstRetryInterval
                : _retryInterval + _retryInterval;

            if (_retryInterval > MaximumRetryInterval)
            {
                _retryInterval = MaximumRetryInterval;
            }

            _nextQueryDue = now + _retryInterval;
            return ReachabilityTransition.None;
        }

        _unanswered++;

        if (now >= RecordExpiresAt)
        {
            return FallUnreachable(now);
        }

        if (_unanswered >= ReconfirmAttempts)
        {
            // All four reconfirmations have gone unanswered. The RFC deletes the
            // record at 100% of its lifetime and not before, so the printer is
            // still held reachable for the remaining few percent, and nothing
            // further is asked in that window. Tick() delivers the verdict when
            // the record actually expires.
            _nextQueryDue = RecordExpiresAt;
            return ReachabilityTransition.None;
        }

        _nextQueryDue = ReconfirmDue(_unanswered);
        return ReachabilityTransition.None;
    }

    /// <summary>
    /// Lets the clock move the state on. Call it on every wake: a record can
    /// reach the end of its lifetime with no query outstanding, and that is
    /// still the moment the printer stops being reachable.
    /// </summary>
    public ReachabilityTransition Tick()
    {
        DateTimeOffset now = _clock.GetUtcNow();

        if (!_reachable || now < RecordExpiresAt)
        {
            return ReachabilityTransition.None;
        }

        return FallUnreachable(now);
    }

    private void AcceptAnswer(ResolvedPrinter answer)
    {
        _reachable = true;
        _answeredAt = answer.ResolvedAt;
        _lifetime = TimeSpan.FromSeconds(answer.Ttl);
        _unanswered = 0;
        _retryInterval = TimeSpan.Zero;
        _nextQueryDue = ReconfirmDue(0);
    }

    private ReachabilityTransition FallUnreachable(DateTimeOffset now)
    {
        _reachable = false;
        _retryInterval = TimeSpan.Zero;

        // Ask at once, then back off. The first interval of the stream is
        // applied after this query, which is where the RFC's one-second minimum
        // between the first two queries comes from.
        _nextQueryDue = now;

        return ReachabilityTransition.BecameUnreachable;
    }

    private DateTimeOffset ReconfirmDue(int attempt)
    {
        double spread = JitterSpan * Math.Clamp(_jitter(), 0.0, 1.0);
        return _answeredAt + (_lifetime * (ReconfirmFractions[attempt] + spread));
    }
}

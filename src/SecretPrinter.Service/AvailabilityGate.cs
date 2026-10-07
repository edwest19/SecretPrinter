// -----------------------------------------------------------------------------
// AvailabilityGate.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// OpenUnless added, and the two statements that only PrinterWatch moves the
// gate corrected - a name conflict now closes it too - by Claude (Anthropic
// model, Claude Opus 5.5) at the direction of Edwin West, 2026-09-28, for
// REQ-ADV-023 and REQ-ADV-024. Reviewed by a human before merge.
//
// The remark on the constructor corrected by Claude (Anthropic model, Claude
// Opus 5.5) at the direction of Edwin West, 2026-10-07: it said the service
// starts the gate open. Since 2026-09-28 the service starts it closed.
// Comment only; no behaviour changed. Reviewed by a human before merge.
//
// Purpose:
//   One switch, read by everything that offers the printer to the client
//   network, so those things cannot disagree about whether it is on offer.
//
//   While the gate is open the service listens for print jobs and answers mDNS
//   queries about itself. While it is closed it does neither. Two things move
//   it: the printer's reachability (REQ-RES-008), through PrinterWatch and
//   Offering, and a name conflict (REQ-ADV-024), which closes it until the
//   service is restarted. Nothing about an adapter moves it.
//
// Why a gate and not a flag:
//   The relay has to stop what it is already doing, not merely decline the next
//   thing. Waiters get a Task rather than polling, so the listener closes at the
//   moment the printer is declared unreachable instead of at the top of some
//   loop.
//
// Opening and closing are idempotent, and safe from any thread.
// -----------------------------------------------------------------------------

namespace SecretPrinter.Service;

/// <summary>
/// Whether the service is currently offering the printer. Read by the relay and
/// by the responder; opened only by <see cref="Offering"/>, after a clear probe,
/// and closed by it when the printer is lost or a name conflict is heard.
/// </summary>
public sealed class AvailabilityGate
{
    private readonly Lock _sync = new();

    private TaskCompletionSource _opened = Signal();
    private TaskCompletionSource _closed = Signal();
    private bool _open;

    /// <param name="open">
    /// The state to start in. The service starts it closed (ServiceHost), and
    /// Offering opens it once the probe for the service's names has come back
    /// clear (REQ-ADV-023). Tests start it in either state.
    /// </param>
    public AvailabilityGate(bool open)
    {
        _open = open;

        if (open)
        {
            _opened.TrySetResult();
        }
        else
        {
            _closed.TrySetResult();
        }
    }

    /// <summary>Whether the printer is currently on offer.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_sync)
            {
                return _open;
            }
        }
    }

    /// <summary>Begins offering the printer. Does nothing if already open.</summary>
    public void Open()
    {
        lock (_sync)
        {
            if (_open)
            {
                return;
            }

            _open = true;
            _closed = Signal();
            _opened.TrySetResult();
        }
    }

    /// <summary>Stops offering the printer. Does nothing if already closed.</summary>
    /// <summary>
    /// Opens the gate unless <paramref name="refuse"/> returns true, deciding and
    /// opening under the gate's lock. Returns whether the gate is open.
    /// </summary>
    /// <remarks>
    /// For a condition that can become true on another thread, which is then
    /// followed by <see cref="Close"/> - a name conflict, recorded by the
    /// responder's receive loop before the gate is closed. Checking and then
    /// calling <see cref="Open"/> would leave a moment in which the gate could
    /// open after the conflict was recorded and after it was closed for it.
    /// Deciding under the lock leaves two orders only: the condition is seen
    /// and the gate stays closed, or the gate opens first and the close that
    /// follows the conflict shuts it again.
    /// </remarks>
    public bool OpenUnless(Func<bool> refuse)
    {
        ArgumentNullException.ThrowIfNull(refuse);

        lock (_sync)
        {
            if (_open)
            {
                return true;
            }

            if (refuse())
            {
                return false;
            }

            _open = true;
            _closed = Signal();
            _opened.TrySetResult();
            return true;
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _opened = Signal();
            _closed.TrySetResult();
        }
    }

    /// <summary>Completes once the printer is on offer, at once if it already is.</summary>
    public Task WaitForOpenAsync(CancellationToken cancellationToken)
    {
        Task wait;

        lock (_sync)
        {
            if (_open)
            {
                return Task.CompletedTask;
            }

            wait = _opened.Task;
        }

        return wait.WaitAsync(cancellationToken);
    }

    /// <summary>Completes once the printer is off offer, at once if it already is.</summary>
    public Task WaitForCloseAsync(CancellationToken cancellationToken)
    {
        Task wait;

        lock (_sync)
        {
            if (!_open)
            {
                return Task.CompletedTask;
            }

            wait = _closed.Task;
        }

        return wait.WaitAsync(cancellationToken);
    }

    // RunContinuationsAsynchronously: a waiter must not run its continuation
    // inside Open() or Close() while the lock above is held.
    private static TaskCompletionSource Signal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

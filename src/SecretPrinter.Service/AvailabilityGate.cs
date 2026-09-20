// -----------------------------------------------------------------------------
// AvailabilityGate.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-20. Reviewed by a human before
// merge.
//
// Purpose:
//   One switch, read by everything that offers the printer to the client
//   network, so those things cannot disagree about whether it is on offer.
//
//   While the gate is open the service listens for print jobs and answers mDNS
//   queries about itself. While it is closed it does neither. PrinterWatch is
//   the only thing that moves it, and it moves it from the reachability state
//   (REQ-RES-008) rather than from anything about an adapter.
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
/// by the responder; written only by <see cref="PrinterWatch"/>.
/// </summary>
public sealed class AvailabilityGate
{
    private readonly Lock _sync = new();

    private TaskCompletionSource _opened = Signal();
    private TaskCompletionSource _closed = Signal();
    private bool _open;

    /// <param name="open">
    /// The state to start in. The service resolves the printer before it
    /// advertises anything (REQ-RES-007), so it starts open.
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

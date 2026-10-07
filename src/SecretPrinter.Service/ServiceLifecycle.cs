// -----------------------------------------------------------------------------
// ServiceLifecycle.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// The failure handler added by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-07. Until then a failure of the work was
// recorded and logged and nothing else happened, so under the service control
// manager the process stayed and Windows went on showing a failed service as
// running. See
// docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.
// Reviewed by a human before merge.
//
// Purpose:
//   The start and stop behaviour a Windows service needs, with none of the
//   Windows service machinery.
//
// Why it is separate from WindowsService:
//   ServiceBase cannot be exercised by a test - it wants a service control
//   manager, which means installing and starting a real service. Everything
//   interesting about starting and stopping lives here instead, where it can be
//   tested: that Start returns promptly rather than blocking the control
//   manager, that a startup failure is reported rather than swallowed, that
//   Stop waits for the advertisement to be retracted, and that stopping
//   something never started is harmless.
//
//   WindowsService is then a thin adapter with nothing left to get wrong except
//   the handshake itself, which only a real install can prove.
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Starts and stops a long-running operation on behalf of a service host.</summary>
public sealed class ServiceLifecycle : IDisposable
{
    private readonly Func<CancellationToken, Task> _run;
    private readonly IServiceLog _log;
    private readonly TimeSpan _stopTimeout;
    private readonly Action<Exception>? _failed;

    private CancellationTokenSource? _stopping;
    private Task? _running;
    private bool _disposed;

    /// <param name="run">The work to run until cancelled.</param>
    /// <param name="log">Where lifecycle events are reported.</param>
    /// <param name="stopTimeout">
    /// How long Stop waits for the work to finish. Kept well inside the service
    /// control manager's own patience, so that a slow shutdown is reported by us
    /// rather than killed by Windows with no explanation.
    /// </param>
    /// <param name="failed">
    /// Told when the work ends with an error that nobody asked for: not when it
    /// is stopped, and not when it fails while a stop is under way, which
    /// <see cref="Stop"/> reports through <see cref="Failure"/>. It is called once,
    /// after the failure has been recorded and logged, as the last act of the
    /// work itself. <see cref="Stop"/> waits for the work, so the handler must
    /// not call <see cref="Stop"/>. Under the service control manager the handler
    /// ends the process (WindowsService), because nothing else tells Windows
    /// that a service which started has since failed.
    /// </param>
    public ServiceLifecycle(
        Func<CancellationToken, Task> run,
        IServiceLog log,
        TimeSpan? stopTimeout = null,
        Action<Exception>? failed = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(log);

        _run = run;
        _log = log;
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(20);
        _failed = failed;
    }

    /// <summary>True once Start has been called and before the work has ended.</summary>
    public bool IsRunning => _running is { IsCompleted: false };

    /// <summary>The exception that ended the work, if it failed.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>
    /// Begins the work and returns immediately.
    /// </summary>
    /// <remarks>
    /// Returning promptly is not a nicety. The service control manager expects
    /// a start to be acknowledged quickly, and blocking here would have Windows
    /// report the service as failing to start while it was in fact running
    /// perfectly well.
    /// </remarks>
    [Requirement("REQ-LIF-001",
        "Start returns as soon as the work is under way, so the service control manager is acknowledged promptly rather than waiting for a long-running operation.")]
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_running is not null)
        {
            throw new InvalidOperationException("Already started.");
        }

        _stopping = new CancellationTokenSource();
        CancellationToken token = _stopping.Token;

        _running = Task.Run(
            async () =>
            {
                try
                {
                    await _run(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // The ordinary way a stop ends.
                }
                catch (Exception ex)
                {
                    // Recorded rather than thrown into a task nobody awaits, so
                    // a service that dies on startup says why instead of
                    // vanishing.
                    Failure = ex;
                    _log.Error($"Service stopped because of an error: {ex.Message}");

                    // Nobody asked for this. Under the service control manager
                    // the handler ends the process; without it the process
                    // would stay, and Windows would show a service that has
                    // failed as running. A failure while a stop is under way is
                    // left to Stop, which is already answering Windows.
                    if (_failed is not null && !token.IsCancellationRequested)
                    {
                        try
                        {
                            _failed(ex);
                        }
                        catch (Exception handlerFailure)
                        {
                            // The handler is the last resort. If it fails too,
                            // the log is the only place left to say so.
                            _log.Error(
                                $"The handler for that failure failed in turn: {handlerFailure.Message}");
                        }
                    }
                }
            },
            CancellationToken.None);
    }

    /// <summary>
    /// Asks the work to stop and waits for it, within the timeout.
    /// </summary>
    /// <returns>True if it finished in time.</returns>
    [Requirement("REQ-LIF-001",
        "Stop signals cancellation and waits, so shutdown work such as retracting the advertisement completes before the process exits.")]
    [Requirement("REQ-LIF-002",
        "Waiting rather than returning immediately is what allows sockets to be closed and multicast groups left in an orderly way.")]
    public bool Stop()
    {
        if (_running is null || _stopping is null)
        {
            // Stopping something never started is not an error: the service
            // control manager may stop a service whose start failed.
            return true;
        }

        _stopping.Cancel();

        bool finished = _running.Wait(_stopTimeout);

        if (!finished)
        {
            _log.Warn(
                $"Shutdown did not finish within {_stopTimeout.TotalSeconds:0.#}s. "
                + "The advertisement may not have been retracted, and clients may show the printer "
                + "until its TTL expires.");
        }

        return finished;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping?.Dispose();
    }
}

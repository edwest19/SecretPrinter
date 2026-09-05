// -----------------------------------------------------------------------------
// WindowsService.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   The handshake with the Windows service control manager, and nothing else.
//
//   Every decision about starting and stopping lives in ServiceLifecycle, where
//   it can be tested. What remains here is the part no test can reach: deriving
//   from ServiceBase and answering the control manager. That is deliberately as
//   small as it can be, because it is the only code in this repository whose
//   correctness rests on a real install rather than on a test.
//
// The dependency this file introduces:
//   ServiceBase is not in the base class library. It comes from a NuGet
//   package, which makes it the first dependency in this project that is not
//   the BCL or a project in this repository. REQ-SEC-009 permits that only when
//   it is documented and justified in README.md, and it is - see the
//   dependencies note there. The alternative was roughly 150 lines of hand
//   written interop against advapi32, in exactly the code path where getting it
//   wrong means the printer never disappears from anyone's list.
//
// NOT COMPILED BY THE AUTHOR:
//   The environment this file was written in has no access to nuget.org, so
//   unlike every other file in this repository it was never built or run before
//   being handed over. Treat it accordingly.
// -----------------------------------------------------------------------------

using System.Runtime.Versioning;
using System.ServiceProcess;
using SecretPrinter.Configuration;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Runs SecretPrinter under the Windows service control manager.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsService : ServiceBase
{
    /// <summary>The name to register with, used by sc.exe and the Services console.</summary>
    public const string ServiceNameConstant = "SecretPrinter";

    private readonly ServiceLifecycle _lifecycle;
    private readonly IServiceLog _log;

    public WindowsService(ServiceConfiguration configuration, IServiceLog log)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
        ServiceName = ServiceNameConstant;

        // The control manager sends stop; it does not send pause or shutdown
        // requests we would do anything different with, so they are not
        // accepted rather than accepted and ignored.
        CanStop = true;
        CanPauseAndContinue = false;
        CanShutdown = true;

        var host = new ServiceHost(configuration, log);
        _lifecycle = new ServiceLifecycle(host.RunAsync, log);
    }

    [Requirement("REQ-LIF-001",
        "Answers the service control manager's start request by delegating to ServiceLifecycle, which returns promptly rather than blocking the handshake.")]
    protected override void OnStart(string[] args)
    {
        _log.Info($"Service '{ServiceName}' starting under the service control manager.");
        _lifecycle.Start();
    }

    [Requirement("REQ-LIF-001",
        "Answers a stop request by cancelling and waiting, so the advertisement is retracted and sockets closed before the process exits.")]
    protected override void OnStop()
    {
        _log.Info($"Service '{ServiceName}' stopping.");

        // Ask Windows for room to finish. Without this a shutdown that takes
        // longer than the control manager's default patience is killed, and the
        // goodbye records never leave.
        RequestAdditionalTime((int)TimeSpan.FromSeconds(30).TotalMilliseconds);

        if (!_lifecycle.Stop())
        {
            _log.Warn("Stopped without completing shutdown cleanly.");
        }

        if (_lifecycle.Failure is { } failure)
        {
            // Surfaced so the Services console shows a failure rather than a
            // service that merely stopped.
            _log.Error($"The service had already failed: {failure.Message}");
            ExitCode = 1;
        }
    }

    /// <summary>Treats machine shutdown exactly like a stop.</summary>
    [Requirement("REQ-LIF-003",
        "A machine shutdown retracts the advertisement the same way a service stop does, so the printer does not linger in client caches after a reboot.")]
    protected override void OnShutdown() => OnStop();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifecycle.Dispose();
        }

        base.Dispose(disposing);
    }
}

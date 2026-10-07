// -----------------------------------------------------------------------------
// WindowsService.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// A failure after the service has started now ends the process, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-07. See "When the service fails after it has started" below, and
// docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.
// The paragraph headed NOT COMPILED BY THE AUTHOR was given its note the same
// day. Reviewed by a human before merge.
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
//   (Note, 2026-10-07, by Claude, Claude Opus 5.5: true of the day it was
//   written and not since. The file has been built by every build of the
//   solution and has run as a service on two machines; see
//   docs/findings/2026-09-18-running-as-localservice.md and
//   docs/findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md.
//   The change of 2026-10-07 was compiled in Claude's workspace and has not
//   yet been run under the control manager.)
//
// When the service fails after it has started:
//   OnStart returns as soon as the work is under way, so Windows has already
//   been told the service is running when anything later fails. Until
//   2026-10-07 nothing told it otherwise: the failure was logged, the work
//   ended, and the process stayed, a service shown as running that did
//   nothing.
//
//   The process is now ended with a non-zero exit code. Microsoft's guidance
//   for a .NET Windows service says the same thing in so many words: "In order
//   for the Windows Service Management system to leverage configured recovery
//   options, we need to terminate the process with a non-zero exit code"
//   (https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service,
//   read 2026-10-07).
//
//   ServiceBase.Stop() was considered and not used. Read in the source of
//   System.ServiceProcess.ServiceController (tag v10.0.0 of dotnet/runtime):
//   after OnStart returns, ServiceBase writes its own event-log entry and only
//   then records and reports the running state, without the lock Stop() takes.
//   A failure that came in that interval would have Stop() report the service
//   stopped and the start-up code then report it running. Ending the process
//   has no such interval. It also needs nothing from the operator beyond the
//   recovery options Windows already offers for a service whose process ends.
//
//   By the time the handler runs, ServiceHost.RunAsync has ended. If it had
//   got as far as starting its parts, it has sent the goodbye, or logged that
//   it could not. The log file is flushed entry by entry (FileServiceLog), so
//   nothing written is lost by ending here.
//
//   Not yet run under the control manager. What Windows then shows for the
//   service, and which event it records, have not been measured.
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

    /// <summary>
    /// The exit code the process ends with when the service fails after it has
    /// started. The same code the console form returns for a failure after the
    /// configuration has been accepted (Program.cs).
    /// </summary>
    public const int FailedExitCode = 4;

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
        _lifecycle = new ServiceLifecycle(host.RunAsync, log, failed: EndTheProcess);
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

    /// <summary>
    /// Ends the process after the service has failed, so that Windows sees a
    /// service that is no longer running. See the header of this file.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="ServiceLifecycle"/> once the failure has been logged,
    /// and never while a stop is under way. It does not return.
    /// </remarks>
    private void EndTheProcess(Exception failure)
    {
        _log.Error(
            $"Ending the process with exit code {FailedExitCode}, so that Windows does not go on showing a "
            + "service that has failed as running. Start the service again once the cause is put right.");

        Environment.Exit(FailedExitCode);
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

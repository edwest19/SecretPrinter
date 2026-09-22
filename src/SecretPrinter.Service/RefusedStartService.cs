// -----------------------------------------------------------------------------
// RefusedStartService.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-22, for REQ-LIF-007.
// Reviewed by a human before merge.
//
// Purpose:
//   Tells the Windows service control manager that the service did not start,
//   when its configuration has been refused. It does nothing else: it opens no
//   socket, advertises nothing, and never reaches the running state.
//
// Why it exists:
//   Until 2026-09-22 a refused configuration under --service ended the process
//   before ServiceBase.Run was called. The control manager had started a
//   process that never answered it, and Windows recorded error 1053, "did not
//   respond to the start or control request in a timely fashion", for what was
//   an immediate refusal written to the service's own log. Nine such refusals
//   are recorded in
//   docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md.
//
// What the control manager is told, read from the source of
// System.ServiceProcess.ServiceController 10.0.11 (the version this project
// references), not yet observed under the control manager:
//   - OnStart below throws the refusal. ServiceBase catches it, sets the
//     service to stopped, and reports exit code 1064,
//     ERROR_EXCEPTION_IN_SERVICE, because no other exit code has been set.
//     1064 is deliberate: every other Win32 code would claim something about
//     the failure that is not true, and ServiceBase gives no way to set a
//     service-specific code.
//   - ServiceBase.Run then rethrows the refusal to its caller, Program, which
//     catches it and exits with 3.
//
// What ServiceBase may also do, stated so it is not hidden:
//   AutoLog is left at ServiceBase's default, true, exactly as it is in
//   WindowsService. With it, ServiceBase tries to write "start failed" and the
//   exception's text to the Windows Application event log, and swallows any
//   failure to do so. Whether that write succeeds when running as
//   LocalService has not been measured.
// -----------------------------------------------------------------------------

using System.Runtime.Versioning;
using System.ServiceProcess;
using SecretPrinter.Configuration;

namespace SecretPrinter.Service;

/// <summary>
/// Reports a refused configuration to the Windows service control manager as a
/// failure to start.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RefusedStartService : ServiceBase
{
    private readonly ConfigurationException _refusal;
    private readonly IServiceLog _log;

    /// <param name="refusal">The refusal, already logged by the caller.</param>
    /// <param name="log">Where the one line this class writes goes.</param>
    public RefusedStartService(ConfigurationException refusal, IServiceLog log)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        ArgumentNullException.ThrowIfNull(log);

        _refusal = refusal;
        _log = log;

        // The same name the service is registered under, so this is
        // unmistakably the SecretPrinter service answering.
        ServiceName = WindowsService.ServiceNameConstant;
    }

    /// <summary>Fails the start with the refusal.</summary>
    protected override void OnStart(string[] args)
    {
        _log.Error("Telling the service control manager that the service did not start.");
        throw _refusal;
    }
}

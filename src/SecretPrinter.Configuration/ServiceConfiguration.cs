// -----------------------------------------------------------------------------
// ServiceConfiguration.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// PrinterIppsInstance added by Claude (Anthropic model, Claude Opus 5) at the
// direction of Edwin West, 2026-09-16, for REQ-CFG-008. Reviewed by a human
// before merge.
//
// PrinterCertificateSha256 added by Claude (Anthropic model, Claude Opus 5) at
// the direction of Edwin West, 2026-09-15, for REQ-CFG-007. Its line in
// Describe() is the startup half of REQ-OBS-008. Reviewed by a human before
// merge.
//
// REQ-OBS-008 marker placed on Describe() by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-22, when the per-connection
// half was added in SecretPrinter.Proxy and SecretPrinter.Service. Until then
// it deliberately carried none, because a marker is placed only when a whole
// requirement is met. Reviewed by a human before merge.
//
// Purpose:
//   The service's settings, after loading and validation. A value of this type
//   is a promise that every interface named has been resolved to a real adapter,
//   that the client and printer sides are genuinely different networks, and that
//   nothing was quietly filled in.
//
// The rule this file exists to enforce:
//   No default may change what is advertised or where traffic is sent
//   (REQ-CFG-001). Interfaces, the printer's identity, the printer's
//   certificate fingerprint, the advertised identity, the UUID and the port are
//   all required. There is no "sensible default" for
//   any of them, because a sensible default is exactly how a service ends up
//   advertising something the operator did not intend on a network they did not
//   expect.
//
//   Tuning values - buffer size, timeouts - do have defaults, because they alter
//   only how fast or how patiently the same work is done. Those defaults are
//   named and documented below rather than hidden.
// -----------------------------------------------------------------------------

using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Configuration;

/// <summary>Thrown when configuration cannot be used. Names every problem found.</summary>
public sealed class ConfigurationException : Exception
{
    public ConfigurationException(IReadOnlyList<string> problems)
        : base(Describe(problems)) => Problems = problems;

    public ConfigurationException(string problem)
        : this([problem])
    {
    }

    /// <summary>Every problem found, so one run of the service reports them all.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string Describe(IReadOnlyList<string> problems) =>
        problems.Count == 1
            ? $"Configuration is not usable: {problems[0]}"
            : $"Configuration is not usable. {problems.Count} problems:{Environment.NewLine}"
              + string.Join(Environment.NewLine, problems.Select(p => $"  - {p}"));
}

/// <summary>Values that change speed or patience, never destination or content.</summary>
/// <param name="RelayBufferBytes">Size of the pooled copy buffer used by the relay.</param>
/// <param name="PrinterConnectTimeout">How long to wait for the printer to accept a connection.</param>
/// <param name="PrinterResolveTimeout">How long to wait for the printer to answer an mDNS query.</param>
public sealed record TuningSettings(
    int RelayBufferBytes,
    TimeSpan PrinterConnectTimeout,
    TimeSpan PrinterResolveTimeout)
{
    /// <summary>
    /// Defaults, applied only when the operator does not state otherwise.
    /// </summary>
    /// <remarks>
    /// These are permitted to have defaults because none of them alters what is
    /// advertised or where traffic goes. They are gathered here, named, so that
    /// "what does this do if I say nothing?" has one answer in one place.
    /// </remarks>
    public static TuningSettings Default { get; } = new(
        RelayBufferBytes: 64 * 1024,
        PrinterConnectTimeout: TimeSpan.FromSeconds(10),
        PrinterResolveTimeout: TimeSpan.FromSeconds(5));
}

/// <summary>Configuration that has been loaded, validated and resolved.</summary>
public sealed class ServiceConfiguration
{
    internal ServiceConfiguration(
        IReadOnlyList<MdnsInterface> clientInterfaces,
        MdnsInterface printerInterface,
        string printerInstance,
        string printerIppsInstance,
        string printerCertificateSha256,
        string advertisedInstanceName,
        string advertisedHostLabel,
        Guid advertisedUuid,
        ushort listenPort,
        TuningSettings tuning)
    {
        ClientInterfaces = clientInterfaces;
        PrinterInterface = printerInterface;
        PrinterInstance = printerInstance;
        PrinterIppsInstance = printerIppsInstance;
        PrinterCertificateSha256 = printerCertificateSha256;
        AdvertisedInstanceName = advertisedInstanceName;
        AdvertisedHostLabel = advertisedHostLabel;
        AdvertisedUuid = advertisedUuid;
        ListenPort = listenPort;
        Tuning = tuning;
    }

    /// <summary>Networks the proxy advertises on and accepts print jobs from.</summary>
    public IReadOnlyList<MdnsInterface> ClientInterfaces { get; }

    /// <summary>The network the real printer is on.</summary>
    public MdnsInterface PrinterInterface { get; }

    /// <summary>
    /// The DNS-SD instance name of the printer's <c>_ipp._tcp</c> service, e.g.
    /// "EPSON ET-3760 Series._ipp._tcp.local". The printer's capabilities come
    /// from its TXT record (REQ-RES-007).
    /// </summary>
    public string PrinterInstance { get; }

    /// <summary>
    /// The DNS-SD instance name of the printer's <c>_ipps._tcp</c> service, e.g.
    /// "EPSON ET-3760 Series._ipps._tcp.local". Connections to the printer go to
    /// the address and port this resolves to (REQ-RES-007).
    /// </summary>
    public string PrinterIppsInstance { get; }

    /// <summary>
    /// The SHA-256 fingerprint the printer's TLS certificate is required to
    /// match (REQ-CFG-007, REQ-SEC-013): the hash of the DER-encoded certificate
    /// as 64 hexadecimal digits, always upper case here whatever case was
    /// configured. A certificate fingerprint is public, not a secret.
    /// </summary>
    public string PrinterCertificateSha256 { get; }

    /// <summary>What the proxy calls itself in a client's printer list.</summary>
    public string AdvertisedInstanceName { get; }

    /// <summary>The single host label the proxy publishes, e.g. "secretprinter".</summary>
    public string AdvertisedHostLabel { get; }

    /// <summary>The proxy's own stable identifier. Never the printer's.</summary>
    public Guid AdvertisedUuid { get; }

    /// <summary>The TCP port the proxy accepts IPP connections on.</summary>
    public ushort ListenPort { get; }

    public TuningSettings Tuning { get; }

    /// <summary>
    /// A description of every setting in force, for the startup log. Includes
    /// how each interface name was resolved, so an operator can see which
    /// adapter the service actually chose.
    /// </summary>
    [Requirement("REQ-OBS-001",
        "Produces a line per interface naming its role, the configured name, and the address and index it resolved to.")]
    [Requirement("REQ-OBS-008",
        "Produces the startup line recording the SHA-256 fingerprint every connection to the printer will require.")]
    public IReadOnlyList<string> Describe()
    {
        var lines = new List<string>();

        foreach (MdnsInterface client in ClientInterfaces)
        {
            lines.Add($"client interface : {client.Name} -> {client.Address} (index {client.Index})");
        }

        lines.Add($"printer interface: {PrinterInterface.Name} -> {PrinterInterface.Address} "
                  + $"(index {PrinterInterface.Index})");
        lines.Add($"printer instance : {PrinterInstance}");
        lines.Add($"ipps instance    : {PrinterIppsInstance}");

        // The startup half of REQ-OBS-008. The per-connection half is the
        // "relaying" line, which names the protocol each handshake negotiated.
        lines.Add($"certificate pin  : SHA-256 {PrinterCertificateSha256}");
        lines.Add($"advertised as    : {AdvertisedInstanceName} on {AdvertisedHostLabel}.local:{ListenPort}");
        lines.Add($"advertised uuid  : {AdvertisedUuid}");
        lines.Add($"relay buffer     : {Tuning.RelayBufferBytes} bytes");
        lines.Add($"connect timeout  : {Tuning.PrinterConnectTimeout.TotalSeconds:0.#}s");
        lines.Add($"resolve timeout  : {Tuning.PrinterResolveTimeout.TotalSeconds:0.#}s");

        return lines;
    }
}

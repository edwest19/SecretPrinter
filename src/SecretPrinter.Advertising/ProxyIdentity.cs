// -----------------------------------------------------------------------------
// ProxyIdentity.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Who the proxy says it is. Deliberately separate from PrinterCapabilities,
//   because the central honesty rule of this project is that those two things
//   must never be mixed up:
//
//     the proxy advertises what the PROXY does,
//     using the format capabilities of the PRINTER.
//
//   Keeping identity in its own type means a reviewer checking "does this ever
//   publish the printer's identity?" reads one small file and the builder that
//   consumes it, rather than chasing strings through the whole advertisement.
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;

namespace SecretPrinter.Advertising;

/// <summary>The identity the proxy publishes for itself.</summary>
public sealed class ProxyIdentity
{
    /// <summary>Longest permitted DNS label, in bytes (RFC 1035 §2.3.4).</summary>
    public const int MaxLabelLength = 63;

    /// <param name="instanceName">
    /// The single DNS-SD instance label shown in a client's printer list. May
    /// contain spaces and dots.
    /// </param>
    /// <param name="hostLabel">
    /// The label for the host name the proxy publishes, e.g. "secretprinter"
    /// producing "secretprinter.local".
    /// </param>
    /// <param name="uuid">
    /// The proxy's own stable identifier. Must not be the printer's; the
    /// builder enforces that.
    /// </param>
    /// <param name="port">The TCP port the proxy accepts IPP connections on.</param>
    [Requirement("REQ-ADV-005",
        "Carries the proxy's own UUID as a required field, so an advertisement cannot be built without one that is distinct from the printer's.")]
    public ProxyIdentity(string instanceName, string hostLabel, Guid uuid, ushort port)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            throw new ArgumentException("Instance name must not be empty.", nameof(instanceName));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(instanceName) > MaxLabelLength)
        {
            throw new ArgumentException(
                $"Instance name encodes to more than {MaxLabelLength} bytes, which is not a valid DNS label.",
                nameof(instanceName));
        }

        if (string.IsNullOrWhiteSpace(hostLabel))
        {
            throw new ArgumentException("Host label must not be empty.", nameof(hostLabel));
        }

        if (hostLabel.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Host label is a single label and must not contain a dot. Pass 'secretprinter', not "
                + "'secretprinter.local'.",
                nameof(hostLabel));
        }

        if (System.Text.Encoding.UTF8.GetByteCount(hostLabel) > MaxLabelLength)
        {
            throw new ArgumentException(
                $"Host label encodes to more than {MaxLabelLength} bytes.", nameof(hostLabel));
        }

        if (uuid == Guid.Empty)
        {
            throw new ArgumentException(
                "The proxy needs a real UUID. An empty one would make every installation identical to "
                + "every other, which defeats the point of having one.",
                nameof(uuid));
        }

        if (port == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Advertised port must be non-zero.");
        }

        InstanceName = instanceName;
        HostLabel = hostLabel;
        Uuid = uuid;
        Port = port;
    }

    public string InstanceName { get; }
    public string HostLabel { get; }
    public Guid Uuid { get; }
    public ushort Port { get; }

    /// <summary>The published host name, e.g. "secretprinter.local".</summary>
    public string HostName => $"{HostLabel}.local";
}

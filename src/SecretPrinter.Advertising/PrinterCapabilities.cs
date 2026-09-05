// -----------------------------------------------------------------------------
// PrinterCapabilities.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   What the real printer says about itself, together with a record of where
//   that information came from.
//
// Why provenance is part of the type:
//   REQ-ADV-010 requires the advertised TXT records to be derived from a live
//   query to the printer, never from values hardcoded in source. A comment
//   saying "these came from a probe" is not enforceable. Making the source a
//   required constructor argument is: capabilities cannot exist without a
//   statement of where they were observed, and the builder rejects any whose
//   provenance is blank.
//
//   That does not make lying impossible - somebody can write a false
//   description. It makes silence impossible, which is the part a type system
//   can actually enforce.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using SecretPrinter.Spec;

namespace SecretPrinter.Advertising;

/// <summary>Where a set of printer capabilities was obtained.</summary>
/// <param name="Description">Human-readable account of how these were observed.</param>
/// <param name="ObservedAt">When the observation was made.</param>
public sealed record CapabilitySource(string Description, DateTimeOffset ObservedAt)
{
    /// <summary>Records capabilities read from a live mDNS query to a printer.</summary>
    public static CapabilitySource FromMdnsQuery(IPAddress printer, DnsNameLike instance, DateTimeOffset observedAt) =>
        new($"mDNS query to {printer} for {instance.Value}", observedAt);

    /// <summary>
    /// Records capabilities supplied by a test. Named so that it is obvious in
    /// any log or report that these were not measured.
    /// </summary>
    public static CapabilitySource ForTest(string description) =>
        new($"TEST FIXTURE: {description}", DateTimeOffset.UnixEpoch);

    public override string ToString() =>
        $"{Description} at {ObservedAt.ToString("u", CultureInfo.InvariantCulture)}";
}

/// <summary>A service instance name, kept as a string to avoid a dependency here on DNS types.</summary>
public readonly record struct DnsNameLike(string Value)
{
    public override string ToString() => Value;
}

/// <summary>What the real printer advertises, as measured.</summary>
public sealed class PrinterCapabilities
{
    /// <param name="txtStrings">TXT entries exactly as the printer published them.</param>
    /// <param name="ippPort">The port the printer's IPP service listens on.</param>
    /// <param name="source">Where this information came from. Required.</param>
    [Requirement("REQ-ADV-010",
        "Capabilities cannot be constructed without a stated source, so advertised TXT records always trace to an observation rather than to a literal in source.")]
    public PrinterCapabilities(IReadOnlyList<string> txtStrings, ushort ippPort, CapabilitySource source)
    {
        ArgumentNullException.ThrowIfNull(txtStrings);
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Description))
        {
            throw new ArgumentException(
                "Capability source description must not be empty. The advertisement must be traceable "
                + "to an observation of the printer (REQ-ADV-010).",
                nameof(source));
        }

        if (ippPort == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ippPort), "A printer's IPP port must be non-zero.");
        }

        TxtStrings = [.. txtStrings];
        IppPort = ippPort;
        Source = source;
    }

    public IReadOnlyList<string> TxtStrings { get; }
    public ushort IppPort { get; }
    public CapabilitySource Source { get; }

    /// <summary>
    /// Looks up a TXT value by key. Keys are matched case-insensitively, as
    /// RFC 6763 §6.4 requires.
    /// </summary>
    public string? Value(string key)
    {
        foreach (string entry in TxtStrings)
        {
            int split = entry.IndexOf('=', StringComparison.Ordinal);
            if (split <= 0)
            {
                continue;
            }

            if (entry.AsSpan(0, split).Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return entry[(split + 1)..];
            }
        }

        return null;
    }
}

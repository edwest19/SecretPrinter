// -----------------------------------------------------------------------------
// CertificatePin.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-15, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Purpose:
//   Decides whether a certificate presented by the printer is the one the
//   operator pinned, by SHA-256 fingerprint and by nothing else.
//
//   The printer's certificate is self-signed, so no certificate authority can
//   vouch for it and no host name can identify it. The fingerprint in
//   configuration is the whole of the trust decision (REQ-SEC-013). This type is
//   that decision, kept apart from any socket so that it can be read and tested
//   on its own.
//
//   It carries no requirement marker. The markers for REQ-SEC-013 belong on the
//   code that applies this check to every connection, because that is what the
//   requirement asks for; a comparison nobody calls satisfies nothing.
//
// What this type deliberately does not do:
//   - It does not open a connection or take part in a handshake.
//   - It does not consider chain or host-name errors. Check takes the
//     certificate and nothing else, so those errors have no way in.
//   - It does not read the certificate's validity dates. The pin identifies one
//     exact certificate, so refusing it on expiry would add no security and
//     would only set a date on which printing stops. Decided by Edwin West,
//     2026-09-15.
//   - It has no mode, flag or setting that accepts a certificate that does not
//     match (REQ-SEC-014).
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SecretPrinter.Proxy;

/// <summary>The result of checking one presented certificate against the pin.</summary>
/// <param name="Matches">
/// True only when a certificate was presented and its SHA-256 fingerprint equals
/// the pin.
/// </param>
/// <param name="PresentedSha256">
/// The presented certificate's SHA-256 fingerprint as upper-case hexadecimal, or
/// null when no certificate was presented.
/// </param>
/// <param name="Reason">
/// One sentence describing the outcome, for a log. It contains certificate
/// fingerprints, which are public, and nothing else.
/// </param>
public sealed record CertificatePinCheck(bool Matches, string? PresentedSha256, string Reason);

/// <summary>
/// The SHA-256 fingerprint a printer's certificate must have, and the check
/// against it.
/// </summary>
public sealed class CertificatePin
{
    private const int Sha256Bytes = 32;

    private readonly byte[] _sha256;

    /// <param name="sha256Hex">
    /// The SHA-256 fingerprint of the DER-encoded certificate: exactly 64
    /// hexadecimal digits, in either case, with no separators or whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The value is not in that form. The configuration loader already refuses
    /// such a value by name (REQ-CFG-007); this check exists because this is a
    /// public type and must not depend on its caller having done so.
    /// </exception>
    public CertificatePin(string sha256Hex)
    {
        ArgumentNullException.ThrowIfNull(sha256Hex);

        if (sha256Hex.Length != Sha256Bytes * 2 || !sha256Hex.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                $"A certificate pin must be exactly {Sha256Bytes * 2} hexadecimal digits with no separators "
                + $"or whitespace; the value given has {sha256Hex.Length} characters.",
                nameof(sha256Hex));
        }

        _sha256 = Convert.FromHexString(sha256Hex);
        Sha256 = Convert.ToHexString(_sha256);
    }

    /// <summary>The pinned fingerprint as upper-case hexadecimal.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Compares the SHA-256 fingerprint of <paramref name="certificate"/> with the
    /// pin.
    /// </summary>
    /// <param name="certificate">
    /// The certificate the printer presented, or null if it presented none, which
    /// is refused.
    /// </param>
    /// <remarks>
    /// The fingerprint is computed over the whole DER-encoded certificate, so it
    /// identifies that exact certificate, not merely its key or its subject.
    /// </remarks>
    public CertificatePinCheck Check(X509Certificate? certificate)
    {
        if (certificate is null)
        {
            return new CertificatePinCheck(
                false,
                null,
                $"The printer presented no certificate; the pinned SHA-256 fingerprint is {Sha256}.");
        }

        byte[] presented = certificate.GetCertHash(HashAlgorithmName.SHA256);
        string presentedHex = Convert.ToHexString(presented);

        // An ordinary comparison, not a constant-time one, on purpose. A
        // constant-time comparison protects a secret from being learned through
        // timing. Neither value here is secret: the pin is written in a
        // configuration file and logged at startup, and the printer hands its
        // certificate to anyone who connects. Using one would suggest a threat
        // that does not exist.
        bool matches = presented.AsSpan().SequenceEqual(_sha256);

        return matches
            ? new CertificatePinCheck(
                true,
                presentedHex,
                $"The printer's certificate matches the pinned SHA-256 fingerprint {Sha256}.")
            : new CertificatePinCheck(
                false,
                presentedHex,
                $"The printer presented a certificate with SHA-256 fingerprint {presentedHex}, "
                + $"which does not match the pinned fingerprint {Sha256}.");
    }
}

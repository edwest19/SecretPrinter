// -----------------------------------------------------------------------------
// CertificatePinTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-15, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Purpose:
//   Verifies the comparison between a presented certificate and the pinned
//   SHA-256 fingerprint, with no sockets and no handshake.
//
//   None of these tests carries a requirement marker. REQ-SEC-013 asks that the
//   comparison be made on every connection, and nothing here makes a
//   connection. The markers belong with the code, and the tests, that apply
//   this check to each one.
//
// Certificates:
//   Each test generates its certificates in memory with CertificateRequest.
//   Their private keys are ephemeral and never leave the process. That is also
//   why no handshake is tested here: on Windows, the TLS component cannot act as
//   a server with an ephemeral key (dotnet/runtime issue 103101), and making it
//   do so would mean writing a key into the Windows key store.
//
//   The expected fingerprint is never taken from the type under test. It is
//   computed here, independently, as SHA-256 over the certificate's raw DER
//   bytes, which is REQ-CFG-007's own definition of the value.
// -----------------------------------------------------------------------------

using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SecretPrinter.TestKit;

namespace SecretPrinter.Proxy.Tests;

internal static class CertificatePinTests
{
    private static X509Certificate2 SelfSigned(
        ECDsa key, string subject, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>A certificate that is valid now.</summary>
    private static X509Certificate2 Current(ECDsa key, string subject) =>
        SelfSigned(key, subject, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

    /// <summary>
    /// The expected fingerprint, computed without the type under test: SHA-256
    /// over the raw DER encoding, as upper-case hexadecimal.
    /// </summary>
    private static string FingerprintOf(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    [TestCase("A certificate matching the pin is accepted")]
    public static void Matching_certificate_is_accepted()
    {
        using ECDsa key = ECDsa.Create();
        using X509Certificate2 certificate = Current(key, "CN=pinned printer");
        string expected = FingerprintOf(certificate);

        // The pin is given in lower case, as an operator may write it, to show
        // that case carries no meaning.
        var pin = new CertificatePin(expected.ToLowerInvariant());
        CertificatePinCheck check = pin.Check(certificate);

        Assert.True(check.Matches, "the pinned certificate must be accepted");
        Assert.Equal(expected, check.PresentedSha256, "the presented fingerprint must be reported as measured");
        Assert.Equal(expected, pin.Sha256, "the pin must be held in upper case whatever case it was given in");
    }

    [TestCase("A different certificate is refused, and the reason gives both fingerprints")]
    public static void Different_certificate_is_refused()
    {
        using ECDsa pinnedKey = ECDsa.Create();
        using ECDsa otherKey = ECDsa.Create();
        using X509Certificate2 pinned = Current(pinnedKey, "CN=pinned printer");
        using X509Certificate2 other = Current(otherKey, "CN=some other device");

        var pin = new CertificatePin(FingerprintOf(pinned));
        CertificatePinCheck check = pin.Check(other);

        Assert.False(check.Matches, "a certificate other than the pinned one must be refused");
        Assert.Equal(FingerprintOf(other), check.PresentedSha256, "the refused certificate must be identified");
        Assert.True(check.Reason.Contains(FingerprintOf(other), StringComparison.Ordinal),
            "the reason must say which certificate was presented");
        Assert.True(check.Reason.Contains(FingerprintOf(pinned), StringComparison.Ordinal),
            "and which one was required, so an operator can tell a wrong pin from a wrong device");
    }

    [TestCase("No certificate at all is refused")]
    public static void Absent_certificate_is_refused()
    {
        using ECDsa key = ECDsa.Create();
        using X509Certificate2 pinned = Current(key, "CN=pinned printer");

        var pin = new CertificatePin(FingerprintOf(pinned));
        CertificatePinCheck check = pin.Check(null);

        Assert.False(check.Matches, "a connection that presents nothing cannot be the pinned printer");
        Assert.Null(check.PresentedSha256, "there is no presented fingerprint to report");
        Assert.True(check.Reason.Contains(pin.Sha256, StringComparison.Ordinal),
            "the reason must still say which fingerprint was required");
    }

    [TestCase("Chain and host-name errors have no way to affect the check")]
    public static void Check_takes_only_the_certificate()
    {
        // This is structural. The handshake reports chain and host-name errors
        // alongside the certificate; for a self-signed certificate those errors
        // are always present and say nothing about whether it is the pinned one
        // (REQ-SEC-013). Rather than test that they are ignored, this checks that
        // no public member of the type can be handed them at all.
        MethodInfo[] checks =
        [
            .. typeof(CertificatePin)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == nameof(CertificatePin.Check)),
        ];

        Assert.Equal(1, checks.Length, "there must be exactly one Check, so no overload can take more");

        ParameterInfo[] parameters = checks[0].GetParameters();
        Assert.Equal(1, parameters.Length, "Check must take the certificate and nothing else");
        Assert.Equal(typeof(X509Certificate), parameters[0].ParameterType,
            "the one parameter must be the certificate");

        MethodBase[] members =
        [
            .. typeof(CertificatePin).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
            .. typeof(CertificatePin).GetConstructors(BindingFlags.Public | BindingFlags.Instance),
        ];

        string[] offending =
        [
            .. members
                .SelectMany(member => member.GetParameters().Select(parameter => (member, parameter)))
                .Where(pair => pair.parameter.ParameterType == typeof(SslPolicyErrors)
                               || pair.parameter.ParameterType == typeof(X509Chain))
                .Select(pair => $"{pair.member.Name}({pair.parameter.Name})"),
        ];

        Assert.Equal(0, offending.Length,
            "no public member may accept the handshake's policy errors or chain: "
            + string.Join(", ", offending));
    }

    [TestCase("The pin identifies the whole certificate, not just its key")]
    public static void Pin_identifies_the_whole_certificate()
    {
        using ECDsa sharedKey = ECDsa.Create();
        using X509Certificate2 pinned = Current(sharedKey, "CN=pinned printer");
        using X509Certificate2 sameKey = Current(sharedKey, "CN=same key but a different certificate");

        Assert.False(FingerprintOf(pinned) == FingerprintOf(sameKey),
            "two certificates over one key must have different fingerprints for this test to mean anything");

        var pin = new CertificatePin(FingerprintOf(pinned));

        Assert.False(pin.Check(sameKey).Matches,
            "a different certificate must be refused even when it carries the pinned certificate's key");
    }

    [TestCase("An expired certificate that matches the pin is accepted, because expiry is not checked")]
    public static void Expired_matching_certificate_is_accepted()
    {
        using ECDsa key = ECDsa.Create();
        using X509Certificate2 expired = SelfSigned(
            key, "CN=expired printer", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        Assert.True(expired.NotAfter < DateTime.Now,
            "the certificate must actually have expired for this test to mean anything");

        var pin = new CertificatePin(FingerprintOf(expired));

        Assert.True(pin.Check(expired).Matches,
            "the pin identifies one exact certificate; its validity dates are deliberately not consulted");
    }

    [TestCase("A malformed pin is refused when the type is constructed")]
    public static void Malformed_pin_is_refused()
    {
        const string valid = "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF";

        var malformed = new (string Value, string Why)[]
        {
            (string.Empty, "an empty value"),
            (valid[..^1], "63 digits"),
            (valid + "0", "65 digits"),
            (valid[..40], "40 digits, the length of a SHA-1 thumbprint"),
            (string.Join(':', valid.Chunk(2).Select(pair => new string(pair))), "colons between byte pairs"),
            ($" {valid} ", "surrounding whitespace"),
            (valid[..^1] + "G", "a character that is not a hexadecimal digit"),
        };

        foreach ((string value, string why) in malformed)
        {
            Assert.Throws<ArgumentException>(
                () => _ = new CertificatePin(value),
                $"a pin with {why} must be refused, not repaired");
        }

        Assert.Throws<ArgumentNullException>(
            () => _ = new CertificatePin(null!),
            "a null pin must be refused");
    }
}

// -----------------------------------------------------------------------------
// TlsConnectionFactoryTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-17, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Purpose:
//   Verifies the connection a print job travels on: that the first thing the
//   printer receives is a TLS record and never a request line, that a handshake
//   which does not complete yields no connection and no fallback, that the
//   callback the handshake installs accepts the pinned certificate and nothing
//   else, and that nothing in this project offers a way to weaken that.
//
// What these tests cannot do, said plainly:
//   They never complete a handshake. On Windows the TLS component cannot act as
//   a server with an ephemeral in-memory key (dotnet/runtime issue 103101), and
//   giving it a usable key would mean writing one into the Windows key store -
//   machine state this project does not create. So every test here drives the
//   client half against an in-memory peer that refuses, answers wrongly, or
//   says nothing at all.
//
//   That covers the failure paths, which is the half REQ-PXY-012 is about, and
//   it exercises the real validation callback for REQ-SEC-013. It does NOT show
//   that a good handshake carries a document, or that this printer accepts what
//   SecretPrinter sends. Only the printer can show that: a correct pin printing
//   a page, and a wrong pin refused with nothing reaching the printer. That run
//   is hardware work and is recorded as a finding when it happens.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Proxy.Tests;

internal static class TlsConnectionFactoryTests
{
    private static readonly IPEndPoint PrinterEndpoint =
        new(IPAddress.Parse("192.168.12.180"), 631);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A pin for the tests that never reach a certificate. Its value is
    /// arbitrary and deliberately not the fingerprint of anything: these tests
    /// end before a certificate is presented, and a pin that looked real would
    /// invite a reader to think otherwise.
    /// </summary>
    private const string UnusedPin = "00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF";

    /// <param name="Factory">The factory under test.</param>
    /// <param name="Printer">The connection underneath, so a test can see it disposed.</param>
    /// <param name="ToPrinter">The relay's own end: what the HANDSHAKE wrote is here.</param>
    /// <param name="PrinterSide">The far end: the TEST writes the printer's answers here.</param>
    private sealed record Harness(
        TlsConnectionFactory Factory,
        FakeConnection Printer,
        RecordingStream ToPrinter,
        RecordingStream PrinterSide);

    private static Harness Build()
    {
        StreamPair link = StreamPair.Create();
        var printer = new FakeConnection(link.Left, PrinterEndpoint);

        var factory = new TlsConnectionFactory(
            new FakeConnectionFactory { OnConnect = _ => printer },
            new CertificatePin(UnusedPin));

        return new Harness(factory, printer, ToPrinter: link.Left, PrinterSide: link.Right);
    }

    private static X509Certificate2 SelfSigned(ECDsa key, string subject)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    /// <summary>SHA-256 over the raw DER encoding, computed without the code under test.</summary>
    private static string FingerprintOf(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    // ---- What goes onto the wire ---------------------------------------------

    [TestCase("The first thing the printer receives is a TLS handshake, not a request line")]
    [Requirement("REQ-PXY-010")]
    public static void First_bytes_are_a_tls_handshake()
    {
        Harness harness = Build();

        Task<IDuplexConnection> connecting =
            harness.Factory.ConnectAsync(PrinterEndpoint, Patience, CancellationToken.None);

        // A TLS record header is five bytes and the handshake type is the
        // sixth, so waiting for six is waiting for everything asserted below.
        // The handshake sends its ClientHello and then waits for an answer, so
        // nothing is being appended while these bytes are read.
        Assert.True(SpinUntil(() => harness.ToPrinter.Written.Count >= 6),
            "the handshake must send something before it waits for an answer");

        List<byte> sent = harness.ToPrinter.Written;

        Assert.Equal((byte)0x16, sent[0], "the first byte must be the TLS handshake content type (0x16)");
        Assert.Equal((byte)0x03, sent[1], "the record's protocol version must begin 0x03");
        Assert.Equal((byte)0x01, sent[5], "the first handshake message must be a ClientHello (0x01)");

        // sent[2], the version's second byte, is deliberately not asserted. RFC
        // 8446 has a TLS 1.3 client write 0x0301 here for compatibility, so its
        // value says nothing about what was offered.

        harness.PrinterSide.CloseWrite();
        _ = Failure(connecting);
    }

    [TestCase("A printer that goes away mid-handshake yields no connection at all")]
    [Requirement("REQ-PXY-012")]
    public static void Handshake_that_is_refused_yields_nothing()
    {
        Harness harness = Build();

        Task<IDuplexConnection> connecting =
            harness.Factory.ConnectAsync(PrinterEndpoint, Patience, CancellationToken.None);

        Assert.True(SpinUntil(() => harness.ToPrinter.Written.Count > 0),
            "the handshake must have started for this test to mean anything");

        // The peer closes without answering, which is what a device that will
        // not speak TLS on this port does.
        harness.PrinterSide.CloseWrite();

        Exception failure = Failure(connecting);

        Assert.False(failure is OperationCanceledException,
            "a refused handshake must be reported as a failure, not as a cancellation: IppRelay treats a "
            + "cancellation as the service stopping and would not report the job at all");

        Assert.True(harness.Printer.Disposed,
            "the connection underneath must be closed when the handshake fails, not left half-open");
    }

    [TestCase("A peer answering in plaintext is refused; there is no unencrypted fallback")]
    [Requirement("REQ-PXY-012")]
    public static void Plaintext_answer_is_refused()
    {
        Harness harness = Build();

        Task<IDuplexConnection> connecting =
            harness.Factory.ConnectAsync(PrinterEndpoint, Patience, CancellationToken.None);

        Assert.True(SpinUntil(() => harness.ToPrinter.Written.Count > 0),
            "the handshake must have started for this test to mean anything");

        // An IPP-over-HTTP answer, which is what the printer would send if this
        // were a plain connection to its IPP port. Nothing in the factory may
        // take it as an invitation to carry on unencrypted.
        byte[] plaintext = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/ipp\r\n\r\n");
        harness.PrinterSide.Write(plaintext, 0, plaintext.Length);
        harness.PrinterSide.CloseWrite();

        Exception failure = Failure(connecting);

        Assert.False(failure is OperationCanceledException, "a refused handshake must be reported as a failure");
        Assert.True(harness.Printer.Disposed, "the connection underneath must be closed");

        // Everything the attempt sent, now that it has ended. A retry in the
        // clear would appear here as a request line; nothing else in this
        // project can produce those bytes.
        string asText = Encoding.ASCII.GetString([.. harness.ToPrinter.Written]);

        foreach (string plaintextStart in new[] { "POST", "GET ", "HTTP/", "ipp://" })
        {
            Assert.False(asText.Contains(plaintextStart, StringComparison.Ordinal),
                $"nothing resembling '{plaintextStart}' may ever be sent to the printer; a failed handshake "
                + "must end the attempt, not fall back to plaintext");
        }
    }

    [TestCase("A handshake that never completes fails inside the time allowed")]
    [Requirement("REQ-PXY-007")]
    public static void Silent_printer_fails_inside_the_budget()
    {
        Harness harness = Build();

        // The peer accepts the connection and then says nothing at all, which
        // is the case a deadline exists for: nothing fails, so without one
        // nothing would ever be reported.
        Task<IDuplexConnection> connecting =
            harness.Factory.ConnectAsync(PrinterEndpoint, TimeSpan.FromSeconds(1), CancellationToken.None);

        Exception failure = Failure(connecting);

        Assert.True(failure is TimeoutException,
            "a handshake that runs out of time must fail as a TimeoutException, not a cancellation: IppRelay "
            + $"does not report cancellations as failed jobs. Got {failure.GetType().Name}: {failure.Message}");

        Assert.True(harness.Printer.Disposed, "the connection underneath must be closed");
    }

    [TestCase("The proxy assembly references no HTTP type")]
    [Requirement("REQ-PXY-011")]
    public static void Assembly_speaks_no_http()
    {
        // REQ-PXY-011 is a claim about absence, checked the way REQ-PXY-004 is:
        // by reading the compiled assembly's type-reference table. It fails if
        // anyone so much as mentions an HTTP type in this project, whether or
        // not the code path runs.
        //
        // It does not scan string literals, so it alone would not catch a
        // request line assembled by hand. First_bytes_are_a_tls_handshake and
        // Plaintext_answer_is_refused cover that, by asserting what actually
        // reaches the socket.
        string assemblyPath = typeof(TlsConnectionFactory).Assembly.Location;
        Assert.True(File_Exists(assemblyPath), "the assembly must be on disk to inspect");

        string[] forbiddenNames =
        [
            "HttpClient", "HttpClientHandler", "HttpListener", "HttpWebRequest", "HttpWebResponse",
            "HttpRequestMessage", "HttpResponseMessage", "WebClient", "WebRequest", "WebResponse",
        ];

        var found = new List<string>();

        using (FileStream stream = OpenRead(assemblyPath))
        using (var peReader = new PEReader(stream))
        {
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (TypeReferenceHandle handle in metadata.TypeReferences)
            {
                TypeReference reference = metadata.GetTypeReference(handle);
                string ns = metadata.GetString(reference.Namespace);
                string name = metadata.GetString(reference.Name);

                if (ns.StartsWith("System.Net.Http", StringComparison.Ordinal)
                    || forbiddenNames.Contains(name, StringComparer.Ordinal))
                {
                    found.Add($"{ns}.{name}");
                }
            }
        }

        Assert.Equal(0, found.Count,
            "SecretPrinter.Proxy must contain no HTTP type reference, but found: " + string.Join(", ", found));
    }

    // ---- The trust decision --------------------------------------------------

    [TestCase("The callback the handshake installs accepts the pinned certificate and nothing else")]
    [Requirement("REQ-SEC-013")]
    public static void Handshake_callback_accepts_only_the_pinned_certificate()
    {
        // This exercises the delegate a connection actually installs, taken
        // from the same builder ConnectAsync uses, rather than a copy of the
        // logic rewritten here.
        using ECDsa key = ECDsa.Create();
        using X509Certificate2 pinned = SelfSigned(key, "CN=EPSON3EA18A test double");

        var factory = new TlsConnectionFactory(
            new FakeConnectionFactory(), new CertificatePin(FingerprintOf(pinned)));

        SslClientAuthenticationOptions options = factory.BuildHandshakeOptions(PrinterEndpoint);

        RemoteCertificateValidationCallback? callback = options.RemoteCertificateValidationCallback;
        Assert.NotNull(callback, "a handshake with no validation callback would accept any certificate at all");

        // Every policy error a self-signed certificate produces, set at once.
        // The pinned certificate is accepted in spite of them, because the pin
        // is the whole of the decision - and nothing else is accepted because
        // of their absence.
        const SslPolicyErrors AllErrors =
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        Assert.True(callback!(factory, pinned, null, AllErrors),
            "the pinned certificate must be accepted even with chain and name errors reported");

        using ECDsa otherKey = ECDsa.Create();
        using X509Certificate2 other = SelfSigned(otherKey, "CN=some other device");

        Assert.False(callback(factory, other, null, SslPolicyErrors.None),
            "a certificate that is not the pinned one must be refused even with no policy error at all");

        Assert.False(callback(factory, null, null, SslPolicyErrors.RemoteCertificateNotAvailable),
            "a peer that presents no certificate cannot be the pinned printer");
    }

    [TestCase("There is no way to connect without the pin, or to supply a check of your own")]
    [Requirement("REQ-SEC-014")]
    public static void Nothing_can_weaken_the_check()
    {
        Assert.Throws<ArgumentNullException>(
            () => _ = new TlsConnectionFactory(new FakeConnectionFactory(), null!),
            "a factory without a pin would be a factory that connects to anything");

        ConstructorInfo[] constructors =
            typeof(TlsConnectionFactory).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(1, constructors.Length,
            "one public constructor, so there is no second way in that omits the pin");

        // No public member of this project ACCEPTS the handshake's own
        // machinery. Handing it out is a different direction and is allowed:
        // BuildHandshakeOptions returns a fresh copy for inspection, and
        // ConnectAsync builds its own regardless. Taking one in is what would
        // let a caller choose the trust decision, and nothing does.
        Type[] cannotBeAccepted =
        [
            typeof(RemoteCertificateValidationCallback),
            typeof(SslClientAuthenticationOptions),
            typeof(SslPolicyErrors),
            typeof(X509Chain),
        ];

        var offending = new List<string>();

        foreach (Type type in typeof(TlsConnectionFactory).Assembly.GetExportedTypes())
        {
            MethodBase[] members =
            [
                .. type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
                .. type.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            ];

            foreach (MethodBase member in members)
            {
                foreach (ParameterInfo parameter in member.GetParameters())
                {
                    if (cannotBeAccepted.Contains(parameter.ParameterType))
                    {
                        offending.Add($"{type.Name}.{member.Name}({parameter.ParameterType.Name} {parameter.Name})");
                    }
                }
            }
        }

        Assert.Equal(0, offending.Count,
            "no public member may accept the handshake's validation machinery, but found: "
            + string.Join(", ", offending));
    }

    [TestCase("Only TLS 1.2 and better may carry a document")]
    [Requirement("REQ-PXY-010")]
    public static void Protocol_floor_is_enforced()
    {
        Assert.Null(TlsConnectionFactory.UnacceptableProtocol(SslProtocols.Tls12),
            "TLS 1.2 is what this printer was measured negotiating");

        Assert.Null(TlsConnectionFactory.UnacceptableProtocol(SslProtocols.Tls13),
            "TLS 1.3 must be accepted, so a Windows 11 host needs no change to this code");

        Assert.NotNull(TlsConnectionFactory.UnacceptableProtocol(SslProtocols.None),
            "None means the handshake settled on nothing identifiable, which cannot be allowed to carry a job");

        // Naming SslProtocols.Tls and SslProtocols.Tls11 draws two diagnostics:
        // SYSLIB0039, because they are obsolete in .NET 7 and later, and
        // CA5397, because a deprecated protocol version was referenced at all.
        // Both are right about production code and wrong about this test, whose
        // entire point is that these exact versions are refused. Naming them is
        // unavoidable: a test that could not say "TLS 1.0" could not check that
        // TLS 1.0 is turned away. The suppression is as narrow as the language
        // allows - two assertions - and covers nothing else in this file or
        // anywhere in the product (REQ-DIST-007).
#pragma warning disable SYSLIB0039, CA5397
        Assert.NotNull(TlsConnectionFactory.UnacceptableProtocol(SslProtocols.Tls),
            "TLS 1.0 must not carry a document, even on a host that still enables it");

        Assert.NotNull(TlsConnectionFactory.UnacceptableProtocol(SslProtocols.Tls11),
            "TLS 1.1 must not carry a document, even on a host that still enables it");
#pragma warning restore SYSLIB0039, CA5397
    }

    [TestCase("The handshake fetches nothing from the network to make its decision")]
    [Requirement("REQ-SEC-008")]
    public static void Handshake_fetches_nothing()
    {
        Harness harness = Build();
        SslClientAuthenticationOptions options = harness.Factory.BuildHandshakeOptions(PrinterEndpoint);

        X509ChainPolicy? policy = options.CertificateChainPolicy;
        Assert.NotNull(policy, "a chain policy must be set: the default revocation mode is not NoCheck");

        Assert.Equal(X509RevocationMode.NoCheck, policy!.RevocationMode,
            "a revocation lookup would send traffic off the two configured networks");

        Assert.True(policy.DisableCertificateDownloads,
            "an AIA fetch for an unknown issuer would send traffic off the two configured networks");

        Assert.Equal(SslProtocols.None, options.EnabledSslProtocols,
            "the operating system chooses the version; the floor is enforced on what it negotiated");
    }

    // ---- Small helpers -------------------------------------------------------

    /// <summary>
    /// Waits for a connection attempt to fail and returns why, failing the test
    /// if it hangs or - worse - succeeds.
    /// </summary>
    private static Exception Failure(Task<IDuplexConnection> connecting)
    {
        try
        {
            if (!connecting.Wait(Patience))
            {
                throw new AssertionException(
                    "ConnectAsync neither returned nor failed within the test's patience; it is hung.");
            }
        }
        catch (AggregateException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }

        throw new AssertionException(
            "ConnectAsync returned a connection where it had to refuse one. A connection to a printer that was "
            + "never verified is exactly what must not exist.");
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    // Kept local, as in IppRelayTests, so this file's own use of System.IO is
    // visible and obviously read-only. The assembly under test is a different
    // assembly, and it references neither.
    private static bool File_Exists(string path) => System.IO.File.Exists(path);

    private static FileStream OpenRead(string path) => System.IO.File.OpenRead(path);
}

// -----------------------------------------------------------------------------
// TlsConnectionFactory.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-17, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Purpose:
//   Opens the connection a print job travels on: TCP first, then a TLS
//   handshake with the printer, and no job byte until both have succeeded.
//
//   The printer refuses job operations over unencrypted IPP
//   (docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md), so
//   this sits between IppRelay and TcpConnectionFactory. The relay asks for a
//   connection and is handed one that is already encrypted and already
//   verified; it then copies bytes exactly as it did before. IppRelay is
//   unchanged by this file, and knows nothing about TLS.
//
// What decides whether the printer is the printer:
//   The pinned SHA-256 fingerprint, and nothing else (REQ-SEC-013). The
//   comparison runs INSIDE the handshake's certificate validation callback, so
//   a certificate that does not match aborts the handshake itself rather than
//   being noticed afterwards. There is no path here that connects first and
//   checks second, and none that connects without checking at all
//   (REQ-SEC-014):
//
//     - The constructor requires a CertificatePin. There is no other
//       constructor, and this file reads no setting, flag, environment
//       variable or build switch.
//     - The callback returns the pin's own verdict. That is its only return
//       statement. Nothing in shipped code returns true unconditionally.
//     - Chain errors, host-name errors and expiry play no part. The printer's
//       certificate is self-signed, so no chain can vouch for it and no name
//       can identify it; the pin is the whole of the trust decision.
//       CertificatePin.Check cannot even be handed those errors - see
//       CertificatePin.cs.
//
// The protocol version, and why the operating system chooses it:
//   Microsoft's guidance for .NET is to leave EnabledSslProtocols at None and
//   let the operating system pick, so that an upgraded machine negotiates a
//   newer protocol without a code change and a deprecated one is refused by the
//   OS rather than by a list compiled into this file:
//   https://learn.microsoft.com/dotnet/core/extensions/sslstream-best-practices
//
//   That is what this does - and then it reads what was actually negotiated and
//   refuses anything below TLS 1.2 before handing the connection back. The two
//   are not in conflict: the first decides what is offered, the second decides
//   what is accepted. A Windows host with TLS 1.0 still enabled cannot quietly
//   carry a document over it.
//
//   Naming TLS 1.3 explicitly was considered and rejected. Microsoft's Schannel
//   documentation says TLS 1.3 is supported starting in Windows 11 and Windows
//   Server 2022:
//   https://learn.microsoft.com/windows/win32/secauthn/protocols-in-tls-ssl--schannel-ssp-
//   SecretPrinter runs on Windows 10 today, and what .NET does on Windows 10
//   when TLS 1.3 is named in the mask is not stated in Microsoft's
//   documentation. This project does not guess about the code that carries
//   documents. Leaving the choice to the OS avoids the question: a Windows 11
//   host will negotiate TLS 1.3 here with no change to this file, and the floor
//   below still holds.
//
// What this file never does:
//   It speaks no HTTP (REQ-PXY-011). The printer advertises _ipps._tcp, which
//   is implicit TLS: the first byte written to the socket is a TLS record, not
//   a request line, so there is no "Upgrade: TLS/1.0" exchange and nothing here
//   reads or writes the traffic the relay carries. Job bytes are handed to
//   SslStream and back untouched, and this file contains no parser of any kind.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SecretPrinter.Spec;

namespace SecretPrinter.Proxy;

/// <summary>An encrypted, pin-verified connection to the printer.</summary>
/// <remarks>
/// Constructed only by <see cref="TlsConnectionFactory"/>, and only after a
/// handshake that the pin approved. Holding one of these is itself the evidence
/// that the check passed: there is no way to make one otherwise.
/// </remarks>
public sealed class TlsConnection : IDuplexConnection
{
    private readonly SslStream _tls;
    private readonly IDuplexConnection _transport;

    internal TlsConnection(SslStream tls, IDuplexConnection transport)
    {
        _tls = tls;
        _transport = transport;
        Protocol = tls.SslProtocol;
    }

    /// <summary>
    /// The encrypted stream. Everything the relay writes to the printer goes
    /// through TLS, because this is the only stream it is given.
    /// </summary>
    public Stream Stream => _tls;

    /// <summary>The printer's endpoint, from the connection underneath.</summary>
    public EndPoint? RemoteEndPoint => _transport.RemoteEndPoint;

    /// <summary>The protocol version this handshake settled on.</summary>
    /// <remarks>
    /// Nothing logs this yet. REQ-OBS-008 asks for it per relayed connection,
    /// and that is the next commit; it is recorded here because this is the
    /// only moment it can be observed, and stating it plainly is better than a
    /// reader wondering why the class knows something it never says.
    /// </remarks>
    public SslProtocols Protocol { get; }

    public async ValueTask DisposeAsync()
    {
        // The TLS stream first: disposing it sends the close notification while
        // the socket underneath is still open. The other order would close the
        // socket and leave the printer to infer the end of the conversation.
        await _tls.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Opens connections to the printer over TLS, refusing any certificate but the
/// pinned one.
/// </summary>
public sealed class TlsConnectionFactory : IConnectionFactory
{
    private readonly IConnectionFactory _transport;
    private readonly CertificatePin _pin;

    /// <param name="transport">
    /// How the plain connection underneath is opened - in the service, a
    /// <see cref="TcpConnectionFactory"/>. Taken as an interface so the
    /// handshake can be driven over an in-memory stream in tests.
    /// </param>
    /// <param name="pin">
    /// The fingerprint the printer's certificate must have. Required: there is
    /// no constructor without it, which is what makes "on every connection"
    /// (REQ-SEC-013) a property of the type rather than a promise about how it
    /// is called.
    /// </param>
    public TlsConnectionFactory(IConnectionFactory transport, CertificatePin pin)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(pin);

        _transport = transport;
        _pin = pin;
    }

    /// <summary>
    /// Says why a negotiated protocol version is not good enough to carry a
    /// document, or null when it is.
    /// </summary>
    /// <remarks>
    /// A list of what is accepted rather than a list of what is refused. A
    /// floor expressed as "not TLS 1.0 and not TLS 1.1" admits anything nobody
    /// thought of, including <see cref="SslProtocols.None"/>, which is what a
    /// handshake reports when it settled on nothing at all.
    /// </remarks>
    public static string? UnacceptableProtocol(SslProtocols negotiated)
    {
        bool acceptable = negotiated is SslProtocols.Tls12 or SslProtocols.Tls13;

        return acceptable
            ? null
            : $"The handshake with the printer settled on {negotiated}. SecretPrinter carries print jobs "
              + "over TLS 1.2 or better only, so the connection was abandoned before any job byte was sent.";
    }

    /// <summary>
    /// Builds the options this factory hands to the handshake, including the
    /// certificate validation callback.
    /// </summary>
    /// <remarks>
    /// Public so that a test can exercise the very callback a connection
    /// installs, rather than a copy of it written in the test. Each call
    /// returns a fresh instance, so nothing a caller does to the returned
    /// object can affect a connection: <see cref="ConnectAsync"/> builds its
    /// own and accepts none from outside. That direction matters - this method
    /// hands options out, and there is no member anywhere in this project that
    /// takes them in (REQ-SEC-014).
    /// </remarks>
    public SslClientAuthenticationOptions BuildHandshakeOptions(IPEndPoint destination) =>
        BuildHandshakeOptions(destination, static _ => { });

    /// <summary>
    /// Opens a TLS connection to the printer, or throws. A returned connection
    /// has completed its handshake and passed the pin check; there is no other
    /// kind.
    /// </summary>
    /// <param name="destination">Where the printer's _ipps._tcp service was resolved to.</param>
    /// <param name="timeout">
    /// The whole budget for reaching the printer: the TCP connection and the
    /// handshake share it, and what the first spends the second does not have.
    /// Running out is reported as a <see cref="TimeoutException"/> rather than
    /// a cancellation, because IppRelay treats a cancellation as the service
    /// shutting down and would not report it as a failed job.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the service is stopping.</param>
    [Requirement("REQ-PXY-010",
        "Hands the socket to SslStream before returning it, so the first byte the relay can send is already "
        + "inside a completed TLS session; a handshake that does not complete yields no connection at all.")]
    [Requirement("REQ-PXY-011",
        "Uses implicit TLS on the printer's _ipps._tcp port. Nothing here emits or reads HTTP, and there is no "
        + "in-band upgrade exchange.")]
    [Requirement("REQ-PXY-012",
        "A failed handshake, a refused certificate or a protocol below TLS 1.2 disposes both connections and "
        + "throws with the reason, which the relay reports as a failed job. There is no unencrypted fallback.")]
    [Requirement("REQ-SEC-013",
        "Every connection built here installs a validation callback that returns the pin's verdict on the "
        + "certificate presented, so a mismatch aborts the handshake.")]
    [Requirement("REQ-SEC-014",
        "The pin is a constructor requirement, and no setting, flag, environment variable or parameter read or "
        + "accepted here can weaken or skip the comparison.")]
    public async Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        long started = Stopwatch.GetTimestamp();

        IDuplexConnection transport = await _transport
            .ConnectAsync(destination, timeout, cancellationToken)
            .ConfigureAwait(false);

        SslStream? tls = null;

        try
        {
            TimeSpan remaining = timeout - Stopwatch.GetElapsedTime(started);

            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Reaching the printer at {destination} used the whole {timeout.TotalSeconds:0.#}s allowed, "
                    + "leaving no time for the TLS handshake.");
            }

            // What the callback decided, kept so the failure can name the
            // fingerprint that was presented. SslStream's own message for a
            // rejected certificate says only that the callback rejected it,
            // which tells an operator nothing about which device answered.
            CertificatePinCheck? verdict = null;

            SslClientAuthenticationOptions options =
                BuildHandshakeOptions(destination, check => verdict = check);

            // leaveInnerStreamOpen: this type disposes the connection
            // underneath itself, in an order it chooses.
            tls = new SslStream(transport.Stream, leaveInnerStreamOpen: true);

            // The deadline is imposed on the handshake TASK, not on the token it
            // was given. A cancellation token does not reliably interrupt a
            // socket read that is already underway on Windows, so a printer that
            // accepts the connection and then says nothing could otherwise hold a
            // job open indefinitely - which is exactly what REQ-PXY-007 forbids.
            // WaitAsync always fires; disposing the stream in the catch below is
            // what actually ends the abandoned handshake.
            Task handshake = tls.AuthenticateAsClientAsync(options, cancellationToken);

            try
            {
                await handshake.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // The abandoned handshake fails once the stream is disposed.
                // Its exception is of no interest by then, and reading it here
                // keeps it from surfacing later as an unobserved task exception.
                Ignore(handshake);

                throw new TimeoutException(
                    $"The TLS handshake with the printer at {destination} did not complete within "
                    + $"{timeout.TotalSeconds:0.#}s.");
            }
            catch (Exception ex) when (verdict is { Matches: false } refused)
            {
                // The handshake failed because the pin refused the certificate.
                // Reported in the pin's own words, with the original kept as the
                // inner exception so nothing about the failure is lost.
                throw new AuthenticationException(refused.Reason, ex);
            }

            string? unacceptable = UnacceptableProtocol(tls.SslProtocol);

            if (unacceptable is not null)
            {
                throw new AuthenticationException(unacceptable);
            }

            return new TlsConnection(tls, transport);
        }
        catch
        {
            // Nothing half-open is left behind for a job that is already
            // failing. Both are closed here; on the success path above,
            // TlsConnection owns them.
            await CloseQuietlyAsync(tls, transport).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Closes what a failed attempt left open, without letting a failure to
    /// close replace the failure being reported.
    /// </summary>
    private static async Task CloseQuietlyAsync(SslStream? tls, IDuplexConnection transport)
    {
        if (tls is not null)
        {
            try
            {
                await tls.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or AuthenticationException)
            {
                // Disposing a stream whose handshake is still unwinding can
                // fail. The connection is being abandoned either way, and the
                // reason the job failed is worth more to an operator than the
                // reason the tidying up did. The socket underneath is closed on
                // the next line regardless, which is what actually matters.
            }
        }

        await transport.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the outcome of a task nobody is waiting for any more, so that its
    /// failure is not reported as unobserved.
    /// </summary>
    private static void Ignore(Task task) =>
        _ = task.ContinueWith(
            static finished => _ = finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private SslClientAuthenticationOptions BuildHandshakeOptions(
        IPEndPoint destination, Action<CertificatePinCheck> record)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return new SslClientAuthenticationOptions
        {
            // The printer's IP address, measured to produce the same
            // certificate as its host name does
            // (docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md).
            // The name plays no part in the trust decision, so the relay needs
            // no knowledge it does not already have.
            TargetHost = destination.Address.ToString(),

            // The operating system chooses the version; the floor is enforced
            // after the handshake by UnacceptableProtocol. See the file header.
            EnabledSslProtocols = SslProtocols.None,

            // Set rather than left to a default, for two reasons. A self-signed
            // certificate has no revocation list and no issuer to fetch, so both
            // lookups could only fail slowly; and REQ-SEC-008 says this service
            // transmits nothing to any host outside the two configured local
            // networks, which a revocation or AIA fetch would do. Microsoft's
            // documentation states that a chain policy supersedes
            // CertificateRevocationCheckMode, so this one setting decides both.
            CertificateChainPolicy = new X509ChainPolicy
            {
                RevocationMode = X509RevocationMode.NoCheck,
                DisableCertificateDownloads = true,
            },

            // The whole of the trust decision. Returning the pin's verdict is
            // the only thing this callback does, and a false verdict aborts the
            // handshake (REQ-SEC-013). The chain and the policy errors the
            // handshake offers are discarded unread: for a self-signed
            // certificate they are always present and say nothing about whether
            // this is the right device.
            RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            {
                CertificatePinCheck check = _pin.Check(certificate);
                record(check);
                return check.Matches;
            },
        };
    }
}

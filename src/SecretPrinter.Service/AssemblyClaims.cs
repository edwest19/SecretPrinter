// -----------------------------------------------------------------------------
// AssemblyClaims.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Declares the requirements the service's code satisfies by NOT containing
//   something.
//
// Why these markers live on the assembly:
//   "This service never creates a firewall rule" is not implemented by any
//   method. No line of code satisfies it; the compiled output as a whole does,
//   by containing no reference to any type capable of it. Attaching the claim to
//   an arbitrary method would misdirect a reviewer following the marker, so it
//   is attached to the assembly instead.
//
//   Each of these is checked by a test in SecretPrinter.Service.Tests that reads
//   the type-reference tables of every assembly of the service. Those tests were
//   verified by deliberately adding a Process.Start and an HttpClient and
//   confirming both were caught and named, so a marker here cannot outlive the
//   property it describes.
//
// Wording corrected by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-06. Reviewed by a human before merge.
//   This header and the five markers below said "shipped". Since 2026-10-04 a
//   release also holds the probe, a separate program that opens a socket and
//   that no test read, so "No shipped assembly outside SecretPrinter.Mdns and
//   SecretPrinter.Proxy references a socket type" was false as written. Each
//   now says "of the service", which is what the tests read. The probe has its
//   own requirement, REQ-SEC-016, with its own marker and tests
//   (docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;

[assembly: Requirement("REQ-SEC-003",
    "No assembly of the service outside SecretPrinter.Mdns and SecretPrinter.Proxy references a socket type, so nothing else in the service can open a port.")]

[assembly: Requirement("REQ-SEC-004",
    "No assembly of the service references System.Diagnostics.Process, so no firewall rule can be created, altered or deleted by this code.")]

[assembly: Requirement("REQ-SEC-005",
    "No assembly of the service references any Microsoft.Win32.Registry type, so the service's own code neither reads nor writes the registry.")]

[assembly: Requirement("REQ-SEC-007",
    "No assembly of the service can run a command or write the registry, which are the means by which IP forwarding and routing would be changed.")]

[assembly: Requirement("REQ-SEC-008",
    "No assembly of the service references an HTTP client type, so there is no telemetry, no update check and no analytics; the service speaks only mDNS and IPP on the two configured networks.")]

// -----------------------------------------------------------------------------
// AssemblyClaims.cs  (SecretPrinter.Probe)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-06, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Declares the requirement the probe satisfies by NOT containing something.
//
// Why the marker is on the assembly:
//   For the reason given in src/SecretPrinter.Service/AssemblyClaims.cs. "The
//   probe cannot start another program" is not implemented by any method. The
//   compiled output as a whole satisfies it, by referencing no type capable of
//   it, so the claim is attached to the assembly and not to a method that
//   would misdirect a reviewer following the marker.
//
//   It is checked by ProbeClaimsTests in SecretPrinter.Service.Tests, which
//   reads the type-reference tables of this assembly and of the two it is
//   built on. Each check was verified by adding the forbidden thing to the
//   probe and confirming the test caught it and named it
//   (docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).
//
// What this adds to the probe:
//   One attribute, and with it a reference to SecretPrinter.Spec, the assembly
//   that defines the attribute. No statement the probe executes changed.
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;

[assembly: Requirement("REQ-SEC-016",
    "This assembly, and the two it is built on, reference no type that can start a program, reach the registry, make an HTTP request or write a file. This assembly alone references a socket type, for the UDP socket Program.cs opens.")]

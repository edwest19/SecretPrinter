# Security policy

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, for the SecretPrinter project. Reviewed by a human before merge.*

## Read this first

**This software was written by an AI.** Claude, an Anthropic model, wrote the
source, the tests and this document, directed by one person. That is not
incidental to the project — it is the project. Source files name the model that
wrote them, and the claim is compiled into every assembly as metadata so it
travels with a binary separated from its repository. Section 13 of `README.md`
says what that does and does not mean.

**It is maintained by one independent developer.** There is no company behind
this, no security team, and no on-call rotation. Reports are read by one person,
in his own time.

**It has never had an outside security review.** No audit, no penetration test,
no third-party assessment. Every security claim in this repository rests on the
specification, the tests, and a reader checking them.

**It forwards print jobs between two networks.** SecretPrinter accepts IPP
connections on one network and relays the bytes to a printer on another. Your
documents pass through the machine running it, in memory. `REQ-PXY-004` requires
that no job data is written to disk, and a test reads the compiled proxy
assembly and fails if it references any file-writing type at all — but the data
does pass through the host, and anyone with control of that host is in the path.

## Reporting a vulnerability

Use **GitHub private security advisories**: open the Security tab on this
repository and choose *Report a vulnerability*. The report stays private between
you and the maintainer until there is something to publish.

Please do not open a public issue for a security problem.

If private reporting is not visible on the Security tab, the setting has not
been enabled and that is a bug in this repository's configuration — open a
public issue saying only that you have a security report and no private channel,
with no details of the finding itself.

### What to expect, stated honestly

**No response-time commitment is offered, because none could be kept.** One
person maintains this alongside everything else in his life. A reply may take
days or considerably longer.

**Silence means nothing.** It is not a denial, not an assessment, not a decision
that your report is unimportant, and not an indication of intent of any kind. It
most likely means the message has not been read yet. If a report matters and you
have heard nothing, send it again.

**You are not obliged to wait.** No embargo period is demanded, and no
disclosure timeline is imposed on you. Coordinated disclosure is appreciated and
gives a fix a chance to exist first, but this project is in no position to make
demands of people doing it a favour.

## What is in scope

- The service and libraries under `src/`.
- The diagnostic tools under `tools/`.
- The CI workflow and, once it exists, the release workflow.
- **A gap between the specification and the code.** `README.md` is the
  specification. If the code does something the specification does not describe,
  or fails to do something a `MUST` requires, that is a defect in a project whose
  entire claim is that the two agree — and worth reporting even when it has no
  direct exploit.
- **A dishonest advertisement.** `REQ-ADV-005` through `-008` require that the
  proxy never republish the printer's identity, its `Scan` capability, its
  Mopria certification or its admin URL. Anything that leaks those is a security
  matter here, not a cosmetic one.

## What is out of scope

- The printer's own firmware and web interface.
- Your router's multicast handling. Consumer ISP gateways commonly filter
  multicast between wireless clients and the wired LAN; nothing in this software
  can change that, and it is recorded in `docs/findings/`.
- IPP is plaintext. SecretPrinter advertises `_ipp._tcp` without TLS, deliberately
  and as documented, so a job crossing the network is readable by anything on the
  path. That is a stated property, not a vulnerability report. Whether to
  implement IPPS is an open question in `README.md` Section 14.
- Running the service on a network you do not control, or exposing it to the
  internet. It is designed for two home networks and nothing else.

## Supported versions

**No release has been tagged yet.** The only way to run this today is to build
it from source at `main`, and `main` carries no support promise of any kind.

Consequently: **any binary presented as a SecretPrinter release does not come
from this project.** None exists. If you have one, it came from somewhere else,
and you should not run it.

This section will be replaced with a real version table at the first release.

## Signing, when releases exist

Signed distribution on GitHub is the goal of this project, and it is written
into the specification rather than left as an intention:

| Requirement | What it says |
| --- | --- |
| `REQ-DIST-004` | Release binaries are signed with Azure Trusted Signing. |
| `REQ-DIST-005` | The release documentation states how to verify a signature, and that unsigned binaries are not official releases. |
| `REQ-DIST-006` | Every release is a tagged commit with a changelog entry. |

All three are currently unmet, and the specification checker
(`tools/SecretPrinter.SpecCheck`) fails the build because of it. That failure is
correct and is left visible on purpose: a project that asks to be trusted should
not hide the requirements it has not yet met. `docs/verification.md` lists them
under "Not yet evidenced" with what each is waiting on.

Verification instructions will be added here when there is a signature to
verify. Until then, treat every artifact as unsigned, because every artifact is.

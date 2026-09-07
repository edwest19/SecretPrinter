# SecretPrinter

**A Windows service that makes an AirPrint printer on one network reachable from
another network, without joining those networks together.**

> **Authorship.** The source code, tests and documentation in this repository
> were written by Claude (Anthropic model, Claude Opus 4.5), at the direction of
> Edwin West, and reviewed by a human before merge. This is stated plainly
> because it is the point of the project, not a disclaimer buried in a footer.
> See [Authorship and AI involvement](#13-authorship-and-ai-involvement).

> **This document is the specification.** It is not an overview of the code; it
> is the definition the code must satisfy. Every normative statement carries an
> identifier such as `REQ-ADV-001`. Those identifiers appear in the source and
> in the tests, and a tool in this repository checks that every requirement has
> both. See [Section 11](#11-how-to-validate-this-readme-against-the-code).

### About the name

There is nothing secret about SecretPrinter.

It belongs to a family of projects — SecretArqc, SecretBeans, SecretCsv,
SecretKey, SecretLetter and others — that share the prefix as a joke. The name
means the opposite of what it says: every one of them is fully open, with its
source, its reasoning, and its limitations published. The prefix is there to
make that inversion obvious once you notice it.

If you arrived expecting something covert, the rest of this document is the
correction.

---

## Read this before installing

Three things about this software that you should know before it runs on your
machine. They are here, at the top, because burying them would defeat the
purpose of the project.

**1. Your print jobs pass through the machine running this service.**
SecretPrinter is a proxy, not a router. Your phone sends the document to this
service, and this service sends it to the printer. The document is held in
memory on the proxy machine while that happens. A design that avoided this was
evaluated and rejected as unworkable — see
[Section 2](#2-why-a-proxy-and-not-a-reflector). If you are not comfortable with
print jobs transiting that machine, do not use this software.

**2. This deliberately departs from the mDNS standard.**
Multicast DNS ([RFC 6762](https://www.rfc-editor.org/rfc/rfc6762)) is designed
to be link-local: names ending in `.local` are meant to be meaningful only on
one network segment. SecretPrinter makes a service on one segment visible on
another. That is a considered departure from the specification, not an
oversight, and it is why the service confines itself to printing.

**3. It advertises what the proxy does, not what the printer does.**
The service does not copy the printer's advertisement wholesale. It publishes a
narrower one describing what the proxy will actually deliver. Capabilities the
proxy does not relay — scanning, faxing — are not advertised, even though the
printer supports them.

---

## Table of contents

1. [Problem](#1-problem)
2. [Why a proxy and not a reflector](#2-why-a-proxy-and-not-a-reflector)
3. [Architecture](#3-architecture)
4. [Requirements: advertisement (ADV)](#4-requirements-advertisement-adv)
5. [Requirements: resolution (RES)](#5-requirements-resolution-res)
6. [Requirements: relay (PXY)](#6-requirements-relay-pxy)
7. [Requirements: configuration (CFG)](#7-requirements-configuration-cfg)
8. [Requirements: security and trust (SEC)](#8-requirements-security-and-trust-sec)
9. [Requirements: lifecycle and observability (LIF, OBS)](#9-requirements-lifecycle-and-observability-lif-obs)
10. [Requirements: build and distribution (DIST)](#10-requirements-build-and-distribution-dist)
11. [How to validate this README against the code](#11-how-to-validate-this-readme-against-the-code)
12. [Repository layout](#12-repository-layout)
13. [Authorship and AI involvement](#13-authorship-and-ai-involvement)
14. [Open questions](#14-open-questions)
15. [License](#15-license)

---

## 1. Problem

A household has an AirPrint printer on one wireless network. Phones and laptops
on a second, unrelated network cannot print to it.

The two networks have no route between them. They are separate consumer
networks with separate gateways, and the second gateway offers no way to add a
static route. One machine is connected to both.

Two things must be true for AirPrint to work, and both fail here:

| Requirement | Status across separate networks |
| --- | --- |
| The client can **discover** the printer | Fails: mDNS is link-local and does not cross subnets |
| The client can **reach** the printer's IP | Fails: no route exists between the networks |

Fixing only the first produces a printer that appears in the list and then fails
on every job. This was observed directly: a tablet on the client network was
captured querying for the printer's `.local` hostname and receiving no answer.

## 2. Why a proxy and not a reflector

The obvious design is an **mDNS reflector**: copy multicast DNS packets from one
network to the other, unmodified. It is simple, and it is what most existing
tools do.

It does not work here, for a specific reason. The printer's mDNS response
contains an `A` record with its own address on its own network. A reflector
copies that response unmodified — that is what makes it a reflector. The client
then believes the printer lives at an address it has no route to, and the
connection fails at TCP.

A reflector is the correct design **when IP routing already works between the
segments** and only discovery is missing. That was not the case here, and could
not be made the case.

SecretPrinter therefore acts as a **proxy**. It advertises *itself* on the
client network, at an address clients can reach, and relays the print job to the
real printer over its other interface.

| | Reflector | Proxy (chosen) |
| --- | --- | --- |
| Requires routing between networks | Yes | No |
| Sees print job contents | No | **Yes** |
| Survives printer address changes | Yes | Yes, via runtime resolution |
| Exposes non-printing services | Yes, all of them | No |

The proxy's cost is real and is disclosed at the top of this document: job data
transits the proxy machine. The reflector's cost is that it does not work in
this situation, and additionally that it bridges every mDNS service on both
networks, not only printers.

## 3. Architecture

```
   CLIENT NETWORK                PROXY HOST                PRINTER NETWORK
                          (dual-homed Windows machine)

  ┌──────────┐                ┌──────────────┐               ┌──────────┐
  │  Phone   │                │ SecretPrinter│               │ Printer  │
  │          │  1. discover   │              │               │          │
  │          │ ──────────────>│  Advertiser  │               │          │
  │          │ <──────────────│  (mDNS)      │               │          │
  │          │  2. proxy's    │              │               │          │
  │          │     own addr   │              │  3. resolve   │          │
  │          │                │  Resolver    │ ─────────────>│          │
  │          │  4. IPP job    │  (mDNS)      │ <─────────────│          │
  │          │ ──────────────>│              │               │          │
  │          │                │  Relay       │  5. IPP job   │          │
  │          │                │  (TCP)       │ ─────────────>│          │
  └──────────┘                └──────────────┘               └──────────┘
```

1. The phone browses for AirPrint printers on the client network.
2. SecretPrinter answers, advertising itself at its own client-network address.
3. On receiving a connection, SecretPrinter resolves the real printer's current
   address by mDNS on the printer network.
4. The phone sends the IPP job to SecretPrinter.
5. SecretPrinter opens a TCP connection to the printer and relays the job.

Address resolution happens at connection time, not at startup, because printers
receive their addresses by DHCP and those addresses change. During development
the target printer moved twice.

## 4. Requirements: advertisement (ADV)

What the service publishes on the client network.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-ADV-001 | MUST | The service advertises an IPP printer service (`_ipp._tcp.local`) on each configured client interface. |
| REQ-ADV-002 | MUST | The service advertises the AirPrint subtype `_universal._sub._ipp._tcp.local`. Measured: iOS queries this subtype exclusively and never the base service type, so a responder omitting it is never discovered. |
| REQ-ADV-003 | MUST | The `SRV` record published for the proxy names a hostname owned by the proxy, and the `A` record for that hostname contains the proxy's own address on the interface the query arrived on. |
| REQ-ADV-004 | MUST NOT | The service publishes the real printer's IP address, hostname, or `A` record on the client network. |
| REQ-ADV-005 | MUST NOT | The service publishes the real printer's `UUID` TXT value. It generates and persists its own UUID, distinct per advertised service. |
| REQ-ADV-006 | MUST NOT | The service publishes any TXT key asserting a capability the proxy does not relay. As of this version that includes `Scan` and `Fax`. |
| REQ-ADV-007 | MUST NOT | The service publishes a certification claim it does not itself hold, including `mopria-certified`. |
| REQ-ADV-008 | MUST | Any `adminurl` published either points at a resource reachable from the client network, or is omitted. The printer's own `adminurl` is never copied verbatim. |
| REQ-ADV-009 | MUST | Format-capability TXT keys obtained from the printer (`pdl`, `URF`, `Color`, `Duplex`, `PaperMax`, `kind`) are published unchanged, because they describe what will actually print. |
| REQ-ADV-010 | MUST | The advertised TXT record set is derived from a live query to the printer, never from values hardcoded in source. |
| REQ-ADV-011 | MUST NOT | The service advertises on any interface not listed in configuration as a client interface. |
| REQ-ADV-012 | MUST | The service responds only to queries for service types it advertises, and ignores all other mDNS queries. |
| REQ-ADV-013 | MUST | Outgoing mDNS packets carry IP TTL 255, per RFC 6762 §11. |
| REQ-ADV-014 | MUST | The service binds UDP 5353 with `SO_REUSEADDR` and does not require exclusive use of the port, so it coexists with the Windows DNS Client service. |
| REQ-ADV-015 | MUST | The arrival interface of each received query is determined from `IP_PKTINFO`, not inferred from the socket's bound address. |
| REQ-ADV-016 | SHOULD | The advertised instance name makes the proxy's role evident to a person reading the printer list, rather than impersonating the printer. |
| REQ-ADV-017 | MUST | The service identifies legacy unicast queriers by a source port other than 5353 (RFC 6762 §6.7), answers them by unicast rather than multicast, echoes the query identifier, and caps response TTLs. |
| REQ-ADV-018 | MUST | The service receives and answers mDNS queries over IPv6 on `ff02::fb` port 5353, on each configured client interface, in addition to IPv4 on `224.0.0.251`. It answers with the same records it would send over IPv4. Measured: an iPhone on the client network queried exclusively over IPv6 and never over IPv4, so a responder holding an IPv4 socket alone never receives the question. |
| REQ-ADV-019 | MUST | Outgoing IPv6 mDNS packets carry hop limit 255, per RFC 6762 §11, as REQ-ADV-013 requires of the IPv4 TTL. |
| REQ-ADV-020 | MUST | The arrival interface of a query received over IPv6 is determined from `IPV6_PKTINFO`, not inferred from the socket's bound address, as REQ-ADV-015 requires for IPv4. |
| REQ-ADV-021 | MUST NOT | The service publishes an `AAAA` record for its own hostname while the relay accepts IPv4 connections only. Advertising an address the relay does not listen on would produce a printer that is discovered and cannot be reached. |
| REQ-ADV-022 | MUST | A response carrying an `A` record for a name that has no `AAAA` includes an `NSEC` record asserting that absence, so the client receives a definite negative rather than silence. Measured: iOS accepted the `NSEC` and opened an IPv4 connection to port 631 seventy milliseconds later. |

## 5. Requirements: resolution (RES)

How the service finds the real printer.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-RES-001 | MUST | The printer's address is resolved by mDNS query on the printer-side interface at the time a connection is relayed. |
| REQ-RES-002 | MUST NOT | The printer's IP address is read from configuration as the primary means of locating it. |
| REQ-RES-003 | MUST | The printer is identified in configuration by mDNS service instance name, which is stable across DHCP address changes. |
| REQ-RES-004 | MAY | A resolved address is cached, for no longer than the DNS TTL of the record it came from. |
| REQ-RES-005 | MUST | A resolution failure causes the relay attempt to fail with a logged, specific error. It never falls back to a guessed or remembered-indefinitely address. |
| REQ-RES-006 | MUST | Resolution queries are sent only on the configured printer-side interface. |

## 6. Requirements: relay (PXY)

How print jobs are moved.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-PXY-001 | MUST | The service accepts TCP connections on the advertised IPP port, on configured client interfaces only. |
| REQ-PXY-002 | MUST | Accepted connections are relayed to the resolved printer address on the printer's IPP port. |
| REQ-PXY-003 | MUST NOT | The service inspects, parses, modifies, or interprets the IPP payload. Bytes are relayed unchanged in both directions. |
| REQ-PXY-004 | MUST NOT | The service writes any part of a print job to disk, at any point, including on error. |
| REQ-PXY-005 | MUST | Buffers holding job data are bounded in size; the service streams rather than accumulating a whole job in memory. |
| REQ-PXY-006 | MUST | Closing either side of a relayed connection closes the other. |
| REQ-PXY-007 | MUST | Connections that cannot be established to the printer are refused promptly, with a logged reason, rather than left hanging. |
| REQ-PXY-008 | MUST | Concurrent relayed connections are supported; one client must not block another. |
| REQ-PXY-009 | MUST | Log entries about a relayed connection record endpoints, byte counts, and timing — never job content. |

## 7. Requirements: configuration (CFG)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-CFG-001 | MUST | Configuration is explicit. The service applies no default that changes what is advertised or where traffic is sent. |
| REQ-CFG-002 | MUST | Configuration names client interfaces and the printer interface separately and unambiguously. |
| REQ-CFG-003 | MUST | Configuration is validated at startup. Any inconsistency causes the service to fail to start, with a message naming the offending setting. |
| REQ-CFG-004 | MUST | Interface names given in configuration are resolved to addresses at startup, and the resolution is logged. |
| REQ-CFG-005 | MUST NOT | The service starts in a partially working state. Either every configured interface is usable, or startup fails. |
| REQ-CFG-006 | MUST | The service refuses to start if a client interface and the printer interface resolve to the same interface. |

## 8. Requirements: security and trust (SEC)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-SEC-001 | MUST NOT | The service forwards, reflects, or relays any mDNS traffic between networks. It answers queries with its own advertisement and nothing else. |
| REQ-SEC-002 | MUST NOT | The service exposes any service type other than those it is configured to proxy. Non-printing services on the printer network remain invisible from the client network. |
| REQ-SEC-003 | MUST NOT | The service opens, listens on, or relays any port other than those required for the configured print services and mDNS. |
| REQ-SEC-004 | MUST NOT | The service creates, modifies, or deletes firewall rules. Required rules are documented for the operator to apply. |
| REQ-SEC-005 | MUST NOT | The service reads or writes the Windows registry other than as required by the service control manager to run as a service. |
| REQ-SEC-006 | MUST | The service runs with the least privilege sufficient to bind its ports and join multicast groups, and documents what that is. |
| REQ-SEC-007 | MUST NOT | The service enables IP forwarding, alters the routing table, or otherwise changes host network configuration. |
| REQ-SEC-008 | MUST NOT | The service transmits anything to any host outside the two configured local networks. There is no telemetry, no update check, and no analytics. |
| REQ-SEC-009 | MUST | Every dependency is either the .NET base class library or a project inside this repository. Packages outside that require explicit documentation and justification in this file; see [Dependencies](#dependencies). |
| REQ-SEC-010 | MUST | The documentation states, prominently, that print job data passes through the proxy host. |
| REQ-SEC-011 | MUST NOT | The service acts as a general-purpose proxy, router, or NAT for any traffic. |
| REQ-SEC-012 | MUST | Relayed connections are accepted only from the configured client networks; connections from elsewhere are refused and logged. |

## 9. Requirements: lifecycle and observability (LIF, OBS)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-LIF-001 | MUST | The service starts and stops cleanly under the Windows service control manager. |
| REQ-LIF-002 | MUST | On shutdown the service leaves multicast groups, closes listening sockets, and terminates in-flight relays. |
| REQ-LIF-003 | MUST | On shutdown the service sends mDNS goodbye records (TTL 0) for everything it advertised, so clients drop it promptly. |
| REQ-LIF-004 | MUST | Fatal configuration or binding errors cause a fast, loud failure — never silent partial operation. |
| REQ-LIF-005 | MUST | Transient network errors are logged and retried, and do not terminate the service. |
| REQ-OBS-001 | MUST | Startup logs list every interface in use, its resolved address, and its role. |
| REQ-OBS-002 | MUST | Every advertisement published is logged, including the full TXT record set. |
| REQ-OBS-003 | MUST | The operator can determine, from logs alone, exactly what the service told the client network. |
| REQ-OBS-004 | MUST NOT | Logs contain print job content, at any log level. |
| REQ-OBS-005 | SHOULD | Logs note that observed mDNS traffic may contain device names, so operators handle captures accordingly. |

## 10. Requirements: build and distribution (DIST)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-DIST-001 | MUST | The project targets .NET 10 (LTS) and builds with the published SDK version recorded in `global.json`. |
| REQ-DIST-002 | MUST | The solution builds with `TreatWarningsAsErrors` and produces zero warnings. |
| REQ-DIST-003 | MUST | Continuous integration builds the solution, runs all tests, and runs the specification checker described in Section 11. |
| REQ-DIST-004 | MUST | Release binaries are signed with Azure Trusted Signing. |
| REQ-DIST-005 | MUST | The release documentation states how to verify a signature, and that unsigned binaries are not official releases. |
| REQ-DIST-006 | MUST | Every release is a tagged commit with a changelog entry. |
| REQ-DIST-007 | MUST | Analyzer suppressions are justified in place, in source, with a written reason. Blanket suppression files are not used. |
| REQ-DIST-008 | MUST | Requirements that cannot be satisfied by code are recorded in `docs/verification.md`, each naming at least one artifact by repository-relative path. |
| REQ-DIST-009 | MUST | A requirement counts as tested only when a test carrying its identifier is recorded as having passed in a test-results file. A marker on a test that was skipped or failed does not count. |
| REQ-DIST-010 | MUST | Test results are accepted only when the assemblies they were produced against are byte-identical to those being checked. Results describing code that has since been rebuilt are refused. |

## 11. How to validate this README against the code

A specification that is only prose is a specification nobody checks. This one is
machine-checkable.

### The mechanism

**Step 1 — Requirements carry identifiers.** Every row in Sections 4–10 has an
ID like `REQ-PXY-003`. These are stable; they are never renumbered. A retired
requirement is marked withdrawn, not deleted.

**Step 2 — Code declares which requirement it implements.** Types and methods in
`src/` are marked with an attribute from `SecretPrinter.Spec`:

```csharp
[Requirement("REQ-PXY-003",
    "Payload bytes are copied between streams without inspection.")]
internal sealed class IppRelay
{
    // ...
}
```

**Step 3 — Tests declare which requirement they verify.**

```csharp
[Fact]
[Requirement("REQ-PXY-003")]
public async Task Relay_does_not_alter_payload_bytes()
{
    // ...
}
```

**Step 3a — Requirements that cannot be code are evidenced instead.** Some
requirements are properties of the build or release process rather than of any
type or method: `REQ-DIST-004` (release binaries are signed) can never carry an
attribute. Those are recorded in `docs/verification.md`, one row per
requirement, each naming the artifacts that satisfy it:

```
| REQ-DIST-004 | .github/workflows/release.yml | The sign step invokes Azure Trusted Signing. |
```

SpecCheck treats such a row as coverage **only after confirming every named path
exists**. Evidence pointing at a renamed or deleted file is reported as
`EVIDENCE BROKEN` and fails the check, so a claim cannot rot unnoticed. Entries
naming no artifact are ignored, which leaves the requirement uncovered.

Evidence is permitted for any requirement, because deciding which requirements
are "really" about code is not a judgement a tool should make. The safeguard is
visibility: requirements covered this way are reported as `OK (evidence)` and
counted separately in every run, so the proportion resting on evidence rather
than tests is always in view.

**Step 3b — Test markers only count when the test actually ran.** A
`[Requirement]` attribute on a test is compiled-in metadata: it exists in the
assembly whether or not the test ever executed. A test that skips on every
machine would otherwise count as full coverage.

The test suites therefore write a results file recording what executed:

```
PASS	REQ-ADV-013	SecretPrinter.Mdns.Tests.MdnsSocketTests.Open_sets_ttl_255
SKIP	REQ-CFG-006	SecretPrinter.Mdns.Tests.MdnsSocketTests.ResolveAll_rejects_shared_interface
```

SpecCheck reads it via `--test-results` and counts a requirement as tested only
when a test carrying its identifier PASSED. Anything else is reported as
`TEST DID NOT RUN`, with the reason, and fails the check.

This was not a hypothetical. `REQ-CFG-006` needs a network adapter holding two
IPv4 addresses; its test skipped on both the development machine and the target
machine, and the matrix reported it `OK` on both until this was added.

**Step 3c — Results must describe the build being checked.** A results file
records the module version id of every SecretPrinter assembly the run
exercised. Builds are deterministic, so that id is a function of content and
changes exactly when the code does.

SpecCheck compares those fingerprints against the assemblies it scans and
refuses any file produced against different code:

```
TEST RESULTS DO NOT DESCRIBE THIS BUILD:
  SecretPrinter.Advertising has been rebuilt since these results were produced
```

This also began as a real mistake rather than a hypothetical: a results file
left over from an earlier run was passed to SpecCheck and counted as coverage
for code it did not describe. The common form is editing code, rebuilding, and
re-running the check without re-running the tests.

Running SpecCheck without `--test-results` is permitted but never silent: the
summary reports `Test execution: NOT VERIFIED` and the result line says so.

**Running it.** Continuous integration
([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) discovers the test
suites, runs each one, and passes every results file to the checker. Doing it by
hand means one `--test-results` flag per suite, which went wrong on most
attempts during development and always in the direction of reporting less
coverage than existed.

**Until every requirement is covered, CI fails.** That is intended: the check
reports every binding requirement that has no implementation, naming each one in
the coverage matrix it writes to the run summary, and a green badge over an
incomplete specification would be the exact overstatement this repository exists
to avoid. An earlier version of this paragraph stated a fixed count of such
requirements. It was correct when written and wrong within days, which is why
the number is now read from the matrix instead of asserted here. The consequence is worth stating plainly - while the build is
red for a known reason, it is less useful at signalling a *new* problem, so read
which step failed rather than trusting the colour.

**Step 4 — A tool checks the correspondence.** `tools/SecretPrinter.SpecCheck`
parses the requirement tables out of this README, reflects over the built
assemblies, and produces a coverage matrix:

```
REQ-PXY-003   MUST NOT  impl: IppRelay                  test: Relay_does_not_alter~  OK
REQ-ADV-005   MUST NOT  impl: AdvertisementBuilder      test: (none)                 MISSING TEST
REQ-ADV-016   SHOULD    impl: (none)                    test: (none)                 NOT IMPLEMENTED
REQ-DIST-002  MUST      impl: evidence: Directory.B~    test: (none)                 OK (evidence)
```

The tool exits non-zero when any `MUST` or `MUST NOT` requirement lacks either an
implementation marker or a test, and CI fails on that. `SHOULD` requirements are
reported but do not fail the build.

It also fails on the reverse error: an attribute in code citing an ID that does
not appear in this README. Requirements and code cannot drift apart in either
direction.

### For an AI agent reviewing this repository

To check whether the code matches this document:

1. Run the checker from the repository root:

   ```
   dotnet run --project tools/SecretPrinter.SpecCheck -- \
     --readme README.md --evidence docs/verification.md --search .
   ```

   It reports which requirements are implemented, tested, evidenced, or none of
   these.
2. For each requirement, read the marked implementation and judge whether it
   actually does what the requirement says. **The tool proves a marker exists
   and that a test carrying it passed. It cannot prove the marker is honest, or
   that the test verifies anything meaningful.** That judgement is the
   reviewer's.
3. Pay particular attention to the `MUST NOT` requirements. Those are claims
   about what the software *does not do*, and they are verified by reading code
   and searching for absence — for example, confirming there is no file-write
   call anywhere in the relay path (REQ-PXY-004).
4. Report any behaviour present in the code but absent from this document. Undocumented
   behaviour is a defect here even when it is harmless, because the entire
   premise of the project is that this document is complete.

### Dependencies

The repository has exactly one *declared* package reference, which brings one
transitive dependency with it. Both are listed, because "one package" would have
been true of the csproj and false of what actually ships:

| Package | How | Why |
| --- | --- | --- |
| `System.ServiceProcess.ServiceController` 10.0.11 | Declared by `SecretPrinter.Service` | Provides `ServiceBase`, without which the program cannot register with the Windows service control manager. Published by Microsoft as part of .NET, but shipped out of band rather than in the base class library. |
| `System.Diagnostics.EventLog` 10.0.11 | Pulled in by the above | `ServiceBase` writes to the Windows Event Log when a service fails to start. SecretPrinter does not use it directly. |

The alternative was hand-written interop against `advapi32` - roughly 150 lines,
untestable, in exactly the code path where a mistake means the printer is never
retracted from clients' lists. That trade was judged the worse one.

**A note relevant to REQ-SEC-005:** the second package can write to the Windows
Event Log, which is a registry-backed facility. SecretPrinter's own code
references no registry type - a test reads the compiled metadata of every
shipped assembly to confirm it - but the capability is present in the dependency
graph via `ServiceBase`'s own failure reporting. Stating that is more useful than
a claim that reads as broader than it is.

Everything else in this repository depends only on the base class library and on
other projects here. `REQ-SEC-009` is what keeps that true, and this table is
what it means by documented.

Confirm it yourself:

```
Select-String -Path (Get-ChildItem -Recurse -Filter *.csproj) -Pattern 'PackageReference'
```

## 12. Repository layout

```
SecretPrinter/
├── README.md                          This specification
├── LICENSE                            MIT                                [done]
├── SECURITY.md                        How to report a vulnerability     [done]
├── CHANGELOG.md                       Per-release history
├── .gitignore                         Ignore rules, incl. captures       [done]
├── global.json                        Pinned SDK version                 [done]
├── Directory.Build.props              Shared build settings              [done]
├── SecretPrinter.slnx                                                    [done]
│
├── docs/
│   ├── architecture.md                Design rationale in depth
│   ├── verification.md                Evidence for non-code requirements  [done]
│   ├── operating.md                   Install, firewall, privileges       [done]
│   ├── threat-model.md                What this does and does not defend against
│   └── findings/                      Dated records of what was measured
│       ├── 2026-09-01-printer-capabilities.md                        [done]
│       ├── 2026-09-01-port-5353-sharing.md                           [done]
│       ├── 2026-09-02-ios-accepts-advertisement.md                   [done]
│       ├── 2026-09-04-service-runs-unelevated.md                     [done]
│       ├── 2026-09-04-windows-service-run.md                         [done]
│       └── 2026-09-04-pre-publication-audit.md                       [done]
│
├── src/
│   ├── SecretPrinter.Spec/            The [Requirement] attribute     [done]
│   ├── SecretPrinter.Dns/             DNS wire format; no sockets        [built]
│   ├── SecretPrinter.Mdns/            mDNS socket layer                  [built]
│   ├── SecretPrinter.Advertising/     What to publish; no sockets        [built]
│   ├── SecretPrinter.Responder/       Answers queries; announces; goodbye [built]
│   ├── SecretPrinter.Resolution/      Finds the printer, on demand       [built]
│   ├── SecretPrinter.Configuration/   Loads and validates settings       [built]
│   ├── SecretPrinter.Service/         The host; wires everything up      [built]
│   ├── SecretPrinter.Proxy/           TCP relay; no file I/O at all      [built]
│
├── tools/
│   ├── SecretPrinter.Probe/           Read what a printer advertises     [built]
│   ├── SecretPrinter.Listen/          Observe mDNS; transmits nothing    [built]
│   ├── SecretPrinter.Respond/         Advertise a fake printer (test)    [built]
│   └── SecretPrinter.SpecCheck/       README-to-code coverage matrix    [built]
│
├── tests/
│   ├── SecretPrinter.TestKit/         Shared harness, no NuGet deps      [built]
│   ├── SecretPrinter.Dns.Tests/
│   ├── SecretPrinter.Mdns.Tests/      19 tests                           [built]
│   ├── SecretPrinter.Advertising.Tests/  18 tests                        [built]
│   ├── SecretPrinter.Responder.Tests/ 17 tests, fake transport           [built]
│   ├── SecretPrinter.Resolution.Tests/ 14 tests, fake transport + clock  [built]
│   ├── SecretPrinter.Proxy.Tests/     18 tests, in-memory streams        [built]
│   ├── SecretPrinter.Configuration.Tests/ 14 tests, fake adapters        [built]
│   ├── SecretPrinter.Service.Tests/   13 tests, assembly metadata scans  [built]
│
└── .github/workflows/
    └── ci.yml                         Build, test, spec-check           [done]
```

`[done]` and `[built]` mark what exists today. The rest is specified and not
yet written.

### Why the layering is what it is

`SecretPrinter.Dns` contains no socket code at all — it converts bytes to
objects and back. `SecretPrinter.Mdns` owns sockets but knows nothing about
printing. `SecretPrinter.Proxy` moves TCP bytes and knows nothing about mDNS.
Only `SecretPrinter.Service` knows the whole story.

The separation exists so that the security-relevant claims can be checked
locally. REQ-PXY-004 says no job data is written to disk; verifying that means
reading one project, not the whole solution - and a test reads that project's
compiled metadata and fails if it references any file-writing type at all. Likewise "could this ever publish
the printer's address?" is answered by reading `SecretPrinter.Advertising`,
which has no socket code and no way to learn the printer's address except from
what it is handed.

## 13. Authorship and AI involvement

This project was written by an AI. That is the point of it, and it is stated
here rather than discovered later.

**What that means concretely:**

- Source files carry a header naming the model that wrote them and the person
  who directed the work.
- Design decisions were made in conversation between a human and Claude. Where a
  decision was contested or uncertain, that is recorded in `docs/` rather than
  presented as settled.
- Claims in this document are backed by measurement where measurement was
  possible. `docs/findings/` records what was actually observed, on real
  hardware, with the tools in `tools/`. Anyone can re-run them.
- Where something was not verified, this document says so. An unverified claim
  labelled as verified would be worse than no claim.

**What it does not mean:**

- It does not mean the code is correct. AI-written code has bugs like any other.
- It does not mean a human is absent. Every merge is reviewed by a person, who
  is responsible for what ships.
- It does not mean you should trust it because of who wrote it. Trust it, if at
  all, because the specification is complete, the code matches it, and you
  checked.

## 14. Open questions

Recorded here rather than resolved silently.

| # | Question | Status |
| --- | --- | --- |
| 1 | **Does iOS require IPPS?** | Resolved: no, not for discovery. An iPhone listed a printer advertised with no IPPS service and no TLS. TLS termination is deferred, not required. Whether a job completes over plain IPP is untested until the relay exists. See [findings](docs/findings/2026-09-02-ios-accepts-advertisement.md). |
| 2 | **Can the service send mDNS responses that clients accept while another responder holds port 5353?** | Resolved: yes. Responses sent from a socket sharing 5353 with `Dnscache` were accepted by iOS, which listed the advertised printer. See [findings](docs/findings/2026-09-02-ios-accepts-advertisement.md). |
| 3 | **What should the project be called?** | Resolved: SecretPrinter. The prefix is a deliberate joke shared across a family of open projects and means the opposite of what it says. Explained under [About the name](#about-the-name). |
| 4 | **Is `mopria-certified` safe to relay?** The bytes would be identical to the printer's, and the printer is certified — but the proxy is not the certified device. | Currently forbidden by REQ-ADV-007. Revisit with evidence. |
| 5 | **What privileges does the service actually need?** | Resolved: none beyond standard user. Measured on 2026-09-04 by running unelevated and confirming every bind, join and relay succeeded. Running under `LocalService` remains unmeasured. See [findings](docs/findings/2026-09-04-service-runs-unelevated.md). |
| 6 | **How should the service register with Windows?** | Resolved: `System.ServiceProcess.ServiceController` is referenced for `ServiceBase`, documented under [Dependencies](#dependencies) as REQ-SEC-009 requires. Run with `--service` to register with the control manager, or without it as a console application. |
| 7 | **Will iOS accept an `A` record delivered over IPv6 mDNS transport, and then connect over IPv4?** | Resolved: yes. An iPhone accepted an `A` record and an `NSEC` denying `AAAA`, both delivered over `ff02::fb`, and sent IPv4 SYNs to port 631 seventy milliseconds later. IPv6 is therefore mostly a transport addition to `SecretPrinter.Mdns`. Resolution and relay are unaffected; advertisement content is not, because REQ-ADV-021 forbids publishing an `AAAA` record and REQ-ADV-022 requires the `NSEC` record that denies one. Specified as REQ-ADV-018 to REQ-ADV-022. See [findings](docs/findings/2026-09-06-ipv6-mdns-transport.md). |

## 15. License

MIT. See [LICENSE](LICENSE).

The MIT license disclaims warranty. That disclaimer is legal boilerplate and
does not lessen the obligations this document places on the code: the
specification above describes what the software does, and if the software
departs from it, that is a defect to be fixed or a specification to be
corrected — not an outcome excused by the license.

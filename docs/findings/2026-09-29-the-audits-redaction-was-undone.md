# The audit's redaction was undone

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-29. Reviewed by a human before merge.*

**Status: redacted in the current tree in the commit that adds this finding.
The git history is not rewritten, by Edwin's decision: commits from 2026-09-06
up to the one before this finding still contain the values described below.**

This finding describes the values without restating them. The
[2026-09-04 audit](2026-09-04-pre-publication-audit.md) records why: a
disclosure that quotes what it redacted has not redacted anything.

## What was found

On 2026-09-29 the household-value check kept in the session notes was run over
the whole committed tree, rather than over the files being changed. It found
four household values already on `main`:

| Value | Where | First committed |
| --- | --- | --- |
| The printer's own host name | `README.md` (open question 8); four findings (2026-09-06, 2026-09-14, 2026-09-15, 2026-09-24); `docs/operating.md`; `tools/SecretPrinter.Respond6/README.md`; five test files, as fixture data | `c5c4e07`, 2026-09-06 |
| The printer-side network's name | Three findings (2026-09-20 and both of 2026-09-22): a profile name and two quoted commands | `1718fe3`, 2026-09-21 |
| The access point's network name | One finding (2026-09-06) | `088f072`, 2026-09-06 |
| The access point's make and model | One finding (2026-09-24) | `a115809`, 2026-09-24 |

Twelve files held the host name. Every one of these values was written by
Claude: in findings that quoted commands, logs and a certificate as they were
pasted, and in test fixtures copied from measurements.

## Why it matters

The audit of 2026-09-04 redacted the printer's host name, and the tail of its
advertised `UUID`, to `000000` in their low three bytes. The ET-3760 derives
both from its MAC address. The `UUID` in this repository still carries the
first half of that identifier, so the real host name beside it gives the whole
of it back.

The audit judged the practical risk small: a MAC address is not routable, is
not a credential, and is broadcast to anyone already on that network. It
redacted anyway, because a project whose claim is care with data is better
placed having been careful with its own. It also kept the two networks'
assigned names out of the repository, writing the carriers instead, and
generalised a named access point, because which access point it was carried no
evidence. The values above undid those three choices.

## Why nothing caught it

The check is a search kept in the session notes and run by hand. In this
session it was run on the files being delivered; how earlier sessions ran it,
and since when, is not recorded in the repository, and no finding records a run
over the whole tree before this one. Nothing in the build, the tests or
continuous integration runs it. A search of the whole tree is what found these.

## What was changed

- **The printer's host name** is `EPSON000000`, the audit's placeholder, in the
  case each place used. In test fixtures it is `EPSON000000` in a `DnsName` and
  in a test certificate's subject.
- **The printer-side network's name** is `<printer-side network>` in all three
  places.
- **The access point's network name** is "the access point's Wi-Fi".
- **The access point's make and model** are "a consumer Wi-Fi router" where it
  is introduced, and "the access point" after that.

Each changed document carries a dated note that says what was replaced, without
restating it, and says nothing else was changed. Where a document gained
anything else, its note says so: the 2026-09-24 finding also gains a status
note on the iPhone's question for the printer's host name, pointing to
[2026-09-29](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md).
Each test fixture carries a comment, and each test file a header line.
Paragraphs whose lines grew past 80 characters were rewrapped; tables were not.

The files changed:

- `README.md`
- `docs/operating.md`
- `docs/findings/2026-09-06-ipv6-mdns-transport.md`
- `docs/findings/2026-09-14-ipv6-is-the-only-transport.md`
- `docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md`
- `docs/findings/2026-09-20-the-wlan-drops-and-nothing-retries.md`
- `docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md`
- `docs/findings/2026-09-22-the-withdrawn-start-on-hardware.md`
- `docs/findings/2026-09-24-ipv4-mdns-from-behind-the-access-point.md`
- `tools/SecretPrinter.Respond6/README.md`
- `tests/SecretPrinter.Proxy.Tests/TlsConnectionFactoryTests.cs`
- `tests/SecretPrinter.Resolution.Tests/PrinterReachabilityTests.cs`
- `tests/SecretPrinter.Service.Tests/AvailabilityGateTests.cs`
- `tests/SecretPrinter.Service.Tests/PrinterWatchTests.cs`
- `tests/SecretPrinter.Service.Tests/StartupWaitTests.cs`

No test asserts the host name's value; each passes it through as data, and the
certificate's pin is computed from the certificate itself. So no test can tell
the old value from the new one, and none was expected to. In Claude's
container, where the Windows-only parts cannot build, the changed suites passed:
Resolution 41 of 41, Proxy 46 of 46, and Service 65 of its 78, the 13 not run
being `SecurityClaimsTests`, which this change does not touch. The full run is
on the dev box before commit.

## The history

It is not rewritten. Commits from 2026-09-06 up to the one before this finding
contain these values, and every clone and fork made since has them. Rewriting
would mean force-pushing over a public repository, and it would change the hash
of every commit that the findings and `docs/operating.md` cite, which would make
those citations false. As the audit said of any repository with history, a
redaction after the fact does not remove anything already published. This one
stops the values being repeated in the current documents and makes the record
say so.

## Not changed

- **The carriers' names and the RFC1918 addresses,** which the audit kept on
  purpose.
- **FIOS-STB-01's own adapters** (their makers and models) **and the machine
  names.** The audit's vendor search matches the adapters' makers. They
  describe the test machine's hardware and are evidence in findings about how
  adapters behave. The audit made no decision about them; they are left as
  they are, for Edwin to decide.
- **The printer's certificate thumbprint and SHA-256 fingerprint,** in the
  2026-09-15 and 2026-09-18 findings. They identify this one printer, are not
  in the check's pattern, and are evidence in the findings on the pin. Raised,
  not acted on.
- **The example configuration's fixed `UUID`,** which is a published fixture,
  not a household value. The audit raised it as a question about the product.

## Not established

- **Whether the values are held elsewhere,** in forks, caches or search
  engines. Not checked.
- **Whether anything else is there.** This was a text search, with every line
  it matched read. The findings were not all read in full, as the audit's were.
  A value the pattern does not describe would not have been found.
- **Whether the check should run automatically,** as a test or a SpecCheck
  rule, so that the whole tree is checked on every build. Not decided; Edwin's
  call.

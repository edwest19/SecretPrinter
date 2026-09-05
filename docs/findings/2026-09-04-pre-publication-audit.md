# Finding: pre-publication privacy audit of the working tree

**Date:** 2026-09-04
**Tool:** text search over the working tree (commands below)
**Recorded by:** Claude (Anthropic model, Claude Opus 5), directed by Edwin West

The repository was about to become public. This is what a scan of it found,
including the thing it found that nobody expected to still be there.

## Why this was measured

`docs/findings/` exists because this project records what it observed on a real
home network. That is the source of its evidence and also its main disclosure
risk: mDNS captures contain whatever the network was saying at the time, and a
publication is not reversible. The working tree was searched before the first
commit rather than after the first push.

## Method

Run from the repository root. Each is reproducible by anyone.

```bash
# Identifiers that look like UUIDs
grep -rnoiE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' .

# MAC-shaped strings
grep -rnoiE '\b([0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b' .

# Network names, by ISP and by common consumer hardware vendor
grep -rniE 'tmobile|t-mobile|optimum|ssid' .
grep -rniE 'netgear|linksys|asus|tp-link|eero|orbi|unifi|arris|technicolor' .

# Hostnames captured from mDNS
grep -rnoiE '[A-Za-z0-9-]+\.local\b' .

# RFC1918 addresses
grep -rnoE '\b(192\.168|10\.|172\.(1[6-9]|2[0-9]|3[01])\.)[0-9.]+' .

# Personal names beyond the documented author
grep -rniE "\b(edwin|edwes|west)('s)?\b" .
```

All five findings documents were then read in full, because a name a search does
not know to look for is a name a search will not find.

## Result 1: a household network name reached the final check

`docs/findings/2026-09-04-service-runs-unelevated.md` named a specific access
point in the operator's home when describing which wireless path could print.

This is the one that matters. Every earlier reference to a network had already
been generalised — the two ISP-assigned SSIDs on this machine appear nowhere in
the repository, having been written throughout as "Optimum" and "T-Mobile", the
carriers rather than the networks. That generalisation was applied consistently
and deliberately, and it still missed one, because the access point was not one
of the two networks the habit had formed around. It was a third device,
mentioned once, in a sentence about something else.

**Changed.** The sentence now reads "a separate access point bridged to the
Optimum router", which was already the entire technical content. The observation
is that a bridging access point passes multicast the ISP gateway filters. Which
access point is irrelevant to that, and it survived to the last check before
publication anyway.

## Result 2: the printer's hardware identifier was published in full

The ET-3760 derives both its mDNS hostname and the tail of its advertised `UUID`
from its MAC address. The captured hostname's final three bytes and the UUID's
final three bytes were the same three bytes. Both appeared in
`docs/findings/2026-09-01-printer-capabilities.md`, in the incidental capture in
`docs/findings/2026-09-01-port-5353-sharing.md`, and as a test fixture in four
test files.

The practical risk is small. A MAC address is not routable, is not a credential,
and is broadcast continuously to anyone already on that LAN. It was redacted
anyway, for a reason that is not risk: the evidential value of that capture is
*which keys the printer advertises* and which of them REQ-ADV-005 through -008
forbid the proxy from copying. The specific bytes carry none of that. A project
whose central claim is that it is careful with other people's data is in a
better position having been careful with its own.

**Changed.** The low three bytes of both identifiers are now `000000`, in the
documents and in every test that uses those records. The hostname reads
`EPSON000000`; the UUID ends `f8d027000000`. The substitution is disclosed in a
note at the head of the capture and in a comment on each fixture, because an
undisclosed edit to a measurement record would be worse than publishing the MAC.

One doc comment had to change with it. The fixture in
`AdvertisementBuilderTests.cs` described itself as the real advertisement
"verbatim". After the redaction that sentence was false, so it was rewritten. A
redaction that leaves a stale accuracy claim behind is a redaction that has
introduced a lie.

**A mistake made while doing this, recorded rather than quietly fixed.** The
first version of that disclosure note explained the substitution by naming the
original hostname and then the replacement, so the reader could see exactly what
had changed. That is a good instinct about disclosure and it published the value
the redaction existed to remove — in the very sentence claiming to have removed
it. The note now describes the change without restating what was taken out. The
general shape of the error is worth keeping: a disclosure that quotes what it
redacted has not redacted anything, and it reads as especially trustworthy while
doing so.

The value chosen is hex rather than a visible mask such as `xxxxxx`, because
`AdvertisementBuilderTests.Colliding_uuid_is_rejected` calls `Guid.Parse` on it
and a mask would not parse. That constraint decided the form; the disclosure
notes carry the meaning instead.

## Result 3: what was clean

- **No third-party device names.** The other devices that appear in captures are
  identified by address only — a tablet at `192.168.1.204`, an iPhone at
  `192.168.1.41`. The iOS advertisement run recorded that 12 of 17 observed
  queries came from other devices and were ignored; it did not record what they
  were, which is the right amount to have written down.
- **No personal names** beyond "Edwin West" in authorship headers and the
  LICENSE copyright line, which is deliberate.
- **No MAC-shaped strings** anywhere in the tree.
- **No signing material.** No `.pfx`, `.snk`, `.p12` or `.key` files exist.
- **`.gitignore` is correct.** `secretprinter.json` is matched by exact name,
  with a comment giving both reasons: the file describes one machine, and a
  committed example UUID would have every installation advertising one identity,
  which is what REQ-ADV-005 exists to prevent. `artifacts/` is ignored, so
  SpecCheck's results files cannot be committed and later read as current — the
  stale-results failure mode this project has already been bitten by once.

## Deliberately not changed: the RFC1918 addresses

`192.168.1.234`, `192.168.12.245`, `192.168.12.180` and the interface indices 11
and 13 stay. They are not routable from outside the network they describe, and
they are load-bearing in the findings: the port-sharing measurement is a claim
about two specific interfaces receiving multicast correctly, and it cannot be
read without them. Removing them would cost real clarity to remove no real
exposure. Recorded as a decision so that it reads as one, rather than as an
oversight.

## Raised, not acted on

`ConfigurationLoader.ExampleJson` contains a fixed UUID, and
`--print-example-config` writes it out unchanged. `docs/operating.md` §3 tells
the reader to generate their own with `[guid]::NewGuid()` before configuring,
and the loader's validation message says so too — but nothing rejects the
example value. A reader who redirects the example to `secretprinter.json`, edits
the interface names and stops there ships the same identity as everyone else who
did the same, which is the condition REQ-ADV-005 exists to prevent.

This was noticed during the audit and is out of its scope. It is a question
about the shipped product, not about what the repository discloses. It should be
settled before a release, not before a push.

## A note on model attribution

Files written before this session carry headers naming Claude Opus 4.5. This
document and the edits made alongside it were written by Claude Opus 5. Existing
headers were left alone, because they are accurate about who wrote those files.
The repository will therefore name more than one model, and that is correct
rather than untidy. A project that asks to be trusted about authorship should
not round its own authorship off for neatness.

## What this audit does not establish

Stated plainly, because a clean scan reads as a guarantee and this one is not.

- **A text search finds what it was told to look for.** These patterns cover
  UUIDs, MACs, one carrier pair, nine consumer hardware vendors, `.local`
  hostnames and RFC1918 addresses. A household device named after a person, a
  street or an inside joke would match none of them. Reading all five findings
  in full is what caught the access point; the searches did not.
- **`secretprinter.json` was never inspected.** It was excluded from the copy
  this audit ran against, deliberately, so that the real configuration was never
  handed over at all. This audit confirms that `.gitignore` names it. It cannot
  confirm what it contains, and it cannot confirm git will actually skip it.
  That is a separate check, run after `git init`:

  ```powershell
  git check-ignore -v secretprinter.json
  git status --porcelain
  ```

  The first must name the ignoring rule. The second must not list the file.

- **No git history was examined, because none existed.** This audit describes a
  working tree before its first commit. It says nothing about any repository
  with history, where a redaction after the fact does not remove anything.
- **The edited tests were not compiled or run.** The environment this audit ran
  in has no .NET SDK and no route to obtain one. Consistency was checked by
  inspection: every assertion referencing a redacted value was confirmed to
  match its fixture, and the redacted UUID was confirmed to be a parseable GUID.
  That is not the same as a green test run. **The suite must pass before this is
  committed**, and if it does not, the cause is here.

## Numbers that nothing was checking

Three separate counts were wrong or unverifiable when this session started. None
of them was caught by SpecCheck, because SpecCheck verifies requirement markers
against code, not prose against reality. They are recorded together because they
share one cause.

**The layout section undercounted the test suites.** Section 12's tree claimed
17, 14, 14 and 8 tests for the Advertising, Responder, Proxy and Service suites.
The measured counts are 18, 17, 18 and 13. The tree summed to 100; the suites
run 113. Every error was an undercount, which is what happens to a number
written once while tests keep being added. Corrected against a counted run.

**A test total was being carried that no file contained.** The figure "114
tests" was in use in conversation. Searching the repository for it found
nothing — the only match for that range was an SDK version, `10.0.111`, in a
REQ-DIST-001 evidence row. The suites report 113 tests and SpecCheck reports 114
*records*, and both are right: `SecurityClaimsTests.No_process_execution`
carries two `[Requirement]` markers, REQ-SEC-004 and REQ-SEC-007, so it writes
two rows. The results format is one row per requirement outcome, not one per
test, which is what lets a single absence proof be credited to two requirements.
The correct phrasing is "113 tests across 7 suites, 114 requirement records".
Neither number was wrong; using them interchangeably was.

**The verification command was wrong, and looked right.** `dotnet test` was
recommended for confirming the redaction. Every suite here is a console
application — `<OutputType>Exe</OutputType>`, no test SDK, run via
`dotnet run -- --results <path>` as `ci.yml` does. `dotnet test` therefore
discovered no test projects, built the solution, printed `Build succeeded`, and
ran nothing. That output is easy to read as a pass.

This is the same failure the results-file mechanism exists to prevent, arrived
at from outside the mechanism. `ci.yml` throws when it finds no suites,
precisely because a run that tests nothing must not resemble a run that tests
everything — and a hand-typed command walked straight into that shape anyway.
The tooling was not at fault and could not have helped: it was never invoked.

The tests were then run properly. All 113 passed in Release with zero skips,
including the three that assert directly on the redacted values. SpecCheck
reported 0 stale assemblies and 0 orphaned markers, so the redaction moved no
coverage number.

**What remains unmeasured about this.** The corrected counts were true when
counted and nothing checks them. They will drift again the next time a test is
added, in exactly the way they drifted before, and this document will then be
wrong too. Making them checkable — having SpecCheck compare the layout's stated
counts against the results files, or removing the per-suite numbers in favour of
something generated — is unresolved. Recorded so that the next wrong number is
recognised as a recurrence rather than a surprise.

## The lesson worth keeping

The two networks in this project got scrubbed everywhere, every time, because
they were the two things everyone knew were sensitive. The access point was
mentioned once, in passing, in a sentence about multicast filtering — and it
went straight through a careful habit, because the habit had a shape and this
did not fit it.

Scrubbing that runs on recognition catches what it recognises. The last check
before publication has to be someone reading the whole thing, and it has to
assume the earlier passes were good, thorough, and incomplete.

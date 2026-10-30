# An answer is believed for at most 120 seconds

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-26. Reviewed by a human before merge.*

**Status: built and measured against the code by tests; not run on a network,
and no release carries it. No answer about the printer is now believed for
longer than 120 seconds, whatever TTL its records carry (README
`REQ-RES-013`): a lookup's answer holds for the shorter of its SRV and A
records' TTLs, never more than 120 seconds, and the printer's SRV and address
records heard between lookups are each held for their TTL, never more than
120 seconds. 120 seconds is the TTL RFC 6762 §10 says such records SHOULD
carry. When a lookup's answer is cut, the TTL received is kept and the startup
log line says both. Until now an answer held for its TTL however long;
releases `0.1.0` to `0.5.0` do so.**

*(Status note, later on 2026-10-26, by Claude, Claude Opus 5.5: committed and
pushed as `de1d171`; CI run 169 passed. Run once on a network that afternoon,
on FIOS-STB-01, in a build of `de1d171` made on the development machine and
not signed: both startup answers had a lifetime of 120 seconds, the startup
lines ended `TTL 120s).` with nothing capped, and a page printed from an
iPhone. No release carries it. See [the finding of the
run](2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md).)*

*(Status note, 2026-10-30, by Claude, Claude Opus 5.5: release 0.6.0, published
that day, is the first release to carry it. Installed on FIOS-STB-01 the same
day, in place of the build of `0b51fcc`, its two startup answers each had a
lifetime of 120 seconds and nothing was capped. See
[the finding of the sixth release](2026-10-30-the-sixth-release-was-published-and-installed-on-fios-stb-01.md).)*

No IPv6 address, hardware address, network name or device name appears in this
finding, and neither does any machine's address on either network or the
printer's host name. The addresses in the tests are made up; the printer's
host name in them is redacted to `EPSON000000`, as in
[the audit of 2026-09-04](2026-09-04-pre-publication-audit.md).

## Why this was done

[The finding of 2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md)
raised it, under "Raised, not decided": "No TTL is capped. An answer, or now a
record heard, with a very long TTL holds the printer reachable without a
question until 80% of it." The handoff of 2026-10-25 listed it second on
Claude's list, after the change of earlier on 2026-10-26
([finding](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md)).
That change is on `main` as `1b4358b` and has not been run on a network; Edwin
West chose on 2026-10-26 that it wait, and be run on the network together
with this one. This change was not put to him as a choice; he reviews it
before the commit.

## What an answer's lifetime decides

Read from the code on 2026-10-26.

- **The cache** (`REQ-RES-004`): a print job's lookup reuses a cached answer,
  address included, while it is within its lifetime, and does not ask.
- **The watch** (`REQ-RES-008`): while the printer is held reachable, the
  service next asks at 80 to 82% of the answer's lifetime, and holds the
  printer unreachable when the lifetime is over with no answer.
- **What is heard between lookups** (`REQ-RES-011`): an SRV or address record
  is held for its TTL, and while the printer is held unreachable, the two
  together make it reachable with the shorter lifetime left.

So before this change, a device on the printer-side network that answered one
of the service's questions with records carrying a TTL of a year would have
had its address reused by print jobs, and the printer held reachable, for
most of a year before the service asked again. A forged address still meets
the pinned certificate (`REQ-SEC-013`), so the jobs would fail rather than
reach the device. That was read from the code; it has not been seen.

## What RFC 6762 says

Read on 2026-10-26 through a fetch tool, from
`https://www.rfc-editor.org/rfc/rfc6762.txt`. The tool quotes at most about 125
characters at a time; it gave this sentence in three pieces and said they were
contiguous, and they are joined here with the RFC's line breaks as spaces.

§10, Resource Record TTL Values and Cache Coherency, its first sentence:

> As a general rule, the recommended TTL value for Multicast DNS resource
> records with a host name as the resource record's name (e.g., A, AAAA, HINFO)
> or a host name contained within the resource record's rdata (e.g., SRV,
> reverse mapping PTR record) SHOULD be 120 seconds.

An answer is made of exactly such records: the instance's SRV record, whose
rdata names the host, and the host's A record. The same section recommends 75
minutes for other records, the TXT record among them.

**What was not found.** In the parts of the RFC the tool returned, no sentence
says a querier may or should cap the TTL of a record it receives; the tool
named the places that shorten a received record's life for other reasons (a
goodbye, §10.1; the cache-flush bit, §10.2; a change of topology, §10.3) and
the cap of the query interval at 60 minutes (§5.2), which is not a TTL. The
tool cut the text off before §21, Security Considerations, in both of its
forms, so that section was not read. The cap is therefore Claude's choice,
made from §10's recommendation, and is not presented as something the RFC
requires.

## What the service does now

- **`ResolvedPrinter.LongestBelievedTtl`**, 120, new, with §10's sentence in
  its comment.
- **A lookup** (`PrinterResolver.ReceiveAnswerAsync`) still takes the shortest
  TTL of the SRV and A records it uses, then gives the answer the smaller of
  that and 120. When that cut the TTL, the answer keeps the TTL received in a
  new field, `ResolvedPrinter.CappedFrom`.
- **What is heard between lookups** (`PrinterAnnouncements`): an SRV or
  address record is held until its TTL or 120 seconds have passed, whichever
  comes first. The TXT record is held for its own TTL, as before: it decides
  only whether its strings are carried, never whether the printer can be
  reached. `CappedFrom` is not set on these answers; they are not logged with
  their lifetime.
- **The startup log** names both answers in full (`Capabilities from …` and
  `Connections to …`), and the line ends `TTL 120s)` as before, or `TTL 120s,
  capped from 4500s)` when the cap cut a TTL of 4500, so the cap is never shown
  as the TTL the printer sent.

Not changed: the cache, the watch and the reachability state, which already
ran on the answer's lifetime; anything the service sends; the TTLs the service
gives its own records.

## Why this should change nothing for this printer

On 2026-10-21 the service's questions to the printer came 96.7 to 98.7
seconds apart, each answered within 0.3 seconds
([finding](2026-10-21-the-printer-announces-itself-when-it-starts.md), "What
the listener heard"). That fits 80 to 82% of an answer lifetime of 120
seconds, and an answer of 120 seconds is not changed by the cap.

Not shown: which TTL the printer gives its SRV and its A record each on its
own. The listener printed the TTLs in each reply, 120 and 4,500, without
saying which record carried which. If the printer gave its SRV a TTL longer
than 120 seconds, an SRV heard in one announcement would now be forgotten
after 120 seconds, and an address heard alone after that would not complete
it. On 2026-10-21 each of the printer's announcements came as three
datagrams within about a tenth of a second, the first beginning with its
address records; which of them carried the SRV of the `_ipps._tcp` instance
was not shown.

## Security

Nothing new is sent, opened or parsed. The change only shortens how long the
service believes what it is told.

- **A device on the printer-side network** can still answer the service's
  questions with records of its own. A single answer now holds for at most
  120 seconds; after that the service asks again, and the printer can answer.
  To keep its address in use, the device has to go on answering.
- **The cache** can no longer hold an address for longer than 120 seconds,
  whoever sent it. That bounds how long a printer that moved is looked for at
  its old address: the failure `REQ-RES-004` exists to prevent.

## The tests

Seven new tests, marked `REQ-RES-013`: four in `PrinterResolverTests.cs` and
three in `PrinterAnnouncementsTests.cs`. They were written first and run
against code in which `ResolvedPrinter` had the new constant and field, so
that the tests compiled, and neither producer capped anything: six failed and
one passed.

| Test | Against the code without the cap |
| --- | --- |
| A lookup believes an answer for no longer than 120 seconds, however long a TTL its records carry | failed |
| An answer whose TTL is 120 seconds keeps it, and is not marked as capped | passed |
| A printer that moved is found at its new address 120 seconds after an answer with a long TTL | failed |
| An answer's description says when its lifetime was capped, and from what | failed |
| An SRV and an address heard with TTLs longer than 120 seconds are believed for 120 | failed |
| An SRV heard with a long TTL is forgotten after 120 seconds, so a later address does not complete it | failed |
| An address heard with a long TTL is forgotten after 120 seconds, so a later SRV does not complete it | failed |

The one that passed says what must not change: an answer at the cap keeps its
TTL and is not marked as capped.

## Breaking each claim on purpose

Six mutations, each made alone, built, run against the resolution suite, and
undone; each file was compared with a saved copy after.

| Mutation | Caught by |
| --- | --- |
| The lookup's cap removed | the long-TTL lookup test, the moved-printer test, the description test |
| `CappedFrom` never set | the long-TTL lookup test, the description test |
| The cap removed from what is heard between lookups | all three announcement tests |
| Only the SRV heard held uncapped | the SRV test |
| Only an address heard held uncapped | the address test |
| The description without its capped clause | the description test |

## What a run on a network would need to show

The same as for the change of earlier on 2026-10-26, with which it will be
run: that the service finds the printer at startup and offers it, that a page
prints, and over the hours after that it does not lose the printer more often
than the build before it. In the startup log, the two lines naming the
printer's answers would show whether the printer's answers are ever capped:
`TTL 120s)` if not.

## What this does not show

- **Any of it on a network, on Windows, or in a release build.**
- **That 120 seconds is right for every printer.** A printer that follows
  §10 is not affected. One that gives its SRV or A record a longer TTL is
  asked more often than its TTL asks for: at most about every 96 seconds
  while it is held reachable, as this printer already is.

## Raised, not decided

Carried, unchanged: the startup wait does not use what is heard; goodbyes are
heard and not acted on, by Edwin's choice; the conflict check of the
responder takes a response from any port (raised in the finding of earlier on
2026-10-26).

## How it was measured

- **In Claude's workspace, on Linux,** with .NET SDK 10.0.113 and runtime
  10.0.13, Ubuntu's build, and the service's one package taken from the SDK's
  own folder in scratch copies of the repository, as in the finding of
  earlier on 2026-10-26. That is not the build a release makes.
- **From `1b4358b`, before this change**: 28, 27, 49 (9 skipped, all IPv6),
  62, 79, 205 and 126 (1 skipped) tests, every other test passing.
- **After this change**: the same, except the resolution suite, 86. SpecCheck:
  110 requirements, 590 test records, 92 requirements with a passing test, 91
  covered by code, 17 by evidence, 107 binding of which 105 covered, and the
  same 2 binding gaps as before, `REQ-ADV-019` and `REQ-ADV-020`, whose tests
  need IPv6. The coverage matrix gains one row, `REQ-RES-013`, `OK`, and no
  other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Resolution/PrinterResolver.cs`: `ResolvedPrinter` gains
  `LongestBelievedTtl` and `CappedFrom`, and its description says when the
  lifetime was capped; the lookup caps the lifetime, with its `REQ-RES-013`
  marker; a section in the header.
- `src/SecretPrinter.Resolution/PrinterAnnouncements.cs`: `BelievedUntil`, with
  its marker, used for the SRV and address records; two passages of the
  header.
- `tests/SecretPrinter.Resolution.Tests/PrinterResolverTests.cs`: four tests,
  and the header.
- `tests/SecretPrinter.Resolution.Tests/PrinterAnnouncementsTests.cs`: three
  tests, and the header.
- `README.md`: a new row, `REQ-RES-013`; notes on `REQ-RES-008` and
  `REQ-RES-011`; a Known problems entry, and the section's note.
- Status notes on the findings of
  [2026-10-22](2026-10-22-the-service-hears-the-printer-announce-itself.md)
  and of [earlier on 2026-10-26](2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md).
- This finding.

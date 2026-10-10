# A question that carries its answer is no longer answered

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-10. Reviewed by a human before merge.*

**Status: corrected in the change this finding comes with, and measured against
the code only. The service's mDNS responder now reads the Answer section of a
query. A record the query already carries there, with at least half the TTL the
service publishes it with, is left out of the answer, as RFC 6762 §7.1
requires. This is the first of the four things recorded on 2026-10-07 as not
done ([finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md),
item 1). It is stated as README `REQ-ADV-026`. Thirteen new tests show it in
Claude's workspace, with the test kit's fake transport in place of a network.
It has not been run on a network, and what an iPhone does with it is not
known.**

*(Status note, 2026-10-10, later the same day, by Claude, Claude Opus 5.5: run on
a network once, on the test machine, with FIOS-STB-01's service stopped. In 9
minutes 41 seconds an iPhone's 26 queries that carried every answer were not
answered, 8 others were, and a page printed. The stop summary's line was
written by `ServiceHost.RunAsync`, which no test runs. See
[the finding of the run](2026-10-10-questions-carrying-their-answers-went-unanswered-on-a-network.md).)*

No address of a real device appears in this finding. The addresses in the
tests are made up.

## Why this was done

- **It is a requirement of the standard, and it was seen on a network.** On
  2026-10-02, in run 1, eight of an iPhone's questions already carried the
  service's `PTR` as a known answer with nearly its full TTL, and the service
  answered each; once more in run 4 and eleven times in run 6
  ([finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md),
  "Also seen", item 2).
- **Edwin West decided on 2026-10-07** that release 0.1.0 would ship with the
  four stated as known problems, and that they would be built afterwards, each
  with a run on hardware. 0.1.0 was published on 2026-10-08.
- **Why this one first** (the order is Claude's): it is one of the two that
  the standard requires; it was the one seen most on a network; and it is
  decided from the query alone, with no clock and nothing remembered between
  queries, so it only takes packets away. The other three are to follow in this
  order: the limit of one multicast of a record a second (§6), which adds the
  record of when each record was last sent; then the unicast answer to a
  question that asks for one (§5.4), whose choice turns on whether the record
  was multicast "recently (within one quarter of its TTL)", which that record
  provides; and last the random delay of 20 to 120 milliseconds before an
  answer for a shared name (§6), which changes the timing of every answer to an
  iPhone's browse.

Before anything was written, what would complete the work was stated: the
responder reads the query's Answer section and leaves out a record listed there
with the same name, type, class IN and data at a TTL of at least half its own;
below half it answers; when every answer is left out it sends nothing, and that
is counted apart from queries about other names; failing tests first, then
mutations; a README row with its marker; "Known problems" rewritten; this
finding; a status note on the finding of 2026-10-07. All of it was done. What
was left out of the work was also said then: a query whose known answers go on
in further packets (§7.2), and leaving known records out of the Additional
section, which §7.1 does not ask for.

## What RFC 6762 says

Read on 2026-10-10 through a tool that fetches a page and returns short exact
quotations, which it limits to 125 characters each, so each sentence below
was asked for in parts. They were not compared with the RFC by hand. The
same tool's own summary of §5.4, asked for first, stated that section's
condition the wrong way round. The exact sentence, which the finding of
2026-10-07 quotes, was asked for again on 2026-10-10 and reads as quoted there.
Nothing here rests on a summary.

- **§7.1:** "A Multicast DNS responder MUST NOT answer a Multicast DNS query if
  the answer it would give is already included in the Answer Section with an
  RR TTL at least half the correct value."
- **§7.1, the next sentence:** "If the RR TTL of the answer as given in the
  Answer Section is less than half of the true RR TTL as known by the Multicast
  DNS responder, the responder MUST send an answer so as to update the
  querier's cache …". The tool gave the words after that, "before the record
  becomes in danger of expiration.", outside quotation marks.
- **§10.2:** "The cache-flush bit MUST NOT be set in any resource records in the
  Known-Answer list of any query message."
- **§7.2,** on a query with the TC bit set, whose known answers continue in
  further packets: "A Multicast DNS responder seeing a Multicast DNS query with
  the TC bit set defers its response". The rest of that sentence was not read.
- **The Additional section.** Asked whether the RFC says anything about leaving
  out of the Additional section records the querier listed as known answers,
  the tool found nothing, in the text up to §10.4 and in the rest, where §15.2
  speaks of multipacket Known-Answer lists only. That is the tool's reading of
  the whole document, not Claude's own.

## What the responder does now

For each record it would put in the Answer section, the responder looks in the
query's Answer section for the same record. Every rule below is in
`MdnsResponder.cs`, and each has a test unless this finding says otherwise.

- **The same record** means the same name (compared without regard to letter
  case, as `DnsName` compares names), the same type, class IN, and the
  same data. The data is compared as the bytes this responder's own writer
  produces, which is how conflict detection already compares data
  (`REQ-ADV-024`). An `NSEC` record is written again from what the reader
  decoded, so a querier listing the service's own `NSEC` is recognised.
- **Class IN is read without the cache-flush bit.** §10.2 says a querier must
  not set it in a known answer. One that does still names a record in class IN.
  A known answer in any other class is another record.
- **At least half** is half the TTL the service publishes the record with:
  2,250 seconds for a `PTR`, 60 for an address record. Exactly half counts.
  Below half the record is sent, with its own TTL.
- **When every answer is left out, nothing is sent.** The query is counted as
  seen and not as answered, and it is counted in a new count,
  `SuppressedByKnownAnswers`, not among the queries about other names, which
  is what it would otherwise have fallen into.
- **The Additional section is chosen from the answers that are sent,** so a
  record left out brings no additionals of its own: a `PTR` left out brings no
  `SRV`, `TXT` or address records with it.
- **A legacy unicast querier** (`REQ-ADV-017`) is treated the same: if it lists
  the answer, it gets nothing.
- **The stop summary says how many.** Its first line was written in
  `ServiceHost.cs`; it is now written by `ResponderActivity.DescribeServed`,
  beside the line for the two transports, and reads, for example: `Served 1
  quer(ies) of 6 seen; 2 were for other services and were ignored; 3 carried
  every answer already and were not answered (RFC 6762 s7.1).` The count is
  always given, zero included. The line moved so that a test could read it.

## Choices the RFC does not make

Each is Claude's, and each is stated in the code beside the rule.

- **Data compared as bytes.** A known answer whose data differs from the
  service's only in the letter case of a name inside it does not keep the
  record out: the record is sent, as every record was before. A querier
  repeats the data it was sent, so this is not expected to arise, and it is not
  tested.
- **The published TTL is "the correct value"** also for a legacy unicast
  querier, whose answer goes with its TTL capped at 10 seconds. §7.1's next
  sentence calls it "the true RR TTL as known by the Multicast DNS responder".
- **Known answers are not looked for among the additionals.** §7.1 asks this
  of answers, and nothing in the RFC asks it of additionals, by the reading
  above. So a record a querier lists can still go out as an additional to
  another answer: for example the host's `A` record behind an `AAAA` answer.
- **Conflict detection is left as it was.** The `NSEC` handling was added in a
  new method used only here, rather than in `RdataOf`, which conflict
  detection and the probe tiebreak also use, so that neither changes.
- **§7.2 is not done.** A query marked truncated is judged on the known answers
  in its own packet and answered at once, as every query was before. The RFC
  says the responder defers its answer to wait for the rest.

## The tests

Thirteen new tests in `tests/SecretPrinter.Responder.Tests/KnownAnswerTests.cs`,
each marked `REQ-ADV-026`. A query carrying known answers is built with the
response builder and has its response and authoritative bits cleared; the test
helper parses it back and checks it reads as a query with the questions and
known answers given before it is used. That is how the responder was measured
on 2026-10-07.

Before the change, with only the new count's shape added (a property that
always read zero), the suite was run: 128 passed and 7 failed.

| Test | Before the change |
| --- | --- |
| The service's own `PTR` listed with its full TTL: not answered, and counted apart | failed |
| Listed with exactly half its TTL: not answered | failed |
| The cache-flush bit on a known answer is not read as part of its class | failed |
| A question answered with an `NSEC` is not answered when the query carries it | failed |
| Of two questions, the one whose answer is known is left out, and only the answer sent brings additionals | failed |
| Of two `AAAA` records, the one listed is left out and the other sent | failed |
| A legacy unicast querier that lists the answer is not answered | failed |
| Listed with less than half its TTL: answered, with the full TTL | passed |
| Another device's `PTR` at the same name does not keep the service's out | passed |
| A known answer at another name, with the same type and data, does not keep it out | passed |
| A known answer of another type whose data is the same bytes does not keep it out | passed |
| A known answer in another class does not keep it out | passed |
| The stop summary's first line gives each count where it belongs | added after the change; see below |

The seven failed for the reason each states, read from the messages. One did
not at first: the `AAAA` test failed because the advertisement builder refuses
a link-local address without a scope, and was corrected to scope its made-up
addresses as the other link-local tests do; it then failed because both
records were sent. The five that passed are there so that the rule cannot be
made to pass by leaving out too much.

After the change: 136 in the responder suite, 0 failed.

## Breaking each claim on purpose

Each mutation changed one thing in the code, rebuilt (stopping if the build
failed), ran the suite and restored the file.

| Mutation | Caught by |
| --- | --- |
| The TTL rule removed: any listed record kept out | Listed with less than half its TTL |
| Exactly half not counted as half | Listed with exactly half its TTL |
| The data not compared | The two `AAAA` records; another device's `PTR` |
| The cache-flush bit not masked off | The cache-flush bit test |
| The class not checked | A known answer in another class |
| The type not compared | A known answer of another type |
| The name not compared | A known answer at another name |
| A listed `NSEC` not decoded | The `NSEC` test |
| A query with every answer left out counted as one about another service | The full-TTL test; the stop summary test |
| The new count left at zero | The full-TTL test; the stop summary test |
| Additionals chosen from the answers left out as well | The two-question test |
| Legacy unicast queriers exempted | The legacy test |
| The stop summary giving another count in place of the new one | The stop summary test |

Two of these did not run as first written:

- **The count left at zero** was first written as removing the line that adds
  to it. The compiler refused it, because a field never assigned is an error
  in this repository. It was written again as a line that adds nothing.
- **The stop summary giving another count** was at first caught by no test:
  the stop summary test then made one query of each kind, so two of its three
  counts were 1 and swapping them changed nothing. The test now makes one, two
  and three, and the mutation is caught.

What no test catches: that the service writes the line to its log at a stop.
`ServiceHost.RunAsync` calls `DescribeServed` as it stops, and no test runs
`ServiceHost.RunAsync`, as recorded on 2026-10-07. And nothing here touches a
network.

## What it changes on a network

- **An iPhone that lists the service's `PTR` in a browse,** with at least half
  its TTL left, now gets no answer to that question from the service, where it
  got one before. On 2026-10-02 that was the case for 8, 1 and 11 questions in
  three runs.
- **Nothing else that is sent changes.** A record not listed is sent as before,
  with the same additionals as before.
- **Whether this changes how an iPhone finds the printer, or whether a page
  prints,** is not known until it is run.

## What a run on a network would need to show

Not decided; how a changed build gets onto a machine on the household's
network is Edwin's, and is to be put to him. What would show the change:

- **The new count above zero** in the stop summary, after an iPhone has
  browsed for printers while the service ran;
- **a page printed** from that iPhone through the service;
- and, if a capture is taken (on the client adapter only, as before, and never
  in this repository), **a question carrying the service's `PTR` with no answer
  from the service** to it.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12.
The six test projects that build directly were built and run. The service and
its test project were built from the repository's own files by two scratch
projects outside the repository, which take
`System.ServiceProcess.ServiceController` from the SDK's own folder because
NuGet cannot be reached; that is not the build a release makes. Nothing in the
code that changed is specific to a platform.

- **Suites:** 28, 27, 34 (9 skipped, for lack of IPv6), 61, 41, 136, and 105 in
  the service suite.
- **SpecCheck,** with the scratch service build added: 100 requirements, 23
  assemblies, 18 evidence rows, 447 test records, 82 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains one row, `REQ-ADV-026`, `OK`, and no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Responder/MdnsResponder.cs`: the rule (`IsKnownAnswer`,
  `KnownAnswerData`, the loop in `HandleAsync`), the new count, and
  `DescribeServed`. The `REQ-SEC-001` note on `HandleAsync` now says a query's
  Answer section is read, and that what is read there can only keep a record
  out of an answer.
- `src/SecretPrinter.Service/ServiceHost.cs`: the stop summary's first line is
  written by `DescribeServed`.
- `tests/SecretPrinter.Responder.Tests/KnownAnswerTests.cs` (new) and
  `Program.cs`, which runs it.
- `README.md`: the row `REQ-ADV-026`; the entry under "Known problems", which
  now lists three things RFC 6762 asks that are not done and says the fourth
  is done and not yet run on a network; a dated note at the head of that
  section.
- A status note on the finding of 2026-10-07.

## What this does not show

- **Any of it on a network, or on Windows.**
- **What an iPhone does** when its known answer is honoured.
- **§7.2,** known answers in further packets.
- **A known answer whose data differs only in letter case.**
- **The other three items** of the finding of 2026-10-07.

# A record is multicast at most once a second

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-12. Reviewed by a human before merge.*

**Status: corrected in the change this finding comes with, and measured against
the code only. The service's mDNS responder now keeps, for each client adapter,
the time it last multicast each record there, and does not multicast a record
there again until a second has passed, as RFC 6762 §6 requires. An answer leaves
out what went out less than a second before; an announcement and a goodbye wait
for the second to be up; an answer to a probe waits for 250 ms. This is the
second of the four things recorded on 2026-10-07 as not done
([finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md),
item 2). It is stated as README `REQ-ADV-027`. Twenty new tests show it in
Claude's workspace, with the test kit's fake transport in place of a network.
It has not been run on a network, and what an iPhone does with it is not
known.**

No address of a real device appears in this finding. The addresses in the
tests are made up, from the ranges set aside for documentation.

## Why this was done

- **It is a requirement of the standard, and it was seen on a network.** On
  2026-10-02, in run 1, three of the service's ten answers followed the one
  before by 0.22, 0.34 and 0.67 seconds, repeating the same records
  ([finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md),
  "Also seen", item 3). On 2026-10-07 the same question put five times drew
  five responses within one millisecond.
- **Edwin West decided on 2026-10-07** that release 0.1.0 would ship with the
  four stated as known problems, and that they would be built afterwards, each
  with a run on hardware.
- **The order is Claude's,** chosen on 2026-10-10: known-answer suppression
  first (done that day, `REQ-ADV-026`), then this, then the unicast answer to a
  question that asks for one (§5.4), whose choice turns on whether a record was
  multicast "recently", which the record kept here provides; and last the
  random delay before an answer for a shared name (§6).

Before anything was written, what would complete the work was told to Edwin:
the rule for answers, announcements, goodbyes and answers to probes; unicast
answers to legacy queriers left alone; an adapter's two transports taken as
one interface; elapsed time read from a clock that cannot go backwards; failing
tests first, then mutations; a README row; the "Known problems" entry; this
finding; a status note on the finding of 2026-10-07. All of it was done. One
thing was added on the way: a send that fails is not counted as a multicast
(see "The tests").

## What RFC 6762 says

Read on 2026-10-12 through a tool that fetches a page and returns short
passages; they were not compared with the RFC by hand. §6:

> a Multicast DNS responder MUST NOT (except in the one special case of
> answering probe queries) multicast a record on a given interface until at
> least one second has elapsed since the last time that record was multicast
> on that particular interface.

Further on in the same paragraph, two sentences give the reason that leaving a
record out is enough:

> A legitimate querier on the network should have seen the previous
> transmission and cached it. A querier that did not receive and cache the
> previous transmission will retry its request and receive a subsequent
> response.

And the exception:

> In this special case only, when responding via multicast to a probe, a
> Multicast DNS responder is only required to delay its transmission as
> necessary to ensure an interval of at least 250 ms since the last time the
> record was multicast on that interface.

(The first passage was quoted in the finding of 2026-10-07. The others were
read in pieces of under 125 characters, which is all the tool returns at once.
The second sentence of the middle passage was asked for as the one that follows
the first; the last passage was read in three pieces, the middle one asked for
as the words between the other two.)

## What the responder does now

- **It remembers.** For each client adapter, the time it last multicast each
  record there, by the record's name, type and data, leaving out the TTL.
- **An answer** leaves out every record, answer or additional, multicast on
  that adapter less than a second before. Additionals are chosen from the
  answers left. When no answer is left, nothing is sent, and the query is
  counted as held back.
- **An announcement** waits until each record in it was last multicast on that
  adapter at least a second before. So does **a goodbye**, which counts as a
  multicast of the records it retracts. Neither waits longer than a second,
  and each still carries every record. The wait between announcements now goes
  through the same wait the responder was given, which in the service is still
  `Task.Delay`.
- **An answer to a probe,** a query with a record in its Authority Section,
  waits until 250 ms have passed since any record in it was last multicast
  there, and leaves nothing out.
- **A unicast answer to a legacy querier** (`REQ-ADV-017`) is neither limited
  nor recorded.
- **The stop summary's first line** gains a count, always given, zero
  included: "N would have repeated records multicast less than a second before
  and were not answered (RFC 6762 s6)."
- **A send that fails** takes its record back, so the records go back to the
  time they held before.

## Choices the RFC does not make

Each is Claude's, and each is stated in the code beside the rule and in
`REQ-ADV-027`.

- **An adapter's IPv4 side and its IPv6 side are one interface.** RFC 6762
  §6.2 says some responders "treat a physical interface with both IPv4 and IPv6
  address as a single interface with two addresses" and that "Other Multicast
  DNS responders may treat this case as logically two interfaces". This
  responder already says it is the first kind, in the remarks on
  `AdditionalsFor`, where §6.2 forbids a responder of the second kind to send
  an `NSEC` that this one sends. So a record multicast over IPv4 is not multicast over IPv6 within the second, nor
  the reverse.
  **What it costs:** the service answers each query over the transport it
  arrived on, and only that one (`REQ-ADV-018`). An iPhone has been measured
  sending the same question over both. When both copies arrive within a
  second, the second gets no answer with those records, and the iPhone hears
  the first answer only if that transport's multicast reaches it. The RFC's
  answer is that it will ask again. Whether this makes any difference to an
  iPhone in this household is for the run on hardware to show.
- **An answer leaves records out; announcements and goodbyes wait.** The RFC's
  "will retry its request" is about a querier, and an answer is what a querier
  asked for. An announcement and a goodbye are sent once each, unasked, so
  leaving a record out of one would leave the network without it; they wait
  instead, and the wait is never more than a second.
- **The service's own probes are not counted.** §8.1 has a host send three
  probes 250 ms apart, each carrying the same proposed records, and announce
  after them: if a probe counted as a multicast of its records, the RFC would
  forbid its own sequence. A probe is a query, and §6 is about responses.
- **A probe is any query with a record in its Authority Section.** That is how
  the responder already told a probe from another query (`NoteSimultaneousProbe`).
  Whether a query really is a probe is not judged, so a device on the client
  network that puts any record in the Authority Section of its questions can
  draw a record up to four times a second instead of once. That is the most §6
  allows for answers to probes.
- **Elapsed time comes from `TimeProvider.GetTimestamp`,** which on the system
  clock does not go backwards, and not from the wall time. A wall clock set
  back an hour would otherwise hold every record back for an hour.
- **A send that fails is not a multicast.** The record of a send is made before
  the send, under the same lock as the decision, so that two tasks cannot both
  decide to send a record; if the send then fails, the record goes back to what
  it was, unless another send has recorded it since.
- **The record of sends is never cleared.** Every record the responder sends is
  in its advertisements or built from them, so it holds at most that many
  entries for each adapter.

## The tests

Twenty new tests in `tests/SecretPrinter.Responder.Tests/OncePerSecondTests.cs`,
thirteen of them marked `REQ-ADV-027` and one `REQ-ADV-024`. All but one build
a responder on two client adapters, the first with an IPv6 companion; the
conflict test builds its own on one. Each is given a `ManualClock`, a new `TimeProvider` in the test kit whose timestamps move only
when a test moves them and whose wait moves them by the wait asked for.

Before the change, with only the change's shape in place (the two new
constructor parameters accepted and not used, and a count that always read
zero), the suite was run: 141 passed and 15 failed. The fifteenth is the
known-answer test of the stop summary's line, whose expected text gained the
new count.

| Test | Before the change |
| --- | --- |
| A question asked again within a second is not answered again by multicast, and is counted as held back | failed |
| At 999 ms the record is still held back | failed |
| Only the records multicast within the second are left out of an answer | failed |
| An adapter's IPv4 entry and its IPv6 companion are one interface | failed |
| A query left unanswered partly by its known answers and partly by the second is counted as held back | failed |
| The clock that decides is the one that cannot go backwards | failed |
| The stop summary counts the queries held back | failed |
| A send that fails takes back only what it recorded | failed |
| A probe within 250 ms is answered when 250 ms have passed | failed |
| Announcements are a second apart whatever interval they are given | failed |
| A conflict heard while an announcement waits stops it (`REQ-ADV-024`) | failed |
| An answer within a second of an announcement is held back | failed |
| An announcement waits until a second after an answer | failed |
| A goodbye waits until a second after the last multicast, and carries every record | failed |
| A second after the last multicast the record is sent again | passed |
| Another adapter is another interface | passed |
| A legacy unicast answer is not limited by the one-second rule | passed |
| A legacy unicast answer does not count as a multicast | passed |
| A query whose every answer it carries is counted as that, not as held back | passed |
| A probe is answered at once when 250 ms have passed | passed |

The fourteen failed for the reason each states, read from the messages; the
conflict test failed because, with no wait, no conflict was heard. The six that
passed are there so that the rule cannot be made to pass by holding back too
much.

**Tests that were there before.** With the change, nine tests that were there
before failed. One is the known-answer test of the stop summary's line, whose
expected text gained the new count. One caught the first version, as told
below. The other seven, six in the responder suite and one in the service
suite, and seven more that waited real seconds, two or three seconds each,
send the same records more than once on one responder, at one instant, to
test something else: that a withdrawn printer is answered again when offered
again; that both transports answer with the same records; four tests of `NSEC`
records; that the log lists the `NSEC` records the responder sends;
announcements, and announcements followed by a goodbye. Their responders are now given `ManualClock.SecondApart()`, a
clock each of whose readings is a second after the one before, so the rule
never holds anything back for them and they check what they checked before.
That is in `MdnsResponderTests.cs`, `LinkLocalAnsweringTests.cs`,
`NsecAnsweringTests.cs`, `AdvertisementLogTests.cs` and `OfferingTests.cs`,
each with a note in its header. The rule itself is tested only in the new
file. The responder suite took 12 seconds before that and about one second
after it.

**One test that was there before caught the first version.** "A send that
fails while answering does not end the receive loop", written on 2026-10-07,
sends three answers and
makes the first two fail. The first version recorded a send before making it
and kept the record when the send failed, so the third answer, a moment later,
was held back as if the first two had gone out. That would also have counted a
query as held back for records that were never sent. The record is now taken
back when a send fails, and the new test "A send that fails takes back only
what it recorded" checks that it goes back to what it was before, not to
nothing.

After the change: 156 in the responder suite and 113 in the service suite, 0
failed.

## Breaking each claim on purpose

Each mutation changed one thing in `MdnsResponder.cs`, rebuilt (stopping if the
build failed), ran the responder suite and restored the file.

| Mutation | Caught by |
| --- | --- |
| A record held back for half a second, not a second | Seven tests, among them the one asked again within a second |
| A record sent exactly a second later counted as within the second | "A second after the last multicast the record is sent again", and others |
| The IPv6 companion kept apart from its adapter | The one-interface test |
| One record of sends for every adapter | "Another adapter is another interface"; the announcements test |
| Additionals not left out | "Only the records multicast within the second are left out" |
| A query held back counted as one about other services | The first test; the mixed test; the stop summary test |
| A query held back by both rules counted as one that carried its answers | The mixed test |
| An answer to a probe held to a second, not 250 ms | Both probe tests |
| An answer to a probe not waiting | "A probe within 250 ms" |
| A probe not recognised | Both probe tests |
| An announcement not waiting | "Announcements are a second apart", "An announcement waits until a second after an answer" and the conflict test |
| A goodbye not waiting | The goodbye test |
| A legacy unicast answer recorded as a multicast | "A legacy unicast answer does not count as a multicast" |
| A legacy unicast answer limited | "A legacy unicast answer is not limited" |
| The wall clock read in place of the timestamps | The suite did not finish: see below |
| A failed send not taken back | The failed-send test of 2026-10-07, and the new one |
| A failed send's record removed rather than put back | "A send that fails takes back only what it recorded" |
| The conflict not read again after the wait | "A conflict heard while an announcement waits stops it" |
| The stop summary giving another count in the new place | Both stop summary tests |
| The count read from the wrong field | Five tests |
| Held back never set | Three tests |
| An answer's records not recorded | Twelve tests |
| An answer's additionals not recorded | "Only the records multicast within the second are left out" |
| A goodbye's records recorded under a different name from the ones it retracts | The goodbye test |

Three of these did not run as first written, or did not end as a test does:

- **The count left at zero** was first written as removing the line that adds
  to it. The compiler refused it, because a field never assigned is an error
  in this repository. It was written again as a line that adds nothing.
- **The wall clock in place of the timestamps** made the suite run without
  end. The test clocks move their timestamps when the responder waits and
  leave their wall time alone, so the wait before an announcement never
  shortened and the responder waited again and again. The run was stopped
  after 120 seconds. It shows something about the code as well: the wait
  ends only because the clock it reads moves while it waits. The system clock
  does.
- **The wait between announcements made with `Task.Delay`,** as it was before,
  rather than with the wait the responder is given, was caught by no test, in
  the responder suite or the service suite. In the service the wait it is
  given is `Task.Delay`, so the two are the same there; the change only lets a
  test move a clock instead of waiting.

What no test catches: that the lock does what it is for. Two tasks deciding
to send the same record at the same moment cannot be arranged in a test that
does not depend on timing, and none was written. And that the service writes
the stop summary's line to its log, as recorded for `REQ-ADV-026`: no test
runs `ServiceHost.RunAsync`. And nothing here touches a network.

## What it changes on a network

- **Fewer packets.** A record the service multicast on the client network less
  than a second before is not multicast again, except in an answer to a probe,
  after 250 ms.
- **Some questions get no answer from the service,** where they got one before:
  a question whose answer went out less than a second before, over either
  transport.
- **An announcement or a goodbye can go out up to a second later** than before,
  if an answer carried the same records just before it; an answer to a probe,
  up to 250 ms later.
- **Whether this changes how an iPhone finds the printer, or whether a page
  prints,** is not known until it is run.

## What a run on a network would need to show

How a changed build gets onto a machine on the household's network, and when,
is Edwin's, and is to be put to him. What would show the change:

- **The new count above zero** in the stop summary, after an iPhone has
  browsed for printers while the service ran;
- **a page printed** from that iPhone through the service;
- and, if a capture is taken (on the client adapter only, as before, and never
  in this repository), **no record sent by the service twice within a second**
  on that adapter, except in an answer to a probe.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12,
built as described in earlier findings: the six test projects that build
directly, and the service and its tests through two scratch projects outside
the repository. That is not the build a release makes. Nothing in the code
that changed is specific to a platform.

- **Suites:** 28, 27, 34 (9 skipped, for lack of IPv6), 61, 44, 156, and 113 in
  the service suite.
- **SpecCheck,** with the scratch service build added: 102 requirements, 23
  assemblies, 18 evidence rows, 478 test records, 84 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains the row `REQ-ADV-027`, `OK`; the row `REQ-ADV-024` gains one test; no
  other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Responder/MdnsResponder.cs`: the record of sends and the
  rule (`WaitToMulticastAsync`, `SendNotedAsync`, `Unnote` and their helpers,
  and the changes to `HandleAsync`, `AnnounceAsync` and `SendGoodbyeAsync`);
  the new count and `DescribeServed`; two new constructor parameters, the clock
  and the wait. The `REQ-SEC-001` note on `HandleAsync` now says that whether a
  query's Authority Section holds a record is read, and that it decides only
  how soon an answer may be multicast.
- `tests/SecretPrinter.Responder.Tests/OncePerSecondTests.cs` (new) and
  `Program.cs`, which runs it.
- `tests/SecretPrinter.TestKit/ManualClock.cs` (new).
- `MdnsResponderTests.cs`, `LinkLocalAnsweringTests.cs`,
  `NsecAnsweringTests.cs`, `AdvertisementLogTests.cs` and `OfferingTests.cs`:
  their responders given `ManualClock.SecondApart()`, with a note in each
  header. `KnownAnswerTests.cs`: the stop summary's expected line.
- `README.md`: the row `REQ-ADV-027`; the entry under "Known problems", which
  now lists two things RFC 6762 asks that are not done and says the other two
  are done; a dated note at the head of that section.
- A status note on the finding of 2026-10-07.

## What this does not show

- **Any of it on a network, or on Windows.**
- **What an iPhone does** when the second copy of its question, over the other
  transport, gets no answer with records it was just sent.
- **Two tasks sending at the same moment.** The lock is read, not tested.
- **The other two items** of the finding of 2026-10-07: the unicast answer
  (§5.4) and the delay for a shared name (§6).

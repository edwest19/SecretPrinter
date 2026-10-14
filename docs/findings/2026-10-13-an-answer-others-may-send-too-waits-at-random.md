# An answer others may send too waits at random

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-13. Reviewed by a human before merge.*

**Status: done in the change this finding comes with, and measured against the
code only. An answer that other devices on the client network may be sending
too now waits a random 20 to 120 milliseconds before it goes, as RFC 6762 §6
and §6.3 recommend: an answer that holds one of the service's three shared
records, the PTR records, and an answer to a query of more than one question.
An answer to a query of one question that holds only records the service
claims alone still goes at once, and so does an answer to a probe. This is the
last of the four things recorded on 2026-10-07 as not done
([finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md),
item 3). It is stated as README `REQ-ADV-029`. Fifteen new tests show it in
Claude's workspace, with the test kit's fake transport and a clock the tests
move by hand. It has not been run on a network. Building it found a gap in
what the service did since 2026-10-12, in no release, and it is closed here:
after an answer to a probe had waited for its 250 ms, nothing was read again
before it was sent.**

No address of a real device appears in this finding. The addresses in the
tests are made up, or from the ranges set aside for documentation.

## Why this was done

- **It is a recommendation of the standard,** recorded on 2026-10-07 as not
  done, and measured then: nothing waited before an answer. On a network on
  2026-09-29 an answer to a browse came about a millisecond after it.
- **Edwin West decided on 2026-10-07** that release 0.1.0 would ship with the
  four stated as known problems, and that they would be built afterwards.
- **The order is Claude's,** chosen on 2026-10-10, with this one last because
  it changes the timing of every answer to a browse.
- **Edwin chose to build it that evening,** on 2026-10-13, after the run of the
  third item, against Claude's recommendation to write the handoff and stop.
  The choice he took said there would be no run on hardware that night.

## What RFC 6762 says

The sentences quoted are from
[RFC 6762](https://www.rfc-editor.org/rfc/rfc6762), read on 2026-10-13 through
a tool that fetches a page and returns the passages asked for. They were not
compared with the RFC by hand.

§6, for an answer others may send too:

> In any case where there may be multiple responses, such as queries where the
> answer is a member of a shared resource record set, each responder SHOULD
> delay its response by a random amount of time selected with uniform random
> distribution in the range 20-120 ms.

§6, for an answer no one else will send:

> In the case where a Multicast DNS responder has good reason to believe that
> it will be the only responder on the link that will send a response (i.e.,
> because it is able to answer every question in the query message, and for
> all of those answer records it has previously verified that the name, rrtype,
> and rrclass are unique on the link), it SHOULD NOT impose any random delay
> before responding, and SHOULD normally generate its response within at most
> 10 ms.

§6.3, for a query of more than one question:

> Unlike single-question queries, where responding without delay is allowed in
> appropriate cases, for query messages containing more than one question, all
> (non-defensive) answers SHOULD be randomly delayed in the range 20-120 ms, or
> 400-500 ms if the TC (truncated) bit is set.

and, after it, "(Answers defending a name, in response to a probe for that
name, are not subject to this delay rule and are still sent immediately.)"

§6, for a probe: "In the special case of answering probe queries, because of
the limited time before the probing host will make its decision about whether
or not to use the name, a Multicast DNS responder MUST respond quickly."

§5.4, for an answer by unicast: "Unicast replies are subject to all the same
packet generation rules as multicast replies, including the cache-flush bit
(Section 10.2) and (except when defending a unique name against a probe from
another host) randomized delays to reduce network collisions (Section 6)."

## What the responder does now

In `HandleAsync`, once the questions have been read and the answer's records
chosen, and before anything else is decided:

- **An answer waits** when it holds a record published without the cache-flush
  bit, which are the three PTR records, at the two service-type names and the
  service enumeration name (`REQ-ADV-012`); or when the query holds more than
  one question. The questions are counted as the query holds them, answered or
  not: when the service can answer only some of them, it is not "able to answer
  every question in the query message", and other devices may be answering the
  rest.
- **The wait** is drawn anew for each answer: a whole number of milliseconds
  from 20 to 120, both included, each as likely as any other, from
  `RandomNumberGenerator`, as the random start of the service's own probe is.
  The draw is a new constructor parameter, `randomAnswerDelay`, so that a test
  can fix it; a draw of zero or less waits for nothing.
- **No wait** for an answer to a query of one question holding only records
  the service claims alone (its host name and service instance name, which it
  probed for); for an answer to a probe, a query with records in its Authority
  section, whatever it holds; or for a query with nothing to answer.
- **Every way an answer goes** takes the wait: by multicast, by unicast
  because a question asked for it (`REQ-ADV-028`), and to a legacy querier
  (`REQ-ADV-017`).
- **The rest is decided after the wait:** which records the one-second rule
  holds back (`REQ-ADV-027`), and which go by unicast (`REQ-ADV-028`). Both
  turn on when a record was last multicast, and the moment that matters is the
  moment the answer goes.
- **After the wait, the gate, the conflict and the probing are read again,**
  through a new method, `AnswersNothing`, which the check on a query's arrival
  now uses too. If the printer has been withdrawn, a conflict heard or a probe
  begun meanwhile, nothing is sent, and the query is counted only as seen.
  Without this, an answer by unicast, which the one-second rule does not hold
  back, could go after the goodbye that withdrew the printer.
- **The count** `ResponderActivity.WaitedAtRandom`, and a clause at the end of
  the first line of the stop summary: "N of those served were answered after a
  random wait of 20 to 120 ms, because the answer held a shared record or the
  query asked more than one question (RFC 6762 s6, s6.3)." Only answers that
  went are counted.

## The gap found in the probe's wait

Since 2026-10-12 (`REQ-ADV-027`), an answer to a probe waits, when one of its
records was multicast less than 250 ms before, until 250 ms have passed. After
that wait, the code sent the answer without reading the gate or the conflict
again. Read from the code: an answer to a probe that was waiting when the
printer was withdrawn, or when a conflict was heard, went when its wait ended.
The goodbye still came after it, unless the answer's wait overran by most of a
second: a goodbye waits until a second has passed since any record it retracts
was last multicast, and an answer to a probe only until 250 ms have. Not seen
on a network.
It is in no release: release 0.2.0 was made before `REQ-ADV-027`.

Now the same reading follows that wait. If the printer has been withdrawn, a
conflict heard or a probe begun, the multicast is not sent and what was noted
for it is taken back. A unicast answer to the same probe, sent before the
wait, has gone; the query counts as answered for it.

## What changed while it was built

- **§6.3 was read before the change was written,** and widened it. The design
  first told to Edwin made only an answer holding a shared record wait. §6.3
  asks the wait of every answer to a query of more than one question. Edwin was
  told of the correction before the code was written. The tests had been
  written for the first design, as `SharedAnswerDelayTests`; they were renamed
  `RandomAnswerDelayTests` and widened, and were never committed under the
  first name.
- **The finding of 2026-10-07 said** of the service's own names that "it
  answers for them at once, which is what is asked". That is so only for a
  query of one question. It is given a status note.
- **A query with nothing to answer was first made to wait** when it held more
  than one question. Most queries seen are about other services, and each
  would have held up the receive loop for nothing. The rule now asks for an
  answer first, and a test holds it.
- **The test of the default draw was strengthened.** It first drew 200 times
  and looked for draws in the lowest and highest quarters of the range. A
  mutation that made the longest draw 119 ms passed it. It now draws 5,000
  times and requires every whole millisecond from 20 to 120, and none drawn
  more than 150 times. The chance that it fails for a correct draw is below 3
  in 10^20 (any one value missing: (100/101)^5000, about 2.5 in 10^22, for 101
  values).

## The tests

Fifteen, in `tests/SecretPrinter.Responder.Tests/RandomAnswerDelayTests.cs`:

- an answer holding a shared record waits the drawn time;
- an answer to one question of records held alone goes at once;
- an answer to two questions waits, even of records held alone;
- an answer to two questions waits when only one is about the service;
- a query of two questions with nothing to answer does not wait;
- the default draw: whole milliseconds, 20 to 120, every one drawn;
- the one-second rule judges the moment of sending, after the wait, and only
  the answer sent is counted as having waited;
- an answer by unicast that a question asked for waits too;
- a legacy answer waits too;
- an answer to a probe does not wait, even for a shared record;
- withdrawn while waiting: nothing sent; a conflict while waiting: nothing
  sent;
- withdrawn during a probe's 250 ms wait: nothing multicast, and what was noted
  taken back;
- withdrawn during a probe's wait after its unicast answer went: that answer
  counts, and nothing is multicast;
- the stop summary's line.

Run against the code with the constructor parameter and the count but nothing
else of the change, twelve failed, as intended, and the two that expect no wait
passed. The test of the one-second rule was among the twelve, at its last line
only, the count: a responder that never waits passes the rest of it, which is
there so that a wait put after the decision fails. Two tests changed after
that run: the test of a query with nothing to answer was added, and with the
wait taken out it passes, as a test expecting no wait should; and the test of
the default draw was strengthened, and with the wait taken out it fails, as
before.

**Changed in four existing test files:**

- `OncePerSecondTests.cs`: every responder there is given a random wait of
  none. With the default wait, two tests failed: at 999 ms the PTR was no
  longer held back, the second answer's own wait having taken it past the
  second; and the conflict that a test of announcements hears during a wait
  was heard during the wait before an answer instead.
- `KnownAnswerTests.cs`, `UnicastAddressTests.cs` and `UnicastResponseTests.cs`:
  the stop summary's line, with the new count, 1, 1 and 3.

The other responder tests, and the service's, run with the default wait. To see
whether any of them depends on how long it is, every suite was run four more
times with the draw fixed: at 20 ms, at 120 ms, alternating 120 then 20, and
alternating 20 then 120. Before the changes above, the same six tests failed in
each, and no other but the test of the default draw, under the two fixed
draws. After them, only that test failed, under those two, as it should.

## Breaking each claim on purpose

Each mutation changed one thing in `MdnsResponder.cs`, rebuilt (stopping if the
build failed), ran the responder suite under a time limit, and restored the
file.

| Mutation | Caught by |
| --- | --- |
| An answer to a probe waits | Two tests |
| A query with nothing to answer waits | "A query of two questions with nothing to answer here does not wait" |
| The questions not counted | Both tests of two questions |
| One question enough to wait | Six tests |
| Records held alone wait, shared ones do not | Sixteen tests |
| Shared records not a reason to wait | Eleven tests |
| A draw of zero still waits | Two tests |
| An answer that waited not marked as having waited | Seven tests |
| Nothing read again after the wait | Both tests of what changes during the wait |
| The gate as read on arrival used after the wait | "If the printer is withdrawn while the answer waits, nothing is sent" |
| The wait after the decision, not before | Three tests |
| Nothing read again after a probe's wait | Two tests |
| A probe's withheld multicast not taken back | "If the printer is withdrawn during the 250 ms wait …" |
| A probe whose multicast was withheld counted as answered without a unicast answer | The same |
| The two cases after a probe's withheld multicast swapped | Two tests |
| The gate as read on arrival used after a probe's wait | Two tests |
| The count not added to | Seven tests |
| The default draw's longest wait 119 ms | The test of the default draw |
| The default draw from 0 ms | The test of the default draw |
| The default draw to 200 ms | The test of the default draw |
| The default draw always none | Four tests |
| A legacy answer exempt from the wait | Two tests |
| The probing left out of `AnswersNothing` | Two tests, from before this change |
| The conflict left out of `AnswersNothing` | Two tests, one from before this change |
| The stop summary giving another count in the new place | Four tests |

All twenty-five were caught. Two were first written in forms that did not
compile, and were rewritten so that they did: nothing read again after a
probe's wait, and the count not added to. The longest draw made 119 ms was not
caught until the test of the default draw was strengthened.

What no test catches: as before, that the service writes the stop summary to
its log, since no test runs `ServiceHost.RunAsync`. And nothing here touches a
network.

## What it costs

- **Some answers are later,** by 20 to 120 ms: every answer to a browse, which
  asks for a PTR, and every answer to a query of more than one question. The
  finding of 2026-09-29 records a device that browsed for printers and asked
  for three record types of a host name in the same packet
  ([finding](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md)),
  so such queries are seen on the network this was built on.
- **The receive loop waits with each answer.** While one answer waits, no other
  query is read; it waits to be read. When several queries that each take the
  wait arrive together, their answers go one after another, and a later one
  can go more than 120 ms after it was asked, outside the range §6 gives. A
  device on the client network that sends such queries often enough keeps the
  loop waiting most of the time, and queries behind them are read late, or
  lost if what the socket holds fills up; how much it holds has not been
  measured. Any device on the client network can already flood the mDNS group
  for every device on it; this makes the service's share easier to fill. Not
  doing this would need answers that wait without holding up the loop, which
  means answers decided on other tasks than the receive loop, and that is not
  done.
- **A legacy query from an address `REQ-SEC-017` refuses waits before it is
  refused,** since the refusal is decided after the answer, as before.
- **The tests take real waits** where a responder runs on the system clock, as
  in `KnownAnswerTests` and `UnicastAddressTests`: each answer there that takes
  the wait waits 20 to 120 ms of real time.

## What is not done

Each is stated in `REQ-ADV-029` and under "Known problems" in the README:

- **A query marked truncated** (§7.2) is not given the 400 to 500 ms §6 asks,
  so that the known answers in the packets after it can arrive. If its answer
  takes the wait, it is answered 20 to 120 ms after it arrived; otherwise at
  once, as `REQ-ADV-026` says.
- **Answers are not held for up to 500 ms to be sent together,** which §6 says
  a responder on Ethernet "SHOULD have the capability of" doing.
- **Answers do not wait apart from the receive loop,** as above.

All three would need answers held and sent by a schedule of their own, apart
from the receive loop.

## What a run on a network would need to show

How a changed build gets onto a machine on the household's network, and when,
is Edwin's, and is to be put to him. What would show the change:

- **the new count above zero** in the stop summary, after an iPhone has browsed
  for printers;
- **a page printed** from that iPhone through the service;
- and, if a capture is taken (on the client adapter only, as before, and never
  in this repository), the time from a browse to its answer, and that it falls
  between 20 and 120 ms or, when queries come together, after it.

How long `Task.Delay` actually waits on Windows for a wait of 20 to 120 ms has
not been measured here.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12,
built as described in earlier findings: the six test projects that build
directly, and the service and its tests through two scratch projects outside
the repository. That is not the build a release makes. Nothing in the code
that changed is specific to a platform.

- **Suites:** 28, 27, 38 (9 skipped, for lack of IPv6), 61, 44, 205, and 113
  in the service suite.
- **SpecCheck,** with the scratch service build added: 105 requirements, 23
  assemblies, 18 evidence rows, 531 test records, 87 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains the row `REQ-ADV-029`, `OK`; no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin West before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Responder/MdnsResponder.cs`: the wait in `HandleAsync`,
  with `MinRandomAnswerDelay`, `MaxRandomAnswerDelay`, `DrawRandomAnswerDelay`
  and the constructor parameter `randomAnswerDelay`; `AnswersNothing`, used on
  a query's arrival and after each wait; the reading after a probe's wait; the
  count `WaitedAtRandom` and its place in `DescribeServed`; `REQ-ADV-029`
  markers on `HandleAsync`, `DrawRandomAnswerDelay` and `DescribeServed`.
- `tests/SecretPrinter.Responder.Tests/RandomAnswerDelayTests.cs` (new) and
  `Program.cs`; `OncePerSecondTests.cs`, `KnownAnswerTests.cs`,
  `UnicastAddressTests.cs` and `UnicastResponseTests.cs`.
- `README.md`: the row `REQ-ADV-029`; dated notes on `REQ-ADV-026` and
  `REQ-ADV-027`; the entry on RFC 6762 under "Known problems", rewritten, and
  the dated note at the head of that section.
- The finding of 2026-10-07: a status note.

## What this does not show

- **Any of it on a network, or on Windows.**
- **What a client makes of answers that come later,** or whether any client
  had been troubled by answers that came at once.
- **How often answers collided** before this change, or collide now.

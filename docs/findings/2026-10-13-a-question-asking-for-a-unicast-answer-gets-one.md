# A question asking for a unicast answer gets one

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-13. Reviewed by a human before merge.*

**Status: done in the change this finding comes with, and measured against the
code only. A question with the unicast-response bit set, from port 5353, is now
answered by unicast when RFC 6762 §5.4 says it should be: the records that only
such questions ask for, and that the service multicast on that adapter less
than a quarter of their TTL before, go to the querier by unicast, and every
other record goes by multicast as before. This is the third of the four things
recorded on 2026-10-07 as not done
([finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md),
item 4). It is stated as README `REQ-ADV-028`. Twenty new tests show it in
Claude's workspace, with the test kit's fake transport in place of a network.
It has not been run on a network, and how often an iPhone asks such a question
is not known.**

*(Status note, later on 2026-10-13, by Claude, Claude Opus 5.5: run on a network
once, on the test machine in place of FIOS-STB-01, for about 13 minutes 26
seconds in all. Of 1,324 queries seen, 31 were answered, and 29 of those by
unicast because they asked for it; none was held back within a second, none was
refused for its address, and an iPhone printed a page. Which devices asked for
unicast, and over which transport, is not known: nothing was captured. See
[the finding of the run](2026-10-13-twenty-nine-answers-went-by-unicast-on-a-network.md).)*

No address of a real device appears in this finding. The addresses in the
tests are made up, or from the ranges set aside for documentation.

## Why this was done

- **It is a recommendation of the standard,** recorded on 2026-10-07 as not
  done, and measured then: a question with the bit set, from port 5353, was
  answered by multicast.
- **Edwin West decided on 2026-10-07** that release 0.1.0 would ship with the
  four stated as known problems, and that they would be built afterwards, each
  with a run on hardware.
- **The order is Claude's,** chosen on 2026-10-10: known-answer suppression
  (`REQ-ADV-026`), then one multicast a second (`REQ-ADV-027`), then this,
  whose choice turns on when a record was last multicast, which `REQ-ADV-027`
  keeps; and last the random delay before an answer for a shared name (§6).

Reading for it found two other things, each dealt with the same day:

- **A unicast answer went to any address a question gave.** What the service
  already did for legacy queriers sent its answer to whatever source address a
  query carried, and Windows routes a unicast by its table. Corrected first, as
  its own change, before this one was written
  ([finding](2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md),
  `REQ-SEC-017`). This change relies on it.
- **The finding of 2026-10-07 misquoted §5.4's first words.** Corrected there,
  with a note.

Before anything was written, the design was told to Edwin in a few bullets: the
choice record by record; the unicast packet made as the multicast one; the
address check; not recorded as a multicast; probes; a count; legacy queriers
and the service's own probes unchanged; and two limits, the IPv4 TTL and the
delay still to come. All of it was done. One thing not in those bullets was
done as well: a unicast answer that cannot be sent takes back the multicast
noted with it (see "The tests").

## What RFC 6762 says

Read on 2026-10-13 through a tool that fetches a page and returns short
passages, at rfc-editor.org and datatracker.ietf.org; not compared with the RFC
by hand. §5.4, the responder:

> When receiving a question with the unicast-response bit set, a responder
> SHOULD usually respond with a unicast packet directed back to the querier.
> However, if the responder has not multicast that record recently (within one
> quarter of its TTL), then the responder SHOULD instead multicast the response
> so as to keep all the peer caches up to date, and to permit passive conflict
> detection.

> In the case of answering a probe question (Section 8.1) with the
> unicast-response bit set, the responder should always generate the requested
> unicast response, but it may also send a multicast announcement if the time
> since the last multicast announcement of that record is more than a quarter of
> its TTL.

> Unicast replies are subject to all the same packet generation rules as
> multicast replies, including the cache-flush bit (Section 10.2) and (except
> when defending a unique name against a probe from another host) randomized
> delays to reduce network collisions (Section 6).

§5.4, the querier:

> A Multicast DNS querier sending its initial batch of questions immediately on
> wake from sleep or interface activation SHOULD set the unicast-response bit in
> those questions.

and the bit "SHOULD NOT be set in subsequent retransmissions of that question".
§6 allows a response by unicast in three cases only, the first "via the
unicast-response bit".

## What the responder does now

- **Record by record.** A record goes by unicast when a question with the bit
  asks for it, no question without the bit in the same query asks for it, and
  the service multicast it on that adapter, over either transport, less than a
  quarter of its TTL before. A quarter is 30 seconds for the records with a TTL
  of 120 seconds (the address records, the SRV, the host's NSEC) and 1,125
  seconds for those of 4,500 (the PTRs, the TXT, the instance's NSEC). Every
  other record goes by multicast, under the one-second rule (`REQ-ADV-027`).
- **The one-second rule never holds back a record asked for by unicast alone.**
  A quarter of every TTL the service publishes is longer than a second, so a
  record multicast within the second was multicast "recently", and goes by
  unicast.
- **The unicast packet** is made as the multicast one would be: identifier 0,
  no question section, TTLs and cache-flush bits as published. Its additionals
  are those of its own answers, as for a multicast, less any record the
  multicast answer to the same query carries; the querier receives that one as
  well. A record that answers the query goes once, in the packet that answers
  with it.
- **Where it goes:** to the querier's address and port, through the interface
  the query arrived on, and only to an address on the network the query
  arrived from (`REQ-SEC-017`). From any other address, the records go by
  multicast instead.
- **It is not a multicast:** it is not recorded as one, so it does not make a
  record "recent" for a later question, and the one-second rule does not hold
  it back.
- **Probes from other devices.** A probe whose question has the bit gets the
  records it asks for by unicast at once, recent or not, and without the 250 ms
  wait an answer by multicast to a probe takes (`REQ-ADV-027`). Records it asks
  for without the bit go by multicast after that wait, as before.
- **Order.** The unicast answer is sent first, then the multicast one, so a
  probe's unicast answer does not wait. If the unicast send fails, the
  multicast is not sent, and what was noted for it is taken back.
- **Counted.** The stop summary's first line ends with how many of the queries
  served were answered by unicast because they asked for it:
  "… N of those served were answered by unicast because they asked for it (RFC
  6762 s5.4)."
- **Unchanged:** a legacy querier (`REQ-ADV-017`), whose whole answer goes by
  unicast for another reason; and the service's own probes, which ask for
  multicast answers on purpose (`REQ-ADV-023`).

## Choices the RFC does not make

- **Record by record, not answer by answer.** §5.4 speaks of "that record"
  being multicast recently, and then of multicasting "the response". Taken a
  record at a time, an answer can go in two packets, one by unicast and one by
  multicast. Taken as a whole, a response with one record multicast within the
  second and one not multicast recently would have to go by multicast, and the
  first would then be held back by the one-second rule and not reach the
  querier at all.
- **A goodbye counts as a multicast here too,** as it does for `REQ-ADV-027`.
  After the service withdraws the printer and says goodbye, it answers nothing
  until it has probed and announced again (`REQ-LIF-006`); only in the moment
  between the gate opening and the first announcement could a question be
  answered by unicast because of a goodbye. The announcement follows at once.
- **The extra multicast a probe's answer "may also" have is not sent.**
- **The unicast packet's additionals are not judged by the quarter rule.** An
  additional goes with the answer it belongs to.
- **No random delay** before either answer. That is the fourth item, §6's 20 to
  120 milliseconds for a shared name, which §5.4 says applies to unicast replies
  too; it is not built.

## The tests

`tests/SecretPrinter.Responder.Tests/UnicastResponseTests.cs` (new), twenty
tests, and `Program.cs`, which runs it.

Run against the code of `8b1fbd9` with only the new count added, 16 tests of
the responder suite failed: thirteen of the new ones, every one that expects an
answer by unicast or the new count, and the three existing tests of the stop
summary, whose expected line now has the new count. The other seven new tests
passed before and after, and are there so that the rule cannot be made to pass
by answering everything by unicast: a record never multicast goes by
multicast; at a quarter of its TTL a record goes by multicast; a record a
question without the bit also asks for goes by multicast; a querier off the
network gets a multicast; another adapter is another interface; a legacy
querier with the bit set is answered as a legacy querier; a probe without the
bit is answered by multicast.

One test was added after the first run of the suite: a unicast answer that
cannot be sent takes back what was noted for the multicast that would have
followed it. The code did that from its first version, because the multicast's
note is taken under the lock before either send; until that test, nothing showed
it.

Existing tests changed: the stop summary's expected line, in
`OncePerSecondTests.cs`, `KnownAnswerTests.cs` and `UnicastAddressTests.cs`,
each with a dated note in its header. No other existing test changed, in the
responder suite or the service suite.

## Breaking each claim on purpose

Each mutation changed one thing in `MdnsResponder.cs`, rebuilt (stopping if the
build failed), ran the responder suite under a time limit, and restored the
file.

| Mutation | Caught by |
| --- | --- |
| The bit never read | Thirteen tests |
| A record also asked for without the bit sent by unicast | Three tests, among them "a question without the bit also asks for" |
| The whole TTL in place of a quarter | "At a quarter of the TTL a record goes by multicast" |
| A second in place of a quarter | Nine tests |
| A record at exactly a quarter counted as recent | "At a quarter of the TTL a record goes by multicast" |
| A unicast answer recorded as a multicast | "A unicast answer does not count as a multicast" |
| The one-second rule applied before the unicast choice | Four tests, among them "a record multicast less than a second before goes by unicast" |
| No address check for the unicast answer | "A querier off the network it asked from gets the records by multicast instead" |
| A probe held to the quarter rule | "A probe asking for a unicast answer is answered by unicast at once" |
| A probe's unicast answer waiting 250 ms | Both probe tests that expect no wait |
| The unicast answer's additionals keeping what the multicast carries | "In one query, only the record asked for by unicast alone … goes by unicast" |
| The unicast answer given the query's identifier | "The unicast answer is made as the multicast one" |
| The unicast answer capped as a legacy one | "The unicast answer is made as the multicast one" |
| The count not added to | Three tests |
| The stop summary giving another count in the new place | Two tests of the stop summary |
| A failed unicast send not taking back the multicast's note | The test added for it |
| The unicast answer sent through the adapter's IPv4 entry when the query came over IPv6 | The IPv6 test |
| The record of multicasts looked up per transport, not per adapter, for the choice | The IPv6 test |

Two guards in the first version could not be broken, and were taken out: a
question of a legacy querier was kept from counting as asking for unicast, and
a legacy querier was kept from the unicast choice. Neither did anything: the
path for a legacy querier does not read the choice.

What no test catches: as before, that the service writes the stop summary to
its log, since no test runs `ServiceHost.RunAsync`. And nothing here touches a
network.

## What it changes on a network

- **Some answers go by unicast,** to a querier that asked for one, where they
  went by multicast. Other devices on the client network do not see those.
- **Some questions are answered where they were not:** a question asking for a
  unicast answer, for records multicast less than a second before, was held
  back since 2026-10-12 (`REQ-ADV-027`), and now gets them by unicast.
- **Nothing changes for questions without the bit,** which are most of them.
  How many questions with the bit an iPhone sends is not known; §5.4 says a
  querier sets it on waking from sleep or joining a network.
- **An IPv4 unicast answer** takes Windows' default IP TTL; whether that is 255
  is not established (`REQ-ADV-013`).
- **The service has not been seen sending anything by unicast on a network.**
  In the six runs captured on 2026-10-02 it sent no unicast reply
  ([finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md),
  "Also seen", item 4), and no finding records one since.

## What a run on a network would need to show

How a changed build gets onto a machine on the household's network, and when,
is Edwin's, and is to be put to him. What would show the change:

- **the new count above zero** in the stop summary, after an iPhone has joined
  the client network, or woken, while browsing for printers;
- **a page printed** from that iPhone through the service;
- and, if a capture is taken (on the client adapter only, as before, and never
  in this repository), a question with the bit set answered by a unicast packet
  from port 5353 to the iPhone, and the IP TTL that packet carries over IPv4.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12,
built as described in earlier findings: the six test projects that build
directly, and the service and its tests through two scratch projects outside
the repository. That is not the build a release makes. Nothing in the code
that changed is specific to a platform.

- **Suites:** 28, 27, 38 (9 skipped, for lack of IPv6), 61, 44, 190, and 113
  in the service suite.
- **SpecCheck,** with the scratch service build added: 104 requirements, 23
  assemblies, 18 evidence rows, 516 test records, 86 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains the row `REQ-ADV-028`, `OK`; no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin West before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Responder/MdnsResponder.cs`: which questions ask for
  unicast is read; the choice in `HandleAsync`, with `QuarterOfTtl`,
  `TakeAll` and `AsksForUnicast`; the unicast packet and its send; the count
  `AnsweredByUnicastOnRequest` and its place in `DescribeServed`; a
  `REQ-ADV-028` marker on both; the texts of the `REQ-ADV-027`, `REQ-SEC-017`
  and `REQ-SEC-001` markers on `HandleAsync`.
- `tests/SecretPrinter.Responder.Tests/UnicastResponseTests.cs` (new) and
  `Program.cs`; the stop summary's line in three test files.
- `README.md`: the row `REQ-ADV-028`; dated notes on `REQ-ADV-013`,
  `REQ-ADV-025`, `REQ-ADV-027` and `REQ-SEC-017`; the entry on RFC 6762 under
  "Known problems", now one thing not done, and the entry on the TTL of a
  unicast answer; the dated note at the head of that section.
- The finding of 2026-10-07: a status note, and its quotation of §5.4
  corrected.

## What this does not show

- **Any of it on a network, or on Windows.**
- **That an iPhone sends questions with the bit,** or when.
- **That an iPhone accepts an answer by unicast from this service.** Nothing of
  the kind has been seen.
- **The fourth item** of the finding of 2026-10-07: the delay for a shared name
  (§6).

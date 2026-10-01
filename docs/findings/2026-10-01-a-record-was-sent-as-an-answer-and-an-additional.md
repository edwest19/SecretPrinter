# A record was sent as an answer and as an additional

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-01. Reviewed by a human before merge.*

**Status: fixed in the same change as this finding. Whether it ever affected a
client is not known. It was found by reading the code and then measured in
Claude's container. No capture this project has made was searched for it.**

## What was found

`MdnsResponder.HandleAsync` answered a query one question at a time. For each
question it added the matching records to the Answer section. It then added the
records the querier would need next to the Additional section, leaving out any
record already in the Answer section.

A record that answered a later question was not yet in the Answer section when
an earlier question's additionals were chosen, so it went into both sections.

Measured on 2026-10-01 with a throwaway check in Claude's container, not kept.
The query asked for the `PTR` records of `_ipp._tcp.local`, then for the
service instance's `SRV` record. The response carried:

- in the Answer section: `PTR`, `SRV`;
- in the Additional section: `SRV`, `TXT`, `A`.

The `SRV` record was sent twice.

The check that left out records already answered shows the intent, and the
order of the work defeated it. One test stated the property in general: "No
record appears twice in one response, though several questions lead to it".
It asked two `PTR` questions only, which never reach this case.

It was found on 2026-10-01 while planning the `NSEC` (`REQ-ADV-022`). Suppose
the host has no link-local address and a query asks for `A`, then `AAAA`. The
host's `NSEC` would have gone into the Additional section through the `A`
answer, and into the Answer section through the `AAAA` question.

Claude has not found a sentence in RFC 6762 that forbids a record in both
sections, and does not claim one. The fault is that the code did not do what it
was written to do.

## What changed

- `HandleAsync` answers every question first. It then chooses the additionals
  for all the answers together, leaving out any record that is an answer. The
  additionals are chosen from the answers in the order they were given, as
  before.
- The test is renamed "Two PTR questions that lead to the same records send each
  of them once", which is what it checks.
- Two tests were added to `tests/SecretPrinter.Responder.Tests/MdnsResponderTests.cs`:

| Test | Before the change |
| --- | --- |
| A record that answers one question is not also sent as an additional, whichever question comes first | failed |
| A record that answers two questions in one query is sent once | passed |

No requirement in `README.md` states this property, so neither test carries a
marker.

## Breaks no test catches

Three breaks were tried, each in turn and each restored afterwards:

- **Answers not left out of the additionals:** two tests failed, the first new
  one and `LinkLocalAnsweringTests`' test of an `ANY` query.
- **Additionals not de-duplicated:** two tests failed.
- **Answers not de-duplicated:** no test failed.

The third is why the second new test was added. It passed before the change and
after, and the same break now fails it. Every break tried fails a test.

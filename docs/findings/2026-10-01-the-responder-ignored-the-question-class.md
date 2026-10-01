# The responder ignored the question's class

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-01. Reviewed by a human before merge.*

**Status: fixed in the same change as this finding, which adds `REQ-ADV-025`.
Whether this ever affected a client is not known. No question in a class other
than IN has been noted in any capture this project has made, but no capture was
searched for one.**

## What was found

Every DNS question carries a name, a type and a class. `DnsMessage` parses all
three. `MdnsResponder` used the name and the type and discarded the class, in
three places:

1. **Answering.** `HandleAsync` matched records by name and type only, so a
   question in any class was answered with the service's records, all of which
   are in class IN.
2. **Probing.** `NoteSimultaneousProbe` took a question in any class, carrying
   a proposal that would win, for a competing probe. The responder then waited
   a second and probed again.
3. **Legacy unicast.** `DnsResponseBuilder.AddQuestion` wrote every repeated
   question in class IN, so a legacy querier that asked in class ANY was sent
   back a question it had not asked.

It was found on 2026-10-01 while reading `HandleAsync` before building the
`NSEC` (`REQ-ADV-022`). Nothing in the repository claimed the class was read,
so no statement was false. The behaviour departed from RFC 6762 all the same.

## What RFC 6762 says

Read from rfc-editor.org on 2026-10-01:

- **§6:** a record answers a question by the standard DNS rules. Its name must
  match, its type must match unless the question asks for ANY, and its class
  must match unless the question's class is ANY. Class 1 (IN) is the usual one,
  but if a client uses other classes, those rules are a MUST.
- **§6.7:** a response to a legacy unicast querier must repeat the query
  identifier and the question given in the query.
- **§8.2:** a probe is a query that carries, in its Authority Section, a record
  that answers its question.

## How it was measured

First, a throwaway check in Claude's container, not kept: a question for
`secretprinter.local`, type A, was handled in class 1 (IN), class 3 (CHAOS) and
class 255 (ANY). All three were answered with the class IN `A` record.

Then the eight tests below were written before the fix. Against the old code,
four failed, one for each part of the defect (the first part twice). The other
four passed, as controls should.

## Why it mattered now

The `NSEC` is a negative answer: it says a record does not exist. The service
may say that only about records it owns, and it owns only class IN records. Had
the `NSEC` been built on the old matching, a question in any class for a type
the service lacks would have been answered with a denial.

## What changed

- `MdnsResponder.HandleAsync` answers a question only when its class, read
  without the unicast-response bit, is IN or ANY. A question in another class
  contributes nothing to the answer. A query left with nothing to answer is
  counted as ignored, as one for a name the service does not hold is.
- `NoteSimultaneousProbe` considers only questions in class IN or ANY.
- `DnsResponseBuilder.AddQuestion` takes the class to repeat and writes it as
  given. The responder passes the class the question was asked in.
- `README.md` states the rule as `REQ-ADV-025`. Its markers are on `HandleAsync`
  and `NoteSimultaneousProbe`.

The eight tests are in
`tests/SecretPrinter.Responder.Tests/QuestionClassTests.cs`, each marked
`REQ-ADV-025`:

| Test | Before the fix |
| --- | --- |
| A question in class CHAOS for the host's `A` record is not answered | failed |
| A question in class ANY for the host's `A` record is answered with it | passed |
| A question in class IN with the unicast-response bit set is answered | passed |
| Of two questions, one in CHAOS and one in IN, only the IN one is answered | failed |
| A legacy unicast question in class ANY is repeated back in class ANY | failed |
| A legacy unicast question in class IN is repeated back in class IN | passed |
| A probe in class CHAOS that would win in class IN does not make the responder defer | failed |
| A probe in class ANY that would win makes the responder defer | passed |

`DnsQueryBuilder` writes class IN only, which is all the service asks about. The
tests therefore build each query with it and then overwrite the class field.
Before using the query, they read every class back through the real parser, so
a helper that wrote to the wrong bytes would fail the test rather than test
something else.

## Breaks no test catches

Five breaks were tried, each in turn and each restored afterwards:

- every class accepted (three tests failed);
- the legacy question always repeated in class IN (one failed);
- the probe check removed (one failed);
- the unicast-response bit not masked off (one failed);
- class ANY refused (three failed).

Every break failed a test, and none hung. Nothing covers the change to
`tools/SecretPrinter.Respond` below, because the tools have no test suites.

## The experiment tools

- **`tools/SecretPrinter.Respond`** builds its responses with `SecretPrinter.Dns`,
  so the change to `AddQuestion` reached it. It now repeats a legacy question in
  the class it was asked in. Its matching still ignores the class. This is now
  stated in its header. It is an experiment tool, not shipped, and it is left as
  it was measured with.
- **`tools/SecretPrinter.Respond6`** has its own DNS code. It reads the class
  and does not use it when matching. It is to be deleted once the `NSEC` is in
  the service, and it is not changed here.

## Not affected

`NoteConflicts` reads the records in other devices' responses, not questions.
It already considered class IN records only.

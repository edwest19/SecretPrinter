# The relay permitted every client when given no network

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-30. Reviewed by a human before merge.*

**Status: fixed in the same change as this finding. No shipped behaviour
changed: the service always gave the relay a network. The check now fails
closed.**

## What was found

`IppRelay` enforces `REQ-SEC-012`, that relayed connections are accepted only
from the configured client networks, with `RelayOptions.AllowedClientNetworks`.
An empty list meant no restriction: every connection was relayed, from any
address. The default was empty.

This was documented. The comment on the setting said empty "means no
restriction, which the service must never configure". `ServiceHost` always set
one network per client interface, so the service as built never relayed for a
client outside its networks, and nothing in the repository was false.

What held the line was that comment. A caller that left the list out would have
had an open relay, and neither the compiler nor the relay would have objected.

It came to light while preparing the relay for IPv6, where a link-local
connection is to be accepted only with the client interface's scope
([finding](2026-09-30-which-ipv6-addresses-to-publish.md)). That rule refuses
when nothing is configured. Beside a network list that permitted everything
when empty, one check would have behaved two opposite ways.

## What was decided

Claude set out two options: make the check fail closed first, as its own
change, or leave the network list as documented and add only the IPv6 rule.
**Edwin chose to make it fail closed, on 2026-09-30, on Claude's
recommendation.**

## What changed

- `AllowedClientNetworks` is a `required` member. Leaving it out is a compile
  error, so no caller can fall into a default.
- A relay built with no list at all (null) is refused when it is built.
- An empty list refuses every connection, and the refusal says that no client
  network is permitted, so the log shows a fault in configuration rather than
  what looks like a foreign client.
- The `REQ-SEC-012` marker on `IppRelay.RelayOneAsync` says so.
- The tests that are meant to relay now name their client network, where they
  had relied on the empty list letting them through.

Three tests were added, each marked `REQ-SEC-012`. All three failed against the
code before the change; the first failed because the connection was relayed.

## A test that would have hung rather than failed

Breaking the network check on purpose, so that every network matched, did not
make the existing foreign-client test fail: it hung the suite. That test waited
for the relay with no time limit, and a client relayed by mistake waits on the
printer forever. On a build server that is a stalled run, not a red one.

It now waits with the file's existing five-second limit, as the other relay
tests do, so a broken refusal fails the test. A refused client returns at once,
so the test is no slower when the refusal works.

## Breaks no test catches

None found among the five tried: an empty list permitting everything again, no
check for a missing list, the list not required, every network matching, and a
refusal that does not give its reason. Each failed a test, and none hung.

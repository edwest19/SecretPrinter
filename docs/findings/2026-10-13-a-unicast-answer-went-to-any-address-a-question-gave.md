# A unicast answer went to any address a question gave

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-13. Reviewed by a human before merge.*

**Status: corrected in the change this finding comes with, and measured against
the code only. Since the first commit, the service's mDNS responder has answered
a legacy query, one sent from a port other than 5353 (README `REQ-ADV-017`), by
unicast to whatever address the query gave as its source, and nothing checked
that address. The socket sets the adapter a multicast leaves by, and not the
one a unicast datagram leaves by, which Windows chooses from its routing table.
So a device on the client network that forged the source address of a query
could have had the service's answer sent out of the printer-side adapter, or
through a router to an address on neither network. Releases 0.1.0 and 0.2.0 do
this. Nothing of the kind has been seen on a network, and nobody is known to
have tried it. The responder now sends an answer by unicast only to an address
on the network the query arrived from, as README `REQ-SEC-017` states. Eighteen
new tests show it in Claude's workspace, with the test kit's fake transport in
place of a network.**

*(Status note, later on 2026-10-13, by Claude, Claude Opus 5.5: run on a network
once, with the change that answers by unicast a question asking for it
(`REQ-ADV-028`). 29 answers went by unicast, each to an address the check
allowed, and none was refused. No legacy querier is known to have asked, and
whether the IPv6 source address of a received datagram carries its scope on
Windows is still not established. See
[the finding of the run](2026-10-13-twenty-nine-answers-went-by-unicast-on-a-network.md).)*

No address of a real device appears in this finding. The addresses below are
made up or from the ranges set aside for documentation.

## How it was found

On 2026-10-13, reading RFC 6762 for the third of the four items recorded on
2026-10-07 as not done: a question that asks for a unicast answer is answered
by multicast (§5.4;
[finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md),
item 4). That item adds a second kind of answer sent by unicast, so before
designing it Claude read where the first kind goes. The section after §5.4 asks
a check the code did not make.

## What the code did

- **`MdnsResponder.HandleAsync`,** for a datagram whose source port is not
  5353, built the answer and passed it to the transport's `SendUnicastAsync`
  with the datagram's source address and port as the destination, and the
  interface the query arrived on as the interface to send through.
- **`MdnsSocket.SendAsync`,** behind `SendUnicastAsync`, checks that the
  interface is one the socket was opened for, sets `IP_MULTICAST_IF` (or
  `IPV6_MULTICAST_IF`) to it, and sends. Microsoft documents `IP_MULTICAST_IF`
  as "the outgoing interface for sending IPv4 multicast traffic", and has a
  separate option, `IP_UNICAST_IF`, "the outgoing interface for sending IPv4
  traffic. … This option is important for multihomed computers."
  `IPV6_MULTICAST_IF` and `IPV6_UNICAST_IF` are described the same way for
  IPv6. The service sets neither unicast option. So the adapter a unicast
  answer leaves by is Windows' choice, from its routing table. (Read on
  2026-10-13 at learn.microsoft.com, "IPPROTO_IP socket options" and
  "IPPROTO_IPV6 socket options", through a tool that fetches a page and
  returns passages; not compared with the pages by hand.)
- **The summary and the marker on `SendUnicastAsync`** said the datagram is
  sent "out of one specific interface", and that the method refuses "to send
  out any interface the socket was not opened for". The second is true of the
  interface named, and not of the adapter the datagram leaves by.

## What that allows

Only a device whose packets arrive on the client adapter can start it, which in
a household means a device on the client network: the responder's socket joins
the mDNS group on the client interfaces only, and a datagram that arrives on any
other interface is not answered (`REQ-ADV-011`). Such a device would send a
query to the mDNS group, from a port other than 5353, with a source address that
is not its own:

- **an address on the printer network.** The answer would leave by the
  printer-side adapter, to that address. That is the service sending its
  advertisement out of an interface `REQ-ADV-011` says it does not advertise
  on.
- **an address on neither network.** The answer would go to the client
  network's router, which might send it on. `REQ-SEC-008` says the service
  transmits nothing to any host outside the two networks.
- **many times, to one address.** The answer is many times the size of the
  question (below), so the service would turn a device's small packets into
  larger ones aimed at someone else.

Over IPv6 the same holds for a source address that is not link-local, which
Windows also routes by its table. A link-local source address, forged or not,
names a host on the link it is read with. If Windows gives it the scope of the
interface it arrived on, as it gives the relay's connections, an answer to it
stays on that link.

The broadcast address of the client network is a case apart. Microsoft documents
that a send to a broadcast address fails with `WSAEACCES` unless the socket has
`SO_BROADCAST`, which this one does not (learn.microsoft.com, the `sendto`
function, read the same way). So an answer to that address would not be sent at
all, and the receive loop would report the error once (`REQ-LIF-005`). That
was not measured, and it is not changed here.

## What RFC 6762 says

Read on 2026-10-13 through the same tool, from the RFC's text at
rfc-editor.org; not compared with the RFC by hand. §5.5, about a query sent by
unicast to port 5353:

> Since it is possible for a unicast query to be received from a machine
> outside the local link, responders SHOULD check that the source address in
> the query packet matches the local subnet for that link (or, in the case of
> IPv6, the source address has an on-link prefix) and silently ignore the
> packet if not.

The queries this finding is about are sent to the multicast group, not by
unicast. In §6.7, on legacy queriers, the tool found no such check; its reading
was that a responder "MUST send a UDP response directly back to the querier,
via unicast, to the query packet's source IP address and port". The check is
made here because what §5.5 guards against, an answer leaving the local link,
is what a forged source address brings about whichever way the query came.

## Measured

A scratch program, not in this repository, built in Claude's workspace against
the repository's `SecretPrinter.Responder` and `tests/SecretPrinter.TestKit` at
commit `9664990`, before any change. It made a responder for one client
adapter, at an address in 192.168.1.0/24, over the test kit's fake transport,
and asked it for the `_ipp._tcp` PTR four times:

| The query | What the responder asked the transport to send |
| --- | --- |
| From 192.168.1.50, port 49152 | By unicast to 192.168.1.50:49152, through the client adapter, 499 bytes |
| From 192.168.12.77, port 49152 | By unicast to 192.168.12.77:49152, through the client adapter, 499 bytes |
| From 203.0.113.9, port 49152 | By unicast to 203.0.113.9:49152, through the client adapter, 499 bytes |
| From 192.168.1.50, port 5353, asking for a unicast answer | By multicast, 478 bytes |

The query was 33 bytes. The advertisement had a TXT record of three entries; a
printer's real TXT record is longer, and so would be the answer.

"Through the client adapter" is the interface the responder named. Where
Windows would send each datagram is what the documentation above says, and was
not measured.

## What the responder does now

An answer sent by unicast goes only to an address on the network the query
arrived from (`MdnsResponder.IsOnArrivalNetwork`):

- **Over IPv4,** the address must be on the client network as the relay works
  it out from the client adapter's address (`REQ-SEC-012`): its /24, or /8 when
  it begins with 10. That rule was `ServiceHost.PrefixLengthFor`, used only by
  the relay; it moved, unchanged, to `src/SecretPrinter.Mdns/ClientNetwork.cs`,
  which both now use.
- **Over IPv6,** the address must be link-local, and its scope must be the
  IPv6 index of the interface the query arrived on. A link-local address with
  no scope is refused, because which link it is on cannot be told.
- **A legacy query from any other address is not answered.** It is counted in
  `ResponderActivity.RefusedOffNetwork`, and the first line of the stop summary
  gives the count, zero included:
  "… N would have been answered by unicast to an address off the network they
  arrived from and were not answered (RFC 6762 s5.5)."
- **A query is counted as refused only when there was an answer to refuse.** One
  about a name the service does not hold is counted as that, whatever its
  address, and one whose every answer it already carried is counted as that
  (`REQ-ADV-026`).
- **An answer sent by multicast is not affected.** It goes to the group on the
  link the query arrived on, whatever the source address.

## Choices the RFC does not make

- **The relay's network, not the adapter's subnet mask.** The service already
  works out the client network from the adapter's address, and states that as
  a known problem (`REQ-SEC-012`). Using the same rule keeps one idea of the
  client network in the service. On a network wider than /24, a querier beyond
  the /24 gets no unicast answer; on one narrower, an address beyond the
  network but within the /24 would pass.
- **IPv6: link-local only.** The service publishes and listens on link-local
  addresses only (`REQ-ADV-021`, decided 2026-09-30), and the relay accepts
  only link-local clients on the client interface's scope (`REQ-SEC-012`).
  §5.5 speaks of "an on-link prefix", which a global address on the link would
  also have. A legacy querier on such an address is not answered.
- **Not answered, rather than answered by multicast.** A legacy querier does
  not listen for multicast answers, which is why §6.7 sends it one by unicast.
  §5.5 says "silently ignore".
- **A count, and no log line for each refusal.** A device sending such queries
  could otherwise fill the log. The count reaches the log once, at the stop.
- **`IP_UNICAST_IF` is not set.** Naming the adapter a unicast leaves by would
  not stop an answer to an address on neither network: it would go to the
  client network's router instead. The address is what has to be checked. It
  would also change the socket configuration that has been measured working
  with an iPhone. Not done.

## The tests

`tests/SecretPrinter.Responder.Tests/UnicastAddressTests.cs` (new), fourteen
tests, and `tests/SecretPrinter.Mdns.Tests/ClientNetworkTests.cs` (new), four
tests of the rule, of which one is new in kind: an IPv6 address given to the
rule is refused. The rule's other three tests pass on the old code's rule too,
which was moved unchanged; until now no test read it.

Of the fourteen, these failed before the change:

- a legacy question from an address on the printer network, from one on no
  local network, and from the next /24 along, is not answered;
- an address on one adapter's network is refused when the query arrives on the
  other adapter;
- over IPv6, a link-local address on another scope, a link-local address with
  no scope, and an address that is not link-local are refused;
- over IPv6, the scope is compared with the interface's IPv6 index, not its
  IPv4 one;
- the stop summary's first line gives the new count.

These passed before and after, and are there so that the check cannot be made
to pass by refusing too much: a legacy querier on the client network is
answered by unicast, at its own address; an adapter in 10.0.0.0/8 has the whole
/8; over IPv6, a link-local querier on the arrival scope is answered; a
multicast question from an address on no local network is still answered, by
multicast; a legacy question from off the network about another service is
counted as that.

Run against the code of `9664990` with only the new count added, 11 tests of
the responder suite failed: the nine above, and the two existing tests of the
stop summary, whose expected line now has the new count.

Four existing tests changed:

- `OncePerSecondTests.cs`: its two tests of a legacy unicast answer asked from
  192.0.2.41, which is not on the client adapter's network in those tests.
  They now ask from 192.168.1.41, through a new parameter of the helper that
  makes the query, and their header says so. Its test of the stop summary
  expects the new count.
- `KnownAnswerTests.cs`: its test of the stop summary expects the new count.

## Breaking each claim on purpose

Each mutation changed one thing, rebuilt (stopping if the build failed), ran
the suites named under a time limit, and restored the file.

| Mutation | Caught by |
| --- | --- |
| The IPv4 network not checked | Five tests: the four IPv4 refusals and the stop summary |
| The IPv6 scope not compared | The three tests about scopes |
| Any scope accepted for an address that is not link-local | "an address that is not link-local is not answered" |
| The IPv6 scope compared with the IPv4 index | "the scope is compared with the interface's IPv6 index" |
| A refusal not counted | Nine tests |
| The stop summary giving another count in the new place | Two tests of the stop summary, the ones whose counts differ |
| The check applied to multicast answers too | Twenty tests, among them the multicast one above |
| A refused query sent anyway | Nine tests |
| 10.0.0.0/8 taken as a /24 | The rule's /8 test and the adapter in 10.0.0.0/8 |
| Other addresses taken as a /16 | Two of the rule's tests, and two refusals, among them the printer network's |
| An IPv6 address not refused by the rule | The rule's IPv6 test |

The address family is not compared on its own in the check, and so not
mutated: `IsIPv6LinkLocal` is false for every IPv4 address, and an IPv4 network
contains no IPv6 address (read in Claude's workspace, runtime 10.0.12). A first
version of the check also compared the family. No test could have caught a
mutation removing either comparison, and both were taken out.

What no test catches: that the relay is given the same network as before. No
test runs `ServiceHost.RunAsync`, where the relay's network is worked out; the
line now calls `ClientNetwork.Of` where it called `PrefixLengthFor`, and the
rule's tests are what show the two give the same network.

## What it changes on a network

- **A legacy querier on the client network:** nothing. It is answered as
  before.
- **A legacy query with a source address off the client network:** no answer,
  where there was one.
- **Everything else:** nothing.

Whether any device on the household's client network sends legacy queries was
not looked for.

## How it was measured

In Claude's workspace, on Linux, with .NET SDK 10.0.112 and runtime 10.0.12,
built as described in earlier findings: the six test projects that build
directly, and the service and its tests through two scratch projects outside
the repository. That is not the build a release makes. Nothing in the code
that changed is specific to a platform.

- **Suites:** 28, 27, 38 (9 skipped, for lack of IPv6), 61, 44, 170, and 113
  in the service suite.
- **SpecCheck,** with the scratch service build added: 103 requirements, 23
  assemblies, 18 evidence rows, 496 test records, 85 requirements with a
  passing test, and the same 2 binding gaps as before this change,
  `REQ-ADV-019` and `REQ-ADV-020`, whose tests need IPv6. The coverage matrix
  gains the row `REQ-SEC-017`, `OK`; no other row changes.
- **On the development machine,** where every test runs, the numbers are taken
  by Edwin West before the commit, and the commit message gives them.

## What changed

- `src/SecretPrinter.Mdns/ClientNetwork.cs` (new): the rule, moved from
  `ServiceHost.cs`.
- `src/SecretPrinter.Responder/MdnsResponder.cs`: `IsOnArrivalNetwork`; the
  check in `HandleAsync`; `ResponderActivity.RefusedOffNetwork` and its count
  in `DescribeServed`; the `REQ-ADV-017` marker's text, and a `REQ-SEC-017`
  marker on each of the three.
- `src/SecretPrinter.Service/ServiceHost.cs`: the relay's network from
  `ClientNetwork.Of`; `PrefixLengthFor` removed.
- `src/SecretPrinter.Mdns/MdnsSocket.cs` and `IMdnsTransport.cs`: the summary of
  `SendUnicastAsync`, and the text of its `REQ-ADV-011` marker, say what it
  does. No code changed there.
- The two new test files, and `Program.cs` in each of the two test projects;
  `OncePerSecondTests.cs` and `KnownAnswerTests.cs` as above.
- `README.md`: the row `REQ-SEC-017`; dated notes on `REQ-ADV-011`,
  `REQ-ADV-017`, `REQ-SEC-001`, `REQ-SEC-008` and `REQ-SEC-012`; an entry under
  "Known problems" for releases 0.1.0 and 0.2.0, and the dated note at the head
  of that section.

## What this does not show

- **Any of it on a network, or on Windows.** Whether Windows passes a received
  datagram whose source address is not on the network it arrived on to the
  socket at all was not measured. If it does not, the old code could not have
  been made to do this over IPv4, and the check costs nothing.
- **That the IPv6 source address of a received datagram carries its scope on
  Windows.** If it did not, every legacy querier over IPv6 would now be refused.
  The relay relies on the same for its connections and has accepted link-local
  clients on the client scope; a datagram is not a connection.
- **That anyone has done this.** Nothing in any log or capture on record
  suggests it.
- **The relay.** It accepts connections only from the client network already
  (`REQ-SEC-012`), and is not changed.

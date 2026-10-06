# SecretPrinter

**A Windows service that makes an AirPrint printer on one network reachable from
another network, without joining those networks together.**

> **Authorship.** The source code, tests and documentation in this repository
> were written by Claude (Anthropic models: Claude Opus 4.5 for the original
> work, Claude Opus 5 and Claude Opus 5.5 for later changes), at the direction of
> Edwin West, and reviewed by a human before merge. Each file's header names the
> model that wrote it and, where a file has been changed since, the model that
> changed it. This is stated plainly
> because it is the point of the project, not a disclaimer buried in a footer.
> See [Authorship and AI involvement](#13-authorship-and-ai-involvement).

> **This document is the specification.** It is not an overview of the code; it
> is the definition the code must satisfy. Every normative statement carries an
> identifier such as `REQ-ADV-001`. Those identifiers appear in the source and
> in the tests, and a tool in this repository checks that every requirement has
> both. See [Section 11](#11-how-to-validate-this-readme-against-the-code).

### About the name

There is nothing secret about SecretPrinter.

It belongs to a family of projects — SecretArqc, SecretBeans, SecretCsv,
SecretKey, SecretLetter and others — that share the prefix as a joke. The name
means the opposite of what it says: every one of them is fully open, with its
source, its reasoning, and its limitations published. The prefix is there to
make that inversion obvious once you notice it.

If you arrived expecting something covert, the rest of this document is the
correction.

---

## Read this before installing

Five things about this software that you should know before it runs on your
machine. They are here, at the top, because burying them would defeat the
purpose of the project.

**1. Your print jobs pass through the machine running this service.**
SecretPrinter is a proxy, not a router. Your phone sends the document to this
service, and this service sends it to the printer. The document is held in
memory on the proxy machine while that happens. A design that avoided this was
evaluated and rejected as unworkable — see
[Section 2](#2-why-a-proxy-and-not-a-reflector). If you are not comfortable with
print jobs transiting that machine, do not use this software.

**2. This deliberately departs from the mDNS standard.**
Multicast DNS ([RFC 6762](https://www.rfc-editor.org/rfc/rfc6762)) is designed
to be link-local: names ending in `.local` are meant to be meaningful only on
one network segment. SecretPrinter makes a service on one segment visible on
another. That is a considered departure from the specification, not an
oversight, and it is why the service confines itself to printing.

**3. It advertises what the proxy does, not what the printer does.**
The service does not copy the printer's advertisement wholesale. It publishes a
narrower one describing what the proxy will actually deliver. Capabilities the
proxy does not relay — scanning, faxing — are not advertised, even though the
printer supports them.

**4. Your job is encrypted on one side and not the other.**
Between your phone and this service, the job travels as plain IPP over TCP, the
same way it would to many printers on a home network. Between this service and
the printer it is TLS, because this printer refuses to accept a job any other
way. So the document is readable by anything that can see traffic on the client
network, and the proxy is the point where it becomes encrypted. That is a
deliberate choice for a home LAN rather than an oversight, and it is stated here
so that anyone for whom it is not acceptable finds out before installing rather
than after. The certificate the proxy will accept is pinned by fingerprint in
configuration and checked on every connection; see
[Section 8](#8-requirements-security-and-trust-sec).

**5. The connection to the printer follows the Windows routing table.**
The service does not tie its connection to the printer to the printer-side
network adapter. Windows chooses the way out from its routing table, as it would
for any program. Normally that is the printer-side adapter, because it gives
Windows the most specific route to the printer's address. On three occasions
Windows has been measured choosing its default route instead, which sends the
attempt to connect out on the client network, addressed to the printer and
handed to the client network's router. The first time, the printer-side adapter
had no link, as it would after a WLAN drop; it had been disconnected by hand for
the measurement. The attempt was captured on the wire: five TCP SYN packets out
the client-side Ethernet, to the router, and nothing answered. The second time,
the printer-side adapter was up, with its address and its route to the printer's
network, but the printer had stopped answering on that network. Measured again
after the printer had come back, Windows chose the printer-side adapter. Why
Windows chose the default route while the adapter was up was not established,
and that time no packets were captured. The third time, two attempts in a row
took the default route and failed, and a later one took the printer-side
adapter and connected; the state of that adapter during the first two was not
read, no packets were captured, and by the operator's account nothing was
reconnected by hand in between.
The first two occasions were measured with Windows' own tools,
`Test-NetConnection` and `Find-NetRoute`, and the third with
`Test-NetConnection` alone, none of them with the service's connection. No
print job goes that way. The service sends job data only after a
TLS handshake with the pinned printer succeeds, and a connection that is never
answered never gets that far. What does leave is the attempt itself: the
printer's address and port, from the proxy's client-side address. The same fact
has a further consequence, which follows from how routing works but has not been
measured: if another adapter on the machine is connected to the printer's
network, Windows may route the connection through that adapter rather than the
one the configuration names. What the router does with such packets was not
observed either. See the findings of
[2026-09-22](docs/findings/2026-09-22-connections-to-the-printer-leave-by-the-default-route.md),
[2026-09-27](docs/findings/2026-09-27-the-printer-went-silent-and-the-join-held.md)
and
[2026-10-03](docs/findings/2026-10-03-the-printer-came-back-at-an-hourly-question.md).
(Updated 2026-10-03 by Claude, Claude Opus 5.5: until then this point said two
occasions and described two.)

---

## Table of contents

1. [Problem](#1-problem)
2. [Why a proxy and not a reflector](#2-why-a-proxy-and-not-a-reflector)
3. [Architecture](#3-architecture)
4. [Requirements: advertisement (ADV)](#4-requirements-advertisement-adv)
5. [Requirements: resolution (RES)](#5-requirements-resolution-res)
6. [Requirements: relay (PXY)](#6-requirements-relay-pxy)
7. [Requirements: configuration (CFG)](#7-requirements-configuration-cfg)
8. [Requirements: security and trust (SEC)](#8-requirements-security-and-trust-sec)
9. [Requirements: lifecycle and observability (LIF, OBS)](#9-requirements-lifecycle-and-observability-lif-obs)
10. [Requirements: build and distribution (DIST)](#10-requirements-build-and-distribution-dist)
11. [How to validate this README against the code](#11-how-to-validate-this-readme-against-the-code)
12. [Repository layout](#12-repository-layout)
13. [Authorship and AI involvement](#13-authorship-and-ai-involvement)
14. [Open questions](#14-open-questions)
15. [License](#15-license)

---

## 1. Problem

A household has an AirPrint printer on one wireless network. Phones and laptops
on a second, unrelated network cannot print to it.

The two networks have no route between them. They are separate consumer
networks with separate gateways, and the second gateway offers no way to add a
static route. One machine is connected to both.

Two things must be true for AirPrint to work, and both fail here:

| Requirement | Status across separate networks |
| --- | --- |
| The client can **discover** the printer | Fails: mDNS is link-local and does not cross subnets |
| The client can **reach** the printer's IP | Fails: no route exists between the networks |

Fixing only the first produces a printer that appears in the list and then fails
on every job: the client is given the printer's own address, which it has no
route to ([Section 2](#2-why-a-proxy-and-not-a-reflector)).

## 2. Why a proxy and not a reflector

The obvious design is an **mDNS reflector**: copy multicast DNS packets from one
network to the other, unmodified. It is simple, and it is what most existing
tools do.

It does not work here, for a specific reason. The printer's mDNS response
contains an `A` record with its own address on its own network. A reflector
copies that response unmodified — that is what makes it a reflector. The client
then believes the printer lives at an address it has no route to, and the
connection fails at TCP.

A reflector is the correct design **when IP routing already works between the
segments** and only discovery is missing. That was not the case here, and could
not be made the case.

SecretPrinter therefore acts as a **proxy**. It advertises *itself* on the
client network, at an address clients can reach, and relays the print job to the
real printer over its other interface.

| | Reflector | Proxy (chosen) |
| --- | --- | --- |
| Requires routing between networks | Yes | No |
| Sees print job contents | No | **Yes** |
| Survives printer address changes | Yes | Yes, via runtime resolution |
| Exposes non-printing services | Yes, all of them | No |

The proxy's cost is real and is disclosed at the top of this document: job data
transits the proxy machine. The reflector's cost is that it does not work in
this situation, and additionally that it bridges every mDNS service on both
networks, not only printers.

## 3. Architecture

```
   CLIENT NETWORK                PROXY HOST                PRINTER NETWORK
                          (dual-homed Windows machine)

  ┌──────────┐                ┌──────────────┐               ┌──────────┐
  │  Phone   │                │ SecretPrinter│               │ Printer  │
  │          │  1. discover   │              │               │          │
  │          │ ──────────────>│  Advertiser  │               │          │
  │          │ <──────────────│  (mDNS)      │               │          │
  │          │  2. proxy's    │              │               │          │
  │          │     own addr   │              │  3. resolve   │          │
  │          │                │  Resolver    │ ─────────────>│          │
  │          │  4. IPP job    │  (mDNS)      │ <─────────────│          │
  │          │ ──────────────>│              │               │          │
  │          │                │  Relay       │  5. IPP job   │          │
  │          │                │  (TCP)       │ ═══TLS═══════>│          │
  └──────────┘                └──────────────┘               └──────────┘
```

1. The phone browses for AirPrint printers on the client network.
2. SecretPrinter answers, advertising itself at its own client-network address.
3. On receiving a connection, SecretPrinter resolves the real printer's current
   address by mDNS on the printer network.
4. The phone sends the IPP job to SecretPrinter.
5. SecretPrinter opens a TLS connection to the printer and relays the job.

Step 4 is plaintext and step 5 is encrypted. That is not a preference; the
ET-3760 answers `Get-Printer-Attributes` over plain IPP but refuses
`Validate-Job` with `426 Upgrade Required`, so a job cannot be submitted without
TLS. The printer advertises `_ipps._tcp` on the same port 631 and accepts TLS
from the first byte, which is why the relay needs no HTTP upgrade handshake and
still parses nothing it carries. Measured on 2026-09-15; see
[findings](docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md).

Step 5 is not bound to the printer-side interface. The relay's outbound socket
is created without a local address, so Windows chooses its path from the
routing table. Normally that is the printer network. Windows has been measured
choosing the default route on the client network with the printer-side link
down, and also with the link up while the printer was not answering on its
network; why it chose that the second time is not established. Both were
measured with `Test-NetConnection` and `Find-NetRoute`, not with the relay's
socket. This is disclosed as the fifth item under
[Read this before installing](#read-this-before-installing).

The address and port for step 5 come from the printer's `_ipps._tcp` service,
and the capabilities advertised in step 2 come from the TXT record of its
`_ipp._tcp` service (REQ-RES-007). On this printer both services name port 631.
That is observed, not relied on.

Address resolution happens at connection time, because printers receive their
addresses by DHCP and those addresses change. During development the target
printer moved twice. Startup also resolves both services, before anything is
advertised, and takes the capabilities from that answer. Until both have
answered, and before that until the printer-side adapter is usable at all, the
service offers nothing to the client network and waits (REQ-LIF-008). A
configuration naming a service the printer does not advertise therefore waits
too, and says so in the log; the service cannot tell that from a printer that is
switched off. Each connection still resolves for itself, reusing an earlier
answer only as REQ-RES-004 allows.

## 4. Requirements: advertisement (ADV)

What the service publishes on the client network.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-ADV-001 | MUST | The service advertises an IPP printer service (`_ipp._tcp.local`) on each configured client interface. |
| REQ-ADV-002 | MUST | The service advertises the AirPrint subtype `_universal._sub._ipp._tcp.local`. Measured: iOS queries this subtype exclusively and never the base service type, so a responder omitting it is never discovered. |
| REQ-ADV-003 | MUST | The `SRV` record published for the proxy names a hostname owned by the proxy, and the `A` record for that hostname contains the proxy's own address on the interface the query arrived on. |
| REQ-ADV-004 | MUST NOT | The service publishes the real printer's IP address, hostname, or `A` record on the client network. |
| REQ-ADV-005 | MUST NOT | The service publishes the real printer's `UUID` TXT value. It generates and persists its own UUID, distinct per advertised service. |
| REQ-ADV-006 | MUST NOT | The service publishes any TXT key asserting a capability the proxy does not relay. As of this version that includes `Scan` and `Fax`. |
| REQ-ADV-007 | MUST NOT | The service publishes a certification claim it does not itself hold, including `mopria-certified`. |
| REQ-ADV-008 | MUST | Any `adminurl` published either points at a resource reachable from the client network, or is omitted. The printer's own `adminurl` is never copied verbatim. |
| REQ-ADV-009 | MUST | Format-capability TXT keys obtained from the printer (`pdl`, `URF`, `Color`, `Duplex`, `PaperMax`, `kind`) are published unchanged, because they describe what will actually print. |
| REQ-ADV-010 | MUST | The advertised TXT record set is derived from a live query to the printer, never from values hardcoded in source. |
| REQ-ADV-011 | MUST NOT | The service advertises on any interface not listed in configuration as a client interface. |
| REQ-ADV-012 | MUST | The service responds only to queries for service types it advertises, and ignores all other mDNS queries. |
| REQ-ADV-013 | MUST | Outgoing mDNS packets carry IP TTL 255, per RFC 6762 §11. |
| REQ-ADV-014 | MUST | The service binds UDP 5353 with `SO_REUSEADDR` and does not require exclusive use of the port, so it coexists with the Windows DNS Client service. |
| REQ-ADV-015 | MUST | The arrival interface of each received query is determined from `IP_PKTINFO`, not inferred from the socket's bound address. |
| REQ-ADV-016 | SHOULD | The advertised instance name makes the proxy's role evident to a person reading the printer list, rather than impersonating the printer. |
| REQ-ADV-017 | MUST | The service identifies legacy unicast queriers by a source port other than 5353 (RFC 6762 §6.7), answers them by unicast rather than multicast, echoes the query identifier, and caps response TTLs. |
| REQ-ADV-018 | MUST | The service receives and answers mDNS queries over IPv6 on `ff02::fb` port 5353, on each configured client interface, in addition to IPv4 on `224.0.0.251`. It answers with the same records it would send over IPv4, delivered over the address family the query arrived on: a query received over IPv6 is answered over IPv6, a query received over IPv4 over IPv4, and neither is answered over both. Measured: an iPhone on the client network sent the same mDNS queries over IPv4 and IPv6, and only the IPv6 copies reached the service's machine, so a responder holding an IPv4 socket alone did not receive the question. Where the IPv4 copies were lost is not established; see [findings](docs/findings/2026-09-24-ipv4-mdns-from-behind-the-access-point.md). |
| REQ-ADV-019 | MUST | Outgoing IPv6 mDNS packets carry hop limit 255, per RFC 6762 §11, as REQ-ADV-013 requires of the IPv4 TTL. |
| REQ-ADV-020 | MUST | The arrival interface of a query received over IPv6 is determined from `IPV6_PKTINFO`, not inferred from the socket's bound address, as REQ-ADV-015 requires for IPv4. |
| REQ-ADV-021 | MUST | The service publishes an `AAAA` record for its own hostname for each IPv6 link-local address (fe80::/10) that the client interface holds in the preferred state, and no other IPv6 address: not the interface's global or unique local addresses, whether public or temporary, and nothing from another interface. The relay listens on exactly the addresses published: its listeners are read out of the published records, and they open together or not at all, because an address published and not listened on would produce a printer that is discovered and cannot be reached. With no preferred link-local address, no `AAAA` is published and print jobs are accepted over IPv4 only. This departs on purpose from RFC 6762 §6.2, which asks for every address valid on the interface: every interface has a link-local address (RFC 4291 §2.1), so every IPv6 device on the link has one to connect from; a link-local address cannot be reached from beyond the link; and it is recognised by its prefix rather than by a platform flag ([the finding](docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md)). The addresses are read once, at startup, as the IPv4 address is. If a published address is gone when the listeners next open, the service logs why, says goodbye and stops, to be restarted; RFC 6762 §8.4 asks a host to announce its new addresses instead, and this does not. What happens to a listener whose address disappears while it is open is not established. |
| REQ-ADV-022 | MUST | The service says what its own names do not have, so that a client gets a definite negative and not silence. It owns two names, its host name and its service instance name, having probed for both with type `ANY` (REQ-ADV-023). When a question in class IN or ANY (REQ-ADV-025) asks for a type that one of those names has no record of, the service answers with an `NSEC` record for that name in the Answer section, as RFC 6762 §6.1 requires. The `NSEC` lists the types the name does have: `A`, and `AAAA` when one is published (REQ-ADV-021), for the host name; `TXT` and `SRV` for the instance name. It is in the restricted form of §6.1: its next domain name is its own name, and it has one type bitmap. A question for type `NSEC` itself is answered the same way. A question for type `ANY` is answered with the records at the name and no `NSEC`. The service-type names (REQ-ADV-001, REQ-ADV-002) are shared with other devices, and RFC 6762 §6 forbids a negative answer for a shared name, so a question for a type the service does not have there is not answered. The service gives no such answer while it is probing, while it offers nothing (REQ-LIF-006), or after a conflict (REQ-ADV-024). A response that carries an address record of the host, in any section, when the host has no address record of the other type, also carries the host's `NSEC` in the Additional section (RFC 6762 §6.2). For this service that is a host with an `A` record and no `AAAA`. RFC 6762 §6.1 also permits an `NSEC` beside any other unique answer, and the service does not send one there. The `NSEC` for the host name has a TTL of 120 seconds and the one for the instance name 4,500 (RFC 6762 §6.1, §10), and both have the cache-flush bit set. A legacy unicast answer (REQ-ADV-017) carries the same `NSEC` with its TTL capped and the bit clear; what a legacy resolver does with it has not been measured. An `NSEC` is never announced and never proposed in a probe, and the goodbye (REQ-LIF-003) retracts both. Both are listed in the startup log (REQ-OBS-003). **Measured on 2026-10-02** on the test machine, running commit `64e8fc2` ([the finding](docs/findings/2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md)): an iPhone asked for types 65, `AAAA` and `A` at the service's host name twelve times, and each time was answered within 7 milliseconds with the `NSEC`, listing `A` and `AAAA`, TTL 120, cache-flush bit set, beside the two address records; it printed a page 63 seconds after one of those answers; the goodbye was captured retracting both `NSEC` records; and the log held no conflict. Seen only in tests, with a fake transport, and not on a network: the instance name's `NSEC` as an answer, the `NSEC` as an additional (the test machine's host name has both address types), and the legacy unicast answer. In the same runs, twice, the iPhone reached the printer and sent no job. The same failure is on record from 2026-10-01 on a build that sent no `NSEC`; its cause is not established, and the finding says why it is not attributed to this requirement. Measured earlier, and the reason for this requirement: on 2026-09-29 `HTTPS` and `AAAA` questions for the service's host name, from two devices, went unanswered ([the finding](docs/findings/2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md)). Measured on 2026-09-06 with the experiment tool `SecretPrinter.Respond6`: an iPhone sent IPv4 SYNs to port 631 seventy milliseconds after a response carrying an `A` record. Whether that response carried an `NSEC` is not recorded, and whether an `NSEC` plays any part was not measured (open question 7). (Corrected 2026-10-01 by Claude, Claude Opus 5.5: this sentence said iOS accepted the `NSEC`, which was not measured.) (Note, 2026-10-02, by Claude, Claude Opus 5.5: `SecretPrinter.Respond6` was deleted on 2026-10-02; [section 12](#12-repository-layout) says where its source is.) (Rewritten 2026-10-01 by Claude, Claude Opus 5.5: until then this row asked only for an `NSEC` in a response carrying an `A` record for a name with no `AAAA`. It did not cover a question for a type one of the service's names lacks, which RFC 6762 §6.1 requires an answer to, and it did not say where the record goes, what it lists, its TTL, or when it is not sent.) (Updated 2026-10-02 by Claude, Claude Opus 5.5: from 2026-10-01 until then this row said the behaviour was not yet measured on hardware, that nothing was marked with this requirement, and that the coverage matrix reported it NOT IMPLEMENTED.) |
| REQ-ADV-023 | MUST | Before announcing on a client interface, at startup and again before restoring an advertisement after any withdrawal, the service probes for the names it advertises as unique (the service instance name and the host name), as RFC 6762 §8.1 describes. It sends three queries for type `ANY`, 250 ms apart, each carrying in its Authority Section the records it proposes to publish, over both IPv4 and IPv6. It announces only if no conflict (REQ-ADV-024) arrives within 250 ms of the third. Both transports are used because on the network this was built on, IPv4 mDNS from part of the client network did not reach the service's machine. A simultaneous probe from another device is resolved as RFC 6762 §8.2 describes. The probes ask for multicast answers, although RFC 6762 §8.1 says they should ask for unicast ones: the service shares port 5353 with the Windows DNS Client (REQ-ADV-014), and which of two sockets sharing a port receives a unicast answer has not been established, so a unicast answer could be missed. |
| REQ-ADV-024 | MUST | A conflict is any response from another device that carries a record for one of those names that the service would not itself publish: a different type, or the same type with different data. Identical records are not a conflict (RFC 6762 §9). An `NSEC` record for one of those names is judged by the types it lists. One that lists exactly the types the service publishes at that name is not a conflict, whoever sent it; that is also how the service's own `NSEC` (REQ-ADV-022), heard back, is recognised. One that lists anything else, or that is not in the restricted form of RFC 6762 §6.1, is a conflict. Its next domain name is not compared, because §6.1 says a receiver should ignore that field. On a conflict, found while probing or at any time after announcing, the service offers nothing, as REQ-LIF-006 describes. It stays that way until restarted, even while the printer is reachable, and does not pick another name. The log gives the name, the record type and the address of the device that answered, so the operator can choose a different name. Any device on the client network can cause this by answering for the service's names. mDNS has no authentication, and this is accepted for a home network. (Added 2026-10-01 by Claude, Claude Opus 5.5: the four sentences about `NSEC` records. Until commit `838e877` the code treated any `NSEC` for one of those names as a conflict, and this row did not mention them.) |
| REQ-ADV-025 | MUST | The service answers a question only when its class, read without the unicast-response bit (RFC 6762 §5.4), is IN (1) or ANY (255). Every record the service sends is in class IN, and RFC 6762 §6 lets a record answer a question only when their classes agree or the question's class is ANY. A question in any other class is ignored, as a question for a name the service does not hold is (REQ-ADV-012), and it is not taken for a competing probe (REQ-ADV-023). A legacy unicast answer (REQ-ADV-017) repeats each question in the class it was asked in, as RFC 6762 §6.7 requires. Until 2026-10-01 the class was not read; see [the finding](docs/findings/2026-10-01-the-responder-ignored-the-question-class.md). |

## 5. Requirements: resolution (RES)

How the service finds the real printer.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-RES-001 | MUST | The printer's address is resolved by mDNS query on the printer-side interface at the time a connection is relayed. |
| REQ-RES-002 | MUST NOT | The printer's IP address is read from configuration as the primary means of locating it. |
| REQ-RES-003 | MUST | The printer is identified in configuration by mDNS service instance name, which is stable across DHCP address changes. |
| REQ-RES-004 | MAY | A resolved address is cached, for no longer than the DNS TTL of the record it came from. |
| REQ-RES-005 | MUST | A resolution failure causes the relay attempt to fail with a logged, specific error. It never falls back to a guessed or remembered-indefinitely address. |
| REQ-RES-006 | MUST | Resolution queries are sent only on the configured printer-side interface. |
| REQ-RES-007 | MUST | The address and port used to connect to the printer come from resolving the configured `_ipps._tcp` instance, on the printer-side interface, when a connection is relayed. The printer's capabilities come from the TXT record of the configured `_ipp._tcp` instance. Neither service supplies what the other is specified to supply. The service resolves both instances at startup, before advertising anything. If either cannot be resolved it logs the instance that did not answer and asks again as REQ-LIF-008 describes, advertising nothing meanwhile. |
| REQ-RES-008 | MUST | The service holds a printer-reachability state derived from mDNS resolution on the printer-side interface - the same query path a relayed job uses. It is never derived from the adapter being reported up, from its address being present, or from a multicast membership being listed. While the printer is reachable, the service reconfirms it on the cache-maintenance schedule of RFC 6762 §5.2: a query at 80-82% of the record's TTL, then 85-87%, 90-92% and 95-97% if unanswered, holding the printer unreachable once the record reaches 100% of its lifetime with no answer. While the printer is unreachable, it re-queries under the same section's continuous-querying rule - the first two queries at least one second apart, each interval at least double the last, capped at 60 minutes. An answer obtained by a resolution performed because a job arrived resets the schedule like any other answer, unless it is no newer than the answer already held or its lifetime is already over; a job's resolution that goes unanswered is not counted, because the reconfirmations already decide when the record has gone. Concurrent jobs share one in-flight query rather than issuing one apiece, whether or not it is answered. Measured: an adapter that had left `224.0.0.251` continued to list it, and an adapter reported up with a working unicast path returned no multicast answers (`docs/findings/2026-09-19-the-printer-side-multicast-membership-is-lost.md`). Interface state has been observed wrong in both directions; resolution has not. |
| REQ-RES-009 | MUST | The service reopens its printer-side mDNS socket before each question it puts to a printer it holds unreachable, and before each startup attempt after the first. Reopening examines the printer adapter again and, if the adapter is usable, opens a new socket joined to `224.0.0.251` on the adapter's current address, with a new resolver on it, then closes the old socket. If the adapter is not usable or the socket cannot be opened, the old socket is kept, and the reason is logged when it first appears and when it changes. Measured: on 2026-09-25 the resolver socket's membership of `224.0.0.251` was found gone after the printer-side Wi-Fi had been out for ten hours and then for three minutes more, and the adapter reconnecting did not bring it back; a new socket joined at once (`docs/findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md`). |

## 6. Requirements: relay (PXY)

How print jobs are moved.

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-PXY-001 | MUST | The service accepts TCP connections on the advertised IPP port, on configured client interfaces only. |
| REQ-PXY-002 | MUST | Accepted connections are relayed to the resolved printer address on the printer's IPP port. |
| REQ-PXY-003 | MUST NOT | The service inspects, parses, modifies, or interprets the IPP payload. Bytes are relayed unchanged in both directions. |
| REQ-PXY-004 | MUST NOT | The service writes any part of a print job to disk, at any point, including on error. |
| REQ-PXY-005 | MUST | Buffers holding job data are bounded in size; the service streams rather than accumulating a whole job in memory. |
| REQ-PXY-006 | MUST | Closing either side of a relayed connection closes the other. |
| REQ-PXY-007 | MUST | Connections that cannot be established to the printer are refused promptly, with a logged reason, rather than left hanging. |
| REQ-PXY-008 | MUST | Concurrent relayed connections are supported; one client must not block another. |
| REQ-PXY-009 | MUST | Log entries about a relayed connection record endpoints, byte counts, and timing — never job content. |
| REQ-PXY-010 | MUST | The connection to the printer is TLS from its first byte. No part of a print job is sent to the printer over an unencrypted connection. |
| REQ-PXY-011 | MUST NOT | The service parses, interprets, or emits HTTP in order to establish that TLS connection. It does not use the in-band `Upgrade: TLS/1.0` mechanism, so nothing in the relay reads the traffic it carries. |
| REQ-PXY-012 | MUST | A failed TLS handshake or a failed certificate check ends the relayed connection with a logged reason, before any job byte reaches the printer. There is no unencrypted fallback. |

## 7. Requirements: configuration (CFG)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-CFG-001 | MUST | Configuration is explicit. The service applies no default that changes what is advertised or where traffic is sent. |
| REQ-CFG-002 | MUST | Configuration names client interfaces and the printer interface separately and unambiguously. |
| REQ-CFG-003 | MUST | Configuration is validated at startup. Any inconsistency causes the service to fail to start, with a message naming the offending setting. |
| REQ-CFG-004 | MUST | Interface names given in configuration are resolved to addresses at startup, and the resolution is logged. |
| REQ-CFG-005 | MUST NOT | The service starts in a partially working state. Either every configured client interface is usable, or startup fails. The printer-side interface is the one exception, defined by REQ-LIF-008: while it is not usable the service offers nothing at all, which is withdrawn rather than partially working. |
| REQ-CFG-006 | MUST | The service refuses to start if a client interface and the printer interface resolve to the same interface. |
| REQ-CFG-007 | MUST | The printer's expected certificate fingerprint is explicit configuration: the SHA-256 hash of the DER-encoded certificate, written as exactly 64 hexadecimal digits in either case, with no separators and no whitespace. The service refuses to start without it or with a value in any other form, and names the setting when it refuses. |
| REQ-CFG-008 | MUST | The instance name of the printer's `_ipps._tcp` service is explicit configuration, separate from the instance name of its `_ipp._tcp` service. The service does not derive either name from the other. It refuses to start without the `_ipps._tcp` instance name, and names the setting when it refuses. |

## 8. Requirements: security and trust (SEC)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-SEC-001 | MUST NOT | The service forwards, reflects, or relays any mDNS traffic between networks. It answers queries with its own advertisement, and with `NSEC` records it builds from that advertisement to say which record types its own names have (REQ-ADV-022), and nothing else. (Amended 2026-10-01 by Claude, Claude Opus 5.5: this said "with its own advertisement and nothing else". An `NSEC` record is built from the advertisement and is not one of its records, so that sentence would have stopped being true when the service began sending them.) |
| REQ-SEC-002 | MUST NOT | The service exposes any service type other than those it is configured to proxy. Non-printing services on the printer network remain invisible from the client network. |
| REQ-SEC-003 | MUST NOT | The service opens, listens on, or relays any port other than those required for the configured print services and mDNS. |
| REQ-SEC-004 | MUST NOT | The service creates, modifies, or deletes firewall rules. Required rules are documented for the operator to apply. |
| REQ-SEC-005 | MUST NOT | The service reads or writes the Windows registry other than as required by the service control manager to run as a service. |
| REQ-SEC-006 | MUST | The service runs with the least privilege sufficient to bind its ports and join multicast groups, and documents what that is. |
| REQ-SEC-007 | MUST NOT | The service enables IP forwarding, alters the routing table, or otherwise changes host network configuration. |
| REQ-SEC-008 | MUST NOT | The service transmits anything to any host outside the two configured local networks. There is no telemetry, no update check, and no analytics. |
| REQ-SEC-009 | MUST | Every dependency is either the .NET base class library or a project inside this repository. Packages outside that require explicit documentation and justification in this file; see [Dependencies](#dependencies). |
| REQ-SEC-010 | MUST | The documentation states, prominently, that print job data passes through the proxy host. |
| REQ-SEC-011 | MUST NOT | The service acts as a general-purpose proxy, router, or NAT for any traffic. |
| REQ-SEC-012 | MUST | Relayed connections are accepted only from the configured client networks; connections from elsewhere are refused and logged. Over IPv6 that means link-local clients on the client interface itself: fe80::/10, arriving with that interface's scope, which is checked separately because a network match cannot see scopes. A relay given no permitted network refuses every connection. |
| REQ-SEC-013 | MUST | The printer's certificate is compared against the configured SHA-256 fingerprint on every connection, and the connection is abandoned if it does not match. No other hash algorithm is accepted in its place. The certificate is self-signed, so no chain and no hostname check can stand in for this. |
| REQ-SEC-014 | MUST NOT | Any configuration setting, command-line flag, build switch, or environment variable disables or weakens that comparison. There is no permissive mode. |
| REQ-SEC-015 | MUST | The documentation states, prominently, that print jobs travel unencrypted on the client network and are encrypted only between the proxy and the printer. |
| REQ-SEC-016 | MUST NOT | The probe, `SecretPrinter.Probe`, which a release includes beside the service (REQ-DIST-011), starts another program, reads or writes the Windows registry, makes an HTTP request, or writes a file. It does open a socket, and only its own assembly can: it puts its questions over UDP to the network of the interface address it is given, and reads the replies. (Added 2026-10-06 by Claude, Claude Opus 5.5. The probe has been in the release folder since 2026-10-04, and until this requirement nothing in this specification or its tests covered it; see [the finding](docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).) |

## 9. Requirements: lifecycle and observability (LIF, OBS)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-LIF-001 | MUST | The service starts and stops cleanly under the Windows service control manager. |
| REQ-LIF-002 | MUST | On shutdown the service leaves multicast groups, closes listening sockets, and terminates in-flight relays. |
| REQ-LIF-003 | MUST | On shutdown the service sends mDNS goodbye records (TTL 0) for everything it advertised, so clients drop it promptly. |
| REQ-LIF-004 | MUST | Fatal configuration or binding errors cause a fast, loud failure — never silent partial operation. |
| REQ-LIF-005 | MUST | Transient network errors are logged and retried, and do not terminate the service. |
| REQ-LIF-006 | MUST | While the printer is unreachable per REQ-RES-008, the service stops accepting new client connections, stops answering mDNS queries about itself, and withdraws its advertisement with goodbye records as REQ-LIF-003 requires on shutdown. It resumes all three when the printer answers again. Each change is logged once, with what was observed. The service does not offer a printer it has established it cannot reach. Measured: on 2026-09-20 the printer-side WLAN dropped at 06:49:38Z and the service, which correctly diagnosed the adapter as down, went on advertising and accepting jobs for nine hours; twelve jobs were accepted after the diagnosis and all twelve failed. |
| REQ-LIF-007 | MUST | Under the Windows service control manager, a configuration the service refuses is reported to the control manager as a failure to start, not left to time out, and the reason is in the log. Measured: on 2026-09-18 and 2026-09-19 nine refusals, each logged at once with its reason, each reached Windows as error 1053, "did not respond to the start or control request in a timely fashion". The code returned before it had called `ServiceBase.Run`, so the control manager was never answered. See [the finding](docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md). |
| REQ-LIF-008 | MUST | At startup the service does not refuse to start because the printer-side interface is unusable or the printer does not answer. It waits, offering nothing to the client network: no advertisement, no answer to any query, and no listener, as while withdrawn under REQ-LIF-006, with no goodbye records because nothing was announced. The printer-side adapter must exist by name when the configuration is loaded; its state is examined locally at a fixed interval, and it is usable once it is up, holds exactly one IPv4 address, that address is outside 169.254.0.0/16, and the platform does not report it tentative, deprecated or invalid. Then the printer is asked on the REQ-RES-008 schedule for an unreachable printer until both instances resolve. The adapter wait is logged when it begins, when its reason changes and when it ends; the printer wait when the printer first fails to answer and when it answers. A misspelled printer instance name is indistinguishable from a printer that is switched off, so it waits as well, and the log says so. Decided 2026-09-22, after nine refusals caused by the printer-side adapter being down at startup (`docs/findings/2026-09-22-a-refused-start-is-reported-as-a-timeout.md`). |
| REQ-OBS-001 | MUST | Startup logs list every interface in use, its resolved address, and its role. |
| REQ-OBS-002 | MUST | Every advertisement published is logged, including the full TXT record set. |
| REQ-OBS-003 | MUST | The operator can determine, from logs alone, exactly what the service told the client network. |
| REQ-OBS-004 | MUST NOT | Logs contain print job content, at any log level. |
| REQ-OBS-005 | SHOULD | Logs note that observed mDNS traffic may contain device names, so operators handle captures accordingly. |
| REQ-OBS-006 | MUST | When a relayed connection fails, the logged reason names which peer the failure came from and whether it happened while reading or writing — and carries nothing derived from job content. |
| REQ-OBS-007 | MUST | The shutdown summary reports queries seen and answered for each transport separately, naming both even when a count is zero, so an operator can tell whether anything was served over IPv6. |
| REQ-OBS-008 | MUST | Startup logs record the fingerprint the service will require of the printer, and each relayed connection logs the TLS protocol negotiated with it, so an operator can confirm from logs alone that the job went encrypted and to which device. |
| REQ-OBS-009 | MUST | The service writes its log to a file when the operator names one on the command line, and refuses to run under the Windows service control manager unless they do — a service has no console, so without a file it would report nothing anywhere. The path has no default, the service creates no folder to hold it, and the file is opened before configuration is read so that a refusal to start is recorded in it. |
| REQ-OBS-010 | MUST | Every log entry occupies exactly one line. Control characters in a message are escaped rather than written, so text learned from the network cannot insert a line break and forge an entry. |

## 10. Requirements: build and distribution (DIST)

| ID | Level | Requirement |
| --- | --- | --- |
| REQ-DIST-001 | MUST | The project targets .NET 10 (LTS) and builds with the published SDK version recorded in `global.json`. |
| REQ-DIST-002 | MUST | The solution builds with `TreatWarningsAsErrors` and produces zero warnings. |
| REQ-DIST-003 | MUST | Continuous integration builds the solution, runs all tests, and runs the specification checker described in Section 11. |
| REQ-DIST-004 | MUST | Release binaries are signed with Azure Trusted Signing. (Microsoft has since renamed that service Artifact Signing; this requirement means the same service. Note added 2026-10-04 by Claude, Claude Opus 5.5.) |
| REQ-DIST-005 | MUST | The release documentation states how to verify a signature, and that unsigned binaries are not official releases. |
| REQ-DIST-006 | MUST | Every release is a tagged commit with a changelog entry. |
| REQ-DIST-007 | MUST | Analyzer suppressions are justified in place, in source, with a written reason. Blanket suppression files are not used. |
| REQ-DIST-008 | MUST | Requirements that cannot be satisfied by code are recorded in `docs/verification.md`, each naming at least one artifact by repository-relative path. |
| REQ-DIST-009 | MUST | A requirement counts as tested only when a test carrying its identifier is recorded as having passed in a test-results file. A marker on a test that was skipped or failed does not count. |
| REQ-DIST-010 | MUST | Test results are accepted only when the assemblies they were produced against are byte-identical to those being checked. Results describing code that has since been rebuilt are refused. |
| REQ-DIST-011 | MUST | Release artifacts run without the .NET SDK or a separately installed .NET runtime. The service, and every tool the operating documentation tells someone using a release to run, are published self-contained. |

## 11. How to validate this README against the code

A specification that is only prose is a specification nobody checks. This one is
machine-checkable.

### The mechanism

**Step 1 — Requirements carry identifiers.** Every row in Sections 4–10 has an
ID like `REQ-PXY-003`. These are stable; they are never renumbered. A retired
requirement is marked withdrawn, not deleted.

**Step 2 — Code declares which requirement it implements.** Types and methods in
`src/` are marked with an attribute from `SecretPrinter.Spec`:

```csharp
[Requirement("REQ-PXY-003",
    "Payload bytes are copied between streams without inspection.")]
internal sealed class IppRelay
{
    // ...
}
```

**Step 3 — Tests declare which requirement they verify.**

```csharp
[Fact]
[Requirement("REQ-PXY-003")]
public async Task Relay_does_not_alter_payload_bytes()
{
    // ...
}
```

**Step 3a — Requirements that cannot be code are evidenced instead.** Some
requirements are properties of the build or release process rather than of any
type or method: `REQ-DIST-004` (release binaries are signed) can never carry an
attribute. Those are recorded in `docs/verification.md`, one row per
requirement, each naming the artifacts that satisfy it:

```
| REQ-DIST-004 | .github/workflows/release.yml | The sign step invokes Azure Trusted Signing. |
```

SpecCheck treats such a row as coverage **only after confirming every named path
exists**. Evidence pointing at a renamed or deleted file is reported as
`EVIDENCE BROKEN` and fails the check, so a claim cannot rot unnoticed. Entries
naming no artifact are ignored, which leaves the requirement uncovered.

Evidence is permitted for any requirement, because deciding which requirements
are "really" about code is not a judgement a tool should make. The safeguard is
visibility: requirements covered this way are reported as `OK (evidence)` and
counted separately in every run, so the proportion resting on evidence rather
than tests is always in view.

**Step 3b — Test markers only count when the test actually ran.** A
`[Requirement]` attribute on a test is compiled-in metadata: it exists in the
assembly whether or not the test ever executed. A test that skips on every
machine would otherwise count as full coverage.

The test suites therefore write a results file recording what executed:

```
PASS	REQ-ADV-013	SecretPrinter.Mdns.Tests.MdnsSocketTests.Open_sets_ttl_255
SKIP	REQ-CFG-006	SecretPrinter.Mdns.Tests.MdnsSocketTests.ResolveAll_rejects_shared_interface
```

SpecCheck reads it via `--test-results` and counts a requirement as tested only
when a test carrying its identifier PASSED. Anything else is reported as
`TEST DID NOT RUN`, with the reason, and fails the check.

This was not a hypothetical. `REQ-CFG-006` needs a network adapter holding two
IPv4 addresses; its test skipped on both the development machine and the target
machine, and the matrix reported it `OK` on both until this was added.

**Step 3c — Results must describe the build being checked.** A results file
records the module version id of every SecretPrinter assembly the run
exercised. Builds are deterministic, so that id is a function of content and
changes exactly when the code does.

SpecCheck compares those fingerprints against the assemblies it scans and
refuses any file produced against different code:

```
TEST RESULTS DO NOT DESCRIBE THIS BUILD:
  SecretPrinter.Advertising has been rebuilt since these results were produced
```

This also began as a real mistake rather than a hypothetical: a results file
left over from an earlier run was passed to SpecCheck and counted as coverage
for code it did not describe. The common form is editing code, rebuilding, and
re-running the check without re-running the tests.

Running SpecCheck without `--test-results` is permitted but never silent: the
summary reports `Test execution: NOT VERIFIED` and the result line says so.

**Running it.** Continuous integration
([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) discovers the test
suites, runs each one, and passes every results file to the checker. Doing it by
hand means one `--test-results` flag per suite, which went wrong on most
attempts during development and always in the direction of reporting less
coverage than existed.

**Until every requirement is covered, CI fails.** That is intended: the check
reports every binding requirement that has no implementation, naming each one in
the coverage matrix it writes to the run summary, and a green badge over an
incomplete specification would be the exact overstatement this repository exists
to avoid. An earlier version of this paragraph stated a fixed count of such
requirements. It was correct when written and wrong within days, which is why
the number is now read from the matrix instead of asserted here. The consequence is worth stating plainly - while the build is
red for a known reason, it is less useful at signalling a *new* problem, so read
which step failed rather than trusting the colour.

(2026-10-04: the last four binding requirements without coverage, REQ-DIST-004,
005, 006 and 011, were covered by evidence that day, and the check passed on
the development machine at the commit that added this note. Whether CI is green
is for its run page to say, not this paragraph. Note added by Claude, Claude
Opus 5.5.)

**Step 4 — A tool checks the correspondence.** `tools/SecretPrinter.SpecCheck`
parses the requirement tables out of this README, reflects over the built
assemblies, and produces a coverage matrix:

```
REQ-PXY-003   MUST NOT  impl: IppRelay                  test: Relay_does_not_alter~  OK
REQ-ADV-005   MUST NOT  impl: AdvertisementBuilder      test: (none)                 MISSING TEST
REQ-ADV-016   SHOULD    impl: (none)                    test: (none)                 NOT IMPLEMENTED
REQ-DIST-002  MUST      impl: evidence: Directory.B~    test: (none)                 OK (evidence)
```

The tool exits non-zero when any `MUST` or `MUST NOT` requirement lacks either an
implementation marker or a test, and CI fails on that. `SHOULD` requirements are
reported but do not fail the build.

It also fails on the reverse error: an attribute in code citing an ID that does
not appear in this README. Requirements and code cannot drift apart in either
direction.

### For an AI agent reviewing this repository

To check whether the code matches this document:

1. Run the checker from the repository root:

   ```
   dotnet run --project tools/SecretPrinter.SpecCheck -- \
     --readme README.md --evidence docs/verification.md --search .
   ```

   It reports which requirements are implemented, tested, evidenced, or none of
   these.
2. For each requirement, read the marked implementation and judge whether it
   actually does what the requirement says. **The tool proves a marker exists
   and that a test carrying it passed. It cannot prove the marker is honest, or
   that the test verifies anything meaningful.** That judgement is the
   reviewer's.
3. Pay particular attention to the `MUST NOT` requirements. Those are claims
   about what the software *does not do*, and they are verified by reading code
   and searching for absence — for example, confirming there is no file-write
   call anywhere in the relay path (REQ-PXY-004).
4. Report any behaviour present in the code but absent from this document. Undocumented
   behaviour is a defect here even when it is harmless, because the entire
   premise of the project is that this document is complete.

### Dependencies

The repository has exactly one *declared* package reference, which brings one
transitive dependency with it. Both are listed, because "one package" would have
been true of the csproj and false of what actually ships:

| Package | How | Why |
| --- | --- | --- |
| `System.ServiceProcess.ServiceController` 10.0.11 | Declared by `SecretPrinter.Service` | Provides `ServiceBase`, without which the program cannot register with the Windows service control manager. Published by Microsoft as part of .NET, but shipped out of band rather than in the base class library. |
| `System.Diagnostics.EventLog` 10.0.11 | Pulled in by the above | `ServiceBase` writes to the Windows Event Log when a service fails to start. SecretPrinter does not use it directly. |

The alternative was hand-written interop against `advapi32` - roughly 150 lines,
untestable, in exactly the code path where a mistake means the printer is never
retracted from clients' lists. That trade was judged the worse one.

**A note relevant to REQ-SEC-005:** the second package can write to the Windows
Event Log, which is a registry-backed facility. SecretPrinter's own code
references no registry type - tests read the compiled metadata of every assembly
of the service, and of the probe, to confirm it - but the capability is present
in the dependency graph via `ServiceBase`'s own failure reporting. Stating that
is more useful than a claim that reads as broader than it is. (Corrected
2026-10-06 by Claude, Claude Opus 5.5: this said "a test reads the compiled
metadata of every shipped assembly". The probe is shipped too, and until that
day no test read it; see
[the finding](docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).)

Everything else in this repository depends only on the base class library and on
other projects here. `REQ-SEC-009` is what keeps that true, and this table is
what it means by documented.

Confirm it yourself:

```
Select-String -Path (Get-ChildItem -Recurse -Filter *.csproj) -Pattern 'PackageReference'
```

## 12. Repository layout

```
SecretPrinter/
├── README.md                          This specification
├── LICENSE                            MIT                                [done]
├── SECURITY.md                        How to report a vulnerability      [done]
├── CHANGELOG.md                       Per-release history                [done]
├── .gitignore                         Ignore rules, incl. captures       [done]
├── .gitattributes                     Line endings, fixed per file type  [done]
├── global.json                        Pinned SDK version                 [done]
├── Directory.Build.props              Shared build settings              [done]
├── SecretPrinter.slnx                                                    [done]
├── run-tests.ps1                      Build, run every suite, save results [done]
├── run-speccheck.ps1                  Run SpecCheck on those results     [done]
├── publish-release.ps1                Publish the release folder         [done]
│
├── docs/
│   ├── architecture.md                Design rationale in depth
│   ├── verification.md                Evidence for non-code requirements [done]
│   ├── operating.md                   Install, firewall, privileges      [done]
│   ├── threat-model.md                What this does and does not defend against
│   └── findings/                      Dated records of what was measured [done]
│                                      One file each, YYYY-MM-DD-subject.md
│
├── src/
│   ├── SecretPrinter.Spec/            The [Requirement] attribute        [done]
│   ├── SecretPrinter.Dns/             DNS wire format; no sockets        [built]
│   ├── SecretPrinter.Mdns/            mDNS socket layer                  [built]
│   ├── SecretPrinter.Advertising/     What to publish; no sockets        [built]
│   ├── SecretPrinter.Responder/       Answers queries; announces; goodbye [built]
│   ├── SecretPrinter.Resolution/      Finds the printer, on demand       [built]
│   ├── SecretPrinter.Configuration/   Loads and validates settings       [built]
│   ├── SecretPrinter.Proxy/           TCP relay; no file I/O at all      [built]
│   └── SecretPrinter.Service/         The host; wires everything up      [built]
│
├── tools/
│   ├── SecretPrinter.Probe/           Read what a printer advertises     [built]
│   ├── SecretPrinter.Listen/          Observe IPv4 mDNS; transmits nothing [built]
│   ├── SecretPrinter.Listen6/         Observe IPv6 mDNS; transmits nothing [built]
│   ├── SecretPrinter.Loop6/           Measure IPv6 multicast loopback    [built]
│   ├── SecretPrinter.Respond/         Advertise a fake printer (test)    [built]
│   └── SecretPrinter.SpecCheck/       README-to-code coverage matrix     [built]
│
├── tests/
│   ├── SecretPrinter.TestKit/         Shared harness, no NuGet deps      [built]
│   ├── SecretPrinter.Dns.Tests/       (see below)
│   ├── SecretPrinter.Mdns.Tests/                                         [built]
│   ├── SecretPrinter.Advertising.Tests/                                  [built]
│   ├── SecretPrinter.Responder.Tests/ Fake transport                     [built]
│   ├── SecretPrinter.Resolution.Tests/ Fake transport and clock          [built]
│   ├── SecretPrinter.Proxy.Tests/     In-memory streams                  [built]
│   ├── SecretPrinter.Configuration.Tests/ Fake adapters                  [built]
│   └── SecretPrinter.Service.Tests/   Includes assembly metadata scans   [built]
│
└── .github/workflows/
    ├── ci.yml                         Build, test, spec-check            [done]
    └── release.yml                    Build, sign, publish on a v tag    [done]
```

`[done]` and `[built]` mark what exists today. The rest is specified and not
yet written.

`SecretPrinter.Dns.Tests` carries no mark because it does not exist yet.
Until it does, `SecretPrinter.Dns` has no test project of its own; its types are used by the test kit and by the Advertising,
Responder, Resolution and Service suites, which is not the same as being tested
directly.

The tree does not list test counts or individual findings, because both change
with almost every commit and a list that goes stale is a false statement. The
current count is whatever `run-tests.ps1` reports; the findings are the files in
`docs/findings/`.

`publish-release.ps1` produces the folder a release will be made from: the
service and the probe, published for 64-bit Windows with the .NET runtime
beside them, into one folder outside the repository. No release exists yet.
On 2026-10-04 a folder it made was run on a machine with no .NET installed, and
an iPhone printed through it
([finding](docs/findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)).
That run is the evidence for REQ-DIST-011 in `docs/verification.md`. (Paragraph,
and the script's line in the tree, added 2026-10-03 by Claude, Claude Opus 5.5.
Its last two sentences replaced 2026-10-04, when the run was made; they had said
REQ-DIST-011 stayed uncovered until such a run was recorded.)

`.github/workflows/release.yml` turns a tag beginning with `v` into a release.
It builds, runs every test, runs `publish-release.ps1`, signs SecretPrinter's
own files in a job that waits for a person's approval, checks every signature,
and publishes the zip only if the specification check passed. The version lives
in one place, `<Version>` in `Directory.Build.props`, and the workflow refuses a
tag that does not match it or that has no entry in `CHANGELOG.md`. It has run
twice, both on 2026-10-04 and both for trial tags that are not releases. The
first run failed before its signing job. The second signed twelve files, and
the signatures were read on a downloaded copy
([finding](docs/findings/2026-10-04-the-release-workflow-signed-a-build.md)).
No release has been published, and the job that publishes one has never run.
(Paragraph, the `release.yml` line in the tree and the `[done]` beside
`CHANGELOG.md` added 2026-10-04 by Claude, Claude Opus 5.5, and corrected twice
the same day as the workflow ran: it first said the workflow had never run, and
then that nothing had been signed.)

`SecretPrinter.Listen6` and `SecretPrinter.Loop6` are measurement tools written
to answer one question each while IPv6 was being added; each one's header
states its question and the finding that records the answer. Neither is part
of the service.

A third, `SecretPrinter.Respond6`, was deleted on 2026-10-02. It advertised a
printer over IPv6 mDNS only, with an `A` record, an `NSEC` and no `AAAA`
record, standing in for the service before the service had an IPv6 socket. The
service now answers over IPv6 (REQ-ADV-018) and sends the `NSEC` (REQ-ADV-022)
itself. Two findings report runs made with the tool:
[2026-09-06](docs/findings/2026-09-06-ipv6-mdns-transport.md), the question it
was written for, and
[2026-09-29](docs/findings/2026-09-29-probing-on-hardware.md), where it was the
second responder that the service found a name conflict with. Its source is in
this repository's history: the last commit that holds
`tools/SecretPrinter.Respond6` is `a10087d`. Repeating either measurement needs
the tool from that commit, or another responder that does the same. (Paragraph
added 2026-10-02 by Claude, Claude Opus 5.5, in the commit that deleted the
tool; until then the paragraph above listed it as a third measurement tool.)

### Why the layering is what it is

`SecretPrinter.Dns` contains no socket code at all — it converts bytes to
objects and back. `SecretPrinter.Mdns` owns sockets but knows nothing about
printing. `SecretPrinter.Proxy` moves TCP bytes and knows nothing about mDNS.
Only `SecretPrinter.Service` knows the whole story.

The separation exists so that the security-relevant claims can be checked
locally. REQ-PXY-004 says no job data is written to disk; verifying that means
reading one project, not the whole solution - and a test reads that project's
compiled metadata and fails if it references any file-writing type at all. Likewise "could this ever publish
the printer's address?" is answered by reading `SecretPrinter.Advertising`,
which has no socket code and no way to learn the printer's address except from
what it is handed.

## 13. Authorship and AI involvement

This project was written by an AI. That is the point of it, and it is stated
here rather than discovered later.

**What that means concretely:**

- Source files carry a header naming the model that wrote them and the person
  who directed the work.
- Design decisions were made in conversation between a human and Claude. Where a
  decision was contested or uncertain, that is recorded in `docs/` rather than
  presented as settled.
- Claims in this document are backed by measurement where measurement was
  possible. `docs/findings/` records what was actually observed, on real
  hardware, with the tools in `tools/`. Anyone can re-run them. One tool has
  since been deleted, and the two findings made with it need it from the
  repository's history ([section 12](#12-repository-layout)). (That sentence
  added 2026-10-02 by Claude, Claude Opus 5.5.)
- Where something was not verified, this document says so. An unverified claim
  labelled as verified would be worse than no claim.

**What it does not mean:**

- It does not mean the code is correct. AI-written code has bugs like any other.
- It does not mean a human is absent. Every merge is reviewed by a person, who
  is responsible for what ships.
- It does not mean you should trust it because of who wrote it. Trust it, if at
  all, because the specification is complete, the code matches it, and you
  checked.

## 14. Open questions

Recorded here rather than resolved silently.

| # | Question | Status |
| --- | --- | --- |
| 1 | **Does iOS require IPPS?** | Resolved: no, not for discovery. An iPhone listed a printer advertised with no IPPS service and no TLS. See [findings](docs/findings/2026-09-02-ios-accepts-advertisement.md). For printing the answer is different and was measured on 2026-09-15: the ET-3760 answers `Get-Printer-Attributes` in the clear but refuses `Validate-Job` with `426 Upgrade Required`, so a job does not complete over plain IPP. TLS is not deferred; it is the remaining blocker to printing. See [findings](docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md) and open question 8. |
| 2 | **Can the service send mDNS responses that clients accept while another responder holds port 5353?** | Resolved: yes. Responses sent from a socket sharing 5353 with `Dnscache` were accepted by iOS, which listed the advertised printer. See [findings](docs/findings/2026-09-02-ios-accepts-advertisement.md). |
| 3 | **What should the project be called?** | Resolved: SecretPrinter. The prefix is a deliberate joke shared across a family of open projects and means the opposite of what it says. Explained under [About the name](#about-the-name). |
| 4 | **Is `mopria-certified` safe to relay?** The bytes would be identical to the printer's, and the printer is certified — but the proxy is not the certified device. | Currently forbidden by REQ-ADV-007. Revisit with evidence. |
| 5 | **What privileges does the service actually need?** | Resolved: none beyond standard user. Measured on 2026-09-04 by running unelevated and confirming every bind, join and relay succeeded. Running as a Windows service under `NT AUTHORITY\LocalService` was measured on 2026-09-21 on FIOS-STB-01: under that account the service bound its sockets, joined both multicast groups, resolved the printer, answered discovery, opened TLS to the printer and relayed pages that printed. That is one machine; the dev box has no service registered. The folder grant it runs with also lets it modify its own configuration file, which the service never does; that is recorded, not decided. (Until 2026-09-22 this entry said running under `LocalService` remained unmeasured, which had stopped being true on 2026-09-21.) See [findings](docs/findings/2026-09-04-service-runs-unelevated.md) and [findings](docs/findings/2026-09-18-running-as-localservice.md). |
| 6 | **How should the service register with Windows?** | Resolved: `System.ServiceProcess.ServiceController` is referenced for `ServiceBase`, documented under [Dependencies](#dependencies) as REQ-SEC-009 requires. Run with `--service` to register with the control manager, or without it as a console application. |
| 7 | **Will iOS accept an `A` record delivered over IPv6 mDNS transport, and then connect over IPv4?** | Resolved: yes. Over `ff02::fb`, an iPhone was sent an mDNS response carrying an `A` record, and seventy milliseconds later it sent IPv4 SYNs to port 631. The responder was the experiment tool `SecretPrinter.Respond6`, which sends an `NSEC` denying `AAAA` with some of its answers and not with others; whether that response carried one is not recorded, and whether an `NSEC` played any part was not measured. (Note, 2026-10-02, by Claude, Claude Opus 5.5: the tool was deleted on 2026-10-02, and what is said of it here describes its last version; [section 12](#12-repository-layout) says where its source is.) IPv6 mDNS transport is specified as REQ-ADV-018 to REQ-ADV-020. Resolution, which talks to the printer, is unaffected by IPv6. The relay is not: REQ-ADV-021 publishes an `AAAA` record for each preferred link-local address of the client interface, and the relay listens on those addresses; open question 11 records which address an iPhone then used. See [findings](docs/findings/2026-09-06-ipv6-mdns-transport.md). (Corrected 2026-10-01 by Claude, Claude Opus 5.5: until then this entry said that the iPhone accepted the `NSEC`, which was not measured; that IPv6 was therefore mostly a transport addition to `SecretPrinter.Mdns`; and that resolution and relay were unaffected because REQ-ADV-021 forbade publishing an `AAAA` record. The last two stopped being true at commit `2c93864`, on 2026-10-01, when REQ-ADV-021 was rewritten to publish link-local `AAAA` records and the relay began listening on them. This entry was not updated in that commit.) |
| 8 | **Should the proxy originate TLS toward the printer?** Open question 1 establishes that this printer will not accept a job without it, so printing depends on the answer. | Resolved 2026-09-15: yes, and by implicit TLS on port 631, where the printer advertises `_ipps._tcp` and accepts a handshake from the first byte. The client side stays plaintext, which is a considered choice for a home LAN and is disclosed as the fourth item under [Read this before installing](#read-this-before-installing). The certificate is self-signed — subject and issuer both `CN=EPSON000000`, the printer's host name with its low three bytes redacted as elsewhere in this repository — so it is pinned by fingerprint in configuration and checked on every connection, with no permissive mode. Trust-on-first-use was rejected because remembering a certificate means writing state to disk, and a test reads `SecretPrinter.Proxy`'s compiled metadata and fails if it references any file-writing type at all. (An earlier wording here said "several tests" enforced that across "this project". There is one test, and it covers the proxy assembly. `SecretPrinter.Service` does write one file, the operator's log — see `REQ-OBS-009`.) Specified as REQ-PXY-010 to REQ-PXY-012, REQ-CFG-007, REQ-SEC-013 to REQ-SEC-015 and REQ-OBS-008. See [findings](docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md). |
| 9 | **How does someone using a release configure the service without developer tools?** When this was asked, on 2026-09-16, configuring meant running the probe with `dotnet run` and measuring the certificate with a PowerShell command. | Decided 2026-10-05 by Edwin West: by hand, following [`docs/operating.md`](docs/operating.md), and release 0.1.0 has no configuration tool. The steps are: print the example configuration with `SecretPrinter.Service.exe --print-example-config`; name the adapters; copy the printer's two instance names from what `SecretPrinter.Probe.exe` prints; generate a UUID; and measure the printer's certificate fingerprint with one PowerShell command. Since 2026-10-04 the probe is a program in the release folder (`REQ-DIST-011`), so no step uses the `dotnet` command or needs the .NET SDK. The steps need the release folder and PowerShell, which Windows includes. What has been run: the example configuration printed, and the probe run, from a release folder on a machine with no .NET, on 2026-10-04 ([finding](docs/findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)); and the certificate command, on Windows 10, on 2026-09-15 ([finding](docs/findings/2026-09-15-printer-requires-tls-for-job-operations.md)). The steps were followed from the first to the last on 2026-10-06, on a folder the release workflow had signed, and an iPhone printed through the configuration they produced ([finding](docs/findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)). Its limits, all in that finding: Edwin West ran commands that Claude took from `docs/operating.md`, and did not work from the document himself; it was one machine, one printer and one page; and only three lines of the example needed changing, because the example was written from this printer and the test machine's printer-side adapter has the name the example uses. The walk found three faults in `docs/operating.md`, corrected the same day: three commands that did not run as printed, a step that did not say where its values come from, and an instance name the probe prints twice. (Updated 2026-10-06 by Claude, Claude Opus 5.5: until then this said "What has not been done: following the steps from the first to the last on a release folder to make a configuration. In the runs of 2026-10-04 the configuration was a file copied from another machine.") **Decided earlier, and now set aside.** On 2026-09-16: a configuration tool, written in C# and reusing `SecretPrinter.Dns`, that would find the interfaces, the printer's instance names and its certificate and write the configuration file, with the operator confirming each before anything was written. On 2026-10-03: that the first release would wait for that tool. No such tool was written, and none is planned in this repository. Edwin West intends one for a separate project he calls SecretPrinterX, for a packaged form of SecretPrinter. This repository holds none of it. Two design points were open for the tool and were never answered: how it would read the certificate without a validation callback that accepts every certificate, which the TLS design rules out in shipped code; and whether it would count as shipped for the checks in `SecurityClaimsTests`, since it would open sockets and write a file. **The second point has not gone away with the tool.** The probe is in the release folder. It is not among the assemblies those checks read, and a comment in `tests/SecretPrinter.Service.Tests/SecurityClaimsTests.cs` says diagnostic tools are left out because "they are not installed". For the probe that stopped being true on 2026-10-04. Corrected 2026-10-06: the probe has a requirement of its own, `REQ-SEC-016`, and tests that read its assembly and the two it is built on. It is held to the checks it can meet (it starts no other program, touches no registry key, makes no HTTP request and writes no file) and not to the one it cannot, because it opens a UDP socket, which is its job. The comment is rewritten, and so are the markers that said "shipped" where they meant the service ([finding](docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md)). (Updated 2026-10-06 by Claude, Claude Opus 5.5: until then this ended "It is not yet corrected, and whether the probe should be held to those checks is not yet decided.") (Updated 2026-10-05 by Claude, Claude Opus 5.5: until then the question's second sentence read "Configuring today means running the probe with `dotnet run` and measuring the certificate with a PowerShell command, as `docs/operating.md` describes.", and this entry read "Decided 2026-09-16: a configuration tool, written in C# and reusing `SecretPrinter.Dns` so the repository keeps one DNS parser, will find the interfaces, the printer's instance names and its certificate, and write the configuration file, with the operator confirming each before anything is written. It will be built after printing over TLS works, so that it writes a configuration proven to print. Still open: how it reads the certificate without a validation callback that accepts every certificate, which the TLS design rules out in shipped code; and whether it counts as shipped for the checks in `SecurityClaimsTests`, since it opens sockets and writes a file.") |
| 10 | **Which Windows versions and processor architectures do releases support?** A self-contained release is published for a specific runtime and architecture, so REQ-DIST-011 cannot be met without an answer. | Decided 2026-09-30 and 2026-10-03; no release exists yet. Releases are built for 64-bit Windows on x64 processors only (`win-x64`), with no arm64 or x86 build; that is what `publish-release.ps1` builds. SecretPrinter is developed and run day to day on Windows 10 22H2 (build 19045), with .NET 10: on the development machine, and on FIOS-STB-01, where it runs as a service (for one such run see [the finding](docs/findings/2026-10-01-an-iphone-printed-over-ipv6-link-local.md)). On Windows 11 it has been run five times, all on one machine, for under half an hour in all: twice on 2026-10-04, on Windows 11 Home, build 26200, from a release folder, and an iPhone printed a page each time ([the finding](docs/findings/2026-10-04-a-release-folder-printed-on-a-machine-with-no-dotnet.md)); once on 2026-10-06, from a folder the release workflow had signed, when an iPhone printed a page again ([the finding](docs/findings/2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)); and twice more later that day, with that signed folder installed as a service, for about thirteen and seven minutes, the second time started by Windows at boot, and an iPhone printed a page in the first ([the finding](docs/findings/2026-10-06-a-signed-service-was-updated-started-at-boot-and-uninstalled.md)). It has not been run on any other version of Windows. Microsoft's [list of the operating systems .NET 10 supports](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), as updated 2026-09-28 and read 2026-10-03, includes Windows 11 and does not include Windows 10 22H2. So the version SecretPrinter has been measured on at length is one that list leaves out, and on Windows 11 it has been measured for under half an hour, on one machine. (Updated 2026-10-03 by Claude, Claude Opus 5.5: until then this entry read "Open. No version or architecture is stated anywhere in this repository." Updated again 2026-10-04, when the first runs on Windows 11 were made: it had said SecretPrinter had been run only on Windows 10, and not as a proxy on Windows 11. Updated 2026-10-06, when a third run was made: it had said "run twice, on 2026-10-04, for about two and three minutes". Updated again 2026-10-06, when two more runs were made: it had said "run three times, for about two or three minutes each", and had ended "measured for a few minutes on one machine".) |
| 11 | **Offered both an `A` record and a link-local `AAAA` record, which address does iOS connect to, and does it connect to a link-local address it learned over mDNS at all?** REQ-ADV-021 publishes both. | Resolved for one iPhone, 2026-10-01: it connected to the link-local address for every connection that carried data, and printed two pages over it; its one IPv4 attempt it reset itself as an IPv6 connection opened. Other devices and iOS versions were not measured. See [the finding](docs/findings/2026-10-01-an-iphone-printed-over-ipv6-link-local.md). |

## 15. License

MIT. See [LICENSE](LICENSE).

The MIT license disclaims warranty. That disclaimer is legal boilerplate and
does not lessen the obligations this document places on the code: the
specification above describes what the software does, and if the software
departs from it, that is a defect to be fixed or a specification to be
corrected — not an outcome excused by the license.

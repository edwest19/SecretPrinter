# The service saw 23 questions and not one of them was IPv4

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-14. Reviewed by a human before merge.*

*Corrected 2026-09-23 by Claude (Anthropic model, Claude Opus 5.5), at the
direction of Edwin West. The title first read "iOS asked 23 questions and not
one of them was IPv4". The status said "REQ-ADV-018 is validated in the field
by this run", when only its IPv6 half was. The passage now headed "What arrived
was IPv6 alone" first opened "The original diagnosis was understated", endorsing
the diagnosis that iOS queries mDNS exclusively over IPv6, and the last section
ended by calling IPv6 "the only transport in play". This run measured what
reached the service, not what iOS sent. On 2026-09-23 another machine on the
client network saw IPv4 queries for the advertised names from `192.168.1.152`
and from an iPad, while the service counted none that asked for them
([2026-09-23](2026-09-23-the-goodbye-and-ipv4-on-the-wire.md)). The title, the
status, that passage and that sentence now say only what was measured. The file
name keeps the original claim, because other documents link to it. Reviewed by
a human before merge.*

**Status: measured on `FIOS-STB-01`, one run, 2026-09-14 16:57:14Z to 16:58:41Z.
The IPv6 half of REQ-ADV-018 is validated in the field by this run. Printing
still does not work, and the cause is narrowed but not found.**

## What was measured

The service ran in the foreground against the real ET-3760, with one iPhone
(`192.168.1.152`, the only client on the network during the run) asked to print
once. The shutdown summary:

```
Served 9 quer(ies) of 23 seen; 14 were for other services and were ignored.
By transport: IPv4 answered 0 of 0 seen; IPv6 answered 9 of 23 seen.
```

Machine facts for the run, from the startup log: `Ethernet` 192.168.1.161,
IPv4 and IPv6 index 15, joined `ff02::fb`; `Wi-Fi` 192.168.12.186, IPv4 index 9;
printer resolved to 192.168.12.180:631, host `EPSON3EA18A.local`, TTL 120s.

## What that settles

**REQ-ADV-018 is no longer merely marked and tested.** Nine real answers went
out over IPv6 to a real client which then discovered the proxy and connected.
The handoff of this morning said the requirement was "marked and tested, and is
NOT validated in the field", and that the responder's counters could not tell an
IPv4 discovery from an IPv6 one. REQ-OBS-007 exists to remove that ambiguity and
it did so on its first run.

**What arrived was IPv6 alone.** The earlier diagnosis was recorded as "iOS
queries mDNS exclusively over IPv6". What this run measured is what reached the
service, not what iOS sent: in 87 seconds, on a dual-stack interface with both
sockets bound and both groups joined, **zero IPv4 mDNS queries arrived at all**.
Before the IPv6 socket existed, this service was not answering a reduced share
of queries. It was answering none.

That also revises an open item recorded on 2026-09-13: *the Verizon router sends
a high volume of IPv4 mDNS queries, which may interact with the relay*. No IPv4
query reached the client socket during this window. The item is not refuted -
one 87-second sample says nothing about the router's behaviour at other times -
but it is not reproducible as written, and nothing should be built on it.

## What it does not settle

The 9 of 23 ratio is not a defect. Fourteen queries were for services this proxy
does not advertise and were ignored, which is REQ-ADV-012 working.

Nothing here says an IPv6 answer is *correct*, only that it was sent over IPv6
and that a client acted on it. The records themselves are covered by other
requirements and other tests.

## The printer is what resets the connections

26 connections were accepted. 4 completed, 22 failed, and every one of the 22
failed with the same reason:

```
Relay ended early while reading from the printer: Unable to read data from the
transport connection: An existing connection was forcibly closed by the remote
host.
```

Not one said "client". The handoff listed two candidates for these resets - the
Epson limiting concurrent connections, or iOS abandoning connections itself -
and said the log collapsed them. REQ-OBS-006 was written to separate them, and
on its first run against real traffic it did: **iOS abandoning connections is
out.** The resets come from the printer's side of the relay.

### One caution about what that message can and cannot prove

The relay runs both directions at once, so a read from the printer is
outstanding essentially always. When a connection is reset, that pending read is
the operation most likely to notice first - **whether or not a write toward the
printer was also in flight.** The complete absence of "while writing to the
printer" in this log therefore does **not** show that little data was being sent
toward the printer. That conclusion is not available from this evidence and must
not be drawn from it.

What the message does establish is the peer: the failure arrived on the socket
connected to 192.168.12.180, not on the socket connected to the phone.

Separately, "the printer reset it" here means an RST arrived from the printer's
address. Whether the Epson's IPP server chose to close, or something between the
two reset the flow, is not distinguishable from this log.

## The concurrency theory does not fit the timestamps

The leading explanation was that the Epson limits concurrent IPP connections and
resets the excess. The run argues against it:

- The **first** reset happened when exactly **two** connections had ever been
  opened. 51621 was accepted at 16:57:32 and 51622 at 16:57:33; 51621 was reset
  at 16:57:35. For a concurrency cap to explain that, the cap would have to be 1.
- Later, **four** connections coexisted for more than thirty seconds - 51622,
  51626, 51632 and 51634 were all open from 16:57:40 and completed at 16:58:10
  or later. A cap of 1 is refuted by this.

No single fixed concurrency limit explains both observations.

**A hypothesis that does fit, recorded as a hypothesis:** the printer reaps
connections that carry no data. Every reset landed between 0 and 3 seconds after
the connection was accepted, most within the same second. The four that survived
are the four that moved bytes. iOS opening connections speculatively and leaving
some unused is ordinary behaviour, and a printer closing idle sockets promptly
would produce exactly this shape.

**This is not testable with the current logging**, for the reason in the next
section. It should not be repeated as a conclusion.

## What this run showed we cannot see, and why that is a gap

`IRelayObserver.RelayFailed` takes a client endpoint and a reason string. It
takes no byte counts and no duration, so a failed relay is reported without any
measure of what it carried - even though `RelayOneAsync` has both to hand and
returns them in `RelayOutcome`.

REQ-PXY-009 says log entries about a relayed connection record endpoints, byte
counts, and timing. A failed relay is a relayed connection. **The requirement is
marked covered and is not met for the failure path.** The test that covers it
asserts counts on the completed path only.

This is why the idle-reap hypothesis cannot be checked: distinguishing "opened
and carried nothing" from "carried a job the printer then rejected" needs
exactly the numbers that are dropped. Closing the gap is the next commit.

## Printing, as it stands

No document was printed. The largest completed exchange was 15,835 bytes sent
and 6,179 received - larger than the 8,428 of the previous session, still two
orders of magnitude short of a print job. The four completed connections ran 30
to 56 seconds each and ended cleanly.

The picture is unchanged in substance: **iOS discovers the proxy, connects,
negotiates, and does not send the document.** What has changed is that two
distractions are gone. The resets are the printer's, not the client's, and they
are almost certainly not the blocker; and the mDNS transport is working, and
every query that reached it came over IPv6.

# The printer-side multicast membership is lost while the service runs

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-19. Reviewed by a human before merge.*

**Status: measured on hardware (FIOS-STB-01). The false message is fixed as of
this commit; the membership itself is NOT yet repaired. The resolver's
membership of 224.0.0.251 on the printer-side interface disappeared while the
service was running and had logged that it joined. The service did not notice,
kept accepting jobs, and blamed the printer for the failures.**

*(Status updated 2026-09-25 by Claude, Claude Opus 5.5: measured again on
2026-09-25, on the Broadcom adapter, with a control, and repaired in code under
`REQ-RES-009`. The printer-side socket is now reopened before every question put
to a printer held unreachable. The repair has not yet run on hardware. See
[`2026-09-25-a-reconnect-did-not-restore-the-membership.md`](2026-09-25-a-reconnect-did-not-restore-the-membership.md).)*
*(Added 2026-09-26: installed on FIOS-STB-01 and run once, through a deliberate
outage in which the join was not lost. Recovery from a loss is still to be seen.
Recorded under "The repair on hardware" in the same finding.)*

This is the mechanism behind the false message recorded in
[2026-09-18-the-printer-side-interface-goes-away.md](2026-09-18-the-printer-side-interface-goes-away.md).
That finding established that one of the two failure messages is false. This one
establishes *why* the lookup fails while the printer is healthy and reachable.

## The measurement

FIOS-STB-01. Printer-side interface is Wi-Fi 2, index 49, 192.168.12.136 — a
Linksys Compact Wireless-G USB adapter fitted on 2026-09-19, replacing the
internal Broadcom. Client-side interface is Ethernet, index 15, 192.168.1.161.
The printer sat at 192.168.12.180 throughout.

The service started at 05:37:59Z and ran continuously until it was restarted by
hand at 17:54:04Z. The log records no stop in between, so everything below
happened to a running service.

### While the service was running, before any restart

`netsh interface ipv4 show joins`, 224.0.0.251:

| Interface | References |
|---|---|
| 15 (Ethernet) | 2 |
| 49 (Wi-Fi 2) | **1** |

The service logs a join on each at startup — line 713 for Wi-Fi 2 at 05:37:59Z,
and the matching responder join on Ethernet. Ethernet carried both joins.
Wi-Fi 2 carried one.

At that same time, from that same machine and that same interface:

- `ping 192.168.12.180` — 4 replies of 4, 87–210 ms.
- `tools/SecretPrinter.Probe --interface 192.168.12.136` — two replies from
  192.168.12.180, 16 records, all five queried service types answered, full TXT
  for `_ipp._tcp.local`.

So at that moment the outbound multicast path off the adapter, the printer's
mDNS responder, and the unicast return path were all healthy. The probe queries
from an ephemeral port with the unicast-response bit set, so its answers come
back unicast and never touch the group.

### Establishing the baseline

The reference counts above only mean something if we know what one join looks
like. Measured by stopping and starting the service, 17:54Z–17:56Z:

| 224.0.0.251 | Service stopped | Service started |
|---|---|---|
| Interface 15 (Ethernet) | 1 | 2 |
| Interface 49 (Wi-Fi 2) | 1 | 2 |

SecretPrinter contributes exactly one join per interface, and one other joiner on
this machine holds the other. That joiner was not identified and is not assumed
here; all that matters is that it is constant across a stop and a start.

Four values were predicted before the commands were run — 1, 1, 2, 2 — and all
four matched.

Later the same evening, with the service stopped **and** the Wi-Fi adapter down,
interface 49 still listed all four groups, 224.0.0.251 among them, every one at
a reference count of **0**. So the presence of a group in this output carries no
information at all; only the count does. That is measured, and it decides the
shape of the repair — see item 3 below.

### What that proves

Ethernet read 2 and Wi-Fi 2 read 1 while the service was running with both joins
logged. **The printer-side membership was lost between 05:37:59Z and the moment
the count was read, without the service being stopped and without anything being
logged.** Restarting the service restored it to 2.

### Recovery

With the membership restored, printing worked again immediately. Six jobs
completed between 18:03:32Z and 18:05:05Z with no failures among them, the
largest sending 38,407 bytes and receiving 5,523, and a page came out of the
printer. Nothing was changed on the machine or the network between the failures
and the recovery except stopping and starting the service.

## Why losing it produces the false message

`PrinterResolver.ReceiveAnswerAsync` builds its lookup query at
`src/SecretPrinter.Resolution/PrinterResolver.cs` lines 326–329:

```csharp
byte[] query = new DnsQueryBuilder(0)
    .AddQuestion(instance, DnsRecordType.Srv, requestUnicastResponse: false)
    .AddQuestion(instance, DnsRecordType.Txt, requestUnicastResponse: false)
    .Build();
```

The address query at lines 418–420 does the same. The resolver socket is bound
to `0.0.0.0:5353`, as the startup log states. A query that arrives from port
5353 with the unicast-response bit clear is answered by multicast to
224.0.0.251:5353 (RFC 6762). A host that has left the group never receives that
answer.

Sending does not fail, because the local address is still valid — this is not
the `WSAEADDRNOTAVAIL` case. The query goes out, the printer answers into a
group nobody here is listening to, the five-second deadline elapses, and the
service reports:

> The printer may be asleep, off, or on a different network. No address is
> assumed.

The printer was awake, on, on the right network, and answering. Seventeen jobs
were failed with that message between 14:51:18Z and 14:52:44Z, queued behind one
another by the `SemaphoreSlim(1, 1)` gate at line 101 until individual jobs had
waited nearly 25 seconds.

## What is not established

- **Why the membership was lost.** Not measured. The adapter did leave the
  network at least once earlier the same day — 22 jobs failed at 13:44Z with
  `The requested address is not valid in its context`, which is the
  address-removed mode — so a reassociation is a candidate. It is not proven,
  and the WLAN event log was not pulled for this finding.
- **When it was lost.** Only that it happened while the service ran, at some
  point before the count was read.
- **That the probe succeeded at the same instant a job was failing.** It did
  not. The last failing job is 14:52:44Z and the probe ran some hours later. The
  probe establishes that the printer and the unicast path were healthy on that
  interface; it does not by itself timestamp the fault. Pairing a probe run
  against a live failure is still worth capturing.
- **Whether the new adapter changed anything.** Both failure modes recurred on
  it the same day. Replacing the Broadcom did not remove the symptom.

## What this owes the product

1. **Stop making the false claim.** *(Done in this commit: the resolver now
   examines its own interface at timeout and states only what it found. See
   `src/SecretPrinter.Resolution/PrinterInterfaceReport.cs`.)* When a lookup times out, the service must
   examine the local side before asserting anything about the printer: the
   interface's `OperationalStatus`, the address and its
   `DuplicateAddressDetectionState`, and — new with this finding — whether the
   group is still joined. The message must distinguish "we could not ask" from
   "the printer did not answer".
2. **Re-establish the membership.** Noticing is not enough. The requirement
   proposed in the 2026-09-18 finding must cover the case where the interface is
   up and the address is valid and only the group is gone. Rebuilding the
   resolver socket on an interface change is the likely shape; this finding does
   not design it.
   *(Added 2026-09-25 by Claude, Claude Opus 5.5: built as `REQ-RES-009`, with
   the trigger Edwin West chose. The socket is rebuilt before every question put
   to a printer held unreachable, rather than on an interface change. A group
   lost while the interface stays up and the address valid lets the record
   expire, withdraws the printer, and is repaired by the next question, seconds
   later.)*
3. **Detecting the loss from managed code looks unavailable.** `netsh` reads
   these membership counts, so Windows exposes them. .NET surfaces the groups an
   interface has joined, through `IPInterfaceProperties.MulticastAddresses`, but
   that is a list of addresses carrying no reference count — and the measurement
   above shows a down adapter still listing the group at zero. With another process
   on this machine holding its own membership of 224.0.0.251 — which is what the
   counts above show — the group would still be listed while the service is
   deaf, and a check reading it would pass. This is read from the shape of the
   API, not measured, and it is recorded here so the next step does not begin by
   writing a check that cannot work. The route that does not depend on it is to
   rebuild the socket when the interface changes, rather than to query for a loss
   that cannot be seen.

## A fix that is rejected

Setting the unicast-response bit on resolver queries would make the printer
answer directly to 192.168.12.136:5353, and lookups would then succeed whether
or not the group membership survived. That would make the symptom disappear
without repairing the defect: the service would still be deaf to multicast on an
interface it believes it is listening to, and would still be wrong about it. It
is not adopted as a fix here. Whether the resolver should set that bit for other
reasons is a separate design question, to be decided on its own merits and in
its own commit.

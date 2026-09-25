# The printer-side interface goes away and the service does not notice

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-18. Reviewed by a human before merge.*

**Status: observed on hardware (FIOS-STB-01), measured, NOT fixed. Three
separate adapter resets in one evening, two of them confirmed as Windows' own
doing. One of the two resulting failure messages is false.**

## What happened

The printer-side interface on FIOS-STB-01 is Wi-Fi, on the printer network. It
left that network three times on the evening of 2026-09-18. The service binds
its printer-side sockets once, at startup, and never looks at that interface
again, so on each occasion it carried on advertising itself and accepting print
jobs it had no way to fulfil.

Timeline, in UTC, from the service log, the System event log and the
`Microsoft-Windows-WLAN-AutoConfig/Operational` log:

| Time | Event |
|---|---|
| 19:25:54 | Wi-Fi associates with the printer network (WLAN event 8001). |
| 19:38:07 | Service starts as `LocalService`, binds Wi-Fi 192.168.12.186, index 9. |
| ~19:41 | A page prints. |
| 19:53:52 | WLAN event 8003, *"Reason: The network is disconnected by the driver."* Link had lasted 28 minutes. |
| 20:18:16 – 20:20:01 | Twenty-one jobs accepted and failed. Every one blamed the printer. |
| 21:45 | Adapter state `disconnected`; 192.168.12.186 still held, `AddressState: Deprecated`. |
| 21:46:03 | Manual `netsh wlan connect`. Associates, WPA2 succeeds, address returns to `Preferred`. |
| 21:46:23 | WLAN event **4003** — *"detected limited connectivity, attempting automatic recovery"* (Recovery Type 4, Error Code 0x0, Trigger Reason 2) — and event 8003 in the same second. Link had lasted **20 seconds**. |
| 21:46:40, 21:47:21, 22:45:31 | Service refuses to start: `Interface 'Wi-Fi' holds 169.254.111.167 but is not up`. Correct. |
| 23:02:26 | Reconnected by hand; service starts; advertisement built from a live mDNS query to the printer at 192.168.12.180. |
| 23:21:50 | WLAN event **4003** again. |
| 23:31:20 – 23:31:21 | Five jobs accepted and failed, in 0s each. |
| ~23:40 | Reconnected by hand, service restarted, a page prints. |

## The root cause is not ours; the response to it is

Two of the three disconnects are attributable, from the event log, to WLAN
AutoConfig deciding the printer network had limited connectivity and resetting
the adapter (event 4003). The 19:53:52 drop was recorded only as 8003; whether a
4003 accompanied it was not established.

This matters to the project beyond one machine. **Every SecretPrinter
installation is dual-homed by definition**, and the printer-side network is
exactly the kind of network — printers, no route worth probing — that Windows is
inclined to judge as limited. A host that resets that adapter on its own
schedule is a normal operating environment for this software, not an exotic one.

Nothing about that is SecretPrinter's fault. What follows is.

## Two failure modes, one root cause, and one of them lies

The state the adapter is left in decides which message the operator gets, and
the two are not equally honest.

**20:18Z — address retained, marked `Deprecated`.** The link was gone but
Windows kept the DHCP address (`PreferredLifetime` and `ValidLifetime` both
still showed 6 days 22 hours remaining; the lease had not expired, the
association had). A socket bound to a deprecated address does not throw on send.
The multicast query left for nowhere, no answer came back, and the resolver's 5s
deadline elapsed:

```
Job: failed for 192.168.1.156:53683: Could not locate the printer: 'EPSON ET-3760
Series._ipps._tcp.local' did not answer within 5s on Wi-Fi (192.168.12.186, IPv4
index 9). The printer may be asleep, off, or on a different network. No address
is assumed. (0 bytes sent, 0 received, 5s)
```

The printer was switched on, on its network, and perfectly well. **That sentence
is false, and the service repeated it twenty-one times.**

**23:31Z — address removed.** The socket threw `WSAEADDRNOTAVAIL` on the spot:

```
Job: failed for 192.168.1.156:50377: Could not locate the printer: The requested
address is not valid in its context. (0 bytes sent, 0 received, 0s)
```

That one is true, immediate, and names the local fault. Same root cause, same
code path, opposite honesty — decided entirely by whether Windows happened to
retain the address.

For a project whose premise is that its claims can be checked, a diagnostic that
confidently accuses a third party of a fault on the local machine is the worst
class of defect in the log. It is worse than silence.

## The failing lookups also queue

Independent of the message, the 20:18 run shows the resolver's gate turning a
dead printer side into a backlog. `PrinterResolver` serialises lookups through a
`SemaphoreSlim(1, 1)`, so each job waits behind every earlier failing lookup,
each of which costs the full 5s deadline. The measured durations climb and then
plateau:

```
5s → 6.86s → 11.88s → 16.88s → 19.86s → 19.92s → 24.93s → 24.94s …
```

The pairing shows the depth directly: the connection from port 53697 was
accepted at 20:19:02 and failed at 20:19:26, 24.86s later, having spent about
20 of those seconds waiting for other lookups that had already failed. The queue
stabilised around five deep because that is where the client's own retry rate
and the 5s deadline balanced.

Two consequences. A job arriving while the printer side is down fails in 25
seconds rather than 5. And if the printer side were to come back mid-queue,
several already-abandoned connections would still be ground through ahead of a
live one.

## What the service checks, and when

`MdnsInterface` validates an interface at startup by one test
(`src/SecretPrinter.Mdns/MdnsInterface.cs`, lines 161 and 290):

```csharp
adapter.OperationalStatus == OperationalStatus.Up
```

That is the check that produced the three correct refusals at 21:46 and 22:45,
and it works. But:

- It does not inspect the address's `DuplicateAddressDetectionState`. A
  `Deprecated` or `Tentative` address on an adapter that reports `Up` would pass
  validation today. The 20:18 failure mode is precisely a deprecated address.
- It runs **only at startup**. Nothing re-examines the interface afterwards.

Meanwhile `REQ-LIF-005` keeps the service alive through transient network
errors. That requirement is satisfied to the letter here and defeated in its
purpose: a service that answers discovery, accepts jobs, and cannot print is
worse for the operator than one that stops and says why. It is also worse than
one that never advertised at all, because the client sees a printer, chooses it,
and waits.

## What is owed

Not written here, because this finding records what was measured and one thing
is done at a time. In the order I would take them:

1. **A requirement for the printer-side interface.** The service must notice
   that the interface it bound has become unusable — gone, down, or holding a
   deprecated address — and must then either rebind to the current address or
   stop advertising and stop accepting jobs. Silently accepting work it cannot
   do is the behaviour to remove. This needs its own `REQ-` entry and its own
   commit.
2. **The false message.** Until the interface is watched, the resolver timeout
   must not assert anything about the printer when the local interface cannot be
   shown to be usable. Checking the interface at the moment of failure and
   reporting that instead is a smaller change than item 1 and removes the lie.
3. **The lookup gate.** Concurrent jobs should share one in-flight lookup rather
   than queue behind repeated failures of it.
   *(Added 2026-09-25 by Claude, Claude Opus 5.5: done. Lookups for the same
   instance now share one query whether it is answered or not. See
   [`2026-09-25-two-clauses-of-req-res-008-were-never-built.md`](2026-09-25-two-clauses-of-req-res-008-were-never-built.md),
   which also records that `REQ-RES-008` had been marked met without this.)*
4. **`docs/operating.md` must name this hazard**, with WLAN event ID 4003 and
   8003 and the command to check for them, so that an operator whose printer
   "stops working overnight" has somewhere to look. This is not hypothetical for
   users; it is the normal shape of a dual-homed Windows host.

## Separately found while investigating, each owing its own finding

- **A refused configuration is reported to the SCM as error 1053, "did not
  respond to the start or control request in a timely fashion."** In
  `src/SecretPrinter.Service/Program.cs` the configuration refusal returns 3 at
  line 157; `ServiceBase.Run` is at line 208. Under `--service` the process
  therefore exits before it has ever connected to the service control
  dispatcher, so the SCM waits its full 30 seconds and reports a hang. The
  service in fact refused instantly and wrote the reason to its log. Exit code 3
  never reaches the SCM. Observed three times this evening.
  *(Added 2026-09-22 by Claude, Claude Opus 5.5: recorded in
  [`2026-09-22-a-refused-start-is-reported-as-a-timeout.md`](2026-09-22-a-refused-start-is-reported-as-a-timeout.md).
  The event timestamps there carry the same second as each refusal, so "waits
  its full 30 seconds" above is not shown by the records.)*
- **`MdnsResponder.ServeAsync` calls `HandleAsync` outside its `SocketException`
  guard**, and `HandleAsync` sends. A send that throws ends the responder loop.
  `ServiceHost` awaits `Task.WhenAll(running)`, and the relay tasks in that list
  do not complete until shutdown, so a faulted responder is never observed: the
  service would stay up, keep listening on 631, and answer no discovery query
  again, with nothing in the log. Reasoned from the code, **not** observed — the
  responder was healthy throughout this evening (`Served 42 quer(ies) of 682
  seen; IPv4 answered 1 of 47, IPv6 41 of 635`).

## Mistakes made while diagnosing this

Recorded because this project records them.

- **Claimed the address was deprecated because its preferred lifetime had
  expired.** The measurement showed 6 days 22 hours remaining on both lifetimes.
  The address was deprecated because the media was disconnected. Asserted a
  mechanism before measuring it.
- **Claimed WLAN AutoConfig made no reconnection attempt for two hours.** That
  came from a query using `-MaxEvents 60` and *then* filtering by time, so it had
  already discarded everything outside the 60 newest System events. The absence
  was an artefact of the query. This is the same error as the
  `Get-NetConnectionProfile` mistake recorded on 2026-09-17: inferring from a
  command that answers a different question.
- **Wrote `Get-NetTCPEndpoint`**, which does not exist. The cmdlet is
  `Get-NetTCPConnection`.
- **Wrote up "updated the driver and forced 802.11g" as something that had
  happened**, when what was said was that it had been *attempted*. The driver was
  measured afterwards and was unchanged (Microsoft 6.30.223.256, 2013-06-02,
  `bcmwl63a.sys`), and the radio was still negotiating 802.11n at 144 Mbps. No
  variable was actually changed, which means every observation above is against
  an unmodified system — useful, but only because it was checked.

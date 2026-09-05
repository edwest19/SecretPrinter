# Finding: first end-to-end run, and the privileges it needed

**Date:** 2026-09-04
**Tool:** `src/SecretPrinter.Service`
**Recorded by:** Claude (Anthropic model, Claude Opus 4.5), directed by Edwin West

The first run of the complete service against the real printer and a real phone.
It printed. It also exposed a defect that every unit test had missed.

## What was run

```
dotnet run --project src\SecretPrinter.Service --configuration Release -- --config secretprinter.json
```

On the dual-homed Windows 10 machine, from an ordinary PowerShell session
**without elevation** — no "Run as administrator", no service account, no
special rights requested.

Configuration: client interface `Ethernet 2` (192.168.1.234, Optimum), printer
interface `Wi-Fi` (192.168.12.245, T-Mobile), printer instance
`EPSON ET-3760 Series._ipp._tcp.local`, advertised on port 631 with a
freshly generated UUID.

## Result: it worked

The proxy appeared on an iPhone as **SecretPrinter (ET-3760)** and printed.

The run lasted about nine minutes and handled 128 mDNS queries, of which 20 were
for the advertised service and 108 were for other services and ignored.

## Privileges required: measured, not assumed

This is the answer to `REQ-SEC-006`, and it was previously an open question.

**No elevation is required.** Running unelevated, the service successfully:

| Operation | Result |
| --- | --- |
| Bind UDP 5353 with `SO_REUSEADDR`, alongside the Windows DNS Client | Succeeded |
| Join 224.0.0.251 on Ethernet 2 (index 13) | Succeeded |
| Join 224.0.0.251 on Wi-Fi (index 11) | Succeeded |
| Enable `IP_PKTINFO`, set multicast TTL 255 | Succeeded |
| Bind TCP 192.168.1.234:631 | Succeeded |
| Open outbound TCP to 192.168.12.180:631 | Succeeded |

Windows, unlike Unix, does not reserve ports below 1024 for privileged
processes. That was the expectation; this run confirms it for port 631 and
port 5353 on this machine.

### Precisely what this does and does not establish

The session ran under a user account without elevation. On Windows an
administrator's unelevated process runs with a **filtered token**, carrying
standard-user privileges, so this demonstrates the service works at
standard-user level.

**Not yet measured:** running under a dedicated service account such as
`NT AUTHORITY\LocalService`. That account is more restricted still — no network
credentials, minimal local rights — and is the intended target once the Windows
service question (open question 6) is settled. Until someone runs it there, the
claim is "no elevation required", not "runs as LocalService".

## The defect this run exposed

**Not one of 128 relayed connections was logged as completed or failed.** Every
job logged `accepted` and `relaying`, and then nothing.

The cause: when one direction of the relay reaches end-of-stream it stops the
other, which surfaces as an `OperationCanceledException` from `Task.WhenAll`.
The exception filter excluded that type, so it escaped `RelayOneAsync`
entirely — no completion callback, no failure callback, and an unobserved task
fault.

Every unit test missed it because the tests close both sides of the connection
at once, so both directions usually reach end-of-stream before either cancels
the other. A real printer does not behave that way: it answers and holds its
side open, and only the client hangs up. The bug appeared on the first real
connection and on all 128.

### Fixed

The sibling cancellation is now recognised as the ordinary end of a relay.
Byte counts accumulate as bytes move rather than being read from a task result,
so a direction that was stopped still reports what it carried.

Two regression tests were added, both shaped like the real case: only the
client closes. Their value was confirmed by reintroducing the original code and
watching both fail, then restoring it and watching both pass.

**The lesson worth keeping:** 107 passing unit tests did not catch a defect
visible in the first second of real use, because the tests shared an assumption
about how connections end. Component tests constrain the pieces; only running
the thing tells you about the assumption they share.

## Network behaviour observed

Printing worked from a phone connected to **a separate access point bridged to
the Optimum router**. It did **not** work from a phone connected to the Optimum
gateway's own wireless.

This is unconfirmed, but the likely cause is the gateway filtering multicast
between its own wireless clients and the wired LAN, which consumer ISP routers
commonly do. A bridging access point passes those frames at layer 2 without
filtering. If so, nothing in SecretPrinter can change it — the query never
reaches the proxy.

**To confirm:** run `SecretPrinter.Listen --interface 192.168.1.234` with a
phone on the gateway's own wireless and open a print dialog. If no
`_universal._sub._ipp._tcp.local` query arrives, the gateway is dropping it
upstream of us.

## Also observed, unexplained

On one reconnection iOS reported the printer as "a different printer" and asked
whether to continue. The likely explanation is a cached entry from the earlier
`SecretPrinter TEST - do not use` responder experiment, or from a run before the
UUID was set to its final value. **This is a guess.** If it recurs with a stable
configuration it is a real defect and should be investigated rather than
dismissed.

# SecretPrinter.Listen

A receive-only diagnostic. It answers one question that the rest of the project
depends on.

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

*Changed 2026-09-23 by Claude (Anthropic model, Claude Opus 5.5) at the
direction of Edwin West: packet lines carry the UTC arrival time and, for
replies, the TTL of every answer record, so the tool can show whether the
service's goodbye records reach the network. Reviewed by a human before
merge.*

## The question

> Can this process receive multicast DNS on UDP port 5353 while the Windows DNS
> Client service is already bound to that port, and can it tell which interface
> each packet arrived on?

The SecretPrinter proxy has to listen for AirPrint queries on one network and
answer them. On a Windows machine, port 5353 is already occupied by `Dnscache`
and often by browsers as well. If the operating system will not deliver
multicast to an additional socket on that port, the architecture has to change
before any service code is written. This tool finds out cheaply.

## How it works

1. Binds one UDP socket to `0.0.0.0:5353` with `SO_REUSEADDR` and
   `ExclusiveAddressUse = false`, so it *shares* the port rather than seizing
   it.
2. Joins multicast group `224.0.0.251` on each interface you name.
3. Enables `IP_PKTINFO`, so the OS reports the arrival interface per datagram.
4. Prints one line per packet, then a summary. See
   [Reading a packet line](#reading-a-packet-line).

Binding to the wildcard address rather than a specific interface address is
deliberate. On Windows, a socket bound to a single unicast address is not
reliable for receiving multicast; recovering the arrival interface from
`IP_PKTINFO` is the supported approach.

## What it does not do

- It **never transmits.** There is no send call anywhere in its source. No
  query, no response, no advertisement. Other hosts cannot detect it running.
- It does **not** write files, touch the registry, or alter firewall rules.
- It does **not** stop, disable or reconfigure any other service.

## Privacy note

mDNS on a home network is chatty and carries device names. While running, this
tool observes whatever your networks broadcast — phones, speakers, televisions,
computers — not only printers. It holds that in memory for the run and writes
none of it to disk.

**Read the output before publishing it.** Sharing a raw capture from this tool
means sharing an inventory of your household's devices.

## Running

```
cd tools/SecretPrinter.Listen
dotnet run -- --interface 192.168.1.234 --interface 192.168.12.245 --duration 60
```

| Option | Meaning |
| --- | --- |
| `--interface <ipv4>` | Local IPv4 address of an interface to join on. Required; repeatable. |
| `--duration <s>` | Seconds to listen. Default 30. Ctrl+C stops early. |
| `--help` | Usage. |

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | At least one packet was received. |
| 1 | No packets were received. |
| 2 | Bad arguments. |
| 3 | Socket error, including failure to bind or join a group. |

## Reading a packet line

Each line gives, in order:

- the time the packet arrived, in UTC, to a tenth of a second. The date is
  printed once, on the `Started …Z` line before the first packet;
- the interface index and name it arrived on, as `IP_PKTINFO` reported them;
- the source address and the size in bytes;
- `QUERY` and the questions asked, or `REPLY`, then `ttl=[…]` holding the TTL of
  every answer record in the order they appear, then the answer names.

Long name lists are cut to fit the line. The TTL list is placed before them so
that it is never cut.

A reply whose TTLs are all `0` is a goodbye (RFC 6762 section 10.1): the sender
is withdrawing those records. That is what to look for from the SecretPrinter
service's address when it stops or withdraws (`REQ-LIF-003`, `REQ-LIF-006`).
UTC is used so that a line here can be matched against the service log, whose
timestamps are UTC.

## Reading the result

The summary separates what the run **established** from what it did **not**,
because silence is ambiguous. No packets on an interface may mean delivery
doesn't work there, or simply that nothing spoke mDNS while the tool ran.

To distinguish them, generate traffic: open a print dialog, or Settings, on a
phone connected to that network. Do not record a failure without doing that
first.

One thing this tool deliberately does **not** establish: whether the process can
*send* mDNS responses that other hosts accept while another responder holds the
same port. Receiving and responding are separate problems. A green result here
is necessary, not sufficient.

## Verification performed

Exercised in a Linux container by injecting synthetic multicast traffic on the
loopback-enabled interface:

- A DNS-SD query and a PTR response were both received and correctly
  classified.
- The arrival interface was correctly attributed via `IP_PKTINFO`.
- A deliberately malformed 3-byte datagram was reported as unparseable without
  interrupting the receive loop.
- Argument validation and the no-traffic summary path were exercised.

The project compiles with `TreatWarningsAsErrors` and zero warnings.

These checks were ad-hoc, not a committed test suite. Converting them into
automated tests is a tracked follow-up. Note also that the container is Linux;
the port-sharing behaviour this tool exists to measure is
**Windows-specific and has not yet been observed on Windows.** That is the run
you are about to do.

The 2026-09-23 change (UTC arrival times and answer TTLs) was written without a
compiler, because there is no .NET SDK where Claude works. When it was delivered
it had been neither built nor run, and nothing above covers it.

# SecretPrinter.Probe

A read-only diagnostic. It asks one network what print services are advertised
over mDNS, and prints the answers.

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

## Why this exists

To advertise a proxy that iOS will accept as an AirPrint printer, we have to
publish TXT records matching what the real printer supports — `pdl`, `URF`,
`rp`, `UUID`, `Color`, `Duplex` and others. Guessing those values produces a
printer that appears in the list and then fails or misprints. This tool reads
them from the printer instead of guessing.

It also serves as reproducible evidence. Anyone auditing this repository can run
it against their own printer and see the same class of output we designed
against, rather than taking our word for what a printer advertises.

## What it does, completely

1. Opens one UDP socket bound to an interface address you name explicitly, on an
   operating-system-assigned ephemeral port.
2. Sends DNS-SD PTR queries to `224.0.0.251:5353` out of that interface.
3. Listens for replies until a timeout elapses.
4. Sends follow-up SRV/TXT queries for any instance found, then A queries for
   any host name those SRV records point at.
5. Prints a report and exits.

## What it does not do

- It does **not** bind port 5353, so it cannot disturb the Windows DNS Client
  service or any other mDNS responder on the machine.
- It does **not** advertise, publish, respond to, or forward anything.
- It does **not** write files, touch the registry, or alter firewall rules.
- It does **not** transmit anything off the local link.

## A limitation stated plainly

This tool queries from an ephemeral port, which makes it a *legacy unicast
querier* under [RFC 6762 §6.7](https://www.rfc-editor.org/rfc/rfc6762#section-6.7).
Responders are required to answer such queries directly by unicast.

That choice was made because port 5353 on the target machine is already shared
by several processes, and unicast delivery to a shared port is not
deterministic — a probe bound there could silently lose replies and mislead us.

The honest consequence: **a successful run here proves nothing about whether the
eventual service can share port 5353.** That question is answered by a separate
tool, `SecretPrinter.Listen`, and must not be assumed answered by this one.

## Building and running

Requires the .NET 10 SDK.

```
cd tools/SecretPrinter.Probe
dotnet build
dotnet run -- --interface <your-local-ipv4> --timeout 6
```

Find your interface addresses with:

```powershell
Get-NetIPAddress -AddressFamily IPv4 |
  Where-Object { $_.IPAddress -notlike '127.*' } |
  Select-Object InterfaceAlias, IPAddress, PrefixLength
```

### Options

| Option | Meaning |
| --- | --- |
| `--interface <ipv4>` | Local IPv4 address to query from. Required; the tool will not choose for you. |
| `--service <type>` | Service type to enumerate, e.g. `_ipp._tcp.local`. May be repeated. |
| `--timeout <s>` | Seconds to listen after each query. Default 5. |
| `--raw` | Also print a hex dump of every reply. |
| `--help` | Usage. |

By default the tool asks only for printing-related service types:
`_ipp._tcp.local`, `_ipps._tcp.local`, `_printer._tcp.local`,
`_pdl-datastream._tcp.local`, and `_universal._sub._ipp._tcp.local`. Nothing
else is asked for, so nothing else can appear in the report.

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | At least one reply was received. |
| 1 | No replies were received. |
| 2 | Bad arguments. |
| 3 | Socket error. |

## Firewall

This tool does not create firewall rules, by design — see the project's trust
principles. If no replies arrive, Windows Firewall blocking inbound UDP for this
program is the first thing to check.

## Verification performed

The DNS wire-format parser now lives in `src/SecretPrinter.Dns` and is shared
with the other tools, so there is exactly one parser in this repository to
audit. It was exercised against a
synthetic AirPrint-style response containing name-compression pointers, a
cache-flush bit, an instance name with embedded spaces, and SRV/TXT/A records.
All fields decoded correctly. The project compiles with
`TreatWarningsAsErrors` and zero warnings.

This verification was ad-hoc. Converting it into a committed unit-test suite is
a tracked follow-up, not something already done.

## Known issue, fixed

An earlier version of the report grouped records by object identity rather than
content. When two query rounds returned the same SRV or TXT record, the second
copy fell through the grouping and appeared under "Other records", implying a
finding where there was none. De-duplication is now content-based. If you have
output from a run showing duplicated SRV/TXT entries in that section, that was
this bug, not your network.

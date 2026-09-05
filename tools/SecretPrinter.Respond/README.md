# SecretPrinter.Respond

An experiment. It advertises **one fake printer that cannot print**, to answer
two questions the whole proxy design rests on.

*Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of Edwin
West. Reviewed by a human before merge.*

## The questions

**1. Can this process send mDNS responses that clients accept, while the Windows
DNS Client service already holds port 5353?**

`SecretPrinter.Listen` confirmed we can *receive* alongside `Dnscache`.
Responding is a different problem, and every requirement in Section 4 of the
[specification](../../README.md) depends on the answer.

**2. Will an iOS client offer a printer advertised only as `_ipp._tcp`, with no
IPPS variant?**

Captured traffic showed an iPad querying `_universal._sub._ipps._tcp.local`
*before* the plain `_ipp` form. If iOS insists on TLS, the proxy must terminate
it — a substantial addition. This tool advertises no IPPS at all, so the printer
list gives a direct answer.

## Running it

```
cd tools/SecretPrinter.Respond
dotnet run -- --interface 192.168.1.234 --duration 300
```

Then, on a device on that same network, open any print dialog and look at the
printer list.

| Option | Meaning |
| --- | --- |
| `--interface <ipv4>` | Interface to advertise on. Required, and only one. |
| `--duration <s>` | Seconds to run. Default 300. Ctrl+C stops early. |
| `--instance <name>` | Instance name. Default `SecretPrinter TEST - do not use`. |
| `--host-label <label>` | Host label published. Default `secretprinter-test`. |
| `--port <n>` | Port advertised. Default 631. Nothing listens there. |
| `--verbose` | Print response destinations. |

### Exit codes

| Code | Meaning |
| --- | --- |
| 0 | At least one query for the advertised service was answered. |
| 1 | No such query arrived. |
| 2 | Bad arguments. |
| 3 | Socket error. |

## Reading the result

**Exit 0 means we answered. It does not mean the client accepted.** The printer
list on the device is the only thing that settles that. Answering is necessary,
not sufficient, and the summary says so at the end of every run.

If queries arrive but none are for the advertised service, no client was
browsing — clients look for printers only when something asks them to. Open a
print dialog and run again.

## What everyone on that network will see

The fake printer appears in the printer list of **every Apple device on the
advertised network** while this runs. The instance name says it is a test and
should not be used.

Selecting it will fail: nothing is listening on the advertised port. That is
expected — discovery is what is being measured.

Goodbye records (TTL 0) are sent twice on exit, including on Ctrl+C, so the
entry disappears promptly. A device that was asleep may hold a stale entry until
its cache expires, about two minutes.

## What it does not do

- It does not print, relay, or listen on the advertised port.
- It does not advertise on any interface but the one named. Queries arriving on
  another interface are counted and dropped.
- It does not answer queries for anything other than its own records.
- It does not read the real printer or touch the printer network at all.
- It does not write files, touch the registry, or alter firewall rules.

## What it advertises, and what it deliberately does not

The honesty rules in Section 4 of the specification get their first exercise
here. Format capabilities are copied from what the Epson actually advertises —
those describe what would print, and they were measured with
`SecretPrinter.Probe`, not invented:

```
pdl=application/octet-stream,image/pwg-raster,image/urf,image/jpeg
URF=CP1,PQ4-5,OB9,OFU0,RS300,SRGB24,W8,DM3,IS1,V1.4,MT1-3-6-8-10-11-12
```

Identity and capability claims are not copied:

| Omitted | Why |
| --- | --- |
| `UUID` | A generated one is used. Two devices claiming one identity breaks clients. |
| `Scan`, `Fax` | This relays nothing, so it claims nothing. |
| `mopria-certified` | The printer holds that certification. This does not. |
| `adminurl` | The printer's points at a host unreachable from the client network. |

The instance name announces that it is a test. Nothing here impersonates the
Epson.

## Known departure from RFC 6762

A conforming responder probes for name conflicts before claiming a name
(§8.1). **This tool does not probe.** It runs briefly, under supervision, with a
name nothing else will plausibly hold. The real service must probe; this is
recorded so the omission is not later mistaken for the intended design.

## No `[Requirement]` markers

This tool carries none, on purpose. The requirements in `README.md` describe the
service, and this experiment will be superseded by it. Marking a throwaway as
implementing service requirements would make the coverage matrix say something
untrue.

## Verification performed

In a Linux container, with the responder running and `SecretPrinter.Probe`
querying it:

- All three announcements were sent (796 bytes each) to `224.0.0.251:5353`.
- The probe resolved the advertised instance through both `_ipp._tcp.local` and
  `_universal._sub._ipp._tcp.local`, receiving matching SRV, TXT and A records.
- The instance name's embedded spaces survived the round trip intact.
- Legacy unicast responses had their TTLs capped to 10 s per RFC 6762 §6.7, and
  the query identifier was echoed.
- Goodbye records were sent twice with TTL 0.
- Argument validation was exercised: missing interface, an address not present
  on the machine (caught before any socket is opened), an over-long instance
  name, and an invalid port.
- The no-traffic summary path was exercised.

The project compiles with `TreatWarningsAsErrors` and zero warnings.

**What the container could not test:** port sharing with `Dnscache` is
Windows-specific behaviour, and whether a real iOS client accepts the
advertisement cannot be simulated. Those are the questions the actual run
answers.

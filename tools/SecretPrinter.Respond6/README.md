# SecretPrinter.Respond6

**An experiment. Not part of the product. Never released, never signed.**

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-06. Reviewed by a human before merge.*

---

## Why this exists

A packet capture taken on `FIOS-STB-01` (Ethernet 3, filter `tcp port 631 or udp port 5353`,
87.2 seconds, 28 packets) showed the following:

- Every packet came from the iPhone at `192.168.1.152`, sent from an IPv6
  link-local address to `ff02::fb`.
- **Zero IPv4 mDNS packets appeared on the segment at all.**
- **Zero responses of any kind were sent.** No packet in the capture originated
  from Ethernet 3's own MAC address.
- The phone asked for `_universal._sub._ipp._tcp.local`, and for `A`, `AAAA` and
  `HTTPS` records of `secretprinter.local`.

`netstat` showed `SecretPrinter.Service` (PID 16300) bound to `0.0.0.0:5353` and
to nothing on `[::]`. The service had no IPv6 socket, so it never received the
queries. It was not failing to answer — it never heard the question.

## The single question this answers

> Will iOS accept an `A` record (IPv4 only, no `AAAA`) delivered over IPv6 mDNS
> transport, and then open an IPP connection over IPv4 to port 631?

This matters because the printer, an Epson ET-3760 at `192.168.12.180`, is
IPv4-only. The answer decides how large the real fix is:

| Answer | What has to change |
|---|---|
| **Yes** | A new transport in `SecretPrinter.Mdns`. Advertising, resolution and proxy are untouched. |
| **No** | IPv6 has to reach into the proxy and the SRV/TXT layers as well. |

This project settles questions like this by measurement. `REQ-ADV-002` — the
`_universal._sub` requirement — came out of the `SecretPrinter.Respond`
experiment, not out of reasoning. Same approach here.

## How the result is read

**From Wireshark, not from this program's console output.**

| Observation | Conclusion |
|---|---|
| TCP SYN to `<this host>:631` | **YES.** iOS accepted A-over-IPv6. |
| No SYN, mDNS queries keep repeating | **NO.** iOS requires AAAA. |

A connection that is **refused** still counts as YES. The measurement is whether
iOS *attempts* the connection. This tool never opens a TCP socket and does not
need `SecretPrinter.Service` running.

## What this tool does and does not do

Does:

- Binds one UDP socket to `[::]:5353` with address reuse.
- Joins `ff02::fb` on exactly one interface, named explicitly on the command line.
- Sends three unsolicited announcements at startup, one second apart.
- Answers queries for the names given on the command line, and only those.
- Returns an `NSEC` record asserting that only an `A` record exists, so iOS gets
  a definite "no AAAA here" instead of silence.

Does not:

- Print anything, proxy anything, or contact the real printer.
- Open any TCP socket.
- Read or write any file, except an optional TXT file passed with `--txt`.
- Touch firewall rules, the registry, or any system setting.
- Answer on IPv4. That would defeat the purpose of the experiment.

## Honest limitations

These are deliberate and scoped to a short manual experiment. Every one of them
would be unacceptable in the shipping service.

1. **No conflict probing** (RFC 6762 §8.1). The tool announces names without
   first checking whether anything else claims them.
2. **No known-answer suppression** (RFC 6762 §7.1). Answer sections of incoming
   queries are not parsed.
3. **No response delay.** Shared records should be delayed 20–120 ms. Responses
   here are immediate, which is simpler to read in a capture.
4. **Names are written without compression.** Legal, but packets are larger than
   Bonjour's. Incoming names *are* decompressed, since iOS uses pointers.
5. **Does not use `SecretPrinter.Dns`.** The wire format here is a standalone
   reimplementation. Claude wrote this without access to the `SecretPrinter.Dns`
   public API, and inventing call signatures would have been a guess. A bug in
   this tool therefore proves nothing about the product code — suspect this tool
   first if the result is surprising.
6. **The built-in TXT entries are not measured values.** They are a plausible
   minimal AirPrint set. If iOS does not display the printer, replace them with
   real output from `SecretPrinter.Probe` via `--txt` rather than adjusting them
   by guess.

## Verification performed before delivery

Claude could not compile this in its container — Microsoft's binary hosts are
blocked by the sandbox egress proxy — so the C# is **not compile-tested**. Expect
to fix build errors on first run.

What *was* verified, by transcribing the encoder and parser line-by-line into
Python and checking against `scapy`, an unrelated implementation:

- The announcement message parses correctly: 6 records, correct types, TTLs
  (120 for host-name records, 4500 for others), and TXT strings.
- The cache-flush bit is set on `SRV`, `TXT`, `A` and `NSEC`, and correctly
  **not** set on the shared `PTR` records. Confirmed in the raw bytes
  (`0x8001` vs `0x0001`).
- The `SRV` record carries port 631 and target `secretprinter.local`.
- The `NSEC` type bitmap (`00 01 40`) asserts that `A` exists and that `AAAA`
  and `HTTPS` do not. Decoded bit by bit.
- The query parser was run against all 28 real packets from the capture and
  agreed with `scapy` on all 107 questions.
- The response-planning logic fires on exactly 11 of the 28 captured packets —
  the subtype, SRV/TXT and host-name queries — and correctly ignores the other
  17 (AirPlay, companion-link, `epson3ea18a.local`, and so on).

## Placement

Put this folder wherever `SecretPrinter.Respond` lives. It has no project
references, so the location does not affect the build.

**Do not add it to `SecretPrinter.sln`.** It carries no `[Requirement]`
attributes because it implements no requirement, and adding it would put an
uncovered project in front of `SpecCheck`.

## Running it

Stop `SecretPrinter.Service` first, so exactly one responder is answering for
these names.

```
dotnet run --project SecretPrinter.Respond6 -- --list
```

```
dotnet run --project SecretPrinter.Respond6 -- ^
    --interface "Ethernet" ^
    --ipv4 192.168.1.163
```

Options: `--instance`, `--host`, `--port`, `--txt`. All defaults are printed at
startup; nothing is auto-detected or silently assumed.

## License

MIT, same as the rest of the repository.

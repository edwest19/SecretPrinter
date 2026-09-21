# The certificate pin refuses a mismatch, measured against the real printer

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-21, from a run on 2026-09-18. Reviewed by a human before merge.*

**Status: measured on hardware. With one hex digit of the pinned fingerprint
changed, every connection was refused after the printer presented its
certificate, the refusal was logged naming both fingerprints, and nothing was
relayed. This is the hardware evidence for `REQ-SEC-013` and `REQ-PXY-012`,
which until then rested on unit tests that never complete a handshake.**

## Why this run was needed

`TlsConnectionFactoryTests` cannot complete a TLS handshake: on Windows, the TLS
component cannot act as a server with an ephemeral in-memory key, and giving it a
usable one would mean writing into the Windows key store. So those tests drive
the client half against peers that refuse, answer wrongly or say nothing. They
exercise the real validation callback, but they never see this printer present
its certificate. Only the printer can show that a wrong pin is refused.

## The run

2026-09-18, on the dev box, run from the console with `--log-file`. FIOS-STB-01
was stopped, so only one host was advertising. The real configuration was
copied to a gitignored `wrongpin.local.json`, and **one digit** of
`printerCertificateSha256` was changed: the first, `3` to `1`. The startup
block confirmed the altered value was the one in force:

```
certificate pin  : SHA-256 1834787341192E3B7B74FFE51A9EE0E67E2DEBD6B4A060C6B203CB5252BE80EF
```

Then a page was printed from the iPhone (then at `192.168.1.156`). iOS made
seven attempts. Every one reads the same way; the first:

```
Job: accepted from 192.168.1.156:60307.
Job: located EPSON ET-3760 Series._ipps._tcp.local at 192.168.12.180:631 (cached, 0.001s).
Job: failed for 192.168.1.156:60307: Could not connect to the printer at 192.168.12.180:631:
  The printer presented a certificate with SHA-256 fingerprint
  3834787341192E3B7B74FFE51A9EE0E67E2DEBD6B4A060C6B203CB5252BE80EF, which does not match the
  pinned fingerprint 1834787341192E3B7B74FFE51A9EE0E67E2DEBD6B4A060C6B203CB5252BE80EF.
  (0 bytes sent, 0 received, 1.2s)
```

(Wrapped here for reading; in the log it is one line.)

| Connection | Refused after |
|---|---|
| `60307` | 1.2 s |
| `60308` | 0.75 s |
| `60309` | 0.75 s |
| `60311` | 0.83 s |
| `60310` | 0.83 s |
| `60312` | 0.84 s |
| `58640` | 0.75 s |

**No `Job: relaying` line appears anywhere in the run.** The two fingerprints
differ in exactly the digit that was changed, and the presented one is the
fingerprint recorded for this printer.

## What this establishes

- **The printer's certificate is checked on every connection.** Seven
  connections, seven checks, seven refusals — none was let through because an
  earlier one had been decided.
- **The refusal comes after the handshake reaches the certificate, and before
  any job byte moves.** The printer did receive a TLS handshake, since it
  presented a certificate; it received nothing of the document. The relay's
  counters read `0 bytes sent, 0 received`, and `relaying` — which is logged
  only once a verified connection exists — never appears.
- **The refusal is logged, with enough to act on.** It names what the printer
  presented and what was pinned. An operator whose printer's certificate has
  genuinely changed can see the new fingerprint in the log, confirm it
  independently before trusting it, and update the configuration. Nothing prints until they do. That is
  the intended behaviour: there is no permissive mode (`REQ-SEC-014`).

A contrast worth recording: this failure surfaces as an
`AuthenticationException`, which `IppRelay` catches and reports. At the time of
this run, a printer that never accepted the TCP connection surfaced as a
cancellation and was *not* reported —
[`2026-09-17-connect-timeout-is-not-reported.md`](2026-09-17-connect-timeout-is-not-reported.md),
fixed on 2026-09-21 in `d29dd4e`. Same path, opposite outcome.

## The correct pin still prints

The confirmation run on 2026-09-18, with the real fingerprint restored, is not
in hand as a log, so it is not relied on here. The same fingerprint,
`3834787341…`, has printed since: on 2026-09-21 FIOS-STB-01, running `d29dd4e`
with that value in its configuration, relayed two pages from the iPhone at
20:19:44Z and 20:20:53Z (39,094 and 38,999 bytes to the printer), and both came
out.

## What this does not establish

- **An attacker's printer.** The run used the real printer with a wrong pin,
  which is the same comparison as a different device presenting a different
  certificate to a correct pin. It was not tested with a second device
  impersonating the printer's address.
- **Anything about the client side.** The connection from the iPhone to the
  proxy is plaintext by design, as the README discloses. This finding is about
  the proxy-to-printer leg only.

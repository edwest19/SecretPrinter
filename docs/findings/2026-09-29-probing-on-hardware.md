# Probing on hardware: a clear start, two conflicts, and recovery

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-29. Reviewed by a human before merge.*

**Status: measured on hardware (FIOS-STB-01, running `8f3ea90` as
LocalService). Probing (`REQ-ADV-023`) and the conflict handling
(`REQ-ADV-024`) behaved as specified in every step below, and a page printed
from an iPhone before and after the conflicts. Commands and outputs are in the
session of 2026-09-29; the log lines quoted are the service's own.**

## The setup

- **FIOS-STB-01:** client side `Ethernet` (192.168.1.161, index 17); printer
  side `Wi-Fi 3`, the Linksys USB adapter, now on a powered USB hub (index 26,
  192.168.12.136). The Broadcom 802.11n adapter is disabled on purpose: Edwin
  judges it unstable. The printer is at 192.168.12.180.
- **The build:** `8f3ea90`, published over `76330ce`. All nine DLLs were written
  between 04:10:09 and 04:10:14 UTC, before the first start below.
- **The second responder:** `SecretPrinter.Respond6` on the dev box (`Ethernet 2`,
  192.168.1.234), run with `--instance "Conflict Test"` and its default host
  name, `secretprinter.local` - the service's host name - with `--ipv4
  192.168.1.234`. A different instance name was chosen so that the iPhone's
  list could show the two apart: with its default instance, `Respond6` would
  have advertised "SecretPrinter (ET-3760)" itself. `Respond6` sends and
  listens over IPv6 only.
  *(Status note, 2026-10-02, by Claude, Claude Opus 5.5: `Respond6` was
  deleted on 2026-10-02. Its source is in the repository's history; the last
  commit that holds `tools/SecretPrinter.Respond6` is `a10087d`. Repeating
  steps 2 to 5 below needs the tool from that commit, or another responder
  that claims the service's host name. No tool now in `tools/` is recorded as
  having been run as one.)*

## What happened

All times UTC. The dev box's clock reads four hours behind.

1. **A clear start.** `Probing for the advertised names...` at 04:11:49,
   `Probe clear` at 04:11:50, `Announced. Answering queries.` and
   `Accepting print jobs on 192.168.1.161:631` at 04:11:52. A page printed from
   an iPhone; every connection was relayed over TLS 1.2 with the certificate
   matching the pin.

2. **A conflict after announcing.** `Respond6` announced at 04:22:41.9. At
   04:22:42 the service logged:

   `ERROR  Name conflict: fe80::…%17 answered for secretprinter.local (A) with a
   record this service would not publish, heard on Ethernet (192.168.1.161, IPv6
   index 17). …`

   and in the same second `No longer accepting print jobs on Ethernet.` The
   conflict is the `A` record because `Respond6`'s PTR, SRV and TXT records were
   all for "Conflict Test", a name the service does not claim. On the iPhone,
   "SecretPrinter (ET-3760)" dropped from the list at once and only "Conflict
   Test" was shown.

3. **A conflict found at startup.** With `Respond6` still running, the service
   was restarted: goodbyes at 04:29:08, `SecretPrinter starting.` at 04:29:09,
   `Probing…` at 04:29:10 and, in the same second, the same `Name conflict`
   line. No `Probe clear`, no announcement, no listener. `Respond6` logged one
   query from FIOS-STB-01, asking type `ANY` for both of the service's names,
   and answered it with its `A` record: the first answer ended the probe, so
   the second and third probes were never sent.

4. **No listener while the conflict was held.** `Get-NetTCPConnection
   -LocalPort 631 -State Listen` on FIOS-STB-01 returned nothing.

5. **Recovery.** `Respond6` was stopped (it reported stopping cleanly) and the
   service restarted: `Probing…` at 04:32:29, `Probe clear` at 04:32:30,
   `Announced` and `Accepting print jobs` at 04:32:32, and the socket table then
   showed `192.168.1.161:631 Listen`. "Conflict Test" was no longer listed on
   the iPhone, "SecretPrinter (ET-3760)" was, and a page printed.

## Also observed

- **IPv6 mDNS between the dev box and FIOS-STB-01 works both ways** on this
  network: the service heard `Respond6`'s announcement over IPv6, and `Respond6`
  heard the service's IPv6 probe. Between these two machines it had not been
  measured before.
- **The published note carried the printer's address.** The iPhone showed it
  under the printer's name. Recorded and fixed separately:
  [2026-09-29](2026-09-29-the-note-published-the-printers-address.md).
- **The iPhone asked the client network for the printer's own `.local` host
  name** (HTTPS, `AAAA` and `A`), seen by `Respond6`. SecretPrinter never
  advertises that name. Where the iPhone learned it is not established. Its
  list of known printers showed "EPSON ET-3760 Series" alongside the proxy, so
  the phone remembers the printer itself; the IPP replies the relay passes on
  unchanged are the other possible source.

  *(Status updated 2026-09-29 by Claude, Claude Opus 5.5: measured. In three
  packet captures on the client network SecretPrinter never carried the
  printer's host name, neither in its mDNS answers nor in the relayed IPP, and
  both devices seen asking for it had been on the printer's own network. See
  [2026-09-29](2026-09-29-the-printers-host-name-did-not-come-through-secretprinter.md).)*
- **FIOS-STB-01 asked for its own Windows host name** (`ANY`), also seen by
  `Respond6`. That is the machine's own name, not one SecretPrinter uses.
- **The iPhone's address.** The first print's connections came from
  192.168.1.156 and the last print's from 192.168.1.152. Which device held which
  address was not established.

## Not established

- **Why the iPhone dropped the printer so promptly.** The service's goodbyes go
  out over IPv4 only, and an iPhone has kept the printer listed after goodbyes
  before (see `docs/operating.md`, Uninstalling). Whether it heard the goodbye
  this time, or dropped the entry for another reason, was not measured.
- **A conflicting device that answers over IPv4 only.** `Respond6` uses IPv6.
  The service probes over both, but an IPv4-only conflict was not run.
- **A simultaneous probe on hardware.** The tiebreak (RFC 6762 §8.2) is covered
  by tests only.
- **A socket failure during a probe when the printer returns.** The service then
  offers nothing until the printer is lost and found again, or the service is
  restarted, and says so in the log. That path is covered by code reading only.

# The note published the printer's address

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-29. Reviewed by a human before merge.*

**Status: fixed in code in the commit that adds this finding; not yet confirmed
on hardware. Found on hardware, on an iPhone, during the probing run on
FIOS-STB-01 on 2026-09-29.**

*(Status updated 2026-09-29 by Claude, Claude Opus 5.5: confirmed on hardware.
With `017e5a8` installed on FIOS-STB-01 the log recorded the published entry
`note=SecretPrinter proxy. Capabilities read from the printer at 2026-09-29
04:53:47Z`, the full source appearing only in the log's `capabilities observed`
line, and an iPhone on the client network's Wi-Fi showed that note, with no
address, under the printer's name at 04:59.)*

## What was seen

On 2026-09-29, during the hardware run of probing (`REQ-ADV-023`,
`REQ-ADV-024`), an iPhone's printer list showed this under
"SecretPrinter (ET-3760)":

> SecretPrinter proxy. Capabilities from: mDNS query to 192.168.12.180 for
> EPSON ET-3760 Series._ipp._tcp.local

`192.168.12.180` is the printer's address on the printer-side network. The
second part is the instance name the printer publishes itself under there.

## Why

`AdvertisementBuilder` publishes a TXT entry, `note`, which read:

`note=SecretPrinter proxy. Capabilities from: {source.Description}`

In the service the source is `CapabilitySource.FromMdnsQuery`, whose
description is `mDNS query to {printer address} for {printer instance}`. So
every advertisement the service has made, since the initial commit (`699598b`,
2026-09-05), put the printer's printer-side address and instance name into a
TXT record on the client network.

`REQ-ADV-004` says the service MUST NOT publish the real printer's IP address,
hostname, or `A` record on the client network. This broke it.

## Why nothing caught it

- The `REQ-ADV-004` test built its advertisement from `CapabilitySource.ForTest`,
  whose description names no address, and it checked only address records and
  SRV targets. It never read a TXT entry.
- The `REQ-ADV-010` test asserted that the note contained "Capabilities from":
  it held the leak in place as intended behaviour.
- So SpecCheck reported `REQ-ADV-004` as `OK`, with a marker and a passing test,
  while the claim was false. That is the limit SpecCheck states about itself:
  it finds omissions, and cannot judge whether a test is meaningful.

## What was changed

Edwin West decided on 2026-09-29 that the note should read:

`note=SecretPrinter proxy. Capabilities read from the printer at <time>`

with the time the capabilities were read, written as the log writes times
(for example `2026-09-01 12:00:00Z`). This was chosen over a note that says
only that it is a proxy. Nothing in the note now comes from the source's
description. The full source is still written to the log at startup, which is
where an operator reads where the capabilities came from.

The tests:

- The `REQ-ADV-004` test now builds from an mDNS-query source, as the service
  does, and checks every TXT entry, record name and PTR target for the
  printer's address, host name and instance name.
- The `REQ-ADV-010` test now pins the note's wording, and still checks that the
  full source survives into the advertisement for the log.

Both failed before the fix, the first reproducing the text the iPhone showed.
Two deliberate breaks - the source description put back into the note, and the
time removed - are each caught.

## Not established

- **Hardware confirmation.** After this build is installed, the iPhone should
  show the new note. How long iOS keeps showing a TXT record it has cached has
  not been measured.
- **Whether the printer's host name reaches the client network another way.**
  On 2026-09-29 the iPhone asked the client network for the printer's own
  `.local` host name (seen by `SecretPrinter.Respond6` on the dev box).
  SecretPrinter never advertises that name. Two sources are possible and
  neither has been checked: the iPhone's own list of known printers, which
  showed "EPSON ET-3760 Series" alongside the proxy, so the phone remembers the
  printer itself; and the IPP replies the relay passes on unchanged
  (`REQ-PXY-002`). It is a separate item.

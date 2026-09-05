# Finding: ET-3760 advertised capabilities

**Date:** 2026-09-01
**Tool:** `tools/SecretPrinter.Probe`
**Recorded by:** Claude (Anthropic model, Claude Opus 4.5), directed by Edwin West

## Why this was measured

To advertise a proxy that iOS accepts as an AirPrint printer, the TXT records
have to match what the real printer supports. Guessing them produces a printer
that appears in the list and then fails or misprints. These values were read
from the device rather than assumed.

## Method

```
dotnet run --project tools/SecretPrinter.Probe -- --interface 192.168.12.245 --timeout 6
```

Run from the dual-homed Windows machine, querying out of the T-Mobile interface.

## Result

> **Redaction, disclosed.** This capture is verbatim except for one substitution.
> The device derives both its hostname and the tail of its `UUID` from its MAC
> address, so the low three bytes of each are replaced with `000000` here and in
> every test that uses these records as a fixture: the hostname reads
> `EPSON000000` and the `UUID` ends `f8d027000000`. Nothing else is altered — every
> key, every value and the structure of both identifiers are as measured. The
> reason is in `2026-09-04-pre-publication-audit.md`.

The printer answered from `192.168.12.180`, hostname `EPSON000000.local`.

| Service type | Port |
| --- | --- |
| `_ipp._tcp.local` | 631 |
| `_ipps._tcp.local` | 631 |
| `_universal._sub._ipp._tcp.local` | 631 |
| `_pdl-datastream._tcp.local` | 9100 |
| `_printer._tcp.local` | 515 |

The presence of `_universal._sub._ipp._tcp` confirms a genuine AirPrint device
rather than merely an IPP one.

### TXT records for `_ipp._tcp.local`

```
txtvers=1
ty=EPSON ET-3760 Series
usb_MFG=EPSON
usb_MDL=ET-3760 Series
product=(EPSON ET-3760 Series)
pdl=application/octet-stream,image/pwg-raster,image/urf,image/jpeg
rp=ipp/print
qtotal=1
Color=T
Duplex=T
Scan=T
Fax=F
kind=document,envelope,photo
PaperMax=legal-A4
URF=CP1,PQ4-5,OB9,OFU0,RS300,SRGB24,W8,DM3,IS1,V1.4,MT1-3-6-8-10-11-12
mopria-certified=1.3
priority=30
adminurl=http://EPSON000000.local.:80/PRESENTATION/BONJOUR
note=
UUID=cfe92100-67c4-11d4-a45f-f8d027000000
TLS=1.2
```

## What this means for the proxy

Values the proxy **copies**, because they describe formats and are honest about
what would print: `pdl`, `URF`, `Color`, `Duplex`, `PaperMax`, `kind`, `rp`.

Values the proxy **must not copy**:

| Value | Reason |
| --- | --- |
| `UUID` | Identifies this specific device. Two devices claiming one identity breaks clients that cache it. (REQ-ADV-005) |
| `Scan=T` | The printer scans; the proxy will not relay scanning. (REQ-ADV-006) |
| `mopria-certified=1.3` | The printer holds that certification. The proxy does not. (REQ-ADV-007) |
| `adminurl` | Points at `EPSON000000.local`, which does not resolve and is not routable from the client network. (REQ-ADV-008) |

## Caveats

**Every TTL in the capture reads 10 seconds.** That is not the printer's real
advertisement. The probe uses legacy unicast queries (RFC 6762 §6.7), which cap
response TTLs. Do not design timing against that number.

**The `_ipps._tcp` TXT record did not arrive** in this run, most likely a
response-size effect. If the proxy ever advertises IPPS, those values need a
separate measurement.

**The printer is on DHCP and moves.** It was observed at `.186` and later at
`.180` during a single session. Any design that stores its address will break.
This is why REQ-RES-001 requires resolution at connection time.

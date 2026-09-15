# The printer requires TLS before it will accept a job, and says so

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-15. Reviewed by a human before merge.*

**Status: measured. Two runs on `FIOS-STB-01` and one packet capture,
2026-09-15 03:11:35Z to 03:33:05Z. The cause of the printing failure is
identified and is not a defect in this software. What to do about it is an open
design question and is not decided here.**

## What the printer says

Every attempt to submit a job draws the same response from the printer:

```
HTTP/1.1 426 Upgrade Required
Upgrade: TLS/1.0, HTTP/1.1
Connection: Upgrade
Server: Epson_IPP-Server/2.0.0
Content-Type: application/ipp
Content-Length: 0
```

Nineteen times in the captured window, byte-identical each time apart from two
occurrences noted below. The request it answers is an IPP **Validate-Job**
(operation `0x0004`, IPP version 2.0) sent by the iPhone as
`POST /ipp/print HTTP/1.1` with `Host: secretprinter.local`,
`Content-Type: application/ipp`, `Transfer-Encoding: chunked` and
`User-Agent: com.apple.PrintKit.PrinterTool/319.1`.

The printer is not rejecting the content of the request. It is refusing to
discuss a job at all over an unencrypted connection, and it names the remedy in
the `Upgrade` header. `Get-Printer-Attributes` is answered in the clear, which
is why discovery works, why the proxy can read the printer's capabilities, and
why some relayed connections complete normally.

A plaintext proxy has no way to satisfy that demand. The phone asks permission,
is told to upgrade, cannot, and retries. The document is never sent.

## The numbers

**Run one, 03:11:35Z to 03:13:44Z.** 53 connections accepted, 48 failed, 4
completed, one still open at shutdown. Every failure had carried bytes before it
ended — 1591 bytes sent in 40 cases, 1314 in five, 2102 in three, 76,516 bytes
toward the printer in total. Every failure had also received a reply: 255 bytes
in 46 cases, 331 in two. Failure durations ran 0.23s to 3.18s.

Two of the four completions are indistinguishable from the failures in what they
carried — 657 bytes sent, 255 received — and differ only in ending cleanly after
the client held the connection open for thirty seconds. A 255-byte reply is
therefore not itself the failure.

**Run two, 03:22:38Z to 03:33:05Z**, is the run the capture covers. Its console
output was pasted with an earlier buffer interleaved into it, so no counts are
taken from that log; the capture is the record.

**The capture, `spt4.pcapng`.** 530 packets, 20 client connections. Nineteen
carried a Validate-Job and every one was answered `426 Upgrade Required`. Two of
the nineteen (client ports 50670 and 50672) received the 426 followed
immediately by `HTTP/1.1 500 Internal Server Error` with `Connection: close`,
which accounts for the 331-byte responses seen in both runs. One connection
carried a `Get-Printer-Attributes` with no response in the capture window. One
long-lived connection was already in progress when capture began; the service
log records it moving 40,348 bytes to the printer and 14,608 back over 108.7
seconds.

## What this evidence does not show

The capture was taken on the client side — `192.168.1.152` to
`192.168.1.161:631` — not on the printer-side Wi-Fi interface. What was read is
therefore what the relay delivered to the phone, not the printer's wire. That it
came from the printer rests on the `Server: Epson_IPP-Server/2.0.0` header and
on REQ-PXY-003, which requires the relay to copy bytes unchanged. That is a
sound inference and it is still an inference of one step.

For the same reason the capture contains no reset from the printer. Its seven
RST packets are all on the client side: six from `192.168.1.161:631` toward the
phone and one from the phone. The printer-side reset is visible only in the
service log, as `while reading from the printer`.

## Two explanations this run killed

**The printer reaps connections carrying no data.** Proposed in
`docs/findings/2026-09-14-ipv6-is-the-only-transport.md` as the hypothesis that
fitted the evidence available then, and it was the reason byte counts were added
to `RelayFailed`. It is refuted: not one failed connection carried zero bytes,
in either run.

**The printer rejects a `Host` header naming the proxy.** Proposed by Claude
immediately before the capture, from the fact that IPP is HTTP and the phone
addresses `secretprinter.local`. It is refuted: the printer receives that header
and answers with a demand for TLS, saying nothing about the host. The hypothesis
was plausible, was labelled as untested when offered, and was wrong. It is
recorded here because a guess that is quietly dropped once it fails teaches
nothing, while one written down next to the measurement that killed it does.

## What it means for the specification

Open question 1 in the README recorded that iOS does not require IPPS for
discovery, and that whether a job completes over plain IPP was untested until a
relay existed. The relay exists and the question is answered: against this
printer, a job does not complete over plain IPP and cannot be made to. The
sentence stating that TLS termination is deferred rather than required is no
longer true and has been corrected in the same commit as this document.

This does not change any requirement. REQ-ADV-006 still forbids republishing the
printer's `TLS=1.2` claim, and the proxy still terminates no TLS. What it
changes is that those are now the reason printing fails, rather than a deferred
convenience — and the decision about whether the proxy should originate TLS
toward the printer is recorded as open question 8 rather than being settled
here. It carries a disclosure consequence that belongs in that decision, not in
this measurement: a proxy that accepts plaintext from the client and encrypts
only toward the printer leaves job data in the clear on the client network, and
the README would have to say so as plainly as it says the data passes through
this machine at all.

## Follow-up measurements, same evening

Two further measurements were taken to answer how TLS would have to be spoken,
before any decision about whether to speak it. Both are recorded here because
the pin that ends up in configuration should have a traceable origin rather than
appearing in a settings file from nowhere.

**The printer advertises `_ipps._tcp`.** `SecretPrinter.Probe`, run on
`192.168.12.186`, found `EPSON ET-3760 Series._ipps._tcp.local` with an SRV
record naming host `EPSON3EA18A.local` and port **631** — the same port as
`_ipp._tcp`. Its TXT record was not returned. The same probe also shows the
printer advertising `_pdl-datastream._tcp` on port 9100 and `_printer._tcp` on
port 515, both raw and both unencrypted: this printer demands TLS for IPP job
operations while accepting plaintext jobs on two other ports. That is a fact
about the device, not an argument for or against anything.

**Implicit TLS on 631 works.** A one-line PowerShell diagnostic opened a TCP
connection to `192.168.12.180:631` and called `AuthenticateAsClient`. The
handshake succeeded:

```
Tls12
Subject     O=SEIKO EPSON CORP., CN=EPSON3EA18A
Issuer      O=SEIKO EPSON CORP., CN=EPSON3EA18A
Thumbprint  201B4A53AF65255258D0FE5AC8115E2073A16675
Validity    2009-12-31 19:00 to 2037-12-31 19:00 (local time as reported)
```

So the printer accepts TLS from the first byte and no in-band
`Upgrade: TLS/1.0` handshake is required. That matters beyond convenience: it
means a relay can wrap its upstream stream and go on copying opaque bytes, where
the in-band mechanism would have required the relay to speak HTTP and would have
collided with REQ-PXY-003.

Two properties of that certificate constrain what verification is possible.
Subject equals issuer, so it is self-signed and no chain will ever validate.
The common name is `EPSON3EA18A`, without the `.local` suffix, and the relay
connects by address rather than by name, so hostname verification does not apply
either. Both standard checks are unavailable, which is why a policy had to be
chosen deliberately.

The diagnostic accepted the certificate unconditionally, which is appropriate
for a tool whose only purpose is to discover what the printer presents, and is
not what the product does. What the product does is recorded as the resolution
of open question 8 in the README, which was decided after these measurements,
not alongside them.

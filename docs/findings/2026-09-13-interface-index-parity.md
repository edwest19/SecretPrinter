# Windows did not number the address families differently, and never had

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-13. Reviewed by a human before merge.*

**Status: measured.** Ten adapters across two machines. No code behaviour
changes as a result; seven comments in four files do.

## The claim that was being carried

Since 2026-09-06 this project has recorded, in source comments and in two
session handoffs, that Windows assigns a different interface index to the same
adapter in each address family, and that `FIOS-STB-01` was the machine where
this was observed: IPv4 index 13, IPv6 index 15.

That claim was the stated empirical justification for three design decisions:

- `MdnsInterface.Matches` comparing address family as well as index
- `MdnsSocket._byIndex` and `_byAddress` being keyed `(AddressFamily, …)`
- The pending fix to `MdnsResponder._byIndex`, which is keyed on index alone

## What was measured

On 2026-09-13, on both machines, reading exactly what
`SystemInterfaceInventory` reads — `GetIPv4Properties().Index` and
`GetIPv6Properties().Index` for every adapter, up or down.

**DESKTOP-URULEFH** (development machine, dual-homed on both networks):

| Adapter | IPv4 index | IPv6 index | IPv4 address | Status |
| --- | --- | --- | --- | --- |
| Ethernet 2 | 13 | 13 | 192.168.1.234 | Up |
| Wi-Fi | 11 | 11 | 192.168.12.245 | Up |
| Local Area Connection* 1 | 15 | 15 | 169.254.7.186 | Down |
| Local Area Connection* 2 | 5 | 5 | 169.254.191.75 | Down |
| Loopback Pseudo-Interface 1 | 1 | 1 | 127.0.0.1 | Up |

**FIOS-STB-01** (target machine, Windows 10.0.19045.7725, dual-homed):

| Adapter | IPv4 index | IPv6 index | IPv4 address | Status |
| --- | --- | --- | --- | --- |
| Ethernet | 15 | 15 | 192.168.1.161 | Up |
| Wi-Fi | 9 | 9 | 192.168.12.186 | Up |
| Local Area Connection* 1 | 17 | 17 | 169.254.254.130 | Down |
| Local Area Connection* 2 | 14 | 14 | 169.254.65.104 | Down |
| Loopback Pseudo-Interface 1 | 1 | 1 | 127.0.0.1 | Up |

Twenty index readings. Every pair equal. On DESKTOP-URULEFH the figures were
cross-checked against `Get-NetIPInterface`, which reported the same value for
both the IPv4 and the IPv6 row of every adapter. That cross-check was not run on
FIOS-STB-01.

**FIOS-STB-01 has no adapter at index 13, in either family.**

## Where 13 and 15 came from

Both numbers are real. Their pairing is not.

**15 is correct, and is reconfirmed above.**
`docs/findings/2026-09-06-ipv6-mdns-transport.md` records, in its environment
table, `FIOS-STB-01`, interface `Ethernet`, **IPv6 index 15**. Today that
adapter still reports index 15, in both families. Its address has moved from
`192.168.1.98` to `192.168.1.161` since — a DHCP lease change, with the index
unaffected. That finding recorded one index, labelled it with its family, named
its machine, and claimed nothing further. It is accurate.

**13 is correct and belongs to the other machine.** It appears in
`docs/findings/2026-09-01-port-5353-sharing.md` and
`docs/findings/2026-09-02-ios-accepts-advertisement.md`, both run against
`192.168.1.234` — DESKTOP-URULEFH. Neither document names the machine it was run
on. Neither states an IPv6 index at all.

So the divergence was manufactured between documents, not observed within one:
an IPv4 index from DESKTOP-URULEFH and an IPv6 index from FIOS-STB-01 were
placed in a single row, attributed to FIOS-STB-01 because that is the name the
IPv6 figure carried, and read as evidence that the two families are numbered
differently. **No source document ever claimed that.** The IPv4 index of
FIOS-STB-01 was, until today, unmeasured and unrecorded.

## What is actually true about Windows

Unknown, and deliberately left unknown here.

.NET surfaces the two values through separate properties,
`IPv4InterfaceProperties.Index` and `IPv6InterfaceProperties.Index`. Microsoft's
reference documents each independently — the first as the index of the interface
associated with the IPv4 address, the second as the index associated with the
IPv6 address — and states no relationship between them. It does not promise they
are equal and it does not promise they differ.

This document therefore claims only what was measured: on these two machines, in
this configuration, on these builds, the two values were equal for every
adapter. It does not claim the platform guarantees that, and no code in this
repository may assume it.

## What this changes

**No code.** Keying on `(family, index)` is correct whether or not the two
values coincide, because the API exposes two values and guarantees nothing about
their relationship. Comparing indexes across families would rely on an
undocumented property of the platform, which this project does not do. The
pending `MdnsResponder` fix proceeds unchanged.

**Seven comments, in four files**, each stating the divergence as a fact about
Windows:

- `src/SecretPrinter.Mdns/InterfaceInventory.cs` — the `IPv6Index` parameter
  documentation
- `src/SecretPrinter.Mdns/MdnsInterface.cs` — the `Index` parameter
  documentation, and the `Matches` remarks, which said an index-only comparison
  would "be wrong on the machine this service is for". That is the reverse of
  the truth: on both machines this service runs on, the values are equal, which
  is what makes an index-only comparison dangerous rather than obviously broken.
- `src/SecretPrinter.Mdns/MdnsSocket.cs` — the `_byIndex` field comment, the
  `Interfaces` property remarks, and the IPv6 multicast membership comment
- `src/SecretPrinter.Responder/MdnsResponder.cs` — the constructor comment

The replacement wording says that the platform exposes an index per family and
guarantees no relationship between them, notes that they were measured equal
here, and cites this document. It does not say they are equal in general, which
would generalise two machines into a platform guarantee — the same mistake in
the opposite direction.

`README.md` and `docs/verification.md` do not contain the claim. It reached
source comments and handoff documents only.

## What this costs

REQ-ADV-020 requires the arrival interface of an IPv6 query to come from
`IPV6_PKTINFO`. The planned measurement for it splits into two claims:

- **That the platform reports a usable, non-zero index at all.** Measurable on
  either machine. Still to be done, and it remains the first thing to do,
  because a negative result changes the attribution design entirely.
- **That the index reported is the IPv6 numbering rather than the IPv4
  numbering.** Not measurable on any hardware available to this project. Where
  the two values are equal, the two hypotheses predict identical observations,
  and no experiment distinguishes them.

The second claim must not be asserted anywhere — not in a comment, not in
`docs/verification.md`, not in the justification text of a `[Requirement]`
marker. What can honestly be said is that the reported index matched the
adapter's index, on machines where the families are numbered alike.

Constructing a machine where they diverge — a Hyper-V virtual switch, a KM-TEST
loopback adapter — was considered and rejected. Since the platform documents no
relationship in either direction, a forced divergence on an artificial adapter
would say nothing about the machines this service runs on.

## How this survived

The two source findings were written by Claude (Opus 4.5) and Claude (Opus 5)
respectively, and each is accurate. The splice, and its propagation into source
comments and two handoff documents, was Claude (Opus 5) across the 2026-09-06
sessions. At no point did a human assert it.

It survived because it was load-bearing in a comfortable direction. It justified
a more defensive design, so nobody had cause to doubt it, and each restatement
made the next one look better attested. A claim that argues for extra care
attracts less scrutiny than one that argues for less.

Two practices would have caught it, and both are nearly free:

- **A finding names the machine it was measured on.** The 2026-09-01 and
  2026-09-02 documents do not. That absence is what allowed a number from one
  machine to be filed under another's name three sessions later. The 2026-09-06
  document does name its machine, and its figure survived contact with
  measurement intact.
- **A number quoted from an earlier document is re-read in that document, not
  recalled from a handoff.** Both handoffs restated 13/15 from the previous
  handoff. Neither opened either source finding. Doing so would have shown
  immediately that no single document contained both numbers.

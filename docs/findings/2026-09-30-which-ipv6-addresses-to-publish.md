# Which IPv6 addresses to publish

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-30. Reviewed by a human before merge.*

**Status: decided, not yet built. Measured on FIOS-STB-01 on 2026-09-30: its
client-side adapter holds seven IPv6 addresses, one link-local, one public and
five temporary. Edwin decided the same day that the service publishes, and the
relay listens on, the client interface's link-local address only. No code has
changed. `REQ-ADV-021` is rewritten in the change that meets it.**

*(Status updated 2026-09-30 by Claude, Claude Opus 5.5: built, in the changes
that followed this finding the same day. The service publishes an `AAAA` record
for each preferred link-local address of each client interface, the relay
listens on exactly the published addresses, and `REQ-ADV-021` is rewritten to
say so. On what happens when an address changes, Edwin chose that addresses
are read once at startup and that the service stops, to be restarted, if a
published address is gone when the listeners next open; following changes, as
RFC 6762 §8.4 asks, is left for later. Not yet verified on hardware: which
address an iPhone connects to is README open question 11.)*

*(Status updated 2026-10-01 by Claude, Claude Opus 5.5: verified on
FIOS-STB-01 running `2c93864`. An iPhone connected to the link-local address
and printed two pages over it; see the
[2026-10-01 finding](2026-10-01-an-iphone-printed-over-ipv6-link-local.md).)*

No address appears in this finding. The commands below print each address's
kind and properties, never the address. The global addresses are household
values and stay out of the repository.

## The question

`REQ-ADV-021` forbids an `AAAA` record for the service's host name while the
relay accepts IPv4 connections only. Step 2 of the order decided on 2026-09-28
([finding](2026-09-28-the-service-claims-its-names-without-probing.md)) is for
the relay to listen on IPv6 as well and for the service to publish `AAAA`.
Which IPv6 addresses to listen on and publish had not been established.

## What the RFCs say

Read from the RFC Editor's texts, paraphrased:

- **RFC 6762 §6.2.** A responder sending its own address records must include
  every address valid on the interface it sends on, and none that is not. Its
  example is an interface with both a link-local and a routable IPv6 address:
  both are included, so each querier can choose. It should also put the other
  family's address records in the Additional section.
- **RFC 6762 §8.4.** When any of a host's addresses change, it must announce
  again.
- **RFC 4862 §1.** A deprecated address's use is discouraged but not
  forbidden; new communication, such as opening a TCP connection, should use a
  preferred address when possible. Microsoft's description of IPv6
  autoconfiguration gives the valid state as covering both preferred and
  deprecated addresses, so read literally, RFC 6762 §6.2 includes deprecated
  addresses.
- **RFC 8981 §2.2 and §3.1.** A host that is also a server keeps a stable
  address, associated with its name, to accept incoming connections, and uses
  temporary addresses when it starts connections itself. Deprecated addresses
  are not used to start new connections.
- **RFC 8981 §3.5.** In normal operation, at most one temporary address per
  prefix is not deprecated at a time.
- **RFC 4291 §2.1.** Every interface is required to have at least one
  link-local unicast address.

## What the client interface holds

On FIOS-STB-01 (Windows 10 22H2, running `017e5a8`), on 2026-09-30, the
client-side adapter `Ethernet`, which holds 192.168.1.161, was read three ways.
Each command is read-only and ran without elevation.

First, `Get-NetIPAddress`, with each address replaced by its kind: link-local
for fe80::/10, unique local for fc00::/7, global unicast for 2000::/3.

```
(Get-NetIPAddress -InterfaceAlias 'Ethernet' -AddressFamily IPv4).IPAddress; Get-NetIPAddress -InterfaceAlias 'Ethernet' -AddressFamily IPv6 | Select-Object @{n='Kind';e={ if ($_.IPAddress -match '^fe[89ab]') {'link-local'} elseif ($_.IPAddress -match '^f[cd]') {'unique local'} elseif ($_.IPAddress -match '^[23]') {'global unicast'} else {'other'} }}, PrefixLength, PrefixOrigin, SuffixOrigin, AddressState, ValidLifetime, PreferredLifetime | Format-Table -AutoSize
```

It listed seven addresses. One was link-local: /64, prefix origin
`WellKnown`, `Preferred`, with infinite lifetimes. Six were global unicast,
from router advertisements. Of those, one was /64 with suffix origin `Link`,
`Preferred`; five were /128 with suffix origin `Random`, one `Preferred` and
four `Deprecated`. Every global address had 1h56m14s of valid lifetime left. A
later read showed 1h58m1s, so the lifetimes had been renewed between the two
reads.

That output does not say which addresses are temporary. Windows' own label
comes from `netsh interface ipv6 show addresses`. For a named interface, netsh
prints one block per address, each with an `Address Type` line. This command
showed that layout, with every address replaced before anything printed:

```
netsh interface ipv6 show addresses interface="Ethernet" | ForEach-Object { $_ -replace '[0-9a-fA-F]{0,4}(:[0-9a-fA-F]{0,4}){2,7}(%\d+)?', '<address>' }
```

The same addresses were then read through
`System.Net.NetworkInformation.NetworkInterface`, the API the service's
interface inventory uses. They were joined to netsh's labels on the address
inside the command, so no address was printed:

```
$t = @{}; $a = $null; netsh interface ipv6 show addresses interface="Ethernet" | ForEach-Object { if ($_ -match '^Address (\S+) Parameters') { $a = $Matches[1] -replace '%\d+$','' } elseif ($_ -match '^Address Type\s+:\s+(\S+)') { $t[$a] = $Matches[1] } }; "netsh addresses: $($t.Count)"; ([System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() | Where-Object Name -eq 'Ethernet').GetIPProperties().UnicastAddresses | Where-Object { $_.Address.AddressFamily -eq 'InterNetworkV6' } | Select-Object @{n='NetshType';e={ $t[($_.Address.ToString() -replace '%\d+$','')] }}, PrefixLength, PrefixOrigin, SuffixOrigin, DuplicateAddressDetectionState, IsDnsEligible | Format-Table -AutoSize
```

| netsh `Address Type` | Count | Prefix length | Prefix origin | Suffix origin | DAD state | `IsDnsEligible` |
| --- | --- | --- | --- | --- | --- | --- |
| `Public` | 1 | 64 | `RouterAdvertisement` | `LinkLayerAddress` | `Preferred` | `True` |
| `Temporary` | 1 | 128 | `RouterAdvertisement` | `Random` | `Preferred` | `False` |
| `Temporary` | 4 | 128 | `RouterAdvertisement` | `Random` | `Deprecated` | `False` |
| `Other` | 1 | 64 | `WellKnown` | `LinkLayerAddress` | `Preferred` | `False` |

All seven joined; none was left without a label. One temporary address not
deprecated and older ones deprecated is what RFC 8981 §3.5 describes.

Two limits on this. Windows PowerShell runs on .NET Framework, not .NET 10.
Both read the same Windows adapter data, but this is not a measurement under
.NET 10. And it is one machine. No documented .NET property says "temporary",
and on a machine where Windows randomises the stable identifier, the public
address may report a different suffix origin. That was not measured.

## The choice

Three options were weighed:

- **A. Every valid address.** This is RFC 6762 §6.2 read literally: all seven
  here, including four deprecated addresses and five temporary ones. The set
  changes each time Windows makes a new temporary address and whenever the
  router's advertisements change. The relay would also listen on addresses
  reachable from beyond the link, if the router let inbound IPv6 through,
  which is not established.
- **B. The link-local address and the preferred public address.** This is RFC
  8981's model of a server. It needs the code to tell temporary from public,
  and no documented property does. On this machine prefix length and
  `IsDnsEligible` separate them, and that is all that is known. The global
  prefix's lifetime would still have to be followed.
- **C. The link-local address only.** Every interface has one (RFC 4291 §2.1),
  so every IPv6 querier on the link has an address to reach it from. It is
  recognised by its prefix, fe80::/10, which is a definition rather than an
  observed correlation. Its lifetime here is infinite. A link-local address
  cannot be reached from beyond the link, so the relay would listen on nothing
  that can.

**Edwin chose C on 2026-09-30, on Claude's recommendation.** It departs furthest
from RFC 6762 §6.2, which names both link-local and routable addresses so that
each querier can choose. The rewritten `REQ-ADV-021` will state the departure
and the reasons. Before this change the service already departs from §6.2: it
publishes no `AAAA` at all, while this interface holds seven IPv6 addresses.

## Measured for the relay's check

`IppRelay` enforces `REQ-SEC-012` with `IPNetwork.Contains`, one network per
client interface. Under option C the IPv6 network is fe80::/10, and every
client address that arrives on a link-local listener carries a scope ID. Run
in Claude's container on .NET 10.0.12:

- `Contains` ignores scope IDs. fe80::/10 contains `fe80::1%17`, and a network
  built as `fe80::%17/10` contains `fe80::1%18` and `fe80::1` with no scope.
- fe80::/10 contains `febf::1`, the last of the range. It does not contain
  `fec0::1`, a global address, an IPv4-mapped address or an IPv4 address.
- `new IPNetwork(fe80::1, 10)` does not refuse the host bits; it clears them.
- `192.168.1.0/24` contains `::ffff:192.168.1.5`, the IPv4-mapped form. The
  IPv4 listener's socket is IPv4 only, so its connections arrive with IPv4
  addresses and this does not arise today.

So for link-local connections the prefix check cannot tie a connection to the
client interface. The listener being bound to the link-local address with the
interface's scope is what does that. The relay will also compare each
connection's scope ID with the client interface's IPv6 index, so that a wrong
bind is refused and logged rather than trusted. The container runs Linux; the
tests that depend on this will also run on Windows.

## What building C involves

This is what completes the item. The code does none of it yet.

- The interface inventory reads each client adapter's link-local addresses.
  Today it reads no IPv6 address, only the IPv6 index.
- The advertisement carries an `AAAA` record for every link-local address the
  client interface holds in the preferred state, beside the `A` record, and no
  other IPv6 address. The same list feeds the listeners, so what is published
  and what is listened on cannot differ. That difference is the danger the
  present `REQ-ADV-021` guards against.
- The relay listens on each of those addresses, bound with the interface's
  scope, as well as on the IPv4 address. `TcpConnectionListener` refuses both
  wildcard addresses itself. Today nothing stops it being handed one, although
  its comment says it binds to one specific address, never the wildcard.
- For IPv6, `REQ-SEC-012` permits fe80::/10 arriving with the client
  interface's scope, and nothing else.
- Probes, announcements, answers and goodbyes carry the `AAAA` records. An
  answer carrying the `A` record also carries the `AAAA` records in the
  Additional section, and the reverse (RFC 6762 §6.2).
- What the service does if the link-local address changes while it runs is
  decided while building it, and the README says what it is.
- A capture on FIOS-STB-01's client adapter shows the `AAAA` answered, which
  address the iPhone connects to, and a page printed.

The `NSEC` (`REQ-ADV-022`) comes after this, then the removal of
`SecretPrinter.Respond6`, in the order decided on 2026-09-28.

*(Status note, 2026-10-02, by Claude, Claude Opus 5.5: the `NSEC` was built in
`64e8fc2` and verified on hardware on 2026-10-02; see
[the finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md).
`SecretPrinter.Respond6` has not been removed yet.)*

## Not established

- Whether iOS connects to a link-local address it received in an `AAAA` record
  over mDNS and, offered both an `A` and an `AAAA` record, which it uses.
- Whether Windows reports the scope ID on connections accepted by a listener
  bound to a link-local address.
- Whether the firewall rules in [`operating.md`](../operating.md) let IPv6
  connections to 631 through. They name no address or family, so they are
  expected to; not measured.
- Whether the pattern in the table holds on other Windows machines. Option C
  does not depend on it.

## Prediction record

Claude predicted:

- For `Get-NetIPAddress`: the adapter holding 192.168.1.161; at least one
  link-local address, /64, `WellKnown`, `Preferred`; at least one global /64
  from router advertisements. **Held.** Not predicted: the five /128 `Random`
  addresses.
- For a first join: `netsh addresses: 7`. **Did not hold: 0.** Its parser
  assumed a table layout that Claude had not seen netsh print on this machine.
  For a named interface, netsh prints one block per address.
- For netsh's output with every address replaced: either an error, or one block
  per address with a line naming the type. **Held.**
- For the corrected join: seven rows, none unlabelled; the /64 from router
  advertisements `Public`, the five /128 `Temporary`, the `WellKnown` /64
  `Other`. **Held,** including `Other`, which Claude had not been sure of.

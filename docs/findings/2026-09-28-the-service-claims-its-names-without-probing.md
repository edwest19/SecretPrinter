# The service claims its names without probing

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-28. Reviewed by a human before merge.*

**Status: open. Found by reading the code at `e900a88` against the text of
RFC 6762, not on hardware. Nothing has been changed yet. Edwin decided on
2026-09-28 to build probing first, before anything that depends on owning the
names; the order is under "What was decided" below.**

*(Status updated 2026-09-29 by Claude, Claude Opus 5.5: step 1 is done.
Probing and conflict handling were built in `bc30991`, `ba54835` and `8f3ea90`
(`REQ-ADV-023`, `REQ-ADV-024`) and run on FIOS-STB-01 on 2026-09-29
([the finding](2026-09-29-probing-on-hardware.md)). The question under "Not
established" of what to do when a probe finds a conflict was decided by Edwin
on 2026-09-28: withdraw and say so, until restarted, rather than rename. Steps
2 and 3 are still to do.)*

*(Status updated 2026-09-30 by Claude, Claude Opus 5.5: for step 2, Edwin
decided which IPv6 addresses the relay listens on and the service publishes:
the client interface's link-local address only, not every address valid on the
interface as step 2 below says. The measurements and the reasons are in the
[2026-09-30 finding](2026-09-30-which-ipv6-addresses-to-publish.md). Step 2 is
not yet built.)*

## What RFC 6762 asks for

Read from the RFC Editor's text of RFC 6762, paraphrased here:

- **§8.1, Probing.** Before a responder treats any of its records as unique on
  the link, it must first send queries for them and listen for a reply, to see
  whether another device already uses the name. The probes should ask for type
  `ANY`, so one probe covers every record type at that name.
- **§8.3 and §10.2, the cache-flush bit.** Records that have been verified
  unique are announced with the cache-flush bit set. That bit tells every
  listener that this is the whole truth for that name and type, and that older
  records from anyone else are to be discarded.
- **§9, Conflict Resolution.** A responder that hears another device answer
  with different data for one of its unique records must go back to probing,
  and the loser stops using the name.
- **§6.1, Negative Responses.** A responder may deny that a record exists (with
  an `NSEC`) only for a name it legitimately owns: one it knows to be unique
  beforehand, or one it claimed by probing for type `ANY`.

## What the service does

- `AdvertisementBuilder.Build` sets the cache-flush bit on the service
  instance's `SRV` and `TXT` records and on the host's `A` record. That asserts
  the service owns those names outright.
- Nothing sends a probe. A case-insensitive search of the tracked files under
  `src/` for "prob", leaving out "probably" and "problem", found four lines.
  Two refer to the `SecretPrinter.Probe` tool, which reads what a printer
  advertises. Two refer to the printer answering measurement probes from the
  service's machine. None is RFC 6762 probing.
- `MdnsResponder.HandleAsync` returns as soon as it sees that a datagram is a
  response ("Somebody else's answer, or our own multicast looping back"). A
  device answering for the same names would never be noticed, so §9 is not
  implemented either.
- The README has no requirement for probing and does not say that the service
  skips it.

## It was known, and not carried forward

The first measurement of the design,
[2026-09-02](2026-09-02-ios-accepts-advertisement.md), recorded that the
experiment tool did not probe and said "The service must probe", so that the
omission would not be mistaken for the intended design.
`tools/SecretPrinter.Respond/README.md` says the same. Neither statement became
a requirement, and the service was built without probing. That is the gap this
finding records.

## What it affects

- **The cache-flush bit claims an ownership that was never established.** If
  another device on the client network used the same host name or instance
  name, each would flush the other's records from clients' caches, and neither
  would notice. No such device has been seen; this is what the protocol would
  do, not something measured.
- **`REQ-ADV-022` cannot be met the way §6.1 permits.** The `NSEC` it requires
  denies that a record exists, and §6.1 allows that only for a name the service
  owns. Until it probes, it does not.

  *(Status note, 2026-10-02, by Claude, Claude Opus 5.5: the service now
  probes, `REQ-ADV-023`, and since `64e8fc2` it sends the `NSEC`. Step 3 below
  is done, and was verified on hardware on 2026-10-02; see
  [the finding](2026-10-02-an-iphone-printed-after-its-host-name-question-was-answered-with-an-nsec.md).)*

## What was decided

Edwin, 2026-09-28, in this order:

1. **Probing first,** as a new requirement in the README, with tests, before
   the `NSEC`.
2. **Then IPv6 on the relay, with `AAAA` published.** The relay listens only on
   the client interface's IPv4 address today. It was built that way before IPv6
   mDNS came up, and on 2026-09-06 it was left that way to keep the IPv6 change
   small ([2026-09-06](2026-09-06-ipv6-mdns-transport.md)). RFC 6762 §6.2 asks a
   responder to include every address valid on the interface, so the relay will
   also listen on the client interface's IPv6 addresses and the service will
   publish them. `REQ-ADV-021`, which forbids an `AAAA` while the relay listens
   on IPv4 only, will be rewritten at that step.
3. **Then the `NSEC`,** `REQ-ADV-022`, reworded at that point to fit a host that
   has both address types.

## Not established

- Whether any other device on this client network uses these names. Nothing
  has been measured.
- What the service should do when a probe finds a conflict. §9 recommends
  choosing a new name and probing again. Not yet decided.

# A test read the real network adapters, and CI has been failing on it

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-09-22. Reviewed by a human before merge.*

**Status: measured, fixed in the test. `PrinterResolverTests.Silence_fails_with_a_reason`
passed or failed depending on the live state of one network adapter on the
machine running it. It failed on the dev box today because that machine's Wi-Fi
was disconnected, and it fails on the CI runner, which has no such adapter. The
product code was not at fault; the test did not isolate what it tested.**

## What happened

On 2026-09-22 the dev box ran the suites for an unrelated change and one test
failed:

```
FAIL  Silence produces a specific failure, not a guess
      Expected true: the message should say what failed
```

Nothing in `SecretPrinter.Resolution` or its tests had changed.

## Why

Since `07aab4f` (2026-09-19), when a lookup times out, `PrinterResolver`
inspects the printer-side adapter before it words the failure, so that it does
not blame the printer for a fault on this machine. It reads the adapters from
the `inventory` passed to its constructor, and when none is passed, from
`SystemInterfaceInventory.Instance`: the adapters actually present.

Every resolver in `PrinterResolverTests.cs` was constructed without an
inventory. For `Silence_fails_with_a_reason`, the one test there that checks
the wording of a timeout, that meant:

- If the machine had an adapter at index 11 that was up and held
  `192.168.12.245` (the test's `PrinterNic`), nothing wrong was found locally,
  the message said the printer "did not answer", and the test passed.
- Otherwise the message said the lookup "could not be asked on" the interface,
  without "did not answer", and the test failed.

The other timeouts in that file only assert that an exception is thrown, so
they passed either way.

## What was measured

- **The dev box, today**, by `Get-NetAdapter -InterfaceIndex 11` and
  `Get-NetIPAddress -InterfaceIndex 11 -AddressFamily IPv4`: `Wi-Fi`,
  `Disconnected`, holding only `169.254.34.71`, `Tentative`. That is the
  failing branch.
- **CI.** Edwin reports that the CI run fails every time, and pasted the log of
  one run: the same test fails on the runner with the same message, and the
  job ends at the test step. When the first failing run was, and whether runs
  before `07aab4f` passed this step, was not checked.

That the test passed on the dev box before today follows from the code: it can
pass only on the first branch above. The dev box's Wi-Fi is on the printer
network when it is connected. Its address at the time of those runs was not
recorded.

## Why nobody noticed

- **CI is red by design.** README Section 11 says CI fails until every
  requirement is covered, and six binding gaps remain, so the SpecCheck step
  fails every run. The failing test changed nothing anyone could see: the run
  was red before it and red after. A CI that is always red cannot report a new
  failure, and this is what that costs.
- **Since `07aab4f` the SpecCheck step has not run on CI at all**, because the
  test step throws first. `REQ-DIST-003` says CI runs the specification
  checker; the workflow is written to, and its evidence row in
  `docs/verification.md` describes the workflow accurately, but on every run
  since then it has not got that far.
- **The test counts recorded in handoffs came from the dev box.** The
  2026-09-22 handoff reports "224 tests, all passing" measured there. It does
  not mention CI, and after each push the check made was that GitHub held the
  delivered files byte for byte, not that CI passed.

## The fix

Every `PrinterResolver` constructed in `PrinterResolverTests.cs` is now given a
fake inventory holding one adapter: `Wi-Fi`, index 11, up, holding
`192.168.12.245` as preferred. That is the state the test was written to assume,
now stated rather than borrowed from the machine. The file's header claimed
that none of its tests needed a network; it now also says they need no
particular adapter state, which is true once the inventory is supplied.

`PrinterInterfaceDiagnosisTests.cs` already supplied an inventory to every
resolver it built, and tests the local-fault branches directly. No test was
added or removed, and the product code did not change.

## Not done here

- **Whether other tests read this machine's state.** Only this file was
  changed. A search for other constructions that fall back to
  `SystemInterfaceInventory` or other live system state was not made.
- **What CI should do while requirements are uncovered.** A red build is how
  the project states that gaps remain, and that is Edwin's design. This finding
  records what it cost, and does not change it.

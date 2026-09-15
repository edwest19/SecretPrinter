# The example configuration leaves the fingerprint empty, but not the UUID

*Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
West, 2026-09-15. Reviewed by a human before merge.*

**Status: a considered inconsistency, recorded rather than resolved. Nothing in
the commit that carries this document changes how the UUID is handled.**

## What the example prints

`SecretPrinter.Service.exe --print-example-config` writes out
`ConfigurationLoader.ExampleJson`. Two of its required settings are the operator's
own values, and they are handled differently:

- `printerCertificateSha256` is printed as an empty string. REQ-CFG-007 refuses
  it at startup, names the setting, and points to the measuring step in
  `docs/operating.md`.
- `advertise.uuid` is printed as a literal value,
  `b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94`, which loads.

## Why the fingerprint is empty

Edwin West decided this on 2026-09-15, choosing between two options.

A fingerprint that looked real, whether the development printer's or an obvious
placeholder, would load. An operator who left it in place would start the
service, see the printer advertised, and learn of the mistake only when a job was
attempted and the certificate did not match. An empty value is caught at startup,
alongside every other configuration problem, which is the point of REQ-CFG-003.

The cost is that the example no longer loads as printed. The configuration tests
therefore start from `ValidExampleJson`, which fills in a synthetic fingerprint
belonging to no real device, and one test,
`Printed_example_is_refused_until_measured`, checks the example exactly as
printed.

## Why the UUID is different

The literal UUID predates this decision. Step 3 of `docs/operating.md` says the
service will not invent a UUID because "a generated default would mean every
installation advertising the same identity". An example that supplies one fixed
UUID has that same effect for any operator who leaves it in place, and nothing
refuses it.

That is not changed here. Changing it would alter existing behaviour and existing
tests that have nothing to do with REQ-CFG-007. Whether the UUID should follow the
fingerprint is Edwin West's decision, and it belongs in its own commit if made.

## A related claim that is not true

The documentation comment on `ExampleJson` says the example is "reproduced in the
documentation so the two cannot disagree". No document in the repository
reproduces it. A search of every `.md` file for `clientInterfaces` and
`printerInterface` finds nothing. The documentation instead tells the operator to
run `--print-example-config`, which does keep the two from disagreeing, but not in
the way the comment says. The comment is left as it is here, and recorded.

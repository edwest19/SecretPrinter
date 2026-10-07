# The example configuration handed every user the same UUID

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-07. Reviewed by a human before merge.*

**Status: corrected in the code in the commit that carries this finding, and
tested. `SecretPrinter.Service.exe --print-example-config` printed a fixed UUID
that loaded, so every installation whose operator left it in advertised the same
identity, which is what `docs/operating.md` says the service avoids. That was
recorded on 2026-09-15 as a decision for Edwin West and never put to him. It was
put to him on 2026-10-07 and he decided that the example prints the UUID empty,
as it prints the certificate fingerprint. A configuration that already holds
the old value goes on loading. The requirement about the UUID, `REQ-ADV-005`,
said the service generates one, which it never has; that is corrected too. The
changed steps have not been walked on a machine.**

## What was so

- **The example.** `--print-example-config` writes out
  `ConfigurationLoader.ExampleJson`. Its `advertise.uuid` was
  `b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94`, a value that loads.
- **The document.** Step 3 of `docs/operating.md` says: "Configuration requires
  one and the service will not invent it, because a generated default would
  mean every installation advertising the same identity".
- **Together.** An operator who skipped step 3 got that same identity from the
  example, and nothing refused it.
- **The record.**
  [The finding of 2026-09-15](2026-09-15-example-config-leaves-fingerprint-empty.md)
  says exactly this, and that whether the UUID should follow the fingerprint
  "is Edwin West's decision". The fingerprint has been printed empty, and
  refused until measured, since that day (`REQ-CFG-007`). The question about
  the UUID was carried in Claude's working notes from session to session and
  not asked.

What sharing a UUID costs has not been measured. Two proxies on one client
network with the same UUID is the case the sentence in step 3 is about; it has
not been tried. Whether anything else follows from many households advertising
one known value is not established.

## What was decided

Claude put two courses to Edwin West on 2026-10-07 and recommended the first.

- **A. The example prints the UUID empty.** The program then does what the
  document says. A configuration that already holds the old value keeps
  loading. The cost is that a new user who skips step 3 is refused for two
  settings instead of one.
- **B. Leave the example, and correct step 3** to say that its value loads and
  should be replaced.

He chose A.

A third course was not put to him: to refuse the old value by name. It would
stop an installed service from starting after an update until its
configuration was edited, and Claude did not think that worth it.

## What changed

- **`ExampleJson`** prints `"uuid": ""`.
- **`RequiredGuid`,** which checks the setting. An empty or blank entry is now
  reported as the missing setting is: "required, and deliberately has no
  default", with the PowerShell command that generates one. Before, an empty
  entry was refused as "'' is not a usable UUID", with no word on how to get
  one. A malformed UUID, and the UUID of all zeros, are still refused as not
  usable, and that message now carries the command too.
- **The remark on `ExampleJson`.** It said the example is "reproduced in the
  documentation so the two cannot disagree". No document reproduces it; the
  finding of 2026-09-15 recorded that and left it. It now says what is so: the
  documentation tells the operator to run `--print-example-config`.
- **`docs/operating.md`,** step 3 and one paragraph under Configuring.
- **`REQ-ADV-005`** in the README; see below.

No requirement was added. `REQ-CFG-001` already says the service applies no
default that changes what is advertised, and the new test of the example is
marked for it.

## `REQ-ADV-005` said the service generates its UUID

The row read: "It generates and persists its own UUID, distinct per advertised
service." The service has never generated a UUID. It requires one in the
configuration and refuses to start without it, which is the opposite, and
`REQ-CFG-001` is the reason. The row now says so, with a dated note.

The reading of the README on 2026-10-07
([finding](2026-10-07-the-readme-was-read-against-the-code.md)) did not catch
it. The row's two markers speak of rejecting the printer's UUID and of
carrying the proxy's own, and neither says anything is generated. It is the
second false statement found since that the reading missed;
[the other](2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md)
was in `REQ-ADV-021`.

## The tests, and each one broken on purpose

Written first, and run against the unchanged code: 23 of the 27 tests in the
configuration suite failed, the two new ones among them. Most of the others
failed because the helper that fills in the example found no empty UUID entry
to fill, which is what it is written to do.

- **New:** "The example configuration as printed has no UUID, and is refused
  until one is generated" (`REQ-CFG-001`).
- **New:** "A UUID that is blank, malformed or all zeros is refused"
  (`REQ-CFG-003`).
- **Changed:** "The example configuration as printed is refused until the
  fingerprint is measured" now expects two problems, where it expected one.
- **Changed:** the helper `ValidExampleJson` fills in a UUID as well as a
  fingerprint. The UUID it uses is the old example value, which belongs to no
  installation in particular.

Each mutation changed one place in `ConfigurationLoader.cs`; the suite was
built and run, and the file restored.

| Mutation | Tests that failed |
| --- | --- |
| The example prints the old value again | 23, as above |
| An empty entry is not treated as a missing one | "The example configuration as printed has no UUID, and is refused until one is generated" |
| The message for an unusable value does not say how to make one | "A UUID that is blank, malformed or all zeros is refused" |
| The UUID of all zeros is accepted | the same |
| The message for a missing value does not say how to make one | those two, and the older "A missing UUID is refused, with instructions" |

## Where it was measured

**In Claude's workspace:** Linux, .NET SDK 10.0.112.

- The configuration suite: 27 passed, where it was 25.
- The other six as before: 28, 34 with 9 skipped, 61, 41, 123 and 105.
- SpecCheck, given the service build made there: 99 requirements, 23
  assemblies, 18 evidence rows, 434 test records where there were 432, and the
  same two gaps as before, for the requirements whose tests skip there. The
  coverage matrix changes in two rows: `REQ-CFG-001` and `REQ-CFG-003` each
  gain a test.
- Every project built with no warning.

**On the development machine,** Edwin ran every suite and SpecCheck before
committing. Those figures are in the commit message.

## What this does not show

- **The program run on Windows.** Nobody has yet run `--print-example-config`
  from this commit, or started the service on its output. The install from
  nothing that is planned for a second machine will do both.
- **Which installations hold the old value.** The two machines this project
  has installed on are not described here. The walk of 2026-10-06 generated
  its own
  ([finding](2026-10-06-a-signed-release-folder-was-configured-by-hand-and-printed.md)).
- **What a client makes of a UUID that changes.** `docs/operating.md` now
  tells an operator who holds the old value how to replace it. What a device
  that has already printed through the proxy then does has not been measured.
- **Two proxies with one UUID on one network.** Not tried.

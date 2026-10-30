# Changelog

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-04. Reviewed by a human before merge.*

Every release of SecretPrinter is a tagged commit with an entry here
(`REQ-DIST-006`). Versions follow [Semantic Versioning](https://semver.org/).

**How this file is used.** `.github/workflows/release.yml` refuses a tag unless
this file has exactly one heading of the form `## [version] - YYYY-MM-DD` for
it, and unless `<Version>` in `Directory.Build.props` says the same version.
Everything under that heading, up to the next one, becomes the release notes.
The workflow then adds a section of its own after it, "Built with", naming the
.NET SDK that built the release and the .NET runtime its folder carries, which
an entry written before the build cannot know. (Sentence added 2026-10-20 by
Claude, Claude Opus 5.5. It ended "No release has carried that section yet."
until later that day, when `0.4.0` was the first to carry it.)

**`0.1.0` is the first release.** It was published on 2026-10-08. `0.2.0`,
published on 2026-10-11, is the second. (Sentence added 2026-10-11 by Claude,
Claude Opus 5.5, in the commit that sets the version to 0.2.0, as "`0.2.0` is
the second version meant as a release."; made exact the same day, once 0.2.0
had been published.) `0.3.0`, published on 2026-10-15, is the third.
(Sentence added 2026-10-15 by Claude, Claude Opus 5.5, in the commit that sets
the version to 0.3.0, as "`0.3.0` is the third version meant as a release.";
made exact the same day, once 0.3.0 had been published.) `0.4.0`, published
on 2026-10-20, is the fourth. (Sentence added 2026-10-20 by Claude, Claude Opus
5.5, in the commit that sets the version to 0.4.0, as "`0.4.0` is the fourth
version meant as a release."; made exact the same day, once 0.4.0 had been
published.) `0.5.0`, published on 2026-10-25, is the fifth. (Sentence added
2026-10-25 by Claude, Claude Opus 5.5, in the commit that sets the version to
0.5.0, as "`0.5.0` is the fifth version meant as a release."; made exact the
same day, once 0.5.0 had been published.) `0.6.0` is the sixth version meant
as a release. (Sentence added 2026-10-30 by Claude, Claude Opus 5.5, in the
commit that sets the version to 0.6.0.) Every version
below `0.1.0` is a pre-release or a trial, not a release: `0.1.0-rc.3` was published on
2026-10-07 for testing, and nothing was published for `0.1.0-rc.1` or
`0.1.0-rc.2`. (Rewritten 2026-10-08 by Claude, Claude Opus 5.5, in the commit
that sets the version to 0.1.0. It said "No release has been published yet.",
that `0.1.0-rc.3` was a pre-release for testing and not a release, and that it
was published on 2026-10-07. Later the same day, once 0.1.0 had been
published, "is the first version meant as a release" became "is the first
release", and the sentence saying when it was published was added.)

## [0.6.0] - 2026-10-30

The sixth version of SecretPrinter meant as a release. The service now ignores,
as RFC 6762 section 6 requires, a response sent by unicast or from a port other
than 5353, both when it looks the printer up and when it checks for a conflict
over its own names. No answer about the printer is believed for longer than
120 seconds, whatever TTL it carries. The probe keeps only a reply from port
5353 that carries the identifier of its own question. Nothing in how it is
installed or configured changes.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **A lookup takes only a multicast answer from port 5353** (`REQ-RES-012`).
  Every question the service puts to the printer asks for a multicast answer.
  RFC 6762 section 6 says a querier must silently ignore a unicast response
  that answers no question asking for one, and any response whose source port
  is not 5353. Until now a lookup took any response that arrived on the
  printer-side interface, whatever its destination and source port; `0.1.0` to
  `0.5.0` do so. Now it takes one only when it was sent to the mDNS group, as
  the destination address that IP_PKTINFO reports shows, and came from port
  5353. The printer's records heard between lookups were already held to both
  rules (`REQ-RES-011`). Whether a response this now ignores ever reached the
  service is not known.
- **No answer is believed for longer than 120 seconds** (`REQ-RES-013`). Until
  now an answer about the printer held for as long as its TTL said, however
  long, so a device on the printer-side network that answered one of the
  service's questions with a very long TTL would have had its address reused
  by print jobs, and the printer held reachable, for most of that time before
  the service asked again; `0.1.0` to `0.5.0` do so. Read from the code; not
  seen. Now a lookup's answer holds for the shorter of the TTLs of the SRV and
  A records it is made of, and never more than 120 seconds, the TTL RFC 6762
  section 10 recommends for such records; the records heard between lookups are
  held the same way. The cap is a choice made from that recommendation, not
  something the RFC requires. When it cuts the answer the service logs at
  startup, that line ends `, capped from` and the TTL received, so the cap is
  never shown as what the printer sent. On FIOS-STB-01 the printer's answers
  carried 120 seconds, and nothing was capped.
- **The conflict check takes only a multicast response from port 5353**
  (`REQ-ADV-030`). Before it offers the printer, and at any time after, the
  service listens on the client network for another device answering for the
  names it claims, and withdraws if one does (`REQ-ADV-024`). Until now it read
  every response that arrived there, whatever its port and destination, so a
  response that RFC 6762 section 6 says must be ignored could withdraw the
  service until a restart; `0.1.0` to `0.5.0` do so. Read from the code; not
  seen. Now it reads one only when it was sent to the mDNS group, 224.0.0.251
  or ff02::fb, and came from port 5353. A device on the client network can
  still make the service withdraw by answering for its names; what changes is
  that a response that should have been ignored is no longer read.
- **The probe keeps only a reply from port 5353 to its own question**
  (`REQ-SEC-018`). The probe asks from a port Windows chooses, so a responder
  answers it by unicast (RFC 6762 section 6.7). Until now it kept every DNS
  response that reached that port, from any port and with any identifier, and
  asked its next questions about the names in it; the probe in `0.1.0` to
  `0.5.0` does so. Read from the code; not seen. Now it keeps a reply only when
  it came from port 5353 and carries the identifier of the question it sent in
  that round, and for anything else prints a line saying it was ignored and
  why, ending `(RFC 6762 s6)` or `(RFC 6762 s6, s6.7)`. `docs/operating.md`
  says more.
- **What these checks do not do.** None of them makes an answer trustworthy.
  The service's questions and the probe's go to the mDNS group, so any device
  on that network can see them and answer from port 5353, to the group, as the
  printer would, and mDNS gives no way to tell them apart. A job sent to a
  forged address still has to meet the pinned certificate, and the relay
  abandons it when it does not (`REQ-SEC-013`). The two things the `0.5.0`
  entry listed as still open, no TTL capped and lookups taking unicast
  responses and responses from any port, are what this version changes.
- **Tests.** 27 were added, for 598 in all on the development machine (571 at
  `0.5.0`). Each was written before its change and run against the code
  without it: 21 failed, and the other 6, which check what must not change,
  passed. Responder tests that handed it a response with no destination now
  give it the mDNS group, so that none passes for the wrong reason. Each change
  was also broken on purpose, one way at a time, to see which tests catch it:
  of 16 such changes, every one was caught. The findings list each:
  `docs/findings/2026-10-26-a-lookup-takes-only-a-multicast-response-from-port-5353.md`,
  `docs/findings/2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md`,
  `docs/findings/2026-10-27-the-conflict-check-takes-only-a-multicast-response-from-port-5353.md`
  and
  `docs/findings/2026-10-28-the-probe-keeps-only-a-reply-from-port-5353-to-its-own-question.md`.
- **Updating from `0.1.0`, `0.2.0`, `0.3.0`, `0.4.0` or `0.5.0`:**
  `docs/operating.md`, "Updating an installed service", "From a release".
  Check the new release first, as below. The configuration file, the service
  registration, the firewall rules and the data folder are the same as before.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. In the release notes GitHub
  shows, the section "Built with" after this entry names it, and so does
  `SecretPrinter.Service.runtimeconfig.json` in the folder. On 2026-10-30
  Microsoft's .NET release index on GitHub and its .NET 10 download page both
  gave 10.0.14, of 2026-10-25, with SDK 10.0.403, as the latest .NET 10
  release; `0.5.0` carries 10.0.14. This entry does not say what this build
  will carry; the section "Built with" does. The one package SecretPrinter
  declares, `System.ServiceProcess.ServiceController`, stays at 10.0.11, as in
  `0.5.0`.
- **Where it has run.** The changes since `0.5.0` have run in tests, on the
  development machine, on Windows, and in Claude's workspace, on Linux. The
  three changes to the service have also run in two builds from source, each
  made on the development machine by `publish-release.ps1` with SDK 10.0.403,
  so carrying runtime 10.0.14, not signed, and installed in turn as the
  service on FIOS-STB-01, which runs Windows 10:
  - A build of `de1d171`, carrying `REQ-RES-012` and `REQ-RES-013`, from
    2026-10-26 in place of `0.5.0`, for 30 hours 20 minutes. It lost the
    printer 14 times and found it again each time by one of its own questions
    being answered, was off offer 10.8% of the time, and accepted 27
    connections, none failing. An iPhone's first attempt to print through it
    said it could not contact the printer while every connection was relayed;
    the second printed a page. `README.md`, "Known problems", has an entry on
    such attempts.
  - A build of `0b51fcc`, carrying all three, from 00:58:46Z on 2026-10-28 in
    place of that build. An iPhone printed a page through it at the first
    attempt. Read on 2026-10-30, its first 65 hours 29 minutes had no restart.
    It lost the printer 17 times and found it again each time by one of its
    own questions being answered, never by hearing it unasked, and was off
    offer 1 hour 37 minutes 27 seconds, 2.5% of the time. Each of its 18 probes
    came back clear, and no conflict was logged. It accepted 86 connections
    and refused none: 83 were relayed over TLS 1.2 with the printer's
    certificate matching the pin, and completed; 3, accepted together at
    16:57:21Z on 2026-10-28, failed because the TLS handshake with the printer
    did not complete within 10 seconds, and why is not shown.
  - The probe's change ran from a clone on the development machine, on the
    printer-side network, on 2026-10-28 and 2026-10-29: it kept the printer's
    replies and ignored nothing. It has not run from a release folder or on
    FIOS-STB-01.

  See `docs/findings/2026-10-26-the-build-of-de1d171-was-installed-on-fios-stb-01.md`
  and `docs/findings/2026-10-27-the-build-of-0b51fcc-was-installed-on-fios-stb-01.md`,
  with its status notes of 2026-10-28 and 2026-10-30. Which versions of
  Windows it can run on: `README.md`, open question 10.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it; installing it on FIOS-STB-01, and running its probe there
    from the release folder, are the first.
  - On a network, a response or reply that any of the three checks ignores.
    None was seen to arrive; the service does not log one it ignores, and only
    tests show what happens to one.
  - A TTL longer than 120 seconds capped on a network. This printer gives 120.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.

## [0.5.0] - 2026-10-25

The fifth version of SecretPrinter meant as a release. While it holds the
printer unreachable, the service now takes the printer's own records, heard on
the printer-side network without being asked, as an answer, so a printer that
announces itself as it starts is offered again when it is heard, not at the
next question. A lookup no longer takes a goodbye record as its answer.
Nothing in how it is installed or configured changes.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **The printer is offered again when it is heard** (`REQ-RES-011`). After a
  loss the service asks for the printer again after 1, 2, 4 and 8 seconds and
  so on, doubling up to once an hour (`REQ-RES-008`), and until now it kept
  what arrived from the printer only while one of those questions was waiting
  for an answer. On 2026-10-21 the printer was seen to announce itself eight
  times in about two minutes after it was switched on, as RFC 6762 section 8.3
  describes; `0.4.0` and earlier use such an announcement only if a question
  happens to be waiting. Now, while the printer is held unreachable, its
  records heard on the printer-side interface make it reachable as an answer
  does, when they hold the configured `_ipps._tcp` instance's SRV record and an
  address record for the host that SRV names, each within its TTL, in one
  datagram or several. They are taken only from a response sent to the mDNS
  group from UDP port 5353, as RFC 6762 section 6 requires, and only from its
  Answer and Additional sections. The service logs
  `Printer reachable again: it was heard on the printer-side interface without being asked (an announcement, or its answer to another device).`
  and offers the printer as after any answer. Nothing changes while the
  printer is held reachable, and a goodbye still does not withdraw it. Edwin
  West chose this on 2026-10-22.
- **A goodbye is no longer taken as an answer.** A record with a TTL of 0 says
  that the record is going away (RFC 6762 section 10.1). Until now a lookup
  took an SRV or address record whatever its TTL. Now it does not, and nothing
  heard between lookups is taken with a TTL of 0 either. Found by reading the
  code on 2026-10-22; what earlier versions did with such an answer has not
  been seen on a machine.
- **What this means for a device on the printer network.** One could already
  make the service offer the printer, by answering one of its questions with
  records of its own. It can now do that whenever the printer is held
  unreachable, not only while a question is waiting. A forged address sends
  print jobs where nothing answers, or to a device whose certificate does not
  match the pinned fingerprint, and the relay abandons them (`REQ-SEC-013`);
  the client sees a failed print, as it would with the printer away. At most
  16 address records, one SRV and one TXT are held. Nothing new is sent and no
  new socket is opened. Not changed, and still open: no TTL is capped, so a
  record with a very long TTL, whether it answers a question or is now heard,
  holds the printer reachable without a question until 80% of it has passed;
  and a lookup still takes a unicast response, and a response from any source
  port, which RFC 6762 section 6 says a querier must ignore.
- **Tests.** 35 were added, for 571 in all on the development machine (536 at
  `0.4.0`). One sends a datagram to 224.0.0.251 and reads back its
  destination address. The wiring in `ServiceHost.RunAsync` is run by no test.
  Each change was also broken on purpose, one way at a time, to see which
  tests catch it: of 40 such changes, 37 were caught, one changed nothing that
  can be seen, and two were in the wiring no test runs.
  `docs/findings/2026-10-22-the-service-hears-the-printer-announce-itself.md`
  lists each.
- **Updating from `0.1.0`, `0.2.0`, `0.3.0` or `0.4.0`:** `docs/operating.md`,
  "Updating an installed service", "From a release". Check the new release
  first, as below. The configuration file, the service registration, the
  firewall rules and the data folder are the same as before.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. In the release notes GitHub
  shows, the section "Built with" after this entry names it, and so does
  `SecretPrinter.Service.runtimeconfig.json` in the folder. Microsoft's latest
  .NET 10 release on 2026-10-25 was 10.0.14, released that day as an
  out-of-band security update, and this release carries it; `0.3.0` and
  `0.4.0` carry 10.0.13. The one package SecretPrinter declares,
  `System.ServiceProcess.ServiceController`, stays at 10.0.11, as in `0.4.0`.
  (Corrected 2026-10-25 by Claude, Claude Opus 5.5, after the release. The
  sentence on Microsoft's latest release said "Microsoft's latest .NET 10
  release on 2026-10-25 was still 10.0.13, of 2026-10-13, which `0.4.0`
  carries." That was untrue when it was written: it came from one reading of
  one page, and 10.0.14 had been announced before. The release notes GitHub
  shows, taken from this file at the tag, carry that sentence, with a
  correction added after publishing; see
  [the finding](docs/findings/2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md).)
- **Where it has run.** The changes since `0.4.0` have run in tests, on the
  development machine, on Windows, and in Claude's workspace, on Linux; and
  in one build from source of the same code, not signed and carrying .NET
  10.0.12, installed as the service on FIOS-STB-01, which runs Windows 10, on
  2026-10-22 in place of `0.4.0`. That evening, with the printer switched off
  for about 16 minutes and held unreachable, the service logged that it had
  heard the printer no more than about 40 seconds after it was switched on,
  when its next question was about 3 minutes away, and an iPhone printed a
  page through it before and after. Read on 2026-10-25, its first 71 hours 21
  minutes there had no restart. It relayed 72 connections with the printer's
  certificate matching the pin, and 12 could not connect to the printer, 11
  of them in that test. It lost the printer 33 times and found it again each
  time, but heard it in none of the 31 losses after the test: each ended when
  one of its own questions was answered, three of them after the printer-side
  adapter was usable again, and the longest after 4 hours 10 minutes, at an
  hourly question. See
  `docs/findings/2026-10-22-the-service-heard-the-printer-come-back-on-fios-stb-01.md`
  and its status note of 2026-10-25. Which versions of Windows it can run on: `README.md`,
  open question 10.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it; installing it on FIOS-STB-01 is the first.
  - That this printer announces itself after a long silence, or when its own
    Wi-Fi reconnects. On FIOS-STB-01 it was heard only when it was switched on;
    whether it announced itself in any other loss is not shown.
  - On a network, that nothing is logged when the printer announces itself
    while it is held reachable, and a goodbye heard during a lookup. Tests
    show both.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.
- **What happened.** The workflow ran for this tag on 2026-10-25 and all three
  of its jobs passed. It named .NET SDK 10.0.403 and the runtime
  Microsoft.NETCore.App 10.0.14, on the run's page and in the section "Built
  with" of the release notes. The signing job waited for Edwin West's approval.
  Twelve SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The release is on the repository's Releases page as
  `SecretPrinter-0.5.0-win-x64.zip`, with SHA-256
  `C16BD249362E0ED71EFECAB380369C4604AFC81DF8040296A3DA9C5D1FD7E568`, and its
  `.sha256` file, marked Latest and not as a pre-release. A copy downloaded with
  `curl.exe` on FIOS-STB-01, which runs Windows 10, passed both checks there and
  was the same as the zip, file by file. **It carries .NET 10.0.14** and is
  stamped `0.5.0+1f1f1ee984f8881528518a140d5e400d75f21ab2`. It was installed
  there the same day by the update steps of `docs/operating.md` with the
  signature check, in place of an unsigned build of `5a291c4` made there on
  2026-10-22. The household had no printer for 10 seconds. The first line the
  new service logged was
  `SecretPrinter.Service version 0.5.0+1f1f1ee984f8881528518a140d5e400d75f21ab2`,
  and an iPhone printed a page through it; see
  [the finding](docs/findings/2026-10-25-the-fifth-release-was-published-and-installed-on-fios-stb-01.md).
  (Added 2026-10-25 by Claude, Claude Opus 5.5, after the run. It is not in the
  release notes GitHub shows, which were taken from this file at the tag.)

## [0.4.0] - 2026-10-20

The fourth version of SecretPrinter meant as a release. The service now names
its own version in its log at every start, and two faults that `0.1.0`,
`0.2.0` and `0.3.0` all have are corrected. Nothing in how it is installed or
configured changes.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **The service logs its version at every start** (`REQ-OBS-011`). The first
  line of every start, written before the configuration is read, is
  `SecretPrinter.Service version` followed by the version stamped in the
  program: in a release, the version, a `+`, and the commit it was built from.
  The log file is appended to across runs and updates, and until now it never
  said which build wrote a line. A start that the configuration refuses logs it
  too.
- **Two faults in `0.1.0`, `0.2.0` and `0.3.0`, corrected:**
  - **A connection accepted in the moment the service stops, or withdraws the
    printer, is now closed** (`REQ-LIF-002`). Until now a client connection
    the listener had just accepted when the service stopped, or withdrew the
    printer because the printer had stopped answering (`REQ-LIF-006`), could
    be left open, with nothing reading it, nothing in the service closing it,
    and nothing in the log: its relay was never started. Found by reading the
    code; never seen on a machine.
  - **Five warnings no longer end a sentence twice.** Each quoted an
    exception's message and added a full stop after it, so a message that
    brought its own, as the one Windows gives a socket error does, was printed
    with two:
    `... not valid in its context.. Treated as no answer.` The other four
    are about a printer that answers again but could not be offered, an
    advertisement that could not be withdrawn on the network, and goodbye
    records that could not be sent after a name conflict or at a stop.
- **The release notes now say what built the release.** After this entry the
  release workflow adds a section of its own, "Built with", naming the .NET
  SDK that built the release and the .NET runtime its folder carries, and puts
  the same on the run's page. Until now both were in the job's log only, which
  needs a sign-in to GitHub. This release is the first the workflow makes that
  way.
- **Tests.** 8 were added, for 536 in all on the development machine (528 at
  `0.3.0`). Each change was also broken on purpose, one way at a time, to see
  which tests catch it; the commit messages and the findings say what was
  tried and what was caught. Of the five warnings, the one at a stop is checked
  by reading, not by a test.
- **Updating from `0.1.0`, `0.2.0` or `0.3.0`:** `docs/operating.md`,
  "Updating an installed service", "From a release". Check the new release
  first, as below. The configuration file, the service registration, the
  firewall rules and the data folder are the same as before.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. In the release notes GitHub
  shows, the section "Built with" after this entry names it, and so does
  `SecretPrinter.Service.runtimeconfig.json` in the folder. Microsoft's latest
  .NET 10 release on 2026-10-20 was still 10.0.13, of 2026-10-13, which
  `0.3.0` carries. The one package SecretPrinter declares,
  `System.ServiceProcess.ServiceController`, stays at 10.0.11, as in `0.3.0`.
- **Where it has run.** The changes since `0.3.0` have run in tests only: on
  the development machine, on Windows, and in Claude's workspace, on Linux.
  One of the tests of `REQ-OBS-011` starts the service's program on Windows
  with a configuration file that does not exist and reads the log it writes.
  None of the changes has run as an installed service, or on a network, before
  this release; installing it on FIOS-STB-01 is the first. `0.3.0`, which has
  none of them, has been the household's proxy there since 2026-10-15. Which
  versions of Windows it can run on: `README.md`, open question 10.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it.
  - Any of the changes since `0.3.0` running as an installed service, or on a
    network.
  - The section "Built with" and the notice on GitHub: this release is their
    first run.
  - Updating an installation from one release to the next by the document's
    steps.
  - A connection caught in the moment the first fault is about: it cannot be
    made to happen on demand.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.
- **What happened.** The workflow ran for this tag on 2026-10-20 and all three
  of its jobs passed. Its new step named .NET SDK 10.0.402 and the runtime
  Microsoft.NETCore.App 10.0.13, on the run's page and in the section "Built
  with" of the release notes. The signing job waited for Edwin West's approval.
  Twelve SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The release is on the repository's Releases page as
  `SecretPrinter-0.4.0-win-x64.zip`, with SHA-256
  `F9FE275C7CD4B5BC24C1897C3C0F94E8FCB701040C2462A9B76B574961A4BB5F`, and its
  `.sha256` file, marked Latest and not as a pre-release. A copy downloaded with
  `curl.exe` on FIOS-STB-01, which runs Windows 10, passed both checks there and
  was the same as the zip, file by file. **It carries .NET 10.0.13** and is
  stamped `0.4.0+22b5252ee16e589bb7159bea2c3a7fe27b49dc42`. It was installed
  there the same day in place of release `0.3.0`, by the update steps of
  `docs/operating.md` with the signature check: **the first update from one
  release to the next.** The household had no printer for 10 seconds. The
  first line the new service logged was
  `SecretPrinter.Service version 0.4.0+22b5252ee16e589bb7159bea2c3a7fe27b49dc42`,
  and an iPhone printed a page through it; see
  [the finding](docs/findings/2026-10-20-the-fourth-release-was-published-and-installed-on-fios-stb-01.md).
  (Added 2026-10-20 by Claude, Claude Opus 5.5, after the run. It is not in the
  release notes GitHub shows, which were taken from this file at the tag.)

## [0.3.0] - 2026-10-15

The third version of SecretPrinter meant as a release. It corrects two faults
that `0.1.0` and `0.2.0` both have, and changes three things in how the
service answers questions on the network. Nothing in how it is installed or
configured changes.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **Two faults in `0.1.0` and `0.2.0`, corrected:**
  - **An answer sent by unicast now goes only to an address on the network
    the question came from** (`REQ-SEC-017`). Until now a legacy mDNS
    question, one sent from a port other than 5353, was answered by unicast to
    whatever source address it carried. A device on the client network that
    forged that address could have had the service send the printer's
    advertisement out of the printer-side adapter, or through a router to an
    address on neither network, and the answer is many times the size of the
    question. This has not been seen on a network. Now, over IPv4, the address
    must be on the client network as the relay works it out from the client
    interface's address; over IPv6, it must be a link-local address carrying
    the scope of the interface the question arrived on. A legacy question from
    any other address is not answered, and the line the service logs when it
    stops counts it.
  - **When Windows shuts down or restarts, the service now stops cleanly**
    (`REQ-LIF-009`). Until now, at every shutdown or restart of Windows, the
    service logged that it was stopping and its process then ended with an
    exception nothing caught: it sent no goodbye records, closed nothing in
    order and wrote no summary. A client could go on listing the printer after
    the machine had gone down, until the records expired: two minutes for the
    address records, 75 minutes for the browse and TXT records. The shutdown
    handler asked Windows for more time, which .NET's `ServiceBase` refuses
    with an exception at a shutdown. Now the service logs
    `Service 'SecretPrinter' stopping: Windows is shutting down.` and stops as
    it does for a stop request, without asking for more time. How long Windows
    waits for a service at a shutdown is set on each machine.
- **Three changes in how the service answers** (RFC 6762, the mDNS standard):
  - **A record is multicast on a client adapter at most once a second**
    (`REQ-ADV-027`; section 6, which requires it; `0.1.0` and `0.2.0` did not,
    as `README.md`, Known problems, said at both releases). An answer leaves
    out any record the service multicast on that adapter less than a second
    before, and when nothing is left, nothing is sent. Announcements and
    goodbyes wait instead, never more than a second, so that each still carries
    every record. An answer to another device's probe waits only until 250
    milliseconds have passed since its records were last multicast there.
  - **A question that asks for a unicast answer gets one** (`REQ-ADV-028`;
    section 5.4), record by record, when the service multicast that record on
    the adapter less than a quarter of its TTL before; otherwise the record
    goes by multicast, as before. Until now such a question, from port 5353,
    was answered by multicast. A unicast answer goes only to an address
    `REQ-SEC-017` allows.
  - **An answer that other devices may be sending too waits a random 20 to
    120 milliseconds first** (`REQ-ADV-029`; sections 6 and 6.3): one that
    holds a shared record, which are the service's three PTR records, or that
    answers a message of more than one question. An answer to a probe, or to a
    single question about records only the service holds, goes at once. An
    answer that waited is checked again before it is sent, and is not sent if
    meanwhile the printer was withdrawn, a conflict was heard or a probe began.
- **The line the service logs when it stops** now gives four more counts:
  queries not answered because the answer would only have repeated records
  multicast less than a second before; queries not answered because the answer
  would have gone by unicast to an address off their network; and, of the
  queries answered, how many were answered by unicast because they asked for
  it, and how many after a random wait.
- **Tests.** 76 were added, for 528 in all on the development machine (452 at
  `0.2.0`). One runs only on Windows: it drives the service's real shutdown
  handler; two more read the compiled service to check what that handler
  calls. Tests written before these changes were given a clock they control,
  or no random wait, where the new timing would otherwise change what they
  see. Each change was also broken on purpose, one way at a time, to see which
  tests catch it; the findings in `docs/findings/` list each attempt.
- **Updating from `0.1.0` or `0.2.0`:** `docs/operating.md`, "Updating an
  installed service", "From a release". Check the new release first, as below.
  The configuration file, the service registration, the firewall rules and the
  data folder are the same as before.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. Its version is in
  `SecretPrinter.Service.runtimeconfig.json` in the folder. It is not known
  when this entry is written. Microsoft's latest .NET 10 release on 2026-10-15
  was 10.0.13, of 2026-10-13, which Microsoft marks as a security release;
  `0.1.0` and `0.2.0` carry 10.0.12. The one package SecretPrinter declares,
  `System.ServiceProcess.ServiceController`, stays at 10.0.11: its source
  folder, and that of `System.Diagnostics.EventLog`, which it brings, did not
  change between the tags `v10.0.11` and `v10.0.13` of dotnet/runtime.
- **Where it has run.** Each of the five changes ran on a network before this
  release. `REQ-ADV-027`, and `REQ-SEC-017` with `REQ-ADV-028`, ran on
  2026-10-12 and 2026-10-13 on a Windows 11 machine, for about 10 and 13
  minutes. `REQ-ADV-029` ran on 2026-10-14 on FIOS-STB-01, on Windows 10 22H2,
  where each of the 17 queries it answered was answered after the random wait.
  An iPhone printed a page in each of those three runs. `REQ-LIF-009` ran at a
  real restart of FIOS-STB-01 on 2026-10-14, with the printer offered: the
  service logged sending its goodbye, wrote its summary and stopped, and
  Windows recorded no application error. Since 2026-10-14 a build of the same
  source code has been the household's proxy on FIOS-STB-01. In the 19 hours 38
  minutes from its last start that day to 16:35Z on 2026-10-15, it lost the
  printer 11 times and found it again each time without a restart, offering it
  again 9 seconds to 6 minutes 24 seconds after withdrawing it. Once the
  printer-side Wi-Fi had dropped; a script of Edwin West's, not part of
  SecretPrinter, reconnected it, and the printer was offered again 4 seconds
  after the script began to reconnect it. Since that build's first start it has
  relayed 79 connections, each to a printer whose certificate matched the pin:
  77 completed, none failed, and the other two were open when Windows
  restarted, so the log has no line for their end. None of those builds was
  signed; the first two were made on the development machine, the others on
  FIOS-STB-01. Microsoft's list of the systems .NET 10 supports (last updated
  2026-09-28) includes Windows 11 and not Windows 10 22H2; `README.md`, open
  question 10, says more.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it.
  - Updating an installation from one release to the next by the document's
    steps.
  - At a shutdown: the goodbye on the wire (it was logged, not captured); a
    shutdown that turns the machine off rather than restarting it; a shutdown
    that arrives while the service is already stopping.
  - On a network, a legacy question from a forged address being refused.
    Tests show it.
  - `REQ-ADV-029` on Windows 11, and `REQ-LIF-009` at a restart of any machine
    but FIOS-STB-01.
  - Three things RFC 6762 recommends about the timing of answers are still not
    done; among them, while one answer waits its random time, the service
    reads no other question. A question sent by unicast straight to the
    service's port 5353 has not been seen answered. `README.md`, "Known
    problems", says more of both.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.
- **What happened.** The workflow ran for this tag on 2026-10-15 and all three
  of its jobs passed. The signing job waited for Edwin West's approval. Twelve
  SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The release is on the repository's Releases page as
  `SecretPrinter-0.3.0-win-x64.zip`, with SHA-256
  `74D7BB3F69EB6CA6422E54012E1A65D237FC5D0EB6807D146A279F5C5DE46660`, and its
  `.sha256` file, marked Latest and not as a pre-release. A copy downloaded with
  `curl.exe` on FIOS-STB-01, which runs Windows 10, passed both checks there and
  was the same as the zip, file by file. **It carries .NET 10.0.13** and is
  stamped `0.3.0+064ca9fc7cf649ff3eed615758274bedb533767f`. It was installed
  there the same day in place of the build of the same source code that had run
  since 2026-10-14, by the update steps of `docs/operating.md` with the
  signature check; the household had no printer for 10 seconds, and an iPhone
  printed a page through it; see
  [the finding](docs/findings/2026-10-15-the-third-release-was-published-and-installed-on-fios-stb-01.md).
  Updating from one release to the next is still not shown.
  (Added 2026-10-15 by Claude, Claude Opus 5.5, after the run. It is not in the
  release notes GitHub shows, which were taken from this file at the tag.)

## [0.2.0] - 2026-10-11

The second version of SecretPrinter meant as a release. It changes two things
in how the service behaves on the network, and nothing in how it is installed
or configured.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **What changed since `0.1.0`:**
  - **The printer is asked again as soon as its network comes back**
    (`REQ-RES-010`). When the service cannot reach the printer it stops
    offering it and asks for it less and less often, down to once an hour
    (`REQ-RES-008`). Now it also examines the printer-side network adapter
    every 5 seconds, and when Windows reports the adapter usable again, or
    usable on a new address, while the printer is away, it asks at once and
    starts that sequence over from one second. It logs one line beginning `The printer-side interface is
    usable again:`. This helps when the printer answers soon after its network
    is back. It does not make every outage shorter: when the printer itself
    stays away for an hour or more after its network returns, it can be found
    later than it would have been before. The service still does not reconnect
    a dropped Wi-Fi link; something else has to.
  - **A question that already carries the answer is not answered**
    (`REQ-ADV-026`; RFC 6762, section 7.1, known-answer suppression). The line
    the service logs when it stops now also counts these questions.
  - Nothing else in the programs changed. The configuration file, the service
    registration, the firewall rules and the data folder are the same as for
    `0.1.0`.
- **Updating from `0.1.0`:** `docs/operating.md`, "Updating an installed
  service", "From a release". Check the new release first, as below.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. Its version is in
  `SecretPrinter.Service.runtimeconfig.json` in the folder. It is not known when
  this entry is written. Microsoft's latest .NET 10 release on 2026-10-11 was
  10.0.12, of 2026-09-08, which `0.1.0` also carries.
- **Where it has run.** Its programs are compiled from the same source code as
  a build that has run as the household's proxy on FIOS-STB-01, on Windows 10
  22H2, since 2026-10-10. In its first 21 hours 31 minutes it lost the printer
  10 times and found it again each time without a restart. The start over
  above acted three times, each within 3 seconds of the Wi-Fi being
  reconnected, and the printer was on offer again within 6 seconds of the
  reconnect. An iPhone printed through it. That build was made on FIOS-STB-01
  and was not signed. The known-answer change also ran once on Windows 11, on
  one machine, for under ten minutes, and a page printed. Microsoft's list of
  the systems .NET 10 supports includes Windows 11 and not Windows 10 22H2;
  `README.md`, open question 10, says more.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it.
  - Updating an installation from one release to the next by the document's
    steps.
  - On a network, that the service says nothing when the printer-side adapter
    comes back while the printer is still being offered. Tests show it.
  - The start over on a machine where nothing reconnects the Wi-Fi. On
    FIOS-STB-01 Windows did not reconnect it by itself; a script of Edwin
    West's, not part of SecretPrinter, did each time.
  - Three things RFC 6762 asks of a responder are still not done. `README.md`,
    "Known problems", lists them.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.
- **What happened.** The workflow ran for this tag on 2026-10-11 and all three
  of its jobs passed. The signing job waited for Edwin West's approval. Twelve
  SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The release is on the repository's Releases page as
  `SecretPrinter-0.2.0-win-x64.zip`, with SHA-256
  `A237646A02776288EFD01FEE865B15223493D677EFC3644B63451B6AAA77B176`, and its
  `.sha256` file, marked Latest and not as a pre-release. A copy downloaded with
  `curl.exe` on FIOS-STB-01, which runs Windows 10, passed both checks there. It
  carries .NET 10.0.12 and is stamped
  `0.2.0+5b846ba027ab96372bde70295009334b37b54405`. It was installed there the
  same day in place of the build of the same source code that had run
  overnight, by the update steps of `docs/operating.md` with the signature
  check, and an iPhone printed a page through it; see
  [the finding](docs/findings/2026-10-11-the-second-release-was-published-and-installed-on-fios-stb-01.md).
  Updating from one release to the next is still not shown.
  (Added 2026-10-11 by Claude, Claude Opus 5.5, after the run. It is not in the
  release notes GitHub shows, which were taken from this file at the tag.)

## [0.1.0] - 2026-10-08

The first version of SecretPrinter meant as a release.

SecretPrinter is a Windows service for a machine connected to two networks. It
makes one AirPrint printer on one network appear on the other, answering for it
over mDNS, and relays each print connection to the printer over TLS, with the
printer's certificate pinned by its fingerprint. It forwards nothing else
between the two networks. `README.md` in the repository is its specification.

- **Before you install it,** read two sections of `README.md`: "Read this
  before installing" and, under it, "Known problems". This software was written
  by an AI, Claude, directed by one person, and has had no outside security
  review. Every measurement of it on a network was made in one household, with
  one printer model, an Epson ET-3760, and almost always an iPhone as the
  client.
- **What it holds.** The service and the probe, for 64-bit Windows on x64
  processors, with the .NET runtime they need beside them, so that nothing else
  has to be installed. It is configured by hand, by the steps in
  `docs/operating.md`; there is no configuration tool.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", gives two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`. A SecretPrinter file
  that is unsigned, or signed under any other name, is not from this project.
- **The .NET runtime inside it** is the one the build machine's SDK supplied,
  and a fix to it arrives only in a new release. Its version is in
  `SecretPrinter.Service.runtimeconfig.json` in the folder. It is not known when
  this entry is written; `0.1.0-rc.3`, built from the same source code on
  2026-10-07, carries .NET 10.0.12.
- **Where it has run.** SecretPrinter has run day to day as a service on
  Windows 10 22H2 since 2026-09-18, built from source; and on Windows 11 Home,
  on one machine, for a little over an hour in all, from release folders. Microsoft's
  list of the systems .NET 10 supports includes Windows 11 and not Windows 10
  22H2; `README.md`, open question 10, says more.
- **How it relates to `0.1.0-rc.3`.** Its programs are compiled from the same
  source code as the pre-release `0.1.0-rc.3` of 2026-10-07. They differ in the
  version and the commit stamped into each file, and in the .NET runtime if the
  build machine's SDK has changed since. What changed in between is
  documentation:
  - `docs/operating.md` was corrected after `0.1.0-rc.3` was downloaded with a
    browser on 2026-10-08, installed from nothing on a second machine by the
    document's own steps, and printed through. The firewall commands now name
    the installed program; there is a command for unpacking; it says what
    Windows shows when the service fails after it has started; and it says
    where an iPhone listed the proxy.
  - `README.md`, `SECURITY.md` and `docs/verification.md` say what that install
    showed, and `SECURITY.md` has a table of versions.
- **What has been shown** on `0.1.0-rc.3`, which has the same source code: the
  published zip downloaded with a browser, with both checks passing on it;
  configured by hand from the example the program prints; installed as a
  Windows service under `LocalService`; a failure of the running service
  provoked on purpose, which stopped the service and ended its process, Windows
  then showing it stopped with error 1067; an iPhone printing through it; and
  the uninstall steps leaving nothing behind.
- **What has not been shown,** at the time this entry is written:
  - This build itself. When this is written nobody has downloaded it, checked
    it or run it.
  - The publishing job making a release that is not marked as a pre-release.
    It has run once, for a pre-release.
  - Updating an installation from one release to the next by the document's
    steps. There is no earlier release to update from.
  - Whether a recovery action set with `sc.exe failure` runs when the service
    ends itself after a failure.
  - Any printer but the one model, any household but one, and any client but
    one iPhone and, once, a second device.
- **Reporting a problem.** Security problems go to the repository's private
  vulnerability reporting, as `SECURITY.md` says, and not to a public issue. No
  response-time commitment is offered.
- **What happened.** The workflow ran for this tag on 2026-10-08 and all three
  of its jobs passed. The signing job waited for Edwin West's approval. Twelve
  SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The release is on the repository's Releases page as
  `SecretPrinter-0.1.0-win-x64.zip`, with SHA-256
  `7006676752EE2C767D264EAB810E835F1D56A4FF88F19DE89D277994DA209D22`, and its
  `.sha256` file, marked Latest and not as a pre-release. A copy downloaded with
  a browser on a second machine passed both checks. It carries .NET 10.0.12 and
  is stamped `0.1.0+27b5f5c98368bbc1fde2ff7ece4aaa870dcb961d`. When this is
  written nothing in it has been run; see
  [the finding](docs/findings/2026-10-08-the-first-release-was-published.md).
  (Added 2026-10-08 by Claude, Claude Opus 5.5, after the run. It is not in the
  release notes GitHub shows, which were taken from this file at the tag.)
  (Added 2026-10-09 by Claude, Claude Opus 5.5: on 2026-10-09 it was downloaded
  on FIOS-STB-01, which runs Windows 10, passed both checks there, and was
  installed in place of a build from source by the update steps of
  `docs/operating.md`. It started, offered the printer, and an iPhone printed
  through it: the first run of this build, and by the record the first run of
  a release folder on Windows 10. Of the things listed above as not shown, this
  build itself was run that day; updating from one release to the next still
  has not been;
  see [the finding](docs/findings/2026-10-09-the-first-release-replaced-a-build-from-source-on-fios-stb-01.md).)

## [0.1.0-rc.3] - 2026-10-07

A pre-release for testing. **It is not release 0.1.0, and it carries no support
promise of any kind.** It exists so that two things are tried once before the
first release: the job that publishes a release, which has never run, and an
install on a clean machine from a zip downloaded with a browser, followed from
`docs/operating.md`.

- **Before you install it,** read two sections of `README.md` in the
  repository: "Read this before installing" and, under it, "Known problems".
  This software was written by an AI and has had no outside security review.
  Every measurement of it on a network was made in one household, with one
  printer model, an Epson ET-3760.
- **What it holds.** The service and the probe, for 64-bit Windows, with the
  .NET runtime they need beside them. It is configured by hand, by the steps in
  `docs/operating.md`; there is no configuration tool.
- **Check it before you install it.** `docs/operating.md`, "Checking a release
  before you install it", has two checks that need only PowerShell: the zip's
  SHA-256 against the `.sha256` file published beside it, and the signature on
  every program file. SecretPrinter's own files are signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`.
- **The .NET runtime inside it** is whichever one the building machine's SDK
  supplied, and a fix to it arrives only in a new build. Its version is in
  `SecretPrinter.Service.runtimeconfig.json` in the folder. It is not known
  when this entry is written.
- **What changed since `0.1.0-rc.2`,** which was a trial and was not published:
  - A part of the running service that fails now stops the whole service,
    which says goodbye as it ends. Before, the rest ran on. Under the Windows
    service control manager the process then ends with exit code 4.
  - The mDNS responder survives a socket error while sending an answer, and
    logs it. Before, one such error ended its answering for good.
  - The example configuration prints `advertise.uuid` empty, as it prints the
    certificate fingerprint, and the service refuses to start until both are
    filled in. Before, it printed a fixed UUID that loaded.
  - The probe is held to its own security requirement, `REQ-SEC-016`, with
    tests that read its compiled files.
  - `README.md` was read against the code from its first line to its last and
    corrected, and gained the section "Known problems".
  - `docs/operating.md` was corrected from two walks through its steps on a
    second machine, and gained "What you will see on an iPhone".
- **What has not been shown,** at the time this entry is written:
  - The publishing job has never run. This tag is its first run.
  - What Windows shows when the service ends its own process after a failure.
    It is tested as far as it can be without Windows and has not been run
    under the service control manager.
  - `--print-example-config` from this build, and a start on its output, on
    Windows.
  - Anything on a printer other than the one model, or in another household.
- **What happened.** The workflow ran for this tag on 2026-10-07 and all three
  of its jobs passed, the publishing job on its first run. The signing job
  waited for Edwin West's approval. Twelve SecretPrinter files were signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, in a folder of 216
  files. The pre-release is on the repository's Releases page as
  `SecretPrinter-0.1.0-rc.3-win-x64.zip`, with SHA-256
  `F8310DD6B5B061925958C1A25521C32E4C9979DC2BAE751E0C69ABCBC827C315`, and its `.sha256`
  file. When this is written nobody has downloaded and installed it; see
  [the finding](docs/findings/2026-10-07-the-release-workflow-published-a-pre-release.md).
  (Added 2026-10-07 by Claude, Claude Opus 5.5, after the run. It is not in
  the release notes GitHub shows, which were taken from this file at the tag.)
  (Added 2026-10-08 by Claude, Claude Opus 5.5: on 2026-10-08 it was
  downloaded with a browser on a second machine, checked by both checks,
  configured by hand, installed as a service by the steps of
  `docs/operating.md`, made to fail once on purpose, and printed through from
  an iPhone. It carries .NET 10.0.12. Two of the things listed above as not
  shown were shown that day: `--print-example-config` and a start on its
  output, and the service ending its own process after a failure, which
  Windows then showed as stopped with error 1067; see
  [the finding](docs/findings/2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md).)

## [0.1.0-rc.2] - 2026-10-04

The second trial of the release workflow. **This is not a release, and nothing
is published for it.**

- **Why it exists.** The first trial, `0.1.0-rc.1`, stopped before anything was
  signed. This one carries the correction and tries again.
- **What changed.** One line in `.github/workflows/release.yml`: the step that
  runs the specification check now ends with exit code 0 once it has recorded
  the check's result. Nothing in SecretPrinter itself changed.
- **Why nothing is published,** and what the build holds: as for `0.1.0-rc.1`
  below.
- **What happened.** The workflow ran for this tag and signed. Twelve
  SecretPrinter files came out signed as
  `CN=Edwin West, O=Edwin West, L=Huntington, S=ny, C=US`, each with a timestamp,
  and the other 190 program files were still validly signed by Microsoft. The
  zip is 37,554,102 bytes with SHA-256
  `8A422569A5BBFB63C61385C5AE4117992CB3C3D173E8CB15B1F0D44A683C2A46`. It was
  kept with the workflow run for seven days and published nowhere. (Added
  2026-10-04 by Claude, Claude Opus 5.5.)

## [0.1.0-rc.1] - 2026-10-04

A trial of the release workflow. **This is not a release, and nothing is
published for it.**

- **Why it exists.** Signing can only be shown to work by signing something.
  This tag makes the release workflow run once from end to end: build, test,
  make the release folder, sign SecretPrinter's files, check every signature,
  and zip the result.
- **Why nothing is published.** The workflow publishes a GitHub Release only
  when the specification check passes. At this tag three binding requirements
  are uncovered: `REQ-DIST-004` (signing), `REQ-DIST-005` (how to check a
  signature) and `REQ-DIST-006` (this changelog and a tagged release). The
  signed zip stays with the workflow run, for examination.
- **What the build holds.** The service and the probe, published for 64-bit
  Windows with the .NET runtime beside them, as `publish-release.ps1` makes
  them. The configuration tool does not exist yet.
- **What happened.** The workflow ran once for this tag and failed in its first
  job. The step that runs the specification check recorded "not passed", as
  intended, and then ended with the check's own exit code of 1, which GitHub
  treats as a failed step. No release folder was made and nothing was signed.
  Claude had exercised that step without the wrapper GitHub puts around a
  PowerShell step, so the mistake did not show before the run. (Added
  2026-10-04 by Claude, Claude Opus 5.5.)

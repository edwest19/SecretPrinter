# An iPhone printed after its host-name question was answered with an NSEC

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-02. Reviewed by a human before merge.*

**Status: `REQ-ADV-022` verified on hardware for the host name's `NSEC` as an
answer, and for the goodbye. On 2026-10-02 FIOS-STB-01, running `64e8fc2`,
answered an iPhone's type 65 question for `secretprinter.local` with the `NSEC`
twelve times, and the iPhone printed a page 63 seconds after one of those
answers. Four other attempts on that build did not print: one because the
printer was lost, one that never reached the service, and two in which the
iPhone reached the printer and sent no job. That last failure is on record from
2026-10-01 on a build that sent no `NSEC`, and its cause is not established.
Part-way through, Claude said the evidence pointed at the `NSEC` and
recommended reverting the commit; that was wrong, and is set out below.**

No IPv6 address, MAC address, device name or printer host name appears in this
finding. They are household values. The captures hold them, and the printed
pages too, so they are kept outside the repository.

## What was being tested

Commit `64e8fc2` made the responder send `NSEC` records: as the answer to a
question for a type that its host name or its instance name does not have, as
an additional beside an address record when the host has no address of the
other type, and in the goodbye (`REQ-ADV-022`). Its tests passed against a fake
transport. It was committed without `REQ-ADV-022` markers, to be marked only
after this, agreed beforehand:

> The iPhone's question for `secretprinter.local` type 65, unanswered on
> 2026-09-29, is answered in a capture by an `NSEC` listing `A` and `AAAA`, a
> page prints, and the log shows no conflict.

## The test bed

- **FIOS-STB-01**, as described in the
  [2026-10-01 finding](2026-10-01-an-iphone-printed-over-ipv6-link-local.md):
  client side `Ethernet`, 192.168.1.161, IPv6 index 15; printer side `Wi-Fi`,
  192.168.12.163, index 32; the printer at 192.168.12.180.
- **Builds**, each put on by the procedure in [`operating.md`](../operating.md):
  `64e8fc2` from 04:02:15Z; `1d7cbbd`, the commit before it, from 15:23:49Z;
  `64e8fc2` again from 16:04:24Z. All times here are UTC on 2026-10-02.
- **The client** was one iPhone, by Edwin's account. The captures cannot name a
  device. Across them it used three link-local addresses and two hardware
  addresses, one of them a private Wi-Fi address. It was absent from the client
  network at the start of run 6 and arrived five and a half minutes in.
- **Captures:** pktmon on the client adapter only, TCP 631 and UDP 5353, whole
  packets, none dropped. The Broadcom's component Id was 12. Each capture
  started only if `pktmon list` showed Id 12 beside the Broadcom's MAC address
  and no connection on port 631 was open; from run 4 on, also only if the
  service was listening on 631.

| Run | Window | Packets | SHA-256 |
| --- | --- | --- | --- |
| 1 | 04:08:38 to 04:12:43 | 813 | `8522228692456CCA7193C8692213F97F3A52924F10B58CCCAD674ADF8A587773` |
| 2 | 04:25:21 to 04:28:42 | 951 | `FF4E5936B0D6B207CD6579EDF0A849A29AFDA1E3AB3FFD43A152B6D3EAB4A80A` |
| 3 | 04:43:34 to 04:48:30 | 532 | `DA8B773B37753B21F1A267210E16F7FA29D3E647C325140358B61DBCE8849E5F` |
| 4 | 15:07:27 to 15:15:49 | 177 | `42DD57BEE7A6B31055DD75079E9AECEEE04EA68CAAD900B54E1D81D20D500EE1` |
| 5 | 15:25:40 to 15:30:36 | 461 | `99E72A8C0AF44622DF950818979CBAEB846D02C2B91F5204B9EF64EE650C40B7` |
| 6 | 16:05:39 to 16:23:43 | 1432 | `1291CE640D4F657CD2657826F7460B087DAC0808DCB2EDD75102660B86BE8211` |

Each hash was the same on FIOS-STB-01, in its OneDrive copy, and as uploaded
for reading. Claude read protocol headers and the printer's status attributes,
and no document data.

## What the service logged at startup

On `64e8fc2` the startup log listed both `NSEC` records after the announced
records, under a line saying they are not announced and when they are sent
(`REQ-OBS-003`): the instance's, `ttl=4500 types=Txt,Srv`, and the host's,
`ttl=120 types=A,Aaaa`. On `1d7cbbd` it listed none, as that build sends none.

## The NSEC on the wire

The iPhone asked for `secretprinter.local` types 65, `AAAA` and `A` in one
multicast question, over IPv6. Before most of them it had sent the same
questions by unicast straight to the service, and those were not answered (see
"Also seen").

The service answered the multicast question twelve times, in runs 2, 3, 4 and
6. Every answer was the same:

- sent by multicast over IPv6, 0.7 to 6.8 milliseconds after the question;
- three records in the Answer section and none anywhere else;
- the `NSEC` for `secretprinter.local`: class IN, cache-flush bit set, TTL 120,
  its next domain name its own name, listing `A` and `AAAA`;
- the `AAAA` record and the `A` record, each with TTL 120 and the bit set.

No `NSEC` appeared in run 1, where nobody asked for a type the service's names
lack: every answer there carried both address records, so there was no absence
to state. No `NSEC` appeared in run 5, on `1d7cbbd`: the same question was
answered with the `AAAA` and the `A` only.

## The goodbye, the probe and the log

- **The goodbye retracts both.** In run 3 the service lost the printer and
  withdrew. At 04:46:26.720 it sent nine records over IPv4 multicast, all with
  TTL 0: three `PTR`, the `SRV`, the `TXT`, the `A`, the `AAAA` and both `NSEC`
  records.
- **The probe came back clear every time.** On `64e8fc2` the service logged
  `Probe clear` at seven starts and restores that were read: 04:49, 07:03,
  08:23, 08:50, 09:54, 15:01 and 16:04.
- **No conflict was logged.** The log was read for 04:02 to 13:52, and for
  16:05 to 16:29. It holds no conflict line. It was not read for 15:01 to
  15:23, which covers run 4; in that run the service went on answering after
  its `NSEC`, which it would not have done while holding a conflict.
- **The service went on answering after every `NSEC` it sent,** so it did not
  take its own `NSEC`, heard back, for another device's (`REQ-ADV-024`).

## The attempts

| Attempt | Build | Host-name question answered with the `NSEC` first | Printer side | Result |
| --- | --- | --- | --- | --- |
| Run 1 | `64e8fc2` | Not asked | Up | Printed |
| Run 2 | `64e8fc2` | Yes, 78 seconds before | Up | **No job sent** |
| 04:37 and 04:40, no capture | `64e8fc2` | Not known | Up | Printed twice, by the log |
| Run 3 | `64e8fc2` | Yes | Lost during the run | No job sent |
| Run 4 | `64e8fc2` | Yes, 0.2 seconds before | Up | **No job sent** |
| Run 5 | `1d7cbbd` | Asked; no `NSEC` sent | Up | Printed |
| Run 6, first attempt | `64e8fc2` | Not established | Up | No job sent |
| Run 6, second attempt | `64e8fc2` | Yes, 63 seconds before | Up | Printed |

**The print that completes the test.** In run 6 the service answered the
host-name question at 16:21:35, 16:21:36 and 16:21:39. The iPhone opened its
first connection to port 631 a tenth of a second after the last. It sent
Validate-Job at 16:22:42, Create-Job at 16:22:44 and Send-Document at 16:22:48,
each answered `successful-ok`, and the printer reported the job
`completed-successfully`. The log records 38,520 bytes relayed on that
connection. Edwin saw the page. The `NSEC` has a TTL of 120 seconds, so the
iPhone still held it.

## The attempts that did not print

**Runs 2 and 4.** The iPhone reached the relay and asked for the printer's
attributes, 26 times in run 2 and 12 in run 4. Every reply seen was `200 OK`.
On the connections checked, one in each run, the iPhone acknowledged every
byte of the reply. The printer reported no problem (`printer-state-reasons` was
`none` wherever it appeared), and the request line, printer URI and client
software were the same as in run 1. The iPhone never sent Validate-Job, and
showed a message that it could not reach the printer. Nothing in the headers
says why.

**The same failure is older than `64e8fc2`.** The
[2026-10-01 finding](2026-10-01-an-iphone-printed-over-ipv6-link-local.md)
records a first attempt on `2c93864`, which sent no `NSEC`: every request
answered `successful-ok`, the printer reporting no problem, no job sent, and a
first connection of 1,445 bytes sent and 2,644 received, the byte counts seen
again in runs 2 and 4. No device asked the host-name question in that window.

**Run 3** has a cause. The printer stopped answering on the printer side. From
04:46:08 three connections ended `Could not locate the printer`, and at
04:46:26 the service logged `Printer unreachable`, closed its listener and sent
the goodbye. It was offered again at 04:49:12.

**Run 6, first attempt.** Nothing from it reached the service's port 631: there
was no connection and no browse for printers before 16:21:39. When the attempt
was made was not recorded. If before 16:11:16, the iPhone was not on the client
network. If after, it had the host-name answer and did not connect.

**What Edwin saw in the list.** At the second attempt of run 6 a second
printer, its name beginning "Epson", was listed beside SecretPrinter; he chose
SecretPrinter. The [2026-09-06 finding](2026-09-06-ipv6-mdns-transport.md)
warned that an unreachable entry for the real printer, if selected, would fail
this way. In run 3 the iPhone asked for the printer's own host name 16 times,
starting 32 milliseconds after the goodbye; in runs 2, 4 and 6 it did not ask
for it at all. Whether a wrong selection played any part in any attempt is not
established.

**So:** the failure to send a job is not explained. It is not attributed to the
`NSEC`, because it happened without one on 2026-10-01 and a page printed with
one fresh in the iPhone's cache in run 6. It is not cleared as a problem
either. Ten attempts on record reached the relay with the printer side up,
three on 2026-10-01 and seven here, and three of the ten ended this way.

## A wrong turn

After run 5 Claude told Edwin that the comparison pointed at `64e8fc2`: with
the same steps, the build that sends the `NSEC` had failed twice and the build
that does not had printed once. Claude recommended reverting the commit, and
Edwin agreed.

Before preparing the revert Claude re-read the 2026-10-01 finding and found the
same failure recorded there on a build with no `NSEC`. Claude had not read it
before making the recommendation. Claude withdrew it, and Edwin chose one more
test: a page printing within two minutes of an `NSEC` answer would clear the
`NSEC`, and three failures in a row would lead to the same three attempts on
`1d7cbbd`. Run 6 was that test.

Earlier, after run 2, Claude headed a section "The two runs were different
devices" because the hardware addresses differed. Edwin corrected it: it was
one iPhone. The addresses differ; that is all the captures show.

## Not measured

- The instance name's `NSEC` as an answer. Nothing asked for a type the
  instance lacks.
- The `NSEC` as an additional. The host has both address types on this
  machine, so the service had no reason to send it.
- The `NSEC` in a legacy unicast answer.
- Any client but this iPhone. Its iOS version was not recorded.
- Why Windows or the iPhone behaved as described under "Also seen".

Those three forms of the `NSEC` have been checked only in tests, with a fake
transport.

## Also seen

1. **The print dialog asks the host-name question itself.** In runs 4 and 5
   the iPhone asked for types 65, `AAAA` and `A` at the host name half a second
   before opening its first connection, minutes after the browser had last
   been used. A step in Safari, added in run 2 to make the iPhone ask, was not
   needed for that.
2. **Known answers are answered anyway.** In run 1 eight of the iPhone's
   questions already carried the service's `PTR` as a known answer with nearly
   its full TTL, and the service answered each. RFC 6762 §7.1 says it should
   not. The same happened once in run 4 and eleven times in run 6.
3. **Answers less than a second apart.** In run 1 three of the service's ten
   answers followed the one before by 0.22, 0.34 and 0.67 seconds, repeating
   the same records. RFC 6762 §6 allows one multicast of a record per second.

   *(Status note, 2026-10-07, by Claude, Claude Opus 5.5: items 2 and 3 now have a
   finding of their own, with two more things RFC 6762 asks of a responder
   that the service does not do, each measured against the code:
   [the finding](2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md).
   The README lists all four under Known problems. Until today it stated
   none of them. The code is unchanged.)*
4. **Direct unicast questions went unanswered again,** as on 2026-10-01: 36 of
   them across the six runs, and no unicast reply from the service in any.
5. **The capture gaps are explained.** tshark marked up to 17 segments as
   missing in a run although pktmon reported none dropped. Each such run holds
   frames of 2,523 or 2,718 bytes from the relay whose IPv6 payload length is
   written as 0: replies larger than one packet, recorded before the adapter
   splits them. tshark does not read them as TCP, and so reports a gap. The
   iPhone's acknowledgements cover the bytes.
6. **Three connections have no ending line in the log.** In run 3 the capture
   shows three connections accepted in the 13 milliseconds before the goodbye.
   Two were reset at once and the third 24 seconds later. The log has no
   `completed` or `failed` line for any of them.
7. **The service withdrew the printer six times in a night** and offered it
   again 14 seconds to 2 minutes 46 seconds later: at 02:39 on the old build,
   and at 04:46, 07:01, 08:22, 08:50 and 09:54 on `64e8fc2`. For the five on
   `64e8fc2` the log gives the reason, the printer unreachable on the printer
   side. For 02:39 the reason was not read. The last four happened with nobody
   printing.
8. **Then it stayed down for three and a half hours.** The printer went
   unreachable at 11:32:22. At 11:50:19 the service logged that `Wi-Fi` held
   its address but was not up. A service restart at 13:52 got no answer, with
   the adapter up. About an hour later a connection to the printer worked, and
   the probe tool got its direct replies, with the service still unanswered. A
   restart at 15:01:07 was answered at once; a listening tool had joined the
   same multicast group on `Wi-Fi` 16 seconds before. Whether the path had
   already recovered or one of those two things restored it is not
   established. After a long silence the service asks only once an hour
   (`REQ-RES-008`), so it can stay withdrawn long after the path is back. See
   the findings of
   [2026-09-19](2026-09-19-the-printer-side-multicast-membership-is-lost.md),
   [2026-09-25](2026-09-25-a-reconnect-did-not-restore-the-membership.md) and
   [2026-09-27](2026-09-27-a-lost-membership-recovered-without-a-restart.md).
   *(Status note, 2026-10-03, by Claude, Claude Opus 5.5: the service does not
   log the questions it asks while the printer is unreachable, so "with the
   service still unanswered" above does not mean it asked at that time. Worked
   out from the schedule in the code, a service started at 13:52 and never
   answered asks at about 14:27 and about 15:01, and not between. That is
   arithmetic, with the start known only to the minute. See
   [the 2026-10-03 finding](2026-10-03-the-printer-came-back-at-an-hourly-question.md),
   which also records Edwin's decision that the hourly schedule stays.)*
9. **The printer closed connections on the relay nine times** between 04:58
   and 05:55 (`forcibly closed by the remote host`), one TLS handshake timed
   out at 05:36, and two lookups failed at 06:02:36 with no withdrawal
   following.
10. **FIOS-STB-01 sent all 15 resets on port 631 in run 1,** as recorded
    before.

## Prediction record

Claude predicted:

- For the commit: the status lines, `10 files changed, 334 insertions(+), 42
  deletions(-)`, 411 tests, 416 test records and 5 binding gaps. **Held.**
- For the update: a fast-forward to `64e8fc2`, the service running, and the
  two `nsec` lines with their TTLs and types. **Held.**
- For run 1: that the iPhone would ask for type 65 at the host name. **Did not
  hold;** it did not ask.
- For run 1's log: 16 `Job:` lines, one for each connection. **Wrong;** the log
  writes up to four lines for a connection. The 16 connections were there.
- For run 2: that the question would be answered with an `NSEC` listing `A`
  and `AAAA`, TTL 120, cache-flush bit set, by multicast over IPv6, and that
  the service would go on answering. **Held.**
- For run 3's log: connections failing because the printer could not be
  reached, an unreachable line at 04:46:26, and no conflict. **Held.**
- For run 5: that `1d7cbbd` would send no `NSEC`. **Held.**
- For run 6's log: nothing before 16:21:39, ten connections, the job's
  connection completing with tens of thousands of bytes, no conflict.
  **Held.**
- Whether a page would print was not predicted in any run; that was the
  question.

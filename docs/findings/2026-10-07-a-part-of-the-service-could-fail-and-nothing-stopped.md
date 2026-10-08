# A part of the service could fail and nothing stopped

*Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
West, 2026-10-07. Reviewed by a human before merge.*

**Status: corrected in the code in the commit that carries this finding, and
not yet run on Windows. Three things were wrong, all found by reading and none
ever seen on a machine. When one part of the running service failed, the other
parts went on: a relay that could not open its listeners logged that the
service was stopping, and the service kept answering queries for a printer with
nothing listening. A send that failed while the responder was answering ended
its receive loop for good, with nothing in the log. And under the Windows
service control manager nothing told Windows that a started service had failed.
The first made a sentence of `REQ-ADV-021` false, and the reading of the README
earlier the same day did not catch it. Nine new tests cover what can be tested
without Windows. What Windows shows when the process ends is not measured.**

*(Status note, 2026-10-08, by Claude, Claude Opus 5.5: run under Windows that
day, and measured. See the note under "A run that would show it".)*

## How it was found

The README was read against the code on 2026-10-07
([finding](2026-10-07-the-readme-was-read-against-the-code.md)). That reading
listed one of these three, the last, as a known problem: a failure after start
is not reported to Windows. It had been noticed on 2026-09-22 and not carried
forward.

Writing the correction for it turned up the other two. The correction hands the
failure to a handler when `ServiceHost.RunAsync` ends with an error. Reading
`RunAsync` to see when it does end with an error showed that, while any part
was still running, it did not.

## What was wrong

### 1. A failed part did not stop the service

`RunAsync` starts the parts of the service as tasks: the responder's receive
loop, the handler for a name conflict, the printer watch, and a relay for each
client interface. It then waited for them with `Task.WhenAll`.

`Task.WhenAll` completes when every task has completed. If one task fails and
the others run on, it goes on waiting. So a part that failed was not noticed
until the service was stopped for some other reason.

- **`REQ-ADV-021` said otherwise.** "If a published address is gone when the
  listeners next open, the service logs why, says goodbye and stops, to be
  restarted." The code for it, written on 2026-09-30 and committed the next day
  as `2c93864`, logs `Could not listen on every address published for` the
  interface, ending "Stopping, so that no address is published with nothing
  listening behind it", and throws. The throw ended that interface's relay and
  nothing else. The responder went on answering, the addresses stayed
  published, and nothing listened on them: the state the requirement exists to
  prevent. No goodbye was sent. The relay did not come back at a later
  withdrawal and restore either, because its task had ended.
- **The code said so in three more places.** The marker for `REQ-ADV-021` on
  the method that opens the listeners reads "the failure is logged and the
  service stops"; the remark above it and the header of `ListenPlan.cs` say the
  service "says goodbye" and stops. None was true until today. All three are
  true now and are left as written.
- **It was on record for the responder.**
  [The finding of 2026-09-18](2026-09-18-the-printer-side-interface-goes-away.md)
  says a faulted responder "is never observed", because `ServiceHost` awaits
  `Task.WhenAll(running)`. That was reasoned and not observed, and it was not
  connected to the design of 2026-09-30.
- **A comment said the opposite.** `PrinterWatch.cs` said "a faulted watch
  brings down the whole service through Task.WhenAll". It would have brought
  nothing down.
- **The reading of the README missed it.** Claude read `ServiceHost.cs` that
  morning, saw the log line and the `throw`, and took the service to stop. It
  did not follow the exception to the wait.

Never seen on a machine. No finding records a listener that could not be
opened.

### 2. A send that failed ended the responder's loop

`MdnsResponder.ServeAsync` guarded the receive against a socket error and not
the answer. Its marker for `REQ-LIF-005` said "A transient socket error is
counted and the loop continues; only cancellation ends it." A send that threw
inside `HandleAsync` ended the method. By 1 above, nothing noticed: the service
stayed up, kept its listeners open, and answered no query until it was
restarted, with nothing in its log.

Recorded on 2026-09-18 in the same finding, as reasoned and not observed. It is
still not observed.

Correcting 1 without correcting this would have made it worse: one failed send
would then have stopped the whole service.

### 3. Windows was not told

As the finding of 2026-09-22 describes and the reading of the README restated:
`OnStart` returns as soon as the work is under way, so Windows has been told
the service is running before anything later can fail. A later failure was
logged as `Service stopped because of an error`, and the process stayed.

## What changed

- **`ServiceHost.RunPartsAsync`,** new, replaces the wait. When a part ends
  with an error it logs `A part of the service failed, so the whole service is
  stopping`, stops the other parts, waits for them, and lets that first failure
  out of `RunAsync`. `RunAsync` sends the goodbye on the way out, as it always
  has when it ends. A part that ends without an error, as the conflict handler
  does, stops nothing.
- **`MdnsResponder.ServeAsync`** guards the answer as it guards the receive. A
  socket error in either is reported to the service through a new constructor
  parameter, once for each different error, and the service logs it as a
  warning. The loop goes on.
- **`ServiceLifecycle`** takes a failure handler. It is told when the work ends
  with an error nobody asked for, after the error is logged. It is not told on
  a stop, or of a failure while a stop is under way.
- **`WindowsService`** supplies that handler. It logs one more line and ends
  the process with exit code 4.
- **`Program`:** run from a console, a failure of any kind after the
  configuration has been accepted is now logged and returns 4. Before, three
  kinds of exception did that and any other ended the process as an unhandled
  exception. The last error line now begins `Stopped because of an error:`
  and gives the exception's type. For those three kinds it used to begin
  `Could not start:`, which would now be false of a listener that failed after
  the service had announced itself, and from that place in the code the two
  cases cannot be told apart. The usage text for exit code 4 is rewritten.
- **`PrinterWatch.cs`:** the comment corrected. No code.

No requirement was added. `REQ-ADV-021`, `REQ-LIF-004` and `REQ-LIF-005` each
said what the code now does, and each has a note in the README saying since
when. `RunPartsAsync` carries markers for the first two.

## Why the process is ended, and `ServiceBase.Stop` is not called

Two ways of telling Windows were considered.

**`ServiceBase.Stop()`, with `ExitCode` set first.** It reports the service as
stopped with an error code. It was not used, because of what the source of
`System.ServiceProcess.ServiceController` shows (`ServiceBase.cs`, tag `v10.0.0`
of `dotnet/runtime`, read on 2026-10-07; the file on the `release/10.0` branch
is identical). After `OnStart` returns, `ServiceBase` writes its own event-log
entry, then sets its state to running, and another thread then reports that
state to Windows. None of that is done under the lock `Stop()` takes. A failure
arriving in that interval would have `Stop()` report the service stopped and
the start-up code then report it running. A socket that cannot be opened fails
within milliseconds of the start, so the interval is not out of reach.

**Ending the process with a non-zero exit code.** This is what Microsoft's
guidance for a .NET Windows service gives. It calls a process that stays after
its work has failed a zombie, and says: "In order for the Windows Service
Management system to leverage configured recovery options, we need to terminate
the process with a non-zero exit code"
([Create Windows Service using BackgroundService](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service),
read 2026-10-07). It has no interval to get wrong.

By the time the handler runs, `RunAsync` has ended. If it had got as far as
starting its parts, it has sent the goodbye, or logged that it could not. The
log file is flushed entry by entry, so what was written is kept.

A configuration that is refused is a different case and is unchanged: it is
reported from inside `OnStart`, which `ServiceBase` handles itself
(`REQ-LIF-007`).

## The tests, and each one broken on purpose

Nine tests, in three files. Each mutation changed one place in the code; the
suite was built and run, and the file restored. In each run the tests named
failed and every other test passed.

| Mutation | Tests that failed |
| --- | --- |
| The answer is not guarded in `ServeAsync`, as before | "A send that fails while answering does not end the receive loop" |
| Every socket error is reported, not each different one once | the same |
| The lifecycle never tells the handler | "A failure nobody asked for reaches the failure handler, after it is logged"; "A failure handler that fails is logged, and nothing escapes" |
| The handler is told during a stop as well | "A failure while a stop is under way is left to Stop, and the handler is not told" |
| The failure is not logged before the handler runs | "A failure nobody asked for reaches the failure handler, after it is logged"; and the older "A startup failure is recorded rather than lost" |
| The handler is called outside its guard | "A failure handler that fails is logged, and nothing escapes" |
| The wait is `Task.WhenAll` again, as before | "When one part of the service fails, the others are stopped and the failure is what comes out"; "The first failure is the one that comes out, whatever the other parts do while stopping" |
| The other parts are not stopped on a failure | the same two |
| What comes out is the first failure in the list, not the first to happen | "The first failure is the one that comes out, whatever the other parts do while stopping" |
| A part that ends without an error counts as a failure | "A part that ends without an error does not stop the others" |

With the wait put back to `Task.WhenAll`, the first of those tests waited its
five seconds for the failure to come out, and it did not. That is the old
behaviour, shown.

**Not broken, because no test covers them:**

- `WindowsService.EndTheProcess`. It needs the service control manager.
- That the goodbye follows a failed part. `RunAsync` sends it in a `finally`
  block, on every way out, and no test runs `RunAsync`: it opens real sockets.
  The tests of `RunPartsAsync` show that the failure comes out of the wait.
- The new `catch` in `Program`, and the usage text.
- The line `ServiceHost` logs for a responder's socket error. The responder's
  report to its callback is tested; the wiring of the callback to the log is one
  expression, read and not tested.
- "A receive that fails is reported, and the loop goes on" passes with or
  without the change to the answer, as it should. It is there because nothing
  ran `ServeAsync` in a test before today.

## Where it was measured

**In Claude's workspace:** Linux, .NET SDK 10.0.112, the service compiled from
the repository's files against the copy of its one package inside the SDK.

- The responder suite: 123 passed, where it was 121.
- The service suite: 105 passed, where it was 98.
- The other five as before: 28, 25, 34 with 9 skipped, 61 and 41.
- SpecCheck, given that build: 99 requirements, 23 assemblies, 18 evidence
  rows, 432 test records where there were 422, and the same two gaps as before,
  for the requirements whose tests skip there. The coverage matrix changes in
  three rows: `REQ-ADV-021`, `REQ-LIF-004` and `REQ-LIF-005` each gain markers
  or tests.
- Every project built with no warning.

**On the development machine,** Edwin ran every suite and SpecCheck before
committing. Those figures are in the commit message.

## What this does not show

- **Anything under the service control manager.** `EndTheProcess` has been
  compiled and never run. Not measured: what `sc.exe query` shows afterwards;
  what `sc.exe start` prints when the failure comes within the start; which
  event Windows records; whether a recovery action set with `sc.exe failure`
  then runs; and whether ending the process from that thread, in a process
  whose main thread is inside `ServiceBase.Run`, behaves as expected.
- **Any of the three faults on a machine,** before or after. A listener that
  cannot be opened, and a send that fails, have never been provoked.
- **That a failed send is always worth surviving.** If the client interface has
  lost its address, every answer fails, and the service now logs that once and
  goes on, answering nothing. It does not follow address changes
  (`REQ-ADV-021`), and a restart is still what brings it back.
- **A part that ends as cancelled when no stop was asked for.** It is taken as
  an ordinary end and stops nothing, as the old wait took it. Each part was
  read for how it handles cancellation and none was seen to end that way; no
  test tries it.
- **The counter.** A socket error while receiving is still counted under
  `Unparseable`, a counter named for something else. Not changed.
- **The connections open when a part fails.** They are cut by the stop, like
  any connection open at a stop, and get no ending line in the log
  (`REQ-PXY-009`).

## A run that would show it

Not yet made. With another program already listening on TCP 631 at the client
interface's address, start the installed service. It should probe, announce,
fail to open its listener, say goodbye, and end. Its log should carry four
error lines, in this order, beginning: `Could not listen on every address
published for`; `A part of the service failed, so the whole service is
stopping`; `Service stopped because of an error`; and `Ending the process with
exit code 4`. `sc.exe query SecretPrinter` should then not read `RUNNING`.
Whether the second listener is refused as expected is itself not measured.

*(Status note, 2026-10-08, by Claude, Claude Opus 5.5: made, on a second
machine, on an install from the published pre-release `0.1.0-rc.3`, with
another program holding TCP 631 at the client interface's address. The second
listener was refused. The service probed, announced, failed to open its
listener, said goodbye and ended, about three seconds after it started, and
its log carries the four error lines above in that order. `sc.exe start` had
printed `START_PENDING`. `sc.exe query` then read `STOPPED`, with
`WIN32_EXIT_CODE` 1067, "The process terminated unexpectedly", and
`SERVICE_EXIT_CODE` 0; the System log recorded event 7034, "The SecretPrinter
service terminated unexpectedly"; and the Application log held nothing naming
SecretPrinter. Whether a recovery action set with `sc.exe failure` would run is
still not measured, and the cause `REQ-ADV-021` names, an address that has
gone, was not tried. See [the finding](2026-10-08-the-published-pre-release-was-installed-from-nothing-by-the-document.md).)*

// -----------------------------------------------------------------------------
// StartupWaitTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-22, for REQ-LIF-008.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds startup's two waits to what REQ-LIF-008 says: the printer-side
//   interface is waited for until it is usable, the printer is asked again on
//   the REQ-RES-008 schedule until it answers, and each wait speaks when it
//   begins, when its reason changes and when it ends - not on every check.
//
//   The adapters are supplied through a fake inventory that each test changes
//   from inside the supplied delay, so an adapter can go from down to up
//   between two checks without anything sleeping. The printer's answers are
//   supplied the same way.
//
// What these tests do NOT prove:
//   That ServiceHost.RunAsync calls these two methods before it opens the
//   responder's socket, builds the advertisement or starts the listener. That
//   is established by reading RunAsync, where each is one call, and on
//   hardware by starting the service with the printer-side adapter down.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class StartupWaitTests
{
    private const string Adapter = "Wi-Fi 2";

    private static readonly IPAddress PrinterSideAddress = IPAddress.Parse("192.168.12.136");

    private static readonly DnsName IppsInstance =
        new(["EPSON ET-3760 Series", "_ipps", "_tcp", "local"]);

    /// <summary>An inventory a test can change between two checks.</summary>
    private sealed class ChangingInventory(params LocalAdapter[] adapters) : IInterfaceInventory
    {
        public IReadOnlyList<LocalAdapter> Adapters { get; set; } = adapters;
    }

    /// <summary>The printer-side adapter, holding one address in one condition.</summary>
    private static LocalAdapter PrinterSide(bool up, IPAddress? address, LocalAddressCondition condition)
    {
        IPAddress[] addresses = address is null ? Array.Empty<IPAddress>() : new[] { address };
        LocalIPv4Address[] conditions = address is null
            ? Array.Empty<LocalIPv4Address>()
            : new[] { new LocalIPv4Address(address, condition) };

        return new LocalAdapter(Adapter, 49, up, true, addresses, null, conditions);
    }

    private static LocalAdapter Usable() => PrinterSide(true, PrinterSideAddress, LocalAddressCondition.Preferred);

    /// <summary>
    /// A delay that records how long it was asked to wait and, after the given
    /// number of waits, applies a change. Nothing sleeps.
    /// </summary>
    private static Func<TimeSpan, CancellationToken, Task> Recording(
        List<TimeSpan> waits, params (int AfterWait, Action Change)[] changes) =>
        (wait, token) =>
        {
            token.ThrowIfCancellationRequested();
            waits.Add(wait);

            foreach ((int afterWait, Action change) in changes)
            {
                if (afterWait == waits.Count)
                {
                    change();
                }
            }

            return Task.CompletedTask;
        };

    private static int Warnings(CollectingServiceLog log) =>
        log.Entries.Count(e => e.Level == LogLevel.Warning);

    private static bool Logged(CollectingServiceLog log, LogLevel level, string text) =>
        log.Entries.Any(e => e.Level == level && e.Message.Contains(text, StringComparison.Ordinal));

    private static PrinterAtStartup Answer()
    {
        var printer = new ResolvedPrinter(
            IppsInstance,
            new DnsName(["EPSON3EA18A", "local"]),
            IPAddress.Parse("192.168.12.180"),
            631,
            [],
            DateTimeOffset.UnixEpoch,
            120);

        return new PrinterAtStartup(printer, printer);
    }

    // ---- The printer-side interface ------------------------------------------

    [TestCase("A printer-side adapter that is down is waited for, and used once it is up")]
    [Requirement("REQ-LIF-008")]
    public static void Down_adapter_is_waited_for()
    {
        var machine = new ChangingInventory(PrinterSide(false, PrinterSideAddress, LocalAddressCondition.Preferred));
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        MdnsInterface found = StartupWait.ForPrinterInterfaceAsync(
                Adapter, machine, log,
                Recording(waits, (3, () => machine.Adapters = [Usable()])),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(PrinterSideAddress, found.Address, "the interface returned is the adapter's address once it is up");
        Assert.Equal(49, found.Index, "and its index");
        Assert.Equal(3, waits.Count, "it was checked until it came up, and no longer");
        Assert.True(waits.All(w => w == StartupWait.AdapterCheckInterval), "every check waits the documented interval");
        Assert.Equal(1, Warnings(log), "the wait is logged once, not once per check");
        Assert.True(Logged(log, LogLevel.Warning, "is not up"), "and the warning gives the reason");
        Assert.True(Logged(log, LogLevel.Warning, "Nothing is offered to the client network"),
            "and says what the service is not doing meanwhile");
        Assert.True(Logged(log, LogLevel.Information, "No longer waiting"), "the end of the wait is logged");
    }

    [TestCase("An adapter already usable is used at once, and its resolution is logged")]
    [Requirement("REQ-LIF-008")]
    [Requirement("REQ-OBS-001")]
    public static void Usable_adapter_is_used_at_once()
    {
        var machine = new ChangingInventory(Usable());
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        _ = StartupWait.ForPrinterInterfaceAsync(Adapter, machine, log, Recording(waits), CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(0, waits.Count, "a usable adapter is not waited for");
        Assert.Equal(0, Warnings(log), "and no wait is reported");
        Assert.True(Logged(log, LogLevel.Information, "printer interface: Wi-Fi 2 -> 192.168.12.136 (index 49)"),
            "the log must show which address and index the configured name resolved to");
    }

    [TestCase("A link-local address is waited out until the printer network gives a real one")]
    [Requirement("REQ-LIF-008")]
    public static void Link_local_address_is_waited_out()
    {
        var machine = new ChangingInventory(
            PrinterSide(true, IPAddress.Parse("169.254.34.71"), LocalAddressCondition.Preferred));
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        MdnsInterface found = StartupWait.ForPrinterInterfaceAsync(
                Adapter, machine, log,
                Recording(waits, (1, () => machine.Adapters = [Usable()])),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(PrinterSideAddress, found.Address, "a 169.254 address must never be bound");
        Assert.True(Logged(log, LogLevel.Warning, "link-local"), "and the reason for waiting must say why");
    }

    [TestCase("An address that is still tentative is not used until it is preferred")]
    [Requirement("REQ-LIF-008")]
    public static void Tentative_address_is_waited_out()
    {
        var machine = new ChangingInventory(PrinterSide(true, PrinterSideAddress, LocalAddressCondition.Tentative));
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        _ = StartupWait.ForPrinterInterfaceAsync(
                Adapter, machine, log,
                Recording(waits, (1, () => machine.Adapters = [Usable()])),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(1, waits.Count, "the tentative address held the wait until it became preferred");
        Assert.True(Logged(log, LogLevel.Warning, "tentative"), "and the reason names the condition");
    }

    [TestCase("The adapter wait is logged again only when its reason changes")]
    [Requirement("REQ-LIF-008")]
    public static void Adapter_wait_speaks_only_on_change()
    {
        var machine = new ChangingInventory(PrinterSide(false, PrinterSideAddress, LocalAddressCondition.Preferred));
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        _ = StartupWait.ForPrinterInterfaceAsync(
                Adapter, machine, log,
                Recording(
                    waits,
                    (3, () => machine.Adapters = [PrinterSide(true, null, LocalAddressCondition.Unknown)]),
                    (5, () => machine.Adapters = [Usable()])),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(5, waits.Count, "three checks down, two with no address, then usable");
        Assert.Equal(2, Warnings(log), "one line per reason, not one per check");
        Assert.True(Logged(log, LogLevel.Warning, "has no IPv4 address"), "the second reason is reported when it appears");
    }

    [TestCase("Stopping during the adapter wait ends it without a verdict")]
    [Requirement("REQ-LIF-008")]
    public static void Stop_ends_the_adapter_wait()
    {
        var machine = new ChangingInventory(PrinterSide(false, PrinterSideAddress, LocalAddressCondition.Preferred));
        using var stopping = new CancellationTokenSource();
        var log = new CollectingServiceLog();

        Assert.Throws<OperationCanceledException>(
            () => _ = StartupWait.ForPrinterInterfaceAsync(
                    Adapter, machine, log,
                    (_, token) =>
                    {
                        stopping.Cancel();
                        token.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    },
                    stopping.Token)
                .GetAwaiter().GetResult(),
            "a stop request must end the wait rather than be absorbed by it");

        Assert.False(Logged(log, LogLevel.Information, "printer interface:"),
            "no interface may be reported as resolved when none was");
    }

    // ---- The printer -------------------------------------------------------------

    [TestCase("A printer that does not answer at startup is asked again after 1, 2, 4, 8 and 16 seconds")]
    [Requirement("REQ-LIF-008")]
    public static void Silent_printer_is_asked_on_the_schedule()
    {
        int attempts = 0;
        var waits = new List<TimeSpan>();
        var log = new CollectingServiceLog();

        PrinterAtStartup found = StartupWait.ForPrinterAsync(
                _ =>
                {
                    attempts++;
                    return attempts <= 5
                        ? throw new PrinterResolutionException($"'{IppsInstance}' did not answer within 5s.")
                        : Task.FromResult(Answer());
                },
                log,
                Recording(waits),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.NotNull(found, "the answer is returned once the printer gives one");
        Assert.Equal(6, attempts, "five unanswered, then answered");
        TimeSpan[] schedule =
        [
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16),
        ];

        Assert.True(
            waits.SequenceEqual(schedule),
            "one second, then each wait double the last, as REQ-RES-008 requires while the printer is away");
        Assert.Equal(1, Warnings(log), "the first unanswered attempt is logged, and the rest are not");
        Assert.True(Logged(log, LogLevel.Warning, IppsInstance.ToString()),
            "the warning names the instance that did not answer");
        Assert.True(Logged(log, LogLevel.Warning, "misspelled"),
            "and says plainly that a wrong instance name looks the same");
        Assert.True(Logged(log, LogLevel.Information, "answered after 5 unanswered attempt(s)"),
            "the end of the wait is logged, with how many attempts went unanswered");
    }

    [TestCase("The wait between startup queries stops growing at one hour")]
    [Requirement("REQ-LIF-008")]
    public static void Printer_wait_is_capped_at_an_hour()
    {
        int attempts = 0;
        var waits = new List<TimeSpan>();

        _ = StartupWait.ForPrinterAsync(
                _ =>
                {
                    attempts++;
                    return attempts <= 16
                        ? throw new PrinterResolutionException("no answer")
                        : Task.FromResult(Answer());
                },
                new CollectingServiceLog(),
                Recording(waits),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(PrinterReachability.MaximumRetryInterval, waits.Max(), "no wait exceeds the cap");
        Assert.Equal(TimeSpan.FromMinutes(60), waits[^1], "and the cap is one hour, as REQ-RES-008 states");
        Assert.Equal(TimeSpan.FromSeconds(2048), waits[11], "the doubling runs until it would pass the cap");
        Assert.Equal(TimeSpan.FromMinutes(60), waits[12], "and the next wait is the cap itself");
    }

    [TestCase("A socket failure while the printer is waited for counts as no answer")]
    [Requirement("REQ-LIF-008")]
    public static void Socket_failure_counts_as_no_answer()
    {
        int attempts = 0;
        var waits = new List<TimeSpan>();

        PrinterAtStartup found = StartupWait.ForPrinterAsync(
                _ =>
                {
                    attempts++;
                    return attempts == 1
                        ? throw new SocketException((int)SocketError.AddressNotAvailable)
                        : Task.FromResult(Answer());
                },
                new CollectingServiceLog(),
                Recording(waits),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.NotNull(found, "a send that could not leave the machine must not end the wait");
        Assert.Equal(1, waits.Count, "it is followed by the same one-second wait as a silence");
    }

    [TestCase("Stopping during the printer wait ends it without a verdict")]
    [Requirement("REQ-LIF-008")]
    public static void Stop_ends_the_printer_wait()
    {
        using var stopping = new CancellationTokenSource();
        var log = new CollectingServiceLog();

        Assert.Throws<OperationCanceledException>(
            () => _ = StartupWait.ForPrinterAsync(
                    _ => throw new PrinterResolutionException("no answer"),
                    log,
                    (_, token) =>
                    {
                        stopping.Cancel();
                        token.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    },
                    stopping.Token)
                .GetAwaiter().GetResult(),
            "a stop request must end the wait rather than be absorbed by it");

        Assert.False(Logged(log, LogLevel.Information, "answered"), "no answer may be reported when none came");
    }
}

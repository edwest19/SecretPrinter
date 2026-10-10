// -----------------------------------------------------------------------------
// AdapterWatchTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-10, for the SecretPrinter project, for REQ-RES-010.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds the adapter watch to what README REQ-RES-010 says of it: it examines
//   the printer-side adapter every five seconds, by the rule the startup wait
//   uses, and reports the interface only when it has become usable after being
//   seen not usable, or when its address or index has changed. A watch that
//   reported every examination would start the printer's questions over every
//   five seconds; one that never reported would change nothing.
//
//   The examination is supplied, so no test reads this machine's adapters.
//   The interfaces are made up, with addresses from 192.0.2.0/24, the block
//   RFC 5737 sets aside for documentation.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class AdapterWatchTests
{
    private static readonly MdnsInterface Usable =
        new("Wi-Fi", IPAddress.Parse("192.0.2.42"), 7, AddressFamily.InterNetwork);

    private static readonly MdnsInterface MovedAddress =
        new("Wi-Fi", IPAddress.Parse("192.0.2.43"), 7, AddressFamily.InterNetwork);

    private static readonly (MdnsInterface? Usable, string? Reason) NotUsable =
        (null, "Interface 'Wi-Fi' holds 169.254.1.1 but is not up.");

    /// <summary>Collects what the watch chose to say.</summary>
    private sealed class RecordingLog : IServiceLog
    {
        public List<string> Lines { get; } = [];

        public void Write(LogLevel level, string message) => Lines.Add($"{level}: {message}");
    }

    /// <summary>
    /// A watch that starts from <see cref="Usable"/> and examines the adapter by
    /// taking the next result from <paramref name="results"/> each time.
    /// </summary>
    private static (AdapterWatch Watch, List<MdnsInterface> Reported, RecordingLog Log) Build(
        params Func<(MdnsInterface? Usable, string? Reason)>[] results)
    {
        var reported = new List<MdnsInterface>();
        var log = new RecordingLog();
        int next = 0;

        var watch = new AdapterWatch(
            Usable,
            () => results[next++](),
            reported.Add,
            log);

        return (watch, reported, log);
    }

    private static Func<(MdnsInterface? Usable, string? Reason)> Is((MdnsInterface? Usable, string? Reason) result) =>
        () => result;

    [TestCase("An interface seen not usable and then usable again is reported once")]
    [Requirement("REQ-RES-010")]
    public static void Usable_again_is_reported_once()
    {
        (AdapterWatch watch, List<MdnsInterface> reported, _) = Build(
            Is(NotUsable), Is(NotUsable), Is((Usable, null)), Is((Usable, null)));

        for (int i = 0; i < 4; i++)
        {
            watch.CheckOnce();
        }

        Assert.Equal(1, reported.Count, "reported when it became usable, and not again while it stayed so");
        Assert.Equal(Usable, reported[0], "as it was found");
    }

    [TestCase("An interface that stays usable is never reported")]
    [Requirement("REQ-RES-010")]
    public static void Staying_usable_is_not_reported()
    {
        (AdapterWatch watch, List<MdnsInterface> reported, RecordingLog log) = Build(
            Is((Usable, null)), Is((Usable, null)), Is((Usable, null)));

        for (int i = 0; i < 3; i++)
        {
            watch.CheckOnce();
        }

        Assert.Equal(0, reported.Count, "nothing changed, so the printer's questions are not started over");
        Assert.Equal(0, log.Lines.Count, "and nothing is said");
    }

    [TestCase("A usable interface found on a new address is reported")]
    [Requirement("REQ-RES-010")]
    public static void A_new_address_is_reported()
    {
        (AdapterWatch watch, List<MdnsInterface> reported, _) = Build(Is((MovedAddress, null)), Is((MovedAddress, null)));

        watch.CheckOnce();
        watch.CheckOnce();

        Assert.Equal(1, reported.Count, "the adapter came back on another address, which is a change, once");
        Assert.Equal(MovedAddress, reported[0], "reported with the new address");
    }

    [TestCase("An examination that fails is logged once, is not taken for a change, and does not end the watch")]
    [Requirement("REQ-RES-010")]
    public static void A_failed_examination_is_survived()
    {
        Func<(MdnsInterface? Usable, string? Reason)> fails =
            () => throw new InvalidOperationException("the adapter list could not be read");

        (AdapterWatch watch, List<MdnsInterface> reported, RecordingLog log) = Build(
            fails, fails, Is((Usable, null)), Is(NotUsable), Is((Usable, null)));

        for (int i = 0; i < 5; i++)
        {
            watch.CheckOnce();
        }

        List<string> warnings = log.Lines.FindAll(line => line.StartsWith("Warning: ", StringComparison.Ordinal));

        Assert.Equal(1, warnings.Count, "the same failure twice is logged once");
        Assert.True(warnings[0].Contains("the adapter list could not be read", StringComparison.Ordinal),
            "naming what failed");
        Assert.Equal(1, reported.Count,
            "a failure says nothing about the adapter: usable before and after it is no change; usable after not usable is one");
    }

    [TestCase("A failed examination that ends and comes back is logged again")]
    [Requirement("REQ-RES-010")]
    public static void A_failure_that_comes_back_is_logged_again()
    {
        Func<(MdnsInterface? Usable, string? Reason)> fails =
            () => throw new InvalidOperationException("the adapter list could not be read");

        (AdapterWatch watch, List<MdnsInterface> reported, RecordingLog log) = Build(
            fails, Is((Usable, null)), fails);

        for (int i = 0; i < 3; i++)
        {
            watch.CheckOnce();
        }

        Assert.Equal(2, log.Lines.FindAll(line => line.StartsWith("Warning: ", StringComparison.Ordinal)).Count,
            "the failure, an examination that worked, and the same failure again: logged both times");
        Assert.Equal(0, reported.Count, "usable before and after: no change");
    }

    [TestCase("The watch examines the adapter every five seconds until it is stopped")]
    [Requirement("REQ-RES-010")]
    public static async Task It_examines_every_five_seconds()
    {
        var waits = new List<TimeSpan>();
        int examined = 0;
        using var stop = new CancellationTokenSource();

        var watch = new AdapterWatch(
            Usable,
            () =>
            {
                examined++;
                return (Usable, null);
            },
            _ => { },
            new RecordingLog(),
            (wait, token) =>
            {
                waits.Add(wait);
                if (waits.Count == 4)
                {
                    stop.Cancel();
                    return Task.FromCanceled(token);
                }

                return Task.CompletedTask;
            });

        await watch.WatchAsync(stop.Token).ConfigureAwait(false);

        Assert.Equal(4, waits.Count, "it waited four times, the last ended by the stop");
        Assert.True(waits.TrueForAll(w => w == TimeSpan.FromSeconds(5)), "each wait is five seconds");
        Assert.Equal(TimeSpan.FromSeconds(5), AdapterWatch.CheckInterval, "the interval the startup wait uses too");
        Assert.Equal(3, examined, "it examined the adapter after each wait that ran to its end");
    }
}

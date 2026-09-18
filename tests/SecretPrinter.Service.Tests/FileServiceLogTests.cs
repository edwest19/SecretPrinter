// -----------------------------------------------------------------------------
// FileServiceLogTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-18, for REQ-OBS-009 and REQ-OBS-010. Reviewed by a human before
// merge.
//
// Purpose:
//   Checks the log file a service leaves behind: that entries arrive, that they
//   survive a restart, that they can be read while the service is still
//   running, and that nothing learned from the network can forge one.
//
// These tests write real files:
//   Each one creates its own temporary folder and deletes it afterwards. A fake
//   file system would test the fake. The thing being claimed here - "you can
//   open the file and read what happened" - is a claim about a real file, and it
//   is checked by opening a real file.
// -----------------------------------------------------------------------------

using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class FileServiceLogTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 18, 12, 34, 56, TimeSpan.Zero);

    /// <summary>Runs a test body with a temporary folder that is always removed.</summary>
    private static void InTemporaryFolder(Action<string> body)
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("secretprinter-log-");

        try
        {
            body(folder.FullName);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    // ---- What reaches the file ----------------------------------------------

    [TestCase("Each entry becomes one line in the file")]
    [Requirement("REQ-OBS-009")]
    public static void Writes_one_line_per_entry() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");

        using (var log = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            log.Write(LogLevel.Information, "first");
            log.Write(LogLevel.Warning, "second");
            log.Write(LogLevel.Error, "third");
        }

        string[] lines = File.ReadAllLines(path);

        Assert.Equal(3, lines.Length, "three entries must produce three lines");

        // The exact text is pinned here rather than rebuilt from LogLine, so
        // that a change to the format has to be made deliberately in two places.
        Assert.Equal(
            "2026-09-18 12:34:56Z  INFORMATION  first",
            lines[0],
            "the file must carry the timestamp, the level and the message, in the console's format");

        Assert.True(
            lines[1].Contains("WARNING", StringComparison.Ordinal)
            && lines[1].EndsWith("second", StringComparison.Ordinal),
            "a warning must be recorded as one, with its message intact");

        Assert.True(
            lines[2].Contains("ERROR", StringComparison.Ordinal)
            && lines[2].EndsWith("third", StringComparison.Ordinal),
            "and an error likewise - errors go to the same file, not only to standard error");
    });

    [TestCase("A restart adds to the file rather than erasing it")]
    [Requirement("REQ-OBS-009")]
    public static void Appends_rather_than_replacing() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");

        using (var first = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            first.Write(LogLevel.Information, "before the restart");
        }

        using (var second = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            second.Write(LogLevel.Information, "after the restart");
        }

        string[] lines = File.ReadAllLines(path);

        Assert.Equal(2, lines.Length, "the second run must not have truncated the first");
        Assert.True(lines[0].EndsWith("before the restart", StringComparison.Ordinal),
            "what happened yesterday must still be readable today");
        Assert.True(lines[1].EndsWith("after the restart", StringComparison.Ordinal),
            "and today's entries must follow it");
    });

    [TestCase("Entries can be read while the service still holds the file")]
    [Requirement("REQ-OBS-009")]
    public static void Entries_are_readable_while_running() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");

        using var log = new FileServiceLog(path, clock: new FixedClock(Noon));
        log.Write(LogLevel.Information, "the service is still running");

        // Both halves of the claim are here: the entry has been flushed, and
        // the share mode lets another program open the file. Without either,
        // watching a running service would mean stopping it first.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        Assert.True(
            reader.ReadToEnd().Contains("the service is still running", StringComparison.Ordinal),
            "an operator must be able to read the log of a service that has not stopped");
    });

    // ---- One entry is one line ----------------------------------------------

    [TestCase("A line break in a message cannot forge an entry")]
    [Requirement("REQ-OBS-010")]
    public static void A_line_break_cannot_forge_an_entry() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");

        // What a hostile device on either network could try: name itself so that
        // the log appears to contain a line the service never wrote.
        const string Forgery = "printer\r\n2026-09-18 00:00:00Z  INFORMATION  Job: relaying nowhere";

        using (var log = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            log.Write(LogLevel.Information, $"Resolved {Forgery}.");
        }

        string[] lines = File.ReadAllLines(path);

        Assert.Equal(1, lines.Length, "one entry must occupy exactly one line, whatever it contains");
        Assert.True(lines[0].Contains("\\x0D\\x0A", StringComparison.Ordinal),
            "the line break must appear escaped, so a reader can see what was really there");
        Assert.True(lines[0].StartsWith("2026-09-18 12:34:56Z  INFORMATION  Resolved printer\\x0D\\x0A",
                        StringComparison.Ordinal),
            "and the rest of the message must survive intact rather than being truncated at the break");
    });

    [TestCase("Ordinary text is written exactly as given")]
    [Requirement("REQ-OBS-010")]
    public static void Ordinary_text_is_unchanged()
    {
        // Escaping that altered ordinary messages would be worse than the
        // problem it solves: instance names contain spaces, brackets and
        // punctuation, and an operator compares them against what the printer
        // shows.
        const string Ordinary = "Job: relaying 192.168.1.50:49231 -> SecretPrinter (ET-3760) [TLS 1.2]";

        Assert.Equal(Ordinary, LogLine.Escape(Ordinary), "nothing printable may be rewritten");

        Assert.Equal(
            "2026-09-18 12:34:56Z  INFORMATION  " + Ordinary,
            LogLine.Format(Noon, LogLevel.Information, Ordinary),
            "and the formatted line must be the message with a stamp and a level in front of it");
    }

    // ---- Failure does not propagate -----------------------------------------

    [TestCase("Writing after the file is closed is reported, not thrown")]
    [Requirement("REQ-OBS-009")]
    public static void Writing_after_close_is_reported() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");
        var console = new CollectingServiceLog();

        var log = new FileServiceLog(path, console, new FixedClock(Noon));
        log.Write(LogLevel.Information, "while open");
        log.Dispose();

        // Shutdown closes the file while a relay may still be finishing. An
        // exception here would surface as a failed print job, which is a far
        // worse outcome than a missing log line.
        log.Write(LogLevel.Information, "after close");
        log.Write(LogLevel.Information, "after close again");

        Assert.Equal(1, console.Entries.Count(e => e.Level == LogLevel.Error),
            "the loss must be reported once - not per entry, which would bury the log it lands in");

        Assert.True(
            console.Entries.Any(e => e.Message.Contains("may be missing", StringComparison.Ordinal)),
            "and the report must say that entries are being lost, naming the file");

        Assert.Equal(1, File.ReadAllLines(path).Length, "what was written before the close is still there");
    });

    [TestCase("A missing folder is refused when the file is opened")]
    [Requirement("REQ-OBS-009")]
    public static void A_missing_folder_is_refused() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "no-such-folder", "secretprinter.log");

        // The service creates no folders, so a mistyped path must stop it at
        // the point the path was given rather than at the first entry nobody
        // reads. Program.cs catches IOException here and exits 5.
        Assert.Throws<IOException>(
            () => new FileServiceLog(path).Dispose(),
            "a path whose folder does not exist must be refused, not created");
    });

    // ---- Several sinks -------------------------------------------------------

    [TestCase("A composite log writes to every sink")]
    [Requirement("REQ-OBS-009")]
    public static void Composite_writes_to_every_sink() => InTemporaryFolder(folder =>
    {
        string path = Path.Combine(folder, "secretprinter.log");
        var console = new CollectingServiceLog();

        using (var file = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            // Declared concretely, not as IServiceLog: CA1859, and nothing here
            // needs the interface. What is being checked is that one Write
            // reaches both sinks.
            var log = new CompositeServiceLog(console, file);
            log.Write(LogLevel.Information, "to both");
        }

        Assert.Equal(1, console.Entries.Count, "the console sink must have received the entry");
        Assert.True(File.ReadAllLines(path)[0].EndsWith("to both", StringComparison.Ordinal),
            "and so must the file - adding a file must not take anything away from the console");
    });

    [TestCase("Concurrent writers produce whole lines")]
    [Requirement("REQ-OBS-009")]
    public static void Concurrent_writers_produce_whole_lines() => InTemporaryFolder(folder =>
    {
        const int Workers = 4;
        const int PerWorker = 100;

        string path = Path.Combine(folder, "secretprinter.log");

        using (var log = new FileServiceLog(path, clock: new FixedClock(Noon)))
        {
            Parallel.For(0, Workers, worker =>
            {
                for (int i = 0; i < PerWorker; i++)
                {
                    log.Write(LogLevel.Information, $"worker {worker} entry {i}");
                }
            });
        }

        string[] lines = File.ReadAllLines(path);

        // This cannot force the writes to overlap, so it does not prove the lock
        // is needed; it fails loudly if the lock is ever removed and they do.
        Assert.Equal(Workers * PerWorker, lines.Length, "every entry must produce exactly one line");
        Assert.Equal(Workers * PerWorker, lines.Distinct(StringComparer.Ordinal).Count(),
            "no line may be a fragment of two entries spliced together");
        Assert.True(
            lines.All(l => l.StartsWith("2026-09-18 12:34:56Z  INFORMATION  worker ", StringComparison.Ordinal)),
            "and every line must be a whole, well-formed entry");
    });
}

/// <summary>A clock that does not move, so the expected line is exact text.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

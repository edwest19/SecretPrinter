// -----------------------------------------------------------------------------
// FileServiceLog.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-18, for REQ-OBS-009. Reviewed by a human before merge.
//
// Purpose:
//   Writes the log to a file, so that a service running under the Windows
//   service control manager can still be read.
//
// Why this exists:
//   Under the control manager there is no console. Standard output goes
//   nowhere: not to a file, not to the event log, nowhere. Until this file
//   existed, a SecretPrinter installed as a service reported everything it knew
//   into a void - which interfaces it chose, what it advertised, why a job
//   failed - and the only way to see any of it was to stop the service and run
//   the same executable by hand in a console, which is a different run.
//
// What it deliberately does not do:
//   It does not choose a path. The operator names the file on the command line
//   and there is no default, for the same reason no other setting that decides
//   where something is written has one (REQ-CFG-001). A default would put a
//   file somewhere nobody chose, under an account whose write permissions
//   nobody checked.
//
//   It does not create the folder. Creating directories is a side effect on a
//   machine this service otherwise leaves alone, and a missing folder is far
//   more often a typed path than an intention.
//
//   It does not rotate, trim or delete anything. The log is written on startup,
//   on shutdown, and a handful of lines per print job; nothing is written per
//   mDNS query. A file that grows by a few lines a job does not need a rotation
//   policy, and one added on speculation would be code that deletes the
//   operator's data for reasons nobody measured.
//
// What the file contains:
//   Exactly what the console receives, formatted identically. Never print job
//   content: the log interface has no parameter that could carry any
//   (REQ-OBS-004). It does contain endpoint addresses and the names of devices
//   observed on both networks (REQ-OBS-005), so the file inherits whatever
//   permissions its folder has and choosing that folder is the operator's.
// -----------------------------------------------------------------------------

using System.Text;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Appends the log to a file the operator named.</summary>
[Requirement("REQ-OBS-009",
    "Writes every entry to the operator's file, flushed per entry and appended across restarts, giving a service with no console a readable log.")]
public sealed class FileServiceLog : IServiceLog, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly TimeProvider _clock;
    private readonly IServiceLog? _failures;
    private readonly object _gate = new();

    private bool _disposed;
    private bool _reportedFailure;

    /// <summary>Opens the file for appending.</summary>
    /// <param name="path">Where to write. The folder must already exist.</param>
    /// <param name="failures">
    /// Optional. Told once if writing to the file stops working, so the loss is
    /// reported somewhere rather than passing unnoticed. In the service this is
    /// the console sink.
    /// </param>
    /// <param name="clock">Optional. Supplied by tests.</param>
    /// <exception cref="IOException">
    /// The folder does not exist, or the file cannot be opened for writing.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The account this process runs as may not write there.
    /// </exception>
    public FileServiceLog(string path, IServiceLog? failures = null, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FilePath = System.IO.Path.GetFullPath(path);
        _clock = clock ?? TimeProvider.System;
        _failures = failures;

        // Append: a restart adds to the record rather than erasing it, which is
        // what makes "it did this yesterday too" an answerable question.
        //
        // FileShare.Read: another program may read the file while the service is
        // running - Get-Content -Wait, or a text editor - but a second writer
        // cannot open it, which would interleave two services' lines in one file.
        var options = new FileStreamOptions
        {
            Mode = FileMode.Append,
            Access = FileAccess.Write,
            Share = FileShare.Read,
        };

        _writer = new StreamWriter(FilePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), options)
        {
            // Every entry reaches the disk before Write returns. A service that
            // is killed, or a machine that loses power, keeps what was written
            // up to that moment - which is exactly the run an operator wants to
            // read afterwards. The cost is one flush per entry, and entries are
            // rare enough that it does not matter.
            AutoFlush = true,
        };
    }

    /// <summary>The file being written, as an absolute path.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Writes one entry. Never throws: a log that cannot be written must not be
    /// able to fail a print job (REQ-LIF-005).
    /// </summary>
    public void Write(LogLevel level, string message)
    {
        string line = LogLine.Format(_clock.GetUtcNow(), level, message);

        lock (_gate)
        {
            if (_disposed)
            {
                // Shutdown closes this sink while a relay may still be finishing.
                ReportOnce("the log file had already been closed");
                return;
            }

            try
            {
                _writer.WriteLine(line);
            }
            catch (IOException ex)
            {
                // A full disk, or a folder that has gone away. Reasoned from the
                // documented behaviour of StreamWriter, not observed here; the
                // closed-file path above is the one a test exercises.
                ReportOnce(ex.Message);
            }
            catch (ObjectDisposedException)
            {
                ReportOnce("the log file had already been closed");
            }
        }
    }

    /// <summary>Flushes and closes the file.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }

        // There is no finalizer, so this changes nothing at run time. It is the
        // form CA1816 asks for and it costs nothing to write.
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tells the other sink, once, that the file is no longer being written.
    /// Once, because a failure that repeats per entry would bury the log it was
    /// reported into.
    /// </summary>
    private void ReportOnce(string reason)
    {
        if (_reportedFailure || _failures is null)
        {
            return;
        }

        _reportedFailure = true;

        _failures.Write(
            LogLevel.Error,
            $"Writing to the log file {FilePath} failed, and entries from here on may be missing: {reason}");
    }
}

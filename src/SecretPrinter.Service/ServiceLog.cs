// -----------------------------------------------------------------------------
// ServiceLog.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Where the service says what it is doing.
//
// The shape is the safeguard:
//   Every method takes a string. None takes a byte array, a stream, or a span.
//   REQ-OBS-004 forbids print job content in logs at any level, and the surest
//   way to keep that promise is for the logger never to be handed any: a sink
//   can only write what it is given. A test asserts this interface's shape, so
//   adding an overload that could carry payload bytes fails the build.
// -----------------------------------------------------------------------------

using System.Globalization;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Information,
    Warning,
    Error,
}

/// <summary>Receives what the service reports about itself.</summary>
[Requirement("REQ-OBS-004",
    "No method accepts a byte array, stream or span, so no implementation can be handed print job content to write.")]
public interface IServiceLog
{
    void Write(LogLevel level, string message);
}

/// <summary>Convenience wrappers over <see cref="IServiceLog.Write"/>.</summary>
public static class ServiceLogExtensions
{
    public static void Info(this IServiceLog log, string message) =>
        log.Write(LogLevel.Information, message);

    public static void Warn(this IServiceLog log, string message) =>
        log.Write(LogLevel.Warning, message);

    public static void Error(this IServiceLog log, string message) =>
        log.Write(LogLevel.Error, message);
}

/// <summary>Writes to standard output and standard error.</summary>
public sealed class ConsoleServiceLog : IServiceLog
{
    private readonly TimeProvider _clock;
    private readonly object _gate = new();

    public ConsoleServiceLog(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public void Write(LogLevel level, string message)
    {
        string stamp = _clock.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        string line = $"{stamp}  {level.ToString().ToUpperInvariant(),-11}  {message}";

        // Serialised so concurrent relays cannot interleave half-lines.
        lock (_gate)
        {
            if (level == LogLevel.Error)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }
}

/// <summary>Collects entries in memory. Used by tests to assert what was reported.</summary>
public sealed class CollectingServiceLog : IServiceLog
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public void Write(LogLevel level, string message)
    {
        lock (_entries)
        {
            _entries.Add((level, message));
        }
    }
}

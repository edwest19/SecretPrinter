// -----------------------------------------------------------------------------
// ServiceLog.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// LogLine, the control-character escaping it performs, and CompositeServiceLog
// added by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-18, for REQ-OBS-009 and REQ-OBS-010. ConsoleServiceLog was
// changed to format through LogLine so that the console and the file cannot
// drift into two formats. Reviewed by a human before merge.
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
using System.Text;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Information,
    Warning,
    Error,
}

/// <summary>
/// Turns one entry into the single line that every sink writes, so the console
/// and the log file cannot end up in two different formats.
/// </summary>
public static class LogLine
{
    /// <summary>Formats one entry. The result never contains a line break.</summary>
    public static string Format(DateTimeOffset when, LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string stamp = when.ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return $"{stamp}  {level.ToString().ToUpperInvariant(),-11}  {Escape(message)}";
    }

    /// <summary>
    /// Replaces every C0 control character and DEL with <c>\xNN</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some of what reaches the log is learned from the network: instance names
    /// and TXT entries read from another device's advertisement, and endpoints
    /// of whatever connected. If such text could carry a line break, a device on
    /// the printer network could append a line of its own choosing to the log
    /// file - a forged entry, indistinguishable from one the service wrote. One
    /// entry is one line, so that cannot happen.
    /// </para>
    /// <para>
    /// The escape covers the whole C0 range and DEL rather than only CR and LF,
    /// which also keeps terminal escape sequences out of an operator's console.
    /// No message the service produces contains any of these characters, so in
    /// normal running this changes nothing that is written.
    /// </para>
    /// </remarks>
    [Requirement("REQ-OBS-010",
        "Escapes every control character to \\xNN, so no message can contain a line break and every entry is exactly one line.")]
    public static string Escape(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        int first = -1;

        for (int i = 0; i < message.Length; i++)
        {
            if (IsControl(message[i]))
            {
                first = i;
                break;
            }
        }

        // The overwhelmingly common case: nothing to escape, nothing to copy.
        if (first < 0)
        {
            return message;
        }

        var builder = new StringBuilder(message.Length + 16);
        builder.Append(message, 0, first);

        for (int i = first; i < message.Length; i++)
        {
            char c = message[i];

            if (IsControl(c))
            {
                builder.Append("\\x").Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsControl(char c) => c < ' ' || c == '\u007F';
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
        string line = LogLine.Format(_clock.GetUtcNow(), level, message);

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

/// <summary>Writes every entry to each of several sinks, in the order given.</summary>
/// <remarks>
/// The console and the log file are two sinks rather than one sink that knows
/// about both, so that adding a third later changes nothing that already works.
/// </remarks>
public sealed class CompositeServiceLog : IServiceLog
{
    private readonly IServiceLog[] _sinks;

    public CompositeServiceLog(params IServiceLog[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        if (sinks.Length == 0)
        {
            throw new ArgumentException(
                "A composite log with no sinks would silently discard everything written to it.",
                nameof(sinks));
        }

        foreach (IServiceLog sink in sinks)
        {
            ArgumentNullException.ThrowIfNull(sink, nameof(sinks));
        }

        // Copied, so a caller holding the array cannot change where the log goes
        // after the service has started.
        _sinks = [.. sinks];
    }

    public void Write(LogLevel level, string message)
    {
        foreach (IServiceLog sink in _sinks)
        {
            sink.Write(level, message);
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

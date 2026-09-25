// -----------------------------------------------------------------------------
// PrinterSide.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-25, for REQ-RES-009.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds the printer-side mDNS socket and the resolver that reads it, as one
//   pair that can be reopened. Everything that asks the printer anything - the
//   startup wait, the reachability watch, and each print job - asks through
//   the pair held here at that moment.
//
// Why it can be reopened:
//   On 2026-09-19, and again on 2026-09-25 on another adapter, the resolver's
//   membership of 224.0.0.251 on the printer-side interface was measured gone
//   while the service ran. On 2026-09-25 it was found gone after the Wi-Fi had
//   been out for ten hours and then, after a reconnect that lasted 36 seconds,
//   for three minutes more, and the adapter's reconnecting did not bring it
//   back. Which outage lost it is not established. It survived outages of 83
//   seconds, 3 minutes and 17 minutes the same day. Only a new socket joined
//   the group again. Without one the service cannot hear the printer's
//   answers, and it would have stayed withdrawn until someone restarted it. See
//   docs/findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md.
//
//   The loss cannot be seen from here: .NET lists the groups an interface has
//   joined without saying who holds them, and another process on the same
//   machine holds its own membership of the same group
//   (docs/findings/2026-09-19-the-printer-side-multicast-membership-is-lost.md,
//   item 3 of what it owes). So the pair is not reopened when a loss is
//   detected. It is reopened, by REQ-RES-009, before every question put to a
//   printer that has not been answering. A socket reopened needlessly costs a
//   few milliseconds; a membership lost and not reopened cost a whole day.
//
// What reopening does:
//   It examines the printer adapter again, by the same rule the startup wait
//   uses (StartupWait.Examine). If the adapter is usable, it opens a new
//   socket joined to 224.0.0.251 on the adapter's address as it is now, which
//   may differ from before, starts a new resolver on it, and only then closes
//   the old pair. If the adapter is not usable, or the new socket cannot be
//   opened, the old pair is kept: a question put through it may fail, and that
//   failure is counted as no answer, as before.
//
// What it logs:
//   A failure to reopen is logged when its reason first appears, and again
//   only if the reason changes. A successful reopen is logged only when it
//   follows a logged failure or lands on a different address or index. A
//   routine reopen on the same interface, which while the printer is away
//   happens after 1, 2, 4, 8... seconds and then hourly, logs nothing: the
//   watch's rule is to speak only when something changed, and the 2026-09-20
//   outage lasted nine hours.
//
// Threading:
//   Reopen is called by one caller at a time: the startup wait before the
//   service is up, and afterwards the watch's own loop. Resolver may be read
//   from any thread. A print job that read the old resolver just before a
//   reopen can have its lookup ended by it; that can only happen while the
//   printer is not being offered, which is when reopening happens.
// -----------------------------------------------------------------------------

using SecretPrinter.Mdns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>
/// The printer-side mDNS socket and the resolver that reads it, held as one
/// pair that can be reopened on the printer adapter's current address.
/// </summary>
public sealed class PrinterSide : IDisposable
{
    private readonly Func<MdnsInterface, IMdnsTransport> _open;
    private readonly Func<(MdnsInterface? Usable, string? Reason)> _examine;
    private readonly IServiceLog _log;
    private readonly IInterfaceInventory? _inventory;

    // Guards the pair and _disposed. Never held while a socket is opened or
    // closed, or while a resolver is disposed.
    private readonly object _sync = new();

    private MdnsInterface _interface;
    private IMdnsTransport _transport;
    private PrinterResolver _resolver;
    private bool _disposed;

    // The reason last logged for a failed reopen, or null when the last reopen
    // succeeded or none has failed. Touched only by Reopen's single caller.
    private string? _reportedFailure;

    /// <param name="printerInterface">The interface the first socket was opened on.</param>
    /// <param name="transport">
    /// The first socket, already open. This instance owns it from here on and
    /// disposes it, if it is disposable, when it is replaced or this is disposed.
    /// </param>
    /// <param name="open">
    /// Opens a new socket joined to 224.0.0.251 on the given interface. Throws
    /// if it cannot.
    /// </param>
    /// <param name="examine">
    /// Examines the printer adapter now: the interface to use, or the reason it
    /// cannot be used. In the service this is <see cref="StartupWait.Examine"/>.
    /// </param>
    /// <param name="log">Where a failed or noteworthy reopen is recorded.</param>
    /// <param name="inventory">
    /// Passed to every resolver made here, for the diagnosis it gives when a
    /// lookup times out. Defaults to the machine's own adapters. Supplied by
    /// tests so they do not read the real ones.
    /// </param>
    public PrinterSide(
        MdnsInterface printerInterface,
        IMdnsTransport transport,
        Func<MdnsInterface, IMdnsTransport> open,
        Func<(MdnsInterface? Usable, string? Reason)> examine,
        IServiceLog log,
        IInterfaceInventory? inventory = null)
    {
        ArgumentNullException.ThrowIfNull(printerInterface);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(examine);
        ArgumentNullException.ThrowIfNull(log);

        _open = open;
        _examine = examine;
        _log = log;
        _inventory = inventory;

        _interface = printerInterface;
        _transport = transport;
        _resolver = new PrinterResolver(transport, printerInterface, inventory: inventory);
    }

    /// <summary>The resolver to ask through now.</summary>
    public PrinterResolver Resolver
    {
        get
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _resolver;
            }
        }
    }

    /// <summary>The interface the current socket is joined on.</summary>
    public MdnsInterface Interface
    {
        get
        {
            lock (_sync)
            {
                return _interface;
            }
        }
    }

    /// <summary>
    /// Examines the printer adapter again and, if it is usable, replaces the
    /// socket and resolver with new ones joined on its current address.
    /// </summary>
    /// <returns>
    /// True when the pair was replaced; false when the old pair was kept,
    /// because the adapter is not usable or the new socket could not be opened.
    /// </returns>
    [Requirement("REQ-RES-009",
        "Examines the printer adapter again and, if it is usable, opens a new socket joined to 224.0.0.251 on "
        + "its current address with a new resolver on it, then closes the old pair; if not, keeps the old pair "
        + "and logs the reason once.")]
    public bool Reopen()
    {
        (MdnsInterface? usable, string? reason) = _examine();

        if (usable is null)
        {
            ReportFailure(reason ?? "the printer adapter is not usable, and no reason was given.");
            return false;
        }

        IMdnsTransport? transport = null;
        PrinterResolver resolver;
        try
        {
            transport = _open(usable);
            resolver = new PrinterResolver(transport, usable, inventory: _inventory);
        }
        catch (Exception ex)
        {
            // Not handled here: reported, and the old pair kept. A question put
            // through the old pair may fail, and that is counted as no answer.
            (transport as IDisposable)?.Dispose();
            ReportFailure($"opening a socket on {usable} failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        MdnsInterface previousInterface;
        IMdnsTransport previousTransport;
        PrinterResolver previousResolver;

        lock (_sync)
        {
            if (_disposed)
            {
                previousInterface = usable;
                previousTransport = transport;
                previousResolver = resolver;
            }
            else
            {
                previousInterface = _interface;
                previousTransport = _transport;
                previousResolver = _resolver;

                _interface = usable;
                _transport = transport;
                _resolver = resolver;
            }
        }

        // The old pair is closed only after the new one is in place, so there
        // is never a moment with no resolver to ask through. When this instance
        // was disposed meanwhile, the pair just made is what gets closed.
        previousResolver.Dispose();
        (previousTransport as IDisposable)?.Dispose();

        if (ReferenceEquals(previousResolver, resolver))
        {
            return false;
        }

        bool moved = !usable.Equals(previousInterface);

        if (_reportedFailure is not null || moved)
        {
            _log.Info(
                $"Printer-side socket reopened on {usable}, and {MdnsSocket.MulticastGroup} joined again"
                + (moved ? $"; it was on {previousInterface}." : "."));
        }

        _reportedFailure = null;
        return true;
    }

    /// <summary>Closes the current socket and resolver.</summary>
    public void Dispose()
    {
        IMdnsTransport transport;
        PrinterResolver resolver;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            transport = _transport;
            resolver = _resolver;
        }

        resolver.Dispose();
        (transport as IDisposable)?.Dispose();
    }

    private void ReportFailure(string reason)
    {
        if (string.Equals(reason, _reportedFailure, StringComparison.Ordinal))
        {
            return;
        }

        _reportedFailure = reason;

        string sentence = reason.EndsWith('.') ? reason : reason + ".";
        _log.Warn(
            $"Could not reopen the printer-side socket: {sentence} Questions go on through the socket already "
            + "open until it can be reopened.");
    }
}

// -----------------------------------------------------------------------------
// PrinterSideTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-09-25, for REQ-RES-009.
// Reviewed by a human before merge.
//
// Purpose:
//   Holds PrinterSide to what it is for: after a reopen, questions go through a
//   new socket on the adapter's current address and the old socket is closed;
//   when the adapter cannot be used, the old socket is kept; and it speaks only
//   when something changed.
//
//   No real socket is opened. Each "socket" is a fake transport wrapped so the
//   test can see whether it was closed. Whether a real socket rejoins
//   224.0.0.251 on Windows is not something these tests can show; it was
//   measured on FIOS-STB-01 on 2026-09-25, where a new socket joined again
//   and the old one had not. See
//   docs/findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class PrinterSideTests
{
    private static readonly MdnsInterface Adapter =
        new("Wi-Fi", IPAddress.Parse("192.168.12.186"), 10, AddressFamily.InterNetwork);

    private static readonly MdnsInterface AdapterMoved =
        new("Wi-Fi", IPAddress.Parse("192.168.12.187"), 10, AddressFamily.InterNetwork);

    private static readonly DnsName Instance = DnsName.Parse("Example Printer._ipps._tcp.local");

    /// <summary>A fake socket that records whether it was closed.</summary>
    private sealed class ClosableTransport(MdnsInterface joined) : IMdnsTransport, IDisposable
    {
        private readonly FakeTransport _inner = new(joined);

        public bool Closed { get; private set; }

        public MdnsInterface Joined => joined;

        public IReadOnlyList<MdnsInterface> Interfaces => _inner.Interfaces;

        public Task<MdnsDatagram> ReceiveAsync(CancellationToken cancellationToken) =>
            _inner.ReceiveAsync(cancellationToken);

        public Task SendMulticastAsync(ReadOnlyMemory<byte> payload, MdnsInterface via, CancellationToken cancellationToken) =>
            _inner.SendMulticastAsync(payload, via, cancellationToken);

        public Task SendUnicastAsync(
            ReadOnlyMemory<byte> payload, IPEndPoint destination, MdnsInterface via, CancellationToken cancellationToken) =>
            _inner.SendUnicastAsync(payload, destination, via, cancellationToken);

        public void Dispose() => Closed = true;
    }

    private sealed class RecordingLog : IServiceLog
    {
        public List<string> Lines { get; } = [];

        public void Write(LogLevel level, string message) => Lines.Add($"{level}: {message}");
    }

    /// <summary>
    /// A PrinterSide whose adapter examination returns whatever
    /// <paramref name="adapterNow"/> says at the time, and which records every
    /// socket it opens.
    /// </summary>
    private static (PrinterSide Side, ClosableTransport First, List<ClosableTransport> Opened, RecordingLog Log)
        Build(Func<(MdnsInterface? Usable, string? Reason)> adapterNow, Func<MdnsInterface, ClosableTransport>? open = null)
    {
        var first = new ClosableTransport(Adapter);
        var opened = new List<ClosableTransport>();
        var log = new RecordingLog();

        var side = new PrinterSide(
            Adapter,
            first,
            joinOn =>
            {
                ClosableTransport made = open is null ? new ClosableTransport(joinOn) : open(joinOn);
                opened.Add(made);
                return made;
            },
            adapterNow,
            log);

        return (side, first, opened, log);
    }

    [TestCase("A reopen puts a new socket and resolver in place, then closes the old ones")]
    [Requirement("REQ-RES-009")]
    public static void Reopen_replaces_the_pair_and_closes_the_old_one()
    {
        (PrinterSide side, ClosableTransport first, List<ClosableTransport> opened, RecordingLog log) =
            Build(() => (Adapter, null));

        using (side)
        {
            PrinterResolver before = side.Resolver;

            Assert.True(side.Reopen(), "a usable adapter must be reopened on");

            Assert.Equal(1, opened.Count, "one new socket");
            Assert.True(first.Closed, "the old socket must be closed once the new one is in place");
            Assert.False(opened[0].Closed, "the new socket must be left open");
            Assert.False(ReferenceEquals(before, side.Resolver), "questions must go through a new resolver");
            Assert.Throws<ObjectDisposedException>(
                () => before.ResolveAsync(Instance, TimeSpan.FromSeconds(1), CancellationToken.None)
                            .GetAwaiter().GetResult(),
                "the old resolver must be closed with its socket");
            Assert.Equal(0, log.Lines.Count, "a routine reopen on the same interface is not narrated");
        }

        Assert.True(opened[0].Closed, "disposing closes the socket held last");
    }

    [TestCase("An adapter that cannot be used keeps the old socket, and the reason is logged once")]
    [Requirement("REQ-RES-009")]
    public static void Unusable_adapter_keeps_the_pair_and_says_so_once()
    {
        (PrinterSide side, ClosableTransport first, List<ClosableTransport> opened, RecordingLog log) =
            Build(() => (null, "Interface 'Wi-Fi' is down."));

        using (side)
        {
            PrinterResolver before = side.Resolver;

            Assert.False(side.Reopen(), "nothing can be reopened on an adapter that cannot be used");
            Assert.False(side.Reopen(), "nor the second time");

            Assert.Equal(0, opened.Count, "no socket is opened on an unusable adapter");
            Assert.False(first.Closed, "the old socket is kept");
            Assert.True(ReferenceEquals(before, side.Resolver), "questions still go through the old resolver");

            Assert.Equal(1, log.Lines.Count, "the same reason twice is logged once");
            Assert.True(log.Lines[0].StartsWith("Warning: ", StringComparison.Ordinal), "it is a warning");
            Assert.True(log.Lines[0].Contains("Interface 'Wi-Fi' is down.", StringComparison.Ordinal),
                "the line must give the reason the adapter could not be used");
        }
    }

    [TestCase("A socket that cannot be opened keeps the old one, and says why")]
    [Requirement("REQ-RES-009")]
    public static void Failed_open_keeps_the_pair_and_says_why()
    {
        (PrinterSide side, ClosableTransport first, _, RecordingLog log) =
            Build(() => (Adapter, null), _ => throw new SocketException((int)SocketError.AddressNotAvailable));

        using (side)
        {
            PrinterResolver before = side.Resolver;

            Assert.False(side.Reopen(), "a socket that could not be opened replaces nothing");
            Assert.False(first.Closed, "the old socket is kept");
            Assert.True(ReferenceEquals(before, side.Resolver), "questions still go through the old resolver");
            Assert.Equal(1, log.Lines.Count, "the failure is logged");
            Assert.True(log.Lines[0].Contains("SocketException", StringComparison.Ordinal),
                "the line must name what failed");
        }
    }

    [TestCase("A reopen after a logged failure is logged, and the next routine one is not")]
    [Requirement("REQ-RES-009")]
    public static void Recovery_after_a_failure_is_logged_once()
    {
        bool usable = false;
        (PrinterSide side, _, _, RecordingLog log) =
            Build(() => usable ? (Adapter, null) : (null, "Interface 'Wi-Fi' is down."));

        using (side)
        {
            side.Reopen();
            usable = true;
            side.Reopen();
            side.Reopen();
        }

        Assert.Equal(2, log.Lines.Count, "one line for the failure, one for the reopen that ended it, none after");
        Assert.True(log.Lines[1].StartsWith("Information: ", StringComparison.Ordinal)
                    && log.Lines[1].Contains("reopened", StringComparison.Ordinal),
            "the second line must record the reopen");
    }

    [TestCase("A reopen on a changed address says where it was and where it is")]
    [Requirement("REQ-RES-009")]
    public static void Reopen_on_a_changed_address_is_logged()
    {
        (PrinterSide side, _, List<ClosableTransport> opened, RecordingLog log) =
            Build(() => (AdapterMoved, null));

        using (side)
        {
            Assert.True(side.Reopen(), "a usable adapter must be reopened on");
            Assert.Equal(AdapterMoved, opened[0].Joined, "the new socket must join on the address the adapter has now");
            Assert.Equal(AdapterMoved, side.Interface, "and that is the interface now held");
        }

        Assert.Equal(1, log.Lines.Count, "a move is worth one line");
        Assert.True(log.Lines[0].Contains("192.168.12.187", StringComparison.Ordinal)
                    && log.Lines[0].Contains("192.168.12.186", StringComparison.Ordinal),
            "the line must give the new address and the old one");
    }
}

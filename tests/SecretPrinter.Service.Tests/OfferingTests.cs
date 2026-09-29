// -----------------------------------------------------------------------------
// OfferingTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-28, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Verifies the decisions ServiceHost hands to Offering: when the printer is
//   offered to the client network and when it is not (README REQ-ADV-023,
//   REQ-ADV-024 and REQ-LIF-006). Each test runs the real MdnsResponder over
//   the fake transport, with the probe's waits recorded instead of slept, so
//   what goes onto the network, and in what order against the gate, is read
//   back exactly.
//
// What these tests do NOT prove:
//   That RunAsync starts the receive loop before the startup probe, calls
//   these methods at startup and from the watch, or passes the responder's
//   conflict to WithdrawOnConflictAsync. That is established by reading
//   RunAsync, where each is a single call, and on the hardware: a conflict the
//   receive loop never delivered would never be logged there.
//
// Addresses and names:
//   Invented for the tests. None is taken from the development network.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Responder;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class OfferingTests
{
    private static readonly MdnsInterface ClientNic =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetwork);

    private static readonly MdnsInterface ClientNicV6 =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetworkV6);

    private sealed class RecordingLog : IServiceLog
    {
        public List<string> Lines { get; } = [];

        public void Write(LogLevel level, string message) => Lines.Add($"{level}: {message}");
    }

    /// <summary>A responder on the fake transport, its gate, its log, and a recording clock.</summary>
    private sealed class Fixture
    {
        public Fixture(bool gateOpen)
        {
            Gate = new AvailabilityGate(gateOpen);
            Advertisement = AdvertisementBuilder.Build(
                new PrinterCapabilities(
                    ["txtvers=1", "rp=ipp/print", "pdl=application/octet-stream,image/urf", "URF=V1.4", "Color=T"],
                    631,
                    CapabilitySource.ForTest("offering tests")),
                new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631),
                ClientNic.Address);
            Responder = new MdnsResponder(
                Transport,
                [new AdvertisedInterface(ClientNic, Advertisement, ClientNicV6)],
                advertising: () => Gate.IsOpen,
                conflicted: found => Conflicts.TrySetResult(found));
        }

        public FakeTransport Transport { get; } = new(ClientNic, ClientNicV6);

        public AvailabilityGate Gate { get; }

        public RecordingLog Log { get; } = new();

        public Advertisement Advertisement { get; }

        public MdnsResponder Responder { get; }

        /// <summary>What the responder reported, as ServiceHost wires it.</summary>
        public TaskCompletionSource<NameConflict> Conflicts { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(TimeSpan Delay, int SentBefore, bool GateOpen)> Waits { get; } = [];

        public Action<int>? During { get; set; }

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Waits.Add((delay, Transport.Sent.Count, Gate.IsOpen));
            During?.Invoke(Waits.Count - 1);
            return Task.CompletedTask;
        }

        public DnsName Host => Advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

        /// <summary>Another device answering for the host name with its own address, over IPv6.</summary>
        public void HearConflict()
        {
            var builder = new DnsResponseBuilder();
            builder.AddAnswer(new OutgoingRecord(
                Host, DnsRecordType.A, 120, CacheFlush: true, new AddressPayload(IPAddress.Parse("192.168.1.50"))));

            Responder.HandleAsync(
                    new MdnsDatagram(
                        builder.Build(), new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353), ClientNicV6.Index, ClientNicV6),
                    CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        public bool Claim() =>
            Offering.ClaimAndOfferAsync(Responder, Gate, Log, Delay, TimeSpan.Zero, CancellationToken.None)
                .GetAwaiter().GetResult();

        public void ReachabilityChanged(bool reachable) =>
            Offering.OnReachabilityChangedAsync(Responder, Gate, Log, reachable, Delay, TimeSpan.Zero, CancellationToken.None)
                .GetAwaiter().GetResult();
    }

    private static int Probes => 2 * MdnsResponder.ProbeCount;

    // ---- Claiming before offering (REQ-ADV-023) ------------------------------

    [TestCase("Nothing is offered until the probe comes back clear; then the gate opens and the announcements go out")]
    [Requirement("REQ-ADV-023")]
    public static void Offered_only_after_a_clear_probe()
    {
        var f = new Fixture(gateOpen: false);

        bool offered = f.Claim();

        Assert.True(offered, "no other device answered, so the printer is offered");
        Assert.True(f.Waits.Count > 0 && f.Waits.All(w => !w.GateOpen),
            "the gate stays closed for the whole probe: nothing is accepted under names not yet claimed");
        Assert.True(f.Transport.Sent.Take(Probes).All(s => !s.Parsed.IsResponse), "the probes go out first");
        Assert.Equal(Probes + MdnsResponder.AnnouncementCount, f.Transport.Sent.Count, "then the announcements");
        Assert.True(f.Transport.Sent.Skip(Probes).All(s => s.Parsed.IsResponse), "which are responses");
        Assert.True(f.Gate.IsOpen, "the gate is open once the names are claimed");
        Assert.True(f.Log.Lines.Any(l => l.Contains("Probe clear", StringComparison.Ordinal)),
            "the log says the probe came back clear, so an operator can see the order on the hardware");
    }

    [TestCase("A conflict found by the probe leaves the printer withdrawn, with nothing announced")]
    [Requirement("REQ-ADV-023")]
    [Requirement("REQ-ADV-024")]
    public static void Conflict_while_probing_offers_nothing()
    {
        var f = new Fixture(gateOpen: false);
        f.During = i =>
        {
            if (i == 1)
            {
                f.HearConflict();
            }
        };

        bool offered = f.Claim();

        Assert.False(offered, "another device answered for the host name");
        Assert.False(f.Gate.IsOpen, "the gate stays closed");
        Assert.False(f.Transport.Sent.Any(s => s.Parsed.IsResponse), "nothing is announced");
    }

    [TestCase("When the printer returns, it is probed for again before it is offered")]
    [Requirement("REQ-ADV-023")]
    public static void Restore_probes_again()
    {
        var f = new Fixture(gateOpen: false);

        f.ReachabilityChanged(reachable: true);

        Assert.True(f.Waits.Count > 0 && f.Waits.All(w => !w.GateOpen), "the gate stays closed while probing");
        Assert.True(f.Transport.Sent.Take(Probes).All(s => !s.Parsed.IsResponse), "a probe goes out before anything else");
        Assert.Equal(Probes + MdnsResponder.AnnouncementCount, f.Transport.Sent.Count, "then the announcements");
        Assert.True(f.Gate.IsOpen, "and the printer is on offer again");
        Assert.True(f.Log.Lines.Any(l => l.Contains("Advertisement restored", StringComparison.Ordinal)),
            "the restore is logged, as before");
    }

    // ---- Conflicts (REQ-ADV-024) --------------------------------------------

    [TestCase("When the printer returns while a conflict is held, nothing is probed or offered, and the log says why")]
    [Requirement("REQ-ADV-024")]
    public static void Returning_printer_is_not_offered_while_a_conflict_is_held()
    {
        var f = new Fixture(gateOpen: false);
        f.HearConflict();

        f.ReachabilityChanged(reachable: true);

        Assert.False(f.Gate.IsOpen, "a conflict lasts until restart, so the gate stays closed");
        Assert.Equal(0, f.Transport.Sent.Count, "no probe, no announcement");
        Assert.True(f.Log.Lines.Any(l => l.StartsWith("Warning: ", StringComparison.Ordinal)
                                         && l.Contains("restarted", StringComparison.Ordinal)),
            "the log says the printer answers but is not offered, and what ends that");
    }

    [TestCase("A conflict after announcing closes the gate, says goodbye, and logs the name, type, address and transport")]
    [Requirement("REQ-ADV-024")]
    public static void Conflict_after_announcing_withdraws_and_says_so()
    {
        var f = new Fixture(gateOpen: true);
        Task withdrawing = Offering.WithdrawOnConflictAsync(
            f.Conflicts.Task, f.Responder, f.Gate, f.Log, CancellationToken.None);

        f.HearConflict();
        withdrawing.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Assert.False(f.Gate.IsOpen, "the gate is closed: no listener, nothing accepted");
        Assert.Equal(1, f.Transport.Sent.Count, "one goodbye");
        Assert.True(f.Transport.Sent[0].Parsed.Answers.All(r => r.Ttl == 0), "every record in it with TTL 0");

        string line = f.Log.Lines.SingleOrDefault(l => l.StartsWith("Error: ", StringComparison.Ordinal)) ?? "";
        Assert.True(line.Contains(f.Host.ToString(), StringComparison.OrdinalIgnoreCase), $"the line names the name: {line}");
        Assert.True(line.Contains("(A)", StringComparison.Ordinal), $"the record type: {line}");
        Assert.True(line.Contains("192.168.1.50", StringComparison.Ordinal), $"the address that answered: {line}");
        Assert.True(line.Contains("IPv6", StringComparison.Ordinal), $"the transport it was heard on: {line}");
        Assert.True(line.Contains("restarted", StringComparison.Ordinal), $"and what ends it: {line}");
    }

    [TestCase("Stopping before any conflict ends the wait for one quietly")]
    public static void Stopping_without_a_conflict_is_quiet()
    {
        var f = new Fixture(gateOpen: true);
        using var stop = new CancellationTokenSource();
        Task withdrawing = Offering.WithdrawOnConflictAsync(f.Conflicts.Task, f.Responder, f.Gate, f.Log, stop.Token);

        stop.Cancel();
        withdrawing.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Assert.True(f.Gate.IsOpen, "no conflict, so the gate is left as it was");
        Assert.Equal(0, f.Transport.Sent.Count, "and nothing is sent");
        Assert.Equal(0, f.Log.Lines.Count, "and nothing is logged");
    }

    // ---- Losing the printer (REQ-LIF-006, unchanged) ------------------------

    [TestCase("When the printer is lost, the gate closes before the goodbye goes out, and the log says so")]
    [Requirement("REQ-LIF-006")]
    public static void Lost_printer_is_withdrawn_gate_first()
    {
        var f = new Fixture(gateOpen: true);
        bool? gateOpenAtSend = null;
        f.Transport.OnSend = _ => gateOpenAtSend ??= f.Gate.IsOpen;

        f.ReachabilityChanged(reachable: false);

        Assert.Equal(false, gateOpenAtSend, "the gate was already closed when the goodbye went out");
        Assert.Equal(1, f.Transport.Sent.Count, "one goodbye");
        Assert.True(f.Transport.Sent[0].Parsed.Answers.All(r => r.Ttl == 0), "every record in it with TTL 0");
        Assert.True(f.Log.Lines.Any(l => l.Contains("Advertisement withdrawn", StringComparison.Ordinal)),
            "the withdrawal is logged, as before");
    }
}

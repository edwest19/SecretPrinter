// -----------------------------------------------------------------------------
// ServiceHostTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-16, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// Tests for REQ-RES-007 - capabilities from the printer's _ipp._tcp service,
// connections to its _ipps._tcp service - added by Claude (Anthropic model,
// Claude Opus 5) at the direction of Edwin West, 2026-09-16. Reviewed by a
// human before merge.
//
// The test of RelayLogger's "relaying" line, for REQ-OBS-008, added by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-22.
// Reviewed by a human before merge.
//
// The startup-lookup test's description and message changed, because since
// REQ-LIF-008 a failed lookup is retried rather than stopping startup, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-22. Reviewed by a human before merge.
//
// The test that a job's lookup hands its answer to the watch, for REQ-RES-008,
// added by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
// West, 2026-09-25. Reviewed by a human before merge.
//
// Purpose:
//   Verifies decisions ServiceHost makes that can be checked without opening a
//   socket: which interfaces each of its two mDNS sockets joins, how each job's
//   printer lookup is described in the log, and which of the printer's two
//   services supplies capabilities and which supplies the connection endpoint.
//
//   The socket-plan and log-line tests carry no requirement marker, because no
//   requirement row states either decision. The first two guard against the defect recorded
//   in docs/findings/2026-09-14-shared-receive-loop.md: the responder and the
//   resolver reading one socket and taking each other's datagrams. The last two
//   protect the log line whose measurements decide whether the cache that
//   REQ-RES-004 permits is kept.
//
//
//   The REQ-RES-007 tests run the real PrinterResolver against a fake printer
//   that advertises its two services at different hosts, addresses and ports,
//   so an answer taken from the wrong service cannot pass by coincidence.
//
// What these tests do NOT prove:
//   That RunAsync opens its sockets from this plan, logs this line, or calls
//   these two methods. That is established by reading RunAsync and
//   RunRelayAsync, where each is a single call. Nor do they
//   prove the resolver's socket receives the printer's reply while the
//   responder's socket runs beside it; that needs the hardware.
//
// Addresses and names:
//   Invented for the tests. None is taken from the development network.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Resolution;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ServiceHostTests
{
    private static readonly MdnsInterface FirstClient =
        new("Client A", IPAddress.Parse("192.168.1.10"), 11, AddressFamily.InterNetwork);

    private static readonly MdnsInterface SecondClient =
        new("Client B", IPAddress.Parse("192.168.3.10"), 13, AddressFamily.InterNetwork);

    private static readonly MdnsInterface Printer =
        new("Printer side", IPAddress.Parse("192.168.2.10"), 12, AddressFamily.InterNetwork);

    private static ResolvedPrinter Answer(DateTimeOffset resolvedAt) =>
        new(
            DnsName.Parse("Example Printer._ipp._tcp.local"),
            DnsName.Parse("example-printer.local"),
            IPAddress.Parse("192.168.2.50"),
            631,
            [],
            resolvedAt,
            120);

    private static readonly DnsName IppInstance = DnsName.Parse("Example Printer._ipp._tcp.local");
    private static readonly DnsName IppsInstance = DnsName.Parse("Example Printer._ipps._tcp.local");

    /// <summary>
    /// A reply naming one service: SRV to <paramref name="host"/> on
    /// <paramref name="port"/>, the TXT strings given, and an A record for the host.
    /// </summary>
    private static MdnsDatagram ServiceReply(
        DnsName instance, string host, ushort port, string address, IReadOnlyList<string> txt)
    {
        DnsName hostName = DnsName.Parse(host);

        var builder = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(instance, DnsRecordType.Srv, 120, true,
                new SrvPayload(0, 0, port, hostName)));

        if (txt.Count > 0)
        {
            builder.AddAnswer(new OutgoingRecord(instance, DnsRecordType.Txt, 120, true, new TxtPayload(txt)));
        }

        byte[] payload = builder
            .AddAdditional(new OutgoingRecord(hostName, DnsRecordType.A, 120, true,
                new AddressPayload(IPAddress.Parse(address))))
            .Build();

        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(address), 5353), Printer.Index, Printer);
    }

    /// <summary>
    /// A fake printer network. Its _ipp._tcp service is at ipp-side.local
    /// (192.168.2.50:631, with TXT); its _ipps._tcp service is at
    /// ipps-side.local (192.168.2.51:8443, no TXT). Nothing about the two agrees,
    /// so a test can tell which answered.
    /// </summary>
    private static FakeTransport PrinterNetwork(bool ippsAnswers = true)
    {
        var transport = new FakeTransport(Printer);
        transport.OnSend = sent =>
        {
            DnsName asked = sent.Parsed.Questions[0].Name;

            if (asked.Equals(IppInstance))
            {
                transport.Enqueue(ServiceReply(
                    IppInstance, "ipp-side.local", 631, "192.168.2.50", ["txtvers=1", "rp=ipp/print"]));
            }
            else if (asked.Equals(IppsInstance) && ippsAnswers)
            {
                transport.Enqueue(ServiceReply(IppsInstance, "ipps-side.local", 8443, "192.168.2.51", []));
            }
        };

        return transport;
    }

    [TestCase("At startup, capabilities come from the _ipp answer and the connection from the _ipps answer")]
    [Requirement("REQ-RES-007")]
    public static void Startup_takes_capabilities_from_ipp_and_connection_from_ipps()
    {
        FakeTransport transport = PrinterNetwork();
        using var resolver = new PrinterResolver(transport, Printer);

        PrinterAtStartup found = ServiceHost
            .ResolveAtStartupAsync(resolver, IppInstance, IppsInstance, TimeSpan.FromSeconds(5), CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.True(found.Capabilities.Instance.Equals(IppInstance), "capabilities must come from the _ipp instance");
        Assert.True(found.Capabilities.TxtStrings.Contains("rp=ipp/print"),
            "the capabilities must carry the _ipp service's TXT record");
        Assert.True(found.Connection.Instance.Equals(IppsInstance), "the connection must come from the _ipps instance");
        Assert.Equal(IPAddress.Parse("192.168.2.51"), found.Connection.Address,
            "the connection address must be the one the _ipps service gave");
        Assert.Equal((ushort)8443, found.Connection.Port, "the connection port must be the one the _ipps service gave");
    }

    [TestCase("The startup lookup fails, naming the _ipps instance, when the printer does not advertise it")]
    [Requirement("REQ-RES-007")]
    public static void Startup_fails_when_ipps_does_not_answer()
    {
        FakeTransport transport = PrinterNetwork(ippsAnswers: false);
        using var resolver = new PrinterResolver(transport, Printer);

        PrinterResolutionException ex = Assert.Throws<PrinterResolutionException>(
            () => _ = ServiceHost
                .ResolveAtStartupAsync(
                    resolver, IppInstance, IppsInstance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                .GetAwaiter().GetResult(),
            "a printer that does not advertise the configured _ipps instance must fail the lookup, which startup then retries");

        Assert.True(ex.Message.Contains(IppsInstance.ToString(), StringComparison.Ordinal),
            "the failure must name the _ipps instance that did not answer");
    }

    [TestCase("Each connection goes to the address and port the _ipps instance gives")]
    [Requirement("REQ-RES-007")]
    public static void Connection_goes_where_ipps_says()
    {
        FakeTransport transport = PrinterNetwork();
        using var resolver = new PrinterResolver(transport, Printer);
        var log = new CollectingServiceLog();

        // The _ipp answer is resolved first and cached, as at startup, so the
        // connection could only get it wrong by asking for the wrong instance.
        _ = resolver.ResolveAsync(IppInstance, TimeSpan.FromSeconds(5), CancellationToken.None)
                    .GetAwaiter().GetResult();

        IPEndPoint endpoint = ServiceHost
            .LocateConnectionAsync(resolver, IppsInstance, TimeSpan.FromSeconds(5), log, _ => { }, CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.2.51"), 8443), endpoint,
            "the connection must go to the _ipps service's address and port, not the _ipp service's");
        Assert.True(log.Entries.Any(e => e.Message.Contains(IppsInstance.ToString(), StringComparison.Ordinal)),
            "the lookup must be logged, naming the _ipps instance");
    }

    [TestCase("A job's lookup hands the answer it used to the watch")]
    [Requirement("REQ-RES-008")]
    public static void A_job_lookup_hands_its_answer_over()
    {
        FakeTransport transport = PrinterNetwork();
        using var resolver = new PrinterResolver(transport, Printer);
        var handed = new List<ResolvedPrinter>();

        IPEndPoint endpoint = ServiceHost
            .LocateConnectionAsync(
                resolver, IppsInstance, TimeSpan.FromSeconds(5), new CollectingServiceLog(), handed.Add,
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Equal(1, handed.Count,
            "a resolution performed because a job arrived must reach the watch, or it cannot reset the schedule");
        Assert.Equal(endpoint, new IPEndPoint(handed[0].Address, handed[0].Port),
            "what reaches the watch must be the answer the job used");
    }

    [TestCase("The resolver's socket joins the printer interface, for IPv4 only, and nothing else")]
    public static void Resolver_socket_joins_only_the_printer_interface()
    {
        MdnsSocketPlan plan = ServiceHost.PlanMdnsBindings([FirstClient, SecondClient], Printer);

        Assert.Equal(1, plan.Resolver.Count, "the resolver's socket must join exactly one interface");
        Assert.Equal(
            new MdnsBinding(Printer.Address, JoinIPv6: false),
            plan.Resolver[0],
            "the resolver's socket must join the printer interface, and must not join IPv6 there");
    }

    [TestCase("The responder's socket joins every client interface, with IPv6, and never the printer interface")]
    public static void Responder_socket_joins_every_client_and_not_the_printer()
    {
        MdnsSocketPlan plan = ServiceHost.PlanMdnsBindings([FirstClient, SecondClient], Printer);

        Assert.Equal(2, plan.Responder.Count, "the responder's socket must join each client interface once");
        Assert.Equal(
            new MdnsBinding(FirstClient.Address, JoinIPv6: true),
            plan.Responder[0],
            "the first client interface must be joined for IPv4 and IPv6");
        Assert.Equal(
            new MdnsBinding(SecondClient.Address, JoinIPv6: true),
            plan.Responder[1],
            "the second client interface must be joined for IPv4 and IPv6");
        Assert.False(
            plan.Responder.Any(binding => binding.Address.Equals(Printer.Address)),
            "the responder's socket must not join the printer interface, or it would read the resolver's replies");
    }

    [TestCase("A lookup answered from the cache is described as cached")]
    public static void Lookup_answered_before_it_began_is_cached()
    {
        var started = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        // Obtained thirty seconds before this lookup began, so only the cache
        // could have supplied it.
        string line = ServiceHost.DescribeLookup(Answer(started.AddSeconds(-30)), started, TimeSpan.Zero);

        Assert.Equal(
            "Job: located Example Printer._ipp._tcp.local at 192.168.2.50:631 (cached, 0s).",
            line,
            "an answer older than the lookup must be reported as cached");
    }

    [TestCase("A lookup that had to query is described as queried, with the instance, address, port and duration")]
    public static void Lookup_answered_after_it_began_is_queried()
    {
        var started = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        TimeSpan elapsed = TimeSpan.FromMilliseconds(412);

        string line = ServiceHost.DescribeLookup(Answer(started + elapsed), started, elapsed);

        Assert.Equal(
            "Job: located Example Printer._ipp._tcp.local at 192.168.2.50:631 (queried, 0.412s).",
            line,
            "an answer obtained during the lookup must be reported as queried, with the time it took");
    }

    /// <summary>Collects what the relay logger chose to say.</summary>
    private sealed class RecordingLog : IServiceLog
    {
        public List<string> Lines { get; } = [];

        public void Write(LogLevel level, string message) => Lines.Add($"{level}: {message}");
    }

    [TestCase("The line recording a relayed connection names how the printer connection is encrypted")]
    [Requirement("REQ-OBS-008")]
    public static void Relaying_line_names_the_encryption()
    {
        var log = new RecordingLog();
        var logger = new RelayLogger(log);

        logger.RelayStarted(
            new IPEndPoint(IPAddress.Parse("192.168.1.41"), 49152),
            new IPEndPoint(IPAddress.Parse("192.168.2.50"), 631),
            "TLS 1.2, certificate matched the pinned fingerprint");

        Assert.Equal(1, log.Lines.Count, "one relayed connection beginning is one line");
        Assert.Equal(
            "Information: Job: relaying 192.168.1.41:49152 -> 192.168.2.50:631; "
            + "encryption to printer: TLS 1.2, certificate matched the pinned fingerprint.",
            log.Lines[0],
            "an operator must be able to read from this line alone how the job travelled to the printer");
    }
}

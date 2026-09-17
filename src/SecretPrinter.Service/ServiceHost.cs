// -----------------------------------------------------------------------------
// ServiceHost.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 binding of client interfaces added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Per-transport line added to the shutdown summary by Claude (Anthropic model,
// Claude Opus 5) at the direction of Edwin West, 2026-09-14, for REQ-OBS-007.
// Reviewed by a human before merge.
//
// Byte counts and duration added to the failed-job log line by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-14,
// for REQ-PXY-009. Reviewed by a human before merge.
//
// Separate mDNS socket for the resolver, and a log line for each job's printer
// lookup, added by Claude (Anthropic model, Claude Opus 5) at the direction of
// Edwin West, 2026-09-16. Reviewed by a human before merge.
//
// Connection endpoint taken from the printer's _ipps._tcp service, with
// capabilities still from its _ipp._tcp service, by Claude (Anthropic model,
// Claude Opus 5) at the direction of Edwin West, 2026-09-16, for REQ-RES-007.
// Reviewed by a human before merge.
//
// Print jobs carried to the printer over TLS, with the printer's certificate
// checked against the configured pin on every connection, by Claude (Anthropic
// model, Claude Opus 5) at the direction of Edwin West, 2026-09-17, for
// REQ-PXY-010 and REQ-SEC-013. Reviewed by a human before merge.
//
// Purpose:
//   Turns seven libraries into a running program: opens the sockets, asks the
//   printer what it can do, builds an advertisement from that answer, publishes
//   it on the client network, and relays print jobs back.
//
//   This is the only file that knows the whole story. Everything below it knows
//   one thing each, which is what makes the security claims checkable a project
//   at a time.
//
// What it logs, and why that matters:
//   Every TXT entry published, and every entry dropped with the reason. An
//   operator reading the log can reconstruct exactly what the client network
//   was told, without running a packet capture and without trusting this
//   document (REQ-OBS-002, REQ-OBS-003). Nothing from a print job is ever
//   logged, at any level; the log interface has no way to accept it.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Configuration;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Proxy;
using SecretPrinter.Resolution;
using SecretPrinter.Responder;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Runs SecretPrinter until cancelled.</summary>
public sealed class ServiceHost
{
    private readonly ServiceConfiguration _configuration;
    private readonly IServiceLog _log;

    public ServiceHost(ServiceConfiguration configuration, IServiceLog log)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(log);

        _configuration = configuration;
        _log = log;
    }

    /// <summary>
    /// Starts everything, serves until cancelled, then retracts the
    /// advertisement and closes cleanly.
    /// </summary>
    [Requirement("REQ-OBS-001",
        "Logs every interface in use, its resolved address and its role, before opening any socket.")]
    [Requirement("REQ-LIF-002",
        "Retracts the advertisement, leaves multicast groups and disposes every socket on the way out, whatever ended the run.")]
    [Requirement("REQ-LIF-004",
        "Any failure opening sockets or locating the printer propagates out of startup rather than leaving the service half-running.")]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _log.Info("SecretPrinter starting.");
        _log.Info("Print job data passes through this machine. See README.md.");

        foreach (string line in _configuration.Describe())
        {
            _log.Info($"  {line}");
        }

        // Parsed here, before any socket is opened, so a fingerprint that is
        // not 64 hexadecimal digits stops the service rather than the first
        // print job. ConfigurationLoader already refuses one (REQ-CFG-007);
        // this is the second reader of the same value and does not assume the
        // first ran. Describe() has just logged it (REQ-OBS-008), so an
        // operator can see what every connection will require.
        var printerPin = new CertificatePin(_configuration.PrinterCertificateSha256);

        // Two sockets, one per role. Until 2026-09-16 the responder and the
        // resolver shared one, and whichever was reading took the next datagram,
        // so the printer's reply to the resolver could be consumed and discarded
        // by the responder (docs/findings/2026-09-14-shared-receive-loop.md).
        // PlanMdnsBindings decides what each socket joins; this method only opens
        // them.
        MdnsSocketPlan plan = PlanMdnsBindings(_configuration.ClientInterfaces, _configuration.PrinterInterface);

        using MdnsSocket responderSocket = MdnsSocket.Open(plan.Responder);
        MdnsSocketConfiguration socketConfiguration = responderSocket.ReadBackConfiguration();

        _log.Info($"Responder socket: bound {socketConfiguration.LocalEndPoint} "
                  + $"(reuse={socketConfiguration.ReuseAddress}, "
                  + $"pktinfo={socketConfiguration.PacketInformation}, "
                  + $"ttl={socketConfiguration.MulticastTimeToLive}), "
                  + $"joined {MdnsSocket.MulticastGroup} on "
                  + $"{string.Join(", ", socketConfiguration.Interfaces)}.");

        MdnsIPv6SocketConfiguration? socketConfiguration6 = responderSocket.ReadBackIPv6Configuration();
        if (socketConfiguration6 is null)
        {
            _log.Info("No IPv6 socket was opened: no interface asked to join ff02::fb.");
        }
        else
        {
            _log.Info($"Responder socket: bound {socketConfiguration6.LocalEndPoint} "
                      + $"(reuse={socketConfiguration6.ReuseAddress}, "
                      + $"v6only={!socketConfiguration6.DualMode}, "
                      + $"pktinfo={socketConfiguration6.PacketInformation}, "
                      + $"hops={socketConfiguration6.MulticastHopLimit}).");

            foreach (MdnsInterface joined in socketConfiguration6.Interfaces)
            {
                _log.Info($"Joined {MdnsSocket.MulticastGroupV6} on {joined}.");
            }

            // Said plainly, in the log, every time. An operator watching startup
            // should be able to see which transports actually answer.
            _log.Info("IPv6 mDNS is joined, read, and answered over the arrival transport "
                      + "(REQ-ADV-018).");
        }

        using MdnsSocket resolverSocket = MdnsSocket.Open(plan.Resolver);
        MdnsSocketConfiguration resolverConfiguration = resolverSocket.ReadBackConfiguration();

        _log.Info($"Resolver socket: bound {resolverConfiguration.LocalEndPoint} "
                  + $"(reuse={resolverConfiguration.ReuseAddress}, "
                  + $"pktinfo={resolverConfiguration.PacketInformation}, "
                  + $"ttl={resolverConfiguration.MulticastTimeToLive}), "
                  + $"joined {MdnsSocket.MulticastGroup} on "
                  + $"{string.Join(", ", resolverConfiguration.Interfaces)} only.");

        using var resolver = new PrinterResolver(resolverSocket, _configuration.PrinterInterface);

        // The advertisement is built from what the printer says now, not from
        // anything stored. If the printer cannot be reached, the service does
        // not start: advertising capabilities nobody has confirmed would be a
        // claim we cannot support.
        DnsName ippInstance = DnsName.Parse(_configuration.PrinterInstance);
        DnsName ippsInstance = DnsName.Parse(_configuration.PrinterIppsInstance);
        _log.Info($"Asking {ippInstance} what it can do, and {ippsInstance} where to connect, "
                  + $"on {_configuration.PrinterInterface}.");

        PrinterAtStartup found = await ResolveAtStartupAsync(
                resolver, ippInstance, ippsInstance, _configuration.Tuning.PrinterResolveTimeout, cancellationToken)
            .ConfigureAwait(false);

        _log.Info($"Capabilities from {found.Capabilities}.");
        _log.Info($"Connections to {found.Connection}.");

        ResolvedPrinter printer = found.Capabilities;

        var capabilities = new PrinterCapabilities(
            printer.TxtStrings,
            printer.Port,
            CapabilitySource.FromMdnsQuery(
                printer.Address, new DnsNameLike(ippInstance.ToString()), printer.ResolvedAt));

        var identity = new ProxyIdentity(
            _configuration.AdvertisedInstanceName,
            _configuration.AdvertisedHostLabel,
            _configuration.AdvertisedUuid,
            _configuration.ListenPort);

        var advertised = new List<AdvertisedInterface>();
        foreach (MdnsInterface client in _configuration.ClientInterfaces)
        {
            Advertisement advertisement = AdvertisementBuilder.Build(capabilities, identity, client.Address);
            AdvertisementLog.Write(_log, client, advertisement);

            // The IPv6 companion is matched by address, because that is the one
            // thing the two entries for an adapter share: MdnsInterfaceResolver
            // builds the companion with the adapter's IPv4 address and its IPv6
            // index. The responder re-checks that invariant rather than trusting
            // it, so a mismatch here is refused there too.
            //
            // Every client binding asked to join IPv6 a few lines above, so a
            // missing companion is a bug in this file or in MdnsSocket.Open - not
            // a configuration an operator chose. It fails loudly rather than
            // passing null, which would start the service answering IPv4 only and
            // reproduce the original fault silently.
            MdnsInterface companion =
                responderSocket.IPv6Interfaces.FirstOrDefault(entry => entry.Address.Equals(client.Address))
                ?? throw new InvalidOperationException(
                    $"No IPv6 companion was opened for {client}, although this host asked to join "
                    + $"{MdnsSocket.MulticastGroupV6} on it. Answering IPv6 queries would be "
                    + "impossible and iOS would not discover the printer.");

            advertised.Add(new AdvertisedInterface(client, advertisement, companion));
            _log.Info($"Answering on {client} and, for queries that arrive over IPv6, {companion}.");
        }

        var responder = new MdnsResponder(responderSocket, advertised);

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new List<Task>();

        try
        {
            await responder.AnnounceAsync(TimeSpan.FromSeconds(1), stopping.Token).ConfigureAwait(false);
            _log.Info("Announced. Answering queries.");

            running.Add(responder.ServeAsync(stopping.Token));

            foreach (MdnsInterface client in _configuration.ClientInterfaces)
            {
                running.Add(RunRelayAsync(client, resolver, ippsInstance, printerPin, stopping.Token));
            }

            await Task.WhenAll(running).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log.Info("Stopping.");
        }
        finally
        {
            await stopping.CancelAsync().ConfigureAwait(false);

            // Goodbye records go out even after a failure, so the printer does
            // not linger in client lists advertising a service that has gone.
            try
            {
                using var goodbyeWindow = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await responder.SendGoodbyeAsync(goodbyeWindow.Token).ConfigureAwait(false);
                _log.Info("Advertisement retracted (goodbye records sent).");
            }
            catch (Exception ex) when (ex is OperationCanceledException or System.Net.Sockets.SocketException)
            {
                _log.Warn($"Could not send goodbye records: {ex.Message}. "
                          + "Clients may show the printer until its TTL expires.");
            }

            ResponderActivity activity = responder.Activity;
            _log.Info($"Served {activity.QueriesAnswered} quer(ies) of {activity.QueriesSeen} seen; "
                      + $"{activity.IgnoredNotOurs} were for other services and were ignored.");
            _log.Info(activity.DescribeByTransport());
            _log.Info("SecretPrinter stopped.");
        }
    }

    /// <summary>Accepts and relays print jobs on one client interface.</summary>
    [Requirement("REQ-PXY-001",
        "Binds the listener to one client interface address, never the wildcard, so jobs cannot be accepted on the printer network.")]
    [Requirement("REQ-SEC-012",
        "Permits connections only from the network of the interface the listener is bound to.")]
    [Requirement("REQ-PXY-010",
        "The relay is given a factory that opens TLS connections only, so there is no path by which a job "
        + "reaches the printer over a plain socket.")]
    private async Task RunRelayAsync(
        MdnsInterface client,
        PrinterResolver resolver,
        DnsName ippsInstance,
        CertificatePin printerPin,
        CancellationToken cancellationToken)
    {
        // The permitted network is derived from the interface itself rather than
        // configured separately, so the two cannot drift apart.
        var permitted = new IPNetwork(client.Address, PrefixLengthFor(client.Address));

        await using var listener = new TcpConnectionListener(client.Address, _configuration.ListenPort);

        _log.Info($"Accepting print jobs on {listener.LocalEndPoint} "
                  + $"({client.Name}); permitted clients: {permitted}.");

        var relay = new IppRelay(
            listener,

            // The TCP factory wrapped in the TLS one: the relay asks for a
            // connection and is handed one that has completed a handshake and
            // passed the pin check, or none at all. IppRelay itself knows
            // nothing about TLS and did not change when this was added.
            new TlsConnectionFactory(new TcpConnectionFactory(), printerPin),
            token => LocateConnectionAsync(
                resolver, ippsInstance, _configuration.Tuning.PrinterResolveTimeout, _log, token),
            new RelayOptions
            {
                BufferSize = _configuration.Tuning.RelayBufferBytes,
                ConnectTimeout = _configuration.Tuning.PrinterConnectTimeout,
                AllowedClientNetworks = [permitted],
            },
            new RelayLogger(_log));

        await relay.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decides which interfaces each of the service's two mDNS sockets joins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The responder's socket joins every client interface, for IPv4 and IPv6.
    /// iOS was measured querying over IPv6 alone, so the client side needs both
    /// (docs/findings/2026-09-06-ipv6-mdns-transport.md).
    /// </para>
    /// <para>
    /// The resolver's socket joins the printer interface, for IPv4 only, and
    /// nothing else. REQ-RES-006 confines resolution to that side, and the
    /// printer is IPv4-only, so joining ff02::fb there would put MLD reports onto
    /// the printer network and gain nothing.
    /// </para>
    /// <para>
    /// Each socket has one reader, so neither component can take a datagram
    /// meant for the other (docs/findings/2026-09-14-shared-receive-loop.md).
    /// The two lists share no interface provided the configuration is valid:
    /// this method does not check that itself, because REQ-CFG-006 already
    /// refuses a configuration that uses one interface for both roles. This is
    /// the one place that knows which interface plays which role - MdnsSocket is
    /// told where to join, never why.
    /// </para>
    /// </remarks>
    public static MdnsSocketPlan PlanMdnsBindings(
        IReadOnlyList<MdnsInterface> clientInterfaces, MdnsInterface printerInterface)
    {
        ArgumentNullException.ThrowIfNull(clientInterfaces);
        ArgumentNullException.ThrowIfNull(printerInterface);

        var responder = new List<MdnsBinding>(clientInterfaces.Count);
        foreach (MdnsInterface client in clientInterfaces)
        {
            responder.Add(new MdnsBinding(client.Address, JoinIPv6: true));
        }

        var resolver = new List<MdnsBinding> { new(printerInterface.Address, JoinIPv6: false) };

        return new MdnsSocketPlan(responder, resolver);
    }

    /// <summary>
    /// Resolves both of the printer's services at startup, before anything is
    /// advertised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>_ipp._tcp</c> answer supplies the capabilities the advertisement
    /// is built from. The <c>_ipps._tcp</c> answer is resolved here only so that
    /// a configuration naming a service the printer does not advertise stops
    /// startup; every connection resolves it again for itself
    /// (<see cref="LocateConnectionAsync"/>). If either cannot be resolved, the
    /// resolver's exception propagates, and its message names the instance that
    /// did not answer.
    /// </para>
    /// <para>
    /// One resolver serves both names, and it caches one answer. Resolving
    /// <c>_ipps._tcp</c> second leaves that answer in the cache, which is the
    /// one every connection asks for. If anything later resolved both names
    /// repeatedly, each would evict the other and every lookup would query.
    /// </para>
    /// </remarks>
    [Requirement("REQ-RES-007",
        "Resolves the _ipp._tcp and _ipps._tcp instances at startup, before anything is advertised, takes capabilities only from the _ipp._tcp answer, and lets a failure to resolve either stop startup with that instance named.")]
    public static async Task<PrinterAtStartup> ResolveAtStartupAsync(
        PrinterResolver resolver,
        DnsName ippInstance,
        DnsName ippsInstance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(ippInstance);
        ArgumentNullException.ThrowIfNull(ippsInstance);

        ResolvedPrinter capabilities = await resolver
            .ResolveAsync(ippInstance, timeout, cancellationToken)
            .ConfigureAwait(false);

        ResolvedPrinter connection = await resolver
            .ResolveAsync(ippsInstance, timeout, cancellationToken)
            .ConfigureAwait(false);

        return new PrinterAtStartup(capabilities, connection);
    }

    /// <summary>
    /// Finds where one connection to the printer should go: resolves the
    /// printer's <c>_ipps._tcp</c> instance, logs the lookup, and returns the
    /// address and port from that answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolver used by the service is built without a clock, so its answers
    /// are stamped by <see cref="TimeProvider.System"/>, the same clock read
    /// here. That is what lets <see cref="DescribeLookup"/> tell a cached answer
    /// from a queried one by comparing the two times.
    /// </para>
    /// <para>
    /// The relay does not say which client a lookup is for, so when two jobs
    /// start together their "located" lines cannot be matched to their
    /// "relaying" lines with certainty. Accepted rather than fixed: fixing it
    /// would mean changing IppRelay.
    /// </para>
    /// </remarks>
    [Requirement("REQ-RES-007",
        "Resolves the _ipps._tcp instance for each connection and returns the address and port from that answer, never from the _ipp._tcp one.")]
    public static async Task<IPEndPoint> LocateConnectionAsync(
        PrinterResolver resolver,
        DnsName ippsInstance,
        TimeSpan timeout,
        IServiceLog log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(ippsInstance);
        ArgumentNullException.ThrowIfNull(log);

        DateTimeOffset started = TimeProvider.System.GetUtcNow();
        long startedTimestamp = Stopwatch.GetTimestamp();

        ResolvedPrinter current = await resolver
            .ResolveAsync(ippsInstance, timeout, cancellationToken)
            .ConfigureAwait(false);

        log.Info(DescribeLookup(current, started, Stopwatch.GetElapsedTime(startedTimestamp)));

        return new IPEndPoint(current.Address, current.Port);
    }

    /// <summary>
    /// The log line for one job's printer lookup: which instance, where it was
    /// found, whether the answer came from the cache or a query, and how long
    /// the lookup took.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An answer obtained before the lookup began can only have come from the
    /// resolver's cache, so it is reported as cached. Any other answer was
    /// obtained while this lookup was waiting and is reported as queried. If two
    /// jobs look the printer up at once, the second waits for the first job's
    /// query and is also reported as queried, with the time it spent waiting.
    /// </para>
    /// <para>
    /// These lines exist to measure what a lookup costs, which decides whether
    /// the cache that REQ-RES-004 permits is kept. Milliseconds are shown for
    /// that reason.
    /// </para>
    /// </remarks>
    /// <param name="printer">The answer the resolver returned.</param>
    /// <param name="lookupStarted">When the lookup began, by the resolver's clock.</param>
    /// <param name="elapsed">How long the lookup took.</param>
    public static string DescribeLookup(ResolvedPrinter printer, DateTimeOffset lookupStarted, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(printer);

        string source = printer.ResolvedAt < lookupStarted ? "cached" : "queried";

        return $"Job: located {printer.Instance} at {printer.Address}:{printer.Port} "
               + $"({source}, {elapsed.TotalSeconds:0.###}s).";
    }

    /// <summary>
    /// Prefix length for a private address range. Home networks are /24 in
    /// practice; this is deliberately conservative, since a wider guess would
    /// permit clients the operator did not intend.
    /// </summary>
    private static int PrefixLengthFor(IPAddress address)
    {
        byte[] octets = address.GetAddressBytes();
        return octets[0] == 10 ? 8 : 24;
    }
}

/// <summary>What startup found for each of the printer's two services.</summary>
/// <param name="Capabilities">
/// The <c>_ipp._tcp</c> answer. Its TXT record is what the advertisement is
/// built from.
/// </param>
/// <param name="Connection">
/// The <c>_ipps._tcp</c> answer at startup. Connections do not use this one;
/// each resolves the instance again when it is made.
/// </param>
public sealed record PrinterAtStartup(ResolvedPrinter Capabilities, ResolvedPrinter Connection);

/// <summary>The bindings for the service's two mDNS sockets.</summary>
/// <param name="Responder">
/// What the responder's socket joins: every client interface. In a valid
/// configuration none of them is the printer interface (REQ-CFG-006).
/// </param>
/// <param name="Resolver">
/// What the resolver's socket joins: the printer interface, and nothing else.
/// </param>
public sealed record MdnsSocketPlan(IReadOnlyList<MdnsBinding> Responder, IReadOnlyList<MdnsBinding> Resolver);

/// <summary>Maps relay events onto the service log.</summary>
/// <remarks>
/// Endpoints, counts and durations only. The observer interface offers nothing
/// else, and neither does this.
/// </remarks>
internal sealed class RelayLogger(IServiceLog log) : IRelayObserver
{
    public void ConnectionAccepted(EndPoint? client) =>
        log.Info($"Job: accepted from {Describe(client)}.");

    public void ConnectionRefused(EndPoint? client, string reason) =>
        log.Warn($"Job: refused {Describe(client)}: {reason}");

    public void RelayStarted(EndPoint? client, IPEndPoint printer) =>
        log.Info($"Job: relaying {Describe(client)} -> {printer}.");

    public void RelayCompleted(
        EndPoint? client, IPEndPoint printer, long bytesToPrinter, long bytesToClient, TimeSpan duration) =>
        log.Info($"Job: completed {Describe(client)} -> {printer}; "
                 + $"{bytesToPrinter} bytes sent, {bytesToClient} received, "
                 + $"{duration.TotalSeconds:0.##}s.");

    // The counts are printed on every failure, including those that happen
    // before a connection exists and therefore read as zero. A uniform line is
    // easier to read across a run than one that sometimes carries numbers: the
    // reason says what went wrong, and the numbers always say how far it got.
    public void RelayFailed(
        EndPoint? client, string reason, long bytesToPrinter, long bytesToClient, TimeSpan duration) =>
        log.Error($"Job: failed for {Describe(client)}: {reason} "
                  + $"({bytesToPrinter} bytes sent, {bytesToClient} received, "
                  + $"{duration.TotalSeconds:0.##}s)");

    private static string Describe(EndPoint? endpoint) => endpoint?.ToString() ?? "(unknown)";
}

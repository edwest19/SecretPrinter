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

        // The IPv6 mDNS group is joined on client interfaces only. iOS was
        // measured querying over IPv6 alone, so the client side needs it; the
        // printer is IPv4-only at 192.168.12.180 and REQ-RES-006 confines
        // resolution to that side, so joining ff02::fb there would put MLD
        // reports onto the printer network and gain nothing. This is the one
        // place that knows which interface plays which role - MdnsSocket is told
        // where to join, never why.
        var bindings = new List<MdnsBinding>();
        foreach (MdnsInterface client in _configuration.ClientInterfaces)
        {
            bindings.Add(new MdnsBinding(client.Address, JoinIPv6: true));
        }

        bindings.Add(new MdnsBinding(_configuration.PrinterInterface.Address, JoinIPv6: false));

        using MdnsSocket socket = MdnsSocket.Open(bindings);
        MdnsSocketConfiguration socketConfiguration = socket.ReadBackConfiguration();

        _log.Info($"Bound {socketConfiguration.LocalEndPoint} "
                  + $"(reuse={socketConfiguration.ReuseAddress}, "
                  + $"pktinfo={socketConfiguration.PacketInformation}, "
                  + $"ttl={socketConfiguration.MulticastTimeToLive}).");

        MdnsIPv6SocketConfiguration? socketConfiguration6 = socket.ReadBackIPv6Configuration();
        if (socketConfiguration6 is null)
        {
            _log.Info("No IPv6 socket was opened: no interface asked to join ff02::fb.");
        }
        else
        {
            _log.Info($"Bound {socketConfiguration6.LocalEndPoint} "
                      + $"(reuse={socketConfiguration6.ReuseAddress}, "
                      + $"v6only={!socketConfiguration6.DualMode}, "
                      + $"pktinfo={socketConfiguration6.PacketInformation}, "
                      + $"hops={socketConfiguration6.MulticastHopLimit}).");

            foreach (MdnsInterface joined in socketConfiguration6.Interfaces)
            {
                _log.Info($"Joined {MdnsSocket.MulticastGroupV6} on {joined}.");
            }

            // Said plainly, in the log, every time. An operator watching startup
            // must not read "joined ff02::fb" as "answers IPv6 queries".
            _log.Info("IPv6 mDNS is joined but not yet read or answered (REQ-ADV-018 unmet).");
        }

        using var resolver = new PrinterResolver(socket, _configuration.PrinterInterface);

        // The advertisement is built from what the printer says now, not from
        // anything stored. If the printer cannot be reached, the service does
        // not start: advertising capabilities nobody has confirmed would be a
        // claim we cannot support.
        DnsName printerInstance = DnsName.Parse(_configuration.PrinterInstance);
        _log.Info($"Asking {printerInstance} what it can do, on {_configuration.PrinterInterface}.");

        ResolvedPrinter printer = await resolver
            .ResolveAsync(printerInstance, _configuration.Tuning.PrinterResolveTimeout, cancellationToken)
            .ConfigureAwait(false);

        _log.Info($"Printer found: {printer}.");

        var capabilities = new PrinterCapabilities(
            printer.TxtStrings,
            printer.Port,
            CapabilitySource.FromMdnsQuery(
                printer.Address, new DnsNameLike(printerInstance.ToString()), printer.ResolvedAt));

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
            advertised.Add(new AdvertisedInterface(client, advertisement));
        }

        var responder = new MdnsResponder(socket, advertised);

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new List<Task>();

        try
        {
            await responder.AnnounceAsync(TimeSpan.FromSeconds(1), stopping.Token).ConfigureAwait(false);
            _log.Info("Announced. Answering queries.");

            running.Add(responder.ServeAsync(stopping.Token));

            foreach (MdnsInterface client in _configuration.ClientInterfaces)
            {
                running.Add(RunRelayAsync(client, resolver, printerInstance, stopping.Token));
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
            _log.Info("SecretPrinter stopped.");
        }
    }

    /// <summary>Accepts and relays print jobs on one client interface.</summary>
    [Requirement("REQ-PXY-001",
        "Binds the listener to one client interface address, never the wildcard, so jobs cannot be accepted on the printer network.")]
    [Requirement("REQ-SEC-012",
        "Permits connections only from the network of the interface the listener is bound to.")]
    private async Task RunRelayAsync(
        MdnsInterface client,
        PrinterResolver resolver,
        DnsName printerInstance,
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
            new TcpConnectionFactory(),
            async token =>
            {
                ResolvedPrinter current = await resolver
                    .ResolveAsync(printerInstance, _configuration.Tuning.PrinterResolveTimeout, token)
                    .ConfigureAwait(false);

                return new IPEndPoint(current.Address, current.Port);
            },
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

    public void RelayFailed(EndPoint? client, string reason) =>
        log.Error($"Job: failed for {Describe(client)}: {reason}");

    private static string Describe(EndPoint? endpoint) => endpoint?.ToString() ?? "(unknown)";
}

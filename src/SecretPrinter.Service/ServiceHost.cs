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
// The printer watched on the RFC 6762 schedule, with the listener closed and
// the advertisement withdrawn while it cannot be reached, by Claude (Anthropic
// model, Claude Opus 5) at the direction of Edwin West, 2026-09-20, for
// REQ-LIF-006. Reviewed by a human before merge.
//
// The encryption of each relayed connection added to its "relaying" log line,
// and RelayLogger made public so a test can read that line, by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-22,
// for REQ-OBS-008. Reviewed by a human before merge.
//
// Startup changed to wait, withdrawn, for a usable printer-side interface and
// for the printer to answer, instead of refusing to start, and the responder's
// socket opened only once both are there, by Claude (Anthropic model, Claude
// Opus 5.5) at the direction of Edwin West, 2026-09-22, for REQ-LIF-008.
// Reviewed by a human before merge.
//
// A comment that said iOS queries over IPv6 alone corrected by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-24. Comments only; no behaviour changed. The original words are kept
// in docs/findings/2026-09-24-the-ipv6-only-claim-was-in-more-places.md.
// Reviewed by a human before merge.
//
// Each job's printer lookup hands its answer to the watch, so that a resolution
// performed because a job arrived resets the reachability schedule, by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-25, for REQ-RES-008. Until then nothing did; see
// docs/findings/2026-09-25-two-clauses-of-req-res-008-were-never-built.md.
// Reviewed by a human before merge.
//
// The printer-side socket and resolver held in a PrinterSide and reopened
// before every question put to a printer that has not been answering, at
// startup and after, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-25, for REQ-RES-009. See
// docs/findings/2026-09-25-a-reconnect-did-not-restore-the-membership.md.
// Reviewed by a human before merge.
//
// The advertised names probed for before every offer, at startup and before
// each restore, and a name conflict withdrawing the printer until restart, by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-28, for REQ-ADV-023 and REQ-ADV-024. The gate now starts closed, the
// receive loop starts before the startup probe, and OfferAsync moved to
// Offering.OnReachabilityChangedAsync. Reviewed by a human before merge.
//
// The call to AdvertisementBuilder.Build given an explicit empty link-local list
// by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-30, when Build began taking one. No behaviour changed: no AAAA record
// is published until the relay listens on the addresses. Reviewed by a human
// before merge.
//
// Each client interface's preferred link-local addresses published as AAAA
// records, and listened on, by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-30, for REQ-ADV-021: the addresses are read
// once at startup; the listeners are read out of the published records by
// ListenPlan and opened all together or not at all; if one cannot be opened,
// the service logs why and stops, to be restarted. Edwin chose that over
// following address changes (docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md).
// Reviewed by a human before merge.
//
// The failure of one part of the running service made to stop the rest, by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-07, for REQ-LIF-004 and REQ-ADV-021. RunAsync waited for its parts
// with Task.WhenAll, which ends only when every part has ended. A part that
// failed was therefore never noticed while the others ran: a listener that
// could not be opened logged that the service was stopping, and the service
// went on answering queries with nothing listening. RunPartsAsync replaces
// that wait. The responder's socket errors are also logged now, through its
// new callback. See
// docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.
// Reviewed by a human before merge.
//
// The first line of the stop summary now written by
// ResponderActivity.DescribeServed, which adds the count of queries left
// unanswered because they carried every answer (REQ-ADV-026), by Claude
// (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-10-10. Without that count those queries would be among the ones seen
// and not served, with nothing to say why; before the responder left them out
// they were answered. The line moved so that a test can read it. See
// docs/findings/2026-10-10-a-question-that-carries-its-answer-is-no-longer-answered.md.
// Reviewed by a human before merge.
//
// An AdapterWatch started next to the printer watch, by Claude (Anthropic
// model, Claude Opus 5.5) at the direction of Edwin West, 2026-10-10, for
// REQ-RES-010. It examines the printer-side adapter every five seconds and
// tells the watch when the adapter is usable again, so that a printer held
// unreachable is asked at once rather than at the next of questions that by
// then may be an hour apart. No test runs this wiring, as no test runs
// RunAsync. See
// docs/findings/2026-10-10-the-printer-is-asked-again-when-its-network-comes-back.md.
// Reviewed by a human before merge.
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
using System.Net.Sockets;
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
    private const string StoppedWhileWithdrawn =
        "Stopped while waiting at startup. Nothing had been offered to the client network, so there is "
        + "nothing to retract.";

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
        "Any failure opening a socket propagates out of startup rather than leaving the service half-running. A printer-side interface that is not usable, or a printer that does not answer, is waited for instead (REQ-LIF-008), with nothing offered meanwhile.")]
    [Requirement("REQ-ADV-021",
        "Publishes an AAAA record for each preferred link-local address of each client interface, read once at startup, and no other IPv6 address; the relays listen on what is published (RunRelayAsync).")]
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

        // The printer side first, and nothing on the client side until it is
        // there (REQ-LIF-008). While the adapter is unusable or the printer
        // silent, the service runs withdrawn: no responder socket, no
        // advertisement, no listener. Both waits log when they begin and end.
        MdnsInterface printerInterface;
        try
        {
            printerInterface = await StartupWait.ForPrinterInterfaceAsync(
                    _configuration.PrinterInterfaceName,
                    SystemInterfaceInventory.Instance,
                    _log,
                    (wait, token) => Task.Delay(wait, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Info(StoppedWhileWithdrawn);
            throw;
        }

        // Two sockets, one per role. Until 2026-09-16 the responder and the
        // resolver shared one, and whichever was reading took the next datagram,
        // so the printer's reply to the resolver could be consumed and discarded
        // by the responder (docs/findings/2026-09-14-shared-receive-loop.md).
        // PlanMdnsBindings decides what each socket joins; this method only opens
        // them. The resolver's is opened first, because the printer must answer
        // before the responder's is opened at all.
        MdnsSocketPlan plan = PlanMdnsBindings(_configuration.ClientInterfaces, printerInterface);

        using MdnsSocket resolverSocket = MdnsSocket.Open(plan.Resolver);
        MdnsSocketConfiguration resolverConfiguration = resolverSocket.ReadBackConfiguration();

        _log.Info($"Resolver socket: bound {resolverConfiguration.LocalEndPoint} "
                  + $"(reuse={resolverConfiguration.ReuseAddress}, "
                  + $"pktinfo={resolverConfiguration.PacketInformation}, "
                  + $"ttl={resolverConfiguration.MulticastTimeToLive}), "
                  + $"joined {MdnsSocket.MulticastGroup} on "
                  + $"{string.Join(", ", resolverConfiguration.Interfaces)} only.");

        // The socket and the resolver on it are held together, so they can be
        // reopened on the adapter's current address while the printer is not
        // answering (REQ-RES-009). PrinterSide disposes whichever socket it
        // holds last. The using on resolverSocket above still disposes the
        // first one if anything between here and there throws; disposing a
        // socket twice is harmless.
        using var printerSide = new PrinterSide(
            printerInterface,
            resolverSocket,
            reopenOn => MdnsSocket.Open(PlanMdnsBindings(_configuration.ClientInterfaces, reopenOn).Resolver),
            () => StartupWait.Examine(_configuration.PrinterInterfaceName, SystemInterfaceInventory.Instance),
            _log);

        // Closed until the advertised names are claimed: it opens only when the
        // startup probe comes back clear (REQ-ADV-023). Everything that offers
        // the printer to the client network reads this one switch.
        var offering = new AvailabilityGate(open: false);

        // The advertisement is built from what the printer says now, not from
        // anything stored. Until the printer answers nothing is advertised:
        // capabilities nobody has confirmed would be a claim we cannot support.
        DnsName ippInstance = DnsName.Parse(_configuration.PrinterInstance);
        DnsName ippsInstance = DnsName.Parse(_configuration.PrinterIppsInstance);
        _log.Info($"Asking {ippInstance} what it can do, and {ippsInstance} where to connect, "
                  + $"on {printerInterface}.");

        PrinterAtStartup found;
        int startupAttempts = 0;
        try
        {
            found = await StartupWait.ForPrinterAsync(
                    // Every attempt after the first follows one that went
                    // unanswered. See AskAtStartupAsync.
                    token => AskAtStartupAsync(
                        printerSide,
                        afterUnanswered: startupAttempts++ > 0,
                        ippInstance,
                        ippsInstance,
                        _configuration.Tuning.PrinterResolveTimeout,
                        token),
                    _log,
                    (wait, token) => Task.Delay(wait, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Info(StoppedWhileWithdrawn);
            throw;
        }

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

            // The interface's preferred link-local addresses: the only IPv6
            // addresses the service publishes, and the relay listens on exactly
            // what is published (REQ-ADV-021; ListenPlan). Read once, here, as
            // the IPv4 address is; a restart picks up new ones.
            IReadOnlyList<IPAddress> linkLocal = MdnsInterfaceResolver.ResolveLinkLocal(companion);
            if (linkLocal.Count == 0)
            {
                _log.Warn($"{client.Name} holds no preferred IPv6 link-local address, so no AAAA record is "
                          + "published for it and print jobs are accepted over IPv4 only.");
            }
            else
            {
                _log.Info($"Publishing {string.Join(", ", linkLocal)} for {client.Name} in AAAA records: its "
                          + "link-local address(es), and no other IPv6 address (REQ-ADV-021).");
            }

            Advertisement advertisement =
                AdvertisementBuilder.Build(capabilities, identity, client.Address, linkLocal);
            AdvertisementLog.Write(_log, client, advertisement);

            advertised.Add(new AdvertisedInterface(client, advertisement, companion));
            _log.Info($"Answering on {client} and, for queries that arrive over IPv6, {companion}.");
        }

        // The responder reports its first name conflict here, on its receive
        // loop. The callback only completes this task; closing the gate, the
        // log line and the goodbyes are WithdrawOnConflictAsync's (REQ-ADV-024).
        var conflict = new TaskCompletionSource<NameConflict>(TaskCreationOptions.RunContinuationsAsynchronously);

        var responder = new MdnsResponder(
            responderSocket,
            advertised,
            () => offering.IsOpen,
            conflicted: found => conflict.TrySetResult(found),

            // The receive loop goes on after a socket error (REQ-LIF-005). The
            // responder reports each different error once, and this is where
            // it reaches the log.
            socketError: description => _log.Warn(
                $"mDNS on the client side met a socket error {description}"
                + (description.EndsWith('.') ? " " : ". ")
                + "The service goes on answering. This is logged again only if the error changes."));

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new List<Task>();

        try
        {
            // The receive loop first: the answers to the probe arrive through it.
            // Nothing is answered meanwhile - the gate is closed, and the
            // responder is silent while it probes.
            running.Add(responder.ServeAsync(stopping.Token));
            running.Add(Offering.WithdrawOnConflictAsync(conflict.Task, responder, offering, _log, stopping.Token));

            if (await Offering.ClaimAndOfferAsync(
                    responder, offering, _log, delay: null, Offering.AnnouncementInterval, stopping.Token)
                .ConfigureAwait(false))
            {
                _log.Info("Announced. Answering queries.");
            }

            var reachability = new PrinterReachability(found.Connection);

            var watch = new PrinterWatch(
                reachability,

                // See AskForWatchAsync. The watch calls this on its own loop,
                // so reading reachability here is reading it in order.
                token => AskForWatchAsync(
                    printerSide,
                    reachability.IsReachable,
                    ippsInstance,
                    _configuration.Tuning.PrinterResolveTimeout,
                    token),
                _log,
                onChanged: (reachable, token) => Offering.OnReachabilityChangedAsync(
                    responder, offering, _log, reachable, delay: null, Offering.AnnouncementInterval, token));

            running.Add(watch.WatchAsync(stopping.Token));

            // The printer-side adapter examined every five seconds, by the
            // startup wait's rule, for as long as the service runs. When it
            // is usable again after being seen not usable, or usable on
            // another address or index, the watch is told; if it holds the
            // printer unreachable, it asks at once and starts the RFC 6762
            // questions over from one second (REQ-RES-010). The adapter's
            // state never makes the printer reachable: only an answer does.
            var adapterWatch = new AdapterWatch(
                printerInterface,
                () => StartupWait.Examine(_configuration.PrinterInterfaceName, SystemInterfaceInventory.Instance),
                usable => watch.StartQuestionsOver(usable.ToString()),
                _log);

            running.Add(adapterWatch.WatchAsync(stopping.Token));

            foreach (AdvertisedInterface entry in advertised)
            {
                running.Add(RunRelayAsync(entry, printerSide, ippsInstance, printerPin, offering, watch, stopping.Token));
            }

            // Until every part has ended. If one fails, the rest are stopped
            // and the failure leaves this method, after the goodbye below.
            await RunPartsAsync(
                    running,
                    failure =>
                    {
                        _log.Error(
                            "A part of the service failed, so the whole service is stopping: "
                            + $"{failure.GetType().Name}: {failure.Message}");
                        return stopping.CancelAsync();
                    })
                .ConfigureAwait(false);
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
            _log.Info(activity.DescribeServed());
            _log.Info(activity.DescribeByTransport());
            _log.Info("SecretPrinter stopped.");
        }
    }

    /// <summary>
    /// Waits for the parts of the running service, and stops all of them when
    /// one fails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parts are the responder's receive loop, the conflict handler, the
    /// printer watch and one relay for each client interface. Each runs until
    /// the service is stopped, except the conflict handler, which ends by
    /// itself once it has dealt with a conflict. A part that ends without an
    /// error is therefore not a reason to stop the others.
    /// </para>
    /// <para>
    /// A part that ends with an error is. Until 2026-10-07 this wait was
    /// <c>Task.WhenAll</c>, which completes only when every task has, so a
    /// failed part was not noticed until the service was stopped for some other
    /// reason. A relay whose listeners could not be opened logged "Stopping"
    /// and threw, and the service went on answering queries for a printer with
    /// nothing listening behind it, which is what REQ-ADV-021 says cannot
    /// happen.
    /// </para>
    /// <para>
    /// On the first failure <paramref name="onFirstFailure"/> is called, once,
    /// and is expected to stop the other parts. They are then waited for, and
    /// the first failure is thrown, whatever the others did on their way out.
    /// </para>
    /// </remarks>
    /// <param name="parts">The tasks that make up the running service.</param>
    /// <param name="onFirstFailure">
    /// Called with the error of the first part to fail. In the service it logs
    /// the error and cancels the token every part runs under.
    /// </param>
    [Requirement("REQ-LIF-004",
        "The failure of any part of the running service stops every other part and leaves RunAsync as that failure, so the service does not go on with a part missing.")]
    [Requirement("REQ-ADV-021",
        "A relay that could not open its listeners stops the whole service, so the published addresses are retracted and not left with nothing listening.")]
    public static async Task RunPartsAsync(IReadOnlyList<Task> parts, Func<Exception, Task> onFirstFailure)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(onFirstFailure);

        var remaining = new List<Task>(parts);
        Task? failed = null;

        while (remaining.Count > 0)
        {
            Task ended = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(ended);

            if (ended.IsFaulted)
            {
                failed = ended;
                break;
            }
        }

        if (failed is null)
        {
            // Every part has ended and none failed. Awaited so that a part that
            // was cancelled surfaces as a cancellation, as it did before.
            await Task.WhenAll(parts).ConfigureAwait(false);
            return;
        }

        // A faulted task holds an AggregateException around what was thrown,
        // and the handler is to be told what was thrown.
        AggregateException held = failed.Exception!;
        await onFirstFailure(held.InnerException ?? held).ConfigureAwait(false);

        try
        {
            await Task.WhenAll(remaining).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // What the other parts did while being stopped is not the news. The
            // first failure is, and it is thrown below.
        }

        await failed.ConfigureAwait(false);
    }

    /// <summary>Accepts and relays print jobs on one client interface.</summary>
    [Requirement("REQ-PXY-001",
        "Binds the listeners to the client interface's own published addresses, never the wildcard, so jobs cannot be accepted on the printer network.")]
    [Requirement("REQ-SEC-012",
        "Permits connections only from the network of the interface each listener is bound to: its IPv4 network, or, on a link-local listener, fe80::/10 on that interface's scope (ListenPlan.From).")]
    [Requirement("REQ-PXY-010",
        "The relay is given a factory that opens TLS connections only, so there is no path by which a job "
        + "reaches the printer over a plain socket.")]
    [Requirement("REQ-LIF-006",
        "Holds the listener open only while the printer is on offer. When the gate closes the listener is "
        + "disposed, so a client with a stale entry is refused at once instead of waiting out a resolve "
        + "timeout for a printer this service has established it cannot reach.")]
    private async Task RunRelayAsync(
        AdvertisedInterface advertised,
        PrinterSide printerSide,
        DnsName ippsInstance,
        CertificatePin printerPin,
        AvailabilityGate offering,
        PrinterWatch watch,
        CancellationToken cancellationToken)
    {
        MdnsInterface client = advertised.Interface;

        // The permitted IPv4 network is derived from the interface itself rather
        // than configured separately, so the two cannot drift apart. What to
        // listen on is read out of what is published, for the same reason
        // (REQ-ADV-021).
        var ipv4Network = new IPNetwork(client.Address, PrefixLengthFor(client.Address));
        IReadOnlyList<ListenEntry> plan = ListenPlan.From(advertised, ipv4Network);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await offering.WaitForOpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var serving = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task withdrawn = offering.WaitForCloseAsync(serving.Token);

            await AcceptUntilWithdrawnAsync(
                    client, plan, printerSide, ippsInstance, printerPin, watch, withdrawn, serving)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Listens on every published address of one client interface until the
    /// printer stops being offered, or until the service stops.
    /// </summary>
    /// <remarks>
    /// The listeners open all together or not at all (ListenPlan.OpenAllAsync).
    /// If one cannot be opened - a published address has gone since startup -
    /// the failure is logged and raised, and the service says goodbye and stops,
    /// rather than serve while an address it publishes has nothing behind it.
    /// Addresses are read once, at startup, so a restart picks up new ones.
    /// </remarks>
    [Requirement("REQ-ADV-021",
        "Opens a listener on every address published for the interface, or none: if one cannot be opened, the failure is logged and the service stops rather than leave a published address with nothing listening.")]
    private async Task AcceptUntilWithdrawnAsync(
        MdnsInterface client,
        IReadOnlyList<ListenEntry> plan,
        PrinterSide printerSide,
        DnsName ippsInstance,
        CertificatePin printerPin,
        PrinterWatch watch,
        Task withdrawn,
        CancellationTokenSource serving)
    {
        IReadOnlyList<IConnectionListener> listeners;
        try
        {
            listeners = await ListenPlan
                .OpenAllAsync(plan, entry => new TcpConnectionListener(entry.Address, _configuration.ListenPort))
                .ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            _log.Error($"Could not listen on every address published for {client.Name} "
                       + $"({string.Join(", ", plan.Select(entry => entry.Address))}): {ex.Message} "
                       + "The addresses are read once, at startup; if one has changed, restart the service to "
                       + "publish and listen on the current ones. Stopping, so that no address is published "
                       + "with nothing listening behind it.");
            throw;
        }

        try
        {
            var accepting = new List<Task>();

            for (int i = 0; i < listeners.Count; i++)
            {
                ListenEntry entry = plan[i];
                IConnectionListener listener = listeners[i];

                _log.Info($"Accepting print jobs on {listener.LocalEndPoint} ({client.Name}); permitted clients: "
                          + string.Join(", ", entry.PermittedNetworks)
                          + (entry.PermittedLinkLocalScopes.Count > 0
                              ? $" on scope {string.Join(", ", entry.PermittedLinkLocalScopes)}."
                              : "."));

                var relay = new IppRelay(
                    listener,

                    // The TCP factory wrapped in the TLS one: the relay asks for a
                    // connection and is handed one that has completed a handshake and
                    // passed the pin check, or none at all. IppRelay itself knows
                    // nothing about TLS and did not change when this was added.
                    new TlsConnectionFactory(new TcpConnectionFactory(), printerPin),
                    // The resolver is read for each connection, not captured once:
                    // it is replaced whenever the printer-side socket is reopened.
                    token => LocateConnectionAsync(
                        printerSide.Resolver,
                        ippsInstance,
                        _configuration.Tuning.PrinterResolveTimeout,
                        _log,
                        watch.RecordJobAnswer,
                        token),
                    new RelayOptions
                    {
                        BufferSize = _configuration.Tuning.RelayBufferBytes,
                        ConnectTimeout = _configuration.Tuning.PrinterConnectTimeout,
                        AllowedClientNetworks = entry.PermittedNetworks,
                        AllowedLinkLocalScopes = entry.PermittedLinkLocalScopes,
                    },
                    new RelayLogger(_log));

                accepting.Add(relay.RunAsync(serving.Token));
            }

            // The first of: the gate closing, or any relay ending on its own.
            // Either way every relay on this interface stops together.
            await Task.WhenAny(Task.WhenAny(accepting), withdrawn).ConfigureAwait(false);
            await serving.CancelAsync().ConfigureAwait(false);

            foreach (Task task in accepting)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected: the gate closed, or the service is stopping.
                }
            }
        }
        finally
        {
            foreach (IConnectionListener listener in listeners)
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }
        }

        _log.Info($"No longer accepting print jobs on {client.Name}.");
    }

    /// <summary>
    /// Decides which interfaces each of the service's two mDNS sockets joins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The responder's socket joins every client interface, for IPv4 and IPv6.
    /// An iPhone was measured sending the same queries over both, and on the
    /// network this was built on only the IPv6 copies reached the service's
    /// machine, so the client side needs both
    /// (docs/findings/2026-09-24-ipv4-mdns-from-behind-the-access-point.md).
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
    /// a configuration naming a service the printer does not advertise is found
    /// at startup; every connection resolves it again for itself
    /// (<see cref="LocateConnectionAsync"/>). If either cannot be resolved, the
    /// resolver's exception propagates, and its message names the instance that
    /// did not answer. <see cref="StartupWait.ForPrinterAsync"/> catches it,
    /// logs it, and asks again (REQ-LIF-008).
    /// </para>
    /// <para>
    /// One resolver serves both names, and it caches one answer. Resolving
    /// <c>_ipps._tcp</c> second leaves that answer in the cache, which is the
    /// one every connection asks for. If anything later resolved both names
    /// repeatedly, each would evict the other and every lookup would query.
    /// </para>
    /// </remarks>
    [Requirement("REQ-RES-007",
        "Resolves the _ipp._tcp and _ipps._tcp instances at startup, before anything is advertised, takes capabilities only from the _ipp._tcp answer, and fails naming the instance when either cannot be resolved.")]
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
    /// One startup attempt to resolve both of the printer's services. An
    /// attempt that follows one that went unanswered reopens the printer-side
    /// socket first (REQ-RES-009), as the watch does for a printer it holds
    /// unreachable: startup can wait for hours, and the adapter can drop and
    /// return while it does.
    /// </summary>
    [Requirement("REQ-RES-009",
        "Reopens the printer-side socket before each startup attempt that follows an unanswered one.")]
    public static Task<PrinterAtStartup> AskAtStartupAsync(
        PrinterSide printerSide,
        bool afterUnanswered,
        DnsName ippInstance,
        DnsName ippsInstance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(printerSide);

        if (afterUnanswered)
        {
            printerSide.Reopen();
        }

        return ResolveAtStartupAsync(printerSide.Resolver, ippInstance, ippsInstance, timeout, cancellationToken);
    }

    /// <summary>
    /// The watch's question to the printer. While the printer is held
    /// unreachable the printer-side socket is reopened first (REQ-RES-009), so
    /// a membership of 224.0.0.251 lost during the outage, or an address that
    /// changed, cannot keep the answer from being heard. The cached answer is
    /// then discarded, so that a question is always put on the printer network.
    /// </summary>
    /// <remarks>
    /// Invalidating matters: every reconfirmation falls at 80% of the record's
    /// TTL or later, which is still inside the window REQ-RES-004 lets the
    /// resolver answer from memory. Without it the watch would be answered from
    /// the cache every time, would never put a question on the printer network,
    /// and would report a printer that had been gone for hours as healthy.
    /// </remarks>
    [Requirement("REQ-RES-009",
        "Reopens the printer-side socket before each question the watch puts to a printer it holds unreachable.")]
    public static Task<ResolvedPrinter> AskForWatchAsync(
        PrinterSide printerSide,
        bool printerHeldReachable,
        DnsName ippsInstance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(printerSide);
        ArgumentNullException.ThrowIfNull(ippsInstance);

        if (!printerHeldReachable)
        {
            printerSide.Reopen();
        }

        PrinterResolver resolver = printerSide.Resolver;
        resolver.Invalidate();
        return resolver.ResolveAsync(ippsInstance, timeout, cancellationToken);
    }

    /// <summary>
    /// Finds where one connection to the printer should go: resolves the
    /// printer's <c>_ipps._tcp</c> instance, logs the lookup, hands the answer
    /// to <paramref name="answered"/>, and returns the address and port from
    /// that answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the service, <paramref name="answered"/> is
    /// <see cref="PrinterWatch.RecordJobAnswer"/>, so a resolution performed
    /// because a job arrived resets the reachability schedule (REQ-RES-008).
    /// Every answer is handed over, including one the resolver gave from its
    /// cache; <see cref="PrinterReachability.RecordDemandAnswer"/> takes only an
    /// answer newer than the one it holds, so a cached one changes nothing. A
    /// lookup that fails throws before anything is handed over.
    /// </para>
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
    [Requirement("REQ-RES-008",
        "Hands the answer each job's lookup obtains to the watch, so that a resolution performed because a job "
        + "arrived resets the reachability schedule.")]
    public static async Task<IPEndPoint> LocateConnectionAsync(
        PrinterResolver resolver,
        DnsName ippsInstance,
        TimeSpan timeout,
        IServiceLog log,
        Action<ResolvedPrinter> answered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(ippsInstance);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(answered);

        DateTimeOffset started = TimeProvider.System.GetUtcNow();
        long startedTimestamp = Stopwatch.GetTimestamp();

        ResolvedPrinter current = await resolver
            .ResolveAsync(ippsInstance, timeout, cancellationToken)
            .ConfigureAwait(false);

        log.Info(DescribeLookup(current, started, Stopwatch.GetElapsedTime(startedTimestamp)));
        answered(current);

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
/// Endpoints, counts, durations and the printer connection's encryption only.
/// The observer interface offers nothing else, and neither does this.
/// <para>
/// Public so that SecretPrinter.Service.Tests can read the line it writes; it
/// was internal until 2026-09-22. Nothing outside this assembly constructs it
/// in shipped code.
/// </para>
/// </remarks>
public sealed class RelayLogger(IServiceLog log) : IRelayObserver
{
    public void ConnectionAccepted(EndPoint? client) =>
        log.Info($"Job: accepted from {Describe(client)}.");

    public void ConnectionRefused(EndPoint? client, string reason) =>
        log.Warn($"Job: refused {Describe(client)}: {reason}");

    [Requirement("REQ-OBS-008",
        "Writes the printer connection's encryption on the line that records each relayed connection beginning.")]
    public void RelayStarted(EndPoint? client, IPEndPoint printer, string printerEncryption) =>
        log.Info($"Job: relaying {Describe(client)} -> {printer}; encryption to printer: {printerEncryption}.");

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

// -----------------------------------------------------------------------------
// SecurityClaimsTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// The call to AdvertisementBuilder.Build given an empty link-local list by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-30, when Build began taking one. No test changed what it checks.
// Reviewed by a human before merge.
//
// Wording corrected by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-10-06. Reviewed by a human before merge. The
// list these tests read was named ShippedAssemblies, and its comment said
// diagnostic tools are left out because "they are not installed". The probe
// has been in the release folder since 2026-10-04. The list is now named
// ServiceAssemblies, three test names and two messages say "the service" where
// they said "shipped", and the probe is read by ProbeClaimsTests. No test here
// changed what it checks
// (docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).
//
// Purpose:
//   Checks the claims in Section 8 of the specification that say what the
//   service does NOT do.
//
// Why these are metadata tests:
//   "Never creates a firewall rule" cannot be proved by running the service and
//   observing that it did not. It can be proved by reading the compiled
//   assemblies and showing they contain no reference to any type capable of it.
//   That is a stronger claim than any behavioural test: it holds for every code
//   path, including ones no test exercises, and it fails the moment somebody
//   adds the capability - whether or not they use it.
//
//   The same technique proved REQ-PXY-004, and was verified there by
//   deliberately adding a File.AppendAllText and watching the test catch it.
//
// What these tests do NOT prove:
//   That the code does the right thing with the capabilities it does have. A
//   service with no Process reference can still misuse a socket. These narrow
//   the surface a reviewer must read; they do not replace reading it.
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class SecurityClaimsTests
{
    /// <summary>
    /// The assemblies that make up the service. The probe is not among them. It
    /// is in every release beside the service, but it is a separate program and
    /// opens a socket by design, so it is read by ProbeClaimsTests against its
    /// own requirement (REQ-SEC-016). The other diagnostic tools, the test
    /// harness and the spec checker are in no release, and holding them to the
    /// service's restrictions would be checking the wrong thing.
    /// </summary>
    private static readonly string[] ServiceAssemblies =
    [
        "SecretPrinter.Advertising",
        "SecretPrinter.Configuration",
        "SecretPrinter.Dns",
        "SecretPrinter.Mdns",
        "SecretPrinter.Proxy",
        "SecretPrinter.Resolution",
        "SecretPrinter.Responder",
        "SecretPrinter.Service",
        "SecretPrinter.Spec",
    ];

    /// <summary>Every type reference in an assembly, as "Namespace.Name".</summary>
    private static List<string> TypeReferences(string assemblyName)
    {
        string directory = Path.GetDirectoryName(typeof(SecurityClaimsTests).Assembly.Location)!;
        string path = Path.Combine(directory, assemblyName + ".dll");

        if (!File.Exists(path))
        {
            throw new AssertionException(
                $"{assemblyName}.dll is not beside the test assembly, so its claims cannot be checked. "
                + "Every assembly of the service must be referenced by this test project.");
        }

        var references = new List<string>();

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        MetadataReader metadata = reader.GetMetadataReader();

        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            references.Add($"{metadata.GetString(reference.Namespace)}.{metadata.GetString(reference.Name)}");
        }

        return references;
    }

    /// <summary>Finds forbidden references across the service's assemblies.</summary>
    private static List<string> FindForbidden(
        IReadOnlyList<string> forbiddenTypes, params string[] exceptAssemblies)
    {
        var found = new List<string>();

        foreach (string assembly in ServiceAssemblies)
        {
            if (exceptAssemblies.Contains(assembly, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (string reference in TypeReferences(assembly))
            {
                foreach (string forbidden in forbiddenTypes)
                {
                    if (reference.Equals(forbidden, StringComparison.Ordinal))
                    {
                        found.Add($"{assembly} references {forbidden}");
                    }
                }
            }
        }

        return found;
    }

    // ---- No external processes ----------------------------------------------

    [TestCase("Nothing in the service can start an external process")]
    [Requirement("REQ-SEC-004")]
    [Requirement("REQ-SEC-007")]
    public static void No_process_execution()
    {
        // netsh, route and every other command-line way of changing firewall
        // rules or the routing table needs one of these. Their absence is what
        // makes "this service does not touch your firewall or your routes" a
        // checkable statement rather than a promise.
        List<string> found = FindForbidden(
            ["System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo"]);

        Assert.Equal(0, found.Count,
            "no assembly of the service may be able to run a command, but found: " + string.Join(", ", found));
    }

    // ---- No registry --------------------------------------------------------

    [TestCase("Nothing in the service reads or writes the registry")]
    [Requirement("REQ-SEC-005")]
    public static void No_registry_access()
    {
        List<string> found = FindForbidden(
        [
            "Microsoft.Win32.Registry",
            "Microsoft.Win32.RegistryKey",
            "Microsoft.Win32.RegistryHive",
        ]);

        Assert.Equal(0, found.Count,
            "the service's own code touches no registry key, but found: " + string.Join(", ", found));
    }

    // ---- No network calls off the local link --------------------------------

    [TestCase("Nothing in the service can make an HTTP request")]
    [Requirement("REQ-SEC-008")]
    public static void No_http_client()
    {
        // There is no telemetry, no update check and no analytics, so there is
        // no reason for any assembly of the service to be able to speak HTTP. If one
        // could, the claim would rest on nobody having called it.
        List<string> found = FindForbidden(
        [
            "System.Net.Http.HttpClient",
            "System.Net.Http.HttpRequestMessage",
            "System.Net.Http.HttpClientHandler",
            "System.Net.WebClient",
            "System.Net.WebRequest",
            "System.Net.HttpWebRequest",
        ]);

        Assert.Equal(0, found.Count,
            "no assembly of the service may speak HTTP, but found: " + string.Join(", ", found));
    }

    // ---- Sockets confined to two projects -----------------------------------

    [TestCase("Only the mDNS and relay projects open sockets")]
    [Requirement("REQ-SEC-003")]
    public static void Sockets_are_confined()
    {
        // Confining socket types to two small projects is what lets a reviewer
        // answer "what does this listen on?" by reading those two rather than
        // the whole solution. SecretPrinter.Mdns owns UDP 5353;
        // SecretPrinter.Proxy owns the configured IPP port. Nothing else may
        // open anything.
        List<string> found = FindForbidden(
            [
                "System.Net.Sockets.Socket",
                "System.Net.Sockets.TcpListener",
                "System.Net.Sockets.TcpClient",
                "System.Net.Sockets.UdpClient",
                "System.Net.Sockets.NetworkStream",
            ],
            exceptAssemblies: ["SecretPrinter.Mdns", "SecretPrinter.Proxy"]);

        Assert.Equal(0, found.Count,
            "socket types belong only in SecretPrinter.Mdns and SecretPrinter.Proxy, but found: "
            + string.Join(", ", found));
    }

    // ---- The service log cannot carry job content ---------------------------

    [TestCase("The service log interface cannot accept payload bytes")]
    [Requirement("REQ-OBS-004")]
    public static void Service_log_cannot_receive_payload()
    {
        var offending = new List<string>();

        foreach (MethodInfo method in typeof(IServiceLog).GetMethods())
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type type = parameter.ParameterType;

                if (type == typeof(byte[]) || type == typeof(Stream)
                    || type == typeof(ReadOnlyMemory<byte>) || type == typeof(Memory<byte>))
                {
                    offending.Add($"{method.Name}({type.Name} {parameter.Name})");
                }
            }
        }

        Assert.Equal(0, offending.Count,
            "a sink can only write what it is given, but found: " + string.Join(", ", offending));
    }

    // ---- The log shows what was published, and what was not ------------------

    [TestCase("The log records every published TXT entry")]
    [Requirement("REQ-OBS-002")]
    public static void Log_shows_what_was_published()
    {
        (CollectingServiceLog log, _) = LogRealAdvertisement();
        string all = string.Join("\n", log.Entries.Select(e => e.Message));

        Assert.True(all.Contains("publish pdl=", StringComparison.Ordinal),
            "the formats actually advertised must appear in the log");
        Assert.True(all.Contains("publish URF=", StringComparison.Ordinal),
            "as must the raster capabilities");
        Assert.True(all.Contains("record  Ptr", StringComparison.Ordinal),
            "every record published must be listed, not only the TXT entries");
        Assert.True(all.Contains("capabilities observed:", StringComparison.Ordinal),
            "the log must say where the capabilities came from");
    }

    [TestCase("The log records every withheld entry and why")]
    [Requirement("REQ-OBS-003")]
    public static void Log_shows_what_was_withheld()
    {
        (CollectingServiceLog log, _) = LogRealAdvertisement();
        string all = string.Join("\n", log.Entries.Select(e => e.Message));

        // An operator must be able to confirm the honesty rules held, from the
        // log alone, without trusting this repository.
        Assert.True(all.Contains("DROP    Scan=T", StringComparison.Ordinal),
            "withholding the scanning claim must be visible");
        Assert.True(all.Contains("DROP    mopria-certified", StringComparison.Ordinal),
            "as must withholding the certification claim");
        Assert.True(all.Contains("DROP    adminurl=", StringComparison.Ordinal),
            "and the unreachable admin URL");
        Assert.True(all.Contains("REQ-ADV-005", StringComparison.Ordinal),
            "each drop should cite the requirement that forbids it");

        Assert.False(all.Contains("publish UUID=cfe92100", StringComparison.OrdinalIgnoreCase),
            "the printer's own UUID must never appear as published");
    }

    /// <summary>Builds and logs an advertisement from the real ET-3760 records.</summary>
    private static (CollectingServiceLog Log, Advertising.Advertisement Advertisement) LogRealAdvertisement()
    {
        // Real ET-3760 records. The MAC-derived tail of the hostname and UUID is
        // redacted to 000000; see docs/findings/2026-09-04-pre-publication-audit.md.
        var capabilities = new Advertising.PrinterCapabilities(
            [
                "txtvers=1",
                "pdl=application/octet-stream,image/urf,image/jpeg",
                "URF=CP1,PQ4-5,OB9,RS300,SRGB24,W8,DM3,IS1,V1.4",
                "rp=ipp/print",
                "Color=T",
                "Scan=T",
                "mopria-certified=1.3",
                "adminurl=http://EPSON000000.local.:80/PRESENTATION/BONJOUR",
                "UUID=cfe92100-67c4-11d4-a45f-f8d027000000",
            ],
            631,
            Advertising.CapabilitySource.ForTest("ET-3760 records, service log tests"));

        var identity = new Advertising.ProxyIdentity(
            "SecretPrinter (ET-3760)", "secretprinter",
            Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631);

        var client = new Mdns.MdnsInterface(
            "Ethernet 2",
            System.Net.IPAddress.Parse("192.168.1.234"),
            13,
            System.Net.Sockets.AddressFamily.InterNetwork);

        Advertising.Advertisement advertisement =
            Advertising.AdvertisementBuilder.Build(capabilities, identity, client.Address, linkLocalAddresses: []);

        var log = new CollectingServiceLog();
        AdvertisementLog.Write(log, client, advertisement);

        return (log, advertisement);
    }

    // ---- Start and stop ------------------------------------------------------

    [TestCase("Start returns without waiting for the work to finish")]
    [Requirement("REQ-LIF-001")]
    public static void Start_returns_promptly()
    {
        // The service control manager expects a start to be acknowledged
        // quickly. Blocking would have Windows report a failure to start while
        // the service was in fact running perfectly well.
        using var began = new ManualResetEventSlim();
        var log = new CollectingServiceLog();

        using var lifecycle = new ServiceLifecycle(
            async token =>
            {
                began.Set();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            },
            log);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        lifecycle.Start();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Start took {stopwatch.Elapsed.TotalSeconds:0.##}s; it must not wait for the work");
        Assert.True(began.Wait(TimeSpan.FromSeconds(5)), "and the work must actually have begun");
        Assert.True(lifecycle.IsRunning, "the lifecycle should report itself running");

        lifecycle.Stop();
    }

    [TestCase("Stop waits for shutdown work to finish")]
    [Requirement("REQ-LIF-001")]
    public static void Stop_waits_for_shutdown()
    {
        // Retracting the advertisement happens during shutdown. If Stop did not
        // wait, the process would exit before the goodbye records left and
        // every client would show the printer until its TTL expired.
        bool retracted = false;
        var log = new CollectingServiceLog();

        using var lifecycle = new ServiceLifecycle(
            async token =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }
                finally
                {
                    Thread.Sleep(150); // stand-in for sending goodbye records
                    retracted = true;
                }
            },
            log);

        lifecycle.Start();
        Assert.True(lifecycle.Stop(), "shutdown should complete within the timeout");
        Assert.True(retracted, "Stop must not return before shutdown work has finished");
    }

    [TestCase("A startup failure is recorded rather than lost")]
    [Requirement("REQ-LIF-004")]
    public static void Startup_failure_is_recorded()
    {
        var log = new CollectingServiceLog();

        using var lifecycle = new ServiceLifecycle(
            _ => throw new InvalidOperationException("the printer could not be found"),
            log);

        lifecycle.Start();
        lifecycle.Stop();

        Assert.NotNull(lifecycle.Failure, "a service that dies on startup must say why");
        Assert.True(
            log.Entries.Any(e => e.Level == LogLevel.Error
                                 && e.Message.Contains("could not be found", StringComparison.Ordinal)),
            "and the reason must reach the log, not vanish into an unobserved task");
    }

    // ---- The failure handler --------------------------------------------------
    //
    // Added by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
    // West, 2026-10-07. Under the service control manager the handler ends the
    // process (WindowsService), which no test can reach. These check what can be
    // checked here: when the handler is told, and when it is not. See
    // docs/findings/2026-10-07-a-part-of-the-service-could-fail-and-nothing-stopped.md.

    [TestCase("A failure nobody asked for reaches the failure handler, after it is logged")]
    [Requirement("REQ-LIF-004")]
    public static void Failure_reaches_the_handler()
    {
        var log = new CollectingServiceLog();
        using var told = new ManualResetEventSlim();
        Exception? handed = null;
        bool loggedFirst = false;

        using var lifecycle = new ServiceLifecycle(
            _ => throw new InvalidOperationException("a listener could not be opened"),
            log,
            failed: failure =>
            {
                handed = failure;
                loggedFirst = log.Entries.Any(
                    e => e.Level == LogLevel.Error
                         && e.Message.Contains("could not be opened", StringComparison.Ordinal));
                told.Set();
            });

        lifecycle.Start();

        Assert.True(told.Wait(TimeSpan.FromSeconds(5)),
            "a service that fails after it has started must tell something; otherwise the process stays "
            + "and Windows goes on showing it as running");
        Assert.True(handed is InvalidOperationException, "the handler is given the failure itself");
        Assert.True(loggedFirst, "and the failure is in the log before the handler runs, because the handler may end the process");
        Assert.True(lifecycle.Stop(), "a stop after the failure has nothing left to wait for");
    }

    [TestCase("A stop is not a failure, and the handler is not told")]
    public static void Stop_does_not_reach_the_handler()
    {
        using var began = new ManualResetEventSlim();
        int told = 0;

        using var lifecycle = new ServiceLifecycle(
            async token =>
            {
                began.Set();
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            },
            new CollectingServiceLog(),
            failed: _ => Interlocked.Increment(ref told));

        lifecycle.Start();
        Assert.True(began.Wait(TimeSpan.FromSeconds(5)), "the work must have begun");

        // The handler runs inside the work, and Stop waits for the work, so by
        // the time Stop returns the handler has run or never will.
        Assert.True(lifecycle.Stop(), "shutdown should complete within the timeout");
        Assert.Equal(0, told, "ending the process on an ordinary stop would turn every stop into a failure");
        Assert.Null(lifecycle.Failure, "and an ordinary stop records no failure");
    }

    [TestCase("A failure while a stop is under way is left to Stop, and the handler is not told")]
    public static void Failure_during_a_stop_does_not_reach_the_handler()
    {
        using var began = new ManualResetEventSlim();
        int told = 0;

        using var lifecycle = new ServiceLifecycle(
            async token =>
            {
                began.Set();
                try
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException("the goodbye could not be sent");
                }
            },
            new CollectingServiceLog(),
            failed: _ => Interlocked.Increment(ref told));

        lifecycle.Start();
        Assert.True(began.Wait(TimeSpan.FromSeconds(5)), "the work must have begun");

        Assert.True(lifecycle.Stop(), "shutdown should complete within the timeout");
        Assert.NotNull(lifecycle.Failure, "the failure is still recorded, for Stop's caller to report");
        Assert.Equal(0, told,
            "Windows is already being answered by the stop; ending the process under it would report a crash");
    }

    [TestCase("A failure handler that fails is logged, and nothing escapes")]
    public static void A_failing_handler_is_logged()
    {
        var log = new CollectingServiceLog();
        using var told = new ManualResetEventSlim();

        using var lifecycle = new ServiceLifecycle(
            _ => throw new InvalidOperationException("a listener could not be opened"),
            log,
            failed: _ =>
            {
                told.Set();
                throw new InvalidOperationException("the handler could not do its work");
            });

        lifecycle.Start();

        Assert.True(told.Wait(TimeSpan.FromSeconds(5)), "the handler must have been told");
        Assert.True(lifecycle.Stop(), "the work still ends, and Stop still returns");
        Assert.True(
            log.Entries.Any(e => e.Level == LogLevel.Error
                                 && e.Message.Contains("could not do its work", StringComparison.Ordinal)),
            "a handler that fails must not vanish into an unobserved task either");
    }

    [TestCase("Stopping something never started is harmless")]
    [Requirement("REQ-LIF-001")]
    public static void Stop_before_start_is_safe()
    {
        // The control manager may stop a service whose start failed.
        using var lifecycle = new ServiceLifecycle(_ => Task.CompletedTask, new CollectingServiceLog());

        Assert.True(lifecycle.Stop(), "stopping something that never started is not an error");
    }

    [TestCase("A slow shutdown is reported rather than hidden")]
    [Requirement("REQ-LIF-001")]
    public static void Slow_shutdown_is_reported()
    {
        var log = new CollectingServiceLog();

        using var lifecycle = new ServiceLifecycle(
            async token =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }
                finally
                {
                    Thread.Sleep(2000); // longer than the timeout below
                }
            },
            log,
            stopTimeout: TimeSpan.FromMilliseconds(200));

        lifecycle.Start();

        Assert.False(lifecycle.Stop(), "a shutdown that overruns must be reported as such");
        Assert.True(
            log.Entries.Any(e => e.Level == LogLevel.Warning
                                 && e.Message.Contains("advertisement may not have been retracted",
                                                       StringComparison.Ordinal)),
            "and the operator must be told what that means for clients");
    }

    // ---- The technique itself -----------------------------------------------

    [TestCase("The scan reads real metadata and would notice a forbidden type")]
    [Requirement("REQ-SEC-004")]
    public static void Scan_actually_reads_metadata()
    {
        // A test that can only pass is worth nothing. This one confirms the
        // scanner sees real references by looking for a type SecretPrinter.Mdns
        // certainly uses. If this ever came back empty, the checks above would
        // be passing vacuously.
        List<string> references = TypeReferences("SecretPrinter.Mdns");

        Assert.True(references.Count > 0, "the assembly must have type references to inspect");
        Assert.True(references.Contains("System.Net.Sockets.Socket", StringComparer.Ordinal),
            "SecretPrinter.Mdns certainly uses Socket; if the scanner cannot see that, it cannot "
            + "see anything, and the absence checks above prove nothing");
    }
}

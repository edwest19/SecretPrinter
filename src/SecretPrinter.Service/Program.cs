// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Entry point. Loads configuration, runs the host, stops cleanly on Ctrl+C.
//
// Two ways to run:
//   Without --service, this is a console application: Ctrl+C stops it. That is
//   how it is developed and how the operating guide has you try it first.
//
//   With --service, it registers with the Windows service control manager. That
//   path needs ServiceBase from a NuGet package, which is the one dependency in
//   this project that is not the base class library or a project in this
//   repository; it is documented and justified in README.md as REQ-SEC-009
//   requires.
// -----------------------------------------------------------------------------

using SecretPrinter.Configuration;
using SecretPrinter.Mdns;

namespace SecretPrinter.Service;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var log = new ConsoleServiceLog();

        string? configPath = null;
        bool asService = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;

                case "--service":
                    asService = true;
                    break;

                case "--print-example-config":
                    Console.WriteLine(ConfigurationLoader.ExampleJson);
                    return 0;

                case "--help" or "-h" or "-?":
                    PrintUsage();
                    return 0;

                default:
                    Console.Error.WriteLine($"Unrecognised argument '{args[i]}'.");
                    PrintUsage();
                    return 2;
            }
        }

        if (configPath is null)
        {
            Console.Error.WriteLine("--config is required. Run with --print-example-config for a starting point.");
            return 2;
        }

        ServiceConfiguration configuration;
        try
        {
            configuration = ConfigurationLoader.LoadFile(configPath, SystemInterfaceInventory.Instance);
        }
        catch (ConfigurationException ex)
        {
            // Fail fast and loudly: a service that starts on bad configuration
            // advertises something nobody chose.
            log.Error("Configuration is not usable, so the service will not start.");
            foreach (string problem in ex.Problems)
            {
                log.Error($"  {problem}");
            }

            return 3;
        }

        if (asService)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("--service is only meaningful on Windows.");
                return 2;
            }

            return RunAsWindowsService(configuration, log);
        }

        using var stopping = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            log.Info("Stop requested (Ctrl+C).");
            stopping.Cancel();
        };

        try
        {
            var host = new ServiceHost(configuration, log);
            await host.RunAsync(stopping.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex) when (ex is MdnsInterfaceException
                                      or System.Net.Sockets.SocketException
                                      or Resolution.PrinterResolutionException)
        {
            log.Error($"Could not start: {ex.Message}");
            return 4;
        }
    }

    /// <summary>
    /// Hands control to the service control manager. Separated and attributed so
    /// the Windows-only dependency is confined to one method that the rest of
    /// the program does not touch on other platforms.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int RunAsWindowsService(ServiceConfiguration configuration, IServiceLog log)
    {
        using var service = new WindowsService(configuration, log);
        System.ServiceProcess.ServiceBase.Run(service);
        return 0;
    }

    private static void PrintUsage() =>
        Console.WriteLine("""
            SecretPrinter - makes an AirPrint printer on one network reachable from another.

            Print job data passes through this machine. See README.md before installing.

            Usage:
              SecretPrinter.Service --config <path>
              SecretPrinter.Service --config <path> --service
              SecretPrinter.Service --print-example-config

            Options:
              --config <path>           Configuration file. Required.
              --service                 Run under the Windows service control manager.
                                        Without it, this is a console application that
                                        stops on Ctrl+C.
              --print-example-config    Write an example configuration to standard output.
              --help                    Show this text.

            Exit codes:
              0  Stopped cleanly.
              2  Bad arguments.
              3  Configuration is not usable; every problem is listed.
              4  Could not start: an interface, socket or the printer was unavailable.
            """);
}

// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// --log-file, its requirement under --service, and exit code 5 added by Claude
// (Anthropic model, Claude Opus 5) at the direction of Edwin West, 2026-09-18,
// for REQ-OBS-009. Reviewed by a human before merge.
//
// Purpose:
//   Entry point. Loads configuration, runs the host, stops cleanly on Ctrl+C.
//
// Where the log goes:
//   Always to the console. Additionally to a file when --log-file names one,
//   which is required with --service because the control manager gives a
//   service no console and standard output would go nowhere at all.
//
//   The file is opened BEFORE the configuration is read, so that a refusal to
//   start - the commonest thing an operator needs to see - is written to it
//   rather than lost.
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
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

internal static class Program
{
    [Requirement("REQ-OBS-009",
        "Refuses --service without --log-file, and opens the named file before the configuration is read so that a refusal to start is written to it.")]
    private static async Task<int> Main(string[] args)
    {
        var console = new ConsoleServiceLog();

        string? configPath = null;
        string? logFilePath = null;
        bool asService = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;

                case "--log-file" when i + 1 < args.Length:
                    logFilePath = args[++i];
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

        if (asService && logFilePath is null)
        {
            // Refusing here is the whole point of the requirement. A service
            // that starts without a file writes its log to a console that does
            // not exist, and everything it reports - including why it failed -
            // is lost.
            Console.Error.WriteLine(
                "--log-file is required with --service. Under the service control manager there is no "
                + "console, so without a file this service would run with no log at all.");
            Console.Error.WriteLine(
                "There is deliberately no default path: you choose where the file goes and which account "
                + "can write there.");
            return 2;
        }

        FileServiceLog? fileLog = null;

        if (logFilePath is not null)
        {
            try
            {
                fileLog = new FileServiceLog(logFilePath, console);
            }
            catch (Exception ex) when (ex is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
            {
                Console.Error.WriteLine($"The log file '{logFilePath}' could not be opened: {ex.Message}");
                Console.Error.WriteLine(
                    "The folder must already exist and the account this runs as must be able to write in "
                    + "it. SecretPrinter creates no folders.");
                return 5;
            }
        }

        using (fileLog)
        {
            IServiceLog log = fileLog is null
                ? console
                : new CompositeServiceLog(console, fileLog);

            return await RunAsync(configPath, asService, log).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads the configuration and runs, with the log already established so
    /// that a configuration that cannot be used is reported to the file as well
    /// as the console.
    /// </summary>
    private static async Task<int> RunAsync(string configPath, bool asService, IServiceLog log)
    {
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
              SecretPrinter.Service --config <path> --log-file <path>
              SecretPrinter.Service --config <path> --log-file <path> --service
              SecretPrinter.Service --print-example-config

            Options:
              --config <path>           Configuration file. Required.
              --log-file <path>         Also append the log to this file. Required with
                                        --service, because a service has no console and
                                        would otherwise log nowhere. No default: the
                                        folder must exist and this process must be able
                                        to write in it.
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
              5  The log file could not be opened.
            """);
}

// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// ServiceHostTests registered by Claude (Anthropic model, Claude Opus 5) at the
// direction of Edwin West, 2026-09-16. Reviewed by a human before merge.
//
// FileServiceLogTests registered by Claude (Anthropic model, Claude Opus 5) at
// the direction of Edwin West, 2026-09-18. Reviewed by a human before merge.
//
// Pass --results <path> to record which requirements had a test that executed
// and passed, for SecretPrinter.SpecCheck to read.
//
// PrinterWatchTests registered by Claude (Anthropic model, Claude Opus 5) at
// the direction of Edwin West, 2026-09-20. Reviewed by a human before merge.
//
// AvailabilityGateTests registered by Claude (Anthropic model, Claude Opus 5)
// at the direction of Edwin West, 2026-09-20. Reviewed by a human before
// merge.
//
// StartupWaitTests registered by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-09-22. Reviewed by a human before merge.
//
// TestHarnessTests registered first by Claude (Anthropic model, Claude Opus
// 5.5) at the direction of Edwin West, 2026-09-25, so that a harness unable to
// fail an async test is caught before any async test after it is believed.
// Reviewed by a human before merge.
//
// PrinterSideTests registered by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-09-25, for REQ-RES-009. Reviewed by a human
// before merge.
// -----------------------------------------------------------------------------

using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? resultsPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--results")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("--results requires a path.");
                    return 2;
                }

                resultsPath = args[++i];
            }
            else
            {
                Console.Error.WriteLine($"Unrecognised argument '{args[i]}'. Only --results <path> is supported.");
                return 2;
            }
        }

        Console.WriteLine("SecretPrinter.Service tests");
        return TestHarness.Run(
            resultsPath,
            typeof(TestHarnessTests),
            typeof(SecurityClaimsTests),
            typeof(ServiceHostTests),
            typeof(FileServiceLogTests),
            typeof(PrinterWatchTests),
            typeof(PrinterSideTests),
            typeof(AvailabilityGateTests),
            typeof(StartupWaitTests));
    }
}

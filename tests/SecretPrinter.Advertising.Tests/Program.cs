// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Advertising.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Pass --results <path> to record which requirements had a test that executed
// and passed. SecretPrinter.SpecCheck reads that file so a skipped test is not
// mistaken for coverage.
// -----------------------------------------------------------------------------

using SecretPrinter.TestKit;

namespace SecretPrinter.Advertising.Tests;

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

        Console.WriteLine("SecretPrinter.Advertising tests");
        return TestHarness.Run(resultsPath, typeof(AdvertisementBuilderTests));
    }
}

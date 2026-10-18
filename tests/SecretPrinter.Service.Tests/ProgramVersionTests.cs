// -----------------------------------------------------------------------------
// ProgramVersionTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-18, for REQ-OBS-011. Reviewed by a human before merge.
//
// Purpose:
//   Checks the line the service logs first at every start: that it carries
//   the stamp Windows reports as the service file's ProductVersion, the same
//   text docs/operating.md has an operator read from the installed file; that
//   it names the assembly it describes; that a program with no stamp is said
//   to have none; and that a real start writes it first, before the
//   configuration is read.
//
// How the start is run:
//   Through the service assembly's own entry point, found by reflection, with
//   a configuration file that does not exist and a log file in a temporary
//   folder. That start is refused, which is the point: a refused start must
//   carry the line too, and it is the one start a test can run on any machine
//   without touching a network adapter. The console is captured for the
//   length of the call and put back afterwards; the harness runs one test at
//   a time.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ProgramVersionTests
{
    private static Assembly ServiceAssembly => typeof(ProgramVersion).Assembly;

    [TestCase("The version line carries the stamp the service file reports as its ProductVersion")]
    [Requirement("REQ-OBS-011")]
    public static void The_line_carries_the_files_product_version()
    {
        string? productVersion = FileVersionInfo.GetVersionInfo(ServiceAssembly.Location).ProductVersion;

        Assert.False(string.IsNullOrWhiteSpace(productVersion),
            "the service file must report a ProductVersion, or this test compares nothing");
        Assert.Equal($"SecretPrinter.Service version {productVersion}", ProgramVersion.LineFor(ServiceAssembly),
            "the line must carry exactly what an operator reads from the installed file, with nothing after it");
    }

    [TestCase("The version line names the assembly it was read from")]
    [Requirement("REQ-OBS-011")]
    public static void The_line_names_the_assembly_it_describes()
    {
        string service = ProgramVersion.LineFor(ServiceAssembly);
        string runner = ProgramVersion.LineFor(typeof(ProgramVersionTests).Assembly);

        Assert.True(service.StartsWith("SecretPrinter.Service version ", StringComparison.Ordinal),
            $"the service's line must name the service, but read '{service}'");
        Assert.True(runner.StartsWith("SecretPrinter.Service.Tests version ", StringComparison.Ordinal),
            $"another assembly's line must name that assembly, not the service, but read '{runner}'");
    }

    [TestCase("A program with no version stamped gets a line that says so")]
    [Requirement("REQ-OBS-011")]
    public static void A_program_with_no_stamp_is_said_to_have_none()
    {
        AssemblyBuilder bare = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("NoStampHere"), AssemblyBuilderAccess.Run);

        Assert.Equal("NoStampHere: no version is stamped in this program.", ProgramVersion.LineFor(bare),
            "a missing stamp must be said, not left out");
    }

    [TestCase("A start writes the version line first, before the configuration is read")]
    [Requirement("REQ-OBS-011")]
    public static void A_start_writes_the_version_line_first()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("secretprinter-version-");

        try
        {
            string configPath = Path.Combine(folder.FullName, "no-such-configuration.json");
            string logPath = Path.Combine(folder.FullName, "secretprinter.log");

            MethodInfo? entry = ServiceAssembly.EntryPoint;
            Assert.NotNull(entry, "the service assembly must have an entry point");

            TextWriter standardOut = Console.Out;
            TextWriter standardError = Console.Error;
            using var capturedOut = new StringWriter();
            using var capturedError = new StringWriter();
            object? exit;

            Console.SetOut(capturedOut);
            Console.SetError(capturedError);
            try
            {
                exit = entry!.Invoke(null, new object[] { new[] { "--config", configPath, "--log-file", logPath } });
            }
            finally
            {
                Console.SetOut(standardOut);
                Console.SetError(standardError);
            }

            Assert.Equal(3, exit is int code ? code : -1,
                "a configuration file that does not exist must be refused, with exit code 3");

            string expected = "  INFORMATION  " + ProgramVersion.LineFor(ServiceAssembly);
            string[] lines = File.ReadAllLines(logPath);

            Assert.True(lines.Length >= 2, $"the log file must hold the version line and the refusal, but held {lines.Length} line(s)");
            Assert.True(lines[0].EndsWith(expected, StringComparison.Ordinal),
                $"the first line of the file must be the version line, but was '{lines[0]}'");
            Assert.True(lines[1].EndsWith("  ERROR        Configuration is not usable, so the service will not start.",
                            StringComparison.Ordinal),
                $"the refusal must come after the version line, but the second line was '{lines[1]}'");

            string[] consoleLines = capturedOut.ToString().Split(
                ["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

            Assert.True(consoleLines.Length >= 1 && consoleLines[0].EndsWith(expected, StringComparison.Ordinal),
                "the console must show the version line first as well");
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}

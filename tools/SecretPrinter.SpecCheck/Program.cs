// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.SpecCheck)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Checks that README.md and the code agree with each other.
//
//   Reads the requirement tables from README.md, scans the compiled assemblies
//   for [Requirement] markers, and reports every requirement that lacks an
//   implementation, lacks a test, or is cited by code but absent from the
//   document.
//
// What this program does:
//   1. Reads a Markdown file and extracts requirement rows.
//   2. Loads the assemblies it is pointed at and reads their attribute metadata.
//   3. Prints a coverage matrix, and optionally writes it as Markdown.
//   4. Exits non-zero when a binding requirement is uncovered.
//
// What this program does NOT do:
//   - It does not open a network socket.
//   - It does not modify README.md or any source file. The only file it can
//     write is the Markdown matrix, and only when explicitly asked with
//     --write-matrix.
//   - It does not invoke code in the assemblies it scans. Attribute values are
//     read from metadata, so no constructor in the scanned assembly runs.
//
// Honest limitation, repeated here because it is the thing most likely to be
// misread: a PASS means every binding requirement has a marker on some code and
// a marker on some test. It does not mean the code is correct. See the note
// printed at the end of every run.
// -----------------------------------------------------------------------------

namespace SecretPrinter.SpecCheck;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Options? options = Options.Parse(args);
            return options is null ? 0 : Run(options);
        }
        catch (OptionException ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"File not found: {ex.Message}");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Specification could not be parsed: {ex.Message}");
            return 3;
        }
    }

    private static int Run(Options options)
    {
        Console.WriteLine("SecretPrinter specification check");
        Console.WriteLine($"  Specification : {options.ReadmePath}");
        Console.WriteLine($"  Evidence      : {options.EvidencePath ?? "(none supplied)"}");
        Console.WriteLine($"  Test results  : "
                          + (options.TestResultsPaths.Count > 0
                              ? string.Join(", ", options.TestResultsPaths)
                              : "(none supplied)"));
        Console.WriteLine($"  Repo root     : {options.RepositoryRoot}");
        Console.WriteLine();

        SpecDocument document = SpecDocument.Load(options.ReadmePath);

        if (document.Requirements.Count == 0)
        {
            Console.Error.WriteLine(
                $"No requirements found in '{options.ReadmePath}'. Expected table rows of the form:");
            Console.Error.WriteLine("  | REQ-AREA-NNN | MUST | Requirement text. |");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Finding zero requirements is treated as a failure, not an empty pass: it almost");
            Console.Error.WriteLine("always means the document moved or its table format changed.");
            return 3;
        }

        Console.WriteLine($"  Requirements  : {document.Requirements.Count}");

        List<string> assemblies = ResolveAssemblies(options);
        if (assemblies.Count == 0)
        {
            Console.Error.WriteLine("No assemblies found to scan. Build the solution first, or check --search.");
            return 2;
        }

        ScanResult scan = MarkerScanner.Scan(assemblies);

        Console.WriteLine($"  Assemblies    : {scan.AssembliesScanned.Count}");
        foreach (string entry in scan.AssembliesScanned)
        {
            Console.WriteLine($"                  {entry}");
        }

        if (scan.Diagnostics.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Scan diagnostics:");
            foreach (string diagnostic in scan.Diagnostics)
            {
                Console.WriteLine($"    {diagnostic}");
            }
        }

        EvidenceDocument evidence = options.EvidencePath is { } evidencePath
            ? EvidenceDocument.Load(evidencePath, options.RepositoryRoot)
            : EvidenceDocument.Empty("(none)");

        if (options.EvidencePath is not null)
        {
            Console.WriteLine($"  Evidence rows : {evidence.Entries.Count}");
        }

        TestResultsDocument results = options.TestResultsPaths.Count > 0
            ? TestResultsDocument.Load(options.TestResultsPaths)
            : TestResultsDocument.NotSupplied();

        if (results.Supplied)
        {
            Console.WriteLine($"  Test records  : {results.RecordCount}, "
                              + $"{results.PassedRequirementCount} requirement(s) with a passing test");
        }

        CoverageMatrix matrix = CoverageMatrix.Build(document, scan, evidence, results);
        matrix.PrintText(Console.Out, options.Verbose);

        if (options.MatrixPath is { } matrixPath)
        {
            File.WriteAllText(matrixPath, matrix.ToMarkdown());
            Console.WriteLine();
            Console.WriteLine($"Matrix written to {matrixPath}");
        }

        return matrix.HasFailures ? 1 : 0;
    }

    /// <summary>
    /// Expands search directories into assembly paths, keeping one assembly per
    /// simple name. Every file used is printed, so the selection is auditable
    /// rather than magical.
    /// </summary>
    private static List<string> ResolveAssemblies(Options options)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        void Add(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (byName.TryAdd(name, path))
            {
                ordered.Add(path);
            }
        }

        foreach (string explicitPath in options.AssemblyPaths)
        {
            if (!File.Exists(explicitPath))
            {
                throw new FileNotFoundException($"assembly '{explicitPath}'");
            }

            Add(explicitPath);
        }

        foreach (string directory in options.SearchDirectories)
        {
            if (!Directory.Exists(directory))
            {
                throw new FileNotFoundException($"search directory '{directory}'");
            }

            // Prefer Release output when both exist, so a CI run and a local run
            // examine the same artifact where possible.
            IEnumerable<string> candidates = Directory
                .EnumerateFiles(directory, "SecretPrinter*.dll", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
                                         StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => p, StringComparer.Ordinal);

            foreach (string candidate in candidates)
            {
                Add(candidate);
            }
        }

        return ordered;
    }
}

internal sealed class OptionException(string message) : Exception(message);

internal sealed class Options
{
    public required string ReadmePath { get; init; }
    public required IReadOnlyList<string> SearchDirectories { get; init; }
    public required IReadOnlyList<string> AssemblyPaths { get; init; }
    public required string? MatrixPath { get; init; }
    public required string? EvidencePath { get; init; }
    public required IReadOnlyList<string> TestResultsPaths { get; init; }
    public required string RepositoryRoot { get; init; }
    public required bool Verbose { get; init; }

    /// <summary>Returns null when the caller asked for help.</summary>
    public static Options? Parse(string[] args)
    {
        string? readme = null;
        var searches = new List<string>();
        var assemblies = new List<string>();
        string? matrix = null;
        string? evidence = null;
        var testResults = new List<string>();
        string? repositoryRoot = null;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h" or "-?":
                    PrintUsage();
                    return null;

                case "--readme":
                    readme = RequireValue(args, ref i, "--readme");
                    break;

                case "--search":
                    searches.Add(RequireValue(args, ref i, "--search"));
                    break;

                case "--assembly":
                    assemblies.Add(RequireValue(args, ref i, "--assembly"));
                    break;

                case "--write-matrix":
                    matrix = RequireValue(args, ref i, "--write-matrix");
                    break;

                case "--evidence":
                    evidence = RequireValue(args, ref i, "--evidence");
                    break;

                case "--test-results":
                    testResults.Add(RequireValue(args, ref i, "--test-results"));
                    break;

                case "--repo-root":
                    repositoryRoot = RequireValue(args, ref i, "--repo-root");
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                default:
                    throw new OptionException($"Unrecognised argument '{args[i]}'.");
            }
        }

        if (readme is null)
        {
            throw new OptionException("--readme is required.");
        }

        if (!File.Exists(readme))
        {
            throw new OptionException($"README not found at '{readme}'.");
        }

        if (searches.Count == 0 && assemblies.Count == 0)
        {
            throw new OptionException("At least one --search directory or --assembly file is required.");
        }

        if (evidence is not null && !File.Exists(evidence))
        {
            throw new OptionException($"Evidence document not found at '{evidence}'.");
        }

        foreach (string resultsFile in testResults)
        {
            if (!File.Exists(resultsFile))
            {
                throw new OptionException(
                    $"Test results not found at '{resultsFile}'. Run the test suites with --results first.");
            }
        }

        // Artifact paths in the evidence document are repository-relative. The
        // root defaults to the directory holding the README, which is where the
        // specification lives; it is echoed at startup so the resolution is
        // never a guess the reader has to make.
        repositoryRoot ??= Path.GetDirectoryName(Path.GetFullPath(readme)) ?? ".";

        if (!Directory.Exists(repositoryRoot))
        {
            throw new OptionException($"Repository root not found at '{repositoryRoot}'.");
        }

        return new Options
        {
            ReadmePath = readme,
            SearchDirectories = searches,
            AssemblyPaths = assemblies,
            MatrixPath = matrix,
            EvidencePath = evidence,
            TestResultsPaths = testResults,
            RepositoryRoot = repositoryRoot,
            Verbose = verbose,
        };
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new OptionException($"{name} requires a value.");
        }

        return args[++index];
    }

    private static void PrintUsage() =>
        Console.WriteLine("""
            SecretPrinter specification check.

            Verifies that README.md and the compiled code agree: every requirement in the
            document should have an implementation marker and a test marker, and every
            marker in code should cite a requirement the document defines.

            Usage:
              SecretPrinter.SpecCheck --readme <path> (--search <dir> | --assembly <dll>)...
                                      [--write-matrix <path>] [--verbose]

            Options:
              --readme <path>         The specification document to parse.
              --search <dir>          Directory searched recursively for SecretPrinter*.dll.
                                      May be repeated. Every file used is printed.
              --assembly <dll>        A specific assembly to scan. May be repeated.
              --evidence <path>       Document recording how requirements that cannot be
                                      satisfied by code are satisfied instead. Each entry
                                      must name artifact paths, and each path is checked
                                      to exist.
              --test-results <path>   Results file written by a test run's --results option.
                                      May be repeated, once per test project. Without it,
                                      a test that never ran still counts as coverage, so
                                      CI must always supply every suite's file.
              --repo-root <dir>       Root that evidence artifact paths resolve against.
                                      Defaults to the directory holding the README.
              --write-matrix <path>   Also write the matrix as Markdown to this path.
                                      This is the only file this tool can write.
              --verbose               Show every marker location instead of an abbreviation.
              --help                  Show this text.

            Example, from the repository root:
              dotnet run --project tools/SecretPrinter.SpecCheck -- --readme README.md --search .

            Conventions:
              An assembly whose simple name ends in '.Tests' provides TEST markers.
              Every other assembly provides IMPLEMENTATION markers.

            Exit codes:
              0  Every binding (MUST / MUST NOT) requirement is covered by code with a
                 test recorded as passing, or by intact evidence; with no orphaned or
                 malformed markers, no duplicate IDs, and no evidence pointing at a
                 missing artifact.
              1  One or more of the above failed.
              2  Bad arguments, or a file was not found.
              3  The specification could not be parsed, or contained no requirements.

            A pass means markers exist. It does not mean the code is correct.
            """);
}

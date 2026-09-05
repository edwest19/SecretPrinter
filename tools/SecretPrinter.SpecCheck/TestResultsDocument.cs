// -----------------------------------------------------------------------------
// TestResultsDocument.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Reads the results file written by the test harness, so SpecCheck can tell
//   the difference between a test that verified a requirement and a test that
//   merely claims to.
//
// The hole this closes:
//   A [Requirement] marker on a test method is compiled-in metadata. It is
//   present in the assembly whether or not the test ever executed. Before this
//   file existed, a test that skipped on every machine still counted as full
//   coverage in the matrix.
//
//   That was found in practice, not in theory: REQ-CFG-006 needs an adapter
//   holding two IPv4 addresses, skipped on both the development container and
//   the target machine, and was reported OK on both.
//
//   With results supplied, a requirement counts as tested only when a test
//   carrying its identifier is recorded as having PASSED.
//
// Format read (tab-separated, '#' comments):
//   PASS<TAB>REQ-ADV-013<TAB>SecretPrinter.Mdns.Tests.MdnsSocketTests.Open_sets_ttl_255
// -----------------------------------------------------------------------------

namespace SecretPrinter.SpecCheck;

/// <summary>Execution counts for one requirement.</summary>
/// <param name="Passed">Tests carrying this identifier that ran and passed.</param>
/// <param name="Failed">Tests that ran and failed.</param>
/// <param name="Skipped">Tests that did not run.</param>
internal sealed record TestExecution(int Passed, int Failed, int Skipped)
{
    public bool AnyPassed => Passed > 0;

    /// <summary>Short description of why a requirement has no passing test.</summary>
    public string Explain() => (Failed, Skipped) switch
    {
        (> 0, > 0) => $"{Failed} failed, {Skipped} skipped",
        (> 0, 0) => $"{Failed} failed",
        (0, > 0) => $"{Skipped} skipped",
        _ => "no test executed",
    };
}

/// <summary>Test outcomes recorded by a harness run.</summary>
internal sealed class TestResultsDocument
{
    private readonly Dictionary<string, TestExecution> _byRequirement;

    private TestResultsDocument(
        Dictionary<string, TestExecution> byRequirement,
        Dictionary<string, Guid> assemblyIds,
        string path,
        bool supplied,
        int lineCount)
    {
        _byRequirement = byRequirement;
        AssemblyIds = assemblyIds;
        Path = path;
        Supplied = supplied;
        RecordCount = lineCount;
    }

    public string Path { get; }

    /// <summary>
    /// Module version ids of the assemblies these results were produced
    /// against, by simple name. Empty when the file predates fingerprinting, in
    /// which case it cannot be trusted to describe the current code.
    /// </summary>
    public IReadOnlyDictionary<string, Guid> AssemblyIds { get; }

    /// <summary>
    /// False when no results file was given. Callers must then report coverage
    /// as unverified rather than silently assuming every marked test ran.
    /// </summary>
    public bool Supplied { get; }

    public int RecordCount { get; }

    public int PassedRequirementCount => _byRequirement.Values.Count(e => e.AnyPassed);

    public static TestResultsDocument NotSupplied() =>
        new([], [], "(none)", supplied: false, lineCount: 0);

    /// <summary>
    /// Loads and merges several results files, one per test project. Counts are
    /// summed, so a requirement passing in any suite counts as tested.
    /// </summary>
    public static TestResultsDocument Load(IReadOnlyList<string> paths)
    {
        var byRequirement = new Dictionary<string, TestExecution>(StringComparer.Ordinal);
        var assemblyIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        int records = 0;

        foreach (string raw in paths.SelectMany(File.ReadAllLines))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split('\t', StringSplitOptions.TrimEntries);
            if (fields.Length < 2)
            {
                continue;
            }

            string outcome = fields[0];
            string requirement = fields[1];

            // A fingerprint line, not a test outcome.
            if (outcome == "ASSEMBLY")
            {
                if (fields.Length >= 3 && Guid.TryParse(fields[2], out Guid id))
                {
                    assemblyIds[requirement] = id;
                }

                continue;
            }

            records++;

            if (requirement == "-")
            {
                continue; // Test carries no requirement marker.
            }

            byRequirement.TryGetValue(requirement, out TestExecution? execution);
            execution ??= new TestExecution(0, 0, 0);

            byRequirement[requirement] = outcome switch
            {
                "PASS" => execution with { Passed = execution.Passed + 1 },
                "FAIL" => execution with { Failed = execution.Failed + 1 },
                "SKIP" => execution with { Skipped = execution.Skipped + 1 },
                _ => execution,
            };
        }

        return new TestResultsDocument(
            byRequirement, assemblyIds, string.Join(", ", paths), supplied: true, records);
    }

    /// <summary>Execution record for a requirement, or null when nothing was recorded.</summary>
    public TestExecution? For(string requirementId) =>
        _byRequirement.TryGetValue(requirementId, out TestExecution? execution) ? execution : null;
}

// -----------------------------------------------------------------------------
// CoverageMatrix.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Joins requirements from README.md to markers found in compiled code, and
//   reports the result.
//
// The limit of what this proves, stated once more because it matters:
//   A requirement shown as OK has an implementation marker and a test marker.
//   That is all. It does not mean the implementation is correct, that the test
//   is meaningful, or that the marker is honestly placed. An empty method
//   carrying the attribute counts as covered. This tool detects OMISSIONS. It
//   does not verify BEHAVIOUR, and a green run is not evidence of compliance.
// -----------------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using SecretPrinter.Spec;

namespace SecretPrinter.SpecCheck;

internal enum CoverageStatus
{
    /// <summary>An implementation marker and a test marker both exist.</summary>
    Covered,

    /// <summary>No code markers, but intact evidence records how it is satisfied.</summary>
    CoveredByEvidence,

    /// <summary>Evidence was offered, but an artifact it names does not exist.</summary>
    EvidenceBroken,

    /// <summary>
    /// A test carries this requirement's marker, but the recorded results show
    /// it never passed - it was skipped, or it failed. The marker exists; the
    /// verification does not.
    /// </summary>
    TestDidNotRun,

    MissingTest,
    MissingImplementation,
    Uncovered,
}

internal sealed class CoverageRow
{
    public required SpecRequirement Requirement { get; init; }
    public required IReadOnlyList<Marker> Implementations { get; init; }
    public required IReadOnlyList<Marker> Tests { get; init; }
    public required IReadOnlyList<Evidence> Evidence { get; init; }

    /// <summary>
    /// Recorded execution for this requirement's tests, or null when no results
    /// file was supplied. Null means execution is unverified, not that it failed.
    /// </summary>
    public required TestExecution? Execution { get; init; }

    /// <summary>True when results were supplied and no test carrying this ID passed.</summary>
    public bool TestMarkedButNotRun => Tests.Count > 0 && Execution is not null && !Execution.AnyPassed;

    /// <summary>
    /// Code coverage takes precedence over evidence. A requirement satisfied by
    /// both is reported as code-covered, because that is the stronger claim; the
    /// evidence still appears in the row so nothing is hidden.
    /// </summary>
    public CoverageStatus Status
    {
        get
        {
            if (Implementations.Count > 0 && Tests.Count > 0)
            {
                // A marker proves a claim was made. The results file proves the
                // test actually ran. Both are needed before this counts.
                return TestMarkedButNotRun ? CoverageStatus.TestDidNotRun : CoverageStatus.Covered;
            }

            if (Evidence.Count > 0)
            {
                return Evidence.Any(e => e.IsIntact)
                    ? CoverageStatus.CoveredByEvidence
                    : CoverageStatus.EvidenceBroken;
            }

            return (Implementations.Count > 0, Tests.Count > 0) switch
            {
                (true, false) => CoverageStatus.MissingTest,
                (false, true) => CoverageStatus.MissingImplementation,
                _ => CoverageStatus.Uncovered,
            };
        }
    }

    /// <summary>True when this row should fail the build.</summary>
    public bool IsFailure =>
        Requirement.IsBinding
        && Status is not (CoverageStatus.Covered or CoverageStatus.CoveredByEvidence);

    public string StatusText => Status switch
    {
        CoverageStatus.Covered => "OK",
        CoverageStatus.CoveredByEvidence => "OK (evidence)",
        CoverageStatus.EvidenceBroken => "EVIDENCE BROKEN",
        CoverageStatus.TestDidNotRun => "TEST DID NOT RUN",
        CoverageStatus.MissingTest => "MISSING TEST",
        CoverageStatus.MissingImplementation => "MISSING IMPL",
        CoverageStatus.Uncovered => "NOT IMPLEMENTED",
        _ => Status.ToString(),
    };
}

internal sealed class CoverageMatrix
{
    private static readonly Regex IdPattern = new(RequirementAttribute.IdPattern, RegexOptions.Compiled);

    public required IReadOnlyList<CoverageRow> Rows { get; init; }

    /// <summary>Markers citing an identifier that README.md does not define.</summary>
    public required IReadOnlyList<Marker> OrphanedMarkers { get; init; }

    /// <summary>Markers whose identifier does not match the expected format.</summary>
    public required IReadOnlyList<Marker> MalformedMarkers { get; init; }

    public required IReadOnlyList<string> DuplicateIds { get; init; }

    /// <summary>Evidence entries citing an identifier README.md does not define.</summary>
    public required IReadOnlyList<Evidence> OrphanedEvidence { get; init; }

    /// <summary>The results document used, so the summary can say whether execution was verified.</summary>
    public required TestResultsDocument Results { get; init; }

    /// <summary>
    /// Assemblies whose code differs from the code the results were produced
    /// against. Each entry means some test outcome in the file describes an
    /// assembly that has since been rebuilt.
    /// </summary>
    public required IReadOnlyList<string> StaleAssemblies { get; init; }

    public static CoverageMatrix Build(
        SpecDocument document, ScanResult scan, EvidenceDocument evidence, TestResultsDocument results)
    {
        var known = document.Requirements.ToDictionary(r => r.Id, StringComparer.Ordinal);

        var malformed = new List<Marker>();
        var orphaned = new List<Marker>();
        var byId = new Dictionary<string, List<Marker>>(StringComparer.Ordinal);

        foreach (Marker marker in scan.Markers)
        {
            if (!IdPattern.IsMatch(marker.Id))
            {
                malformed.Add(marker);
                continue;
            }

            if (!known.ContainsKey(marker.Id))
            {
                orphaned.Add(marker);
                continue;
            }

            if (!byId.TryGetValue(marker.Id, out List<Marker>? list))
            {
                list = [];
                byId[marker.Id] = list;
            }

            list.Add(marker);
        }

        var evidenceById = new Dictionary<string, List<Evidence>>(StringComparer.Ordinal);
        var orphanedEvidence = new List<Evidence>();

        foreach (Evidence entry in evidence.Entries)
        {
            if (!known.ContainsKey(entry.Id))
            {
                orphanedEvidence.Add(entry);
                continue;
            }

            if (!evidenceById.TryGetValue(entry.Id, out List<Evidence>? list))
            {
                list = [];
                evidenceById[entry.Id] = list;
            }

            list.Add(entry);
        }

        var rows = new List<CoverageRow>();
        foreach (SpecRequirement requirement in document.Requirements)
        {
            List<Marker> markers = byId.TryGetValue(requirement.Id, out List<Marker>? found) ? found : [];
            List<Evidence> entries = evidenceById.TryGetValue(requirement.Id, out List<Evidence>? e) ? e : [];

            rows.Add(new CoverageRow
            {
                Requirement = requirement,
                Implementations = [.. markers.Where(m => !m.IsTest)],
                Tests = [.. markers.Where(m => m.IsTest)],
                Evidence = entries,
                Execution = results.Supplied ? results.For(requirement.Id) ?? new TestExecution(0, 0, 0) : null,
            });
        }

        return new CoverageMatrix
        {
            StaleAssemblies = FindStaleAssemblies(scan, results),
            Rows = rows,
            OrphanedMarkers = orphaned,
            MalformedMarkers = malformed,
            DuplicateIds = document.DuplicateIds,
            OrphanedEvidence = orphanedEvidence,
            Results = results,
        };
    }

    /// <summary>
    /// Compares the fingerprints in the results file against the assemblies
    /// actually scanned.
    /// </summary>
    /// <remarks>
    /// A results file describes the code it ran against. Without this check,
    /// a file left over from before a change still counts as coverage - which
    /// happened in practice, and is exactly the sort of quiet overstatement the
    /// whole mechanism exists to prevent.
    /// </remarks>
    private static List<string> FindStaleAssemblies(ScanResult scan, TestResultsDocument results)
    {
        var stale = new List<string>();

        if (!results.Supplied)
        {
            return stale;
        }

        if (results.AssemblyIds.Count == 0)
        {
            stale.Add(
                "the results file records no assembly fingerprints, so there is no way to tell which "
                + "code it was produced against. Re-run the test suites.");
            return stale;
        }

        foreach ((string name, Guid recorded) in results.AssemblyIds)
        {
            if (!scan.AssemblyIds.TryGetValue(name, out Guid current))
            {
                continue; // Not among the assemblies being checked.
            }

            if (current != recorded)
            {
                stale.Add(
                    $"{name} has been rebuilt since these results were produced "
                    + $"(results {recorded:D}, current {current:D}).");
            }
        }

        return stale;
    }

    public bool HasFailures =>
        StaleAssemblies.Count > 0
        || Rows.Any(r => r.IsFailure)
        || OrphanedMarkers.Count > 0
        || MalformedMarkers.Count > 0
        || DuplicateIds.Count > 0
        || OrphanedEvidence.Count > 0
        || Rows.Any(r => r.Evidence.Any(e => !e.IsIntact));

    /// <summary>
    /// True when coverage rests on markers alone because no results file was
    /// supplied. Not a failure, but the report must not read as if it were
    /// verified.
    /// </summary>
    public bool TestExecutionUnverified => !Results.Supplied && Rows.Any(r => r.Tests.Count > 0);

    public void PrintText(TextWriter output, bool verbose)
    {
        output.WriteLine();
        output.WriteLine(new string('=', 100));
        output.WriteLine("REQUIREMENT COVERAGE MATRIX");
        output.WriteLine(new string('=', 100));

        string? currentArea = null;
        foreach (CoverageRow row in Rows)
        {
            if (row.Requirement.Area != currentArea)
            {
                currentArea = row.Requirement.Area;
                output.WriteLine();
                output.WriteLine($"-- {currentArea} " + new string('-', 96 - currentArea.Length));
            }

            string implementation = row.Evidence.Count > 0 && row.Implementations.Count == 0
                ? DescribeEvidence(row.Evidence)
                : Describe(row.Implementations, verbose);
            string test = row.TestMarkedButNotRun
                ? $"{Describe(row.Tests, verbose)} [{row.Execution!.Explain()}]"
                : Describe(row.Tests, verbose);

            output.WriteLine(
                $"{row.Requirement.Id,-14} {row.Requirement.LevelText,-9} "
                + $"impl: {Fit(implementation, 26)}  test: {Fit(test, 26)}  {row.StatusText}");
        }

        PrintProblems(output);
        PrintSummary(output);
    }

    private void PrintProblems(TextWriter output)
    {
        if (DuplicateIds.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("DUPLICATE REQUIREMENT IDS IN README (a defect in the document):");
            foreach (string duplicate in DuplicateIds)
            {
                output.WriteLine($"  {duplicate}");
            }
        }

        if (MalformedMarkers.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("MALFORMED IDENTIFIERS IN CODE:");
            foreach (Marker marker in MalformedMarkers)
            {
                output.WriteLine($"  '{marker.Id}' at {marker.Location} ({marker.Assembly})");
            }

            output.WriteLine($"  Expected form: {RequirementAttribute.IdPattern}");
        }

        if (StaleAssemblies.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("TEST RESULTS DO NOT DESCRIBE THIS BUILD:");
            foreach (string entry in StaleAssemblies)
            {
                output.WriteLine($"  {entry}");
            }

            output.WriteLine("  Coverage below cannot be trusted: some of it may have been recorded");
            output.WriteLine("  against code that has since changed. Re-run the test suites, then");
            output.WriteLine("  re-run this check.");
        }

        var notRun = Rows.Where(r => r.TestMarkedButNotRun).ToList();
        if (notRun.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("REQUIREMENTS WHOSE TESTS DID NOT ACTUALLY RUN:");
            foreach (CoverageRow row in notRun)
            {
                output.WriteLine($"  {row.Requirement.Id,-14} {row.Execution!.Explain()}");
            }

            output.WriteLine("  A marker on a test is compiled-in metadata and exists whether or not the");
            output.WriteLine("  test executed. These requirements are NOT verified. Either make the test");
            output.WriteLine("  runnable in this environment, or stop claiming it covers the requirement.");
        }

        var broken = Rows.SelectMany(r => r.Evidence).Where(e => !e.IsIntact).ToList();
        if (broken.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("EVIDENCE NAMING ARTIFACTS THAT DO NOT EXIST:");
            foreach (Evidence entry in broken)
            {
                output.WriteLine(
                    $"  {entry.Id} (line {entry.LineNumber}) -> {string.Join(", ", entry.MissingArtifacts)}");
            }

            output.WriteLine("  The artifact was renamed, moved or deleted. Evidence that points at");
            output.WriteLine("  nothing is not evidence; update the entry or the requirement.");
        }

        if (OrphanedEvidence.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("EVIDENCE CITING REQUIREMENTS THE README DOES NOT DEFINE:");
            foreach (Evidence entry in OrphanedEvidence)
            {
                output.WriteLine($"  {entry.Id} (line {entry.LineNumber})");
            }
        }

        if (OrphanedMarkers.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("MARKERS CITING REQUIREMENTS THE README DOES NOT DEFINE:");
            foreach (Marker marker in OrphanedMarkers)
            {
                output.WriteLine($"  {marker.Id} at {marker.Location} ({marker.Assembly})");
            }

            output.WriteLine("  Either the requirement was removed from README.md, or the ID is a typo.");
        }
    }

    private void PrintSummary(TextWriter output)
    {
        int total = Rows.Count;
        int covered = Rows.Count(r => r.Status == CoverageStatus.Covered);
        int byEvidence = Rows.Count(r => r.Status == CoverageStatus.CoveredByEvidence);
        int binding = Rows.Count(r => r.Requirement.IsBinding);
        int bindingCovered = Rows.Count(r =>
            r.Requirement.IsBinding
            && r.Status is CoverageStatus.Covered or CoverageStatus.CoveredByEvidence);
        int failures = Rows.Count(r => r.IsFailure);

        output.WriteLine();
        output.WriteLine(new string('-', 100));
        output.WriteLine($"Requirements          : {total}");
        output.WriteLine($"  covered by code     : {covered}");
        output.WriteLine($"  covered by evidence : {byEvidence}");
        output.WriteLine($"  binding (MUST/NOT)  : {binding}, of which covered: {bindingCovered}");
        output.WriteLine($"  binding gaps        : {failures}");
        output.WriteLine($"Orphaned markers      : {OrphanedMarkers.Count}");
        output.WriteLine($"Malformed markers     : {MalformedMarkers.Count}");
        output.WriteLine($"Duplicate IDs         : {DuplicateIds.Count}");
        output.WriteLine($"Orphaned evidence     : {OrphanedEvidence.Count}");
        output.WriteLine($"Tests marked, not run : {Rows.Count(r => r.TestMarkedButNotRun)}");
        output.WriteLine($"Stale assemblies      : {StaleAssemblies.Count}");
        output.WriteLine(
            Results.Supplied
                ? $"Test execution        : verified from {Results.Path} ({Results.RecordCount} record(s))"
                : "Test execution        : NOT VERIFIED (no --test-results supplied)");
        output.WriteLine(new string('-', 100));
        output.WriteLine();

        if (HasFailures)
        {
            output.WriteLine("RESULT: FAIL - see the gaps listed above.");
        }
        else if (TestExecutionUnverified)
        {
            output.WriteLine("RESULT: PASS, BUT TEST EXECUTION IS UNVERIFIED.");
            output.WriteLine();
            output.WriteLine("  No results file was supplied, so this run cannot tell a test that ran");
            output.WriteLine("  from one that was skipped. Run the test suites with --results and pass");
            output.WriteLine("  the file to --test-results before treating this as a pass.");
        }
        else
        {
            output.WriteLine("RESULT: PASS - every binding requirement has an implementation and a test");
            output.WriteLine("that is recorded as having passed.");
        }

        output.WriteLine();
        output.WriteLine("What this result means, and what it does not:");
        output.WriteLine("  A requirement marked OK carries an implementation marker and a test marker.");
        output.WriteLine("  That is all it means. This tool finds OMISSIONS. It cannot judge whether the");
        output.WriteLine("  marked code does what the requirement says, whether the test is meaningful,");
        output.WriteLine("  or whether a marker was placed honestly. A green run is not compliance;");
        output.WriteLine("  reading the marked code is.");

        if (byEvidence > 0)
        {
            output.WriteLine();
            output.WriteLine($"  {byEvidence} requirement(s) are satisfied by recorded evidence rather than by");
            output.WriteLine("  code and tests. Their artifacts were confirmed to exist, which proves the");
            output.WriteLine("  reference is live - not that the artifact does what the entry claims.");
            output.WriteLine("  These are listed as 'OK (evidence)' above and deserve closer reading.");
        }
    }

    /// <summary>Renders the matrix as Markdown, for committing to docs/.</summary>
    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Requirement coverage matrix");
        builder.AppendLine();
        builder.AppendLine(
            $"Generated by `tools/SecretPrinter.SpecCheck`. "
            + $"{Rows.Count(r => r.Status == CoverageStatus.Covered)} of {Rows.Count} requirements fully covered.");
        builder.AppendLine();
        builder.AppendLine("A row marked OK has an implementation marker and a test marker. That is all it");
        builder.AppendLine("means: this table records omissions, not correctness.");
        builder.AppendLine();
        builder.AppendLine("| ID | Level | Implementation | Test | Status |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (CoverageRow row in Rows)
        {
            builder.AppendLine(
                $"| {row.Requirement.Id} | {row.Requirement.LevelText} "
                + $"| {Escape(row.Evidence.Count > 0 && row.Implementations.Count == 0 ? DescribeEvidence(row.Evidence) : Describe(row.Implementations, verbose: false))} "
                + $"| {Escape(Describe(row.Tests, verbose: false))} "
                + $"| {row.StatusText} |");
        }

        return builder.ToString();
    }

    private static string DescribeEvidence(IReadOnlyList<Evidence> entries)
    {
        string artifacts = string.Join(", ", entries.SelectMany(e => e.Artifacts));
        return $"evidence: {artifacts}";
    }

    private static string Describe(IReadOnlyList<Marker> markers, bool verbose)
    {
        if (markers.Count == 0)
        {
            return "(none)";
        }

        if (verbose)
        {
            return string.Join("; ", markers.Select(m => m.Location));
        }

        string first = ShortName(markers[0].Location);
        return markers.Count == 1 ? first : $"{first} +{markers.Count - 1}";
    }

    /// <summary>Trims a fully-qualified location down to something readable in a column.</summary>
    private static string ShortName(string location)
    {
        int space = location.IndexOf(' ', StringComparison.Ordinal);
        string qualified = space >= 0 ? location[(space + 1)..] : location;

        string[] parts = qualified.Split('.');
        return parts.Length <= 2 ? qualified : string.Join('.', parts[^2..]);
    }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text.PadRight(width) : string.Concat(text.AsSpan(0, width - 1), "~");

    private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}

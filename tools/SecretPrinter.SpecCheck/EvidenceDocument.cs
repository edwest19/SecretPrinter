// -----------------------------------------------------------------------------
// EvidenceDocument.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Reads docs/verification.md, which records how requirements that cannot be
//   satisfied by code are satisfied instead.
//
// Why this file has to exist:
//   Some requirements are not about code at all. "Release binaries are signed"
//   and "CI runs the specification checker" are properties of the build and
//   release process; no attribute on a class can ever satisfy them. Without a
//   way to record them, those requirements sit permanently at NOT IMPLEMENTED,
//   the check can never pass, and a check that can never pass gets ignored.
//
// What keeps this from becoming an escape hatch:
//   1. Every entry MUST name at least one artifact by repository-relative path,
//      and SpecCheck verifies each path exists. Evidence pointing at a file
//      that was renamed or deleted is reported as a failure, not quietly
//      accepted. A claim that cannot rot unnoticed is worth something; a free
//      text assertion is not.
//   2. Requirements covered by evidence rather than by code are counted and
//      listed separately in every run, so the proportion is always visible.
//      Evidence is permitted for any requirement, because forbidding it for
//      some would require this tool to decide which requirements are "really"
//      about code - a judgement it has no business making. Making the usage
//      loud is the safeguard instead.
//
// Expected format, exactly:
//   | REQ-DIST-004 | .github/workflows/release.yml | The sign step invokes ... |
//
//   Three fields: identifier, one or more artifact paths separated by commas or
//   semicolons, and an explanation. Backticks around paths are tolerated
//   because Markdown authors habitually add them.
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SecretPrinter.SpecCheck;

/// <summary>One recorded piece of evidence for a requirement.</summary>
/// <param name="Id">The requirement this evidence is offered for.</param>
/// <param name="Artifacts">Repository-relative paths named as evidence.</param>
/// <param name="Note">Explanation of how the artifacts satisfy the requirement.</param>
/// <param name="LineNumber">Line in the evidence document, for error messages.</param>
internal sealed record Evidence(
    string Id,
    IReadOnlyList<string> Artifacts,
    string Note,
    int LineNumber)
{
    /// <summary>Artifact paths that do not exist on disk. Empty means the evidence still holds.</summary>
    public IReadOnlyList<string> MissingArtifacts { get; private set; } = [];

    /// <summary>Resolves each artifact against the repository root and records any that are absent.</summary>
    public void Verify(string repositoryRoot)
    {
        var missing = new List<string>();
        foreach (string artifact in Artifacts)
        {
            string resolved = Path.Combine(repositoryRoot, artifact.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(resolved) && !Directory.Exists(resolved))
            {
                missing.Add(artifact);
            }
        }

        MissingArtifacts = missing;
    }

    public bool IsIntact => MissingArtifacts.Count == 0;
}

/// <summary>The evidence entries extracted from a verification document.</summary>
internal sealed class EvidenceDocument
{
    private static readonly Regex RowPattern = new(
        @"^\|\s*(?<id>REQ-[A-Z]{3,4}-\d{3})\s*\|\s*(?<artifacts>[^|]+?)\s*\|\s*(?<note>.+?)\s*\|\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly char[] ArtifactSeparators = [',', ';'];

    private EvidenceDocument(IReadOnlyList<Evidence> entries, string path)
    {
        Entries = entries;
        Path = path;
    }

    public IReadOnlyList<Evidence> Entries { get; }

    public string Path { get; }

    public static EvidenceDocument Empty(string path) => new([], path);

    public static EvidenceDocument Load(string path, string repositoryRoot)
    {
        string[] lines = File.ReadAllLines(path);
        var entries = new List<Evidence>();

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = RowPattern.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var artifacts = match.Groups["artifacts"].Value
                .Split(ArtifactSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(a => a.Trim('`', ' '))
                .Where(a => a.Length > 0)
                .ToList();

            if (artifacts.Count == 0)
            {
                // An entry naming no artifact is not evidence, it is an
                // assertion. Skipping it leaves the requirement uncovered,
                // which is the honest outcome.
                continue;
            }

            var evidence = new Evidence(
                match.Groups["id"].Value,
                artifacts,
                match.Groups["note"].Value,
                i + 1);

            evidence.Verify(repositoryRoot);
            entries.Add(evidence);
        }

        return new EvidenceDocument(entries, path);
    }
}

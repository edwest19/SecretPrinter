// -----------------------------------------------------------------------------
// SpecDocument.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Reads README.md and extracts the numbered requirements from its tables.
//
// Why parse the README rather than a separate machine-readable file:
//   A YAML or JSON copy of the requirements would be easier to parse and would
//   immediately begin to drift from the prose that humans actually read. The
//   README is the specification, so the README is what gets parsed. If the
//   table format changes, this parser breaks loudly, which is the correct
//   outcome - it means the document a reader sees and the document the tool
//   checks are the same document.
//
// The format this expects, exactly:
//   | REQ-AREA-NNN | MUST | Requirement text. |
//
//   Four pipe-delimited fields, in a Markdown table. Header and separator rows
//   are ignored. Anything that is not a well-formed requirement row is not a
//   requirement, including prose that mentions an ID in passing.
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SecretPrinter.SpecCheck;

/// <summary>How binding a requirement is, using RFC 2119 terms.</summary>
internal enum RequirementLevel
{
    /// <summary>Absolute requirement. Missing coverage fails the build.</summary>
    Must,

    /// <summary>Absolute prohibition. Missing coverage fails the build.</summary>
    MustNot,

    /// <summary>Recommended. Reported but does not fail the build.</summary>
    Should,

    /// <summary>Optional. Reported but does not fail the build.</summary>
    May,
}

/// <summary>One requirement, as written in README.md.</summary>
internal sealed record SpecRequirement(string Id, RequirementLevel Level, string Text, int LineNumber)
{
    /// <summary>Area code from the identifier, e.g. "PXY" from "REQ-PXY-003".</summary>
    public string Area => Id.Split('-')[1];

    /// <summary>True when a missing implementation or test should fail the build.</summary>
    public bool IsBinding => Level is RequirementLevel.Must or RequirementLevel.MustNot;

    public string LevelText => Level switch
    {
        RequirementLevel.Must => "MUST",
        RequirementLevel.MustNot => "MUST NOT",
        RequirementLevel.Should => "SHOULD",
        RequirementLevel.May => "MAY",
        _ => Level.ToString().ToUpperInvariant(),
    };
}

/// <summary>The requirements extracted from a specification document.</summary>
internal sealed class SpecDocument
{
    /// <summary>
    /// Matches a requirement table row. Deliberately strict: the pipes and
    /// spacing must be exactly as the document writes them, so that a row which
    /// merely looks like a requirement cannot be silently accepted.
    /// </summary>
    private static readonly Regex RowPattern = new(
        @"^\|\s*(?<id>REQ-[A-Z]{3,4}-\d{3})\s*\|\s*(?<level>MUST NOT|MUST|SHOULD|MAY)\s*\|\s*(?<text>.+?)\s*\|\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private SpecDocument(
        IReadOnlyList<SpecRequirement> requirements,
        IReadOnlyList<string> duplicateIds,
        string path)
    {
        Requirements = requirements;
        DuplicateIds = duplicateIds;
        Path = path;
    }

    public IReadOnlyList<SpecRequirement> Requirements { get; }

    /// <summary>Identifiers appearing more than once. Always a defect in the document.</summary>
    public IReadOnlyList<string> DuplicateIds { get; }

    public string Path { get; }

    public static SpecDocument Load(string path)
    {
        string[] lines = File.ReadAllLines(path);

        var requirements = new List<SpecRequirement>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = RowPattern.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string id = match.Groups["id"].Value;
            RequirementLevel level = match.Groups["level"].Value switch
            {
                "MUST NOT" => RequirementLevel.MustNot,
                "MUST" => RequirementLevel.Must,
                "SHOULD" => RequirementLevel.Should,
                "MAY" => RequirementLevel.May,

                // Unreachable while the regex and this switch agree. Kept as a
                // loud failure rather than a silent default, so that widening
                // one without the other cannot pass unnoticed.
                _ => throw new InvalidDataException(
                    $"{path}({i + 1}): unhandled requirement level '{match.Groups["level"].Value}'."),
            };

            if (seen.TryGetValue(id, out int firstLine))
            {
                duplicates.Add($"{id} (lines {firstLine} and {i + 1})");
                continue;
            }

            seen[id] = i + 1;
            requirements.Add(new SpecRequirement(id, level, match.Groups["text"].Value, i + 1));
        }

        return new SpecDocument(requirements, duplicates, path);
    }
}

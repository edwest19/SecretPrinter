// -----------------------------------------------------------------------------
// RequirementAttribute.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Ties a piece of code, or a test, to a numbered requirement in README.md.
//
//   The README is the specification. Requirements there carry stable IDs of the
//   form REQ-AREA-NNN. Applying this attribute is how code says "I am the part
//   that satisfies REQ-PXY-003", and how a test says "I am the part that proves
//   it". The SpecCheck tool reads both sides and reports any requirement that
//   is missing an implementation, missing a test, or citing an ID the README
//   does not contain.
//
// What this attribute proves, and what it does not:
//   It proves a claim was MADE. It does not prove the claim is TRUE.
//
//   Nothing stops someone marking an empty method with [Requirement(...)] and
//   satisfying the tool. The tool exists to make omissions visible, not to
//   verify correctness. Judging whether the marked code actually does what the
//   requirement says is a job for a reviewer - human or AI - reading the code.
//   This is stated here, in the attribute itself, so that nobody mistakes a
//   green SpecCheck run for a proof of compliance.
// -----------------------------------------------------------------------------

namespace SecretPrinter.Spec;

/// <summary>
/// Declares that the annotated element implements, or verifies, a numbered
/// requirement from README.md.
/// </summary>
/// <remarks>
/// <para>
/// Apply to the narrowest element that carries the responsibility. A
/// requirement about one method belongs on that method, not on its containing
/// class, because a reviewer following the marker should land on the code that
/// matters rather than on a page of unrelated members.
/// </para>
/// <para>
/// Multiple attributes may be applied when one element genuinely satisfies
/// several requirements. If a single method accumulates many, that is usually a
/// sign it should be split.
/// </para>
/// <para>
/// Assembly-level markers exist for requirements satisfied by an ABSENCE. "This
/// service never starts an external process" is not implemented by any method;
/// it is a property of the whole compiled output, and the honest place to
/// declare it is the assembly. Such claims are verified by tests that read the
/// compiled metadata, so a marker cannot outlive the property it describes.
/// </para>
/// <para>
/// This attribute is retained in release builds. SpecCheck reflects over the
/// compiled assemblies, including the ones that ship, so that the shipped
/// artifact can be checked rather than only a debug build.
/// </para>
/// </remarks>
/// <example>
/// On an implementation:
/// <code>
/// [Requirement("REQ-PXY-003",
///     "Payload bytes are copied between streams without inspection.")]
/// internal sealed class IppRelay { }
/// </code>
/// On a test:
/// <code>
/// [Fact]
/// [Requirement("REQ-PXY-003")]
/// public async Task Relay_does_not_alter_payload_bytes() { }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Assembly
        | AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Enum
        | AttributeTargets.Method
        | AttributeTargets.Constructor
        | AttributeTargets.Property
        | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false)]
public sealed class RequirementAttribute : Attribute
{
    /// <summary>
    /// Creates a requirement marker.
    /// </summary>
    /// <param name="id">
    /// The requirement identifier exactly as written in README.md, for example
    /// <c>REQ-PXY-003</c>.
    /// </param>
    /// <param name="note">
    /// Optional. How this element satisfies the requirement, when that is not
    /// obvious from the code. Not a restatement of the requirement text: the
    /// README already holds that, and duplicating it invites the two to drift.
    /// </param>
    /// <remarks>
    /// The identifier is deliberately NOT validated here, and this constructor
    /// deliberately does not throw. Attribute constructors run during
    /// reflection, so a throwing constructor would break the very tool meant to
    /// detect the problem - the failure would surface as a crash in SpecCheck
    /// rather than as a clear report. Format and existence are checked by
    /// SpecCheck against README.md, where a malformed or unknown ID can be
    /// reported plainly alongside every other finding.
    /// </remarks>
    public RequirementAttribute(string id, string? note = null)
    {
        Id = id;
        Note = note;
    }

    /// <summary>The requirement identifier, as written in README.md.</summary>
    public string Id { get; }

    /// <summary>Optional explanation of how this element satisfies the requirement.</summary>
    public string? Note { get; }

    /// <summary>
    /// The identifier pattern requirements are expected to follow:
    /// <c>REQ-</c>, an uppercase area code, and a three-digit number.
    /// </summary>
    /// <remarks>
    /// Exposed as a constant so SpecCheck and any future tooling apply the same
    /// definition, rather than each carrying its own copy of the pattern.
    /// </remarks>
    public const string IdPattern = @"^REQ-[A-Z]{3,4}-\d{3}$";

    /// <inheritdoc />
    public override string ToString() =>
        Note is null ? Id : $"{Id}: {Note}";
}

// -----------------------------------------------------------------------------
// ProgramVersion.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-18, for REQ-OBS-011. Reviewed by a human before merge.
//
// Purpose:
//   Words the line the service logs first at every start: which program this
//   is, and the version stamped in it.
//
// Why the log needs it:
//   The log file is appended to across runs and across updates. Until this
//   line existed, nothing in it said which build had written what followed;
//   an operator had to read the program file's ProductVersion, which says
//   what is installed now, not what was running when a line was written.
//
// What the stamp is:
//   The assembly's informational version, which is what Windows shows as the
//   file's ProductVersion. Directory.Build.props sets the version, and the .NET
//   SDK appends a '+' and the commit the building clone was on when it builds
//   from a git clone; a release reads like
//   0.3.0+064ca9fc7cf649ff3eed615758274bedb533767f. Built from a project that
//   is not in a git clone, the stamp is the version alone, as measured in
//   Claude's workspace on 2026-10-18. Nothing here adds to or interprets the
//   stamp; it is written as found.
//
// The program it describes:
//   The caller passes the assembly, and the line names it, so that the line
//   cannot silently describe some other assembly in the process, such as a
//   test runner that hosts the service's code.
// -----------------------------------------------------------------------------

using System.Reflection;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>
/// The first line the service logs at every start: the program's name and the
/// version stamped in it.
/// </summary>
public static class ProgramVersion
{
    /// <summary>
    /// Words the version line for <paramref name="program"/>.
    /// </summary>
    /// <returns>
    /// <c>{name} version {stamp}</c>, with nothing after the stamp so that it
    /// can be copied exactly, or, for an assembly that carries no
    /// informational version, a line saying so.
    /// </returns>
    [Requirement("REQ-OBS-011",
        "Words the line from the assembly's informational version, the text Windows reports as the file's ProductVersion, and names the assembly it was read from; says so when no version is stamped.")]
    public static string LineFor(Assembly program)
    {
        ArgumentNullException.ThrowIfNull(program);

        string name = program.GetName().Name ?? "(an assembly with no name)";
        string? stamp = program.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(stamp)
            ? $"{name}: no version is stamped in this program."
            : $"{name} version {stamp}";
    }
}

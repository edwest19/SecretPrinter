// -----------------------------------------------------------------------------
// MarkerScanner.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Finds every [Requirement] marker in a set of compiled assemblies.
//
// Two implementation choices worth explaining, because both look odd:
//
//   1. Attributes are read via GetCustomAttributesData(), not
//      GetCustomAttributes<RequirementAttribute>().
//
//      The scanned assemblies carry their own copy of SecretPrinter.Spec.dll.
//      A RequirementAttribute loaded from that copy is a DIFFERENT type, as far
//      as the runtime is concerned, from the one this tool was compiled
//      against, so a generic lookup silently returns nothing - the worst
//      possible failure, since it reports full coverage as zero coverage with
//      no error. Matching on the attribute's full type name avoids that
//      entirely. It also means attribute constructors are never invoked, so
//      scanning cannot run code from the assembly being scanned.
//
//   2. An assembly is treated as a TEST assembly when its simple name ends in
//      ".Tests".
//
//      A convention, not a detection. It is stated here and in the README so
//      that it is a rule the repository follows rather than a heuristic that
//      might guess wrong. A test project named otherwise would have its markers
//      counted as implementations, which is why the convention is documented
//      rather than inferred.
// -----------------------------------------------------------------------------

using System.Reflection;
using SecretPrinter.Spec;

namespace SecretPrinter.SpecCheck;

/// <summary>One [Requirement] marker found in compiled code.</summary>
/// <param name="Id">The requirement identifier as written in the attribute.</param>
/// <param name="Note">Optional explanation supplied at the marker.</param>
/// <param name="Location">Human-readable description of what carries the marker.</param>
/// <param name="Assembly">Simple name of the containing assembly.</param>
/// <param name="IsTest">True when the marker is in a test assembly.</param>
internal sealed record Marker(string Id, string? Note, string Location, string Assembly, bool IsTest);

/// <summary>Result of scanning a set of assemblies for markers.</summary>
internal sealed class ScanResult
{
    public required IReadOnlyList<Marker> Markers { get; init; }
    public required IReadOnlyList<string> AssembliesScanned { get; init; }

    /// <summary>
    /// Module version id of each assembly scanned, by simple name. Compared
    /// against the fingerprints in a results file so that results produced
    /// against different code are refused.
    /// </summary>
    public required IReadOnlyDictionary<string, Guid> AssemblyIds { get; init; }

    /// <summary>Problems encountered while scanning; reported, never swallowed.</summary>
    public required IReadOnlyList<string> Diagnostics { get; init; }
}

internal static class MarkerScanner
{
    /// <summary>
    /// The attribute's full name, taken from the type this tool references, so
    /// that renaming the attribute cannot leave a stale string behind here.
    /// </summary>
    private static readonly string AttributeFullName = typeof(RequirementAttribute).FullName!;

    private const string TestAssemblySuffix = ".Tests";

    public static ScanResult Scan(IReadOnlyList<string> assemblyPaths)
    {
        var markers = new List<Marker>();
        var scanned = new List<string>();
        var diagnostics = new List<string>();
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (string path in assemblyPaths)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                diagnostics.Add($"Could not load '{path}': {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            string simpleName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(path);
            bool isTest = simpleName.EndsWith(TestAssemblySuffix, StringComparison.Ordinal);
            scanned.Add($"{simpleName}{(isTest ? "  (test)" : string.Empty)}  <- {path}");

            try
            {
                ids[simpleName] = assembly.ManifestModule.ModuleVersionId;
            }
            catch (NotSupportedException)
            {
                diagnostics.Add($"'{simpleName}': no module version id, so staleness cannot be checked.");
            }

            // Assembly-level markers: requirements satisfied by an absence,
            // which no type or method can carry.
            CollectFromCustomAttributes(
                assembly.GetCustomAttributesData(),
                $"assembly {simpleName}",
                simpleName,
                isTest,
                markers);

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Report the partial load rather than pretending it was clean.
                types = [.. ex.Types.Where(t => t is not null).Select(t => t!)];
                diagnostics.Add(
                    $"'{simpleName}': {ex.Types.Length - types.Length} type(s) failed to load; "
                    + "markers on those types cannot be seen.");
            }

            foreach (Type type in types)
            {
                CollectFrom(type, $"type {type.FullName}", simpleName, isTest, markers);

                MemberInfo[] members;
                try
                {
                    members = type.GetMembers(
                        BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static
                        | BindingFlags.DeclaredOnly);
                }
                catch (TypeLoadException ex)
                {
                    diagnostics.Add($"'{simpleName}': could not enumerate members of {type.FullName}: {ex.Message}");
                    continue;
                }

                foreach (MemberInfo member in members)
                {
                    CollectFrom(
                        member,
                        $"{DescribeKind(member)} {type.FullName}.{member.Name}",
                        simpleName,
                        isTest,
                        markers);
                }
            }
        }

        return new ScanResult
        {
            Markers = markers,
            AssembliesScanned = scanned,
            Diagnostics = diagnostics,
            AssemblyIds = ids,
        };
    }

    private static void CollectFrom(
        MemberInfo member, string location, string assembly, bool isTest, List<Marker> into)
    {
        IList<CustomAttributeData> attributes;
        try
        {
            attributes = member.GetCustomAttributesData();
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException)
        {
            // A marker we cannot read is invisible to coverage. Rather than
            // guess, leave it out; the assembly-level diagnostic already warns
            // that this assembly did not load cleanly.
            return;
        }

        CollectFromCustomAttributes(attributes, location, assembly, isTest, into);
    }

    private static void CollectFromCustomAttributes(
        IList<CustomAttributeData> attributes, string location, string assembly, bool isTest, List<Marker> into)
    {
        foreach (CustomAttributeData attribute in attributes)
        {
            if (!string.Equals(attribute.AttributeType.FullName, AttributeFullName, StringComparison.Ordinal))
            {
                continue;
            }

            IList<CustomAttributeTypedArgument> arguments = attribute.ConstructorArguments;
            if (arguments.Count == 0 || arguments[0].Value is not string id)
            {
                continue;
            }

            string? note = arguments.Count > 1 ? arguments[1].Value as string : null;
            into.Add(new Marker(id, note, location, assembly, isTest));
        }
    }

    private static string DescribeKind(MemberInfo member) => member switch
    {
        ConstructorInfo => "ctor",
        MethodInfo => "method",
        PropertyInfo => "property",
        FieldInfo => "field",
        Type => "nested type",
        _ => "member",
    };
}

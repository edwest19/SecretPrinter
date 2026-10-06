// -----------------------------------------------------------------------------
// ProbeClaimsTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-06, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks REQ-SEC-016: what the probe, SecretPrinter.Probe, cannot do. The
//   probe is the one tool a release includes beside the service (REQ-DIST-011),
//   so it is installed wherever the service is.
//
// Why this is a separate file from SecurityClaimsTests:
//   Those tests read the nine assemblies that make up the service. The probe is
//   a different program with a different job. It opens a UDP socket to put its
//   questions to a network, which no assembly of the service outside
//   SecretPrinter.Mdns and SecretPrinter.Proxy may do, so it cannot simply be
//   added to that list. From 2026-10-04, when the probe went into the release
//   folder, until this file was written, no test read it at all
//   (docs/findings/2026-10-06-the-probe-was-in-the-release-and-outside-the-security-checks.md).
//
// How:
//   The same way as SecurityClaimsTests: by reading the type-reference table of
//   each compiled assembly. A type an assembly does not reference is a thing no
//   code path in it can use. The probe's process loads three assemblies of this
//   project, its own and the two it is built on, and all three are read.
//
// What these tests do NOT prove:
//   That the probe uses its socket well, or that it opens only one. They show
//   which capabilities are absent. What the probe does with the one it has is
//   read from tools/SecretPrinter.Probe/Program.cs, whose header states it in
//   full.
// -----------------------------------------------------------------------------

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ProbeClaimsTests
{
    /// <summary>The probe's own assembly.</summary>
    private const string ProbeAssembly = "SecretPrinter.Probe";

    /// <summary>
    /// The assemblies of this project that the probe is built on. They are
    /// loaded into the probe's process, so what they can do, the probe can do.
    /// The last test in this file fails if this list falls behind the probe's
    /// project file.
    /// </summary>
    private static readonly string[] BuiltOn = ["SecretPrinter.Dns", "SecretPrinter.Spec"];

    /// <summary>Every assembly of this project in the probe's process.</summary>
    private static readonly string[] ProbeAssemblies = [ProbeAssembly, .. BuiltOn];

    private static string PathOf(string assemblyName)
    {
        string directory = Path.GetDirectoryName(typeof(ProbeClaimsTests).Assembly.Location)!;
        string path = Path.Combine(directory, assemblyName + ".dll");

        if (!File.Exists(path))
        {
            throw new AssertionException(
                $"{assemblyName}.dll is not beside the test assembly, so the probe's claims cannot be checked. "
                + "This test project must reference the probe's project.");
        }

        return path;
    }

    /// <summary>Every type reference in an assembly, as a namespace and a name.</summary>
    private static List<(string Namespace, string Name)> TypeReferences(string assemblyName)
    {
        var references = new List<(string Namespace, string Name)>();

        using FileStream stream = File.OpenRead(PathOf(assemblyName));
        using var reader = new PEReader(stream);
        MetadataReader metadata = reader.GetMetadataReader();

        foreach (TypeReferenceHandle handle in metadata.TypeReferences)
        {
            TypeReference reference = metadata.GetTypeReference(handle);
            references.Add((metadata.GetString(reference.Namespace), metadata.GetString(reference.Name)));
        }

        return references;
    }

    /// <summary>The simple name of every assembly an assembly references.</summary>
    private static List<string> AssemblyReferences(string assemblyName)
    {
        var references = new List<string>();

        using FileStream stream = File.OpenRead(PathOf(assemblyName));
        using var reader = new PEReader(stream);
        MetadataReader metadata = reader.GetMetadataReader();

        foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
        {
            references.Add(metadata.GetString(metadata.GetAssemblyReference(handle).Name));
        }

        return references;
    }

    /// <summary>Finds forbidden type references in the given assemblies.</summary>
    private static List<string> FindForbidden(
        IReadOnlyList<string> assemblies, IReadOnlyList<string> forbiddenTypes)
    {
        var found = new List<string>();

        foreach (string assembly in assemblies)
        {
            foreach ((string ns, string name) in TypeReferences(assembly))
            {
                string reference = $"{ns}.{name}";

                if (forbiddenTypes.Contains(reference, StringComparer.Ordinal))
                {
                    found.Add($"{assembly} references {reference}");
                }
            }
        }

        return found;
    }

    // ---- No external processes ----------------------------------------------

    [TestCase("The probe cannot start another program")]
    [Requirement("REQ-SEC-016")]
    public static void Probe_cannot_start_a_program()
    {
        // The same two types SecurityClaimsTests forbids the service. Without
        // them the probe has no way to run netsh, route or anything else.
        List<string> found = FindForbidden(
            ProbeAssemblies,
            ["System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo"]);

        Assert.Equal(0, found.Count,
            "the probe must not be able to run a command, but found: " + string.Join(", ", found));
    }

    // ---- No registry --------------------------------------------------------

    [TestCase("The probe cannot read or write the registry")]
    [Requirement("REQ-SEC-016")]
    public static void Probe_has_no_registry_access()
    {
        List<string> found = FindForbidden(
            ProbeAssemblies,
            [
                "Microsoft.Win32.Registry",
                "Microsoft.Win32.RegistryKey",
                "Microsoft.Win32.RegistryHive",
            ]);

        Assert.Equal(0, found.Count,
            "the probe must not touch the registry, but found: " + string.Join(", ", found));
    }

    // ---- No HTTP ------------------------------------------------------------

    [TestCase("The probe cannot make an HTTP request")]
    [Requirement("REQ-SEC-016")]
    public static void Probe_has_no_http_client()
    {
        List<string> found = FindForbidden(
            ProbeAssemblies,
            [
                "System.Net.Http.HttpClient",
                "System.Net.Http.HttpRequestMessage",
                "System.Net.Http.HttpClientHandler",
                "System.Net.WebClient",
                "System.Net.WebRequest",
                "System.Net.HttpWebRequest",
            ]);

        Assert.Equal(0, found.Count,
            "the probe must not be able to speak HTTP, but found: " + string.Join(", ", found));
    }

    // ---- No files -----------------------------------------------------------

    [TestCase("The probe references no file-writing type")]
    [Requirement("REQ-SEC-016")]
    public static void Probe_writes_no_files()
    {
        // The rule the relay is held to for REQ-PXY-004, with the same names:
        // any of these types, in any namespace beginning System.IO. The probe
        // prints to the console; System.Console and System.IO.TextWriter are
        // not in the list, and it references both.
        string[] forbidden =
        [
            "File", "FileStream", "FileInfo", "StreamWriter", "Directory", "DirectoryInfo",
            "IsolatedStorageFile", "MemoryMappedFile",
        ];

        var found = new List<string>();

        foreach (string assembly in ProbeAssemblies)
        {
            foreach ((string ns, string name) in TypeReferences(assembly))
            {
                if (ns.StartsWith("System.IO", StringComparison.Ordinal)
                    && forbidden.Contains(name, StringComparer.Ordinal))
                {
                    found.Add($"{assembly} references {ns}.{name}");
                }
            }
        }

        Assert.Equal(0, found.Count,
            "the probe must contain no file-writing type reference, but found: " + string.Join(", ", found));
    }

    // ---- The one socket -----------------------------------------------------

    [TestCase("The probe opens a socket, and only its own assembly can")]
    [Requirement("REQ-SEC-016")]
    public static void Only_the_probes_own_assembly_references_a_socket()
    {
        // Two things at once. First, the probe does reference Socket. That is
        // its job, the requirement says so, and it is why the probe is not in
        // the list SecurityClaimsTests reads. It is also this file's check that
        // the scan sees real references: if it could not see this one, the
        // absence checks above would be passing with nothing to look at.
        List<(string Namespace, string Name)> probe = TypeReferences(ProbeAssembly);

        Assert.True(probe.Count > 0, "the probe's assembly must have type references to inspect");
        Assert.True(probe.Contains(("System.Net.Sockets", "Socket")),
            "SecretPrinter.Probe opens a UDP socket; if the scan cannot see that reference, it cannot "
            + "see anything, and the absence checks in this file prove nothing");

        // Second, the two assemblies it is built on reference no socket type, so
        // the only place the probe's network use can be is its own assembly.
        List<string> found = FindForbidden(
            BuiltOn,
            [
                "System.Net.Sockets.Socket",
                "System.Net.Sockets.TcpListener",
                "System.Net.Sockets.TcpClient",
                "System.Net.Sockets.UdpClient",
                "System.Net.Sockets.NetworkStream",
            ]);

        Assert.Equal(0, found.Count,
            "the assemblies the probe is built on must reference no socket type, but found: "
            + string.Join(", ", found));
    }

    // ---- The list above is the whole list -----------------------------------

    [TestCase("The probe is built on no project assembly these tests do not read")]
    [Requirement("REQ-SEC-016")]
    public static void Probe_is_built_on_what_is_checked()
    {
        // Every check above reads ProbeAssemblies. If the probe's project file
        // gained a reference to another SecretPrinter assembly, that assembly
        // would be loaded into the probe's process and read by nothing here.
        // The probe went unread for two days in exactly that way: a list that
        // was right when written and was not looked at again.
        var unread = new List<string>();

        foreach (string assembly in ProbeAssemblies)
        {
            foreach (string referenced in AssemblyReferences(assembly))
            {
                if (referenced.StartsWith("SecretPrinter.", StringComparison.Ordinal)
                    && !ProbeAssemblies.Contains(referenced, StringComparer.Ordinal))
                {
                    unread.Add($"{assembly} references {referenced}");
                }
            }
        }

        Assert.Equal(0, unread.Count,
            "every SecretPrinter assembly in the probe's process must be in ProbeAssemblies, but found: "
            + string.Join(", ", unread));
    }
}

// -----------------------------------------------------------------------------
// ConfigurationLoader.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Reads the configuration file, checks every setting, resolves every
//   interface name, and either returns something the service can run on or
//   refuses with a list of what is wrong.
//
// Two decisions worth explaining:
//
//   Problems are collected, not thrown one at a time. An operator setting this
//   up on a machine they had to walk to should learn about all four mistakes in
//   one run, not discover a fifth after four restarts.
//
//   The JSON is read with JsonDocument and inspected by hand rather than
//   deserialised into a type. Deserialisation gives "cannot convert value" and
//   leaves the operator hunting; reading it by hand lets every message name the
//   exact setting, which is what REQ-CFG-003 asks for. It also avoids the
//   trimming and reflection machinery that automatic deserialisation drags in.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text.Json;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Configuration;

/// <summary>Loads and validates the service's configuration.</summary>
public static class ConfigurationLoader
{
    /// <summary>Loads from a file.</summary>
    public static ServiceConfiguration LoadFile(string path, IInterfaceInventory inventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new ConfigurationException(
                $"No configuration file at '{path}'. The service will not start without one, because "
                + "every setting that decides what is advertised and where traffic goes must be stated "
                + "explicitly.");
        }

        return Load(File.ReadAllText(path), inventory);
    }

    /// <summary>
    /// Loads from JSON text.
    /// </summary>
    /// <exception cref="ConfigurationException">
    /// Any setting is missing, malformed, or inconsistent. The exception names
    /// every problem found, not merely the first.
    /// </exception>
    [Requirement("REQ-CFG-001",
        "Every setting that affects what is advertised or where traffic is sent is required. Defaults exist only for tuning values, which are gathered in TuningSettings.Default.")]
    [Requirement("REQ-CFG-002",
        "Client interfaces and the printer interface are named by separate settings that cannot be confused for one another.")]
    [Requirement("REQ-CFG-003",
        "Validates every setting and throws naming each offending one, rather than failing on the first or starting with a bad value.")]
    [Requirement("REQ-CFG-005",
        "Validation and interface resolution complete before any configuration is returned, so the service never begins with a partially usable set.")]
    public static ServiceConfiguration Load(string json, IInterfaceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(inventory);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException(
                $"The configuration file is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var problems = new List<string>();
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigurationException("The configuration file must contain a JSON object.");
            }

            List<string> clientNames = RequiredStringArray(root, "clientInterfaces", problems);
            string? printerName = RequiredString(root, "printerInterface", problems);
            string? printerInstance = RequiredString(root, "printerInstance", problems);

            JsonElement advertise = Section(root, "advertise", problems);
            string? instanceName = RequiredString(advertise, "instanceName", problems, "advertise.");
            string? hostLabel = RequiredString(advertise, "hostLabel", problems, "advertise.");
            Guid uuid = RequiredGuid(advertise, "uuid", problems, "advertise.");
            ushort port = RequiredPort(advertise, "port", problems, "advertise.");

            TuningSettings tuning = ReadTuning(root, problems);

            // Interfaces are resolved only once the names themselves are sound,
            // so a missing name does not also produce a confusing lookup error.
            var clientInterfaces = new List<MdnsInterface>();
            MdnsInterface? printerInterface = null;

            foreach (string name in clientNames)
            {
                try
                {
                    clientInterfaces.Add(MdnsInterfaceResolver.ResolveByName(name, inventory));
                }
                catch (MdnsInterfaceException ex)
                {
                    problems.Add($"clientInterfaces: {ex.Message}");
                }
            }

            if (printerName is not null)
            {
                try
                {
                    printerInterface = MdnsInterfaceResolver.ResolveByName(printerName, inventory);
                }
                catch (MdnsInterfaceException ex)
                {
                    problems.Add($"printerInterface: {ex.Message}");
                }
            }

            CheckInterfacesAreDistinct(clientInterfaces, printerInterface, problems);

            if (problems.Count > 0)
            {
                throw new ConfigurationException(problems);
            }

            return new ServiceConfiguration(
                clientInterfaces,
                printerInterface!,
                printerInstance!,
                instanceName!,
                hostLabel!,
                uuid,
                port,
                tuning);
        }
    }

    /// <summary>
    /// The client and printer sides must be different interfaces, or an arriving
    /// datagram could not be attributed to a role and the service could not tell
    /// a client from a printer.
    /// </summary>
    [Requirement("REQ-CFG-006",
        "Refuses a configuration where a client interface and the printer interface are the same adapter.")]
    private static void CheckInterfacesAreDistinct(
        List<MdnsInterface> clientInterfaces, MdnsInterface? printerInterface, List<string> problems)
    {
        var seen = new Dictionary<int, string>();

        foreach (MdnsInterface client in clientInterfaces)
        {
            if (seen.TryGetValue(client.Index, out string? already))
            {
                problems.Add(
                    $"clientInterfaces: '{client.Name}' is listed more than once (also as '{already}').");
                continue;
            }

            seen[client.Index] = client.Name;
        }

        if (printerInterface is not null && seen.TryGetValue(printerInterface.Index, out string? clash))
        {
            problems.Add(
                $"printerInterface: '{printerInterface.Name}' is also a client interface (as '{clash}'). "
                + "The two sides must be different networks, or arriving traffic could not be attributed "
                + "to one side or the other.");
        }
    }

    private static JsonElement Section(JsonElement root, string name, List<string> problems)
    {
        if (root.TryGetProperty(name, out JsonElement section) && section.ValueKind == JsonValueKind.Object)
        {
            return section;
        }

        problems.Add($"{name}: required section is missing.");
        return default;
    }

    private static List<string> RequiredStringArray(JsonElement parent, string name, List<string> problems)
    {
        var values = new List<string>();

        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            problems.Add($"{name}: required, and has no default. List the interfaces by name, "
                         + "for example [\"Ethernet 2\"].");
            return values;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            problems.Add($"{name}: must be an array of interface names.");
            return values;
        }

        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                problems.Add($"{name}: contains an entry that is not a non-empty string.");
                continue;
            }

            values.Add(item.GetString()!);
        }

        if (values.Count == 0 && element.GetArrayLength() == 0)
        {
            problems.Add($"{name}: at least one interface is required, or the service would advertise nowhere.");
        }

        return values;
    }

    private static string? RequiredString(
        JsonElement parent, string name, List<string> problems, string prefix = "")
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return null; // The missing section was already reported.
        }

        if (!parent.TryGetProperty(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            problems.Add($"{prefix}{name}: required, and has no default.");
            return null;
        }

        return element.GetString();
    }

    private static Guid RequiredGuid(
        JsonElement parent, string name, List<string> problems, string prefix)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return Guid.Empty;
        }

        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            problems.Add(
                $"{prefix}{name}: required, and deliberately has no default. Generate one with "
                + "[guid]::NewGuid() in PowerShell. It identifies this proxy to clients and must not be "
                + "the printer's.");
            return Guid.Empty;
        }

        if (!Guid.TryParse(element.GetString(), out Guid value) || value == Guid.Empty)
        {
            problems.Add($"{prefix}{name}: '{element.GetString()}' is not a usable UUID.");
            return Guid.Empty;
        }

        return value;
    }

    private static ushort RequiredPort(
        JsonElement parent, string name, List<string> problems, string prefix)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (!parent.TryGetProperty(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out int value))
        {
            problems.Add($"{prefix}{name}: required, and has no default. IPP normally uses 631.");
            return 0;
        }

        if (value is < 1 or > 65535)
        {
            problems.Add($"{prefix}{name}: {value} is not a valid port.");
            return 0;
        }

        return (ushort)value;
    }

    /// <summary>
    /// Reads tuning values, falling back to the documented defaults. These are
    /// the only settings permitted to default, because none changes what is
    /// advertised or where traffic is sent.
    /// </summary>
    private static TuningSettings ReadTuning(JsonElement root, List<string> problems)
    {
        if (!root.TryGetProperty("tuning", out JsonElement tuning) || tuning.ValueKind != JsonValueKind.Object)
        {
            return TuningSettings.Default;
        }

        int buffer = OptionalInt(tuning, "relayBufferBytes", TuningSettings.Default.RelayBufferBytes, problems);
        int connect = OptionalInt(
            tuning, "connectTimeoutSeconds",
            (int)TuningSettings.Default.PrinterConnectTimeout.TotalSeconds, problems);
        int resolve = OptionalInt(
            tuning, "resolveTimeoutSeconds",
            (int)TuningSettings.Default.PrinterResolveTimeout.TotalSeconds, problems);

        if (buffer is < 1024 or > 1024 * 1024)
        {
            problems.Add(
                $"tuning.relayBufferBytes: {buffer} is outside 1024 - 1048576. Too small wastes syscalls; "
                + "too large defeats the point of streaming.");
        }

        if (connect is < 1 or > 300)
        {
            problems.Add($"tuning.connectTimeoutSeconds: {connect} is outside 1 - 300.");
        }

        if (resolve is < 1 or > 300)
        {
            problems.Add($"tuning.resolveTimeoutSeconds: {resolve} is outside 1 - 300.");
        }

        return new TuningSettings(
            buffer, TimeSpan.FromSeconds(connect), TimeSpan.FromSeconds(resolve));
    }

    private static int OptionalInt(JsonElement parent, string name, int fallback, List<string> problems)
    {
        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            return fallback;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            problems.Add($"tuning.{name}: must be a whole number.");
            return fallback;
        }

        return value;
    }

    /// <summary>
    /// An example configuration, used by the tests and reproduced in the
    /// documentation so the two cannot disagree.
    /// </summary>
    public static string ExampleJson => """
        {
          "clientInterfaces": [ "Ethernet 2" ],
          "printerInterface": "Wi-Fi",
          "printerInstance": "EPSON ET-3760 Series._ipp._tcp.local",

          "advertise": {
            "instanceName": "SecretPrinter (ET-3760)",
            "hostLabel": "secretprinter",
            "uuid": "b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94",
            "port": 631
          },

          "tuning": {
            "relayBufferBytes": 65536,
            "connectTimeoutSeconds": 10,
            "resolveTimeoutSeconds": 5
          }
        }
        """;

    /// <summary>Formats a problem list for a log or console, one per line.</summary>
    public static string FormatProblems(IReadOnlyList<string> problems) =>
        string.Join(
            Environment.NewLine,
            problems.Select((p, i) => $"{(i + 1).ToString(CultureInfo.InvariantCulture)}. {p}"));
}

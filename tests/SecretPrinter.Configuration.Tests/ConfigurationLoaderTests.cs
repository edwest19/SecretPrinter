// -----------------------------------------------------------------------------
// ConfigurationLoaderTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Verifies that nothing is quietly defaulted, that every mistake is named,
//   and that a configuration which would leave the service unable to tell a
//   client from a printer is refused rather than accepted.
//
//   Adapters are supplied through IInterfaceInventory, so these run anywhere.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Configuration.Tests;

internal static class ConfigurationLoaderTests
{
    /// <summary>The machine this project was developed against: two networks, one adapter each.</summary>
    private static FakeInventory RealisticMachine() => new(
        new LocalAdapter("Ethernet 2", 13, IsUp: true, SupportsMulticast: true,
            [IPAddress.Parse("192.168.1.234")]),
        new LocalAdapter("Wi-Fi", 11, IsUp: true, SupportsMulticast: true,
            [IPAddress.Parse("192.168.12.245")]),
        new LocalAdapter("Loopback", 1, IsUp: true, SupportsMulticast: false,
            [IPAddress.Parse("127.0.0.1")]));

    private static ServiceConfiguration LoadExample() =>
        ConfigurationLoader.Load(ConfigurationLoader.ExampleJson, RealisticMachine());

    private static ConfigurationException LoadExpectingFailure(string json, string because) =>
        Assert.Throws<ConfigurationException>(
            () => _ = ConfigurationLoader.Load(json, RealisticMachine()), because);

    private static bool Mentions(ConfigurationException ex, string setting) =>
        ex.Problems.Any(p => p.Contains(setting, StringComparison.OrdinalIgnoreCase));

    // ---- The happy path -----------------------------------------------------

    [TestCase("The example configuration loads and resolves its interfaces")]
    [Requirement("REQ-CFG-004")]
    public static void Example_resolves_interface_names_to_addresses()
    {
        ServiceConfiguration configuration = LoadExample();

        Assert.Equal(1, configuration.ClientInterfaces.Count, "one client interface was configured");
        Assert.Equal(IPAddress.Parse("192.168.1.234"), configuration.ClientInterfaces[0].Address,
            "the client interface name must resolve to that adapter's address");
        Assert.Equal(13, configuration.ClientInterfaces[0].Index, "and to its index");
        Assert.Equal(IPAddress.Parse("192.168.12.245"), configuration.PrinterInterface.Address,
            "the printer interface must resolve too");
    }

    [TestCase("Client and printer interfaces are separate settings")]
    [Requirement("REQ-CFG-002")]
    public static void Roles_are_configured_separately()
    {
        ServiceConfiguration configuration = LoadExample();

        Assert.True(configuration.ClientInterfaces[0].Index != configuration.PrinterInterface.Index,
            "the two roles must resolve to different adapters");
        Assert.Equal("Ethernet 2", configuration.ClientInterfaces[0].Name, "client side");
        Assert.Equal("Wi-Fi", configuration.PrinterInterface.Name, "printer side");
    }

    [TestCase("The startup description names each interface and how it resolved")]
    [Requirement("REQ-OBS-001")]
    public static void Description_shows_resolution()
    {
        IReadOnlyList<string> lines = LoadExample().Describe();

        Assert.True(lines.Any(l => l.Contains("client interface", StringComparison.Ordinal)
                                   && l.Contains("Ethernet 2", StringComparison.Ordinal)
                                   && l.Contains("192.168.1.234", StringComparison.Ordinal)),
            "the log must show which adapter a configured name actually chose");
        Assert.True(lines.Any(l => l.Contains("printer interface", StringComparison.Ordinal)
                                   && l.Contains("192.168.12.245", StringComparison.Ordinal)),
            "and the same for the printer side");
    }

    // ---- Nothing is silently defaulted --------------------------------------

    [TestCase("Every setting that decides behaviour is required")]
    [Requirement("REQ-CFG-001")]
    public static void Behavioural_settings_have_no_defaults()
    {
        // An empty object: if anything at all were defaulted, some of these
        // would be missing from the report.
        ConfigurationException ex = LoadExpectingFailure(
            "{}", "an empty configuration cannot possibly describe a network");

        foreach (string required in
            new[] { "clientInterfaces", "printerInterface", "printerInstance", "advertise" })
        {
            Assert.True(Mentions(ex, required), $"'{required}' must be reported as required");
        }
    }

    [TestCase("Tuning values may be omitted and are then documented defaults")]
    [Requirement("REQ-CFG-001")]
    public static void Tuning_may_default()
    {
        // The example is trimmed rather than retyped, so this test cannot drift
        // from the configuration the documentation actually shows.
        //
        // The trim locates the block by its key and the comma before it. An
        // earlier version searched for the literal "\n\n" preceding it, which
        // assumed the line endings of the source file that holds ExampleJson.
        // That passed on a CRLF working copy and failed on a fresh checkout
        // where .gitattributes normalises to LF - found by CI on its first run,
        // not by any local test. Nothing here should depend on how a file is
        // stored.
        string example = ConfigurationLoader.ExampleJson;

        int tuning = example.IndexOf("\"tuning\"", StringComparison.Ordinal);
        Assert.True(tuning > 0,
            "the example configuration must contain a tuning block for this test to trim");

        int comma = example.LastIndexOf(',', tuning);
        Assert.True(comma > 0,
            "the tuning block must be preceded by a comma, which is where the trim cuts");

        string json = example[..comma] + "\n}";

        ServiceConfiguration configuration = ConfigurationLoader.Load(json, RealisticMachine());

        Assert.Equal(TuningSettings.Default.RelayBufferBytes, configuration.Tuning.RelayBufferBytes,
            "tuning may default because it changes neither destination nor content");
        Assert.Equal(TuningSettings.Default.PrinterConnectTimeout, configuration.Tuning.PrinterConnectTimeout,
            "likewise the connect timeout");
    }

    [TestCase("A missing UUID is refused, with instructions")]
    [Requirement("REQ-CFG-001")]
    public static void Uuid_is_required()
    {
        string json = ConfigurationLoader.ExampleJson.Replace(
            "\"uuid\": \"b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94\",", string.Empty, StringComparison.Ordinal);

        ConfigurationException ex = LoadExpectingFailure(
            json, "a generated-by-default UUID would make every installation identical");

        Assert.True(Mentions(ex, "uuid"), "the missing setting must be named");
        Assert.True(ex.Problems.Any(p => p.Contains("NewGuid", StringComparison.Ordinal)),
            "the message should say how to produce one");
    }

    // ---- Mistakes are all reported ------------------------------------------

    [TestCase("Several mistakes are reported together, not one per run")]
    [Requirement("REQ-CFG-003")]
    public static void All_problems_reported_at_once()
    {
        string json = """
            {
              "clientInterfaces": [ "No Such Adapter" ],
              "printerInterface": "Also Missing",
              "advertise": { "port": 70000 }
            }
            """;

        ConfigurationException ex = LoadExpectingFailure(json, "this configuration is wrong in many ways");

        Assert.True(ex.Problems.Count >= 5,
            $"an operator should learn everything in one run, but only {ex.Problems.Count} problems "
            + "were reported");
        Assert.True(Mentions(ex, "printerInstance"), "the missing printer instance must be named");
        Assert.True(Mentions(ex, "instanceName"), "the missing advertised name must be named");
        Assert.True(Mentions(ex, "port"), "the impossible port must be named");
    }

    [TestCase("An unknown interface name is refused, listing what does exist")]
    [Requirement("REQ-CFG-003")]
    public static void Unknown_interface_lists_alternatives()
    {
        string json = ConfigurationLoader.ExampleJson.Replace(
            "\"Ethernet 2\"", "\"Ethernet 47\"", StringComparison.Ordinal);

        ConfigurationException ex = LoadExpectingFailure(json, "no such adapter exists");

        Assert.True(Mentions(ex, "Ethernet 47"), "the offending name must appear");
        Assert.True(Mentions(ex, "Wi-Fi"), "and the real adapters should be listed to help");
    }

    [TestCase("An adapter with two addresses is refused rather than guessed at")]
    [Requirement("REQ-CFG-003")]
    public static void Ambiguous_adapter_is_refused()
    {
        var ambiguous = new FakeInventory(
            new LocalAdapter("Ethernet 2", 13, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.234"), IPAddress.Parse("192.168.1.235")]),
            new LocalAdapter("Wi-Fi", 11, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.12.245")]));

        var ex = Assert.Throws<ConfigurationException>(
            () => _ = ConfigurationLoader.Load(ConfigurationLoader.ExampleJson, ambiguous),
            "picking one of two addresses would leave the advertised address to chance");

        Assert.True(Mentions(ex, "several IPv4"), "the ambiguity must be explained");
    }

    [TestCase("Malformed JSON is reported as such")]
    [Requirement("REQ-CFG-003")]
    public static void Malformed_json_is_reported()
    {
        ConfigurationException ex = LoadExpectingFailure(
            "{ this is not json", "a typo in the file should be obvious");

        Assert.True(Mentions(ex, "not valid JSON"), "the operator should know it is a syntax problem");
    }

    // ---- The two sides must differ ------------------------------------------

    [TestCase("Using one adapter for both roles is refused")]
    [Requirement("REQ-CFG-006")]
    public static void Same_adapter_for_both_roles_is_refused()
    {
        string json = ConfigurationLoader.ExampleJson.Replace(
            "\"printerInterface\": \"Wi-Fi\"", "\"printerInterface\": \"Ethernet 2\"",
            StringComparison.Ordinal);

        ConfigurationException ex = LoadExpectingFailure(
            json, "one adapter cannot be both sides: arriving traffic could not be attributed to a role");

        Assert.True(Mentions(ex, "also a client interface"), "the clash must be explained plainly");
    }

    [TestCase("Listing the same client interface twice is refused")]
    [Requirement("REQ-CFG-006")]
    public static void Duplicate_client_interface_is_refused()
    {
        string json = ConfigurationLoader.ExampleJson.Replace(
            "[ \"Ethernet 2\" ]", "[ \"Ethernet 2\", \"ethernet 2\" ]", StringComparison.Ordinal);

        ConfigurationException ex = LoadExpectingFailure(json, "a duplicate is a mistake, not a setup");

        Assert.True(Mentions(ex, "more than once"), "the duplication must be named");
    }

    // ---- Nothing partial ----------------------------------------------------

    [TestCase("A failed load returns nothing usable")]
    [Requirement("REQ-CFG-005")]
    public static void Failure_yields_no_configuration()
    {
        // The only way to obtain a ServiceConfiguration is a successful Load, so
        // a partially valid file cannot produce a half-usable object to start on.
        Assert.Equal(0, typeof(ServiceConfiguration).GetConstructors().Length,
            "ServiceConfiguration must have no public constructor; it may only come from a "
            + "completed, validated load");

        ConfigurationException ex = LoadExpectingFailure(
            """{ "clientInterfaces": [ "Ethernet 2" ] }""",
            "a file good in parts is still not usable");

        Assert.True(ex.Problems.Count > 0, "and the reasons must be reported");
    }

    [TestCase("An unusable interface is refused with the specific reason")]
    [Requirement("REQ-CFG-005")]
    public static void Down_interface_is_refused()
    {
        var machine = new FakeInventory(
            new LocalAdapter("Ethernet 2", 13, IsUp: false, SupportsMulticast: true,
                [IPAddress.Parse("192.168.1.234")]),
            new LocalAdapter("Wi-Fi", 11, IsUp: true, SupportsMulticast: true,
                [IPAddress.Parse("192.168.12.245")]));

        var ex = Assert.Throws<ConfigurationException>(
            () => _ = ConfigurationLoader.Load(ConfigurationLoader.ExampleJson, machine),
            "starting on a down adapter would receive nothing, silently");

        Assert.True(Mentions(ex, "not up"), "the reason must be specific");
    }
}

/// <summary>Supplies a fixed set of adapters so these tests run on any machine.</summary>
internal sealed class FakeInventory(params LocalAdapter[] adapters) : IInterfaceInventory
{
    public IReadOnlyList<LocalAdapter> Adapters { get; } = adapters;
}

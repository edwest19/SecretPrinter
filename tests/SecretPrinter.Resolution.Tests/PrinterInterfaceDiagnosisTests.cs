// -----------------------------------------------------------------------------
// PrinterInterfaceDiagnosisTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project, 2026-09-19. Reviewed by a human before
// merge.
//
// Purpose:
//   Holds the resolver to what it is entitled to say when a lookup times out.
//
//   The message these tests guard was measured false thirty-eight times across
//   2026-09-18 and 2026-09-19 - twenty-one jobs, then seventeen - while the
//   printer was awake, on the right network, and answering probes from the same
//   machine on the same interface. The tests therefore assert the absence of a
//   claim as much as the presence of one: the word "asleep" must not appear,
//   because nothing the resolver can observe would support it.
//
//   Every interface state below is supplied through IInterfaceInventory, so
//   these run on a machine with no adapters at all.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.TestKit;

namespace SecretPrinter.Resolution.Tests;

internal static class PrinterInterfaceDiagnosisTests
{
    private static readonly MdnsInterface PrinterNic =
        new("Wi-Fi 2", IPAddress.Parse("192.168.12.136"), 49, AddressFamily.InterNetwork);

    private static readonly DnsName Instance =
        new(["EPSON ET-3760 Series", "_ipps", "_tcp", "local"]);

    /// <summary>The adapter as it looks when everything is in order.</summary>
    private static LocalAdapter Healthy() => new(
        "Wi-Fi 2",
        49,
        true,
        true,
        [IPAddress.Parse("192.168.12.136")],
        null,
        [new LocalIPv4Address(IPAddress.Parse("192.168.12.136"), LocalAddressCondition.Preferred)]);

    [TestCase("An adapter that has gone is named, and the printer is not blamed")]
    public static void Missing_adapter_is_reported_as_the_local_fault()
    {
        PrinterInterfaceReport report =
            PrinterInterfaceInspector.Inspect(PrinterNic, new FakeInventory());

        Assert.Equal(PrinterInterfaceCondition.NotFound, report.Condition,
            "an index no adapter carries is a local fault, not a printer fault");
        Assert.True(report.LocalFaultFound, "the local side accounts for the failure on its own");
    }

    [TestCase("An adapter that is down is named")]
    public static void Adapter_that_is_down_is_reported_as_the_local_fault()
    {
        LocalAdapter down = Healthy() with { IsUp = false };

        PrinterInterfaceReport report =
            PrinterInterfaceInspector.Inspect(PrinterNic, new FakeInventory(down));

        Assert.Equal(PrinterInterfaceCondition.NotUp, report.Condition,
            "an adapter that is not up cannot have carried the query");
        Assert.True(report.Description.Contains("Wi-Fi 2", StringComparison.Ordinal),
            "the operator needs to be told which adapter");
    }

    [TestCase("An address that has been removed is named")]
    public static void Address_gone_is_reported_as_the_local_fault()
    {
        LocalAdapter moved = Healthy() with
        {
            IPv4Addresses = [IPAddress.Parse("169.254.111.167")],
            IPv4AddressConditions =
                [new LocalIPv4Address(IPAddress.Parse("169.254.111.167"), LocalAddressCondition.Preferred)],
        };

        PrinterInterfaceReport report =
            PrinterInterfaceInspector.Inspect(PrinterNic, new FakeInventory(moved));

        Assert.Equal(PrinterInterfaceCondition.AddressGone, report.Condition,
            "the bound address is gone, so no reply could have come back to it");
    }

    [TestCase("An address still held but deprecated is named")]
    public static void Deprecated_address_is_reported_as_the_local_fault()
    {
        // The state measured on FIOS-STB-01 at 21:45Z on 2026-09-18: the link
        // had gone, the DHCP lease had not expired, and the address stayed put.
        // Sending did not fail. Twenty-one jobs blamed the printer.
        LocalAdapter deprecated = Healthy() with
        {
            IPv4AddressConditions =
                [new LocalIPv4Address(IPAddress.Parse("192.168.12.136"), LocalAddressCondition.Deprecated)],
        };

        PrinterInterfaceReport report =
            PrinterInterfaceInspector.Inspect(PrinterNic, new FakeInventory(deprecated));

        Assert.Equal(PrinterInterfaceCondition.AddressUnusable, report.Condition,
            "an address that is held but deprecated is exactly the case that produced the false message");
    }

    [TestCase("An interface with nothing wrong is not reported as a fault")]
    public static void Healthy_interface_reports_no_local_fault()
    {
        PrinterInterfaceReport report =
            PrinterInterfaceInspector.Inspect(PrinterNic, new FakeInventory(Healthy()));

        Assert.Equal(PrinterInterfaceCondition.NothingWrongFound, report.Condition,
            "nothing checked was wrong, and the report must not invent a fault");
        Assert.False(report.LocalFaultFound, "the local side does not account for the failure here");
    }

    [TestCase("A timeout on a healthy interface does not claim the printer is asleep")]
    public static void Timeout_on_a_healthy_interface_claims_nothing_about_the_printer()
    {
        var transport = new FakeTransport(PrinterNic);
        using var resolver = new PrinterResolver(
            transport, PrinterNic, clock: null, inventory: new FakeInventory(Healthy()));

        PrinterResolutionException failure = Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(50), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "a lookup nobody answers must fail");

        Assert.False(failure.Message.Contains("asleep", StringComparison.OrdinalIgnoreCase),
            "the resolver observed nothing about the printer's power state and must not assert one");
        Assert.True(failure.Message.Contains("was not established", StringComparison.Ordinal),
            "an unexplained timeout must say it is unexplained");
        Assert.True(failure.Message.Contains("No address is assumed", StringComparison.Ordinal),
            "REQ-RES-005: no stale address may be substituted for the missing answer");
    }

    [TestCase("A timeout on an interface that is down blames the interface")]
    public static void Timeout_on_a_down_interface_names_the_interface()
    {
        LocalAdapter down = Healthy() with { IsUp = false };
        var transport = new FakeTransport(PrinterNic);
        using var resolver = new PrinterResolver(
            transport, PrinterNic, clock: null, inventory: new FakeInventory(down));

        PrinterResolutionException failure = Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(50), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "a lookup sent from an interface that is down must fail");

        Assert.True(failure.Message.Contains("is not up", StringComparison.Ordinal),
            "the operator must be pointed at the adapter, which is the thing they can fix");
        Assert.True(failure.Message.Contains("nothing is claimed about it", StringComparison.Ordinal),
            "the printer was never reached, so no claim about it is available to make");
    }
}

/// <summary>Supplies whatever set of adapters a test needs, including none.</summary>
internal sealed class FakeInventory(params LocalAdapter[] adapters) : IInterfaceInventory
{
    public IReadOnlyList<LocalAdapter> Adapters { get; } = adapters;
}

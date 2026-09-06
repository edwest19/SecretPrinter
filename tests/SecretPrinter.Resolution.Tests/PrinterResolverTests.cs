// -----------------------------------------------------------------------------
// PrinterResolverTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Purpose:
//   Verifies that the printer is found by name rather than by remembered
//   address, and that a stale answer is never returned.
//
//   The test that matters most is Printer_that_moved_is_found_at_its_new_address.
//   It reproduces what actually happened during development: the printer was at
//   192.168.12.186, then at 192.168.12.180. A resolver that kept the first
//   address would leave the printer visible in the client's list while every
//   job failed at TCP connect - the worst possible failure, because nothing
//   reports an error.
//
//   Every test runs against a fake transport and a fake clock, so none needs a
//   printer, a network, or real elapsed time.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Resolution.Tests;

internal static class PrinterResolverTests
{
    private static readonly MdnsInterface PrinterNic =
        new("Wi-Fi", IPAddress.Parse("192.168.12.245"), 11, AddressFamily.InterNetwork);

    private static readonly MdnsInterface ClientNic =
        new("Ethernet 2", IPAddress.Parse("192.168.1.234"), 13, AddressFamily.InterNetwork);

    private static readonly DnsName Instance =
        new(["EPSON ET-3760 Series", "_ipp", "_tcp", "local"]);

    /// <summary>
    /// The real printer's hostname, with the low three bytes of its MAC-derived
    /// label redacted to <c>000000</c> before publication. The shape is what
    /// matters here, not the bytes. See
    /// docs/findings/2026-09-04-pre-publication-audit.md.
    /// </summary>
    private static readonly DnsName PrinterHost = new(["EPSON000000", "local"]);

    /// <summary>Builds the reply an Epson sends: SRV, TXT and A together.</summary>
    private static MdnsDatagram PrinterReply(
        string address, uint ttl = 120, int interfaceIndex = 11, DnsName? instance = null)
    {
        DnsName target = instance ?? Instance;

        byte[] payload = new DnsResponseBuilder()
            .AddAnswer(new OutgoingRecord(target, DnsRecordType.Srv, ttl, true,
                new SrvPayload(0, 0, 631, PrinterHost)))
            .AddAnswer(new OutgoingRecord(target, DnsRecordType.Txt, 4500, true,
                new TxtPayload(["txtvers=1", "rp=ipp/print", "Color=T"])))
            .AddAdditional(new OutgoingRecord(PrinterHost, DnsRecordType.A, ttl, true,
                new AddressPayload(IPAddress.Parse(address))))
            .Build();

        return new MdnsDatagram(
            payload,
            new IPEndPoint(IPAddress.Parse(address), 5353),
            interfaceIndex,
            interfaceIndex == PrinterNic.Index ? PrinterNic : null);
    }

    /// <summary>A transport that answers each query with the given reply.</summary>
    private static FakeTransport TransportAnswering(Func<MdnsDatagram> reply)
    {
        var transport = new FakeTransport(PrinterNic, ClientNic);
        transport.OnSend = _ => transport.Enqueue(reply());
        return transport;
    }

    private static ResolvedPrinter Resolve(PrinterResolver resolver, TimeSpan? timeout = null) =>
        resolver.ResolveAsync(Instance, timeout ?? TimeSpan.FromSeconds(5), CancellationToken.None)
                .GetAwaiter().GetResult();

    // ---- Resolution ---------------------------------------------------------

    [TestCase("The printer is located by instance name")]
    [Requirement("REQ-RES-001")]
    public static void Resolves_instance_to_address()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180"));
        var resolver = new PrinterResolver(transport, PrinterNic);

        ResolvedPrinter resolved = Resolve(resolver);

        Assert.Equal(IPAddress.Parse("192.168.12.180"), resolved.Address, "the address must come from the reply");
        Assert.Equal((ushort)631, resolved.Port, "the port must come from the SRV record");
        Assert.Equal("EPSON000000.local", resolved.Host.ToString(), "the host must come from the SRV target");
    }

    [TestCase("Resolution carries the printer's TXT records out")]
    [Requirement("REQ-ADV-010")]
    public static void Resolution_returns_txt_for_the_advertisement()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180"));
        var resolver = new PrinterResolver(transport, PrinterNic);

        ResolvedPrinter resolved = Resolve(resolver);

        Assert.True(resolved.TxtStrings.Contains("rp=ipp/print"),
            "the advertisement must be built from a live observation, so the TXT must survive resolution");
    }

    [TestCase("The query goes out only on the printer-side interface")]
    [Requirement("REQ-RES-006")]
    public static void Query_uses_only_the_printer_interface()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180"));
        var resolver = new PrinterResolver(transport, PrinterNic);

        Resolve(resolver);

        Assert.True(transport.Sent.Count > 0, "a query must actually be sent");
        Assert.True(transport.Sent.All(s => s.Via.Index == PrinterNic.Index),
            "querying on the client network would leak the printer's name where it does not belong");
    }

    [TestCase("A reply arriving on the client interface is ignored")]
    [Requirement("REQ-RES-006")]
    public static void Reply_on_wrong_interface_is_ignored()
    {
        var transport = new FakeTransport(PrinterNic, ClientNic);
        bool answered = false;
        transport.OnSend = _ =>
        {
            if (answered)
            {
                return;
            }

            answered = true;

            // Something on the client network claiming to be the printer.
            transport.Enqueue(PrinterReply("10.0.0.9", interfaceIndex: ClientNic.Index));
        };

        var resolver = new PrinterResolver(transport, PrinterNic);

        Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "an answer from the wrong network must not be trusted");
    }

    [TestCase("A reply about a different instance is ignored")]
    [Requirement("REQ-RES-003")]
    public static void Reply_about_other_instance_is_ignored()
    {
        var other = new DnsName(["Some Other Printer", "_ipp", "_tcp", "local"]);
        FakeTransport transport = TransportAnswering(
            () => PrinterReply("192.168.12.99", instance: other));

        var resolver = new PrinterResolver(transport, PrinterNic);

        Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "another printer's records must not satisfy a lookup for ours");
    }

    // ---- Caching and the DHCP problem ---------------------------------------

    [TestCase("A fresh answer is reused without querying again")]
    [Requirement("REQ-RES-004")]
    public static void Fresh_answer_is_cached()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180", ttl: 120));
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var resolver = new PrinterResolver(transport, PrinterNic, clock);

        Resolve(resolver);
        int afterFirst = transport.Sent.Count;

        clock.Advance(TimeSpan.FromSeconds(60));
        Resolve(resolver);

        Assert.Equal(afterFirst, transport.Sent.Count,
            "within the TTL the cached answer should be reused, not re-queried");
    }

    [TestCase("A printer that moved is found at its new address")]
    [Requirement("REQ-RES-004")]
    public static void Printer_that_moved_is_found_at_its_new_address()
    {
        // This is the failure the whole design exists to prevent. The real
        // device was seen at .186 and later at .180 within one session.
        string current = "192.168.12.186";
        FakeTransport transport = TransportAnswering(() => PrinterReply(current, ttl: 120));
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var resolver = new PrinterResolver(transport, PrinterNic, clock);

        Assert.Equal(IPAddress.Parse("192.168.12.186"), Resolve(resolver).Address, "first lookup");

        current = "192.168.12.180";
        clock.Advance(TimeSpan.FromSeconds(121));

        Assert.Equal(IPAddress.Parse("192.168.12.180"), Resolve(resolver).Address,
            "once the TTL lapses the printer must be found where it now is, not where it was");
    }

    [TestCase("Cache expiry is bounded by the TTL that was received")]
    [Requirement("REQ-RES-004")]
    public static void Expiry_follows_the_received_ttl()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180", ttl: 30));
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var resolver = new PrinterResolver(transport, PrinterNic, clock);

        ResolvedPrinter resolved = Resolve(resolver);

        Assert.Equal(30u, resolved.Ttl, "the shortest TTL among the records used bounds the answer");
        Assert.True(resolved.IsFreshAt(clock.GetUtcNow().AddSeconds(29)), "still fresh just before expiry");
        Assert.False(resolved.IsFreshAt(clock.GetUtcNow().AddSeconds(31)), "stale just after");
    }

    // ---- Failure ------------------------------------------------------------

    [TestCase("Silence produces a specific failure, not a guess")]
    [Requirement("REQ-RES-005")]
    public static void Silence_fails_with_a_reason()
    {
        var transport = new FakeTransport(PrinterNic, ClientNic);
        var resolver = new PrinterResolver(transport, PrinterNic);

        var ex = Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "a printer that does not answer must produce an error the operator can act on");

        Assert.True(ex.Message.Contains("did not answer", StringComparison.Ordinal),
            "the message should say what failed");
        Assert.True(ex.Message.Contains("No address is assumed", StringComparison.Ordinal),
            "the message should make clear that nothing was guessed");
    }

    [TestCase("An expired answer is discarded when the printer goes silent")]
    [Requirement("REQ-RES-005")]
    public static void Expired_answer_is_not_reused_on_failure()
    {
        bool answering = true;
        var transport = new FakeTransport(PrinterNic, ClientNic);
        transport.OnSend = _ =>
        {
            if (answering)
            {
                transport.Enqueue(PrinterReply("192.168.12.180", ttl: 30));
            }
        };

        var clock = new FakeClock(DateTimeOffset.UnixEpoch);
        var resolver = new PrinterResolver(transport, PrinterNic, clock);

        Resolve(resolver);
        Assert.NotNull(resolver.Cached, "the first lookup should be cached");

        answering = false;
        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "a stale address that once worked is worse than an error: the job vanishes silently");

        Assert.Null(resolver.Cached, "the expired entry must be discarded, not held for a later fallback");
    }

    [TestCase("Invalidate forces the next lookup to query again")]
    [Requirement("REQ-RES-004")]
    public static void Invalidate_clears_the_cache()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180"));
        var resolver = new PrinterResolver(transport, PrinterNic);

        Resolve(resolver);
        int afterFirst = transport.Sent.Count;

        resolver.Invalidate();
        Resolve(resolver);

        Assert.True(transport.Sent.Count > afterFirst,
            "after invalidation the printer must be looked up afresh");
    }

    // ---- Structure ----------------------------------------------------------

    [TestCase("No public entry point accepts a printer address")]
    [Requirement("REQ-RES-002")]
    public static void No_api_accepts_a_printer_address()
    {
        // REQ-RES-002 forbids taking the printer's address from configuration.
        // That is a claim about what the type does NOT offer, so it is checked
        // structurally: if someone later adds an address parameter as a
        // convenience or a fallback, this fails.
        //
        // The one IPAddress the resolver legitimately handles is the local
        // interface it queries from, which arrives inside MdnsInterface. A bare
        // IPAddress parameter would be the printer's, and must not exist.
        var offending = new List<string>();

        foreach (System.Reflection.ConstructorInfo constructor in typeof(PrinterResolver).GetConstructors())
        {
            foreach (System.Reflection.ParameterInfo parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType == typeof(IPAddress))
                {
                    offending.Add($"constructor parameter '{parameter.Name}'");
                }
            }
        }

        foreach (System.Reflection.MethodInfo method in typeof(PrinterResolver).GetMethods(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly))
        {
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(IPAddress))
                {
                    offending.Add($"{method.Name} parameter '{parameter.Name}'");
                }
            }
        }

        Assert.Equal(0, offending.Count,
            "the printer address must only ever come from a live lookup, but found: "
            + string.Join(", ", offending));
    }

    [TestCase("Resolution is the only source of the address it returns")]
    [Requirement("REQ-RES-002")]
    public static void Address_comes_only_from_the_reply()
    {
        // Two resolvers, identical except for what the network says. If the
        // address came from anywhere but the reply, these would agree.
        FakeTransport first = TransportAnswering(() => PrinterReply("192.168.12.50"));
        FakeTransport second = TransportAnswering(() => PrinterReply("192.168.12.51"));

        using var a = new PrinterResolver(first, PrinterNic);
        using var b = new PrinterResolver(second, PrinterNic);

        Assert.Equal(IPAddress.Parse("192.168.12.50"), Resolve(a).Address, "first resolver follows its network");
        Assert.Equal(IPAddress.Parse("192.168.12.51"), Resolve(b).Address, "second follows its own");
    }

    // ---- Construction -------------------------------------------------------

    [TestCase("A resolver pointed at an interface the transport lacks is rejected")]
    [Requirement("REQ-RES-006")]
    public static void Unknown_interface_is_rejected()
    {
        var transport = new FakeTransport(ClientNic);

        Assert.Throws<ArgumentException>(
            () => _ = new PrinterResolver(transport, PrinterNic),
            "resolution would send nothing and time out, which is a confusing way to fail");
    }
}

/// <summary>A clock a test can move, so cache expiry needs no real waiting.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

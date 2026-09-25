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
// Test that a reply queued before a lookup is not taken as its answer added by
// Claude (Anthropic model, Claude Opus 5) at the direction of Edwin West,
// 2026-09-16. Reviewed by a human before merge.
//
// Every resolver in this file given a fake interface inventory, instead of the
// adapters of whatever machine runs the tests, by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-09-22. Until then
// Silence_fails_with_a_reason passed only while the machine running it had an
// adapter at index 11 that was up and held 192.168.12.245, so it failed on the
// CI runner. See
// docs/findings/2026-09-22-a-test-read-the-real-network-adapters.md.
// Reviewed by a human before merge.
//
// Tests that lookups made at the same time share one query, including when it
// goes unanswered, added by Claude (Anthropic model, Claude Opus 5.5) at the
// direction of Edwin West, 2026-09-25. See
// docs/findings/2026-09-25-two-clauses-of-req-res-008-were-never-built.md.
// Reviewed by a human before merge.
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
//   Every test runs against a fake transport, a fake clock where time matters,
//   and a fake interface inventory, so none needs a printer, a network, real
//   elapsed time, or any particular state of this machine's adapters.
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

    /// <summary>
    /// The adapters a timeout is diagnosed against: the printer-side adapter,
    /// up and holding PrinterNic's address as preferred. Supplied so that what
    /// a timeout reports depends on the test, not on the machine running it.
    /// Without it the resolver reads SystemInterfaceInventory.
    /// </summary>
    private static readonly FakeInventory LocalAdapters = new(new LocalAdapter(
        "Wi-Fi",
        11,
        true,
        true,
        [IPAddress.Parse("192.168.12.245")],
        null,
        [new LocalIPv4Address(IPAddress.Parse("192.168.12.245"), LocalAddressCondition.Preferred)]));

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
        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        ResolvedPrinter resolved = Resolve(resolver);

        Assert.True(resolved.TxtStrings.Contains("rp=ipp/print"),
            "the advertisement must be built from a live observation, so the TXT must survive resolution");
    }

    [TestCase("The query goes out only on the printer-side interface")]
    [Requirement("REQ-RES-006")]
    public static void Query_uses_only_the_printer_interface()
    {
        FakeTransport transport = TransportAnswering(() => PrinterReply("192.168.12.180"));
        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

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

        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

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

        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, clock, LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, clock, LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, clock, LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, clock, LocalAdapters);

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
        var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        Resolve(resolver);
        int afterFirst = transport.Sent.Count;

        resolver.Invalidate();
        Resolve(resolver);

        Assert.True(transport.Sent.Count > afterFirst,
            "after invalidation the printer must be looked up afresh");
    }

    // ---- Sharing one query ----------------------------------------------------

    [TestCase("Lookups made together share one query when it goes unanswered")]
    [Requirement("REQ-RES-008")]
    public static void Concurrent_lookups_that_fail_share_one_query()
    {
        // On 2026-09-18 print jobs queued behind one another's failed lookups,
        // each waiting out its own timeout in turn. Three jobs arriving together
        // must put one question on the printer network and all fail with it.
        var transport = new FakeTransport(PrinterNic, ClientNic);
        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        Task<ResolvedPrinter>[] lookups =
        [
            .. Enumerable.Range(0, 3).Select(_ =>
                resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)),
        ];

        foreach (Task<ResolvedPrinter> lookup in lookups)
        {
            Assert.Throws<PrinterResolutionException>(
                () => lookup.GetAwaiter().GetResult(),
                "every caller waiting on the unanswered query must get its failure");
        }

        Assert.Equal(1, transport.Sent.Count,
            "three lookups made together must share one query, not ask three times in turn");
    }

    [TestCase("Lookups made together share one query when it is answered")]
    [Requirement("REQ-RES-008")]
    public static void Concurrent_lookups_that_succeed_share_one_query()
    {
        var transport = new FakeTransport(PrinterNic, ClientNic);
        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        Task<ResolvedPrinter>[] lookups =
        [
            .. Enumerable.Range(0, 3).Select(_ =>
                resolver.ResolveAsync(Instance, TimeSpan.FromSeconds(5), CancellationToken.None)),
        ];

        Assert.Equal(1, transport.Sent.Count, "one question is on the network while all three wait");

        transport.Enqueue(PrinterReply("192.168.12.180"));

        foreach (Task<ResolvedPrinter> lookup in lookups)
        {
            Assert.Equal(IPAddress.Parse("192.168.12.180"), lookup.GetAwaiter().GetResult().Address,
                "every caller waiting on the query must get its answer");
        }

        Assert.Equal(1, transport.Sent.Count, "the answer must not prompt anyone to ask again");
    }

    [TestCase("A caller that stops waiting does not cancel the query others are waiting on")]
    [Requirement("REQ-RES-008")]
    public static void Caller_that_gives_up_leaves_the_shared_query_running()
    {
        var transport = new FakeTransport(PrinterNic, ClientNic);
        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);
        using var gaveUp = new CancellationTokenSource();

        Task<ResolvedPrinter> first = resolver.ResolveAsync(Instance, TimeSpan.FromSeconds(5), gaveUp.Token);
        Task<ResolvedPrinter> second = resolver.ResolveAsync(Instance, TimeSpan.FromSeconds(5), CancellationToken.None);

        gaveUp.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => first.GetAwaiter().GetResult(),
            "the caller that was cancelled stops waiting");

        transport.Enqueue(PrinterReply("192.168.12.180"));

        Assert.Equal(IPAddress.Parse("192.168.12.180"), second.GetAwaiter().GetResult().Address,
            "the caller still waiting must get the answer to the query already on the network");
        Assert.Equal(1, transport.Sent.Count,
            "one caller giving up must not end the query and make the other ask again");
    }

    [TestCase("A lookup for another instance waits for the one in flight, then asks its own question")]
    [Requirement("REQ-RES-008")]
    public static void Lookup_for_another_instance_waits_its_turn()
    {
        // The resolver listens on behalf of one query at a time, so a lookup for
        // a different instance cannot share the one in flight and must not run
        // beside it.
        var other = new DnsName(["EPSON ET-3760 Series", "_ipps", "_tcp", "local"]);
        var transport = new FakeTransport(PrinterNic, ClientNic);
        int sends = 0;
        transport.OnSend = _ =>
        {
            if (Interlocked.Increment(ref sends) == 2)
            {
                transport.Enqueue(PrinterReply("192.168.12.180", instance: other));
            }
        };

        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        Task<ResolvedPrinter> first = resolver.ResolveAsync(
            Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Task<ResolvedPrinter> second = resolver.ResolveAsync(
            other, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(1, transport.Sent.Count, "the second instance must wait while the first is being asked");

        Assert.Throws<PrinterResolutionException>(
            () => first.GetAwaiter().GetResult(),
            "the first lookup goes unanswered");

        Assert.True(second.GetAwaiter().GetResult().Instance.Equals(other),
            "the second lookup must ask its own question once the first is over, and get its own answer");
        Assert.Equal(2, transport.Sent.Count, "one question for each instance");
    }

    [TestCase("A failed lookup is not handed to the next caller")]
    [Requirement("REQ-RES-008")]
    public static void Failed_lookup_is_not_handed_to_the_next_caller()
    {
        bool answering = false;
        var transport = new FakeTransport(PrinterNic, ClientNic);
        transport.OnSend = _ =>
        {
            if (answering)
            {
                transport.Enqueue(PrinterReply("192.168.12.180"));
            }
        };

        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        Assert.Throws<PrinterResolutionException>(
            () => resolver.ResolveAsync(Instance, TimeSpan.FromMilliseconds(300), CancellationToken.None)
                          .GetAwaiter().GetResult(),
            "the first lookup goes unanswered");

        answering = true;

        Assert.Equal(IPAddress.Parse("192.168.12.180"), Resolve(resolver).Address,
            "a lookup that starts after a failure must ask again, not receive the old failure");
        Assert.Equal(2, transport.Sent.Count, "the second lookup put its own question on the network");
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

        using var a = new PrinterResolver(first, PrinterNic, inventory: LocalAdapters);
        using var b = new PrinterResolver(second, PrinterNic, inventory: LocalAdapters);

        Assert.Equal(IPAddress.Parse("192.168.12.50"), Resolve(a).Address, "first resolver follows its network");
        Assert.Equal(IPAddress.Parse("192.168.12.51"), Resolve(b).Address, "second follows its own");
    }

    // ---- Reading between lookups ---------------------------------------------

    [TestCase("A reply that arrived before the lookup began is not taken as the answer")]
    public static void Reply_that_arrived_between_lookups_is_discarded()
    {
        // Unmarked. No requirement row states this directly; it protects
        // REQ-RES-004 and REQ-RES-005 from an old reply being treated as new.
        var transport = new FakeTransport(PrinterNic, ClientNic);
        using var resolver = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters);

        // The printer at an address it has since left, answering while no
        // lookup is waiting - as it would answer another device's query.
        transport.Enqueue(PrinterReply("192.168.12.186"));

        // Wait until the resolver has read it. The resolver takes a datagram off
        // the transport and then decides, in the same step, whether a lookup is
        // waiting; the short pause covers that step after the count reaches zero.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (transport.Pending > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.Equal(0, transport.Pending, "the resolver must read its transport while no lookup is running");
        Thread.Sleep(100);

        // The printer's answer to our own query, from where it is now.
        transport.OnSend = _ => transport.Enqueue(PrinterReply("192.168.12.180"));

        Assert.Equal(IPAddress.Parse("192.168.12.180"), Resolve(resolver).Address,
            "a reply that was waiting before the query was sent must not be taken as its answer");
    }

    // ---- Construction -------------------------------------------------------

    [TestCase("A resolver pointed at an interface the transport lacks is rejected")]
    [Requirement("REQ-RES-006")]
    public static void Unknown_interface_is_rejected()
    {
        var transport = new FakeTransport(ClientNic);

        Assert.Throws<ArgumentException>(
            () => _ = new PrinterResolver(transport, PrinterNic, inventory: LocalAdapters),
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

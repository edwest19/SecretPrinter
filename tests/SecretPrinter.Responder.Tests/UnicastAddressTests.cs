// -----------------------------------------------------------------------------
// UnicastAddressTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-13, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder sends an answer by unicast only to an address on
//   the network the query arrived from (README REQ-SEC-017). Over IPv4 that is
//   the client interface's network as the relay works it out
//   (SecretPrinter.Mdns.ClientNetwork, README REQ-SEC-012). Over IPv6 it is a
//   link-local address carrying the scope of the interface the query arrived
//   on. A legacy unicast query (README REQ-ADV-017) from any other address is
//   not answered, and is counted.
//
//   Until 2026-10-13 a legacy query was answered by unicast to whatever address
//   it gave as its source, and Windows chooses the adapter a unicast datagram
//   leaves by from its routing table: the service sets the outgoing interface
//   for multicast only. A query arriving on the client adapter with a forged
//   source address on the printer network, or on no local network at all,
//   would have had its answer sent there. Measured against the code that day
//   with the fake transport below; not seen on a network. See
//   docs/findings/2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md.
//   RFC 6762 section 5.5, for a query sent by unicast: "responders SHOULD
//   check that the source address in the query packet matches the local subnet
//   for that link (or, in the case of IPv6, the source address has an on-link
//   prefix) and silently ignore the packet if not."
//
//   The tests that expect a refusal failed before the change. Those that expect
//   an answer passed before and after: they are there so that the check cannot
//   be made to pass by refusing too much.
//
// Addresses:
//   The client adapter is MdnsResponderTests.ClientNic, 192.168.1.234, whose
//   network is 192.168.1.0/24, and the querier on it is 192.168.1.41, as in
//   the other responder tests. 192.168.12.77 stands for an address on the
//   printer network, as in MdnsResponderTests. 192.168.2.41 is made up, on the
//   next /24 along. 203.0.113.9 is from RFC 5737's documentation ranges. A
//   second adapter, 10.20.30.40, and its querier, 10.200.1.2, are made up, for
//   the /8 rule. The IPv6 addresses are made up: fe80::41, and 2001:db8::41
//   from RFC 3849's documentation prefix.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class UnicastAddressTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>A client adapter on a network in 10.0.0.0/8.</summary>
    private static readonly MdnsInterface TenNic =
        new("Ethernet 4", IPAddress.Parse("10.20.30.40"), 31, AddressFamily.InterNetwork);

    /// <summary>
    /// A client adapter whose IPv6 index differs from its IPv4 one. The other
    /// adapters here have equal indexes in both families, as every adapter on
    /// this project's machines has, and so cannot tell which index a scope is
    /// compared with.
    /// </summary>
    private static readonly MdnsInterface SplitNic =
        new("Ethernet 5", IPAddress.Parse("192.0.2.50"), 41, AddressFamily.InterNetwork);

    private static readonly MdnsInterface SplitNicV6 =
        new("Ethernet 5", IPAddress.Parse("192.0.2.50"), 42, AddressFamily.InterNetworkV6);

    private static readonly DnsName Subtype = DnsName.Parse("_universal._sub._ipp._tcp.local");

    /// <summary>A port other than 5353, which makes a querier a legacy one (RFC 6762 s6.7).</summary>
    private const int LegacyPort = 50000;

    private static (MdnsResponder Responder, FakeTransport Transport) Build()
    {
        var transport = new FakeTransport(ClientNic, ClientNicV6, TenNic, SplitNic, SplitNicV6);
        var responder = new MdnsResponder(
            transport,
            [
                new AdvertisedInterface(ClientNic, MdnsResponderTests.BuildAdvertisement(), ClientNicV6),
                new AdvertisedInterface(TenNic, MdnsResponderTests.BuildAdvertisement()),
                new AdvertisedInterface(SplitNic, MdnsResponderTests.BuildAdvertisement(), SplitNicV6),
            ]);
        return (responder, transport);
    }

    /// <summary>A query for the AirPrint subtype, from this address and port, arriving on this interface.</summary>
    private static MdnsDatagram From(string source, MdnsInterface arrivedOn, int port = LegacyPort, string name = "_universal._sub._ipp._tcp.local")
    {
        byte[] payload = new DnsQueryBuilder(0x1234)
            .AddQuestion(DnsName.Parse(name), DnsRecordType.Ptr, requestUnicastResponse: false)
            .Build();
        return new MdnsDatagram(payload, new IPEndPoint(IPAddress.Parse(source), port), arrivedOn.Index, arrivedOn);
    }

    private static bool Handle(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>Asserts that nothing was sent and the query was counted as refused, and as nothing else.</summary>
    private static void AssertRefused(MdnsResponder responder, FakeTransport transport, bool answered, string why)
    {
        Assert.False(answered, why);
        Assert.Equal(0, transport.Sent.Count, "nothing is sent, by unicast or by multicast");

        ResponderActivity activity = responder.Activity;
        Assert.Equal(1, activity.QueriesSeen, "the query was seen");
        Assert.Equal(0, activity.QueriesAnswered, "and not answered");
        Assert.Equal(1, activity.RefusedOffNetwork, "it is counted as refused for its address");
        Assert.Equal(0, activity.IgnoredNotOurs, "not as a question about another name");
        Assert.Equal(0, activity.SuppressedByKnownAnswers, "nor as one that carried its answer");
        Assert.Equal(0, activity.HeldBackWithinASecond, "nor as one held back by the one-second rule");
    }

    // ---- IPv4 ----------------------------------------------------------------

    [TestCase("A legacy querier on the client network is answered by unicast, to its own address")]
    [Requirement("REQ-SEC-017")]
    public static void A_legacy_querier_on_the_client_network_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("192.168.1.41", ClientNic));

        Assert.True(answered, "192.168.1.41 is on 192.168.1.0/24, the client adapter's network");
        SentDatagram sent = transport.Sent.Single();
        Assert.False(sent.WasMulticast, "a legacy querier is answered by unicast (RFC 6762 s6.7)");
        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.1.41"), LegacyPort), sent.Destination,
            "to the address and port it asked from");
        Assert.True(sent.Via.Matches(ClientNic), "through the adapter it arrived on");
        Assert.Equal(0, responder.Activity.RefusedOffNetwork, "nothing refused");
    }

    [TestCase("A legacy question from an address on the printer network is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void A_legacy_question_from_the_printer_network_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("192.168.12.77", ClientNic));

        AssertRefused(responder, transport, answered,
            "an answer sent there would leave by whichever adapter Windows routes it to, the printer side among them");
    }

    [TestCase("A legacy question from an address on no local network is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void A_legacy_question_from_no_local_network_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("203.0.113.9", ClientNic));

        AssertRefused(responder, transport, answered,
            "an answer sent there would go through a router, off both networks (REQ-SEC-008)");
    }

    [TestCase("The next /24 along is another network: a legacy question from it is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void A_legacy_question_from_the_next_network_along_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("192.168.2.41", ClientNic));

        AssertRefused(responder, transport, answered,
            "192.168.2.41 is outside 192.168.1.0/24, the network the relay permits too (REQ-SEC-012)");
    }

    [TestCase("A client adapter in 10.0.0.0/8 has the whole /8 as its network, as the relay has")]
    [Requirement("REQ-SEC-017")]
    public static void A_client_adapter_in_ten_has_the_whole_slash_eight()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("10.200.1.2", TenNic));

        Assert.True(answered, "10.200.1.2 is on 10.0.0.0/8, the network worked out for 10.20.30.40");
        SentDatagram sent = transport.Sent.Single();
        Assert.Equal(new IPEndPoint(IPAddress.Parse("10.200.1.2"), LegacyPort), sent.Destination, "by unicast, to it");
        Assert.True(sent.Via.Matches(TenNic), "through the adapter it arrived on");
    }

    [TestCase("Each adapter has its own network: an address on one is refused when the query arrives on the other")]
    [Requirement("REQ-SEC-017")]
    public static void An_address_on_one_adapters_network_is_refused_on_the_other()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("192.168.1.41", TenNic));

        AssertRefused(responder, transport, answered,
            "192.168.1.41 is on the other adapter's network, not on the one the query arrived on");
    }

    // ---- IPv6 ----------------------------------------------------------------

    [TestCase("Over IPv6, a legacy querier at a link-local address on the arrival interface's scope is answered by unicast")]
    [Requirement("REQ-SEC-017")]
    public static void Over_ipv6_a_link_local_querier_on_the_arrival_scope_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From($"fe80::41%{ClientNicV6.Index}", ClientNicV6));

        Assert.True(answered, "a link-local address with the scope of the interface the query arrived on");
        SentDatagram sent = transport.Sent.Single();
        Assert.False(sent.WasMulticast, "by unicast");
        Assert.Equal(IPAddress.Parse($"fe80::41%{ClientNicV6.Index}"), sent.Destination!.Address, "to that address");
        Assert.Equal((long)ClientNicV6.Index, sent.Destination.Address.ScopeId, "with that scope");
        Assert.True(sent.Via.Matches(ClientNicV6), "over IPv6, through the adapter it arrived on");
    }

    [TestCase("Over IPv6, a link-local address on another scope is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void Over_ipv6_a_link_local_address_on_another_scope_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("fe80::41%7", ClientNicV6));

        AssertRefused(responder, transport, answered,
            "a link-local address names a host only together with its scope, and scope 7 is another link");
    }

    [TestCase("Over IPv6, a link-local address with no scope is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void Over_ipv6_a_link_local_address_with_no_scope_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("fe80::41", ClientNicV6));

        AssertRefused(responder, transport, answered,
            "with no scope, which link the address is on cannot be told, so it is not taken to be this one");
    }

    [TestCase("Over IPv6, an address that is not link-local is not answered")]
    [Requirement("REQ-SEC-017")]
    public static void Over_ipv6_an_address_that_is_not_link_local_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        // Given the arrival interface's scope, so that only its not being
        // link-local can refuse it.
        bool answered = Handle(responder, From($"2001:db8::41%{ClientNicV6.Index}", ClientNicV6));

        AssertRefused(responder, transport, answered,
            "over IPv6 the service deals only with link-local addresses on the client interface (REQ-ADV-021, REQ-SEC-012)");
    }

    [TestCase("Over IPv6, the scope is compared with the interface's IPv6 index, not its IPv4 one")]
    [Requirement("REQ-SEC-017")]
    public static void Over_ipv6_the_scope_is_compared_with_the_ipv6_index()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool withIPv4Index = Handle(responder, From($"fe80::41%{SplitNic.Index}", SplitNicV6));
        bool withIPv6Index = Handle(responder, From($"fe80::41%{SplitNicV6.Index}", SplitNicV6));

        Assert.False(withIPv4Index, "41 is the adapter's IPv4 index; as a scope it names another IPv6 interface");
        Assert.True(withIPv6Index, "42 is its IPv6 index, the scope a querier on it has");
        SentDatagram sent = transport.Sent.Single();
        Assert.True(sent.Via.Matches(SplitNicV6), "answered by unicast over IPv6 through that adapter");
        Assert.Equal(1, responder.Activity.RefusedOffNetwork, "the first was refused");
    }

    // ---- What the check does not touch ---------------------------------------

    [TestCase("A multicast question from an address on no local network is still answered, by multicast")]
    public static void A_multicast_question_from_any_address_is_answered_by_multicast()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("203.0.113.9", ClientNic, port: 5353));

        Assert.True(answered, "an answer by multicast goes to the group on the link the query arrived on, whatever the source");
        Assert.True(transport.Sent.Single().WasMulticast, "by multicast");
        Assert.Equal(0, responder.Activity.RefusedOffNetwork, "nothing refused");
    }

    [TestCase("A legacy question from off the network about another service is counted as about another service")]
    public static void A_legacy_question_about_another_service_is_counted_as_that()
    {
        (MdnsResponder responder, FakeTransport transport) = Build();

        bool answered = Handle(responder, From("203.0.113.9", ClientNic, name: "_airplay._tcp.local"));

        Assert.False(answered, "the service holds nothing at that name");
        Assert.Equal(0, transport.Sent.Count, "nothing sent");
        Assert.Equal(1, responder.Activity.IgnoredNotOurs, "counted as a question about another name");
        Assert.Equal(0, responder.Activity.RefusedOffNetwork,
            "a query is counted as refused only when there was an answer to refuse");
    }

    // ---- The stop summary ----------------------------------------------------

    [TestCase("The stop summary's first line counts the refused queries apart from the others")]
    [Requirement("REQ-SEC-017")]
    public static void The_stop_summary_counts_refused_queries()
    {
        (MdnsResponder responder, _) = Build();

        // One answered, two about another service, three refused: three
        // different counts, so a line that gave one in place of another would
        // read differently.
        Handle(responder, From("192.168.1.41", ClientNic));
        for (int i = 0; i < 2; i++)
        {
            Handle(responder, From("192.168.1.41", ClientNic, name: "_airplay._tcp.local"));
        }

        for (int i = 0; i < 3; i++)
        {
            Handle(responder, From("203.0.113.9", ClientNic));
        }

        Assert.Equal(
            "Served 1 quer(ies) of 6 seen; 2 were for other services and were ignored; "
            + "0 carried every answer already and were not answered (RFC 6762 s7.1); "
            + "0 would have repeated records multicast less than a second before and were not answered (RFC 6762 s6); "
            + "3 would have been answered by unicast to an address off the network they arrived from and were not answered (RFC 6762 s5.5).",
            responder.Activity.DescribeServed(),
            "each count where it belongs");
    }
}

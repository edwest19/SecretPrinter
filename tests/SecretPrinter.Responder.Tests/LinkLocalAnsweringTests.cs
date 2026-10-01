// -----------------------------------------------------------------------------
// LinkLocalAnsweringTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-30, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks what the responder does with an advertisement that carries AAAA
//   records: the client interface's link-local addresses, which Edwin decided
//   on 2026-09-30 are the only IPv6 addresses the service may publish
//   (docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md).
//
//   The host here has two link-local addresses on purpose. One is the usual
//   case, but with one, a responder that kept a single record per name and
//   type would pass every test below; with two, it cannot.
//
// Why these carry no [Requirement] marker:
//   REQ-ADV-021 decides which IPv6 addresses are published and that the relay
//   listens on them. That is decided and marked in MdnsInterfaceResolver,
//   AdvertisementBuilder, ListenPlan and ServiceHost. These tests check
//   something else: that the responder sends whatever address records the
//   advertisement holds, all of them, paired as RFC 6762 asks. (Updated
//   2026-09-30, by Claude, Claude Opus 5.5, when the service began publishing
//   AAAA records; until then the reason given here was that it did not.)
//
// The test that said "No record appears twice in one response, though several
// questions lead to it" renamed by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-10-01, to say what it checks: two PTR
// questions leading to the same records. It never covered a record that
// answers a later question being sent as an additional of an earlier one,
// which happened; that case is now MdnsResponderTests.
// An_answer_is_never_also_an_additional. See
// docs/findings/2026-10-01-a-record-was-sent-as-an-answer-and-an-additional.md.
// Reviewed by a human before merge.
//
// Addresses:
//   fe80::10 and fe80::20 are made up, scoped to 13, the IPv6 index of the test
//   adapter. fe80::99 stands for another device. No real address appears here.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class LinkLocalAnsweringTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    private static readonly IPAddress LinkLocal1 = IPAddress.Parse("fe80::10%13");
    private static readonly IPAddress LinkLocal2 = IPAddress.Parse("fe80::20%13");

    private static (MdnsResponder Responder, FakeTransport Transport, Advertisement Advertisement) Build(
        params IPAddress[] linkLocal)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement(linkLocal);
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)]);
        return (responder, transport, advertisement);
    }

    /// <summary>The proxy's host name, as the advertisement holds it.</summary>
    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    /// <summary>A multicast query from a client on the client network.</summary>
    private static MdnsDatagram Query(params (DnsName Name, DnsRecordType Type)[] questions)
    {
        var builder = new DnsQueryBuilder(0);
        foreach ((DnsName name, DnsRecordType type) in questions)
        {
            builder.AddQuestion(name, type, requestUnicastResponse: false);
        }

        return new MdnsDatagram(
            builder.Build(), new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), ClientNic.Index, ClientNic);
    }

    /// <summary>Handles a query and returns the response the responder sent.</summary>
    private static DnsMessage Answer(MdnsResponder responder, FakeTransport transport, MdnsDatagram query)
    {
        bool answered = responder.HandleAsync(query, CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(answered, "the query is about the proxy's own records, so it is answered");
        return transport.Sent[^1].Parsed;
    }

    /// <summary>The addresses of the records of one type, in the order they appear.</summary>
    private static List<IPAddress> Addresses(IEnumerable<DnsRecord> records, DnsRecordType type) =>
        [.. records.Where(r => r.Type == type).Select(r => r.Address!)];

    /// <summary>An address as it reads back from the wire, which carries no scope.</summary>
    private static IPAddress OnTheWire(IPAddress address) => new(address.GetAddressBytes());

    /// <summary>A response from another device, carrying the given records.</summary>
    private static MdnsDatagram ResponseFrom(string source, params OutgoingRecord[] records)
    {
        var builder = new DnsResponseBuilder();
        foreach (OutgoingRecord record in records)
        {
            builder.AddAnswer(record);
        }

        return new MdnsDatagram(
            builder.Build(), new IPEndPoint(IPAddress.Parse(source), 5353), ClientNic.Index, ClientNic);
    }

    // ---- Answering ----------------------------------------------------------

    [TestCase("An AAAA query for the host is answered with every AAAA record, and the A record as an additional")]
    public static void Aaaa_query_is_answered_with_every_aaaa_record()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal1, LinkLocal2);

        DnsMessage reply = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.Aaaa)));

        Assert.Equal(2, Addresses(reply.Answers, DnsRecordType.Aaaa).Count,
            "both AAAA records are answered; one per name and type would drop the second");
        Assert.Equal(OnTheWire(LinkLocal1), Addresses(reply.Answers, DnsRecordType.Aaaa)[0], "the first address");
        Assert.Equal(OnTheWire(LinkLocal2), Addresses(reply.Answers, DnsRecordType.Aaaa)[1], "the second address");
        Assert.Equal(0, Addresses(reply.Answers, DnsRecordType.A).Count, "an AAAA question is not answered with A");
        Assert.Equal(ClientNic.Address, Addresses(reply.Additionals, DnsRecordType.A).Single(),
            "RFC 6762 section 6.2: the other address type goes in the Additional section");
    }

    [TestCase("An A query for the host is answered with the A record, and every AAAA record as an additional")]
    public static void A_query_carries_every_aaaa_record_as_additional()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal1, LinkLocal2);

        DnsMessage reply = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.A)));

        Assert.Equal(ClientNic.Address, Addresses(reply.Answers, DnsRecordType.A).Single(), "the A record answers");
        Assert.Equal(2, Addresses(reply.Additionals, DnsRecordType.Aaaa).Count,
            "RFC 6762 section 6.2: both AAAA records go in the Additional section");
    }

    [TestCase("An ANY query for the host is answered with the A record and every AAAA record")]
    public static void Any_query_is_answered_with_every_address_record()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(LinkLocal1, LinkLocal2);

        DnsMessage reply = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.Any)));

        Assert.Equal(1, Addresses(reply.Answers, DnsRecordType.A).Count, "the A record");
        Assert.Equal(2, Addresses(reply.Answers, DnsRecordType.Aaaa).Count, "and both AAAA records");
        Assert.Equal(0, reply.Additionals.Count,
            "everything at the name is already an answer, so nothing is repeated as an additional");
    }

    [TestCase("A PTR answer carries every AAAA record as an additional, beside the A record")]
    public static void Ptr_answer_carries_every_aaaa_record()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal1, LinkLocal2);

        DnsMessage reply = Answer(responder, transport, Query((DnsName.Parse("_ipp._tcp.local"), DnsRecordType.Ptr)));

        Assert.Equal(1, Addresses(reply.Additionals, DnsRecordType.A).Count, "the A record the SRV leads to");
        Assert.Equal(2, Addresses(reply.Additionals, DnsRecordType.Aaaa).Count, "and both AAAA records");
    }

    [TestCase("Two PTR questions that lead to the same records send each of them once")]
    public static void No_record_is_sent_twice()
    {
        // Two PTR questions in one query both lead to the same SRV, TXT, A and
        // AAAA records. Each must appear once: identical records are the same
        // record, while two AAAA records with different addresses are two.
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal1, LinkLocal2);

        DnsMessage reply = Answer(responder, transport, Query(
            (DnsName.Parse("_ipp._tcp.local"), DnsRecordType.Ptr),
            (DnsName.Parse("_universal._sub._ipp._tcp.local"), DnsRecordType.Ptr)));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DnsRecord record in reply.Answers.Concat(reply.Additionals))
        {
            string identity = $"{record.Name}|{record.Type}|{record.Address}|{record.PtrTarget}|{record.SrvTarget}";
            Assert.True(seen.Add(identity), $"{record.Name} {record.Type} appears once");
        }

        Assert.Equal(2, Addresses(reply.Additionals, DnsRecordType.Aaaa).Count, "both AAAA records, once each");
    }

    [TestCase("With no link-local address, an A answer carries no AAAA record")]
    public static void Without_link_local_addresses_no_aaaa_is_sent()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        DnsMessage reply = Answer(responder, transport, Query((Host(advertisement), DnsRecordType.A)));

        Assert.Equal(0, reply.AllRecords.Count(r => r.Type == DnsRecordType.Aaaa),
            "nothing is invented: an advertisement without AAAA records sends none");
    }

    // ---- Announcing, retracting, probing ------------------------------------

    [TestCase("Every announcement carries every AAAA record")]
    public static void Announcements_carry_every_aaaa_record()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal1, LinkLocal2);

        responder.AnnounceAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(transport.Sent.Count > 0, "announcements went out");
        foreach (SentDatagram sent in transport.Sent)
        {
            Assert.Equal(2, Addresses(sent.Parsed.Answers, DnsRecordType.Aaaa).Count,
                "each announcement carries both AAAA records");
        }
    }

    [TestCase("The goodbye retracts every AAAA record with TTL zero")]
    public static void Goodbye_retracts_every_aaaa_record()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal1, LinkLocal2);

        responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();

        List<DnsRecord> aaaa = [.. transport.Sent.Single().Parsed.Answers.Where(r => r.Type == DnsRecordType.Aaaa)];
        Assert.Equal(2, aaaa.Count, "both AAAA records are retracted");
        Assert.True(aaaa.All(r => r.Ttl == 0), "with TTL zero");
    }

    [TestCase("A probe proposes every AAAA record")]
    public static void Probe_proposes_every_aaaa_record()
    {
        (MdnsResponder responder, FakeTransport transport, _) = Build(LinkLocal1, LinkLocal2);

        responder.ProbeAsync(static (_, _) => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(transport.Sent.Count > 0, "probes went out");
        foreach (SentDatagram sent in transport.Sent)
        {
            Assert.Equal(2, Addresses(sent.Parsed.Authorities, DnsRecordType.Aaaa).Count,
                "the host's addresses are claimed like its other records (RFC 6762 section 8.1)");
        }
    }

    // ---- Conflicts ----------------------------------------------------------

    [TestCase("The responder's own AAAA records, heard back, are not a conflict")]
    public static void Own_aaaa_records_heard_back_are_not_a_conflict()
    {
        (MdnsResponder responder, _, Advertisement advertisement) = Build(LinkLocal1, LinkLocal2);

        OutgoingRecord[] ours = [.. advertisement.Records.Where(r => r.Type is DnsRecordType.A or DnsRecordType.Aaaa)];
        responder.HandleAsync(ResponseFrom("192.168.1.234", ours), CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(responder.Conflict is null, "identical records are not a conflict (RFC 6762 section 9)");
    }

    [TestCase("Another device's AAAA for the host, beside one of ours, is a conflict")]
    public static void Foreign_aaaa_for_the_host_is_a_conflict()
    {
        // The foreign record comes after one of the responder's own, so a check
        // that settled for "some AAAA here matches ours" would miss it.
        (MdnsResponder responder, _, Advertisement advertisement) = Build(LinkLocal1, LinkLocal2);
        DnsName host = Host(advertisement);
        OutgoingRecord ours = advertisement.Records.First(r => r.Type == DnsRecordType.Aaaa);
        var theirs = new OutgoingRecord(
            host, DnsRecordType.Aaaa, 120, CacheFlush: true, new AddressPayload(IPAddress.Parse("fe80::99")));

        responder.HandleAsync(ResponseFrom("192.168.1.77", ours, theirs), CancellationToken.None).GetAwaiter().GetResult();

        Assert.True(responder.Conflict is { Type: DnsRecordType.Aaaa },
            "an address for this host that the responder does not hold is somebody else's claim on the name");
    }
}

// -----------------------------------------------------------------------------
// KnownAnswerTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-10, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder does not answer with a record the query already
//   carries (README REQ-ADV-026; RFC 6762 section 7.1, known-answer
//   suppression). A querier lists in its Answer section the answers it already
//   holds. RFC 6762 section 7.1: "A Multicast DNS responder MUST NOT answer a
//   Multicast DNS query if the answer it would give is already included in the
//   Answer Section with an RR TTL at least half the correct value." and, below
//   half, "the responder MUST send an answer". Until 2026-10-10 the responder
//   never read a query's Answer section and answered regardless
//   (docs/findings/2026-10-07-four-things-rfc-6762-asks-of-a-responder-are-not-done.md,
//   item 1).
//
//   Seven of these tests expect a record to be left out, and failed before the
//   change. Five expect a record to be sent although something like it was
//   listed, and passed before and after: they are there so that the rule
//   cannot be made to pass by leaving out too much. The last reads the stop
//   summary's first line, which gained a count with the change; it was added
//   after a mutation of that line was caught by no test.
//
// How a query carrying known answers is made:
//   DnsQueryBuilder writes questions and an Authority section only, because
//   the service never sends known answers. QueryCarrying builds the message
//   with DnsResponseBuilder instead, which writes questions and an Answer
//   section, and then clears the response and authoritative bits in the
//   header, so the message is a query. It parses the result with the real
//   parser before returning and checks that it reads as a query with those
//   questions and those answers, so a helper that wrote the wrong bytes fails
//   the test instead of quietly testing something else. The same way was used
//   to measure the responder on 2026-10-07.
//
// Known answers are written without the cache-flush bit, which RFC 6762
// section 10.2 says MUST NOT be set in a Known-Answer list, except in the one
// test about that bit.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class KnownAnswerTests
{
    private const ushort ClassIn = 1;
    private const ushort ClassChaos = 3;

    /// <summary>A record type this repository does not decode, so the reader keeps its data as bytes.</summary>
    private const DnsRecordType UndecodedType = (DnsRecordType)99;

    /// <summary>HTTPS (RFC 9460), a type the service publishes at no name, so its question is answered with an NSEC.</summary>
    private const DnsRecordType Https = (DnsRecordType)65;

    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    private static readonly DnsName ServiceType = DnsName.Parse("_ipp._tcp.local");
    private static readonly DnsName Subtype = DnsName.Parse("_universal._sub._ipp._tcp.local");

    private static (MdnsResponder Responder, FakeTransport Transport, Advertisement Advertisement) Build(
        params IPAddress[] linkLocal)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement(linkLocal);
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)]);
        return (responder, transport, advertisement);
    }

    /// <summary>The service's own record of this type at this name.</summary>
    private static OutgoingRecord Ours(Advertisement advertisement, DnsName name, DnsRecordType type) =>
        advertisement.Records.Single(r => r.Name.Equals(name) && r.Type == type);

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    /// <summary>
    /// A record as a querier lists it among its known answers: with the TTL it
    /// has left, and without the cache-flush bit (RFC 6762 section 10.2).
    /// </summary>
    private static OutgoingRecord Known(OutgoingRecord record, uint ttlLeft) =>
        record with { Ttl = ttlLeft, CacheFlush = false };

    /// <summary>
    /// A query asking these questions, in class IN, and carrying these records
    /// in its Answer section. One known answer's class field can be set to
    /// another value.
    /// </summary>
    private static byte[] QueryCarrying(
        (DnsName Name, DnsRecordType Type)[] questions,
        OutgoingRecord[] known,
        (int Index, ushort RawClass)? classOf = null)
    {
        byte[] message = Write(questions, known);

        // A query, not a response: the response bit and the authoritative bit
        // cleared. Nothing else in the header is set by the builder.
        message[2] = 0;
        message[3] = 0;

        if (classOf is { } change)
        {
            // Where the known answer begins is the length of the same message
            // without it and those after it. Its name runs up to its type and
            // class; the rest of the record is the type, class, TTL and data
            // length (10 bytes) and the data.
            int start = Write(questions, known[..change.Index]).Length;
            OutgoingRecord record = known[change.Index];
            int recordLength = new DnsResponseBuilder().AddAnswer(record).Build().Length - 12;
            int nameLength = recordLength - 10 - DnsRecordWriter.EncodeRdata(record.Type, record.Payload).Length;
            int classAt = start + nameLength + 2;

            message[classAt] = (byte)(change.RawClass >> 8);
            message[classAt + 1] = (byte)(change.RawClass & 0xFF);
        }

        DnsMessage parsed = DnsMessage.Parse(message, message.Length);
        Assert.False(parsed.IsResponse, "the helper made a query");
        Assert.Equal(questions.Length, parsed.Questions.Count, "with the questions given");
        Assert.Equal(known.Length, parsed.Answers.Count, "carrying the known answers given");
        if (classOf is { } check)
        {
            Assert.Equal(check.RawClass, parsed.Answers[check.Index].RawClass,
                "the helper wrote the class where the parser reads it");
        }

        return message;

        static byte[] Write((DnsName Name, DnsRecordType Type)[] questions, OutgoingRecord[] known)
        {
            var builder = new DnsResponseBuilder(0);
            foreach ((DnsName name, DnsRecordType type) in questions)
            {
                builder.AddQuestion(name, type, ClassIn);
            }

            foreach (OutgoingRecord record in known)
            {
                builder.AddAnswer(record);
            }

            return builder.Build();
        }
    }

    /// <summary>A datagram from a client on the client network, received over IPv4.</summary>
    private static MdnsDatagram From(byte[] payload, int sourcePort = 5353) =>
        new(payload, new IPEndPoint(IPAddress.Parse("192.168.1.41"), sourcePort), ClientNic.Index, ClientNic);

    private static bool Handle(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    // ---- Left out ---------------------------------------------------------------

    [TestCase("A question that carries the service's own PTR with its full TTL is not answered, and is counted apart from queries for other names")]
    [Requirement("REQ-ADV-026")]
    public static void Ptr_listed_with_its_full_ttl_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord ptr = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        bool answered = Handle(responder, From(QueryCarrying(
            [(Subtype, DnsRecordType.Ptr)], [Known(ptr, ptr.Ttl)])));

        Assert.False(answered, "RFC 6762 section 7.1: the querier already holds the only answer");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent");
        Assert.Equal(1, responder.Activity.QueriesSeen, "the query is seen");
        Assert.Equal(0, responder.Activity.QueriesAnswered, "and not answered");
        Assert.Equal(1, responder.Activity.SuppressedByKnownAnswers,
            "it is counted as left unanswered because every answer was known");
        Assert.Equal(0, responder.Activity.IgnoredNotOurs,
            "and not as a query for another service, which it was not");
    }

    [TestCase("A known answer whose TTL is exactly half the record's own still keeps it out of the answer")]
    [Requirement("REQ-ADV-026")]
    public static void Ptr_listed_with_exactly_half_its_ttl_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord ptr = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        bool answered = Handle(responder, From(QueryCarrying(
            [(Subtype, DnsRecordType.Ptr)], [Known(ptr, ptr.Ttl / 2)])));

        Assert.Equal(4500u, ptr.Ttl, "the PTR is published with 4,500 s, so half is 2,250 s");
        Assert.False(answered, "\"at least half the correct value\" includes exactly half");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent");
    }

    [TestCase("The cache-flush bit on a known answer is not read as part of its class")]
    [Requirement("REQ-ADV-026")]
    public static void Cache_flush_bit_on_a_known_answer_is_not_part_of_its_class()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);
        OutgoingRecord a = Ours(advertisement, host, DnsRecordType.A);

        // As published: the cache-flush bit set, which a querier MUST NOT do in
        // a Known-Answer list (RFC 6762 section 10.2). Its class is still IN.
        Assert.True(a.CacheFlush, "the A record is published with the cache-flush bit");
        bool answered = Handle(responder, From(QueryCarrying([(host, DnsRecordType.A)], [a])));

        Assert.False(answered, "the known answer is the same record in class IN, so it is not sent");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent");
    }

    [TestCase("A question answered with an NSEC is not answered when the query carries that NSEC")]
    [Requirement("REQ-ADV-026")]
    public static void Nsec_listed_as_a_known_answer_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);
        OutgoingRecord nsec = MdnsResponder.NsecRecordsFor(advertisement).Single(n => n.Name.Equals(host));

        // Without the known answer, the same question is answered with the NSEC.
        Assert.True(Handle(responder, From(QueryCarrying([(host, Https)], []))),
            "an HTTPS question for the host name is answered (REQ-ADV-022)");
        Assert.Equal(DnsRecordType.Nsec, transport.Sent[0].Parsed.Answers.Single().Type, "with its NSEC");

        bool answered = Handle(responder, From(QueryCarrying([(host, Https)], [Known(nsec, nsec.Ttl)])));

        Assert.False(answered, "the querier already holds the NSEC");
        Assert.Equal(1, transport.Sent.Count, "nothing more is sent");
    }

    [TestCase("Of two questions, the one whose answer is known is left out, and only the answer sent brings additionals")]
    [Requirement("REQ-ADV-026")]
    public static void Only_the_known_answer_is_left_out()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);
        OutgoingRecord ptr = Ours(advertisement, ServiceType, DnsRecordType.Ptr);

        bool answered = Handle(responder, From(QueryCarrying(
            [(ServiceType, DnsRecordType.Ptr), (host, DnsRecordType.A)], [Known(ptr, ptr.Ttl)])));

        Assert.True(answered, "the A question is answered");
        DnsMessage reply = transport.Sent.Single().Parsed;
        Assert.Equal(1, reply.Answers.Count, "with one answer");
        Assert.Equal(DnsRecordType.A, reply.Answers[0].Type, "the host's A record");
        Assert.False(reply.AllRecords.Any(r => r.Type == DnsRecordType.Ptr), "the known PTR is not sent");
        Assert.False(reply.AllRecords.Any(r => r.Type is DnsRecordType.Srv or DnsRecordType.Txt),
            "nor the SRV and TXT records that follow a PTR answer, because the PTR was not an answer");
        Assert.Equal(1, responder.Activity.QueriesAnswered, "the query is counted as answered");
        Assert.Equal(0, responder.Activity.SuppressedByKnownAnswers, "and not as left unanswered");
    }

    [TestCase("Of the host's two AAAA records, the one the query carries is left out and the other is sent")]
    [Requirement("REQ-ADV-026")]
    public static void Only_the_known_aaaa_record_of_two_is_left_out()
    {
        // Made up, and scoped to 13, the IPv6 index of the test adapter, as in
        // LinkLocalAnsweringTests. An address read off the wire has no scope,
        // so the checks below compare the address without it.
        IPAddress first = IPAddress.Parse("fe80::10%13");
        IPAddress second = IPAddress.Parse("fe80::20%13");
        var firstOnWire = new IPAddress(first.GetAddressBytes());
        var secondOnWire = new IPAddress(second.GetAddressBytes());
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(first, second);
        DnsName host = Host(advertisement);
        OutgoingRecord knownAaaa = advertisement.Records.Single(
            r => r.Type == DnsRecordType.Aaaa && ((AddressPayload)r.Payload).Address.Equals(first));

        bool answered = Handle(responder, From(QueryCarrying(
            [(host, DnsRecordType.Aaaa)], [Known(knownAaaa, knownAaaa.Ttl)])));

        Assert.True(answered, "the AAAA record the querier does not hold is sent");
        DnsMessage reply = transport.Sent.Single().Parsed;
        Assert.Equal(1, reply.Answers.Count(r => r.Type == DnsRecordType.Aaaa), "one AAAA record is sent as an answer");
        Assert.Equal(secondOnWire, reply.Answers.Single(r => r.Type == DnsRecordType.Aaaa).Address!,
            "the answer is the other AAAA record: records with different data are different records");
        Assert.False(reply.AllRecords.Any(r => firstOnWire.Equals(r.Address)), "the known one is sent nowhere in the reply");
    }

    [TestCase("A legacy unicast querier that lists the answer is not answered either")]
    [Requirement("REQ-ADV-026")]
    public static void Legacy_querier_listing_the_answer_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);
        OutgoingRecord a = Ours(advertisement, host, DnsRecordType.A);

        bool answered = Handle(responder, From(
            QueryCarrying([(host, DnsRecordType.A)], [Known(a, a.Ttl)]), sourcePort: 49152));

        Assert.False(answered, "the rule is about the query, whatever port it came from");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent, by unicast or multicast");
    }

    // ---- Sent all the same ------------------------------------------------------

    [TestCase("A known answer whose TTL is less than half the record's own does not keep it out, and the record goes with its full TTL")]
    [Requirement("REQ-ADV-026")]
    public static void Ptr_listed_with_less_than_half_its_ttl_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord ptr = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        bool answered = Handle(responder, From(QueryCarrying(
            [(Subtype, DnsRecordType.Ptr)], [Known(ptr, (ptr.Ttl / 2) - 1)])));

        Assert.True(answered, "RFC 6762 section 7.1: below half, the responder MUST send an answer");
        DnsRecord sent = transport.Sent.Single().Parsed.Answers.Single(r => r.Type == DnsRecordType.Ptr);
        Assert.Equal(ptr.Ttl, sent.Ttl, "with the record's own TTL, which refreshes the querier's cache");
    }

    [TestCase("Another device's PTR at the same name does not keep the service's own PTR out")]
    [Requirement("REQ-ADV-026")]
    public static void Another_devices_ptr_does_not_keep_ours_out()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord ours = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        // What an iPhone holds for a printer that answers for itself: a PTR at
        // the same shared name, pointing at another instance.
        var theirs = new OutgoingRecord(
            Subtype, DnsRecordType.Ptr, 4500, CacheFlush: false,
            new PtrPayload(DnsName.Parse("EPSON ET-3760 Series._ipp._tcp.local")));

        bool answered = Handle(responder, From(QueryCarrying([(Subtype, DnsRecordType.Ptr)], [theirs])));

        Assert.True(answered, "the querier does not hold the service's PTR");
        Assert.Equal(((PtrPayload)ours.Payload).Target,
            transport.Sent.Single().Parsed.Answers.Single(r => r.Type == DnsRecordType.Ptr).PtrTarget!,
            "the service's own PTR is sent");
    }

    [TestCase("A known answer at another name, with the same type and data, does not keep the record out")]
    [Requirement("REQ-ADV-026")]
    public static void Known_answer_for_another_name_does_not_keep_it_out()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord atServiceType = Ours(advertisement, ServiceType, DnsRecordType.Ptr);
        OutgoingRecord atSubtype = Ours(advertisement, Subtype, DnsRecordType.Ptr);
        Assert.Equal(((PtrPayload)atServiceType.Payload).Target, ((PtrPayload)atSubtype.Payload).Target,
            "both PTR records point at the service's instance, so only their names differ");

        bool answered = Handle(responder, From(QueryCarrying(
            [(Subtype, DnsRecordType.Ptr)], [Known(atServiceType, atServiceType.Ttl)])));

        Assert.True(answered, "the querier holds the PTR at _ipp._tcp.local, not the one asked for");
        Assert.True(transport.Sent.Single().Parsed.Answers.Any(r => r.Type == DnsRecordType.Ptr && r.Name.Equals(Subtype)),
            "the PTR at the subtype is sent");
    }

    [TestCase("A known answer of another type whose data is the same bytes does not keep the record out")]
    [Requirement("REQ-ADV-026")]
    public static void Known_answer_of_another_type_does_not_keep_it_out()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        // Type 99 is not decoded, so the reader keeps its four bytes of data as
        // they arrived: the same four bytes as the host's A record.
        var other = new OutgoingRecord(host, UndecodedType, 120, CacheFlush: false, new AddressPayload(ClientNic.Address));

        bool answered = Handle(responder, From(QueryCarrying([(host, DnsRecordType.A)], [other])));

        Assert.True(answered, "a record of another type is another record, whatever its data");
        Assert.Equal(ClientNic.Address, transport.Sent.Single().Parsed.Answers.Single(r => r.Type == DnsRecordType.A).Address!,
            "the A record is sent");
    }

    [TestCase("A known answer in another class does not keep the record out")]
    [Requirement("REQ-ADV-026")]
    public static void Known_answer_in_another_class_does_not_keep_it_out()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();
        OutgoingRecord ptr = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        bool answered = Handle(responder, From(QueryCarrying(
            [(Subtype, DnsRecordType.Ptr)], [Known(ptr, ptr.Ttl)], classOf: (0, ClassChaos))));

        Assert.True(answered, "a record in class CHAOS is not the service's record, which is in class IN");
        Assert.Equal(1, transport.Sent.Single().Parsed.Answers.Count(r => r.Type == DnsRecordType.Ptr), "the PTR is sent");
    }

    // ---- What the operator is told ----------------------------------------------

    [TestCase("The stop summary's first line gives the queries left unanswered as known apart from those for other services")]
    [Requirement("REQ-ADV-026")]
    public static void Stop_summary_names_the_queries_left_unanswered_as_known()
    {
        (MdnsResponder responder, _, Advertisement advertisement) = Build();
        OutgoingRecord ptr = Ours(advertisement, Subtype, DnsRecordType.Ptr);

        // One answered, two for other services, three carrying their answer:
        // three different counts, so a line that gave one in place of another
        // would read differently. (A first version made one of each, and a
        // mutation that swapped two of the counts was caught by no test.)
        Handle(responder, From(QueryCarrying([(Subtype, DnsRecordType.Ptr)], [])));
        for (int i = 0; i < 2; i++)
        {
            Handle(responder, From(QueryCarrying([(DnsName.Parse("_airplay._tcp.local"), DnsRecordType.Ptr)], [])));
        }

        for (int i = 0; i < 3; i++)
        {
            Handle(responder, From(QueryCarrying([(Subtype, DnsRecordType.Ptr)], [Known(ptr, ptr.Ttl)])));
        }

        string line = responder.Activity.DescribeServed();

        Assert.Equal(
            "Served 1 quer(ies) of 6 seen; 2 were for other services and were ignored; "
            + "3 carried every answer already and were not answered (RFC 6762 s7.1).",
            line,
            "each count where it belongs");
    }
}

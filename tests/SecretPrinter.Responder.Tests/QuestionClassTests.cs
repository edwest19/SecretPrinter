// -----------------------------------------------------------------------------
// QuestionClassTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-01, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the responder reads the class of each question it is asked
//   (README REQ-ADV-025). Every record it sends is in class IN, and RFC 6762
//   section 6 lets a record answer a question only when their classes agree,
//   or when the question asks for any class (ANY, 255). Until 2026-10-01 the
//   class was thrown away as the questions were read: a question in any class
//   was answered, taken for a competing probe, and echoed back to a legacy
//   querier as class IN
//   (docs/findings/2026-10-01-the-responder-ignored-the-question-class.md).
//
// How a question in another class is made:
//   DnsQueryBuilder writes class IN only, because that is all the service ever
//   asks about. WithClasses builds the query with it and then overwrites each
//   question's class field. It reads every class back through the real parser
//   before returning, so a helper that wrote to the wrong bytes fails the test
//   instead of quietly testing something else.
//
// Class 3 (CHAOS) stands for "a class other than IN and ANY". It is used
// because it is a real class; nothing on a home network is known to send it.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class QuestionClassTests
{
    private const ushort ClassIn = 1;
    private const ushort ClassChaos = 3;
    private const ushort ClassAny = 255;
    private const ushort UnicastResponseBit = 0x8000;

    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>Records every wait a probe asks for; nothing waits in real time.</summary>
    private sealed class Clock
    {
        public List<TimeSpan> Requested { get; } = [];

        /// <summary>Called during the wait with that index, before it completes.</summary>
        public Action<int>? During { get; set; }

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Requested.Add(delay);
            During?.Invoke(Requested.Count - 1);
            return Task.CompletedTask;
        }
    }

    private static (MdnsResponder Responder, FakeTransport Transport, Advertisement Advertisement) Build(
        Func<bool>? advertising = null)
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport, [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)], advertising);
        return (responder, transport, advertisement);
    }

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static DnsName Instance(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.Srv).Name;

    /// <summary>
    /// The query DnsQueryBuilder makes for these questions and proposed records,
    /// with each question's class field then set to the class given.
    /// </summary>
    /// <remarks>
    /// The builder writes the 12-byte header, then the questions in order, then
    /// the proposed records. So question i's class is the last two bytes of a
    /// message built from the first i + 1 questions alone, and proposed records
    /// written after the questions do not move it.
    /// </remarks>
    private static byte[] WithClasses(
        (DnsName Name, DnsRecordType Type, ushort RawClass)[] questions, params OutgoingRecord[] proposed)
    {
        var full = new DnsQueryBuilder(0);
        var prefix = new DnsQueryBuilder(0);
        var classAt = new List<int>();

        foreach ((DnsName name, DnsRecordType type, _) in questions)
        {
            full.AddQuestion(name, type, requestUnicastResponse: false);
            prefix.AddQuestion(name, type, requestUnicastResponse: false);
            classAt.Add(prefix.Build().Length - 2);
        }

        foreach (OutgoingRecord record in proposed)
        {
            full.AddAuthority(record with { CacheFlush = false });
        }

        byte[] message = full.Build();
        for (int i = 0; i < questions.Length; i++)
        {
            message[classAt[i]] = (byte)(questions[i].RawClass >> 8);
            message[classAt[i] + 1] = (byte)(questions[i].RawClass & 0xFF);
        }

        DnsMessage parsed = DnsMessage.Parse(message, message.Length);
        for (int i = 0; i < questions.Length; i++)
        {
            Assert.Equal(questions[i].RawClass, parsed.Questions[i].RawClass,
                "the helper wrote the class where the parser reads it");
        }

        return message;
    }

    /// <summary>A datagram from a client on the client network, received over IPv4.</summary>
    private static MdnsDatagram From(byte[] payload, int sourcePort = 5353) =>
        new(payload, new IPEndPoint(IPAddress.Parse("192.168.1.41"), sourcePort), ClientNic.Index, ClientNic);

    private static bool Handle(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    private static OutgoingRecord AddressRecord(DnsName name, string address) =>
        new(name, DnsRecordType.A, 120, CacheFlush: true, new AddressPayload(IPAddress.Parse(address)));

    // ---- Answering ----------------------------------------------------------

    [TestCase("A question in class CHAOS for the host's A record is not answered")]
    [Requirement("REQ-ADV-025")]
    public static void Question_in_another_class_is_not_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        bool answered = Handle(responder, From(WithClasses([(Host(advertisement), DnsRecordType.A, ClassChaos)])));

        Assert.False(answered, "the host's A record is in class IN, so it does not answer a CHAOS question");
        Assert.Equal(0, transport.Sent.Count, "nothing is sent");
        Assert.Equal(1, responder.Activity.IgnoredNotOurs,
            "counted as ignored, as a question for a name the service does not hold is");
    }

    [TestCase("A question in class ANY for the host's A record is answered with it")]
    [Requirement("REQ-ADV-025")]
    public static void Question_in_class_any_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        bool answered = Handle(responder, From(WithClasses([(Host(advertisement), DnsRecordType.A, ClassAny)])));

        Assert.True(answered, "RFC 6762 section 6: a question in class ANY matches records of every class");
        Assert.Equal(ClientNic.Address, transport.Sent[0].Parsed.Answers.Single(r => r.Type == DnsRecordType.A).Address!,
            "the answer is the host's A record");
    }

    [TestCase("A question in class IN with the unicast-response bit set is answered")]
    [Requirement("REQ-ADV-025")]
    public static void Unicast_response_bit_is_not_read_as_class()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        bool answered = Handle(responder, From(WithClasses(
            [(Host(advertisement), DnsRecordType.A, (ushort)(UnicastResponseBit | ClassIn))])));

        Assert.True(answered, "the top bit asks for a unicast answer (RFC 6762 section 5.4); the class is still IN");
        Assert.Equal(1, transport.Sent.Count, "one answer is sent");
    }

    [TestCase("Of two questions, one in class CHAOS and one in class IN, only the IN one is answered")]
    [Requirement("REQ-ADV-025")]
    public static void Only_the_question_in_class_in_is_answered()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        bool answered = Handle(responder, From(WithClasses(
        [
            (Instance(advertisement), DnsRecordType.Txt, ClassChaos),
            (Host(advertisement), DnsRecordType.A, ClassIn),
        ])));

        Assert.True(answered, "the question in class IN is answered");
        DnsMessage reply = transport.Sent[0].Parsed;
        Assert.Equal(1, reply.Answers.Count(r => r.Type == DnsRecordType.A), "with the host's A record");
        Assert.False(reply.AllRecords.Any(r => r.Type == DnsRecordType.Txt),
            "the TXT record, asked for in class CHAOS, is not in the reply");
    }

    // ---- Legacy unicast ------------------------------------------------------

    [TestCase("A legacy unicast question in class ANY is repeated back in class ANY")]
    [Requirement("REQ-ADV-025")]
    public static void Legacy_question_is_repeated_in_its_own_class()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        bool answered = Handle(responder, From(
            WithClasses([(Host(advertisement), DnsRecordType.A, ClassAny)]), sourcePort: 49152));

        Assert.True(answered, "a legacy querier asking in class ANY is answered");
        DnsMessage reply = transport.Sent[0].Parsed;
        Assert.Equal(1, reply.Questions.Count, "RFC 6762 section 6.7: the question is repeated");
        Assert.Equal(ClassAny, reply.Questions[0].RawClass, "as it was asked, in class ANY");
    }

    [TestCase("A legacy unicast question in class IN is repeated back in class IN")]
    [Requirement("REQ-ADV-025")]
    public static void Legacy_question_in_class_in_is_repeated_in_class_in()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build();

        Handle(responder, From(WithClasses([(Host(advertisement), DnsRecordType.A, ClassIn)]), sourcePort: 49152));

        Assert.Equal(ClassIn, transport.Sent[0].Parsed.Questions.Single().RawClass, "as it was asked, in class IN");
    }

    // ---- Probing --------------------------------------------------------------

    [TestCase("A probe in class CHAOS that would win in class IN does not make the responder defer")]
    [Requirement("REQ-ADV-025")]
    public static void Probe_in_another_class_is_not_a_competing_probe()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(static () => false);
        DnsName host = Host(advertisement);
        var clock = new Clock
        {
            // 192.168.1.250 is lexicographically later than the interface's
            // 192.168.1.234, so this proposal would win if it were for class IN.
            During = i =>
            {
                if (i == 1)
                {
                    Handle(responder, From(WithClasses(
                        [(host, DnsRecordType.Any, ClassChaos)], AddressRecord(host, "192.168.1.250"))));
                }
            },
        };

        ProbeResult result = responder.ProbeAsync(clock.Delay, CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(clock.Requested.Contains(MdnsResponder.TiebreakDeferral),
            "a question in class CHAOS is not a probe for the service's class IN records, so nothing is deferred");
        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count,
            "three probes over each transport, and no probe sent again");
        Assert.True(result.IsClear, "the names are clear");
    }

    [TestCase("A probe in class ANY that would win makes the responder defer")]
    [Requirement("REQ-ADV-025")]
    public static void Probe_in_class_any_is_a_competing_probe()
    {
        (MdnsResponder responder, FakeTransport transport, Advertisement advertisement) = Build(static () => false);
        DnsName host = Host(advertisement);
        var clock = new Clock
        {
            During = i =>
            {
                if (i == 1)
                {
                    Handle(responder, From(WithClasses(
                        [(host, DnsRecordType.Any, ClassAny)], AddressRecord(host, "192.168.1.250"))));
                }
            },
        };

        ProbeResult result = responder.ProbeAsync(clock.Delay, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal(MdnsResponder.TiebreakDeferral, clock.Requested[2],
            "a question in class ANY asks about class IN too, so the losing responder waits one second");
        Assert.Equal(2 + 2 * MdnsResponder.ProbeCount, transport.Sent.Count,
            "after the first probe it probes again from the start");
        Assert.True(result.IsClear, "with nobody answering the new probes, the names are clear");
    }
}

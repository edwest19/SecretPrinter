// -----------------------------------------------------------------------------
// ProbeRepliesTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-28, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks REQ-SEC-018: which datagrams the probe, SecretPrinter.Probe, keeps
//   as replies. The probe asks from a port the operating system chooses, so
//   under RFC 6762 s6.7 a responder answers it by unicast, from port 5353,
//   repeating the identifier of its query. The probe keeps a reply only when it
//   came from port 5353 and carries the identifier of the round's query, and
//   prints a line saying why for any other.
//
// How:
//   By calling Program.Judge, the one method the probe's receive loop hands
//   every datagram to, with datagrams built here. No socket is opened.
//
// Written before the change:
//   These tests were run first against the probe with the decision moved into
//   Judge and nothing else changed. The five that a datagram is not kept, or is
//   printed as ignored, failed; the three controls passed. See
//   docs/findings/2026-10-28-the-probe-keeps-only-a-reply-from-port-5353-to-its-own-question.md.
//
// What these tests do NOT prove:
//   That the receive loop passes Judge the identifier of the query it sent.
//   That is read from tools/SecretPrinter.Probe/Program.cs; no test opens the
//   probe's socket.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Probe;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class ProbeRepliesTests
{
    /// <summary>The identifier of the round's query in every test here.</summary>
    private const ushort QueryId = 0x1234;

    /// <summary>A documentation address (RFC 5737), standing for the printer.</summary>
    private static readonly IPAddress Printer = IPAddress.Parse("192.0.2.10");

    private static readonly DnsName ServiceType = DnsName.Parse("_ipp._tcp.local");

    /// <summary>A legacy unicast answer, as RFC 6762 s6.7 describes one, with the given identifier.</summary>
    private static byte[] Answer(ushort id) =>
        new DnsResponseBuilder(id)
            .AddQuestion(ServiceType, DnsRecordType.Ptr, 1)
            .AddAnswer(new OutgoingRecord(
                ServiceType,
                DnsRecordType.Ptr,
                10,
                false,
                new PtrPayload(DnsName.Parse("Example Printer._ipp._tcp.local"))))
            .Build();

    private static Arrival JudgeFrom(int port, byte[] datagram) =>
        SecretPrinter.Probe.Program.Judge(new IPEndPoint(Printer, port), datagram, datagram.Length, QueryId, "1");

    // ---- Controls: what the probe did before, and still does ----------------

    [TestCase("An answer from port 5353 carrying the round's identifier is kept")]
    [Requirement("REQ-SEC-018")]
    public static void Answer_from_port_5353_with_the_rounds_identifier_is_kept()
    {
        Arrival arrival = JudgeFrom(5353, Answer(QueryId));

        Assert.NotNull(arrival.Reply, "an answer to the round's own query, from port 5353, is what the probe asked for");
        Assert.Equal(QueryId, arrival.Reply!.Message.Id, "the kept reply is the one given");
        Assert.True(arrival.Line is not null && arrival.Line.Contains("1 answer(s)", StringComparison.Ordinal),
            "the kept reply is printed with its counts, as before; the line was: " + arrival.Line);
    }

    [TestCase("A malformed datagram from port 5353 is printed as unparseable and not kept")]
    [Requirement("REQ-SEC-018")]
    public static void Malformed_datagram_from_port_5353_is_reported_unparseable()
    {
        Arrival arrival = JudgeFrom(5353, [0x12, 0x34, 0x84]);

        Assert.Null(arrival.Reply, "a datagram that cannot be read is not a reply");
        Assert.True(arrival.Line is not null && arrival.Line.Contains("unparseable", StringComparison.Ordinal),
            "a malformed reply is itself a finding and is printed; the line was: " + arrival.Line);
    }

    [TestCase("A query from port 5353 is neither kept nor printed")]
    [Requirement("REQ-SEC-018")]
    public static void Query_from_port_5353_is_neither_kept_nor_printed()
    {
        byte[] query = new DnsQueryBuilder(QueryId)
            .AddQuestion(ServiceType, DnsRecordType.Ptr, requestUnicastResponse: false)
            .Build();

        Arrival arrival = JudgeFrom(5353, query);

        Assert.Null(arrival.Reply, "a query is not an answer");
        Assert.Null(arrival.Line, "a query is passed over without a line, as before");
    }

    // ---- The port: RFC 6762 s6 ----------------------------------------------

    [TestCase("A reply from any port but 5353 is not kept, whatever its identifier")]
    [Requirement("REQ-SEC-018")]
    public static void Reply_from_any_other_port_is_not_kept()
    {
        foreach (int port in new[] { 1, 53, 5352, 5354, 49152, 65535 })
        {
            Arrival arrival = JudgeFrom(port, Answer(QueryId));

            Assert.Null(arrival.Reply,
                $"RFC 6762 s6: every mDNS response comes from port 5353, so one from port {port} is not taken");
        }
    }

    [TestCase("A reply from another port is printed as ignored, with its port")]
    [Requirement("REQ-SEC-018")]
    public static void Reply_from_another_port_is_printed_as_ignored()
    {
        Arrival arrival = JudgeFrom(49152, Answer(QueryId));

        Assert.True(
            arrival.Line is not null
            && arrival.Line.Contains("ignored", StringComparison.Ordinal)
            && arrival.Line.Contains("port 49152", StringComparison.Ordinal)
            && arrival.Line.Contains("5353", StringComparison.Ordinal),
            "the probe reports rather than hides, so the line says it was ignored and why; the line was: "
            + arrival.Line);
    }

    [TestCase("A malformed datagram from another port is ignored for its port, not read")]
    [Requirement("REQ-SEC-018")]
    public static void Malformed_datagram_from_another_port_is_ignored_for_its_port()
    {
        Arrival arrival = JudgeFrom(49152, [0x12, 0x34, 0x84]);

        Assert.Null(arrival.Reply, "nothing from another port is kept");
        Assert.True(
            arrival.Line is not null
            && arrival.Line.Contains("ignored", StringComparison.Ordinal)
            && !arrival.Line.Contains("unparseable", StringComparison.Ordinal),
            "the port is checked before the datagram is read; the line was: " + arrival.Line);
    }

    // ---- The identifier: RFC 6762 s6 and s6.7 -------------------------------

    [TestCase("A reply from port 5353 carrying another identifier is not kept")]
    [Requirement("REQ-SEC-018")]
    public static void Reply_with_another_identifier_is_not_kept()
    {
        foreach (ushort id in new ushort[] { 0, QueryId - 1, QueryId + 1, 0xFFFF })
        {
            Arrival arrival = JudgeFrom(5353, Answer(id));

            Assert.Null(arrival.Reply,
                $"RFC 6762 s6.7: an answer to the probe repeats its query's identifier, so one carrying {id} answers no query of this round");
        }
    }

    [TestCase("A reply carrying another identifier is printed as ignored, with that identifier")]
    [Requirement("REQ-SEC-018")]
    public static void Reply_with_another_identifier_is_printed_as_ignored()
    {
        Arrival arrival = JudgeFrom(5353, Answer(0x4321));

        Assert.True(
            arrival.Line is not null
            && arrival.Line.Contains("ignored", StringComparison.Ordinal)
            && arrival.Line.Contains(0x4321.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
            "the line says it was ignored and names the identifier it carried; the line was: " + arrival.Line);
    }
}

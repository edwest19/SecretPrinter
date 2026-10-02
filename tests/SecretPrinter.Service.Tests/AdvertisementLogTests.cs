// -----------------------------------------------------------------------------
// AdvertisementLogTests.cs  (SecretPrinter.Service.Tests)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-01, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Checks that the startup log lists the NSEC records the responder sends
//   (README REQ-ADV-022), so that REQ-OBS-003 stays true: an operator can tell
//   from the log alone exactly what the service told the client network.
//
//   The NSEC records are not in the advertisement. The responder builds one
//   for each name the advertisement claims, and AdvertisementLog wrote only
//   the advertisement's own records. Without the lines checked here, the
//   service would have sent records its log never mentioned.
//
//   The last test compares the log with what a responder actually sends for
//   the same advertisement, record by record. A log line written from a second
//   calculation could drift from the wire; that test is what would catch it.
//
// Why this is its own file:
//   The other tests of AdvertisementLog are in SecurityClaimsTests.cs. These
//   are kept apart because they need the responder and a fake transport, and
//   nothing else in that file does.
//
// Addresses:
//   fe80::10 is made up, scoped to 15, the made-up IPv6 index of the client
//   adapter here. 192.168.1.161 is the client address already used throughout
//   the repository, and 192.168.1.41 stands for a client.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Responder;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Service.Tests;

internal static class AdvertisementLogTests
{
    private static readonly MdnsInterface Client =
        new("Ethernet", IPAddress.Parse("192.168.1.161"), 15, AddressFamily.InterNetwork);

    private static readonly IPAddress LinkLocal = IPAddress.Parse("fe80::10%15");

    private static Advertisement Advertised(params IPAddress[] linkLocal) =>
        AdvertisementBuilder.Build(
            new PrinterCapabilities(["txtvers=1", "rp=ipp/print"], 631, CapabilitySource.ForTest("advertisement log tests")),
            new ProxyIdentity("SecretPrinter", "secretprinter", Guid.Parse("b6f4e2a1-9c37-4d58-8e0b-7a1f3d6c5e94"), 631),
            Client.Address,
            linkLocal);

    /// <summary>The log's lines about NSEC records: those that begin with the word.</summary>
    private static List<string> NsecLines(Advertisement advertisement)
    {
        var log = new CollectingServiceLog();
        AdvertisementLog.Write(log, Client, advertisement);
        return [.. log.Entries.Select(e => e.Message).Where(m => m.StartsWith("  nsec ", StringComparison.Ordinal))];
    }

    [TestCase("The startup log lists the NSEC record for the host and for the instance, each with its TTL and types")]
    [Requirement("REQ-OBS-002")]
    public static void The_log_lists_both_nsec_records()
    {
        List<string> lines = NsecLines(Advertised(LinkLocal));

        Assert.Equal(2, lines.Count, "one line for each name the advertisement claims");
        Assert.True(lines.Contains("  nsec    secretprinter.local ttl=120 types=A,Aaaa"),
            "the host's: " + string.Join(" | ", lines));
        Assert.True(lines.Contains("  nsec    SecretPrinter._ipp._tcp.local ttl=4500 types=Txt,Srv"),
            "the instance's: " + string.Join(" | ", lines));
    }

    [TestCase("With no link-local address, the log shows the host's NSEC listing A alone")]
    [Requirement("REQ-OBS-002")]
    public static void Without_a_link_local_address_the_log_shows_a_alone()
    {
        List<string> lines = NsecLines(Advertised());

        Assert.True(lines.Contains("  nsec    secretprinter.local ttl=120 types=A"),
            "the log says what the NSEC will say: the host has an A record and nothing else: "
            + string.Join(" | ", lines));
    }

    [TestCase("The log says when the NSEC records are sent, and that they are not announced")]
    [Requirement("REQ-OBS-003")]
    public static void The_log_says_when_they_are_sent()
    {
        var log = new CollectingServiceLog();
        AdvertisementLog.Write(log, Client, Advertised(LinkLocal));
        List<string> messages = [.. log.Entries.Select(e => e.Message)];

        int heading = messages.FindIndex(m => m.Contains("NSEC records", StringComparison.Ordinal));
        Assert.True(heading >= 0, "a line introduces the NSEC records");
        Assert.True(messages[heading].Contains("not announced", StringComparison.Ordinal),
            "and says they are not announced, unlike the records listed before them");
        Assert.True(messages[heading].Contains("REQ-ADV-022", StringComparison.Ordinal),
            "and names the requirement that says when they are sent");
        Assert.True(messages[heading + 1].StartsWith("  nsec ", StringComparison.Ordinal),
            "the records follow it directly");
        Assert.True(
            messages.FindLastIndex(m => m.StartsWith("  record ", StringComparison.Ordinal)) < heading,
            "after every announced record, so the two kinds are not mixed");
    }

    [TestCase("Every NSEC a responder sends for an advertisement is one the log listed, and the log lists no other")]
    [Requirement("REQ-OBS-003")]
    public static void The_log_matches_what_the_responder_sends()
    {
        foreach (IPAddress[] linkLocal in new IPAddress[][] { [], [LinkLocal] })
        {
            Advertisement advertisement = Advertised(linkLocal);
            List<string> logged = NsecLines(advertisement);

            var transport = new FakeTransport(Client);
            var responder = new MdnsResponder(transport, [new AdvertisedInterface(Client, advertisement)]);

            // The goodbye carries every NSEC the responder holds, with TTL 0.
            // The negative answers carry them with their real TTLs.
            responder.SendGoodbyeAsync(CancellationToken.None).GetAwaiter().GetResult();
            List<DnsRecord> inGoodbye =
                [.. transport.Sent.Single().Parsed.Answers.Where(r => r.Type == DnsRecordType.Nsec)];
            Assert.Equal(logged.Count, inGoodbye.Count,
                $"{linkLocal.Length} link-local address(es): the log lists as many NSEC records as the responder holds");

            foreach (DnsRecord held in inGoodbye)
            {
                byte[] query = new DnsQueryBuilder(0)
                    .AddQuestion(held.Name, (DnsRecordType)65, requestUnicastResponse: false)
                    .Build();
                responder.HandleAsync(
                    new MdnsDatagram(query, new IPEndPoint(IPAddress.Parse("192.168.1.41"), 5353), Client.Index, Client),
                    CancellationToken.None).GetAwaiter().GetResult();

                DnsRecord sent = transport.Sent[^1].Parsed.Answers.Single();
                string asLogged =
                    $"  nsec    {sent.Name} ttl={sent.Ttl} types={string.Join(",", sent.NsecTypes!)}";

                Assert.True(logged.Contains(asLogged),
                    $"the responder sent [{asLogged.Trim()}], and the log listed: " + string.Join(" | ", logged));
            }
        }
    }
}

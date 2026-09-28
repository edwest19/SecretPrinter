// -----------------------------------------------------------------------------
// MdnsProbeTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-28, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   Verifies the probe the responder sends before it claims its names, and how
//   it treats what it hears while probing (README REQ-ADV-023 and REQ-ADV-024;
//   RFC 6762 s8.1, s8.2 and s9).
//
// No [Requirement] markers here, on purpose:
//   REQ-ADV-023 also requires the probe to happen at startup and before every
//   restore, which is the service's wiring, not the responder's. REQ-ADV-024
//   also requires a conflict heard after announcing to withdraw the service.
//   Neither is fully met by what these tests cover, and a marker is placed only
//   when a requirement is fully met. The markers go on when the wiring does.
//
// Time:
//   Every wait the probe makes goes through an injected delay, recorded here,
//   so nothing waits in real time and each test can deliver a datagram at an
//   exact point in the sequence - "during" a given wait - by handing it to the
//   responder the way the receive loop would.
//
// Throughout, the responder is built with nothing on offer, because that is
// the state it probes in: before the first announcement, and while withdrawn
// before a restore. What it hears must count in that state too.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.TestKit;

namespace SecretPrinter.Responder.Tests;

internal static class MdnsProbeTests
{
    private static readonly MdnsInterface ClientNic = MdnsResponderTests.ClientNic;
    private static readonly MdnsInterface ClientNicV6 = MdnsResponderTests.ClientNicV6;

    /// <summary>Records every wait the probe asks for, and what had been sent by then.</summary>
    private sealed class RecordingClock(FakeTransport transport)
    {
        public List<(TimeSpan Delay, int SentBefore)> Requested { get; } = [];

        /// <summary>Called during the wait with that index, before it completes.</summary>
        public Action<int>? During { get; set; }

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            Requested.Add((delay, transport.Sent.Count));
            During?.Invoke(Requested.Count - 1);
            return Task.CompletedTask;
        }
    }

    private static (MdnsResponder Responder, FakeTransport Transport, RecordingClock Clock, Advertisement Advertisement) Build()
    {
        Advertisement advertisement = MdnsResponderTests.BuildAdvertisement();
        var transport = new FakeTransport(ClientNic, ClientNicV6);
        var responder = new MdnsResponder(
            transport,
            [new AdvertisedInterface(ClientNic, advertisement, ClientNicV6)],
            advertising: static () => false);
        return (responder, transport, new RecordingClock(transport), advertisement);
    }

    private static ProbeResult Probe(MdnsResponder responder, RecordingClock clock) =>
        responder.ProbeAsync(clock.Delay, CancellationToken.None).GetAwaiter().GetResult();

    private static void Hear(MdnsResponder responder, MdnsDatagram datagram) =>
        responder.HandleAsync(datagram, CancellationToken.None).GetAwaiter().GetResult();

    private static DnsName Host(Advertisement advertisement) =>
        advertisement.Records.Single(r => r.Type == DnsRecordType.A).Name;

    private static List<OutgoingRecord> Unique(Advertisement advertisement) =>
        [.. advertisement.Records.Where(r => r.CacheFlush)];

    private static OutgoingRecord AddressRecord(DnsName name, string address) =>
        new(name, IPAddress.Parse(address).AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? DnsRecordType.Aaaa
                : DnsRecordType.A,
            120, CacheFlush: true, new AddressPayload(IPAddress.Parse(address)));

    /// <summary>A response, as another device on the client network would send it.</summary>
    private static MdnsDatagram ResponseFrom(string source, MdnsInterface arrivedOn, params OutgoingRecord[] records)
    {
        var builder = new DnsResponseBuilder();
        foreach (OutgoingRecord record in records)
        {
            builder.AddAnswer(record);
        }

        return new MdnsDatagram(builder.Build(), new IPEndPoint(IPAddress.Parse(source), 5353), arrivedOn.Index, arrivedOn);
    }

    /// <summary>A probe from another device: a question for the name, and what it proposes.</summary>
    private static MdnsDatagram ProbeFrom(string source, MdnsInterface arrivedOn, DnsName name, params OutgoingRecord[] proposed)
    {
        var builder = new DnsQueryBuilder(0).AddQuestion(name, DnsRecordType.Any, requestUnicastResponse: false);
        foreach (OutgoingRecord record in proposed)
        {
            builder.AddAuthority(record with { CacheFlush = false });
        }

        return new MdnsDatagram(builder.Build(), new IPEndPoint(IPAddress.Parse(source), 5353), arrivedOn.Index, arrivedOn);
    }

    // ---- What goes out ------------------------------------------------------

    [TestCase("A probe is sent three times over each transport, by multicast")]
    public static void Three_probes_over_each_transport()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, _) = Build();

        Probe(responder, clock);

        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count,
            "RFC 6762 s8.1: three probes, on the IPv4 entry and on its IPv6 companion");
        Assert.Equal(MdnsResponder.ProbeCount, transport.Sent.Count(s => s.Via.Matches(ClientNic)),
            "three over IPv4");
        Assert.Equal(MdnsResponder.ProbeCount, transport.Sent.Count(s => s.Via.Matches(ClientNicV6)),
            "three over IPv6: on this network IPv4 mDNS from part of the client side did not arrive, "
            + "so a probe over IPv4 alone could miss the device that holds the name");
        Assert.True(transport.Sent.All(s => s.WasMulticast), "probes are multicast");
        Assert.False(transport.Sent.Any(s => s.Parsed.IsResponse), "a probe is a query, not a response");
    }

    [TestCase("Each probe asks for every unique name with type ANY and proposes its records")]
    public static void Probe_asks_for_each_unique_name_and_proposes_its_records()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();
        List<OutgoingRecord> unique = Unique(advertisement);
        string[] expectedNames = [.. unique.Select(r => r.Name.ToString()).Distinct().Order(StringComparer.Ordinal)];
        string[] expectedProposals = [.. unique.Select(r => $"{r.Name} {r.Type}").Order(StringComparer.Ordinal)];

        Probe(responder, clock);

        foreach (SentDatagram sent in transport.Sent)
        {
            DnsMessage probe = sent.Parsed;

            Assert.Equal(string.Join("|", expectedNames),
                string.Join("|", probe.Questions.Select(q => q.Name.ToString()).Order(StringComparer.Ordinal)),
                "one question per name the advertisement marks unique - the instance and the host");
            Assert.True(probe.Questions.All(q => q.Type == DnsRecordType.Any),
                "RFC 6762 s8.1: probes ask for type ANY, so one probe claims every type at the name");
            Assert.True(probe.Questions.All(q => (q.RawClass & 0x8000) == 0),
                "probes ask for multicast answers; see the note on this in MdnsResponder.Probing.cs");

            Assert.Equal(string.Join("|", expectedProposals),
                string.Join("|", probe.Authorities.Select(r => $"{r.Name} {r.Type}").Order(StringComparer.Ordinal)),
                "RFC 6762 s8.2: the Authority Section carries every record being claimed");
            Assert.True(probe.Authorities.All(r => !r.CacheFlush),
                "RFC 6762 s10.2 sets the cache-flush bit only in responses");
            Assert.Equal(ClientNic.Address,
                probe.Authorities.Single(r => r.Type == DnsRecordType.A).Address,
                "the proposed A record is the one this interface's advertisement publishes");
        }
    }

    [TestCase("Probes start after up to 250 ms, go 250 ms apart, and the last is followed by a 250 ms wait")]
    public static void Probe_timing()
    {
        (MdnsResponder responder, _, RecordingClock clock, _) = Build();

        Probe(responder, clock);

        Assert.Equal(1 + MdnsResponder.ProbeCount, clock.Requested.Count,
            "a random start, then one wait after each of the three probes");

        (TimeSpan start, int sentBeforeStart) = clock.Requested[0];
        Assert.True(start >= TimeSpan.Zero && start <= MdnsResponder.MaxProbeStartDelay,
            $"RFC 6762 s8.1: a random start of 0-250 ms, got {start.TotalMilliseconds} ms");
        Assert.Equal(0, sentBeforeStart, "the random start comes before the first probe");

        for (int round = 1; round <= MdnsResponder.ProbeCount; round++)
        {
            Assert.Equal(MdnsResponder.ProbeInterval, clock.Requested[round].Delay,
                $"wait {round} is 250 ms");
            Assert.Equal(2 * round, clock.Requested[round].SentBefore,
                $"wait {round} follows probe {round}, sent over both transports");
        }
    }

    // ---- What counts as a conflict ------------------------------------------

    [TestCase("With nothing answering, the probe comes back clear")]
    public static void Unanswered_probe_is_clear()
    {
        (MdnsResponder responder, _, RecordingClock clock, _) = Build();

        ProbeResult result = Probe(responder, clock);

        Assert.True(result.IsClear, "no device answered, so the names are free to claim");
        Assert.Null(responder.Conflict, "no conflict is recorded");
    }

    [TestCase("Another device's different address for our host name is a conflict, and probing stops")]
    public static void Different_address_for_our_host_is_a_conflict()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        // During the wait after the second probe.
        clock.During = i =>
        {
            if (i == 2)
            {
                Hear(responder, ResponseFrom("192.168.1.50", ClientNicV6, AddressRecord(host, "192.168.1.50")));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.False(result.IsClear, "RFC 6762 s8.1: an answer for a probed name is a conflict");
        NameConflict conflict = result.Conflict!;
        Assert.True(conflict.Name.Equals(host), "the conflict names the host name");
        Assert.Equal(DnsRecordType.A, conflict.Type, "and the record type");
        Assert.Equal(IPAddress.Parse("192.168.1.50"), conflict.From, "and the device that answered");
        Assert.True(conflict.ArrivedOn.Matches(ClientNicV6), "and the transport it was heard on");
        Assert.Equal(4, transport.Sent.Count, "no third probe goes out once the name is known to be taken");
        Assert.Equal(conflict, responder.Conflict, "the responder keeps the conflict it found");
    }

    [TestCase("Another device's record of a type we do not publish, for our name, is a conflict")]
    public static void Other_type_for_our_name_is_a_conflict()
    {
        (MdnsResponder responder, _, RecordingClock clock, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ResponseFrom("192.168.1.51", ClientNic, AddressRecord(host, "fe80::51")));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.False(result.IsClear,
            "probing for type ANY claims every type at the name, so any other device's record for it conflicts");
        Assert.Equal(DnsRecordType.Aaaa, result.Conflict!.Type, "the conflicting type is named");
    }

    [TestCase("Records identical to our own are not a conflict")]
    public static void Identical_records_are_not_a_conflict()
    {
        (MdnsResponder responder, _, RecordingClock clock, Advertisement advertisement) = Build();

        // Exactly what this responder publishes - its own announcement heard back,
        // or a cooperating responder giving identical answers (RFC 6762 s9).
        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ResponseFrom("192.168.1.234", ClientNic, [.. advertisement.Records]));
            }
        };

        Assert.True(Probe(responder, clock).IsClear, "RFC 6762 s9: identical records are never a conflict");
    }

    [TestCase("A response about other names is not a conflict")]
    public static void Unrelated_response_is_not_a_conflict()
    {
        (MdnsResponder responder, _, RecordingClock clock, _) = Build();

        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ResponseFrom(
                    "192.168.1.60", ClientNic, AddressRecord(DnsName.Parse("otherprinter.local"), "192.168.1.60")));
            }
        };

        Assert.True(Probe(responder, clock).IsClear, "only records for the names being claimed can conflict");
    }

    // ---- Simultaneous probes (RFC 6762 s8.2) --------------------------------

    [TestCase("Losing a simultaneous probe defers one second and probes again from the start")]
    public static void Losing_tiebreak_defers_and_probes_again()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        // 192.168.1.250 is lexicographically later than this interface's
        // 192.168.1.234 (last byte 0xFA against 0xEA), so the other device wins.
        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ProbeFrom("192.168.1.250", ClientNic, host, AddressRecord(host, "192.168.1.250")));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.Equal(MdnsResponder.TiebreakDeferral, clock.Requested[2].Delay,
            "RFC 6762 s8.2: the loser waits one second");
        Assert.Equal(2, clock.Requested[2].SentBefore, "the deferral follows the first probe");
        Assert.Equal(2 + 2 * MdnsResponder.ProbeCount, transport.Sent.Count,
            "and then probes again from the first probe");
        Assert.True(result.IsClear, "with nobody answering the new probes, the names are clear");
    }

    [TestCase("The winner answering during the deferral ends the probe, with no probe sent again")]
    public static void Winner_answering_during_deferral_is_a_conflict()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        // The other device wins the tiebreak during the first wait, then claims
        // the name and answers for it during this responder's one-second wait.
        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ProbeFrom("192.168.1.250", ClientNic, host, AddressRecord(host, "192.168.1.250")));
            }
            else if (i == 2)
            {
                Hear(responder, ResponseFrom("192.168.1.250", ClientNic, AddressRecord(host, "192.168.1.250")));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.Equal(MdnsResponder.TiebreakDeferral, clock.Requested[2].Delay, "the loser was deferring");
        Assert.False(result.IsClear, "the winner's answer is a conflict");
        Assert.Equal(IPAddress.Parse("192.168.1.250"), result.Conflict!.From, "and it names the winner");
        Assert.Equal(2, transport.Sent.Count, "no probe goes out again once the name is known to be taken");
    }

    [TestCase("A responder already holding a conflict probes no more")]
    public static void Existing_conflict_ends_the_probe_before_it_starts()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();

        // Heard before this probe began: after an earlier announcement, say.
        // A conflict lasts until restart, so a later probe must not reclaim the name.
        Hear(responder, ResponseFrom("192.168.1.50", ClientNic, AddressRecord(Host(advertisement), "192.168.1.50")));
        NameConflict earlier = responder.Conflict!;

        ProbeResult result = Probe(responder, clock);

        Assert.Equal(earlier, result.Conflict, "the probe reports the conflict already held");
        Assert.Equal(0, transport.Sent.Count, "and sends nothing");
    }

    [TestCase("Winning a simultaneous probe changes nothing")]
    public static void Winning_tiebreak_is_ignored()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, Advertisement advertisement) = Build();
        DnsName host = Host(advertisement);

        // 192.168.1.10 is lexicographically earlier than 192.168.1.234, so this
        // responder wins and carries on.
        clock.During = i =>
        {
            if (i == 1)
            {
                Hear(responder, ProbeFrom("192.168.1.10", ClientNic, host, AddressRecord(host, "192.168.1.10")));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.False(clock.Requested.Any(r => r.Delay == MdnsResponder.TiebreakDeferral), "the winner does not defer");
        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count, "three probes per transport, as usual");
        Assert.True(result.IsClear, "a lost tiebreak is the other device's problem");
    }

    [TestCase("Our own probe, heard back, is a tie and changes nothing")]
    public static void Own_probe_heard_back_is_a_tie()
    {
        (MdnsResponder responder, FakeTransport transport, RecordingClock clock, _) = Build();

        clock.During = i =>
        {
            if (i == 1)
            {
                byte[] ownProbe = transport.Sent[0].Payload;
                Hear(responder, new MdnsDatagram(
                    ownProbe, new IPEndPoint(ClientNic.Address, 5353), ClientNic.Index, ClientNic));
            }
        };

        ProbeResult result = Probe(responder, clock);

        Assert.False(clock.Requested.Any(r => r.Delay == MdnsResponder.TiebreakDeferral),
            "RFC 6762 s8.2.1: identical proposals are no conflict, which is what an echo of our own probe is");
        Assert.Equal(2 * MdnsResponder.ProbeCount, transport.Sent.Count, "probing carries on unchanged");
        Assert.True(result.IsClear, "our own probe is not another device");
    }
}

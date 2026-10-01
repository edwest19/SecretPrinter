// -----------------------------------------------------------------------------
// MdnsResponder.Probing.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-28, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// The first conflict is now also reported to the service, once, through the
// constructor's 'conflicted' callback, by Claude (Anthropic model, Claude Opus
// 5.5) at the direction of Edwin West, 2026-09-28. Reviewed by a human before
// merge.
//
// REQ-ADV-023 and REQ-ADV-024 markers placed, and the paragraph saying they
// were not yet placed rewritten, once the service wiring (Offering.cs) made
// both requirements fully met, by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-09-28. Reviewed by a human before merge.
//
// A question in a class other than IN or ANY no longer taken for a competing
// probe, by Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin
// West, 2026-10-01, for REQ-ADV-025. Before, the class was not read, so such a
// question with a winning proposal made this responder defer and probe again.
// See docs/findings/2026-10-01-the-responder-ignored-the-question-class.md.
// Reviewed by a human before merge.
//
// Purpose:
//   Before a responder may treat a name as its own, RFC 6762 s8.1 has it ask
//   whether anyone else already uses it. This file is that question, and the
//   judgement of what the answers mean:
//
//   - ProbeAsync sends the probe: three queries for type ANY, 250 ms apart,
//     after a random start, each carrying the records being claimed.
//   - A response from another device carrying a record for a claimed name that
//     this responder would not itself publish is a conflict (RFC 6762 s8.1, s9;
//     README REQ-ADV-024). The first one found is kept, and never cleared: the
//     decision on 2026-09-28 was that a conflict withdraws the service until it
//     is restarted with a different name, rather than renaming it.
//   - A probe from another device for the same name, heard while probing, is
//     settled by comparing the two proposals (RFC 6762 s8.2).
//
//   Which names are claimed is read out of the advertisement, like everything
//   else this responder says: the names of its records marked unique (the
//   cache-flush bit set). There is no list of names in this file.
//
//   What this file does not do: decide WHEN to probe, or what the service does
//   about a conflict. Both are the service's (README REQ-ADV-023, REQ-ADV-024;
//   src/SecretPrinter.Service/Offering.cs).
//
// Probes ask for multicast answers, a deliberate departure:
//   RFC 6762 s8.1 says probes SHOULD set the unicast-response bit, so that a
//   device defending the name can answer at once, by unicast. This service
//   shares UDP port 5353 with the Windows DNS Client (REQ-ADV-014), and which
//   of two sockets sharing a port receives a unicast datagram has not been
//   established by this project. A unicast answer could therefore go to the
//   other socket and never be seen, and the probe would come back clear with
//   the name taken. A multicast answer reaches every member of the group. The
//   cost is the RFC's: a defender may wait up to 250 ms before a multicast
//   answer, which is the length of the wait after the last probe.
// -----------------------------------------------------------------------------

using System.Net;
using System.Security.Cryptography;
using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Responder;

/// <summary>Another device's record for a name this responder claims.</summary>
/// <param name="Name">The claimed name the record was for.</param>
/// <param name="Type">The type of the other device's record.</param>
/// <param name="From">The address the response came from.</param>
/// <param name="ArrivedOn">The interface and transport it arrived on.</param>
public sealed record NameConflict(DnsName Name, DnsRecordType Type, IPAddress From, MdnsInterface ArrivedOn);

/// <summary>How a probe ended.</summary>
/// <param name="Conflict">The conflict that ended it, or null when the names are free.</param>
public sealed record ProbeResult(NameConflict? Conflict)
{
    /// <summary>True when nobody else answered for the names being claimed.</summary>
    public bool IsClear => Conflict is null;
}

public sealed partial class MdnsResponder
{
    /// <summary>Probes sent before a name is claimed (RFC 6762 s8.1).</summary>
    public const int ProbeCount = 3;

    /// <summary>
    /// The gap after each probe, including the last, during which an answer
    /// counts (RFC 6762 s8.1).
    /// </summary>
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The longest random wait before the first probe, so devices started by
    /// the same event do not probe in step (RFC 6762 s8.1).
    /// </summary>
    public static readonly TimeSpan MaxProbeStartDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long the loser of a simultaneous probe waits before probing again
    /// (RFC 6762 s8.2).
    /// </summary>
    public static readonly TimeSpan TiebreakDeferral = TimeSpan.FromSeconds(1);

    // Written by the receive loop (HandleAsync) and read by ProbeAsync and the
    // service, which run on other tasks: hence Volatile and Interlocked.
    private NameConflict? _conflict;
    private readonly Action<NameConflict>? _conflicted;
    private bool _probing;
    private bool _lostTiebreak;

    /// <summary>
    /// The first conflict heard for a name this responder claims, or null. Once
    /// set it stays set for the life of this responder.
    /// </summary>
    public NameConflict? Conflict => Volatile.Read(ref _conflict);

    /// <summary>
    /// Asks whether anyone else uses the names this responder claims, on every
    /// advertised interface, over IPv4 and, where there is one, over its IPv6
    /// companion.
    /// </summary>
    /// <remarks>
    /// Answers are heard by <see cref="HandleAsync"/>, so the receive loop must
    /// be running while this runs. Nothing here reads a socket.
    /// </remarks>
    /// <param name="delay">
    /// Every wait goes through this, so tests need not wait in real time.
    /// Defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// </param>
    [Requirement("REQ-ADV-023",
        "Sends the probe: three queries for type ANY, 250 ms apart after a random start, on every advertised interface over IPv4 and its IPv6 companion, each carrying the claimed records; stops at the first conflict, and settles a simultaneous probe as RFC 6762 s8.2 describes.")]
    public async Task<ProbeResult> ProbeAsync(
        Func<TimeSpan, CancellationToken, Task>? delay, CancellationToken cancellationToken)
    {
        delay ??= Task.Delay;

        List<(byte[] Payload, MdnsInterface Via)> probes = BuildProbes();

        Volatile.Write(ref _lostTiebreak, false);
        Volatile.Write(ref _probing, true);
        try
        {
            TimeSpan start = TimeSpan.FromMilliseconds(
                RandomNumberGenerator.GetInt32(0, (int)MaxProbeStartDelay.TotalMilliseconds + 1));
            await delay(start, cancellationToken).ConfigureAwait(false);

            int sent = 0;
            while (sent < ProbeCount)
            {
                if (Conflict is { } before)
                {
                    return new ProbeResult(before);
                }

                foreach ((byte[] payload, MdnsInterface via) in probes)
                {
                    await _transport.SendMulticastAsync(payload, via, cancellationToken).ConfigureAwait(false);
                }

                sent++;
                await delay(ProbeInterval, cancellationToken).ConfigureAwait(false);

                if (Conflict is { } heard)
                {
                    // RFC 6762 s8.1: an answer during probing means the name is
                    // taken. No further probe is sent.
                    return new ProbeResult(heard);
                }

                if (Interlocked.Exchange(ref _lostTiebreak, false))
                {
                    // RFC 6762 s8.2: the loser waits a second and starts again.
                    // A real winner will have claimed the name by then and will
                    // answer the next probe, which is a conflict. A stale copy
                    // of a probe will not, and the name is claimed after all.
                    await delay(TiebreakDeferral, cancellationToken).ConfigureAwait(false);
                    sent = 0;
                }
            }

            return new ProbeResult(Conflict);
        }
        finally
        {
            Volatile.Write(ref _probing, false);
        }
    }

    /// <summary>
    /// One probe per advertised interface, sent over the IPv4 entry and over the
    /// IPv6 companion where there is one.
    /// </summary>
    private List<(byte[] Payload, MdnsInterface Via)> BuildProbes()
    {
        var probes = new List<(byte[] Payload, MdnsInterface Via)>();

        foreach (AdvertisedInterface entry in _advertised)
        {
            List<OutgoingRecord> claimed = Claimed(entry.Advertisement);
            if (claimed.Count == 0)
            {
                continue;
            }

            // Query identifier 0, as RFC 6762 s18.1 asks of multicast queries.
            var builder = new DnsQueryBuilder(0);

            foreach (DnsName name in claimed.Select(record => record.Name).Distinct())
            {
                // Type ANY claims every type at the name (RFC 6762 s8.1), which
                // is what entitles the service to deny that a type exists there
                // later (s6.1). Multicast answers: see the header of this file.
                builder.AddQuestion(name, DnsRecordType.Any, requestUnicastResponse: false);
            }

            foreach (OutgoingRecord record in claimed)
            {
                // Proposed, not asserted: RFC 6762 s10.2 sets the cache-flush
                // bit only in responses.
                builder.AddAuthority(record with { CacheFlush = false });
            }

            byte[] payload = builder.Build();
            probes.Add((payload, entry.Interface));

            if (entry.IPv6Interface is { } companion)
            {
                probes.Add((payload, companion));
            }
        }

        return probes;
    }

    /// <summary>The records the advertisement marks unique: the ones claimed.</summary>
    private static List<OutgoingRecord> Claimed(Advertisement advertisement) =>
        [.. advertisement.Records.Where(record => record.CacheFlush)];

    /// <summary>
    /// Records the first conflict in a response: a record for a claimed name
    /// that this responder would not itself publish, whether another type or
    /// the same type with different data. Identical records are not a conflict
    /// (RFC 6762 s9); that is also how this responder's own multicast, heard
    /// back, is recognised.
    /// </summary>
    [Requirement("REQ-ADV-024",
        "Defines a conflict: a class IN record from another device, for a claimed name, that this responder would not itself publish - another type, or the same type with different data. Identical records are not one. The first is kept for the life of the responder and reported once.")]
    private void NoteConflicts(DnsMessage response, MdnsDatagram datagram, MdnsInterface arrivedOn, Advertisement advertisement)
    {
        if (Conflict is not null)
        {
            return;
        }

        List<OutgoingRecord> claimed = Claimed(advertisement);

        foreach (DnsRecord theirs in response.AllRecords)
        {
            // Class IN only, with the cache-flush bit masked off
            // (RFC 6762 s10.2). A record in another class says nothing about
            // the names this responder claims in class IN.
            if ((theirs.RawClass & 0x7FFF) != 1)
            {
                continue;
            }

            List<OutgoingRecord> ours = [.. claimed.Where(record => record.Name.Equals(theirs.Name))];
            if (ours.Count == 0)
            {
                continue;
            }

            byte[]? theirData = RdataOf(theirs);
            if (theirData is not null && ours.Any(
                    record => record.Type == theirs.Type
                              && DnsRecordWriter.EncodeRdata(record.Type, record.Payload).AsSpan().SequenceEqual(theirData)))
            {
                continue;
            }

            var found = new NameConflict(theirs.Name, theirs.Type, datagram.Source.Address, arrivedOn);

            // Only the call that records the conflict reports it, so the
            // service hears of it exactly once.
            if (Interlocked.CompareExchange(ref _conflict, found, null) is null)
            {
                _conflicted?.Invoke(found);
            }

            return;
        }
    }

    /// <summary>
    /// While probing, settles a probe from another device for a name this
    /// responder is also probing for (RFC 6762 s8.2 and s8.2.1). Losing is
    /// remembered for ProbeAsync, which defers and starts again. Winning, and a
    /// tie - which is what this responder's own probe heard back looks like -
    /// change nothing. A question in a class other than IN or ANY is not about
    /// this responder's records, all of which are in class IN, so it is not a
    /// competing probe whatever it proposes.
    /// </summary>
    [Requirement("REQ-ADV-025",
        "Takes a query for a competing probe only for a question in class IN or ANY, read without the unicast-response bit, because every record this responder claims is in class IN.")]
    private void NoteSimultaneousProbe(DnsMessage query, Advertisement advertisement)
    {
        if (!Volatile.Read(ref _probing) || query.Authorities.Count == 0)
        {
            return;
        }

        List<OutgoingRecord> claimed = Claimed(advertisement);

        foreach ((DnsName name, _, ushort rawClass) in query.Questions)
        {
            if (!AsksAboutClassIn(rawClass))
            {
                continue;
            }

            List<OutgoingRecord> ours = [.. claimed.Where(record => record.Name.Equals(name))];
            List<DnsRecord> theirs = [.. query.Authorities.Where(record => record.Name.Equals(name))];

            if (ours.Count == 0 || theirs.Count == 0)
            {
                continue;
            }

            List<(int Class, int Type, byte[] Data)> ourProposal =
                [.. ours.Select(record => (1, (int)record.Type, DnsRecordWriter.EncodeRdata(record.Type, record.Payload)))];

            List<(int Class, int Type, byte[] Data)> theirProposal = [];
            foreach (DnsRecord record in theirs)
            {
                byte[]? data = RdataOf(record);
                if (data is null)
                {
                    // Data that cannot be written back out cannot be compared.
                    // Treated as a proposal this responder does not beat, so it
                    // defers and probes again rather than claiming over it.
                    Volatile.Write(ref _lostTiebreak, true);
                    return;
                }

                theirProposal.Add((record.RawClass & 0x7FFF, (int)record.Type, data));
            }

            if (CompareProposals(ourProposal, theirProposal) < 0)
            {
                Volatile.Write(ref _lostTiebreak, true);
                return;
            }
        }
    }

    /// <summary>
    /// RFC 6762 s8.2.1: sort each side by class, then type, then data compared
    /// as unsigned bytes; compare pairwise until a difference; if one list runs
    /// out first, the one with records remaining is later. Negative when ours
    /// is earlier, which is losing.
    /// </summary>
    private static int CompareProposals(
        List<(int Class, int Type, byte[] Data)> ours, List<(int Class, int Type, byte[] Data)> theirs)
    {
        ours.Sort(CompareRecord);
        theirs.Sort(CompareRecord);

        for (int i = 0; i < Math.Min(ours.Count, theirs.Count); i++)
        {
            int difference = CompareRecord(ours[i], theirs[i]);
            if (difference != 0)
            {
                return difference;
            }
        }

        return ours.Count.CompareTo(theirs.Count);

        static int CompareRecord((int Class, int Type, byte[] Data) a, (int Class, int Type, byte[] Data) b)
        {
            int byClass = a.Class.CompareTo(b.Class);
            if (byClass != 0)
            {
                return byClass;
            }

            int byType = a.Type.CompareTo(b.Type);
            if (byType != 0)
            {
                return byType;
            }

            // Unsigned byte by byte; a shorter run that matches throughout is
            // earlier (RFC 6762 s8.2: the record with data remaining is later).
            return a.Data.AsSpan().SequenceCompareTo(b.Data);
        }
    }

    /// <summary>
    /// A received record's data, uncompressed, in the form this responder would
    /// write it; or null when it cannot be written back out.
    /// </summary>
    /// <remarks>
    /// Received names may be compressed on the wire, and RFC 6762 s8.2 compares
    /// data with names uncompressed, so decoded records are re-encoded by the
    /// same writer that produced this responder's own data. Types the reader
    /// does not decode are compared as the bytes that arrived.
    /// </remarks>
    private static byte[]? RdataOf(DnsRecord record)
    {
        DnsRecordPayload? payload = record.Type switch
        {
            DnsRecordType.A or DnsRecordType.Aaaa when record.Address is not null => new AddressPayload(record.Address),
            DnsRecordType.Ptr when record.PtrTarget is not null => new PtrPayload(record.PtrTarget),
            DnsRecordType.Srv when record.SrvTarget is not null =>
                new SrvPayload(record.SrvPriority, record.SrvWeight, record.SrvPort, record.SrvTarget),
            DnsRecordType.Txt when record.TxtStrings is not null => new TxtPayload(record.TxtStrings),
            _ => null,
        };

        if (payload is null)
        {
            return record.RawData;
        }

        try
        {
            return DnsRecordWriter.EncodeRdata(record.Type, payload);
        }
        catch (ArgumentException)
        {
            // Something decoded that the writer refuses - a label that
            // re-encodes longer than 63 bytes, say. It came from the network,
            // so it must not end the receive loop.
            return null;
        }
    }
}

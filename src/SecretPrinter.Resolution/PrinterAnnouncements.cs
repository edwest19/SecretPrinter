// -----------------------------------------------------------------------------
// PrinterAnnouncements.cs  (SecretPrinter.Resolution)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, for the SecretPrinter project, 2026-10-22, for REQ-RES-011.
// Reviewed by a human before merge.
//
// Purpose:
//   Hears the printer when it speaks without being asked. A printer that
//   starts announces its records (RFC 6762 s8.3), and every mDNS response it
//   multicasts, whatever question it answers, reaches every device on the
//   network. This class holds what such responses say about the configured
//   instance and its host, and says when that is enough to reach the printer:
//   the instance's SRV record and an address record for the host the SRV
//   names, each still within its TTL.
//
// Why it exists:
//   On 2026-10-21 the printer was restarted with a listener on its network. It
//   probed and then announced itself eight times in 2 minutes 7 seconds. The
//   service heard none of it unless one of its own questions happened to be
//   waiting, because PrinterResolver kept a datagram only for a lookup that
//   was waiting for it. After a long outage the service's questions are an
//   hour apart, so a printer that came back was found up to an hour later
//   (docs/findings/2026-10-21-the-printer-announces-itself-when-it-starts.md).
//   RFC 6762 s6: "Multicast DNS queriers receiving Multicast DNS responses do
//   not care what question elicited the response; they care only that the
//   information in the response is true and accurate."
//
// What is taken, and what is not (each rule from RFC 6762 where it says so):
//   - Only a datagram that arrived on the printer-side interface, as a lookup
//     takes only those (REQ-RES-006).
//   - Only one sent to the mDNS group. s6: "A Multicast DNS querier MUST only
//     accept unicast responses if they answer a recently sent query (e.g.,
//     sent within the last two seconds) that explicitly requested unicast
//     responses. A Multicast DNS querier MUST silently ignore all other
//     unicast responses." Nothing heard here answers a query, so every unicast
//     response is ignored. A datagram whose destination is not known is
//     ignored too.
//   - Only one from UDP port 5353. s6: "Multicast DNS implementations MUST
//     silently ignore any Multicast DNS responses they receive where the
//     source UDP port is not 5353."
//   - Only a response, never a question; records in a question's sections
//     are proposals or known answers, not the printer's word.
//   - Only records of class IN, from the Answer and Additional sections.
//   - A record with a TTL of 0 is a goodbye (s10.1). It is never taken as a
//     record held; it forgets the same record if it is held.
//   - An SRV or address record is held for its TTL, but never for longer than
//     ResolvedPrinter.LongestBelievedTtl, 120 seconds, the TTL RFC 6762 s10
//     says such records SHOULD carry (REQ-RES-013). Added by Claude
//     (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
//     2026-10-26; see
//     docs/findings/2026-10-26-an-answer-is-believed-for-at-most-120-seconds.md.
//     The TXT record is held for its own TTL: it decides only whether its
//     strings are carried, never whether the printer can be reached.
//
// Records held:
//   An announcement can spread its records over several datagrams; the
//   printer measured on 2026-10-21 sent each announcement as three, with its
//   address record in the first and the SRV in a later one. So records are
//   held, each with the moment it stops being valid (its TTL from when it was
//   heard, and for an SRV or address record at most 120 seconds, as above),
//   and the printer is reported heard when a datagram brings the
//   instance's SRV or an address record for the host it names, and the other
//   is already held and still valid. A datagram that brings neither reports
//   nothing, so traffic about other devices never does.
//
//   Address records are held for any host name, because the SRV that names
//   the host may come after them. At most MaximumHeldAddresses are held: one
//   whose TTL has run out is forgotten first, then the one heard longest ago,
//   so a device that sends records for many names cannot make this grow.
//
// What it reports:
//   A ResolvedPrinter whose lifetime is the shorter of the two records'
//   remaining lifetimes, counted from now, in whole seconds. TXT strings are
//   carried if the instance's TXT record is held, and are empty otherwise; the
//   service uses this answer only for whether the printer can be reached.
//
// What it does not do:
//   It sends nothing. It does not decide anything about reachability: the
//   caller is told, and PrinterReachability.RecordAnnouncement decides. It is
//   given datagrams only between lookups; a datagram that arrives while a
//   lookup is waiting goes to that lookup, as before.
//
// Threading:
//   Hear may be called from more than one resolver's reader at once, for a
//   moment, when PrinterSide replaces its resolver (REQ-RES-009); the state is
//   guarded by a lock. The caller's callback is invoked outside the lock.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Resolution;

/// <summary>
/// Holds the records the printer sends without being asked, and reports the
/// printer heard when they include the configured instance's SRV record and an
/// address record for the host it names, each within its TTL (README
/// REQ-RES-011).
/// </summary>
public sealed class PrinterAnnouncements
{
    /// <summary>The most address records held at once.</summary>
    public const int MaximumHeldAddresses = 16;

    // DNS class IN, without the cache-flush bit (RFC 6762 s10.2).
    private const ushort ClassInternet = 1;

    private readonly DnsName _instance;
    private readonly Action<ResolvedPrinter> _heard;
    private readonly TimeProvider _clock;

    // Guards everything below. Never held while the callback runs.
    private readonly object _sync = new();

    private Held<(DnsName Target, ushort Port)>? _srv;
    private Held<IReadOnlyList<string>>? _txt;
    private readonly Dictionary<DnsName, Held<IPAddress>> _addresses = [];

    /// <summary>One record held, and when it was heard and stops being valid.</summary>
    private sealed record Held<T>(T Value, DateTimeOffset HeardAt, DateTimeOffset ExpiresAt);

    /// <param name="instance">The configured <c>_ipps._tcp</c> instance to listen for.</param>
    /// <param name="heard">
    /// Told each time a datagram completes what is needed to reach the printer.
    /// Called on the reader that heard it, so it must return at once and must
    /// not throw; in the service it queues the answer for the watch.
    /// </param>
    /// <param name="clock">Supplied so lifetimes can be tested without waiting.</param>
    public PrinterAnnouncements(DnsName instance, Action<ResolvedPrinter> heard, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(heard);

        _instance = instance;
        _heard = heard;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The instance listened for.</summary>
    public DnsName Instance => _instance;

    /// <summary>How many address records are held now. Exposed for tests.</summary>
    public int HeldAddresses
    {
        get
        {
            lock (_sync)
            {
                return _addresses.Count;
            }
        }
    }

    /// <summary>
    /// Takes one datagram that arrived while no lookup was waiting for it, and
    /// reports the printer heard if it completes what is needed to reach it.
    /// Anything that does not pass the checks at the top of this file is
    /// ignored without a word, as RFC 6762 says.
    /// </summary>
    /// <param name="datagram">The datagram, as the transport delivered it.</param>
    /// <param name="printerInterface">The interface the resolver reads, facing the printer.</param>
    [Requirement("REQ-RES-011",
        "Takes only a response that arrived on the printer-side interface, was sent to the mDNS group, came from "
        + "UDP port 5353 and holds records of class IN; holds the instance's SRV and address records, each within "
        + "its TTL, across datagrams; forgets a record its goodbye names; and reports the printer heard when a "
        + "datagram completes the instance's SRV and an address record for the host it names.")]
    public void Hear(MdnsDatagram datagram, MdnsInterface printerInterface)
    {
        ArgumentNullException.ThrowIfNull(datagram);
        ArgumentNullException.ThrowIfNull(printerInterface);

        // Replies from any other interface are not about our printer
        // (REQ-RES-006).
        if (datagram.ArrivedOn is not { } arrivedOn || !arrivedOn.Matches(printerInterface))
        {
            return;
        }

        // RFC 6762 s6: an unsolicited unicast response is ignored, and so is a
        // datagram whose destination is not known.
        if (!datagram.SentToGroup)
        {
            return;
        }

        // RFC 6762 s6: a response from any port but 5353 is ignored.
        if (datagram.Source.Port != MdnsSocket.MdnsPort)
        {
            return;
        }

        DnsMessage message;
        try
        {
            message = DnsMessage.Parse(datagram.Payload, datagram.Payload.Length);
        }
        catch (InvalidDataException)
        {
            return;
        }

        if (!message.IsResponse)
        {
            return;
        }

        ResolvedPrinter? found;

        lock (_sync)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            bool brought = false;

            foreach (DnsRecord record in message.Answers.Concat(message.Additionals))
            {
                if ((record.RawClass & 0x7FFF) != ClassInternet)
                {
                    continue;
                }

                switch (record.Type)
                {
                    case DnsRecordType.Srv when record.Name.Equals(_instance) && record.SrvTarget is { } target:
                        brought |= TakeSrv(target, record.SrvPort, record.Ttl, now);
                        break;

                    case DnsRecordType.Txt when record.Name.Equals(_instance) && record.TxtStrings is { } strings:
                        TakeTxt(strings, record.Ttl, now);
                        break;

                    case DnsRecordType.A when record.Address is { } address:
                        brought |= TakeAddress(record.Name, address, record.Ttl, now);
                        break;
                }
            }

            found = brought ? Complete(now) : null;
        }

        if (found is not null)
        {
            _heard(found);
        }
    }

    /// <summary>Holds or forgets the instance's SRV. True when it was held now.</summary>
    private bool TakeSrv(DnsName target, ushort port, uint ttl, DateTimeOffset now)
    {
        if (ttl == 0)
        {
            // A goodbye (RFC 6762 s10.1) for the SRV held forgets it.
            if (_srv is { } held && held.Value.Target.Equals(target) && held.Value.Port == port)
            {
                _srv = null;
            }

            return false;
        }

        _srv = new Held<(DnsName, ushort)>((target, port), now, BelievedUntil(now, ttl));
        return true;
    }

    /// <summary>
    /// When an SRV or address record taken now stops being held: after its TTL,
    /// but never after <see cref="ResolvedPrinter.LongestBelievedTtl"/>.
    /// </summary>
    [Requirement("REQ-RES-013",
        "Holds an SRV or address record heard without being asked for its TTL, but never for longer than 120 "
        + "seconds.")]
    private static DateTimeOffset BelievedUntil(DateTimeOffset now, uint ttl) =>
        now + TimeSpan.FromSeconds(Math.Min(ttl, ResolvedPrinter.LongestBelievedTtl));

    /// <summary>Holds or forgets the instance's TXT strings.</summary>
    private void TakeTxt(IReadOnlyList<string> strings, uint ttl, DateTimeOffset now)
    {
        if (ttl == 0)
        {
            if (_txt is { } held && held.Value.SequenceEqual(strings, StringComparer.Ordinal))
            {
                _txt = null;
            }

            return;
        }

        _txt = new Held<IReadOnlyList<string>>(strings, now, now + TimeSpan.FromSeconds(ttl));
    }

    /// <summary>
    /// Holds or forgets one host's address. True when it was held now and the
    /// host is the one the SRV held names.
    /// </summary>
    private bool TakeAddress(DnsName host, IPAddress address, uint ttl, DateTimeOffset now)
    {
        if (ttl == 0)
        {
            if (_addresses.TryGetValue(host, out Held<IPAddress>? held) && held.Value.Equals(address))
            {
                _addresses.Remove(host);
            }

            return false;
        }

        if (!_addresses.ContainsKey(host))
        {
            MakeRoomForAnAddress(now);
        }

        _addresses[host] = new Held<IPAddress>(address, now, BelievedUntil(now, ttl));
        return _srv is { } srv && srv.Value.Target.Equals(host);
    }

    /// <summary>
    /// Keeps the number of addresses held below the cap: those whose TTL has
    /// run out go first, then the one heard longest ago.
    /// </summary>
    private void MakeRoomForAnAddress(DateTimeOffset now)
    {
        foreach (DnsName expired in _addresses.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
        {
            _addresses.Remove(expired);
        }

        while (_addresses.Count >= MaximumHeldAddresses)
        {
            DnsName oldest = _addresses.MinBy(entry => entry.Value.HeardAt).Key;
            _addresses.Remove(oldest);
        }
    }

    /// <summary>
    /// The printer as the records held say it can be reached, or null when the
    /// SRV or the address it needs is missing or has run out.
    /// </summary>
    private ResolvedPrinter? Complete(DateTimeOffset now)
    {
        if (_srv is not { } srv
            || !_addresses.TryGetValue(srv.Value.Target, out Held<IPAddress>? address))
        {
            return null;
        }

        // The shorter of the two lifetimes left. Under a second left, or none,
        // is no answer: a lifetime of 0 would make the printer reachable only
        // to lose it at once.
        DateTimeOffset expires = srv.ExpiresAt < address.ExpiresAt ? srv.ExpiresAt : address.ExpiresAt;
        TimeSpan left = expires - now;

        if (left < TimeSpan.FromSeconds(1))
        {
            return null;
        }

        uint ttl = (uint)Math.Floor(left.TotalSeconds);

        IReadOnlyList<string> txt = _txt is { } heldTxt && heldTxt.ExpiresAt > now ? heldTxt.Value : [];

        return new ResolvedPrinter(_instance, srv.Value.Target, address.Value, srv.Value.Port, txt, now, ttl);
    }
}

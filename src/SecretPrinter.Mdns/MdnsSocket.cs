// -----------------------------------------------------------------------------
// MdnsSocket.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// IPv6 groundwork for REQ-ADV-018 added by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// IPv6 socket opened and mDNS group joined by Claude (Anthropic model, Claude
// Opus 5) at the direction of Edwin West, 2026-09-06. Reviewed by a human
// before merge.
//
// Purpose:
//   The service's single point of contact with the network for mDNS. It binds
//   UDP 5353, joins the multicast group on configured interfaces, receives
//   datagrams with their arrival interface, and sends datagrams out a chosen
//   interface.
//
//   It knows nothing about printers, DNS records, or what should be
//   advertised. It moves bytes and reports where they came from. Everything
//   else is somebody else's job, which is what makes the security claims about
//   this layer checkable by reading one file.
//
// Where this design came from:
//   Not from first principles. The IPv4 socket is the exact configuration that
//   was measured working against a real iPhone on 2026-09-02
//   (docs/findings/2026-09-02-ios-accepts-advertisement.md), promoted from
//   tools/SecretPrinter.Respond with the option handling made explicit and the
//   interface confinement made mandatory rather than incidental.
//
//   The IPv6 socket is the configuration measured working on 2026-09-06
//   (docs/findings/2026-09-06-ipv6-mdns-transport.md), promoted from
//   tools/SecretPrinter.Respond6 - with one honest exception noted below.
//
// How far the IPv6 work has got:
//   This file now opens a second socket, binds it to [::]:5353 and joins
//   ff02::fb on the interfaces that asked for it. It does NOT yet receive on
//   that socket, and it does NOT yet send on it. So:
//
//     REQ-ADV-018 (receive and answer over IPv6)  - NOT met. Nothing is read
//                                                   from the IPv6 socket.
//     REQ-ADV-019 (hop limit 255)                 - option set below, but the
//                                                   requirement is about
//                                                   OUTGOING datagrams and none
//                                                   go out yet.
//     REQ-ADV-020 (arrival from IPV6_PKTINFO)     - option set below, but
//                                                   nothing reads an arrival
//                                                   interface from it yet.
//
//   None of the three carries a [Requirement] marker here for that reason. A
//   marker on a set option would make the coverage matrix report a behaviour
//   the service does not have, which is the one thing this project's matrix
//   exists to prevent.
//
// One thing in the IPv6 configuration is NOT measured:
//   Respond6 named a single interface on its command line and therefore never
//   set IPV6_PKTINFO. Arrival attribution over IPv6 is new ground here. The
//   option is set; that it reports a usable interface index on Windows has not
//   been observed by anyone on this project and must not be assumed.
//
// One socket per family, not one per interface:
//   Each family has a single socket bound to its wildcard address, handling
//   both receiving and sending, with the multicast egress interface set per
//   send under a semaphore. The alternative - a send socket per interface -
//   avoids the option race without a lock, but changes the configuration away
//   from the one actually verified against a client. Sends here are small and
//   infrequent; serialising them costs nothing and keeps the wire behaviour
//   identical to what was measured.
//
// Why the IPv6 socket is not dual-mode:
//   Both sockets bind port 5353. A dual-mode IPv6 socket receives IPv4 traffic
//   as well, so every IPv4 datagram would arrive twice - once on each socket -
//   and be answered twice. IPV6_V6ONLY is therefore set, before the bind, and
//   read back so a test can prove it.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using SecretPrinter.Spec;

namespace SecretPrinter.Mdns;

/// <summary>A received datagram, with the interface it arrived on.</summary>
/// <param name="Payload">The bytes received. Owned by the caller.</param>
/// <param name="Source">Where it came from, including source port.</param>
/// <param name="InterfaceIndex">OS interface index the datagram arrived on.</param>
/// <param name="ArrivedOn">
/// The configured interface matching <paramref name="InterfaceIndex"/>, or null
/// when the datagram arrived on an interface the service was not configured
/// for. Callers must treat null as "not for us".
/// </param>
public sealed record MdnsDatagram(
    byte[] Payload,
    IPEndPoint Source,
    int InterfaceIndex,
    MdnsInterface? ArrivedOn)
{
    /// <summary>
    /// True when the sender used a port other than 5353, marking it a legacy
    /// unicast querier under RFC 6762 §6.7. Such queriers must be answered
    /// directly, with the query identifier echoed and TTLs capped.
    /// </summary>
    [Requirement("REQ-ADV-017",
        "Identifies a legacy unicast querier by source port, which is the detection half of the requirement. Answering by unicast is the responder's half.")]
    public bool IsLegacyUnicastQuerier => Source.Port != MdnsSocket.MdnsPort;
}

/// <summary>The IPv4 socket options actually in effect, read back from the OS.</summary>
/// <remarks>
/// Read back rather than remembered. Code that records what it asked for proves
/// nothing; this reports what the operating system actually did, which is what
/// makes it usable both for startup logging and for tests.
/// </remarks>
public sealed record MdnsSocketConfiguration(
    bool ReuseAddress,
    bool ExclusiveAddressUse,
    bool PacketInformation,
    int MulticastTimeToLive,
    IPEndPoint LocalEndPoint,
    IReadOnlyList<MdnsInterface> Interfaces);

/// <summary>The IPv6 socket options actually in effect, read back from the OS.</summary>
/// <remarks>
/// A separate record rather than a reuse of <see cref="MdnsSocketConfiguration"/>,
/// for two reasons. The hop limit is not a TTL and calling it one would invite a
/// reader to assume the two families are configured identically. And IPv6 has
/// two claims of its own worth proving - that the socket is not dual-mode, and
/// that it is bound to the IPv6 wildcard - which have no IPv4 counterpart.
/// </remarks>
public sealed record MdnsIPv6SocketConfiguration(
    bool ReuseAddress,
    bool ExclusiveAddressUse,
    bool DualMode,
    bool PacketInformation,
    int MulticastHopLimit,
    IPEndPoint LocalEndPoint,
    IReadOnlyList<MdnsInterface> Interfaces);

/// <summary>Binds UDP 5353 and moves mDNS datagrams. Knows nothing about their contents.</summary>
public sealed class MdnsSocket : IMdnsTransport, IDisposable
{
    /// <summary>The IPv4 multicast group for mDNS (RFC 6762 §3).</summary>
    public static readonly IPAddress MulticastGroup = IPAddress.Parse("224.0.0.251");

    /// <summary>The IPv6 link-local multicast group for mDNS (RFC 6762 §3).</summary>
    public static readonly IPAddress MulticastGroupV6 = IPAddress.Parse("ff02::fb");

    /// <summary>The mDNS port (RFC 6762 §3).</summary>
    public const int MdnsPort = 5353;

    /// <summary>
    /// RFC 6762 §11 requires mDNS packets to carry IP TTL 255. Receivers may
    /// check it, and a lower value invites silent, hard-to-diagnose drops.
    /// </summary>
    public const int RequiredMulticastTtl = 255;

    /// <summary>
    /// The IPv6 counterpart of <see cref="RequiredMulticastTtl"/>. RFC 6762 §11
    /// requires hop limit 255 so a receiver can reject anything that has been
    /// routed. Stated as its own constant because a hop limit is not a TTL, and
    /// a shared name would suggest the two families are configured together
    /// when they are set through different socket options at different levels.
    /// </summary>
    public const int RequiredMulticastHopLimit = 255;

    private const int ReceiveBufferSize = 9000;

    private readonly Socket _socket;
    private readonly Socket? _socket6;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Keyed by family AND index, and by family AND address. Windows numbers the
    // address families separately, so one adapter has two indexes and the same
    // integer can name different interfaces in each family. Keying on the number
    // alone would silently attribute an IPv6 datagram to an IPv4 interface. The
    // string half of the address key compares ordinally, which is the default
    // for string inside a ValueTuple key.
    private readonly Dictionary<(AddressFamily Family, int Index), MdnsInterface> _byIndex;
    private readonly Dictionary<(AddressFamily Family, string Address), MdnsInterface> _byAddress;

    private bool _disposed;

    private MdnsSocket(
        Socket socket,
        Socket? socket6,
        IReadOnlyList<MdnsInterface> ipv4Interfaces,
        IReadOnlyList<MdnsInterface> ipv6Interfaces)
    {
        _socket = socket;
        _socket6 = socket6;
        IPv4Interfaces = ipv4Interfaces;
        IPv6Interfaces = ipv6Interfaces;
        Interfaces = [.. ipv4Interfaces, .. ipv6Interfaces];

        _byIndex = Interfaces.ToDictionary(i => (i.Transport, i.Index));
        _byAddress = Interfaces.ToDictionary(i => (i.Transport, i.Address.ToString()));
    }

    /// <summary>
    /// Every interface this socket joined, IPv4 entries first and then their
    /// IPv6 companions.
    /// </summary>
    /// <remarks>
    /// One adapter can appear twice, once per family, with a different index
    /// each time. Anything matching an entry against this list must compare the
    /// family as well as the index - use <see cref="MdnsInterface.Matches"/>
    /// rather than comparing <see cref="MdnsInterface.Index"/> alone.
    /// </remarks>
    public IReadOnlyList<MdnsInterface> Interfaces { get; }

    /// <summary>The IPv4 entries, in configuration order.</summary>
    public IReadOnlyList<MdnsInterface> IPv4Interfaces { get; }

    /// <summary>
    /// The IPv6 entries, in configuration order. Empty when no binding asked to
    /// join IPv6, in which case no IPv6 socket was opened at all.
    /// </summary>
    public IReadOnlyList<MdnsInterface> IPv6Interfaces { get; }

    /// <summary>
    /// Binds UDP 5353 and joins the mDNS group on each supplied interface: the
    /// IPv4 group on all of them, and the IPv6 group on those whose binding
    /// asked for it.
    /// </summary>
    /// <exception cref="MdnsInterfaceException">An interface could not be resolved or used.</exception>
    /// <exception cref="SocketException">Binding or joining failed at the OS level.</exception>
    [Requirement("REQ-ADV-014",
        "Binds with SO_REUSEADDR and ExclusiveAddressUse false, so the port is shared with the Windows DNS Client rather than seized.")]
    [Requirement("REQ-ADV-013",
        "Sets IP_MULTICAST_TTL to 255 on the sending socket.")]
    [Requirement("REQ-ADV-015",
        "Enables IP_PKTINFO so the arrival interface of each datagram is reported by the OS rather than inferred.")]
    [Requirement("REQ-LIF-004",
        "Any bind or join failure propagates instead of leaving the socket partially configured.")]
    public static MdnsSocket Open(IReadOnlyList<MdnsBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        IReadOnlyList<MdnsInterface> ipv4 =
            MdnsInterfaceResolver.ResolveAll([.. bindings.Select(binding => binding.Address)]);

        // Matched by address rather than by list position. ResolveAll does
        // preserve order today, but a caller reading this file should not have
        // to know that to believe the pairing is right. ResolveAll has already
        // rejected duplicate addresses, so each lookup has exactly one answer.
        Dictionary<string, MdnsInterface> byAddress =
            ipv4.ToDictionary(entry => entry.Address.ToString(), StringComparer.Ordinal);

        var ipv6 = new List<MdnsInterface>();
        foreach (MdnsBinding binding in bindings)
        {
            if (binding.JoinIPv6)
            {
                ipv6.Add(MdnsInterfaceResolver.ResolveIPv6(byAddress[binding.Address.ToString()]));
            }
        }

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        Socket? socket6 = null;
        try
        {
            // Both of these must precede Bind. On Windows ExclusiveAddressUse
            // defaults to true for a socket created this way, which would
            // defeat SO_REUSEADDR and make the bind fail against Dnscache.
            socket.ExclusiveAddressUse = false;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Wildcard bind. A socket bound to one unicast address is not
            // reliable for receiving multicast on Windows; the arrival
            // interface is recovered from IP_PKTINFO instead.
            socket.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));

            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
            socket.SetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, RequiredMulticastTtl);

            foreach (MdnsInterface joined in ipv4)
            {
                socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.AddMembership,
                    new MulticastOption(MulticastGroup, joined.Address));
            }

            if (ipv6.Count > 0)
            {
                socket6 = OpenIPv6(ipv6);
            }

            return new MdnsSocket(socket, socket6, ipv4, ipv6);
        }
        catch
        {
            // Fail fast and clean: a half-configured socket that silently
            // receives nothing is worse than an exception at startup.
            socket6?.Dispose();
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens the IPv6 socket and joins ff02::fb on each supplied interface.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Open"/> so that a reader auditing the IPv6
    /// claims has one short method to read, and so that a failure here disposes
    /// its own socket before the caller disposes the IPv4 one.
    /// </remarks>
    private static Socket OpenIPv6(IReadOnlyList<MdnsInterface> interfaces)
    {
        var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            // As on IPv4: port 5353 is shared with the Windows DNS Client and
            // any other mDNS participant on this host, so it is shared rather
            // than seized. Both options must precede Bind.
            socket.ExclusiveAddressUse = false;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // IPV6_V6ONLY, and it must also precede Bind. See the note at the
            // top of this file: a dual-mode socket would receive the IPv4
            // traffic the IPv4 socket already receives, duplicating every
            // datagram on this host.
            socket.DualMode = false;

            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, MdnsPort));

            // Set here for the same reason as their IPv4 counterparts, and read
            // back by ReadBackIPv6Configuration so a test can prove it. Neither
            // does anything yet: nothing receives on this socket and nothing
            // sends on it. REQ-ADV-019 and REQ-ADV-020 remain unmet.
            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.PacketInformation, true);
            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.MulticastTimeToLive,
                RequiredMulticastHopLimit);

            foreach (MdnsInterface joined in interfaces)
            {
                // IPv6 membership is by interface index, not by local address.
                // The index here is the adapter's IPv6 index, which differs from
                // its IPv4 index on Windows; MdnsInterfaceResolver.ResolveIPv6
                // is what puts the right one in this entry.
                socket.SetSocketOption(
                    SocketOptionLevel.IPv6,
                    SocketOptionName.AddMembership,
                    new IPv6MulticastOption(MulticastGroupV6, joined.Index));
            }

            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the IPv4 socket options actually in effect back from the operating
    /// system. Used for startup logging and for tests that assert the socket is
    /// configured as the specification requires.
    /// </summary>
    public MdnsSocketConfiguration ReadBackConfiguration()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new MdnsSocketConfiguration(
            ReuseAddress: (int)_socket.GetSocketOption(
                SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)! != 0,
            ExclusiveAddressUse: _socket.ExclusiveAddressUse,
            PacketInformation: (int)_socket.GetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.PacketInformation)! != 0,
            MulticastTimeToLive: (int)_socket.GetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive)!,
            LocalEndPoint: (IPEndPoint)_socket.LocalEndPoint!,
            Interfaces: IPv4Interfaces);
    }

    /// <summary>
    /// Reads the IPv6 socket options back from the operating system, or returns
    /// null when no binding asked to join IPv6 and no IPv6 socket was opened.
    /// </summary>
    /// <remarks>
    /// Null means "there is no IPv6 socket", which is a legitimate configuration
    /// - the printer interface alone, for instance. It does not mean the read
    /// failed. A caller logging startup should say so plainly rather than print
    /// nothing.
    /// </remarks>
    public MdnsIPv6SocketConfiguration? ReadBackIPv6Configuration()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_socket6 is null)
        {
            return null;
        }

        return new MdnsIPv6SocketConfiguration(
            ReuseAddress: (int)_socket6.GetSocketOption(
                SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)! != 0,
            ExclusiveAddressUse: _socket6.ExclusiveAddressUse,
            DualMode: _socket6.DualMode,
            PacketInformation: (int)_socket6.GetSocketOption(
                SocketOptionLevel.IPv6, SocketOptionName.PacketInformation)! != 0,
            MulticastHopLimit: (int)_socket6.GetSocketOption(
                SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive)!,
            LocalEndPoint: (IPEndPoint)_socket6.LocalEndPoint!,
            Interfaces: IPv6Interfaces);
    }

    /// <summary>
    /// Waits for one datagram, on IPv4 only.
    /// </summary>
    /// <remarks>
    /// Datagrams arriving on an interface the service was not configured for
    /// are still returned, with <see cref="MdnsDatagram.ArrivedOn"/> null. They
    /// are not silently dropped here because the caller may want to count them;
    /// but a null <c>ArrivedOn</c> means the datagram must not be answered.
    ///
    /// The IPv6 socket is joined to ff02::fb but is not read here. Until it is,
    /// a query that arrives only over IPv6 is received by the operating system
    /// and then discarded when the socket buffer fills - which is precisely the
    /// fault recorded in docs/findings/2026-09-06-ipv6-mdns-transport.md, now
    /// one step closer to fixed rather than fixed. REQ-ADV-018 is unmet.
    /// </remarks>
    [Requirement("REQ-ADV-015",
        "Uses ReceiveMessageFromAsync with IP_PKTINFO to report the true arrival interface of each datagram.")]
    public async Task<MdnsDatagram> ReceiveAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var buffer = new byte[ReceiveBufferSize];

        SocketReceiveMessageFromResult result = await _socket.ReceiveMessageFromAsync(
            buffer,
            SocketFlags.None,
            new IPEndPoint(IPAddress.Any, 0),
            cancellationToken).ConfigureAwait(false);

        var payload = new byte[result.ReceivedBytes];
        Array.Copy(buffer, payload, result.ReceivedBytes);

        int index = result.PacketInformation.Interface;
        _byIndex.TryGetValue((AddressFamily.InterNetwork, index), out MdnsInterface? arrivedOn);

        return new MdnsDatagram(payload, (IPEndPoint)result.RemoteEndPoint, index, arrivedOn);
    }

    /// <summary>
    /// Sends a datagram to the mDNS multicast group, out of one specific
    /// interface.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The interface was not one this socket was opened for. This is a hard
    /// error rather than a fallback: on a dual-homed host, sending out the
    /// wrong interface means advertising onto the printer network, which is
    /// precisely what this project must never do.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The interface is an IPv6 entry. Sending over IPv6 is REQ-ADV-018 and is
    /// not implemented yet.
    /// </exception>
    [Requirement("REQ-ADV-011",
        "Refuses to send out any interface the socket was not opened for, so advertisement cannot leak onto an unconfigured network.")]
    [Requirement("REQ-ADV-013",
        "Reasserts IP_MULTICAST_TTL 255 immediately before each multicast send.")]
    public Task SendMulticastAsync(
        ReadOnlyMemory<byte> payload, MdnsInterface via, CancellationToken cancellationToken) =>
        SendAsync(payload, new IPEndPoint(MulticastGroup, MdnsPort), via, cancellationToken);

    /// <summary>
    /// Sends a datagram directly to one endpoint, out of one specific
    /// interface. Used for legacy unicast queriers (RFC 6762 §6.7).
    /// </summary>
    [Requirement("REQ-ADV-011",
        "Refuses to send out any interface the socket was not opened for.")]
    public Task SendUnicastAsync(
        ReadOnlyMemory<byte> payload,
        IPEndPoint destination,
        MdnsInterface via,
        CancellationToken cancellationToken) =>
        SendAsync(payload, destination, via, cancellationToken);

    private async Task SendAsync(
        ReadOnlyMemory<byte> payload,
        IPEndPoint destination,
        MdnsInterface via,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(via);

        // Confinement is checked first and unconditionally. It is the check that
        // keeps advertisement off the printer network, and no later condition
        // may short-circuit it.
        if (!_byAddress.TryGetValue((via.Transport, via.Address.ToString()), out MdnsInterface? known)
            || !known.Matches(via))
        {
            throw new ArgumentException(
                $"Interface {via} was not opened by this socket. Opened interfaces: "
                + string.Join(", ", Interfaces.Select(i => i.ToString())),
                nameof(via));
        }

        if (via.IsIPv6)
        {
            // The IPv6 socket exists and has joined the group, but nothing has
            // been written to make it send: IPV6_MULTICAST_IF takes an interface
            // index rather than the address bytes used below, and no such send
            // has been measured. Refusing loudly is the only honest option -
            // falling through would transmit over IPv4 while the caller believed
            // it had asked for IPv6.
            throw new NotSupportedException(
                $"Sending over IPv6 ({via}) is not implemented. REQ-ADV-018 is unmet; "
                + "see the header of MdnsSocket.cs.");
        }

        // IP_MULTICAST_IF is socket-wide state, so sends are serialised. The
        // option is set immediately before each send rather than once at open,
        // because a socket serving several interfaces would otherwise send out
        // whichever interface was configured last.
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _socket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                via.Address.GetAddressBytes());

            _socket.SetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, RequiredMulticastTtl);

            await _socket.SendToAsync(payload, SocketFlags.None, destination, cancellationToken)
                         .ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Leaves every joined multicast group, then closes both sockets.</summary>
    /// <remarks>
    /// Leaving groups explicitly rather than relying on socket closure is
    /// deliberate: it tells the local network stack immediately, rather than
    /// leaving membership to time out. On IPv6 that means an MLD Done report
    /// leaves the client network at shutdown, which is a visible, checkable
    /// sign that the service stopped cleanly.
    /// </remarks>
    [Requirement("REQ-LIF-002",
        "Leaves each multicast group and disposes the socket on shutdown.")]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (MdnsInterface joined in IPv4Interfaces)
        {
            try
            {
                _socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.DropMembership,
                    new MulticastOption(MulticastGroup, joined.Address));
            }
            catch (SocketException)
            {
                // The socket may already be unusable, for instance if the
                // adapter was removed. Shutdown continues regardless; a failed
                // leave is not worth aborting a clean stop.
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        if (_socket6 is not null)
        {
            foreach (MdnsInterface joined in IPv6Interfaces)
            {
                try
                {
                    _socket6.SetSocketOption(
                        SocketOptionLevel.IPv6,
                        SocketOptionName.DropMembership,
                        new IPv6MulticastOption(MulticastGroupV6, joined.Index));
                }
                catch (SocketException)
                {
                    // Same reasoning as the IPv4 loop above.
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }

            _socket6.Dispose();
        }

        _socket.Dispose();
        _sendLock.Dispose();
    }
}

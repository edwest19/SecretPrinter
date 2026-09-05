// -----------------------------------------------------------------------------
// MdnsSocket.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
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
//   Not from first principles. This is the exact socket configuration that was
//   measured working against a real iPhone on 2026-09-02
//   (docs/findings/2026-09-02-ios-accepts-advertisement.md), promoted from
//   tools/SecretPrinter.Respond with the option handling made explicit and the
//   interface confinement made mandatory rather than incidental.
//
// One socket, not one per interface:
//   A single socket bound to the wildcard address handles both receiving and
//   sending, with IP_MULTICAST_IF set per send under a semaphore. The
//   alternative - a send socket per interface - avoids the option race without
//   a lock, but changes the configuration away from the one actually verified
//   against a client. Sends here are small and infrequent; serialising them
//   costs nothing and keeps the wire behaviour identical to what was measured.
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

/// <summary>The socket options actually in effect, read back from the OS.</summary>
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

/// <summary>Binds UDP 5353 and moves mDNS datagrams. Knows nothing about their contents.</summary>
public sealed class MdnsSocket : IMdnsTransport, IDisposable
{
    /// <summary>The IPv4 multicast group for mDNS (RFC 6762 §3).</summary>
    public static readonly IPAddress MulticastGroup = IPAddress.Parse("224.0.0.251");

    /// <summary>The mDNS port (RFC 6762 §3).</summary>
    public const int MdnsPort = 5353;

    /// <summary>
    /// RFC 6762 §11 requires mDNS packets to carry IP TTL 255. Receivers may
    /// check it, and a lower value invites silent, hard-to-diagnose drops.
    /// </summary>
    public const int RequiredMulticastTtl = 255;

    private const int ReceiveBufferSize = 9000;

    private readonly Socket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Dictionary<int, MdnsInterface> _byIndex;
    private readonly Dictionary<string, MdnsInterface> _byAddress;
    private bool _disposed;

    private MdnsSocket(Socket socket, IReadOnlyList<MdnsInterface> interfaces)
    {
        _socket = socket;
        Interfaces = interfaces;
        _byIndex = interfaces.ToDictionary(i => i.Index);
        _byAddress = interfaces.ToDictionary(i => i.Address.ToString(), StringComparer.Ordinal);
    }

    /// <summary>The interfaces this socket joined, in configuration order.</summary>
    public IReadOnlyList<MdnsInterface> Interfaces { get; }

    /// <summary>
    /// Binds UDP 5353 and joins the mDNS group on each supplied interface.
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
    public static MdnsSocket Open(IReadOnlyList<IPAddress> interfaceAddresses)
    {
        IReadOnlyList<MdnsInterface> interfaces = MdnsInterfaceResolver.ResolveAll(interfaceAddresses);

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
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

            foreach (MdnsInterface joined in interfaces)
            {
                socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.AddMembership,
                    new MulticastOption(MulticastGroup, joined.Address));
            }

            return new MdnsSocket(socket, interfaces);
        }
        catch
        {
            // Fail fast and clean: a half-configured socket that silently
            // receives nothing is worse than an exception at startup.
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the socket options actually in effect back from the operating
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
            Interfaces: Interfaces);
    }

    /// <summary>
    /// Waits for one datagram.
    /// </summary>
    /// <remarks>
    /// Datagrams arriving on an interface the service was not configured for
    /// are still returned, with <see cref="MdnsDatagram.ArrivedOn"/> null. They
    /// are not silently dropped here because the caller may want to count them;
    /// but a null <c>ArrivedOn</c> means the datagram must not be answered.
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
        _byIndex.TryGetValue(index, out MdnsInterface? arrivedOn);

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

        if (!_byAddress.TryGetValue(via.Address.ToString(), out MdnsInterface? known)
            || known.Index != via.Index)
        {
            throw new ArgumentException(
                $"Interface {via} was not opened by this socket. Opened interfaces: "
                + string.Join(", ", Interfaces.Select(i => i.ToString())),
                nameof(via));
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

    /// <summary>Leaves every joined multicast group, then closes the socket.</summary>
    /// <remarks>
    /// Leaving groups explicitly rather than relying on socket closure is
    /// deliberate: it tells the local network stack immediately, rather than
    /// leaving membership to time out.
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

        foreach (MdnsInterface joined in Interfaces)
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

        _socket.Dispose();
        _sendLock.Dispose();
    }
}

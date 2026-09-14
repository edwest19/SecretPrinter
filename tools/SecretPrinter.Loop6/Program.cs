// -----------------------------------------------------------------------------
// Program.cs
//
// SecretPrinter.Loop6 - a loopback measurement. It sends one small datagram to
// ff02::fb from this host and reads what arrives back on this host.
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-14, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// THE ONE QUESTION THIS TOOL EXISTS TO ANSWER
// -------------------------------------------
// When a socket on this host sends to ff02::fb, and the operating system loops
// a copy back to another socket on the same host that has joined that group on
// a real adapter, what does IPV6_PKTINFO report as the arrival interface?
//
// Two answers are possible and they lead to different places:
//
//   the adapter's IPv6 index  - a test can send to the group from a second
//                               socket and assert that MdnsSocket.ReceiveAsync
//                               attributed the datagram to the right interface.
//                               REQ-ADV-020 becomes testable in code.
//
//   1, or 0, or anything else - the same test would look up an index the socket
//                               never joined on, get no match, and fail against
//                               correct product code. That test design is then
//                               unavailable and the socket half of REQ-ADV-018
//                               and REQ-ADV-020 has to be covered some other
//                               way.
//
// SecretPrinter.Listen6 measured that IPV6_PKTINFO reports a usable index for
// datagrams arriving from the link (docs/findings/2026-09-13-ipv6-pktinfo-
// arrival.md, 62 datagrams). It says nothing about a locally looped copy, which
// never travels on the link and may well be attributed differently. That is the
// gap this tool closes, and the reason it sends where Listen6 refuses to.
//
// WHY BOTH SOCKETS ARE IN ONE PROCESS
// -----------------------------------
// Because the test whose feasibility is being measured would be in one process.
// Measuring across two processes would measure something adjacent to the
// question and invite the answer to be carried over to a case it was not taken
// from, which is the failure this project recorded on 2026-09-13 in
// docs/findings/2026-09-13-interface-index-parity.md.
//
// WHAT THIS TOOL PUTS ON THE NETWORK
// ----------------------------------
// By default, nothing.
//
// The hop limit defaults to 0. An IPv6 multicast datagram with hop limit 0 is
// confined to the sending node and is not transmitted on the link, while
// IPV6_MULTICAST_LOOP governs local delivery independently of it. That is how
// the IPv4 equivalent (TTL 0) is long established to behave and how IPv6 is
// specified to behave.
//
// It has NOT been verified on Windows by anyone on this project. So:
//
//   - the hop limit is a command-line option, not a constant;
//   - a run at hop limit 0 in which nothing arrives is reported as INCONCLUSIVE,
//     never as a negative result for loopback;
//   - --hops 255 makes the datagram leave the adapter, and the program says so
//     on screen before it sends.
//
// The payload is deliberately not a DNS message. It is an ASCII marker and a
// random nonce. Should it ever reach the link, no mDNS implementation can act
// on it: RFC 6762 requires a malformed message to be silently ignored. The
// nonce is also what lets the receiver pick its own datagram out of whatever
// else is arriving on ff02::fb without having to parse any of it.
//
// WHAT IT CANNOT ANSWER
// ---------------------
// Which family's numbering the reported index came from. Every adapter this
// project has measured reports the same index in both families, so no
// observation here can separate those hypotheses. Same limitation as Listen6,
// same reason: docs/findings/2026-09-13-interface-index-parity.md.
//
// Whether the product's own socket would attribute the same way. This tool
// duplicates the product's socket configuration; it does not call it. A match
// here makes the test design available. It does not make the test unnecessary.
//
// PRIVACY
// -------
// The receiver is joined to ff02::fb on a real adapter, so it sees the link's
// ordinary mDNS traffic, which names devices. Datagrams that do not carry this
// run's nonce are COUNTED AND OTHERWISE DISCARDED - no address, no port, no
// bytes, nothing printed. That is a deliberate difference from Listen6, whose
// job was to characterise real traffic. A transcript from this tool contains
// only this machine's own addresses and is safe to paste into a finding.
//
// Nothing is written to disk. No elevation is needed. No registry key is read
// or written.
//
// FIDELITY TO THE PRODUCT
// -----------------------
// The receiving socket below is opened with the same options, in the same
// order, as MdnsSocket.OpenIPv6 in src/SecretPrinter.Mdns/MdnsSocket.cs:
//
//   product                                              here
//   --------------------------------------------------   ------------
//   ExclusiveAddressUse = false                          (1)
//   SO_REUSEADDR = true                                  (2)
//   DualMode = false          (IPV6_V6ONLY, before bind) (3)
//   Bind([::]:5353)                                      (4)
//   IPV6_PKTINFO = true                                  (5)
//   IPV6_MULTICAST_HOPS = 255                            (6)
//   AddMembership(ff02::fb, adapter's IPv6 index)        (7)
//
// Written out in full rather than shared with SecretPrinter.Mdns, for the same
// reason Listen6 does it: what is being measured must not silently change when
// the product changes. The duplication is on purpose and the correspondence is
// listed above for a reader to check line by line.
//
// The SENDING socket is not a copy of anything in the product. The product
// sends and receives on one socket; this sends from a second one, because that
// is what a test would do.
//
// A NOTE FOR WHOEVER WRITES THE TEST
// ----------------------------------
// The sending socket binds an ephemeral port, so the datagram arrives with a
// source port that is not 5353. Through the product that makes
// MdnsDatagram.IsLegacyUnicastQuerier true (RFC 6762 6.7), which changes how a
// responder answers. A test built on this pattern must either bind its sender
// to 5353 as well - the port is shareable, that is why SO_REUSEADDR is set - or
// expect the legacy-unicast path. It is not a problem, but it is a trap.
//
// NO REQUIREMENT MARKERS
// ----------------------
// Deliberately, as with Listen6, Respond and Respond6. The requirements in
// README.md describe the service. A marker here would make the coverage matrix
// report a service behaviour when what exists is a measurement of the operating
// system.
//
// EXIT CODES
// ----------
//   0  at least one probe datagram came back
//   1  none came back (see the verdict on screen for what that does and does
//      not mean)
//   2  usage or configuration error
//   3  socket error
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SecretPrinter.Loop6;

internal static class Program
{
    /// <summary>The IPv6 link-local multicast group for mDNS (RFC 6762 §3).</summary>
    private static readonly IPAddress MdnsGroupV6 = IPAddress.Parse("ff02::fb");

    /// <summary>
    /// Prefix identifying a datagram from this tool. Not a DNS header, and not
    /// mistakable for one: a DNS message begins with a 16-bit identifier and
    /// flags, never with printable ASCII of this shape.
    /// </summary>
    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("SECRETPRINTER-LOOP6-PROBE-V1");

    private const int MdnsPort = 5353;
    private const int ReceiverHopLimit = 255;
    private const int BufferSize = 9000;
    private const int NonceBytes = 16;

    private const int DefaultSendHopLimit = 0;
    private const int DefaultDatagrams = 3;
    private const int DefaultTimeoutSeconds = 5;

    private static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? ipv4Text = null;
        int hops = DefaultSendHopLimit;
        int count = DefaultDatagrams;
        int timeout = DefaultTimeoutSeconds;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--ipv4", StringComparison.Ordinal))
            {
                ipv4Text = args[i + 1];
            }
            else if (string.Equals(args[i], "--hops", StringComparison.Ordinal)
                && TryParseInt(args[i + 1], out int parsedHops))
            {
                hops = parsedHops;
            }
            else if (string.Equals(args[i], "--count", StringComparison.Ordinal)
                && TryParseInt(args[i + 1], out int parsedCount))
            {
                count = parsedCount;
            }
            else if (string.Equals(args[i], "--timeout", StringComparison.Ordinal)
                && TryParseInt(args[i + 1], out int parsedTimeout))
            {
                timeout = parsedTimeout;
            }
        }

        if (ipv4Text is null)
        {
            Console.Error.WriteLine(
                "usage: --ipv4 <IPv4 address of the interface to join and send on>");
            Console.Error.WriteLine(
                "       [--hops <0-255, default 0>] [--count <default 3>] [--timeout <seconds, default 5>]");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "  --hops 0 keeps the datagram on this host. Any higher value transmits it on the link.");
            return 2;
        }

        if (!IPAddress.TryParse(ipv4Text, out IPAddress? ipv4)
            || ipv4.AddressFamily != AddressFamily.InterNetwork)
        {
            Console.Error.WriteLine($"'{ipv4Text}' is not an IPv4 address.");
            return 2;
        }

        if (hops is < 0 or > 255)
        {
            Console.Error.WriteLine($"--hops must be between 0 and 255; got {hops}.");
            return 2;
        }

        // Upper bound because the sequence number is one byte of the payload.
        // A higher count would wrap it and two probes could match each other.
        if (count is < 1 or > 64)
        {
            Console.Error.WriteLine($"--count must be between 1 and 64; got {count}.");
            return 2;
        }

        if (timeout < 1)
        {
            Console.Error.WriteLine($"--timeout must be at least 1 second; got {timeout}.");
            return 2;
        }

        try
        {
            return await RunAsync(ipv4, hops, count, timeout).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Socket error: {ex.Message} (code {ex.SocketErrorCode}).");
            return 3;
        }
    }

    private static async Task<int> RunAsync(IPAddress ipv4, int hops, int count, int timeout)
    {
        // The adapter is identified by an IPv4 address, the same way the service
        // is configured, and then BOTH family indexes are read from it. Both are
        // printed because their relationship is exactly what a reader needs in
        // order to judge what this run can and cannot show.
        (string? adapterName, int? ipv4Index, int? ipv6Index) = FindAdapter(ipv4);

        if (adapterName is null)
        {
            Console.Error.WriteLine($"No adapter on this machine holds {ipv4}.");
            return 2;
        }

        if (ipv6Index is not { } joinIndex)
        {
            Console.Error.WriteLine(
                $"Adapter '{adapterName}' holds {ipv4} but reports no IPv6 index, "
                + $"so it cannot join {MdnsGroupV6}.");
            return 2;
        }

        Console.WriteLine("SecretPrinter IPv6 multicast loopback probe.");
        Console.WriteLine();
        Console.WriteLine($"  Machine          : {Environment.MachineName}");
        Console.WriteLine($"  OS               : {Environment.OSVersion.VersionString}");
        Console.WriteLine($"  Adapter          : {adapterName}");
        Console.WriteLine($"  IPv4 address     : {ipv4}");
        Console.WriteLine($"  IPv4 index       : {(ipv4Index is { } v4 ? v4 : 0)}");
        Console.WriteLine($"  IPv6 index       : {joinIndex}");
        Console.WriteLine($"  Expected arrival : {joinIndex}");

        if (ipv4Index == joinIndex)
        {
            Console.WriteLine();
            Console.WriteLine("  NOTE: this adapter reports the SAME index in both families.");
            Console.WriteLine("        A match below shows that a usable index is reported.");
            Console.WriteLine("        It CANNOT show which family's numbering it came from.");
        }

        Console.WriteLine();

        if (hops == 0)
        {
            Console.WriteLine("Hop limit 0: the datagram should stay on this host and not reach the link.");
            Console.WriteLine("That expectation is what this run also tests. If nothing arrives, the");
            Console.WriteLine("result is INCONCLUSIVE, not negative - re-run with --hops 255.");
        }
        else
        {
            Console.WriteLine($"Hop limit {hops}: THIS DATAGRAM WILL LEAVE THE ADAPTER and reach the link.");
            Console.WriteLine("It is not a DNS message, so any mDNS implementation receiving it must");
            Console.WriteLine("silently ignore it (RFC 6762). Nothing is asked of any device.");
        }

        Console.WriteLine();

        // ---- the receiving socket: MdnsSocket.OpenIPv6, written out in full ----
        using var receiver = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

        // Share the port rather than seize it: Dnscache already holds 5353, and
        // the service may be running too. Both of these must precede Bind.
        receiver.ExclusiveAddressUse = false;                                                     // (1)
        receiver.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);  // (2)

        // IPV6_V6ONLY, also before Bind.
        receiver.DualMode = false;                                                                // (3)

        receiver.Bind(new IPEndPoint(IPAddress.IPv6Any, MdnsPort));                               // (4)

        receiver.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.PacketInformation, true);                    // (5)
        receiver.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, ReceiverHopLimit);      // (6)
        receiver.SetSocketOption(
            SocketOptionLevel.IPv6,
            SocketOptionName.AddMembership,
            new IPv6MulticastOption(MdnsGroupV6, joinIndex));                                     // (7)

        // ---- the sending socket: not a copy of anything in the product ----
        using var sender = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

        // An ephemeral port, deliberately not 5353. See the note at the top of
        // this file about what that would mean for a test built on this pattern.
        sender.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

        // IPV6_MULTICAST_IF takes a bare interface index in host byte order, not
        // address bytes. Same as the product's send path.
        sender.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, joinIndex);

        sender.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, hops);

        // IPV6_MULTICAST_LOOP is READ, never written. The product leaves it at
        // the operating system default (decided by Edwin West, 2026-09-13,
        // docs/findings/2026-09-13-ipv6-pktinfo-arrival.md), so a run that set
        // it would measure a configuration the product does not use. It is the
        // SENDING socket's option that decides whether a copy comes back.
        bool loopback = ReadFlag(sender, SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback);

        Console.WriteLine("Receiving socket, read back from the OS:");
        Console.WriteLine($"  bound to            : {receiver.LocalEndPoint}");
        Console.WriteLine($"  DualMode            : {receiver.DualMode}");
        Console.WriteLine($"  ExclusiveAddressUse : {receiver.ExclusiveAddressUse}");
        Console.WriteLine($"  SO_REUSEADDR        : {ReadFlag(receiver, SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)}");
        Console.WriteLine($"  IPV6_PKTINFO        : {ReadFlag(receiver, SocketOptionLevel.IPv6, SocketOptionName.PacketInformation)}");
        Console.WriteLine($"  IPV6_MULTICAST_HOPS : {ReadInt(receiver, SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive)}");
        Console.WriteLine($"  joined              : {MdnsGroupV6} on interface index {joinIndex}");
        Console.WriteLine();
        Console.WriteLine("Sending socket, read back from the OS:");
        Console.WriteLine($"  bound to            : {sender.LocalEndPoint}");
        Console.WriteLine($"  IPV6_MULTICAST_IF   : {ReadInt(sender, SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface)}");
        Console.WriteLine($"  IPV6_MULTICAST_HOPS : {ReadInt(sender, SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive)}");
        Console.WriteLine($"  IPV6_MULTICAST_LOOP : {loopback}   (read, not set - OS default)");
        Console.WriteLine();

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceBytes);

        Console.WriteLine(
            $"Sending {count} probe datagram(s) to [{MdnsGroupV6}]:{MdnsPort}, "
            + $"waiting up to {timeout}s for each.");
        Console.WriteLine();

        return await ProbeAsync(receiver, sender, nonce, joinIndex, count, timeout, hops, loopback)
            .ConfigureAwait(false);
    }

    private static async Task<int> ProbeAsync(
        Socket receiver,
        Socket sender,
        byte[] nonce,
        int expectedIndex,
        int count,
        int timeout,
        int hops,
        bool loopback)
    {
        var arrivalIndexes = new Dictionary<int, int>();
        var destinations = new Dictionary<string, int>(StringComparer.Ordinal);
        var buffer = new byte[BufferSize];

        int returned = 0;
        int ignored = 0;

        for (int sequence = 0; sequence < count; sequence++)
        {
            // Typed as ReadOnlyMemory rather than left as byte[]. A byte[]
            // converts implicitly to both ReadOnlyMemory<byte> and
            // ArraySegment<byte>, and SendToAsync has an overload for each, so
            // the call would be ambiguous. Naming the type picks the overload.
            ReadOnlyMemory<byte> payload = BuildPayload(nonce, sequence);

            await sender.SendToAsync(
                payload,
                SocketFlags.None,
                new IPEndPoint(MdnsGroupV6, MdnsPort)).ConfigureAwait(false);

            // Each datagram is sent and then waited for, rather than sending all
            // of them and draining afterwards. It keeps at most one probe in the
            // socket buffer at a time, so a busy link cannot push an early probe
            // out before it is read.
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            bool found = false;

            try
            {
                while (!found)
                {
                    // IPv6Any, not IPAddress.Any: an IPv4 endpoint handed to an
                    // IPv6 socket is rejected on the address family.
                    SocketReceiveMessageFromResult result = await receiver.ReceiveMessageFromAsync(
                        buffer,
                        SocketFlags.None,
                        new IPEndPoint(IPAddress.IPv6Any, 0),
                        cancellation.Token).ConfigureAwait(false);

                    if (!Matches(buffer, result.ReceivedBytes, nonce, sequence))
                    {
                        // Somebody else's mDNS traffic. Counted so the run can
                        // report that the receiver was live, and discarded
                        // without printing anything that names a device.
                        ignored++;
                        continue;
                    }

                    found = true;
                    returned++;

                    int index = result.PacketInformation.Interface;
                    string destination = result.PacketInformation.Address?.ToString() ?? "(none)";

                    arrivalIndexes[index] = arrivalIndexes.GetValueOrDefault(index) + 1;
                    destinations[destination] = destinations.GetValueOrDefault(destination) + 1;

                    string flag = index == expectedIndex
                        ? string.Empty
                        : "   <- NOT the expected index";

                    Console.WriteLine(
                        $"  probe {sequence}: returned, index {index,-4} to {destination,-24} "
                        + $"from {result.RemoteEndPoint} {result.ReceivedBytes}B{flag}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"  probe {sequence}: did not return within {timeout}s");
            }
        }

        return Summarise(returned, ignored, count, expectedIndex, hops, loopback, arrivalIndexes, destinations);
    }

    private static int Summarise(
        int returned,
        int ignored,
        int count,
        int expectedIndex,
        int hops,
        bool loopback,
        Dictionary<int, int> arrivalIndexes,
        Dictionary<string, int> destinations)
    {
        Console.WriteLine();
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine($"Probes sent               : {count}");
        Console.WriteLine($"Probes returned           : {returned}");
        Console.WriteLine($"Other datagrams discarded : {ignored}");

        if (returned == 0)
        {
            Console.WriteLine();

            if (ignored == 0)
            {
                Console.WriteLine("The receiver read nothing at all, not even ordinary link traffic. Treat");
                Console.WriteLine("this whole run as suspect rather than as a result: suspect the local");
                Console.WriteLine("firewall, or an adapter with no IPv6 mDNS activity. Run");
                Console.WriteLine("SecretPrinter.Listen6 on the same adapter as a control first.");
            }
            else
            {
                Console.WriteLine($"The receiver was live - it read {ignored} other datagram(s) - and none of");
                Console.WriteLine("this run's probes came back to it.");
            }

            Console.WriteLine();

            if (hops == 0)
            {
                Console.WriteLine("INCONCLUSIVE. At hop limit 0 there are two explanations and this run");
                Console.WriteLine("cannot separate them:");
                Console.WriteLine("  - Windows does not deliver a hop-limit-0 multicast datagram locally, or");
                Console.WriteLine("  - loopback delivery does not happen here at all.");
                Console.WriteLine();
                Console.WriteLine("Re-run with --hops 255 to separate them. That WILL put the datagram on");
                Console.WriteLine("the link. Only if that run also returns nothing is loopback ruled out.");
            }
            else
            {
                Console.WriteLine($"NEGATIVE for loopback delivery at hop limit {hops}, with");
                Console.WriteLine($"IPV6_MULTICAST_LOOP reading {loopback} on the sending socket.");
                Console.WriteLine();
                Console.WriteLine("A test that sends to ff02::fb and asserts arrival is not available on");
                Console.WriteLine("this machine without setting that option - which the product does not");
                Console.WriteLine("set, so such a test would measure a configuration the service never uses.");
            }

            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Arrival index reported:");
        foreach ((int index, int number) in arrivalIndexes.OrderByDescending(entry => entry.Value))
        {
            string verdict = index == expectedIndex
                ? "matches the adapter's index"
                : index == 0
                    ? "ZERO - no usable index reported"
                    : "does NOT match the adapter's index";

            Console.WriteLine($"  {index,-6} {number,5} probe(s)   {verdict}");
        }

        Console.WriteLine();
        Console.WriteLine("Destination address reported:");
        foreach ((string address, int number) in destinations.OrderByDescending(entry => entry.Value))
        {
            Console.WriteLine($"  {address,-26} {number,5} probe(s)");
        }

        bool everyProbeMatched = arrivalIndexes.Count == 1 && arrivalIndexes.ContainsKey(expectedIndex);

        Console.WriteLine();
        Console.WriteLine("What this run supports:");

        if (everyProbeMatched)
        {
            Console.WriteLine("  A locally looped multicast datagram IS attributed to the adapter it was");
            Console.WriteLine("  sent through, not to loopback. A test may send to ff02::fb from a second");
            Console.WriteLine("  socket and assert that MdnsSocket.ReceiveAsync returned an ArrivedOn");
            Console.WriteLine("  naming that adapter.");
            Console.WriteLine();
            Console.WriteLine("  That test depends on IPV6_MULTICAST_LOOP being on by default, which this");
            Console.WriteLine($"  run read as {loopback} and did not set. Say so in the test's name and");
            Console.WriteLine("  comment, so a future default change is diagnosed and not merely suffered.");
        }
        else
        {
            Console.WriteLine("  A locally looped multicast datagram is NOT attributed to the adapter it");
            Console.WriteLine("  was sent through. A test asserting attribution on a looped datagram would");
            Console.WriteLine("  fail against correct product code. That test design is unavailable.");
        }

        Console.WriteLine();
        Console.WriteLine("What it cannot support:");
        Console.WriteLine("  - any claim about which family's numbering the index came from");
        Console.WriteLine("  - any claim about how the product's own socket attributes; this tool");
        Console.WriteLine("    duplicates that socket's configuration, it does not call it");

        return 0;
    }

    /// <summary>Marker, then the run's nonce, then one sequence byte.</summary>
    private static byte[] BuildPayload(byte[] nonce, int sequence)
    {
        var payload = new byte[Marker.Length + nonce.Length + 1];

        Marker.CopyTo(payload, 0);
        nonce.CopyTo(payload, Marker.Length);
        payload[^1] = (byte)sequence;

        return payload;
    }

    /// <summary>
    /// True when the received bytes are this run's probe with this sequence
    /// number. Matched on the exact payload, so nothing has to be parsed and no
    /// other device's datagram can be mistaken for one of ours.
    /// </summary>
    private static bool Matches(byte[] buffer, int received, byte[] nonce, int sequence)
    {
        int expected = Marker.Length + nonce.Length + 1;

        if (received != expected)
        {
            return false;
        }

        var payload = new ReadOnlySpan<byte>(buffer, 0, received);

        return payload[..Marker.Length].SequenceEqual(Marker)
            && payload.Slice(Marker.Length, nonce.Length).SequenceEqual(nonce)
            && payload[^1] == (byte)sequence;
    }

    /// <summary>
    /// Finds the adapter holding an IPv4 address and reads both family indexes
    /// from it, the same way SystemInterfaceInventory does.
    /// </summary>
    private static (string? Name, int? IPv4Index, int? IPv6Index) FindAdapter(IPAddress ipv4)
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties properties = adapter.GetIPProperties();

            if (!properties.UnicastAddresses.Any(unicast => unicast.Address.Equals(ipv4)))
            {
                continue;
            }

            int? ipv4Index = null;
            try
            {
                ipv4Index = properties.GetIPv4Properties()?.Index;
            }
            catch (NetworkInformationException)
            {
                // No IPv4 configuration. Reported as null rather than guessed at.
            }

            int? ipv6Index = null;
            try
            {
                ipv6Index = properties.GetIPv6Properties()?.Index;
            }
            catch (NetworkInformationException)
            {
                // No IPv6 configuration on this adapter.
            }

            return (adapter.Name, ipv4Index, ipv6Index);
        }

        return (null, null, null);
    }

    private static bool TryParseInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool ReadFlag(Socket socket, SocketOptionLevel level, SocketOptionName name) =>
        ReadInt(socket, level, name) != 0;

    private static int ReadInt(Socket socket, SocketOptionLevel level, SocketOptionName name) =>
        (int)socket.GetSocketOption(level, name)!;
}

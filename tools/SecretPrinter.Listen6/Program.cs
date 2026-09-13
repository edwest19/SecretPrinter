// -----------------------------------------------------------------------------
// Program.cs
//
// SecretPrinter.Listen6 - a receive-only diagnostic, the IPv6 counterpart of
// SecretPrinter.Listen.
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, 2026-09-13, for the SecretPrinter project. Reviewed by a human before
// merge.
//
// THE ONE QUESTION THIS TOOL EXISTS TO ANSWER
// -------------------------------------------
// When a datagram arrives on a socket bound to [::]:5353 with IPV6_PKTINFO
// enabled, does SocketReceiveMessageFromResult.PacketInformation.Interface
// report a non-zero index naming the adapter it arrived on?
//
// REQ-ADV-020 requires the service to determine the arrival interface of an
// IPv6 query from IPV6_PKTINFO. Before this tool was run, nobody on this
// project had observed Windows reporting a usable index there. The option was
// set and read back, which proves only that the operating system accepted it.
// Building arrival attribution on an unobserved behaviour would have put the
// whole receive design at risk of a late rewrite.
//
// The result is recorded in docs/findings/2026-09-13-ipv6-pktinfo-arrival.md.
//
// WHAT THIS TOOL CANNOT ANSWER, AND MUST NOT BE READ AS ANSWERING
// ---------------------------------------------------------------
// Whether the reported number is the IPv6 interface numbering or the IPv4
// numbering. On every adapter this project has measured, the two families
// report the same index for a given adapter, so no observation here can
// separate those hypotheses. See
// docs/findings/2026-09-13-interface-index-parity.md.
//
// The program prints both indexes and says so in its own output, so that a
// reader of a transcript cannot mistake the stronger claim for the weaker one
// that was actually measured.
//
// WHAT IT DOES NOT DO
// -------------------
// It never sends. Not a query, not an announcement, not a reply. It joins a
// multicast group and reads. That keeps it clear of the MulticastLoopback
// question, which belongs to the service's receive path and not here.
//
// It writes no files, touches no registry key, and needs no elevation.
//
// PRIVACY, THE SAME CAVEAT AS SecretPrinter.Listen
// ------------------------------------------------
// mDNS is chatty and names devices. This tool prints source addresses and byte
// counts for every datagram on the link, from phones, televisions and computers
// alike, not only printers. It holds nothing on disk. If you publish a
// transcript, read it first.
//
// FIDELITY TO THE PRODUCT, WHICH IS THE WHOLE POINT
// -------------------------------------------------
// The socket below is opened with the same options, in the same order, as
// MdnsSocket.OpenIPv6 in src/SecretPrinter.Mdns/MdnsSocket.cs:
//
//   product                                    here
//   ---------------------------------------    ---------------------------
//   ExclusiveAddressUse = false                marked (1)
//   SO_REUSEADDR = true                        marked (2)
//   DualMode = false        (before Bind)      marked (3)
//   Bind([::]:5353)                            marked (4)
//   IPV6_PKTINFO = true                        marked (5)
//   IPV6_MULTICAST_HOPS = 255                  marked (6)
//   AddMembership ff02::fb by interface index  marked (7)
//
// If those diverge, this tool measures a socket the product never creates and
// its result does not transfer. Check the correspondence before believing the
// output.
//
// NO REQUIREMENT MARKERS
// ----------------------
// Deliberately carries none, for the same reason as SecretPrinter.Respond and
// SecretPrinter.Respond6: the requirements in README.md describe the service.
// A marker here would make the coverage matrix report that the service has a
// behaviour, when what exists is a diagnostic that measured the platform.
// REQ-ADV-020 is earned by SecretPrinter.Mdns or not at all.
//
// Unlike Respond6, this tool is not scheduled for deletion. Respond6 is a
// stand-in for product behaviour and is superseded once the product has it.
// This one measures a property of the operating system, which stays worth
// re-checking on a new machine, a new Windows build, or a new adapter.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SecretPrinter.Listen6;

internal static class Program
{
    /// <summary>The IPv6 link-local multicast group for mDNS (RFC 6762 §3).</summary>
    private static readonly IPAddress MdnsGroupV6 = IPAddress.Parse("ff02::fb");

    private const int MdnsPort = 5353;
    private const int HopLimit = 255;
    private const int BufferSize = 9000;
    private const int DefaultDurationSeconds = 120;

    private static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? ipv4Text = null;
        int seconds = DefaultDurationSeconds;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--ipv4", StringComparison.Ordinal))
            {
                ipv4Text = args[i + 1];
            }
            else if (string.Equals(args[i], "--duration", StringComparison.Ordinal)
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                seconds = parsed;
            }
        }

        if (ipv4Text is null)
        {
            Console.Error.WriteLine(
                "usage: --ipv4 <IPv4 address of the interface to join on> [--duration <seconds>]");
            return 2;
        }

        if (!IPAddress.TryParse(ipv4Text, out IPAddress? ipv4)
            || ipv4.AddressFamily != AddressFamily.InterNetwork)
        {
            Console.Error.WriteLine($"'{ipv4Text}' is not an IPv4 address.");
            return 2;
        }

        try
        {
            return await RunAsync(ipv4, seconds).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Socket error: {ex.Message} (code {ex.SocketErrorCode}).");
            return 3;
        }
    }

    private static async Task<int> RunAsync(IPAddress ipv4, int seconds)
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

        Console.WriteLine("SecretPrinter IPv6 mDNS listener (receive-only; transmits nothing).");
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

        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

        // Share the port rather than seize it: Dnscache already holds 5353.
        // Both of these must precede Bind.
        socket.ExclusiveAddressUse = false;                                                     // (1)
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);  // (2)

        // IPV6_V6ONLY, also before Bind. A dual-mode socket would additionally
        // receive the IPv4 traffic, which is not what is being measured.
        socket.DualMode = false;                                                                // (3)

        socket.Bind(new IPEndPoint(IPAddress.IPv6Any, MdnsPort));                               // (4)

        socket.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.PacketInformation, true);                  // (5)
        socket.SetSocketOption(
            SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, HopLimit);            // (6)
        socket.SetSocketOption(
            SocketOptionLevel.IPv6,
            SocketOptionName.AddMembership,
            new IPv6MulticastOption(MdnsGroupV6, joinIndex));                                   // (7)

        // Read back what the operating system actually did. This proves the
        // options were accepted and nothing more - the same limitation the
        // product's own read-back tests have.
        Console.WriteLine("Socket options, read back from the OS:");
        Console.WriteLine($"  bound to            : {socket.LocalEndPoint}");
        Console.WriteLine($"  DualMode            : {socket.DualMode}");
        Console.WriteLine($"  ExclusiveAddressUse : {socket.ExclusiveAddressUse}");
        Console.WriteLine($"  SO_REUSEADDR        : {ReadFlag(socket, SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)}");
        Console.WriteLine($"  IPV6_PKTINFO        : {ReadFlag(socket, SocketOptionLevel.IPv6, SocketOptionName.PacketInformation)}");
        Console.WriteLine($"  IPV6_MULTICAST_HOPS : {ReadInt(socket, SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive)}");
        Console.WriteLine();
        Console.WriteLine($"Joined {MdnsGroupV6} on interface index {joinIndex}. Listening {seconds}s. Sending nothing.");
        Console.WriteLine();

        return await ListenAsync(socket, joinIndex, seconds).ConfigureAwait(false);
    }

    private static async Task<int> ListenAsync(Socket socket, int expectedIndex, int seconds)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));

        var indexCounts = new Dictionary<int, int>();
        var addressCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var buffer = new byte[BufferSize];

        DateTime start = DateTime.UtcNow;
        int received = 0;

        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                // IPv6Any, not IPAddress.Any: an IPv4 endpoint handed to an IPv6
                // socket is rejected on the address family.
                SocketReceiveMessageFromResult result = await socket.ReceiveMessageFromAsync(
                    buffer,
                    SocketFlags.None,
                    new IPEndPoint(IPAddress.IPv6Any, 0),
                    cancellation.Token).ConfigureAwait(false);

                received++;

                int index = result.PacketInformation.Interface;
                string destination = result.PacketInformation.Address?.ToString() ?? "(none)";

                indexCounts[index] = indexCounts.GetValueOrDefault(index) + 1;
                addressCounts[destination] = addressCounts.GetValueOrDefault(destination) + 1;

                double elapsed = (DateTime.UtcNow - start).TotalSeconds;
                string flag = index == expectedIndex ? string.Empty : "   <- NOT the expected index";

                Console.WriteLine(
                    $"[{elapsed,6:F1}s] index {index,-4} to {destination,-24} "
                    + $"from {result.RemoteEndPoint} {result.ReceivedBytes}B{flag}");
            }
        }
        catch (OperationCanceledException)
        {
            // The duration elapsed. Not an error.
        }

        return Summarise(received, expectedIndex, indexCounts, addressCounts);
    }

    private static int Summarise(
        int received,
        int expectedIndex,
        Dictionary<int, int> indexCounts,
        Dictionary<string, int> addressCounts)
    {
        Console.WriteLine();
        Console.WriteLine("----------------------------------------------------------------");
        Console.WriteLine($"Datagrams received : {received}");

        if (received == 0)
        {
            Console.WriteLine();
            Console.WriteLine("NOT a negative result for IPV6_PKTINFO. Nothing arrived, so nothing was");
            Console.WriteLine("reported. Suspect the local firewall, or no querier being active on the");
            Console.WriteLine("link. Re-run on a machine known to receive IPv6 mDNS as a control.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Arrival index reported:");
        foreach ((int index, int count) in indexCounts.OrderByDescending(entry => entry.Value))
        {
            string verdict = index == expectedIndex
                ? "matches the adapter's index"
                : index == 0
                    ? "ZERO - no usable index reported"
                    : "does NOT match the adapter's index";

            Console.WriteLine($"  {index,-6} {count,5} datagram(s)   {verdict}");
        }

        Console.WriteLine();
        Console.WriteLine("Destination address reported:");
        foreach ((string address, int count) in addressCounts.OrderByDescending(entry => entry.Value))
        {
            Console.WriteLine($"  {address,-26} {count,5} datagram(s)");
        }

        Console.WriteLine();
        Console.WriteLine("What this run can support, at most:");
        Console.WriteLine("  - whether a non-zero index was reported at all");
        Console.WriteLine("  - whether that index equals the adapter's index");
        Console.WriteLine("It cannot support any claim about which family's numbering was used.");

        return 0;
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

    private static bool ReadFlag(Socket socket, SocketOptionLevel level, SocketOptionName name) =>
        ReadInt(socket, level, name) != 0;

    private static int ReadInt(Socket socket, SocketOptionLevel level, SocketOptionName name) =>
        (int)socket.GetSocketOption(level, name)!;
}

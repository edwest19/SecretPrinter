// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Listen)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Answer one specific engineering question before any service code is
//   written:
//
//     Can this process receive multicast DNS on UDP port 5353 while the
//     Windows DNS Client service (and any other responder) is already bound
//     to that port, and can it tell which interface each packet arrived on?
//
//   The whole SecretPrinter proxy design depends on "yes". This tool finds out
//   cheaply, without committing to an architecture.
//
// What this program does, in full:
//   1. Binds one UDP socket to 0.0.0.0:5353 with SO_REUSEADDR, so it shares
//      the port rather than seizing it.
//   2. Joins multicast group 224.0.0.251 on each interface you name.
//   3. Enables IP_PKTINFO so the operating system reports the arrival
//      interface for every datagram.
//   4. Prints one line per packet received, and a summary when time is up.
//
// What this program does NOT do:
//   - It never transmits. Not a query, not a response, not an advertisement.
//     There is no send call anywhere in this file. Other hosts cannot tell
//     this program is running.
//   - It does not write files, touch the registry, or alter firewall rules.
//   - It does not stop, disable or reconfigure any other service.
//
// Privacy note, stated plainly:
//   mDNS on a home network is chatty and carries device names. While running,
//   this tool will observe whatever your networks are broadcasting - phones,
//   speakers, televisions, computers - not only printers. It holds that data
//   in memory for the duration of the run and writes none of it to disk. If
//   you publish output from this tool, read it first.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SecretPrinter.Dns;

namespace SecretPrinter.Listen;

internal static class Program
{
    private static readonly IPAddress MdnsGroup = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Options? options = Options.Parse(args);
            return options is null ? 0 : await RunAsync(options).ConfigureAwait(false);
        }
        catch (OptionException ex)
        {
            Console.Error.WriteLine($"Argument error: {ex.Message}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Socket error: {ex.Message} (code {ex.SocketErrorCode}).");
            Console.Error.WriteLine();
            Console.Error.WriteLine("If this is AddressAlreadyInUse, then port 5353 cannot be shared on");
            Console.Error.WriteLine("this machine, which is itself the answer this tool exists to find.");
            return 3;
        }
    }

    private static async Task<int> RunAsync(Options options)
    {
        Console.WriteLine("SecretPrinter mDNS listener (receive-only; transmits nothing).");
        Console.WriteLine();

        InterfaceMap interfaces = InterfaceMap.Build();

        Console.WriteLine("Joining multicast group on:");
        foreach (IPAddress address in options.InterfaceAddresses)
        {
            string name = interfaces.DescribeByAddress(address);
            Console.WriteLine($"  {address,-15} {name}");
        }

        Console.WriteLine();

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Share the port rather than seize it. Both of these must be set before
        // Bind. On Windows, ExclusiveAddressUse defaults to true for sockets
        // created this way, and would defeat SO_REUSEADDR.
        socket.ExclusiveAddressUse = false;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Bind to the wildcard address. Binding to a specific unicast address
        // is unreliable for receiving multicast on Windows; the arrival
        // interface is recovered from IP_PKTINFO instead.
        socket.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));
        Console.WriteLine($"Bound to {socket.LocalEndPoint} with SO_REUSEADDR. Port sharing accepted by the OS.");

        // Ask the OS to report the arrival interface for each datagram.
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

        foreach (IPAddress address in options.InterfaceAddresses)
        {
            try
            {
                socket.SetSocketOption(
                    SocketOptionLevel.IP,
                    SocketOptionName.AddMembership,
                    new MulticastOption(MdnsGroup, address));
            }
            catch (SocketException ex)
            {
                Console.Error.WriteLine(
                    $"Failed to join {MdnsGroup} on {address}: {ex.Message} ({ex.SocketErrorCode}).");
                Console.Error.WriteLine("Failing fast rather than listening on a partial set of interfaces.");
                return 3;
            }

            Console.WriteLine($"Joined {MdnsGroup} on {address}.");
        }

        Console.WriteLine();
        Console.WriteLine($"Listening for {options.Duration.TotalSeconds:0.#} s. Press Ctrl+C to stop early.");
        Console.WriteLine();

        var statistics = new Statistics();
        await ListenAsync(socket, options, interfaces, statistics).ConfigureAwait(false);

        statistics.PrintSummary(options, interfaces);
        return statistics.TotalPackets > 0 ? 0 : 1;
    }

    private static async Task ListenAsync(
        Socket socket, Options options, InterfaceMap interfaces, Statistics statistics)
    {
        using var cancellation = new CancellationTokenSource(options.Duration);

        // Ctrl+C stops the loop cleanly instead of killing the process.
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Stopping (Ctrl+C).");
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;

        var buffer = new byte[9000];
        DateTime start = DateTime.UtcNow;

        try
        {
            while (true)
            {
                SocketReceiveMessageFromResult result;
                try
                {
                    result = await socket.ReceiveMessageFromAsync(
                        buffer,
                        SocketFlags.None,
                        new IPEndPoint(IPAddress.Any, 0),
                        cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var source = (IPEndPoint)result.RemoteEndPoint;
                int interfaceIndex = result.PacketInformation.Interface;
                string interfaceLabel = interfaces.DescribeByIndex(interfaceIndex);

                statistics.Record(interfaceIndex, interfaceLabel, source.Address, result.ReceivedBytes);

                double seconds = (DateTime.UtcNow - start).TotalSeconds;
                string summary;
                try
                {
                    DnsMessage message = DnsMessage.Parse(buffer, result.ReceivedBytes);
                    summary = Describe(message);
                    statistics.RecordParsed(message);
                }
                catch (InvalidDataException ex)
                {
                    summary = $"UNPARSEABLE: {ex.Message}";
                    statistics.RecordUnparseable();
                }

                Console.WriteLine(
                    $"[{seconds,6:0.0}s] if={interfaceIndex,-3} {interfaceLabel,-14} "
                    + $"from {source.Address,-15} {result.ReceivedBytes,5}B  {summary}");
            }
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    /// <summary>One-line human summary of an mDNS message.</summary>
    private static string Describe(DnsMessage message)
    {
        if (!message.IsResponse)
        {
            var names = new List<string>();
            foreach ((DnsName name, DnsRecordType type, _) in message.Questions)
            {
                names.Add($"{type} {name}");
            }

            string joined = names.Count > 0 ? string.Join(", ", names) : "(no questions)";
            return $"QUERY  {Truncate(joined, 90)}";
        }

        var described = new List<string>();
        foreach (DnsRecord record in message.Answers)
        {
            described.Add($"{record.Type} {record.Name}");
        }

        string answers = described.Count > 0 ? string.Join(", ", described) : "(no answers)";
        return $"REPLY  {Truncate(answers, 90)}";
    }

    private static string Truncate(string text, int maximum) =>
        text.Length <= maximum ? text : string.Concat(text.AsSpan(0, maximum - 3), "...");
}

/// <summary>Maps operating-system interface indexes to friendly descriptions.</summary>
internal sealed class InterfaceMap
{
    private readonly Dictionary<int, string> _byIndex = [];
    private readonly Dictionary<string, string> _byAddress = new(StringComparer.Ordinal);

    public static InterfaceMap Build()
    {
        var map = new InterfaceMap();

        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties properties = adapter.GetIPProperties();

            IPv4InterfaceProperties? ipv4;
            try
            {
                ipv4 = properties.GetIPv4Properties();
            }
            catch (NetworkInformationException)
            {
                continue; // Adapter has no IPv4 configuration.
            }

            if (ipv4 is null)
            {
                continue;
            }

            map._byIndex[ipv4.Index] = adapter.Name;

            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    map._byAddress[unicast.Address.ToString()] = adapter.Name;
                }
            }
        }

        return map;
    }

    public string DescribeByIndex(int index) =>
        _byIndex.TryGetValue(index, out string? name) ? name : $"(index {index})";

    public string DescribeByAddress(IPAddress address) =>
        _byAddress.TryGetValue(address.ToString(), out string? name) ? name : "(not a local address)";
}

/// <summary>Counters, kept so the summary reports what happened rather than what we hoped.</summary>
internal sealed class Statistics
{
    private readonly Dictionary<int, (string Label, int Packets, long Bytes)> _byInterface = [];
    private readonly Dictionary<string, int> _bySource = new(StringComparer.Ordinal);

    public int TotalPackets { get; private set; }

    private int _queries;
    private int _responses;
    private int _unparseable;

    public void Record(int interfaceIndex, string label, IPAddress source, int bytes)
    {
        TotalPackets++;

        _byInterface.TryGetValue(interfaceIndex, out (string Label, int Packets, long Bytes) entry);
        _byInterface[interfaceIndex] = (label, entry.Packets + 1, entry.Bytes + bytes);

        string key = source.ToString();
        _bySource[key] = _bySource.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    public void RecordParsed(DnsMessage message)
    {
        if (message.IsResponse)
        {
            _responses++;
        }
        else
        {
            _queries++;
        }
    }

    public void RecordUnparseable() => _unparseable++;

    public void PrintSummary(Options options, InterfaceMap interfaces)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("SUMMARY");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
        Console.WriteLine($"Total packets : {TotalPackets}");
        Console.WriteLine($"  queries     : {_queries}");
        Console.WriteLine($"  responses   : {_responses}");
        Console.WriteLine($"  unparseable : {_unparseable}");
        Console.WriteLine();

        if (_byInterface.Count > 0)
        {
            Console.WriteLine("By arrival interface:");
            foreach ((int index, (string label, int packets, long bytes)) in _byInterface.OrderBy(e => e.Key))
            {
                Console.WriteLine($"  index {index,-4} {label,-16} {packets,5} packet(s)  {bytes,7} byte(s)");
            }

            Console.WriteLine();
        }

        if (_bySource.Count > 0)
        {
            Console.WriteLine($"Distinct sources seen: {_bySource.Count}");
            foreach ((string source, int count) in _bySource.OrderByDescending(e => e.Value).Take(15))
            {
                Console.WriteLine($"  {source,-15} {count,5} packet(s)");
            }

            Console.WriteLine();
        }

        Console.WriteLine(new string('-', 72));
        Console.WriteLine("WHAT THIS RUN DID AND DID NOT ESTABLISH");
        Console.WriteLine(new string('-', 72));
        Console.WriteLine();

        Console.WriteLine("Established:");
        Console.WriteLine("  * Binding UDP 5353 with SO_REUSEADDR alongside the existing responders");
        Console.WriteLine("    succeeded. (Had it failed, this program would have exited with code 3.)");

        var receivedIndexes = new HashSet<int>(_byInterface.Keys);
        var joinedButSilent = new List<string>();

        foreach (IPAddress address in options.InterfaceAddresses)
        {
            string label = interfaces.DescribeByAddress(address);
            bool sawTraffic = _byInterface.Values.Any(
                entry => string.Equals(entry.Label, label, StringComparison.Ordinal));

            if (sawTraffic)
            {
                Console.WriteLine($"  * Multicast was delivered to this process on {label} ({address}),");
                Console.WriteLine("    and the arrival interface was correctly reported by IP_PKTINFO.");
            }
            else
            {
                joinedButSilent.Add($"{label} ({address})");
            }
        }

        Console.WriteLine();

        if (joinedButSilent.Count > 0)
        {
            Console.WriteLine("NOT established:");
            foreach (string entry in joinedButSilent)
            {
                Console.WriteLine($"  * No packets arrived on {entry} during this run.");
            }

            Console.WriteLine();
            Console.WriteLine("    This is ambiguous. It may mean delivery does not work on that");
            Console.WriteLine("    interface, or simply that nothing on that network spoke mDNS while");
            Console.WriteLine("    the tool was running. Distinguish the two by generating traffic:");
            Console.WriteLine("    open a print dialog on a phone connected to that network, then");
            Console.WriteLine("    re-run. Do not treat silence as a failure without doing that.");
            Console.WriteLine();
        }

        Console.WriteLine("Out of scope for this run:");
        Console.WriteLine("  * Whether this process can SEND mDNS responses that other hosts accept");
        Console.WriteLine("    while another responder holds the same port. Receiving and responding");
        Console.WriteLine("    are different problems; nothing here tested the second one.");
        Console.WriteLine();

        if (receivedIndexes.Count == 0)
        {
            Console.WriteLine("No traffic at all was observed. Before concluding anything, check that");
            Console.WriteLine("Windows Firewall permits inbound UDP 5353 for this program. This tool");
            Console.WriteLine("does not create firewall rules, by design.");
        }
    }
}

internal sealed class OptionException(string message) : Exception(message);

/// <summary>Command-line options. Every setting is explicit and echoed at startup.</summary>
internal sealed class Options
{
    public required IReadOnlyList<IPAddress> InterfaceAddresses { get; init; }
    public required TimeSpan Duration { get; init; }

    /// <summary>Returns null when the caller asked for help.</summary>
    public static Options? Parse(string[] args)
    {
        var addresses = new List<IPAddress>();
        double durationSeconds = 30;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h" or "-?":
                    PrintUsage();
                    return null;

                case "--interface":
                {
                    string value = RequireValue(args, ref i, "--interface");
                    if (!IPAddress.TryParse(value, out IPAddress? parsed)
                        || parsed.AddressFamily != AddressFamily.InterNetwork)
                    {
                        throw new OptionException($"'{value}' is not an IPv4 address.");
                    }

                    if (!addresses.Contains(parsed))
                    {
                        addresses.Add(parsed);
                    }

                    break;
                }

                case "--duration":
                {
                    string value = RequireValue(args, ref i, "--duration");
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds)
                        || durationSeconds is <= 0 or > 3600)
                    {
                        throw new OptionException(
                            $"--duration must be a number of seconds between 0 and 3600; got '{value}'.");
                    }

                    break;
                }

                default:
                    throw new OptionException($"Unrecognised argument '{args[i]}'.");
            }
        }

        if (addresses.Count == 0)
        {
            throw new OptionException(
                "At least one --interface is required. This tool will not guess which networks to join.");
        }

        return new Options
        {
            InterfaceAddresses = addresses,
            Duration = TimeSpan.FromSeconds(durationSeconds),
        };
    }

    private static string RequireValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new OptionException($"{name} requires a value.");
        }

        return args[++index];
    }

    private static void PrintUsage() =>
        Console.WriteLine("""
            SecretPrinter mDNS listener - a receive-only diagnostic.

            Tests whether this process can share UDP port 5353 with the Windows DNS
            Client service and still receive multicast DNS, correctly attributed to
            the interface each packet arrived on.

            This program never transmits. There is no send call in its source.

            Note: while running, it observes all mDNS traffic on the joined networks,
            which includes device names of things other than printers. Nothing is
            written to disk. Read the output before publishing it.

            Usage:
              SecretPrinter.Listen --interface <ipv4> [--interface <ipv4>]... [--duration <s>]

            Options:
              --interface <ipv4>  Local IPv4 address of an interface to join the mDNS
                                  group on. Required; may be repeated.
              --duration <s>      Seconds to listen. Default 30. Ctrl+C stops early.
              --help              Show this text.

            Exit codes:
              0  At least one packet was received.
              1  No packets were received.
              2  Bad arguments.
              3  Socket error (including failure to bind or to join a group).
            """);
}

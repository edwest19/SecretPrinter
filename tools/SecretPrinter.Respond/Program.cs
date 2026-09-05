// -----------------------------------------------------------------------------
// Program.cs  (SecretPrinter.Respond)
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   An experiment. It answers two questions the whole proxy design rests on:
//
//     1. Can this process SEND mDNS responses that clients accept, while the
//        Windows DNS Client service already holds port 5353?
//     2. Will an iOS client offer a printer advertised only as _ipp._tcp,
//        with no IPPS variant?
//
//   Receiving alongside Dnscache is already confirmed working
//   (see SecretPrinter.Listen). Responding is a different problem and is what
//   this tool measures.
//
// What this program does, in full:
//   1. Binds UDP 5353 with SO_REUSEADDR, sharing the port rather than seizing it.
//   2. Joins 224.0.0.251 on ONE interface that you name.
//   3. Announces one fake printer, three times, a second apart (RFC 6762 s8.3).
//   4. Answers queries for names it owns, and ignores everything else.
//   5. On exit, sends goodbye records so clients drop it immediately.
//
// What this program does NOT do:
//   - It does not print, relay, or listen on the advertised port. Selecting the
//     fake printer on a client WILL fail; that is expected. Discovery is the
//     question, not printing.
//   - It does not advertise on any interface but the one named.
//   - It does not answer queries for anything other than its own records.
//   - It does not read the real printer, or touch the printer network at all.
//   - It does not write files, touch the registry, or alter firewall rules.
//
// Everyone on the advertised network sees this:
//   The fake printer appears in the printer list of every Apple device on that
//   network for as long as this runs. The instance name says so. Goodbye records
//   on exit remove it promptly, but a client that was asleep may hold a stale
//   entry until its cache expires - two minutes at the TTL used here.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SecretPrinter.Dns;

namespace SecretPrinter.Respond;

internal static class Program
{
    private static readonly IPAddress MdnsGroup = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;

    /// <summary>
    /// TTL cap for responses to legacy unicast queriers, per RFC 6762 s6.7.
    /// Such clients do not understand cache-flush semantics, so answers to them
    /// must expire quickly.
    /// </summary>
    private const uint LegacyUnicastTtl = 10;

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
            return 3;
        }
    }

    private static async Task<int> RunAsync(Options options)
    {
        // Validate the interface BEFORE touching a socket. Doing it afterwards
        // means a typo surfaces as a bare AddressNotAvailable from a
        // setsockopt call, which tells the operator far less than naming the
        // problem does.
        int expectedInterfaceIndex = ResolveInterfaceIndex(options.InterfaceAddress);

        var advertisement = new TestAdvertisement(
            options.InstanceName, options.InterfaceAddress, options.Port, options.HostLabel);

        PrintBanner(options, advertisement);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Share port 5353 rather than seize it. Both settings must precede Bind.
        socket.ExclusiveAddressUse = false;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));

        // Learn the arrival interface of each query, so queries from other
        // networks can be ignored rather than answered.
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

        // Send only out of the named interface. Without this, multicast follows
        // the default route, which on a dual-homed host is likely the wrong
        // network - and advertising onto the printer network would be exactly
        // the mistake this project exists to avoid.
        socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.MulticastInterface,
            options.InterfaceAddress.GetAddressBytes());

        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);

        socket.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.AddMembership,
            new MulticastOption(MdnsGroup, options.InterfaceAddress));

        Console.WriteLine($"Bound {socket.LocalEndPoint} (SO_REUSEADDR). "
                          + $"Joined {MdnsGroup} on {options.InterfaceAddress} (index {expectedInterfaceIndex}).");
        Console.WriteLine();

        using var cancellation = new CancellationTokenSource(options.Duration);
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Stopping (Ctrl+C). Sending goodbye records...");
            cancellation.Cancel();
        };

        Console.CancelKeyPress += handler;

        var stats = new Stats();

        try
        {
            await AnnounceAsync(socket, advertisement, stats, cancellation.Token).ConfigureAwait(false);
            await ServeAsync(socket, advertisement, options, expectedInterfaceIndex, stats, cancellation.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= handler;

            // Goodbye records go out regardless of how the loop ended, so the
            // fake printer does not linger in client caches.
            SendGoodbye(socket, advertisement, stats);
        }

        stats.Print(options);
        return stats.QueriesAnswered > 0 ? 0 : 1;
    }

    /// <summary>
    /// Sends unsolicited announcements. RFC 6762 s8.3 asks for at least two, one
    /// second apart; three gives a little margin against packet loss on wireless.
    /// </summary>
    private static async Task AnnounceAsync(
        Socket socket, TestAdvertisement advertisement, Stats stats, CancellationToken token)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var builder = new DnsResponseBuilder();
            foreach (OutgoingRecord record in advertisement.AnnouncementRecords())
            {
                builder.AddAnswer(record);
            }

            byte[] message = builder.Build();
            await socket.SendToAsync(message, SocketFlags.None, new IPEndPoint(MdnsGroup, MdnsPort), token)
                        .ConfigureAwait(false);

            stats.RecordAnnouncement(message.Length);
            Console.WriteLine($"  announce {attempt}/3  {message.Length} bytes to {MdnsGroup}:{MdnsPort}");

            if (attempt < 3)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Announced. Now answering queries. Open a print dialog on a client to test.");
        Console.WriteLine();
    }

    private static async Task ServeAsync(
        Socket socket,
        TestAdvertisement advertisement,
        Options options,
        int expectedInterfaceIndex,
        Stats stats,
        CancellationToken token)
    {
        var buffer = new byte[9000];
        DateTime start = DateTime.UtcNow;

        while (true)
        {
            SocketReceiveMessageFromResult result;
            try
            {
                result = await socket.ReceiveMessageFromAsync(
                    buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var source = (IPEndPoint)result.RemoteEndPoint;
            int arrivedOn = result.PacketInformation.Interface;

            // Queries from the wrong interface are counted and dropped. This is
            // the check that keeps the experiment confined to one network.
            if (arrivedOn != expectedInterfaceIndex)
            {
                stats.RecordWrongInterface();
                continue;
            }

            DnsMessage query;
            try
            {
                query = DnsMessage.Parse(buffer, result.ReceivedBytes);
            }
            catch (InvalidDataException)
            {
                stats.RecordUnparseable();
                continue;
            }

            if (query.IsResponse)
            {
                continue; // Someone else's answer, including our own loopback.
            }

            stats.RecordQuerySeen();

            // RFC 6762 s6.7: a source port other than 5353 marks a legacy
            // unicast querier, which must be answered directly, with the query
            // identifier echoed and TTLs capped.
            bool legacyUnicast = source.Port != MdnsPort;

            var builder = new DnsResponseBuilder(legacyUnicast ? query.Id : (ushort)0);
            var matched = new List<string>();

            foreach ((DnsName name, DnsRecordType type, ushort rawClass) in query.Questions)
            {
                (IReadOnlyList<OutgoingRecord> answers, IReadOnlyList<OutgoingRecord> additionals) =
                    advertisement.Answer(name, type);

                if (answers.Count == 0)
                {
                    continue;
                }

                matched.Add($"{type} {name}");

                if (legacyUnicast)
                {
                    builder.AddQuestion(name, type);
                }

                foreach (OutgoingRecord record in answers)
                {
                    builder.AddAnswer(Adjust(record, legacyUnicast));
                }

                foreach (OutgoingRecord record in additionals)
                {
                    builder.AddAdditional(Adjust(record, legacyUnicast));
                }

                // The unicast-response bit (RFC 6762 s5.4) is noted for the log
                // but not acted on separately: this experiment answers by
                // multicast unless the querier is a legacy unicast client, which
                // is the simpler behaviour to reason about while measuring.
                if ((rawClass & 0x8000) != 0)
                {
                    stats.RecordUnicastRequested();
                }
            }

            if (builder.AnswerCount == 0)
            {
                continue; // Not about us. Say nothing.
            }

            byte[] response = builder.Build();
            IPEndPoint destination = legacyUnicast ? source : new IPEndPoint(MdnsGroup, MdnsPort);

            await socket.SendToAsync(response, SocketFlags.None, destination, token).ConfigureAwait(false);
            stats.RecordAnswered(response.Length);

            double seconds = (DateTime.UtcNow - start).TotalSeconds;
            Console.WriteLine(
                $"[{seconds,6:0.0}s] answered {source.Address,-15} "
                + $"{(legacyUnicast ? "unicast" : "multicast"),-9} {response.Length,4}B  "
                + string.Join(", ", matched));

            if (options.Verbose)
            {
                Console.WriteLine($"           -> {destination}");
            }
        }
    }

    /// <summary>Caps TTLs for legacy unicast responses; leaves multicast responses untouched.</summary>
    private static OutgoingRecord Adjust(OutgoingRecord record, bool legacyUnicast) =>
        legacyUnicast
            ? record with { Ttl = Math.Min(record.Ttl, LegacyUnicastTtl), CacheFlush = false }
            : record;

    private static void SendGoodbye(Socket socket, TestAdvertisement advertisement, Stats stats)
    {
        try
        {
            var builder = new DnsResponseBuilder();
            foreach (OutgoingRecord record in advertisement.GoodbyeRecords())
            {
                builder.AddAnswer(record);
            }

            byte[] message = builder.Build();

            // Sent twice, because a lost goodbye leaves a phantom printer in
            // client lists until the TTL expires.
            for (int i = 0; i < 2; i++)
            {
                socket.SendTo(message, SocketFlags.None, new IPEndPoint(MdnsGroup, MdnsPort));
                Thread.Sleep(250);
            }

            stats.GoodbyeSent = true;
            Console.WriteLine($"Goodbye records sent ({message.Length} bytes, twice, TTL 0).");
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Could not send goodbye records: {ex.Message}");
            Console.Error.WriteLine("The fake printer may linger in client caches until its TTL expires.");
        }
    }

    private static int ResolveInterfaceIndex(IPAddress address)
    {
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            IPInterfaceProperties properties = adapter.GetIPProperties();
            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.Equals(address))
                {
                    try
                    {
                        return properties.GetIPv4Properties()?.Index ?? -1;
                    }
                    catch (NetworkInformationException)
                    {
                        return -1;
                    }
                }
            }
        }

        throw new OptionException(
            $"{address} is not an address on any interface of this machine. "
            + "Check the address with Get-NetIPAddress.");
    }

    private static void PrintBanner(Options options, TestAdvertisement advertisement)
    {
        Console.WriteLine("SecretPrinter responder experiment");
        Console.WriteLine();
        Console.WriteLine("  THIS ADVERTISES A FAKE PRINTER THAT CANNOT PRINT.");
        Console.WriteLine("  It will appear on every Apple device on this network while it runs.");
        Console.WriteLine("  Selecting it will fail. Discovery is what is being measured.");
        Console.WriteLine();
        Console.WriteLine($"  Interface   : {options.InterfaceAddress}");
        Console.WriteLine($"  Instance    : {advertisement.Instance}");
        Console.WriteLine($"  Host        : {advertisement.Host} -> {options.InterfaceAddress}");
        Console.WriteLine($"  Port        : {options.Port} (nothing is listening there)");
        Console.WriteLine($"  Duration    : {options.Duration.TotalSeconds:0.#} s");
        Console.WriteLine();
        Console.WriteLine("  Advertising:");
        Console.WriteLine($"    {advertisement.ServiceType}");
        Console.WriteLine($"    {advertisement.AirPrintSubtype}");
        Console.WriteLine($"    {advertisement.ServiceEnumeration}");
        Console.WriteLine();
        Console.WriteLine("  TXT records:");
        foreach (string entry in advertisement.TxtStrings)
        {
            Console.WriteLine($"    {entry}");
        }

        Console.WriteLine();
        Console.WriteLine("  Not advertised, deliberately: Scan, Fax, mopria-certified, adminurl,");
        Console.WriteLine("  and the printer's UUID. This tool relays nothing, so it claims nothing.");
        Console.WriteLine();
    }
}

internal sealed class Stats
{
    private int _announcements;
    private int _queriesSeen;
    private int _unparseable;
    private int _wrongInterface;
    private int _unicastRequested;
    private long _bytesSent;

    public int QueriesAnswered { get; private set; }
    public bool GoodbyeSent { get; set; }

    public void RecordAnnouncement(int bytes)
    {
        _announcements++;
        _bytesSent += bytes;
    }

    public void RecordQuerySeen() => _queriesSeen++;

    public void RecordAnswered(int bytes)
    {
        QueriesAnswered++;
        _bytesSent += bytes;
    }

    public void RecordUnparseable() => _unparseable++;

    public void RecordWrongInterface() => _wrongInterface++;

    public void RecordUnicastRequested() => _unicastRequested++;

    public void Print(Options options)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("SUMMARY");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
        Console.WriteLine($"Announcements sent      : {_announcements}");
        Console.WriteLine($"Queries seen (our iface): {_queriesSeen}");
        Console.WriteLine($"Queries answered        : {QueriesAnswered}");
        Console.WriteLine($"Unicast-response asked  : {_unicastRequested}");
        Console.WriteLine($"Dropped, wrong interface: {_wrongInterface}");
        Console.WriteLine($"Unparseable packets     : {_unparseable}");
        Console.WriteLine($"Bytes sent              : {_bytesSent}");
        Console.WriteLine($"Goodbye records sent    : {(GoodbyeSent ? "yes" : "NO")}");
        Console.WriteLine();
        Console.WriteLine(new string('-', 72));

        if (QueriesAnswered > 0)
        {
            Console.WriteLine("A client on this network queried for the advertised service and was");
            Console.WriteLine("answered. Sending mDNS responses while another responder holds port 5353");
            Console.WriteLine("works on this machine.");
            Console.WriteLine();
            Console.WriteLine("What this does NOT tell you: whether the client ACCEPTED the answer and");
            Console.WriteLine("showed the printer. Only looking at the device's printer list tells you");
            Console.WriteLine("that. Answering is necessary, not sufficient.");
        }
        else if (_queriesSeen > 0)
        {
            Console.WriteLine("Queries arrived on this interface, but none were for the advertised");
            Console.WriteLine("service. Clients browse for printers only when something asks them to -");
            Console.WriteLine("open a print dialog on a device on this network and run again.");
        }
        else
        {
            Console.WriteLine("No queries arrived on this interface at all.");
            Console.WriteLine();
            Console.WriteLine("Check, in order: that a client is on the same network as");
            Console.WriteLine($"{options.InterfaceAddress}; that Windows Firewall permits inbound UDP 5353");
            Console.WriteLine("for this program; and that a print dialog was actually opened.");
        }
    }
}

internal sealed class OptionException(string message) : Exception(message);

internal sealed class Options
{
    public required IPAddress InterfaceAddress { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string InstanceName { get; init; }
    public required string HostLabel { get; init; }
    public required ushort Port { get; init; }
    public required bool Verbose { get; init; }

    /// <summary>Returns null when the caller asked for help.</summary>
    public static Options? Parse(string[] args)
    {
        IPAddress? address = null;
        double durationSeconds = 300;
        string instance = "SecretPrinter TEST - do not use";
        string host = "secretprinter-test";
        ushort port = 631;
        bool verbose = false;

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

                    address = parsed;
                    break;
                }

                case "--duration":
                {
                    string value = RequireValue(args, ref i, "--duration");
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds)
                        || durationSeconds is <= 0 or > 3600)
                    {
                        throw new OptionException(
                            $"--duration must be seconds between 0 and 3600; got '{value}'.");
                    }

                    break;
                }

                case "--instance":
                    instance = RequireValue(args, ref i, "--instance");
                    break;

                case "--host-label":
                    host = RequireValue(args, ref i, "--host-label");
                    break;

                case "--port":
                {
                    string value = RequireValue(args, ref i, "--port");
                    if (!ushort.TryParse(value, CultureInfo.InvariantCulture, out port) || port == 0)
                    {
                        throw new OptionException($"--port must be 1-65535; got '{value}'.");
                    }

                    break;
                }

                case "--verbose":
                    verbose = true;
                    break;

                default:
                    throw new OptionException($"Unrecognised argument '{args[i]}'.");
            }
        }

        if (address is null)
        {
            throw new OptionException(
                "--interface is required. This tool will not guess which network to advertise on, "
                + "because advertising onto the wrong one is the mistake worth preventing.");
        }

        if (instance.Length is 0 or > 63)
        {
            throw new OptionException(
                $"--instance must be 1-63 characters; got {instance.Length}. DNS labels are limited to 63 bytes.");
        }

        return new Options
        {
            InterfaceAddress = address,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            InstanceName = instance,
            HostLabel = host,
            Port = port,
            Verbose = verbose,
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
            SecretPrinter responder experiment.

            Advertises ONE FAKE PRINTER on ONE network, to find out whether mDNS
            responses can be sent while the Windows DNS Client service holds port 5353,
            and whether an iOS client will offer a printer advertised without IPPS.

            The fake printer cannot print. Selecting it will fail. That is expected;
            discovery is what is being measured.

            While this runs, the fake printer appears in the printer list of every Apple
            device on the named network. Goodbye records are sent on exit so it
            disappears promptly.

            Usage:
              SecretPrinter.Respond --interface <ipv4> [--duration <s>] [--instance <name>]
                                    [--host-label <label>] [--port <n>] [--verbose]

            Options:
              --interface <ipv4>   Local IPv4 address of the interface to advertise on.
                                   Required, and only one; the tool will not guess.
              --duration <s>       Seconds to run. Default 300. Ctrl+C stops early.
              --instance <name>    Advertised instance name.
                                   Default: 'SecretPrinter TEST - do not use'
              --host-label <label> Host label to publish. Default: 'secretprinter-test'
              --port <n>           Port to advertise. Default 631. Nothing listens there.
              --verbose            Print response destinations.
              --help               Show this text.

            Exit codes:
              0  At least one query for the advertised service was answered.
              1  No such query arrived.
              2  Bad arguments.
              3  Socket error.

            Answering a query does not prove the client accepted it. Look at the
            device's printer list for that.
            """);
}

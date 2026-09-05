// -----------------------------------------------------------------------------
// Program.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   A read-only diagnostic. It asks the local network what print services are
//   being advertised over mDNS, and prints exactly what came back.
//
// What this program does, in full:
//   1. Opens one UDP socket bound to an interface address you name explicitly,
//      on an operating-system-assigned ephemeral port.
//   2. Sends DNS-SD PTR queries to 224.0.0.251:5353 out of that interface.
//   3. Listens for replies until a timeout elapses.
//   4. Optionally sends a second round of SRV/TXT/A queries for whatever
//      instances round one revealed.
//   5. Prints a report and exits.
//
// What this program does NOT do:
//   - It does not bind port 5353, so it cannot disturb the Windows DNS Client
//     service or any other mDNS responder already running on this machine.
//   - It does not advertise, publish, respond to, or forward anything.
//   - It does not write files, touch the registry, or alter firewall rules.
//   - It does not transmit anything off the local link.
//
// Why ephemeral-port queries rather than binding 5353:
//   Port 5353 on this machine is already shared by several processes. Unicast
//   delivery to a shared port is not deterministic, so a probe bound there
//   could silently lose replies and mislead us. Querying from an ephemeral
//   port makes us a "legacy unicast querier" under RFC 6762 s6.7; responders
//   are required to answer us directly, and nobody else can intercept the
//   reply. The trade-off is honest and worth stating: this proves nothing
//   about whether the eventual service can share port 5353. That question is
//   deliberately left to a separate, later experiment.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SecretPrinter.Dns;

namespace SecretPrinter.Probe;

internal static class Program
{
    private static readonly IPAddress MdnsGroup = IPAddress.Parse("224.0.0.251");
    private const int MdnsPort = 5353;

    /// <summary>
    /// Service types queried when the caller does not name any. These are the
    /// printing-related types relevant to this project; nothing else is asked
    /// for, so nothing else can appear in the report.
    /// </summary>
    private static readonly string[] DefaultServiceTypes =
    [
        "_ipp._tcp.local",
        "_ipps._tcp.local",
        "_printer._tcp.local",
        "_pdl-datastream._tcp.local",
        "_universal._sub._ipp._tcp.local",
    ];

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Options? options = Options.Parse(args);
            if (options is null)
            {
                return 0; // --help was requested; usage already printed.
            }

            return await RunAsync(options).ConfigureAwait(false);
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
        Console.WriteLine("SecretPrinter mDNS probe (read-only diagnostic).");
        Console.WriteLine($"  Interface address : {options.InterfaceAddress}");
        Console.WriteLine($"  Query target      : {MdnsGroup}:{MdnsPort}");
        Console.WriteLine($"  Listen timeout    : {options.Timeout.TotalSeconds:0.#} s per round");
        Console.WriteLine();

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Bind to the chosen interface on an ephemeral port. Binding to a
        // specific address (rather than 0.0.0.0) is what guarantees our source
        // address, and therefore the return path for unicast replies.
        socket.Bind(new IPEndPoint(options.InterfaceAddress, 0));

        // Choose the egress interface for multicast explicitly. Without this,
        // the multicast query would follow the default route, which on a
        // multi-homed host is very likely the wrong network.
        socket.SetSocketOption(
            SocketOptionLevel.IP,
            SocketOptionName.MulticastInterface,
            options.InterfaceAddress.GetAddressBytes());

        // RFC 6762 s11 requires mDNS packets to carry IP TTL 255.
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);

        var localEndPoint = (IPEndPoint)socket.LocalEndPoint!;
        Console.WriteLine($"Listening on {localEndPoint}. Sending round 1 (service enumeration).");
        Console.WriteLine();

        // ---- Round 1: PTR queries for each service type -----------------------
        var round1 = new DnsQueryBuilder(NextQueryId());
        foreach (string serviceType in options.ServiceTypes)
        {
            round1.AddQuestion(DnsName.Parse(serviceType), DnsRecordType.Ptr, requestUnicastResponse: true);
        }

        List<Reply> replies = await QueryAsync(socket, round1.Build(), options, "1").ConfigureAwait(false);

        // Collect instance names from PTR answers.
        var instances = new List<DnsName>();
        foreach (Reply reply in replies)
        {
            foreach (DnsRecord record in reply.Message.AllRecords)
            {
                if (record.Type == DnsRecordType.Ptr && record.PtrTarget is { } target
                    && !instances.Contains(target))
                {
                    instances.Add(target);
                }
            }
        }

        // ---- Round 2: SRV/TXT for each instance, A for each SRV target --------
        if (instances.Count > 0)
        {
            Console.WriteLine($"Round 1 named {instances.Count} instance(s). Sending round 2 (details).");
            Console.WriteLine();

            var round2 = new DnsQueryBuilder(NextQueryId());
            foreach (DnsName instance in instances)
            {
                round2.AddQuestion(instance, DnsRecordType.Srv, requestUnicastResponse: true);
                round2.AddQuestion(instance, DnsRecordType.Txt, requestUnicastResponse: true);
            }

            replies.AddRange(await QueryAsync(socket, round2.Build(), options, "2").ConfigureAwait(false));

            // Any SRV target host we have not yet resolved gets an A query.
            var hosts = new List<DnsName>();
            var known = new List<DnsName>();
            foreach (Reply reply in replies)
            {
                foreach (DnsRecord record in reply.Message.AllRecords)
                {
                    if (record.Type == DnsRecordType.A)
                    {
                        known.Add(record.Name);
                    }
                }
            }

            foreach (Reply reply in replies)
            {
                foreach (DnsRecord record in reply.Message.AllRecords)
                {
                    if (record.Type == DnsRecordType.Srv && record.SrvTarget is { } target
                        && !known.Contains(target) && !hosts.Contains(target))
                    {
                        hosts.Add(target);
                    }
                }
            }

            if (hosts.Count > 0)
            {
                Console.WriteLine($"Resolving {hosts.Count} host name(s). Sending round 3 (addresses).");
                Console.WriteLine();

                var round3 = new DnsQueryBuilder(NextQueryId());
                foreach (DnsName host in hosts)
                {
                    round3.AddQuestion(host, DnsRecordType.A, requestUnicastResponse: true);
                }

                replies.AddRange(await QueryAsync(socket, round3.Build(), options, "3").ConfigureAwait(false));
            }
        }

        Report.Print(replies, options.ServiceTypes, options.ShowRaw);
        return replies.Count > 0 ? 0 : 1;
    }

    /// <summary>Sends one query and collects replies until the timeout elapses.</summary>
    private static async Task<List<Reply>> QueryAsync(Socket socket, byte[] query, Options options, string label)
    {
        await socket.SendToAsync(query, SocketFlags.None, new IPEndPoint(MdnsGroup, MdnsPort))
                    .ConfigureAwait(false);

        var replies = new List<Reply>();
        var buffer = new byte[9000]; // Larger than any mDNS message we expect.

        using var cancellation = new CancellationTokenSource(options.Timeout);

        while (true)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await socket.ReceiveFromAsync(
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

            try
            {
                DnsMessage message = DnsMessage.Parse(buffer, result.ReceivedBytes);
                if (!message.IsResponse)
                {
                    continue; // Someone else's query, not an answer to ours.
                }

                var raw = new byte[result.ReceivedBytes];
                Array.Copy(buffer, raw, result.ReceivedBytes);
                replies.Add(new Reply(source, message, raw));

                Console.WriteLine(
                    $"  [round {label}] {result.ReceivedBytes,5} bytes from {source.Address}: "
                    + $"{message.Answers.Count} answer(s), {message.Additionals.Count} additional(s)");
            }
            catch (InvalidDataException ex)
            {
                // Report rather than hide. A malformed reply is itself a finding.
                Console.WriteLine($"  [round {label}] unparseable {result.ReceivedBytes} bytes from " +
                                  $"{source.Address}: {ex.Message}");
            }
        }

        Console.WriteLine();
        return replies;
    }

    private static ushort NextQueryId() => (ushort)Random.Shared.Next(1, ushort.MaxValue);
}

/// <summary>One reply, retained with its source and original bytes.</summary>
internal sealed record Reply(IPEndPoint Source, DnsMessage Message, byte[] RawBytes);

internal sealed class OptionException(string message) : Exception(message);

/// <summary>Command-line options. Every setting is explicit; there are no hidden defaults beyond those printed at startup.</summary>
internal sealed class Options
{
    public required IPAddress InterfaceAddress { get; init; }
    public required IReadOnlyList<string> ServiceTypes { get; init; }
    public required TimeSpan Timeout { get; init; }
    public required bool ShowRaw { get; init; }

    /// <summary>Returns null when the caller asked for help.</summary>
    public static Options? Parse(string[] args)
    {
        IPAddress? interfaceAddress = null;
        var serviceTypes = new List<string>();
        double timeoutSeconds = 5;
        bool showRaw = false;

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

                    interfaceAddress = parsed;
                    break;
                }

                case "--service":
                    serviceTypes.Add(RequireValue(args, ref i, "--service"));
                    break;

                case "--timeout":
                {
                    string value = RequireValue(args, ref i, "--timeout");
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out timeoutSeconds)
                        || timeoutSeconds is <= 0 or > 300)
                    {
                        throw new OptionException($"--timeout must be a number of seconds between 0 and 300; got '{value}'.");
                    }

                    break;
                }

                case "--raw":
                    showRaw = true;
                    break;

                default:
                    throw new OptionException($"Unrecognised argument '{args[i]}'.");
            }
        }

        if (interfaceAddress is null)
        {
            throw new OptionException(
                "--interface is required. This tool will not guess which network to query.");
        }

        return new Options
        {
            InterfaceAddress = interfaceAddress,
            ServiceTypes = serviceTypes.Count > 0 ? serviceTypes : DefaultServiceTypesCopy(),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            ShowRaw = showRaw,
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

    private static List<string> DefaultServiceTypesCopy() =>
    [
        "_ipp._tcp.local",
        "_ipps._tcp.local",
        "_printer._tcp.local",
        "_pdl-datastream._tcp.local",
        "_universal._sub._ipp._tcp.local",
    ];

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SecretPrinter mDNS probe - a read-only diagnostic.

            Asks one network what print services it advertises, and prints the answers.
            Sends nothing except DNS-SD queries. Advertises nothing. Changes nothing.

            Usage:
              SecretPrinter.Probe --interface <ipv4> [--service <type>]... [--timeout <s>] [--raw]

            Options:
              --interface <ipv4>  Local IPv4 address of the interface to query from.
                                  Required; the tool will not choose for you.
              --service <type>    Service type to enumerate, e.g. _ipp._tcp.local.
                                  May be repeated. Defaults to the printing types.
              --timeout <s>       Seconds to listen after each query. Default 5.
              --raw               Also print a hex dump of every reply.
              --help              Show this text.

            Exit codes:
              0  At least one reply was received.
              1  No replies were received.
              2  Bad arguments.
              3  Socket error.
            """);
    }
}

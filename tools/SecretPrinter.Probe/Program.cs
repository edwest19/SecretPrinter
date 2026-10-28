// -----------------------------------------------------------------------------
// Program.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Step 4 of "What this program does" corrected by Claude (Anthropic model,
// Claude Opus 5.5) at the direction of Edwin West, 2026-10-07. It described two
// rounds of questions. The code has sent a third, for addresses, since the
// first commit. Comment only; no behaviour changed. Reviewed by a human before
// merge.
//
// Which replies are kept changed by Claude (Anthropic model, Claude Opus 5.5) at
// the direction of Edwin West, 2026-10-28 (REQ-SEC-018). A reply is now kept
// only when it came from UDP port 5353 and carries the identifier of the query
// the round sent, as RFC 6762 s6 and s6.7 require; any other datagram is
// printed as ignored, with why. Until then every DNS response that reached the
// socket was kept, from any port and with any identifier. The decision is made
// in Judge, which the receive loop hands every datagram to. Reviewed by a human
// before merge.
//
// Purpose:
//   A read-only diagnostic. It asks the local network what print services are
//   being advertised over mDNS, and prints exactly what came back.
//
// What this program does, in full:
//   1. Opens one UDP socket bound to an interface address you name explicitly,
//      on an operating-system-assigned ephemeral port.
//   2. Sends DNS-SD PTR queries to 224.0.0.251:5353 out of that interface.
//   3. Listens for replies until a timeout elapses. It keeps a reply only when
//      it came from port 5353 and carries the identifier of the query just
//      sent, and prints a line for any other datagram saying why it was not.
//   4. If round one named any instance, sends a second round of SRV and TXT
//      queries for each. If that leaves an SRV target with no address, sends
//      a third round of A queries for those host names.
//   5. Prints a report and exits.
//
// What this program does NOT do:
//   - It does not bind port 5353, so it cannot disturb the Windows DNS Client
//     service or any other mDNS responder already running on this machine.
//   - It does not advertise, publish, respond to, or forward anything.
//   - It does not write files, touch the registry, or alter firewall rules.
//   - It does not transmit anything off the local link.
//
// Which of those are checked (note added by Claude (Anthropic model, Claude
// Opus 5.5) at the direction of Edwin West, 2026-10-06; no code changed):
//   Since that day ProbeClaimsTests, in SecretPrinter.Service.Tests, reads this
//   assembly and the two it is built on, and fails if any of them references a
//   type that can write a file, reach the registry, start another program
//   (which is what altering a firewall rule would take) or make an HTTP request
//   (REQ-SEC-016). The other lines above are not checked by any test: that it
//   does not bind port 5353, that it does not advertise, respond or forward,
//   and that nothing leaves the local link. Those are read from the code below.
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
using SecretPrinter.Spec;

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
        ushort round1Id = NextQueryId();
        var round1 = new DnsQueryBuilder(round1Id);
        foreach (string serviceType in options.ServiceTypes)
        {
            round1.AddQuestion(DnsName.Parse(serviceType), DnsRecordType.Ptr, requestUnicastResponse: true);
        }

        List<Reply> replies = await QueryAsync(socket, round1.Build(), round1Id, options, "1").ConfigureAwait(false);

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

            ushort round2Id = NextQueryId();
            var round2 = new DnsQueryBuilder(round2Id);
            foreach (DnsName instance in instances)
            {
                round2.AddQuestion(instance, DnsRecordType.Srv, requestUnicastResponse: true);
                round2.AddQuestion(instance, DnsRecordType.Txt, requestUnicastResponse: true);
            }

            replies.AddRange(await QueryAsync(socket, round2.Build(), round2Id, options, "2").ConfigureAwait(false));

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

                ushort round3Id = NextQueryId();
                var round3 = new DnsQueryBuilder(round3Id);
                foreach (DnsName host in hosts)
                {
                    round3.AddQuestion(host, DnsRecordType.A, requestUnicastResponse: true);
                }

                replies.AddRange(await QueryAsync(socket, round3.Build(), round3Id, options, "3").ConfigureAwait(false));
            }
        }

        Report.Print(replies, options.ServiceTypes, options.ShowRaw);
        return replies.Count > 0 ? 0 : 1;
    }

    /// <summary>Sends one query and collects replies until the timeout elapses.</summary>
    /// <param name="queryId">The identifier <paramref name="query"/> was built with.</param>
    private static async Task<List<Reply>> QueryAsync(
        Socket socket, byte[] query, ushort queryId, Options options, string label)
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
            Arrival arrival = Judge(source, buffer, result.ReceivedBytes, queryId, label);

            if (arrival.Line is { } line)
            {
                Console.WriteLine(line);
            }

            if (arrival.Reply is { } reply)
            {
                replies.Add(reply);
            }
        }

        Console.WriteLine();
        return replies;
    }

    /// <summary>
    /// Decides what one datagram that reached the probe's socket is: a reply to
    /// keep, or not, and the line to print about it.
    /// </summary>
    /// <remarks>
    /// The probe asks from a port the operating system chooses, so responders
    /// answer it as a legacy querier, by unicast (RFC 6762 s6.7). RFC 6762 s6:
    /// "The source UDP port in all Multicast DNS responses MUST be 5353", and
    /// implementations "MUST silently ignore any Multicast DNS responses they
    /// receive where the source UDP port is not 5353"; and a querier "MUST only
    /// accept unicast responses if they answer a recently sent query". s6.7: a
    /// legacy unicast response "MUST repeat the query ID and the question given
    /// in the query message". So a real answer always comes from port 5353 and
    /// carries the identifier of the query it answers, and neither check loses
    /// one. "Silently" is taken to mean that nothing is sent back and nothing
    /// is kept: this is a diagnostic, so it still prints a line saying what it
    /// ignored and why. The port is checked before the datagram is read.
    /// </remarks>
    [Requirement("REQ-SEC-018",
        "Keeps a reply only when it came from UDP port 5353 and carries the identifier of the round's query; prints any other datagram as ignored, with why, and keeps nothing of it.")]
    internal static Arrival Judge(IPEndPoint source, byte[] datagram, int length, ushort queryId, string label)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(datagram);

        if (source.Port != MdnsPort)
        {
            return new Arrival(
                null,
                $"  [round {label}] {length,5} bytes from {source.Address} port {source.Port}: "
                + $"ignored, not sent from port {MdnsPort} (RFC 6762 s6).");
        }

        DnsMessage message;
        try
        {
            message = DnsMessage.Parse(datagram, length);
        }
        catch (InvalidDataException ex)
        {
            // Report rather than hide. A malformed reply is itself a finding.
            return new Arrival(null, $"  [round {label}] unparseable {length} bytes from {source.Address}: {ex.Message}");
        }

        if (!message.IsResponse)
        {
            return new Arrival(null, null); // Someone else's query, not an answer to ours.
        }

        if (message.Id != queryId)
        {
            return new Arrival(
                null,
                $"  [round {label}] {length,5} bytes from {source.Address}: ignored, query ID {message.Id} "
                + $"is not this round's, {queryId} (RFC 6762 s6, s6.7).");
        }

        var raw = new byte[length];
        Array.Copy(datagram, raw, length);

        return new Arrival(
            new Reply(source, message, raw),
            $"  [round {label}] {length,5} bytes from {source.Address}: "
            + $"{message.Answers.Count} answer(s), {message.Additionals.Count} additional(s)");
    }

    private static ushort NextQueryId() => (ushort)Random.Shared.Next(1, ushort.MaxValue);
}

/// <summary>One reply, retained with its source and original bytes.</summary>
internal sealed record Reply(IPEndPoint Source, DnsMessage Message, byte[] RawBytes);

/// <summary>What the probe makes of one datagram that reached its socket.</summary>
/// <param name="Reply">The reply to keep, or null when the datagram is not kept.</param>
/// <param name="Line">The line to print about it, or null to print nothing.</param>
internal sealed record Arrival(Reply? Reply, string? Line);

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

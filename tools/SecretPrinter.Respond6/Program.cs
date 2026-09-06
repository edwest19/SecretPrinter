// Program.cs
//
// SecretPrinter.Respond6 - an experiment, not a product.
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// THE ONE QUESTION THIS EXPERIMENT EXISTS TO ANSWER
// -------------------------------------------------
// A packet capture taken on 2026-09-06 on FIOS-STB-01 showed an iPhone issuing
// mDNS queries exclusively over IPv6 (to ff02::fb), including queries for
// "_universal._sub._ipp._tcp.local" and for A, AAAA and HTTPS records of
// "secretprinter.local". SecretPrinter.Service was running at the time but was
// bound only to 0.0.0.0:5353, so it never received those queries and sent
// nothing at all. Zero IPv4 mDNS appeared on that segment.
//
// Before adding IPv6 to the shipping service, we need to know which shape the
// fix has to take. The open question:
//
//     Will iOS accept an A record (IPv4 only, no AAAA) delivered over IPv6
//     mDNS transport, and then open an IPP connection over IPv4 to port 631?
//
// If YES: the fix is an additional transport in SecretPrinter.Mdns. The proxy,
//         advertising and resolution layers are unaffected.
// If NO:  IPv6 has to reach into the proxy and the SRV/TXT layers as well,
//         because the printer itself is IPv4-only at 192.168.12.180.
//
// Those are very different amounts of work, and this project answers questions
// like this by measurement rather than by reasoning. The "_universal._sub"
// requirement (REQ-ADV-002) was found the same way.
//
// HOW THIS ANSWERS IT
// -------------------
// This tool listens on IPv6 mDNS only and answers with an A record and nothing
// else. It deliberately returns NSEC to state that no AAAA record exists, so
// iOS gets a definite negative answer instead of retrying until it gives up.
//
// The result is read off Wireshark, not off this program's output:
//
//     TCP SYN to <this host>:631  ==>  answer is YES.
//     No SYN, only repeated mDNS  ==>  answer is NO.
//
// A connection that is refused still counts as YES. We are measuring whether
// iOS ATTEMPTS the connection, so this tool does not listen on port 631 at all
// and does not need SecretPrinter.Service running. Run it with the service
// STOPPED, so that exactly one responder is answering for these names.
//
// WHAT THIS TOOL DOES NOT DO
// --------------------------
//   * It does not print anything, proxy anything, or touch the real printer.
//   * It does not open any TCP socket.
//   * It does not read or write any file except an optional TXT record file
//     that you pass explicitly on the command line.
//   * It does not modify firewall rules, the registry, or any system setting.
//   * It answers only for the names given on its command line, and only on
//     UDP 5353 to the group ff02::fb on the single interface you name.
//
// It is not signed and must never be shipped as a release artifact.

using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SecretPrinter.Respond6;

internal static class Program
{
    /// <summary>The IPv6 link-local mDNS group, the counterpart of 224.0.0.251.</summary>
    private static readonly IPAddress MdnsGroupV6 = IPAddress.Parse("ff02::fb");

    private const int MdnsPort = 5353;

    // RFC 6762 section 10: records that contain a host name get a 120 second TTL,
    // everything else gets 75 minutes.
    private const uint TtlHostName = 120;
    private const uint TtlOther = 4500;

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("SecretPrinter.Respond6 - IPv6 mDNS experiment");
        Console.WriteLine(
            "Written by Claude (Anthropic model, Claude Opus 5). "
            + "Experiment only; not a release artifact.");
        Console.WriteLine();

        Options options;
        try
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintUsage();
                return 0;
            }

            if (args.Contains("--list"))
            {
                ListInterfaces();
                return 0;
            }

            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            Console.Error.WriteLine();
            PrintUsage();
            return 2;
        }

        NetworkInterface nic;
        int interfaceIndex;
        try
        {
            (nic, interfaceIndex) = ResolveInterface(options.InterfaceName);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Interface error: {ex.Message}");
            Console.Error.WriteLine("Run with --list to see interface names.");
            return 2;
        }

        string instanceFqdn = $"{options.InstanceName}._ipp._tcp.local";

        Console.WriteLine("Configuration (all values explicit, no hidden defaults applied):");
        Console.WriteLine($"  Interface        : {nic.Name} (IPv6 index {interfaceIndex})");
        Console.WriteLine($"  Service instance : {instanceFqdn}");
        Console.WriteLine($"  Host name        : {options.HostName}");
        Console.WriteLine($"  A record value   : {options.IPv4Address}");
        Console.WriteLine($"  SRV port         : {options.Port}");
        Console.WriteLine($"  TXT entries      : {options.TxtEntries.Count}");
        foreach (string entry in options.TxtEntries)
        {
            Console.WriteLine($"                     {entry}");
        }

        Console.WriteLine();
        Console.WriteLine("This tool answers ONLY the names above, ONLY on UDP 5353 to ff02::fb,");
        Console.WriteLine("ONLY on the interface above. It opens no TCP socket and prints nothing.");
        Console.WriteLine();

        using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            // Port 5353 is shared with the Windows Dnscache service and any other
            // mDNS participant on this host, so address reuse is required.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // IPv6 only. This experiment must not accidentally answer over IPv4,
            // or the result would not isolate the variable under test.
            socket.DualMode = false;

            socket.Bind(new IPEndPoint(IPAddress.IPv6Any, MdnsPort));

            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.AddMembership,
                new IPv6MulticastOption(MdnsGroupV6, interfaceIndex));

            // IPV6_MULTICAST_IF takes a plain interface index. Do not trust this
            // line on assurance alone: the run instructions have you confirm in
            // Wireshark that responses actually egress the named interface.
            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.MulticastInterface,
                interfaceIndex);

            // mDNS requires hop limit 255 so receivers can reject off-link forgeries.
            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.MulticastTimeToLive,
                255);

            // We do not want to receive our own announcements.
            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.MulticastLoopback,
                false);
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Socket setup failed: {ex.SocketErrorCode} - {ex.Message}");
            Console.Error.WriteLine("Failing fast rather than running in a partial state.");
            return 3;
        }

        Console.WriteLine($"Listening on [{MdnsGroupV6}]:{MdnsPort} via {nic.Name}.");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Shutdown requested.");
            shutdown.Cancel();
        };

        var target = new IPEndPoint(MdnsGroupV6, MdnsPort);

        try
        {
            await AnnounceAsync(socket, target, options, instanceFqdn, shutdown.Token);
            await ReceiveLoopAsync(socket, target, options, instanceFqdn, shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C.
        }

        try
        {
            socket.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.DropMembership,
                new IPv6MulticastOption(MdnsGroupV6, interfaceIndex));
        }
        catch (SocketException ex)
        {
            Console.Error.WriteLine($"Note: leaving the multicast group failed: {ex.SocketErrorCode}");
        }

        Console.WriteLine("Stopped cleanly.");
        return 0;
    }

    /// <summary>
    /// Sends unsolicited announcements so iOS can discover the service without
    /// waiting for its own query timer. RFC 6762 section 8.3 calls for at least
    /// two, one second apart.
    ///
    /// Note: conflict probing (section 8.1) is NOT performed. That is acceptable
    /// for a short manual experiment on names we control, and unacceptable for
    /// the shipping service.
    /// </summary>
    private static async Task AnnounceAsync(
        Socket socket,
        IPEndPoint target,
        Options options,
        string instanceFqdn,
        CancellationToken token)
    {
        const int announcements = 3;

        for (int i = 1; i <= announcements; i++)
        {
            token.ThrowIfCancellationRequested();

            var builder = new DnsResponseBuilder();
            builder.AddPtr("_universal._sub._ipp._tcp.local", instanceFqdn, TtlOther);
            builder.AddPtr("_ipp._tcp.local", instanceFqdn, TtlOther);
            builder.AddSrv(instanceFqdn, 0, 0, options.Port, options.HostName, TtlHostName);
            builder.AddTxt(instanceFqdn, options.TxtEntries, TtlOther);
            builder.AddA(options.HostName, options.IPv4Address.GetAddressBytes(), TtlHostName);
            builder.AddNsecAOnly(options.HostName, TtlHostName);

            byte[] message = builder.Build();
            await socket.SendToAsync(message, SocketFlags.None, target, token);

            Log($"ANNOUNCE {i}/{announcements}: {builder.AnswerCount} records, {message.Length} bytes");

            if (i < announcements)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }

        Console.WriteLine();
    }

    private static async Task ReceiveLoopAsync(
        Socket socket,
        IPEndPoint target,
        Options options,
        string instanceFqdn,
        CancellationToken token)
    {
        byte[] buffer = new byte[9000];
        EndPoint remote = new IPEndPoint(IPAddress.IPv6Any, 0);

        while (!token.IsCancellationRequested)
        {
            SocketReceiveFromResult result;
            try
            {
                result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, token);
            }
            catch (SocketException ex)
            {
                // A transient receive error must be logged but must not kill the loop.
                Log($"receive error (continuing): {ex.SocketErrorCode}");
                continue;
            }

            var span = new ReadOnlySpan<byte>(buffer, 0, result.ReceivedBytes);

            if (!DnsReader.TryReadQuestions(span, out _, out List<DnsQuestion> questions))
            {
                continue;
            }

            if (questions.Count == 0)
            {
                continue;
            }

            var plan = ResponsePlan.For(questions, options, instanceFqdn);

            string summary = string.Join(", ",
                questions.Select(q => $"{q.Name} [{RrType.Name(q.QType)}]"));

            if (!plan.HasAnything)
            {
                Log($"query from {result.RemoteEndPoint}: {summary} -> not ours, ignored");
                continue;
            }

            byte[] message = plan.Build(options, instanceFqdn);
            await socket.SendToAsync(message, SocketFlags.None, target, token);

            Log($"query from {result.RemoteEndPoint}: {summary}");
            Log($"  -> answered: {plan.Describe()} ({message.Length} bytes)");
        }
    }

    /// <summary>
    /// Decides which records to send for a set of questions. Kept separate from
    /// the socket code so the decision is readable on its own.
    /// </summary>
    private sealed class ResponsePlan
    {
        private bool _ptrSubtype;
        private bool _ptrBase;
        private bool _srv;
        private bool _txt;
        private bool _a;
        private bool _nsecDirect;

        // Records we were not asked for but that save the client a round trip.
        private bool _srvExtra;
        private bool _txtExtra;
        private bool _aExtra;
        private bool _nsecExtra;

        public bool HasAnything =>
            _ptrSubtype || _ptrBase || _srv || _txt || _a || _nsecDirect;

        public static ResponsePlan For(
            IReadOnlyList<DnsQuestion> questions,
            Options options,
            string instanceFqdn)
        {
            var plan = new ResponsePlan();

            foreach (DnsQuestion q in questions)
            {
                bool any = q.QType == RrType.ANY;

                if (Same(q.Name, "_universal._sub._ipp._tcp.local")
                    && (q.QType == RrType.PTR || any))
                {
                    plan._ptrSubtype = true;
                }
                else if (Same(q.Name, "_ipp._tcp.local")
                    && (q.QType == RrType.PTR || any))
                {
                    plan._ptrBase = true;
                }
                else if (Same(q.Name, instanceFqdn))
                {
                    if (q.QType == RrType.SRV || any)
                    {
                        plan._srv = true;
                    }

                    if (q.QType == RrType.TXT || any)
                    {
                        plan._txt = true;
                    }
                }
                else if (Same(q.Name, options.HostName))
                {
                    if (q.QType == RrType.A || any)
                    {
                        plan._a = true;
                    }

                    // AAAA and HTTPS/SVCB both get a definite "does not exist"
                    // rather than silence. This is the crux of the experiment.
                    if (q.QType == RrType.AAAA || q.QType == RrType.HTTPS)
                    {
                        plan._nsecDirect = true;
                    }
                }
            }

            // A PTR answer is useless on its own, so bundle the resolution chain.
            if (plan._ptrSubtype || plan._ptrBase)
            {
                plan._srvExtra = !plan._srv;
                plan._txtExtra = !plan._txt;
                plan._aExtra = !plan._a;
                plan._nsecExtra = !plan._nsecDirect;
            }

            // An SRV answer points at a host name, so include its address.
            if (plan._srv && !plan._a)
            {
                plan._aExtra = true;
                plan._nsecExtra = !plan._nsecDirect;
            }

            return plan;
        }

        public byte[] Build(Options options, string instanceFqdn)
        {
            var b = new DnsResponseBuilder();

            if (_ptrSubtype)
            {
                b.AddPtr("_universal._sub._ipp._tcp.local", instanceFqdn, TtlOther);
            }

            if (_ptrBase)
            {
                b.AddPtr("_ipp._tcp.local", instanceFqdn, TtlOther);
            }

            if (_srv)
            {
                b.AddSrv(instanceFqdn, 0, 0, options.Port, options.HostName, TtlHostName);
            }

            if (_txt)
            {
                b.AddTxt(instanceFqdn, options.TxtEntries, TtlOther);
            }

            if (_a)
            {
                b.AddA(options.HostName, options.IPv4Address.GetAddressBytes(), TtlHostName);
            }

            if (_nsecDirect)
            {
                b.AddNsecAOnly(options.HostName, TtlHostName);
            }

            if (_srvExtra)
            {
                b.AddSrv(instanceFqdn, 0, 0, options.Port, options.HostName, TtlHostName, additional: true);
            }

            if (_txtExtra)
            {
                b.AddTxt(instanceFqdn, options.TxtEntries, TtlOther, additional: true);
            }

            if (_aExtra)
            {
                b.AddA(options.HostName, options.IPv4Address.GetAddressBytes(), TtlHostName, additional: true);
            }

            if (_nsecExtra)
            {
                b.AddNsecAOnly(options.HostName, TtlHostName, additional: true);
            }

            return b.Build();
        }

        public string Describe()
        {
            var parts = new List<string>();

            if (_ptrSubtype) parts.Add("PTR(_universal._sub)");
            if (_ptrBase) parts.Add("PTR(_ipp)");
            if (_srv) parts.Add("SRV");
            if (_txt) parts.Add("TXT");
            if (_a) parts.Add("A");
            if (_nsecDirect) parts.Add("NSEC(no AAAA)");
            if (_srvExtra) parts.Add("+SRV");
            if (_txtExtra) parts.Add("+TXT");
            if (_aExtra) parts.Add("+A");
            if (_nsecExtra) parts.Add("+NSEC(no AAAA)");

            return string.Join(" ", parts);
        }

        private static bool Same(string a, string b) =>
            string.Equals(a.TrimEnd('.'), b.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
    }

    private static (NetworkInterface Nic, int Index) ResolveInterface(string name)
    {
        foreach (NetworkInterface candidate in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.OperationalStatus != OperationalStatus.Up)
            {
                throw new ArgumentException($"Interface '{name}' is not up.");
            }

            if (!candidate.Supports(NetworkInterfaceComponent.IPv6))
            {
                throw new ArgumentException($"Interface '{name}' does not support IPv6.");
            }

            try
            {
                int index = candidate.GetIPProperties().GetIPv6Properties().Index;
                return (candidate, index);
            }
            catch (NetworkInformationException ex)
            {
                throw new ArgumentException(
                    $"Interface '{name}' has no usable IPv6 properties: {ex.Message}");
            }
        }

        throw new ArgumentException($"No interface named '{name}'.");
    }

    private static void ListInterfaces()
    {
        Console.WriteLine("Interfaces on this machine:");
        Console.WriteLine();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            string index;
            try
            {
                // Invariant culture: this is a diagnostic identifier, not a
                // number to be presented in the operator's local format.
                index = nic.GetIPProperties()
                    .GetIPv6Properties()
                    .Index
                    .ToString(CultureInfo.InvariantCulture);
            }
            catch (NetworkInformationException)
            {
                index = "no IPv6";
            }

            Console.WriteLine($"  {nic.Name}");
            Console.WriteLine($"      status      : {nic.OperationalStatus}");
            Console.WriteLine($"      IPv6 index  : {index}");

            foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
            {
                Console.WriteLine($"      address     : {addr.Address}");
            }

            Console.WriteLine();
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SecretPrinter.Respond6 --list");
        Console.WriteLine("  SecretPrinter.Respond6 --interface <name> --ipv4 <address> [options]");
        Console.WriteLine();
        Console.WriteLine("Required:");
        Console.WriteLine("  --interface <name>   Interface to join ff02::fb on, e.g. \"Ethernet\".");
        Console.WriteLine("  --ipv4 <address>     Value to put in the A record, e.g. 192.168.1.163.");
        Console.WriteLine();
        Console.WriteLine("Optional:");
        Console.WriteLine("  --instance <name>    Service instance name.");
        Console.WriteLine("                       Default: SecretPrinter (ET-3760)");
        Console.WriteLine("  --host <name>        Host name for the SRV target and A record.");
        Console.WriteLine("                       Default: secretprinter.local");
        Console.WriteLine("  --port <number>      SRV port. Default: 631");
        Console.WriteLine("  --txt <file>         File of TXT entries, one key=value per line.");
        Console.WriteLine("                       Lines starting with # are ignored.");
        Console.WriteLine("                       If omitted, a small built-in set is used and");
        Console.WriteLine("                       its unverified values are printed at startup.");
        Console.WriteLine("  --list               List interfaces and exit.");
    }

    private static void Log(string message) =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

    /// <summary>Command line options. Every value is explicit; nothing is auto-detected.</summary>
    private sealed class Options
    {
        public required string InterfaceName { get; init; }

        public required IPAddress IPv4Address { get; init; }

        public required string InstanceName { get; init; }

        public required string HostName { get; init; }

        public required ushort Port { get; init; }

        /// <summary>
        /// TXT record entries, one "key=value" string per record entry.
        /// Typed as the concrete List&lt;string&gt; rather than IReadOnlyList&lt;string&gt;
        /// because CA1859 is enforced as an error in this repository and Options
        /// is a private nested type with no external consumers to insulate.
        /// </summary>
        public required List<string> TxtEntries { get; init; }

        public static Options Parse(string[] args)
        {
            string? interfaceName = null;
            string? ipv4 = null;
            string instance = "SecretPrinter (ET-3760)";
            string host = "secretprinter.local";
            ushort port = 631;
            string? txtFile = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--interface":
                        interfaceName = Next(args, ref i);
                        break;
                    case "--ipv4":
                        ipv4 = Next(args, ref i);
                        break;
                    case "--instance":
                        instance = Next(args, ref i);
                        break;
                    case "--host":
                        host = Next(args, ref i);
                        break;
                    case "--port":
                        string raw = Next(args, ref i);
                        if (!ushort.TryParse(raw, out port) || port == 0)
                        {
                            throw new ArgumentException($"Not a valid port: {raw}");
                        }

                        break;
                    case "--txt":
                        txtFile = Next(args, ref i);
                        break;
                    default:
                        throw new ArgumentException($"Unrecognised argument: {args[i]}");
                }
            }

            if (interfaceName is null)
            {
                throw new ArgumentException("--interface is required.");
            }

            if (ipv4 is null)
            {
                throw new ArgumentException("--ipv4 is required.");
            }

            if (!IPAddress.TryParse(ipv4, out IPAddress? parsed)
                || parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException($"Not a valid IPv4 address: {ipv4}");
            }

            if (!host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Host name must end in .local: {host}");
            }

            return new Options
            {
                InterfaceName = interfaceName,
                IPv4Address = parsed,
                InstanceName = instance,
                HostName = host,
                Port = port,
                TxtEntries = LoadTxt(txtFile, instance),
            };
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"{args[i]} needs a value.");
            }

            return args[++i];
        }

        // Returns the concrete List<string> rather than IReadOnlyList<string>,
        // as required by CA1859, which this repository enforces as an error.
        private static List<string> LoadTxt(string? path, string instance)
        {
            if (path is not null)
            {
                if (!File.Exists(path))
                {
                    throw new ArgumentException($"TXT file not found: {path}");
                }

                var loaded = new List<string>();

                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string trimmed = line.Trim();

                    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    loaded.Add(trimmed);
                }

                return loaded;
            }

            // Built-in fallback. These are a plausible minimal AirPrint TXT set,
            // NOT values measured from the real ET-3760. If iOS declines to show
            // or connect to the printer, replace them with output from
            // SecretPrinter.Probe using --txt rather than adjusting them by guess.
            Console.WriteLine("WARNING: using built-in TXT entries. These are plausible defaults,");
            Console.WriteLine("         not values measured from the ET-3760. If iOS does not show");
            Console.WriteLine("         the printer, supply real values with --txt before drawing");
            Console.WriteLine("         any conclusion from this experiment.");
            Console.WriteLine();

            return new List<string>
            {
                "txtvers=1",
                "qtotal=1",
                "rp=ipp/print",
                $"ty={instance}",
                "pdl=application/octet-stream,image/urf,image/jpeg,application/pdf",
                "Color=T",
                "Duplex=T",
                "note=SecretPrinter.Respond6 experiment",
            };
        }
    }
}

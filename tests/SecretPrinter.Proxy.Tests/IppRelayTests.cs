// -----------------------------------------------------------------------------
// IppRelayTests.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Verifies the print path: that bytes arrive unchanged, that both sides close
//   together, that failures are prompt and reported, and - the two claims the
//   README leads with - that job data is never altered and never written to
//   disk.
//
//   Those last two are statements about what the code does NOT do, so two of
//   these tests are structural rather than behavioural. Assembly_writes_no_files
//   reads the compiled assembly's own metadata and asserts it references no
//   file-writing type at all. That is a stronger check than any behavioural test
//   could be: it fails if someone so much as adds a using of File, whether or
//   not the path is ever taken.
// -----------------------------------------------------------------------------

using System.Net;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using SecretPrinter.Spec;
using SecretPrinter.TestKit;

namespace SecretPrinter.Proxy.Tests;

internal static class IppRelayTests
{
    private static readonly IPEndPoint PrinterEndpoint =
        new(IPAddress.Parse("192.168.12.180"), 631);

    private static readonly IPEndPoint ClientEndpoint =
        new(IPAddress.Parse("192.168.1.41"), 49152);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <param name="ClientSide">The far end of the client link; the TEST writes here.</param>
    /// <param name="PrinterSide">The far end of the printer link; the TEST writes replies here.</param>
    /// <param name="ToClient">The relay's own end of the client link; what the RELAY wrote is here.</param>
    /// <param name="ToPrinter">The relay's own end of the printer link; what the RELAY wrote is here.</param>
    private sealed record Harness(
        IppRelay Relay,
        FakeConnection Client,
        RecordingStream ClientSide,
        RecordingStream PrinterSide,
        RecordingStream ToClient,
        RecordingStream ToPrinter,
        FakeConnectionFactory Factory,
        RecordingObserver Observer);

    private static Harness Build(RelayOptions? options = null, EndPoint? clientEndpoint = null)
    {
        StreamPair clientLink = StreamPair.Create();
        StreamPair printerLink = StreamPair.Create();

        var client = new FakeConnection(clientLink.Left, clientEndpoint ?? ClientEndpoint);
        var factory = new FakeConnectionFactory
        {
            OnConnect = _ => new FakeConnection(printerLink.Left, PrinterEndpoint),
        };

        var observer = new RecordingObserver();

        var relay = new IppRelay(
            new NeverAcceptsListener(),
            factory,
            _ => Task.FromResult(PrinterEndpoint),
            options ?? new RelayOptions(),
            observer);

        return new Harness(
            relay, client,
            ClientSide: clientLink.Right,
            PrinterSide: printerLink.Right,
            ToClient: clientLink.Left,
            ToPrinter: printerLink.Left,
            factory, observer);
    }

    private static RelayOutcome Run(Harness harness, Action drive)
    {
        Task<RelayOutcome> relaying = harness.Relay.RelayOneAsync(harness.Client, CancellationToken.None);
        drive();

        if (!relaying.Wait(Patience))
        {
            throw new AssertionException("The relay did not finish; it is hung.");
        }

        return relaying.Result;
    }

    // ---- The payload --------------------------------------------------------

    [TestCase("Bytes reach the printer exactly as the client sent them")]
    [Requirement("REQ-PXY-003")]
    public static void Payload_reaches_the_printer_unchanged()
    {
        Harness harness = Build();

        byte[] job = Encoding.UTF8.GetBytes("POST /ipp/print HTTP/1.1\r\n\r\n\x02\x00\x00\x0bPRINTJOBDATA");

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.Write(job, 0, job.Length);
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "the relay should complete cleanly");
        Assert.Equal(job.Length, harness.ToPrinter.Written.Count, "every byte must arrive");
        Assert.True(job.SequenceEqual(harness.ToPrinter.Written),
            "the payload must be byte-identical; the proxy alters nothing");
    }

    [TestCase("The printer's reply reaches the client unchanged")]
    [Requirement("REQ-PXY-002")]
    public static void Reply_reaches_the_client_unchanged()
    {
        Harness harness = Build();

        byte[] reply = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n\x02\x00\x00\x00");

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.PrinterSide.Write(reply, 0, reply.Length);
            harness.PrinterSide.CloseWrite();
            harness.ClientSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "the relay should complete cleanly");
        Assert.True(reply.SequenceEqual(harness.ToClient.Written),
            "the printer's response must reach the client unaltered");
    }

    [TestCase("A large job is streamed, not held whole")]
    [Requirement("REQ-PXY-005")]
    public static void Large_job_is_streamed_in_bounded_pieces()
    {
        var options = new RelayOptions { BufferSize = 4096 };
        Harness harness = Build(options);

        // Much larger than the buffer, as a photo print would be.
        byte[] job = new byte[512 * 1024];
        Random.Shared.NextBytes(job);

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.Write(job, 0, job.Length);
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "a large job must relay successfully");
        Assert.Equal(job.Length, harness.ToPrinter.Written.Count, "all of it must arrive");
        Assert.True(job.SequenceEqual(harness.ToPrinter.Written), "and arrive unchanged");

        // The relay reads the job from its own end of the client link, so that
        // is where the read sizes it requested are recorded.
        Assert.True(harness.ToClient.LargestRead <= options.BufferSize,
            $"no read may exceed the configured buffer, but one was {harness.ToClient.LargestRead}");
        Assert.True(harness.ToClient.ReadCount > 1,
            "a job far larger than the buffer must take several reads, or it was not streamed");
    }

    [TestCase("Byte counts are reported, and only counts")]
    [Requirement("REQ-PXY-009")]
    public static void Counts_are_reported()
    {
        Harness harness = Build();
        byte[] job = Encoding.UTF8.GetBytes("hello printer");
        byte[] reply = Encoding.UTF8.GetBytes("ok");

        Run(harness, () =>
        {
            harness.ClientSide.Write(job, 0, job.Length);
            harness.PrinterSide.Write(reply, 0, reply.Length);
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.Equal(job.Length, (int)harness.Observer.LastToPrinter, "bytes sent should be counted");
        Assert.Equal(reply.Length, (int)harness.Observer.LastToClient, "bytes received should be counted");
    }

    [TestCase("A job is reported complete when only the client closes")]
    [Requirement("REQ-PXY-009")]
    public static void Completion_is_reported_when_one_side_closes()
    {
        // What a real printer does: it answers, then holds its side of the
        // connection open. Only the client hangs up.
        //
        // The relay stops the second direction when the first ends, which
        // surfaces as a cancellation. That was originally allowed to escape, so
        // no connection was ever reported as completed or failed - invisible
        // here until this test existed, because the other tests close both
        // sides at once and usually reach end-of-stream before the cancellation
        // happens. Against the real ET-3760, 128 relayed connections produced
        // no completion log at all.
        Harness harness = Build();

        byte[] job = Encoding.UTF8.GetBytes("POST /ipp/print HTTP/1.1\r\n\r\njob bytes");
        byte[] reply = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n");

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.Write(job, 0, job.Length);
            harness.PrinterSide.Write(reply, 0, reply.Length);

            // Only the client hangs up. The printer stays open, as one does.
            harness.ClientSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded,
            "a client hanging up is the ordinary end of a print job, not a failure");
        Assert.True(harness.Observer.Events.Any(e => e.StartsWith("completed", StringComparison.Ordinal)),
            "the connection must be reported as completed; without this no job is ever logged as finished");
        Assert.False(harness.Observer.Events.Any(e => e.StartsWith("failed", StringComparison.Ordinal)),
            "and it must not be reported as a failure");
    }

    [TestCase("Byte counts survive a direction being stopped")]
    [Requirement("REQ-PXY-009")]
    public static void Counts_survive_cancellation()
    {
        Harness harness = Build();

        byte[] job = Encoding.UTF8.GetBytes("a job of known length");
        byte[] reply = Encoding.UTF8.GetBytes("a reply");

        Run(harness, () =>
        {
            harness.ClientSide.Write(job, 0, job.Length);
            harness.PrinterSide.Write(reply, 0, reply.Length);
            Thread.Sleep(50); // let both directions move their bytes
            harness.ClientSide.CloseWrite();
        });

        // Counts are accumulated as bytes move, not read from a task result,
        // so the direction that was stopped still reports what it carried.
        Assert.Equal(job.Length, (int)harness.Observer.LastToPrinter,
            "bytes sent must be counted even though that direction ended first");
        Assert.Equal(reply.Length, (int)harness.Observer.LastToClient,
            "and bytes received must survive that direction being stopped");
    }

    // ---- Structural claims --------------------------------------------------

    [TestCase("The proxy assembly references no file-writing type")]
    [Requirement("REQ-PXY-004")]
    public static void Assembly_writes_no_files()
    {
        // REQ-PXY-004 is a claim about absence, so it is checked by reading the
        // compiled assembly's type-reference table. This fails if anyone so much
        // as mentions File in this project, whether or not the code path runs.
        string assemblyPath = typeof(IppRelay).Assembly.Location;
        Assert.True(File_Exists(assemblyPath), "the assembly must be on disk to inspect");

        string[] forbidden =
        [
            "File", "FileStream", "FileInfo", "StreamWriter", "Directory", "DirectoryInfo",
            "IsolatedStorageFile", "MemoryMappedFile",
        ];

        var found = new List<string>();

        using (var stream = OpenRead(assemblyPath))
        using (var peReader = new PEReader(stream))
        {
            MetadataReader metadata = peReader.GetMetadataReader();

            foreach (TypeReferenceHandle handle in metadata.TypeReferences)
            {
                TypeReference reference = metadata.GetTypeReference(handle);
                string ns = metadata.GetString(reference.Namespace);
                string name = metadata.GetString(reference.Name);

                if (ns.StartsWith("System.IO", StringComparison.Ordinal)
                    && forbidden.Contains(name, StringComparer.Ordinal))
                {
                    found.Add($"{ns}.{name}");
                }
            }
        }

        Assert.Equal(0, found.Count,
            "SecretPrinter.Proxy must contain no file-writing type reference, but found: "
            + string.Join(", ", found));
    }

    [TestCase("The observer interface cannot carry job content")]
    [Requirement("REQ-OBS-004")]
    public static void Observer_cannot_receive_payload()
    {
        // A logger can only write what it is given. If no observer method can
        // accept bytes, no implementation can log a document by accident.
        var offending = new List<string>();

        foreach (System.Reflection.MethodInfo method in typeof(IRelayObserver).GetMethods())
        {
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                Type type = parameter.ParameterType;

                if (type == typeof(byte[]) || type == typeof(Stream)
                    || type == typeof(ReadOnlyMemory<byte>) || type == typeof(Memory<byte>)
                    || type == typeof(Span<byte>) || type == typeof(ReadOnlySpan<byte>))
                {
                    offending.Add($"{method.Name}({type.Name} {parameter.Name})");
                }
            }
        }

        Assert.Equal(0, offending.Count,
            "no observer method may accept payload data, but found: " + string.Join(", ", offending));
    }

    // ---- Lifecycle and failure ----------------------------------------------

    [TestCase("Closing one side ends the relay and closes the other")]
    [Requirement("REQ-PXY-006")]
    public static void Closing_one_side_closes_both()
    {
        Harness harness = Build();

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "an empty but clean conversation still ends cleanly");
        Assert.True(harness.Client.Disposed, "the client connection must be closed when the relay ends");
    }

    [TestCase("A printer that cannot be reached fails promptly")]
    [Requirement("REQ-PXY-007")]
    public static void Unreachable_printer_fails_promptly()
    {
        Harness harness = Build();
        harness.Factory.FailWith = new System.Net.Sockets.SocketException(10061); // refused

        Task<RelayOutcome> relaying = harness.Relay.RelayOneAsync(harness.Client, CancellationToken.None);

        Assert.True(relaying.Wait(Patience), "a refused connection must not leave the client hanging");

        RelayOutcome outcome = relaying.Result;
        Assert.False(outcome.Succeeded, "the job cannot succeed with no printer");
        Assert.NotNull(outcome.Failure, "the failure must carry a reason");
        Assert.True(harness.Client.Disposed, "the client connection must be closed");
        Assert.True(harness.Observer.Events.Any(e => e.StartsWith("failed", StringComparison.Ordinal)),
            "the failure must be reported, not swallowed");
    }

    [TestCase("A printer that cannot be located fails without connecting")]
    [Requirement("REQ-PXY-007")]
    public static void Unresolvable_printer_fails_without_connecting()
    {
        var factory = new FakeConnectionFactory();
        var observer = new RecordingObserver();
        StreamPair link = StreamPair.Create();
        var client = new FakeConnection(link.Left, ClientEndpoint);

        var relay = new IppRelay(
            new NeverAcceptsListener(),
            factory,
            _ => throw new InvalidOperationException("printer did not answer"),
            new RelayOptions(),
            observer);

        RelayOutcome outcome = relay.RelayOneAsync(client, CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(outcome.Succeeded, "no address means no job");
        Assert.Equal(0, factory.Attempts.Count, "nothing should be dialled when the address is unknown");
        Assert.True(outcome.Failure!.Contains("locate the printer", StringComparison.Ordinal),
            "the reason should say the printer could not be located");
    }

    // ---- Access control -----------------------------------------------------

    [TestCase("A connection from outside the permitted networks is refused")]
    [Requirement("REQ-SEC-012")]
    public static void Foreign_client_is_refused()
    {
        var options = new RelayOptions
        {
            AllowedClientNetworks = [IPNetwork.Parse("192.168.1.0/24")],
        };

        Harness harness = Build(options, new IPEndPoint(IPAddress.Parse("203.0.113.5"), 40000));

        RelayOutcome outcome = harness.Relay
            .RelayOneAsync(harness.Client, CancellationToken.None).GetAwaiter().GetResult();

        Assert.False(outcome.Succeeded, "a client from an unexpected network must not be relayed");
        Assert.Equal(0, harness.Factory.Attempts.Count,
            "the printer must not be contacted on behalf of a refused client");
        Assert.True(harness.Observer.Events.Any(e => e.StartsWith("refused", StringComparison.Ordinal)),
            "the refusal must be reported");
    }

    [TestCase("A connection from a permitted network is relayed")]
    [Requirement("REQ-SEC-012")]
    public static void Permitted_client_is_relayed()
    {
        var options = new RelayOptions
        {
            AllowedClientNetworks = [IPNetwork.Parse("192.168.1.0/24")],
        };

        Harness harness = Build(options, new IPEndPoint(IPAddress.Parse("192.168.1.41"), 40000));

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "a client on the configured network must be served");
    }

    // ---- Not a general-purpose proxy ----------------------------------------

    [TestCase("Client bytes cannot influence where the relay connects")]
    [Requirement("REQ-SEC-011")]
    public static void Client_cannot_choose_the_destination()
    {
        Harness harness = Build();

        // The shapes a client would use against an open proxy. To this relay
        // they are simply bytes to forward: the destination was decided before
        // any of them arrived.
        byte[] attempt = Encoding.UTF8.GetBytes(
            "CONNECT 203.0.113.9:443 HTTP/1.1\r\nHost: 203.0.113.9\r\n\r\n"
            + "GET http://example.invalid/ HTTP/1.1\r\nHost: example.invalid\r\n\r\n");

        RelayOutcome outcome = Run(harness, () =>
        {
            harness.ClientSide.Write(attempt, 0, attempt.Length);
            harness.ClientSide.CloseWrite();
            harness.PrinterSide.CloseWrite();
        });

        Assert.True(outcome.Succeeded, "the bytes are relayed like any others");
        Assert.Equal(1, harness.Factory.Attempts.Count, "exactly one connection should be opened");
        Assert.Equal(PrinterEndpoint, harness.Factory.Attempts[0],
            "the only destination ever dialled is the resolved printer");
        Assert.True(attempt.SequenceEqual(harness.ToPrinter.Written),
            "and the bytes went to the printer unchanged, as opaque payload");
    }

    [TestCase("No public entry point accepts a destination")]
    [Requirement("REQ-SEC-011")]
    public static void No_api_accepts_a_destination()
    {
        // REQ-SEC-011 is a claim about absence, so it is checked structurally.
        // The destination reaches the relay only through the delegate supplied
        // at construction; if someone later adds a parameter that lets a caller
        // name a destination, this fails.
        var offending = new List<string>();

        foreach (System.Reflection.MethodInfo method in typeof(IppRelay).GetMethods(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly))
        {
            foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(IPEndPoint)
                    || parameter.ParameterType == typeof(EndPoint)
                    || parameter.ParameterType == typeof(Uri))
                {
                    offending.Add($"{method.Name}({parameter.ParameterType.Name} {parameter.Name})");
                }
            }
        }

        Assert.Equal(0, offending.Count,
            "no caller may name where a job goes, but found: " + string.Join(", ", offending));
    }

    // ---- Configuration ------------------------------------------------------

    [TestCase("An unreasonable buffer size is rejected")]
    [Requirement("REQ-PXY-005")]
    public static void Absurd_buffer_size_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new IppRelay(
                new NeverAcceptsListener(), new FakeConnectionFactory(),
                _ => Task.FromResult(PrinterEndpoint),
                new RelayOptions { BufferSize = 64 * 1024 * 1024 },
                new RecordingObserver()),
            "a 64 MiB buffer would defeat the point of streaming");
    }

    // ---- The accept loop ----------------------------------------------------

    [TestCase("Accepted connections are relayed")]
    [Requirement("REQ-PXY-001")]
    public static void Run_relays_an_accepted_connection()
    {
        StreamPair clientLink = StreamPair.Create();
        StreamPair printerLink = StreamPair.Create();

        var listener = new QueueListener();
        listener.Offer(new FakeConnection(clientLink.Left, ClientEndpoint));

        var factory = new FakeConnectionFactory
        {
            OnConnect = _ => new FakeConnection(printerLink.Left, PrinterEndpoint),
        };

        var observer = new RecordingObserver();
        var relay = new IppRelay(
            listener, factory, _ => Task.FromResult(PrinterEndpoint), new RelayOptions(), observer);

        using var stopping = new CancellationTokenSource();
        Task running = relay.RunAsync(stopping.Token);

        byte[] job = Encoding.UTF8.GetBytes("a job from the accept loop");
        clientLink.Right.Write(job, 0, job.Length);
        clientLink.Right.CloseWrite();
        printerLink.Right.CloseWrite();

        Assert.True(SpinUntil(() => printerLink.Left.Written.Count == job.Length),
            "a connection taken from the listener must be relayed like any other");

        stopping.Cancel();
        listener.Release();
        running.Wait(Patience);

        Assert.True(job.SequenceEqual(printerLink.Left.Written), "and relayed unchanged");
    }

    [TestCase("A stalled job does not block another")]
    [Requirement("REQ-PXY-008")]
    public static void Concurrent_jobs_do_not_block_each_other()
    {
        // The first client connects and then says nothing, as a phone that
        // sleeps mid-job would. The second must still complete.
        StreamPair stalledClient = StreamPair.Create();
        StreamPair stalledPrinter = StreamPair.Create();
        StreamPair activeClient = StreamPair.Create();
        StreamPair activePrinter = StreamPair.Create();

        var listener = new QueueListener();

        int connectCount = 0;
        var factory = new FakeConnectionFactory
        {
            OnConnect = _ => new FakeConnection(
                Interlocked.Increment(ref connectCount) == 1 ? stalledPrinter.Left : activePrinter.Left,
                PrinterEndpoint),
        };

        var relay = new IppRelay(
            listener, factory, _ => Task.FromResult(PrinterEndpoint),
            new RelayOptions(), new RecordingObserver());

        using var stopping = new CancellationTokenSource();
        Task running = relay.RunAsync(stopping.Token);

        // Offer the stalled client and WAIT for its relay to have dialled the
        // printer before offering the second.
        //
        // Without this the two relays race to call ConnectAsync, and the fake
        // factory - which hands out streams in call order - can give the second
        // job the first job's printer stream. That is a defect in the test, not
        // in the relay, and it is the kind that passes on one machine and fails
        // on another: it passed in a Linux container and failed on Windows.
        listener.Offer(new FakeConnection(stalledClient.Left, ClientEndpoint));

        Assert.True(SpinUntil(() => Volatile.Read(ref connectCount) >= 1),
            "the first job should reach the printer before the second is offered");

        listener.Offer(new FakeConnection(activeClient.Left, ClientEndpoint));

        Assert.True(SpinUntil(() => Volatile.Read(ref connectCount) >= 2),
            "the second job must be accepted while the first is still stalled, "
            + "which is the whole point: the accept loop must not wait for a job to finish");

        byte[] job = Encoding.UTF8.GetBytes("the second job");
        activeClient.Right.Write(job, 0, job.Length);
        activeClient.Right.CloseWrite();
        activePrinter.Right.CloseWrite();

        Assert.True(SpinUntil(() => activePrinter.Left.Written.Count == job.Length),
            "the second job must complete while the first is still stalled");
        Assert.Equal(0, stalledPrinter.Left.Written.Count, "the first job is still waiting, as intended");

        // Let everything unwind.
        stalledClient.Right.CloseWrite();
        stalledPrinter.Right.CloseWrite();
        stopping.Cancel();
        listener.Release();
        running.Wait(Patience);
    }

    /// <summary>Waits briefly for a condition, so tests need no fixed sleeps.</summary>
    private static bool SpinUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    // Small helpers kept local so this file's own use of System.IO is visible
    // and obviously read-only. The assembly under test is a different assembly.
    private static bool File_Exists(string path) => System.IO.File.Exists(path);

    private static FileStream OpenRead(string path) => System.IO.File.OpenRead(path);
}

/// <summary>A listener that hands out prepared connections, then waits.</summary>
internal sealed class QueueListener : IConnectionListener
{
    private readonly Queue<IDuplexConnection> _pending = new();
    private readonly SemaphoreSlim _available = new(0);

    public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Parse("192.168.1.234"), 631);

    public void Offer(IDuplexConnection connection)
    {
        lock (_pending)
        {
            _pending.Enqueue(connection);
        }

        _available.Release();
    }

    /// <summary>Unblocks a waiting Accept so RunAsync can observe cancellation.</summary>
    public void Release() => _available.Release();

    public async Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        await _available.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_pending)
        {
            if (_pending.Count > 0)
            {
                return _pending.Dequeue();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException();
    }

    public ValueTask DisposeAsync()
    {
        _available.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>A listener that never produces a connection; the tests drive RelayOneAsync directly.</summary>
internal sealed class NeverAcceptsListener : IConnectionListener
{
    public IPEndPoint LocalEndPoint { get; } = new(IPAddress.Parse("192.168.1.234"), 631);

    public Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken) =>
        Task.FromCanceled<IDuplexConnection>(new CancellationToken(canceled: true));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

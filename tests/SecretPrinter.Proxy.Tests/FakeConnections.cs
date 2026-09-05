// -----------------------------------------------------------------------------
// FakeConnections.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   In-memory stand-ins for TCP, so the relay's behaviour can be asserted
//   exactly and deterministically: bytes in equal bytes out, closing one side
//   closes the other, a refused connection fails promptly.
// -----------------------------------------------------------------------------

using System.Net;

namespace SecretPrinter.Proxy.Tests;

/// <summary>
/// A duplex stream pair joined back to back, the way two ends of a socket are.
/// What one side writes, the other reads.
/// </summary>
internal sealed class StreamPair
{
    private StreamPair(RecordingStream left, RecordingStream right)
    {
        Left = left;
        Right = right;
    }

    public RecordingStream Left { get; }

    public RecordingStream Right { get; }

    public static StreamPair Create()
    {
        var leftToRight = new SharedBuffer();
        var rightToLeft = new SharedBuffer();

        return new StreamPair(
            new RecordingStream(readFrom: rightToLeft, writeTo: leftToRight),
            new RecordingStream(readFrom: leftToRight, writeTo: rightToLeft));
    }
}

/// <summary>A byte queue one side writes and the other reads, closable from either end.</summary>
internal sealed class SharedBuffer : IDisposable
{
    private readonly Queue<byte> _bytes = new();
    private readonly SemaphoreSlim _available = new(0);
    private volatile bool _closed;

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_bytes)
        {
            foreach (byte b in data)
            {
                _bytes.Enqueue(b);
            }
        }

        _available.Release();
    }

    public void Close()
    {
        _closed = true;
        _available.Release();
    }

    public void Dispose() => _available.Dispose();

    public async Task<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_bytes)
            {
                if (_bytes.Count > 0)
                {
                    int taken = 0;
                    while (taken < destination.Length && _bytes.Count > 0)
                    {
                        destination.Span[taken++] = _bytes.Dequeue();
                    }

                    return taken;
                }

                if (_closed)
                {
                    return 0;
                }
            }

            await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A stream over a SharedBuffer that records how it was used, so a test can
/// assert that copying was streamed in bounded pieces rather than buffered
/// whole.
/// </summary>
internal sealed class RecordingStream(SharedBuffer readFrom, SharedBuffer writeTo) : Stream
{
    private readonly List<int> _readSizes = [];

    public List<byte> Written { get; } = [];

    /// <summary>Largest single read the relay requested.</summary>
    public int LargestRead => _readSizes.Count == 0 ? 0 : _readSizes.Max();

    /// <summary>How many reads it took, which reveals whether copying was streamed.</summary>
    public int ReadCount => _readSizes.Count;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void CloseWrite() => writeTo.Close();

    public void CloseRead() => readFrom.Close();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _readSizes.Add(buffer.Length);
        return await readFrom.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Written.AddRange(buffer.ToArray());
        writeTo.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Write(byte[] buffer, int offset, int count) =>
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>A connection over an in-memory stream.</summary>
internal sealed class FakeConnection(Stream stream, EndPoint? remote) : IDuplexConnection
{
    public Stream Stream { get; } = stream;

    public EndPoint? RemoteEndPoint { get; } = remote;

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>A factory that hands out a prepared connection, or fails on demand.</summary>
internal sealed class FakeConnectionFactory : IConnectionFactory
{
    public Func<IPEndPoint, IDuplexConnection>? OnConnect { get; set; }

    public Exception? FailWith { get; set; }

    public List<IPEndPoint> Attempts { get; } = [];

    public Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Attempts.Add(destination);

        if (FailWith is { } failure)
        {
            return Task.FromException<IDuplexConnection>(failure);
        }

        return Task.FromResult(OnConnect!(destination));
    }
}

/// <summary>Records what the relay reported, so tests can assert on it.</summary>
internal sealed class RecordingObserver : IRelayObserver
{
    public List<string> Events { get; } = [];

    public long LastToPrinter { get; private set; }

    public long LastToClient { get; private set; }

    public void ConnectionAccepted(EndPoint? client) => Events.Add($"accepted {client}");

    public void ConnectionRefused(EndPoint? client, string reason) => Events.Add($"refused {client}: {reason}");

    public void RelayStarted(EndPoint? client, IPEndPoint printer) => Events.Add($"started {client} -> {printer}");

    public void RelayCompleted(
        EndPoint? client, IPEndPoint printer, long bytesToPrinter, long bytesToClient, TimeSpan duration)
    {
        LastToPrinter = bytesToPrinter;
        LastToClient = bytesToClient;
        Events.Add($"completed {client} -> {printer} up={bytesToPrinter} down={bytesToClient}");
    }

    public void RelayFailed(EndPoint? client, string reason) => Events.Add($"failed {client}: {reason}");
}

// -----------------------------------------------------------------------------
// Connections.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   The narrowest possible view of TCP, so the relay can be tested with
//   in-memory streams.
//
//   TcpListener and TcpClient are concrete. A relay written against them
//   directly could only be tested with real sockets, real ports and real
//   timing - which means slow, flaky tests that eventually get skipped. The
//   print path is the most security-sensitive code in this project and is the
//   last place that should happen.
//
// Note on what is NOT here:
//   Nothing in this file, or anywhere in SecretPrinter.Proxy, opens a file. The
//   project deliberately contains no other responsibility, so a reviewer
//   checking REQ-PXY-004 - that no part of a print job is ever written to disk -
//   reads one small project rather than the whole solution. There is a test
//   that inspects the compiled assembly's type references to enforce it.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace SecretPrinter.Proxy;

/// <summary>A two-way byte stream to some peer.</summary>
public interface IDuplexConnection : IAsyncDisposable
{
    /// <summary>The stream carrying bytes in both directions.</summary>
    Stream Stream { get; }

    /// <summary>Who is at the other end, for logging. Null when unknown.</summary>
    EndPoint? RemoteEndPoint { get; }
}

/// <summary>Accepts incoming connections.</summary>
public interface IConnectionListener : IAsyncDisposable
{
    /// <summary>Where this listener is bound.</summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>Waits for the next incoming connection.</summary>
    Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken);
}

/// <summary>Opens outgoing connections.</summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Connects to a destination, failing rather than hanging when it cannot be
    /// reached.
    /// </summary>
    Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>A real TCP connection.</summary>
public sealed class TcpConnection : IDuplexConnection
{
    private readonly TcpClient _client;

    public TcpConnection(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;

        // Print jobs are streamed, so Nagle buffering only adds latency between
        // the client and the printer without reducing packet count meaningfully.
        _client.NoDelay = true;

        Stream = client.GetStream();
        RemoteEndPoint = client.Client.RemoteEndPoint;
    }

    public Stream Stream { get; }

    public EndPoint? RemoteEndPoint { get; }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Listens for real TCP connections on one address and port.</summary>
public sealed class TcpConnectionListener : IConnectionListener
{
    private readonly TcpListener _listener;

    /// <summary>
    /// Binds to one specific address, never to the wildcard. Binding every
    /// interface would accept print jobs on the printer network too, which
    /// REQ-PXY-001 forbids.
    /// </summary>
    public TcpConnectionListener(IPAddress address, ushort port)
    {
        ArgumentNullException.ThrowIfNull(address);

        _listener = new TcpListener(address, port);
        _listener.Start();
        LocalEndPoint = (IPEndPoint)_listener.LocalEndpoint;
    }

    public IPEndPoint LocalEndPoint { get; }

    public async Task<IDuplexConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        return new TcpConnection(client);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Opens real TCP connections to the printer.</summary>
public sealed class TcpConnectionFactory : IConnectionFactory
{
    public async Task<IDuplexConnection> ConnectAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var client = new TcpClient();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);

            await client.ConnectAsync(destination, deadline.Token).ConfigureAwait(false);
            return new TcpConnection(client);
        }
        catch
        {
            // A half-open client left behind would hold a socket handle for a
            // job that is already failing.
            client.Dispose();
            throw;
        }
    }
}

// -----------------------------------------------------------------------------
// FakeTransport.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   An IMdnsTransport that records what it was asked to send instead of sending
//   it, so the responder's decisions can be asserted with no network involved.
//
//   Shared by every test project that needs a network without a network.
//
//   Sent payloads are parsed back with the real SecretPrinter.Dns reader rather
//   than inspected as bytes. That means a test asserting "the answer contained
//   an SRV record for the proxy" is also exercising the encoder and the decoder
//   against each other - a malformed record would fail to parse and the test
//   would fail, rather than passing because nobody looked.
// -----------------------------------------------------------------------------

using System.Net;
using System.Threading.Channels;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;

namespace SecretPrinter.TestKit;

/// <summary>One datagram the responder handed to the transport.</summary>
/// <param name="Payload">Raw bytes as sent.</param>
/// <param name="Destination">Null for multicast, an endpoint for unicast.</param>
/// <param name="Via">The interface it was sent out of.</param>
public sealed record SentDatagram(byte[] Payload, IPEndPoint? Destination, MdnsInterface Via)
{
    public bool WasMulticast => Destination is null;

    /// <summary>The payload parsed back with the project's own reader.</summary>
    public DnsMessage Parsed => DnsMessage.Parse(Payload, Payload.Length);
}

public sealed class FakeTransport : IMdnsTransport
{
    private readonly Channel<MdnsDatagram> _inbound = Channel.CreateUnbounded<MdnsDatagram>();

    public FakeTransport(params MdnsInterface[] interfaces) => Interfaces = interfaces;

    /// <summary>
    /// Invoked as each datagram is sent, before the send completes. Lets a test
    /// answer a query the way a printer would - which is the only way to
    /// exercise code that sends and then waits for a reply.
    /// </summary>
    public Action<SentDatagram>? OnSend { get; set; }

    public IReadOnlyList<MdnsInterface> Interfaces { get; }

    public List<SentDatagram> Sent { get; } = [];

    /// <summary>When set, the next ReceiveAsync throws this instead of returning.</summary>
    public Exception? ThrowOnReceive { get; set; }

    public void Enqueue(MdnsDatagram datagram) => _inbound.Writer.TryWrite(datagram);

    /// <summary>
    /// Returns a queued datagram, or waits for one. Waiting rather than
    /// returning immediately matters: a real socket blocks when the network is
    /// quiet, and code that only works against an always-ready fake would hide
    /// a spin.
    /// </summary>
    public async Task<MdnsDatagram> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (ThrowOnReceive is { } toThrow)
        {
            ThrowOnReceive = null;
            throw toThrow;
        }

        return await _inbound.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SendMulticastAsync(
        ReadOnlyMemory<byte> payload, MdnsInterface via, CancellationToken cancellationToken)
    {
        RequireKnown(via);
        var sent = new SentDatagram(payload.ToArray(), null, via);
        Sent.Add(sent);
        OnSend?.Invoke(sent);
        return Task.CompletedTask;
    }

    public Task SendUnicastAsync(
        ReadOnlyMemory<byte> payload, IPEndPoint destination, MdnsInterface via, CancellationToken cancellationToken)
    {
        RequireKnown(via);
        var sent = new SentDatagram(payload.ToArray(), destination, via);
        Sent.Add(sent);
        OnSend?.Invoke(sent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mirrors the real socket's refusal to send out an interface it does not
    /// hold. A fake that accepted anything would let a test pass while the real
    /// transport threw.
    /// </summary>
    private void RequireKnown(MdnsInterface via)
    {
        if (!Interfaces.Any(i => i.Index == via.Index))
        {
            throw new ArgumentException($"Interface {via} is not held by this transport.", nameof(via));
        }
    }
}

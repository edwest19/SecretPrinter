// -----------------------------------------------------------------------------
// IMdnsTransport.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   The narrowest possible view of the network, so that everything above it can
//   be tested without one.
//
// Why this exists:
//   MdnsSocket is a real socket. Code written against it directly can only be
//   tested on a machine with the right adapters, and a test that cannot run in
//   every environment eventually gets skipped. That is not hypothetical here:
//   REQ-CFG-006 sat at "TEST DID NOT RUN" for exactly that reason.
//
//   With this interface, the responder's decisions - which query gets an
//   answer, which records go out, which interface they leave by, whether the
//   reply is unicast or multicast - are all testable with a fake that records
//   what it was asked to send. No hardware, no timing, no skips.
//
//   The interface is deliberately tiny. Everything a fake must reimplement is a
//   place a fake can diverge from reality, so there is as little of it as the
//   responder can manage with.
// -----------------------------------------------------------------------------

using System.Net;

namespace SecretPrinter.Mdns;

/// <summary>Sends and receives mDNS datagrams on known interfaces.</summary>
public interface IMdnsTransport
{
    /// <summary>The interfaces this transport is bound to, in configuration order.</summary>
    IReadOnlyList<MdnsInterface> Interfaces { get; }

    /// <summary>
    /// Waits for one datagram. A datagram whose
    /// <see cref="MdnsDatagram.ArrivedOn"/> is null came from an interface the
    /// service is not configured for and must not be answered.
    /// </summary>
    Task<MdnsDatagram> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>Sends to the mDNS multicast group, out of one specific interface.</summary>
    /// <exception cref="ArgumentException">The interface is not one this transport holds.</exception>
    Task SendMulticastAsync(ReadOnlyMemory<byte> payload, MdnsInterface via, CancellationToken cancellationToken);

    /// <summary>Sends directly to one endpoint, out of one specific interface.</summary>
    /// <exception cref="ArgumentException">The interface is not one this transport holds.</exception>
    Task SendUnicastAsync(
        ReadOnlyMemory<byte> payload,
        IPEndPoint destination,
        MdnsInterface via,
        CancellationToken cancellationToken);
}

// -----------------------------------------------------------------------------
// ClientNetwork.cs
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-10-13, for the SecretPrinter project. Reviewed by a human
// before merge.
//
// Purpose:
//   The one place that says what the IPv4 network of a client interface is.
//   Two parts of the service ask:
//
//   - the relay, which accepts a print connection over IPv4 only from an
//     address on that network (README REQ-SEC-012), and
//   - the mDNS responder, which sends an answer by unicast over IPv4 only to an
//     address on that network (README REQ-SEC-017).
//
//   Until 2026-10-13 the rule lived in ServiceHost.cs, as PrefixLengthFor, and
//   only the relay used it. It moved here, unchanged, when the responder came
//   to need it too, so that the two cannot come to disagree. See
//   docs/findings/2026-10-13-a-unicast-answer-went-to-any-address-a-question-gave.md.
//
// What the rule is, and what it is not:
//   The network is worked out from the interface's address. It is not read
//   from the adapter's subnet mask. An address in 10.0.0.0/8 is taken to be on
//   a /8 network, and any other address on a /24. Home networks are /24 in
//   practice. This is a guess, stated as a known problem in the README
//   (REQ-SEC-012); a network wider than /24 has addresses this rule leaves out,
//   which is the safe side to be wrong on, and a network narrower than /24 has
//   none of its own left out but takes in addresses beyond it.
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace SecretPrinter.Mdns;

/// <summary>The IPv4 network of a client interface, as the service works it out.</summary>
public static class ClientNetwork
{
    /// <summary>
    /// The network an IPv4 client interface address is taken to be on: /8 for
    /// an address in 10.0.0.0/8, /24 for any other.
    /// </summary>
    /// <remarks>
    /// Moved from ServiceHost.PrefixLengthFor on 2026-10-13. The rule is
    /// unchanged: the first octet decides, and nothing else is read. New here:
    /// an address that is not IPv4 is refused. The old method would have read
    /// the first byte of an IPv6 address; nothing ever passed it one.
    ///
    /// The network's base address is the interface address with its host part
    /// cleared. <see cref="IPNetwork"/> clears it, on the .NET 10 runtime
    /// (read in Claude's workspace on 2026-10-13, runtime 10.0.12: an address
    /// in 192.168.1.0/24 given with prefix length 24 printed as 192.168.1.0/24).
    /// </remarks>
    /// <exception cref="ArgumentException">The address is not an IPv4 address.</exception>
    public static IPNetwork Of(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                $"{address} is not an IPv4 address. A client network is worked out only for IPv4; over IPv6 "
                + "the service deals with link-local addresses on the client interface's own scope.",
                nameof(address));
        }

        byte[] octets = address.GetAddressBytes();
        int prefixLength = octets[0] == 10 ? 8 : 24;
        return new IPNetwork(address, prefixLength);
    }
}

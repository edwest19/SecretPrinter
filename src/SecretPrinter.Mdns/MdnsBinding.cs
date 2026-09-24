// -----------------------------------------------------------------------------
// MdnsBinding.cs
//
// Written by Claude (Anthropic model, Claude Opus 5) at the direction of Edwin
// West, for the SecretPrinter project. Reviewed by a human before merge.
//
// A comment on JoinIPv6 that said iOS queries over IPv6 only corrected by
// Claude (Anthropic model, Claude Opus 5.5) at the direction of Edwin West,
// 2026-09-24. Comments only; no behaviour changed. The original words are kept
// in docs/findings/2026-09-24-the-ipv6-only-claim-was-in-more-places.md.
// Reviewed by a human before merge.
//
// Purpose:
//   One line of instruction to MdnsSocket: use this interface, and join the
//   IPv6 mDNS group on it or do not.
//
// Why a type rather than a second list:
//   MdnsSocket used to take a bare list of addresses, which was enough while
//   every interface was treated identically. REQ-ADV-018 ends that: the IPv6
//   group is joined on client interfaces only, because the printer is IPv4-only
//   and joining ff02::fb on the printer side would put MLD reports onto that
//   network for no benefit.
//
//   Two parallel lists - all addresses, plus the subset to join IPv6 on - would
//   express the same thing, but their relationship would be an invariant checked
//   at run time rather than a fact about the shape of the data. Pairing the flag
//   with the address makes the mistake unwritable.
//
// Why the flag says nothing about clients or printers:
//   MdnsSocket's security claim is that it knows nothing about printers, and
//   that claim is checkable because it is true of every line in the file. A
//   parameter named for a role would end that. The caller says where to join;
//   ServiceHost keeps the reason.
//
// This file opens no sockets and sends nothing.
// -----------------------------------------------------------------------------

using System.Net;

namespace SecretPrinter.Mdns;

/// <summary>One interface to bind, and whether to join the IPv6 mDNS group on it.</summary>
/// <param name="Address">
/// The IPv4 address identifying the interface. Configuration names interfaces by
/// IPv4 address and nothing else, including the interfaces that will carry IPv6:
/// the IPv6 identity of an adapter is derived from this, never configured
/// separately. See <see cref="MdnsInterfaceResolver.ResolveIPv6"/>.
/// </param>
/// <param name="JoinIPv6">
/// True to also join <c>ff02::fb</c> on this interface, in addition to
/// <c>224.0.0.251</c>. When true, the adapter must have IPv6 configured or
/// opening the socket fails. An iPhone was measured sending the same queries
/// over IPv4 and IPv6, and on the network this was built on only the IPv6
/// copies reached the service's machine
/// (docs/findings/2026-09-24-ipv4-mdns-from-behind-the-access-point.md). There,
/// starting without IPv6 would produce a service that runs and answers none of
/// the iPhone's queries.
/// </param>
public readonly record struct MdnsBinding(IPAddress Address, bool JoinIPv6)
{
    public override string ToString() =>
        $"{Address} ({(JoinIPv6 ? "IPv4 and IPv6" : "IPv4 only")})";
}

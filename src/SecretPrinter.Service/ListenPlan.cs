// -----------------------------------------------------------------------------
// ListenPlan.cs  (SecretPrinter.Service)
//
// Written by Claude (Anthropic model, Claude Opus 5.5) at the direction of
// Edwin West, 2026-09-30, for the SecretPrinter project, for REQ-ADV-021.
// Reviewed by a human before merge.
//
// Purpose:
//   Decides what the relay listens on for one client interface, by reading it
//   out of the advertisement published there: one listener for the A record's
//   address, and one for each AAAA record's link-local address. Nothing else
//   decides it. The relay therefore listens on exactly the addresses the
//   service publishes, and the two cannot drift apart, which is what
//   REQ-ADV-021 exists to guarantee: an address published and not listened on
//   is a printer that is discovered and cannot be reached.
//
//   Each listener permits only clients that can arrive on it: the IPv4
//   listener the interface's IPv4 network, and each link-local listener
//   fe80::/10 with the interface's IPv6 index as the scope (REQ-SEC-012).
//
//   It also opens the listeners for an interface all together or not at all.
//   If one cannot be opened, for instance because a published link-local
//   address is gone, those already open are closed and the failure is raised,
//   so nothing listens while an AAAA record points at nothing. The service
//   then says goodbye, logs why and stops, as Edwin decided on 2026-09-30:
//   addresses are read once, at startup, and a restart picks up new ones.
//   See docs/findings/2026-09-30-which-ipv6-addresses-to-publish.md.
// -----------------------------------------------------------------------------

using System.Net;
using SecretPrinter.Dns;
using SecretPrinter.Proxy;
using SecretPrinter.Responder;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>One listener the relay opens on a client interface, and whom it permits.</summary>
/// <param name="Address">
/// The address to bind: the interface's IPv4 address, or one of its link-local
/// addresses with the scope that ties it to the interface.
/// </param>
/// <param name="PermittedNetworks">The client networks this listener's relay permits.</param>
/// <param name="PermittedLinkLocalScopes">
/// The scopes a link-local client may arrive with on this listener: the
/// interface's IPv6 index for a link-local listener, none for the IPv4 one.
/// </param>
public sealed record ListenEntry(
    IPAddress Address,
    IReadOnlyList<IPNetwork> PermittedNetworks,
    IReadOnlyList<long> PermittedLinkLocalScopes);

/// <summary>What the relay listens on for one client interface, read from its advertisement.</summary>
public static class ListenPlan
{
    private static readonly IPNetwork LinkLocal = IPNetwork.Parse("fe80::/10");

    /// <summary>
    /// One listener per address record in the interface's advertisement, in
    /// the advertisement's order.
    /// </summary>
    /// <param name="advertised">The client interface and what is published on it.</param>
    /// <param name="ipv4Network">The client interface's IPv4 network, which must hold its address.</param>
    /// <exception cref="ArgumentException">The IPv4 network does not hold the interface's address.</exception>
    /// <exception cref="InvalidOperationException">
    /// The advertisement carries an address that is not this interface's: an A
    /// record for another address, or an AAAA record that is not link-local on
    /// this interface's IPv6 scope, or one with no IPv6 entry to check it
    /// against. Refused rather than skipped, because skipping would leave a
    /// published address with no listener behind it.
    /// </exception>
    [Requirement("REQ-ADV-021",
        "The relay's listeners are read out of the published address records, one per record, so the service listens on exactly the addresses it publishes. An address record that is not the interface's own is refused rather than skipped.")]
    [Requirement("REQ-SEC-012",
        "Each listener permits only the clients that can arrive on it: the IPv4 listener the interface's IPv4 network, and each link-local listener fe80::/10 on the interface's IPv6 scope.")]
    public static IReadOnlyList<ListenEntry> From(AdvertisedInterface advertised, IPNetwork ipv4Network)
    {
        ArgumentNullException.ThrowIfNull(advertised);

        if (!ipv4Network.Contains(advertised.Interface.Address))
        {
            throw new ArgumentException(
                $"{ipv4Network} does not hold {advertised.Interface.Address}, the address of {advertised.Interface.Name}.",
                nameof(ipv4Network));
        }

        var entries = new List<ListenEntry>();

        foreach (OutgoingRecord record in advertised.Advertisement.Records)
        {
            if (record.Payload is not AddressPayload { Address: var address })
            {
                continue;
            }

            if (record.Type == DnsRecordType.A)
            {
                if (!address.Equals(advertised.Interface.Address))
                {
                    throw new InvalidOperationException(
                        $"The advertisement for {advertised.Interface} publishes {address} in its A record, which is "
                        + "not that interface's address. It would be discovered and not listened on.");
                }

                entries.Add(new ListenEntry(address, [ipv4Network], []));
            }
            else if (record.Type == DnsRecordType.Aaaa)
            {
                if (advertised.IPv6Interface is not { } companion)
                {
                    throw new InvalidOperationException(
                        $"The advertisement for {advertised.Interface} publishes {address} in an AAAA record, but "
                        + "the interface has no IPv6 entry, so nothing says which link the address is on.");
                }

                if (!address.IsIPv6LinkLocal || address.ScopeId != companion.Index)
                {
                    throw new InvalidOperationException(
                        $"The advertisement for {advertised.Interface} publishes {address} in an AAAA record, which "
                        + $"is not a link-local address on scope {companion.Index}, that interface's IPv6 index.");
                }

                entries.Add(new ListenEntry(address, [LinkLocal], [companion.Index]));
            }
        }

        return entries;
    }

    /// <summary>
    /// Opens a listener for every entry, or none: if one cannot be opened,
    /// those already open are closed and the failure is raised.
    /// </summary>
    /// <param name="entries">What to listen on, from <see cref="From"/>.</param>
    /// <param name="open">Opens one listener; the service binds a TCP listener.</param>
    /// <returns>The listeners, in the order of the entries.</returns>
    [Requirement("REQ-ADV-021",
        "Opens every listener in the plan or none, so a published address is never left without a listener while the others serve.")]
    public static async Task<IReadOnlyList<IConnectionListener>> OpenAllAsync(
        IReadOnlyList<ListenEntry> entries, Func<ListenEntry, IConnectionListener> open)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(open);

        var opened = new List<IConnectionListener>();
        try
        {
            foreach (ListenEntry entry in entries)
            {
                opened.Add(open(entry));
            }

            return opened;
        }
        catch
        {
            // Closed in reverse, then the original failure goes on unchanged:
            // it is what the log needs to say.
            for (int i = opened.Count - 1; i >= 0; i--)
            {
                await opened[i].DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }
}

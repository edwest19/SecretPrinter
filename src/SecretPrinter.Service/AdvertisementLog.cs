// -----------------------------------------------------------------------------
// AdvertisementLog.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// Purpose:
//   Writes the complete advertisement to the log: every record published, every
//   TXT entry published, and every entry dropped from the printer's own
//   advertisement together with the reason it was dropped.
//
// Why this is a separate, testable unit:
//   It is what makes the honesty rules auditable in the field rather than only
//   in this repository. A person reading the log can see that the printer's
//   UUID, its Scan flag and its adminurl were dropped, and why, without taking
//   the documentation's word for it and without running a packet capture.
//
//   Buried inside the host it could only be verified by starting a service; on
//   its own it is verified by an ordinary test, so the claim that the log shows
//   everything is itself checked.
// -----------------------------------------------------------------------------

using SecretPrinter.Advertising;
using SecretPrinter.Dns;
using SecretPrinter.Mdns;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Reports an advertisement in full.</summary>
public static class AdvertisementLog
{
    /// <summary>Writes everything published on one interface, and everything withheld.</summary>
    [Requirement("REQ-OBS-002",
        "Logs every record and every TXT entry published, per interface, along with where the capabilities were observed.")]
    [Requirement("REQ-OBS-003",
        "Logs every entry dropped from the printer's advertisement with its reason, so the log alone shows both what the client network was told and what it was deliberately not told.")]
    public static void Write(IServiceLog log, MdnsInterface client, Advertisement advertisement)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(advertisement);

        log.Info($"Advertisement for {client.Name} ({client.Address}):");
        log.Info($"  capabilities observed: {advertisement.Source}");

        foreach (OutgoingRecord record in advertisement.Records)
        {
            log.Info($"  record  {record.Type,-4} {record.Name} ttl={record.Ttl}");
        }

        foreach (string entry in advertisement.TxtStrings)
        {
            log.Info($"  publish {entry}");
        }

        foreach (DroppedTxtEntry dropped in advertisement.Dropped)
        {
            log.Info($"  DROP    {dropped.Entry}  <- {dropped.Reason}");
        }
    }
}

// -----------------------------------------------------------------------------
// AdvertisementLog.cs
//
// Written by Claude (Anthropic model, Claude Opus 4.5) at the direction of
// Edwin West, for the SecretPrinter project. Reviewed by a human before merge.
//
// The NSEC records listed as well, by Claude (Anthropic model, Claude Opus 5.5)
// at the direction of Edwin West, 2026-10-01, when the responder began sending
// them (README REQ-ADV-022). They are not in the advertisement; the responder
// builds them from it. Without these lines the service would have sent records
// its log never mentioned, and REQ-OBS-003 would no longer have been true.
// Reviewed by a human before merge.
//
// Purpose:
//   Writes the complete advertisement to the log: every record published, the
//   NSEC records the responder builds from them, every TXT entry published, and
//   every entry dropped from the printer's own advertisement together with the
//   reason it was dropped.
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
using SecretPrinter.Responder;
using SecretPrinter.Spec;

namespace SecretPrinter.Service;

/// <summary>Reports an advertisement in full.</summary>
public static class AdvertisementLog
{
    /// <summary>Writes everything published on one interface, and everything withheld.</summary>
    /// <remarks>
    /// The NSEC records are read from <see cref="MdnsResponder.NsecRecordsFor"/>,
    /// the method the responder's own constructor calls, so what is logged here
    /// is what the responder holds. They are listed apart from the other
    /// records because they are sent on other occasions: never announced, and
    /// only as README REQ-ADV-022 describes.
    /// </remarks>
    [Requirement("REQ-OBS-002",
        "Logs every record and every TXT entry published, per interface, and the NSEC records the responder builds from them, along with where the capabilities were observed.")]
    [Requirement("REQ-OBS-003",
        "Logs every entry dropped from the printer's advertisement with its reason, and every NSEC record with the types it lists, so the log alone shows both what the client network was told and what it was deliberately not told.")]
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

        IReadOnlyList<OutgoingRecord> nsecs = MdnsResponder.NsecRecordsFor(advertisement);
        if (nsecs.Count > 0)
        {
            log.Info("  NSEC records, not announced. Each says its name has no record of a type it does not list. "
                     + "Sent in answer to a question for such a type, beside an address record when the host has "
                     + "no address of the other family, and in the goodbye (REQ-ADV-022):");

            foreach (OutgoingRecord nsec in nsecs)
            {
                // NsecRecordsFor returns NSEC records only. The cast says so:
                // anything else would stop the service at startup, not be
                // logged as something it is not.
                var listed = (NsecPayload)nsec.Payload;
                log.Info($"  nsec    {nsec.Name} ttl={nsec.Ttl} types={string.Join(",", listed.Types)}");
            }
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

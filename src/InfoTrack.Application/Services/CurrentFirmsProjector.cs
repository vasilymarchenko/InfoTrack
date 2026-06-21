using InfoTrack.Application.Configuration;
using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using Microsoft.Extensions.Options;

namespace InfoTrack.Application.Services;

/// <summary>
/// Computes the current-firms projection on read from stored observations.
/// This is a pure derived view — never a separate source of truth. It can be rebuilt from
/// history at any time and produce the identical result.
/// </summary>
public sealed class CurrentFirmsProjector(
    ISightingRepository sightings,
    ChangeConfirmer confirmer,
    IOptions<ChangeDetectionOptions> opts)
{
    private int K => opts.Value.ConfirmationWindow;

    public async Task<IReadOnlyList<CurrentFirm>> BuildAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var locations = await sightings.GetLocationsWithSuccessfulRunsAsync(ct);

        // firmId -> accumulator
        var byFirm = new Dictionary<Guid, FirmAccumulator>();

        foreach (var loc in locations)
        {
            // Get K recent successful runs to determine Active vs ProvisionallyAbsent vs ConfirmedGone.
            var recentSets = await sightings.GetRecentLocationSightingsAsync(loc, now, K, ct);
            var allFirms   = await sightings.GetLocationFirmLastSeenAsync(loc, ct);

            // Firms in the most recent successful run of this location are Active.
            var activeFirmKeys = recentSets.Count > 0
                ? recentSets[0].FirmsByKey.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var firmInfo in allFirms)
            {
                if (!byFirm.TryGetValue(firmInfo.FirmId, out var acc))
                {
                    acc = new FirmAccumulator(firmInfo.FirmId, firmInfo.FirstSeenAt);
                    byFirm[firmInfo.FirmId] = acc;
                }

                // Refresh "latest" to the most recently observed version.
                if (firmInfo.LastSeenAt >= acc.LastSeenAt)
                {
                    acc.Latest = firmInfo.Latest;
                    acc.LastSeenAt = firmInfo.LastSeenAt;
                }

                if (acc.FirstSeenAt > firmInfo.FirstSeenAt)
                    acc.FirstSeenAt = firmInfo.FirstSeenAt;

                FirmStatus status;
                if (activeFirmKeys.Contains(firmInfo.IdentityKey))
                {
                    status = FirmStatus.Active;
                }
                else
                {
                    // Determine if confirmed gone by checking absence across the K-run window.
                    var absenceConfidence = confirmer.ConfidenceForAbsent(
                        firmInfo.IdentityKey, recentSets, K);
                    status = absenceConfidence == ChangeConfidence.Confirmed
                        ? FirmStatus.ConfirmedGone
                        : FirmStatus.ProvisionallyAbsent;
                }

                // Keep the most "alive" state per location.
                var existing = acc.Locations.FirstOrDefault(l =>
                    string.Equals(l.Location, loc, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    acc.Locations.Add(new FirmLocationState(loc, status, firmInfo.LastSeenAt));
                }
                else if (status < existing.Status) // Active(0) < ProvisionallyAbsent(1) < ConfirmedGone(2)
                {
                    acc.Locations.Remove(existing);
                    acc.Locations.Add(new FirmLocationState(loc, status, firmInfo.LastSeenAt));
                }
            }
        }

        return byFirm.Values.Select(acc =>
        {
            var rollup = acc.Locations.Count == 0
                ? FirmStatus.ConfirmedGone
                : acc.Locations.Min(l => l.Status); // most alive across all locations

            return new CurrentFirm(
                FirmId: acc.FirmId,
                Latest: acc.Latest!,
                FirstSeenAt: acc.FirstSeenAt,
                LastSeenAt: acc.LastSeenAt,
                RollupStatus: rollup,
                Locations: acc.Locations.AsReadOnly());
        }).ToList();
    }

    private sealed class FirmAccumulator(Guid firmId, DateTimeOffset firstSeenAt)
    {
        public Guid FirmId { get; } = firmId;
        public Domain.Solicitor? Latest { get; set; }
        public DateTimeOffset FirstSeenAt { get; set; } = firstSeenAt;
        public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.MinValue;
        public List<FirmLocationState> Locations { get; } = [];
    }
}

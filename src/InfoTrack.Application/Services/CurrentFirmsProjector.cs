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

        // 2 queries instead of 1 + 2N (one per location).
        var recentByLocation   = await sightings.GetRecentSightingsPerLocationAsync(now, K, ct);
        var allFirmsByLocation = await sightings.GetAllFirmLastSeenPerLocationAsync(ct);

        var byFirm = new Dictionary<Guid, FirmAccumulator>();

        foreach (var (loc, allFirms) in allFirmsByLocation)
        {
            recentByLocation.TryGetValue(loc, out var recentSets);
            ProcessLocation(loc, allFirms, recentSets ?? [], byFirm);
        }

        return BuildResult(byFirm);
    }

    /// <summary>
    /// Projects current state for a single firm, scoped to only the locations where
    /// that firm has been seen. Avoids loading the full projection for a per-firm lookup.
    /// Returns null when the firm ID is unknown.
    /// </summary>
    public async Task<CurrentFirm?> BuildForFirmAsync(Guid firmId, CancellationToken ct)
    {
        // GetFirmReviewHistoryAsync already does one query and gives us the firm's locations.
        var history = await sightings.GetFirmReviewHistoryAsync(firmId, ct);
        var firmLocations = history.Select(p => p.Location).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (firmLocations.Count == 0)
            return null;

        var now = DateTimeOffset.UtcNow;
        var byFirm = new Dictionary<Guid, FirmAccumulator>();

        foreach (var loc in firmLocations)
        {
            var recentSets = await sightings.GetRecentLocationSightingsAsync(loc, now, K, ct);
            var allFirms   = await sightings.GetLocationFirmLastSeenAsync(loc, ct);
            ProcessLocation(loc, allFirms, recentSets, byFirm);
        }

        return BuildResult(byFirm).FirstOrDefault(f => f.FirmId == firmId);
    }

    private void ProcessLocation(
        string loc,
        IReadOnlyList<LocationFirmLastSeen> allFirms,
        IReadOnlyList<LocationRunSightings> recentSets,
        Dictionary<Guid, FirmAccumulator> byFirm)
    {
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
                var absenceConfidence = confirmer.ConfidenceForAbsent(firmInfo.IdentityKey, recentSets, K);
                status = absenceConfidence == ChangeConfidence.Confirmed
                    ? FirmStatus.ConfirmedGone
                    : FirmStatus.ProvisionallyAbsent;
            }

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

    private static IReadOnlyList<CurrentFirm> BuildResult(Dictionary<Guid, FirmAccumulator> byFirm) =>
        byFirm.Values.Select(acc =>
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

    private sealed class FirmAccumulator(Guid firmId, DateTimeOffset firstSeenAt)
    {
        public Guid FirmId { get; } = firmId;
        public Domain.Solicitor? Latest { get; set; }
        public DateTimeOffset FirstSeenAt { get; set; } = firstSeenAt;
        public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.MinValue;
        public List<FirmLocationState> Locations { get; } = [];
    }
}

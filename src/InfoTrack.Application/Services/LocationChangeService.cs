using InfoTrack.Application.Configuration;
using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.Extensions.Options;

namespace InfoTrack.Application.Services;

/// <summary>
/// Produces the per-location-baseline change view with confidence tagging.
/// For each location in the subject run, picks the most recent earlier successful run of
/// that specific location as the baseline — reducing NotComparable verdicts caused by
/// runs covering different location subsets.
/// </summary>
public sealed class LocationChangeService(
    ISearchRunRepository repository,
    ISightingRepository sightings,
    ChangeConfirmer confirmer,
    IOptions<ChangeDetectionOptions> opts)
{
    private int K => opts.Value.ConfirmationWindow;

    /// <summary>
    /// Builds the default per-location view for the given subject run.
    /// Each location uses its own most recent earlier successful run as the baseline.
    /// </summary>
    public async Task<ChangeView> BuildDefaultViewAsync(Guid subjectRunId, CancellationToken ct)
    {
        var subject = await repository.GetAsync(subjectRunId, ct);
        if (subject is null)
            throw new KeyNotFoundException($"Run {subjectRunId} not found.");

        var locations = new List<LocationChange>();

        foreach (var loc in subject.Locations.OrderBy(l => l.Location, StringComparer.OrdinalIgnoreCase))
        {
            if (loc.Status != LocationOutcomeStatus.Success)
            {
                locations.Add(new LocationChange(loc.Location, ComparabilityStatus.ScrapeFailed, null, [], []));
                continue;
            }

            // Fetch K+1 recent successful runs of this location (subject + up to K prior).
            var recentSets = await sightings.GetRecentLocationSightingsAsync(
                loc.Location, subject.RunAtUtc, K + 1, ct);

            // recentSets[0] is the subject run; need at least one prior run.
            if (recentSets.Count < 2)
            {
                locations.Add(new LocationChange(loc.Location, ComparabilityStatus.NoBaseline, null, [], []));
                continue;
            }
            var subjectSet  = recentSets[0];
            var baselineSet = recentSets[1];

            var newKeys    = subjectSet.FirmsByKey.Keys.Except(baselineSet.FirmsByKey.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var absentKeys = baselineSet.FirmsByKey.Keys.Except(subjectSet.FirmsByKey.Keys, StringComparer.OrdinalIgnoreCase).ToList();

            var newFirms = newKeys
                .Select(k => new ChangedFirm(subjectSet.FirmsByKey[k], confirmer.ConfidenceForNew(k, recentSets, K)))
                .ToList();
            var absentFirms = absentKeys
                .Select(k => new ChangedFirm(baselineSet.FirmsByKey[k], confirmer.ConfidenceForAbsent(k, recentSets, K)))
                .ToList();

            locations.Add(new LocationChange(
                loc.Location,
                ComparabilityStatus.Comparable,
                baselineSet.RunId,
                newFirms,
                absentFirms));
        }

        return new ChangeView(subjectRunId, subject.RunAtUtc, locations);
    }

}

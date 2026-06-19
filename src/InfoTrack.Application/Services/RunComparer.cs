using InfoTrack.Application.DTOs;
using InfoTrack.Domain;

namespace InfoTrack.Application.Services;

// Pure: no I/O. Register as singleton.
// A change claim is only made when a location is Success in BOTH runs; every other
// location is labelled NotRequested or ScrapeFailed rather than inventing a result.
public sealed class RunComparer
{
    public RunDiff Compare(StoredRun subject, StoredRun baseline)
    {
        var subjectByLoc  = subject.Locations .ToDictionary(l => l.Location, StringComparer.OrdinalIgnoreCase);
        var baselineByLoc = baseline.Locations.ToDictionary(l => l.Location, StringComparer.OrdinalIgnoreCase);

        var allLocations = subjectByLoc.Keys.Union(baselineByLoc.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase);

        var diffs = new List<LocationDiff>();

        foreach (var loc in allLocations)
        {
            subjectByLoc .TryGetValue(loc, out var s);
            baselineByLoc.TryGetValue(loc, out var b);

            ComparabilityStatus verdict;
            if (s is null || b is null)
                verdict = ComparabilityStatus.NotRequested;
            else if (s.Status != LocationOutcomeStatus.Success || b.Status != LocationOutcomeStatus.Success)
                verdict = ComparabilityStatus.ScrapeFailed;
            else
                verdict = ComparabilityStatus.Comparable;

            if (verdict != ComparabilityStatus.Comparable)
            {
                diffs.Add(new LocationDiff(loc, verdict, [], []));
                continue;
            }

            var subjById  = s!.Firms.ToDictionary(FirmIdentity.BranchKey, StringComparer.OrdinalIgnoreCase);
            var baseById  = b!.Firms.ToDictionary(FirmIdentity.BranchKey, StringComparer.OrdinalIgnoreCase);

            var newFirms    = subjById.Where(kv => !baseById.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
            var absentFirms = baseById.Where(kv => !subjById.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();

            diffs.Add(new LocationDiff(loc, ComparabilityStatus.Comparable, newFirms, absentFirms));
        }

        return new RunDiff(subject.RunId, baseline.RunId, null, diffs);
    }
}

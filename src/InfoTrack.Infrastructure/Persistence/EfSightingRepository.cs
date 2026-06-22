using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoTrack.Infrastructure.Persistence;

public sealed class EfSightingRepository(AppDbContext db) : ISightingRepository
{
    public async Task<IReadOnlyList<LocationRunSightings>> GetRecentLocationSightingsAsync(
        string location, DateTimeOffset upToInclusive, int count, CancellationToken ct)
    {
        // Step 1: find the eligible LocationOutcome IDs.
        var eligibleIds = await (
            from run in db.SearchRuns
            join lo in db.LocationOutcomes on run.Id equals lo.SearchRunId
            where lo.Location == location
               && lo.Status == LocationOutcomeStatus.Success
               && run.RunAtUtc <= upToInclusive
            orderby run.RunAtUtc descending, lo.SearchRunId descending
            select lo.Id
        ).Take(count).ToListAsync(ct);

        if (eligibleIds.Count == 0)
            return [];

        // Step 2: load the full outcomes with run + sightings + firms.
        var outcomes = await db.LocationOutcomes
            .Where(lo => eligibleIds.Contains(lo.Id))
            .Include(lo => lo.SearchRun)
            .Include(lo => lo.Sightings)
                .ThenInclude(s => s.Firm)
            .ToListAsync(ct);

        // Restore the original ordering (newest first) from step 1.
        return eligibleIds
            .Select(id => outcomes.First(o => o.Id == id))
            .Select(lo => new LocationRunSightings(
                RunId: lo.SearchRunId,
                RunAtUtc: lo.SearchRun.RunAtUtc,
                FirmsByKey: lo.Sightings.ToDictionary(
                    s => s.Firm.IdentityKey,
                    s => FirmMapper.ToSolicitor(s, location, lo.SearchRun.RunAtUtc),
                    StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<IReadOnlyList<LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(
        string location, CancellationToken ct)
    {
        var rows = await (
            from s in db.Sightings
            join lo in db.LocationOutcomes on s.LocationOutcomeId equals lo.Id
            join run in db.SearchRuns on lo.SearchRunId equals run.Id
            join f in db.Firms on s.FirmId equals f.Id
            where lo.Location == location
               && lo.Status == LocationOutcomeStatus.Success
            select new
            {
                FirmId = s.FirmId,
                f.IdentityKey,
                f.FirmName, f.Address, f.Town, f.Postcode,
                f.Phone, f.WebsiteUrl, f.EnquiryUrl, f.ProfileUrl,
                f.Description, f.LogoUrl, f.FirstSeenAt,
                RunAtUtc = run.RunAtUtc,
                Tier = s.Tier
            }
        ).ToListAsync(ct);

        return rows
            .GroupBy(r => r.FirmId)
            .Select(g =>
            {
                var lastSeenAt  = g.Max(r => r.RunAtUtc);
                var firstSeenAt = g.Min(r => r.FirstSeenAt);
                var latest      = g.OrderByDescending(r => r.RunAtUtc).First();
                return new LocationFirmLastSeen(
                    IdentityKey: latest.IdentityKey,
                    FirmId: latest.FirmId,
                    Latest: new Solicitor(
                        FirmName: latest.FirmName,
                        SearchedLocation: location,
                        Address: latest.Address,
                        Town: latest.Town,
                        Postcode: latest.Postcode,
                        Phone: latest.Phone,
                        WebsiteUrl: latest.WebsiteUrl,
                        EnquiryUrl: latest.EnquiryUrl,
                        ProfileUrl: latest.ProfileUrl,
                        ReviewCount: null,
                        Description: latest.Description,
                        LogoUrl: latest.LogoUrl,
                        Tier: latest.Tier,
                        ScrapedAtUtc: lastSeenAt),
                    LastSeenAt: lastSeenAt,
                    FirstSeenAt: firstSeenAt);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetLocationsWithSuccessfulRunsAsync(CancellationToken ct) =>
        await db.LocationOutcomes
            .Where(l => l.Status == LocationOutcomeStatus.Success)
            .Select(l => l.Location)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReviewPoint>> GetFirmReviewHistoryAsync(Guid firmId, CancellationToken ct)
    {
        return await (
            from s in db.Sightings
            join lo in db.LocationOutcomes on s.LocationOutcomeId equals lo.Id
            join run in db.SearchRuns on lo.SearchRunId equals run.Id
            where s.FirmId == firmId
            select new { RunAtUtc = run.RunAtUtc, lo.Location, s.ReviewCount }
        )
            .OrderBy(r => r.RunAtUtc)
            .Select(r => new ReviewPoint(r.RunAtUtc, r.Location, r.ReviewCount))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<LocationRunSightings>>> GetRecentSightingsPerLocationAsync(
        DateTimeOffset upTo, int count, CancellationToken ct)
    {
        // Step 1: load all eligible (outcomeId, location, runId, runAtUtc) for successful runs.
        // "Top K per group" is grouped in memory.
        var allEligible = await (
            from run in db.SearchRuns
            join lo in db.LocationOutcomes on run.Id equals lo.SearchRunId
            where lo.Status == LocationOutcomeStatus.Success && run.RunAtUtc <= upTo
            orderby run.RunAtUtc descending
            select new { lo.Id, lo.Location, lo.SearchRunId, run.RunAtUtc }
        ).ToListAsync(ct);

        var topKByLocation = allEligible
            .GroupBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Take(count).ToList(),
                StringComparer.OrdinalIgnoreCase);

        if (topKByLocation.Count == 0)
            return new Dictionary<string, IReadOnlyList<LocationRunSightings>>(StringComparer.OrdinalIgnoreCase);

        var topKIds = topKByLocation.Values.SelectMany(v => v.Select(r => r.Id)).ToList();

        // Step 2: load all sightings for the selected outcomes in one query.
        var sightingRows = await db.Sightings
            .Where(s => topKIds.Contains(s.LocationOutcomeId))
            .Include(s => s.Firm)
            .ToListAsync(ct);
        var sightingsByOutcome = sightingRows.ToLookup(s => s.LocationOutcomeId);

        return topKByLocation.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<LocationRunSightings>)kv.Value
                .Select(outcome => new LocationRunSightings(
                    RunId: outcome.SearchRunId,
                    RunAtUtc: outcome.RunAtUtc,
                    FirmsByKey: sightingsByOutcome[outcome.Id]
                        .ToDictionary(
                            s => s.Firm.IdentityKey,
                            s => FirmMapper.ToSolicitor(s, kv.Key, outcome.RunAtUtc),
                            StringComparer.OrdinalIgnoreCase)))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<LocationFirmLastSeen>>> GetAllFirmLastSeenPerLocationAsync(
        CancellationToken ct)
    {
        var rows = await (
            from s in db.Sightings
            join lo in db.LocationOutcomes on s.LocationOutcomeId equals lo.Id
            join run in db.SearchRuns on lo.SearchRunId equals run.Id
            join f in db.Firms on s.FirmId equals f.Id
            where lo.Status == LocationOutcomeStatus.Success
            select new
            {
                lo.Location,
                FirmId = s.FirmId,
                f.IdentityKey,
                f.FirmName, f.Address, f.Town, f.Postcode,
                f.Phone, f.WebsiteUrl, f.EnquiryUrl, f.ProfileUrl,
                f.Description, f.LogoUrl, f.FirstSeenAt,
                RunAtUtc = run.RunAtUtc,
                Tier = s.Tier
            }
        ).ToListAsync(ct);

        return rows
            .GroupBy(r => r.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LocationFirmLastSeen>)g
                    .GroupBy(r => r.FirmId)
                    .Select(fg =>
                    {
                        var lastSeenAt  = fg.Max(r => r.RunAtUtc);
                        var firstSeenAt = fg.Min(r => r.FirstSeenAt);
                        var latest      = fg.OrderByDescending(r => r.RunAtUtc).First();
                        return new LocationFirmLastSeen(
                            IdentityKey: latest.IdentityKey,
                            FirmId: latest.FirmId,
                            Latest: new Solicitor(
                                FirmName: latest.FirmName,
                                SearchedLocation: g.Key,
                                Address: latest.Address,
                                Town: latest.Town,
                                Postcode: latest.Postcode,
                                Phone: latest.Phone,
                                WebsiteUrl: latest.WebsiteUrl,
                                EnquiryUrl: latest.EnquiryUrl,
                                ProfileUrl: latest.ProfileUrl,
                                ReviewCount: null,
                                Description: latest.Description,
                                LogoUrl: latest.LogoUrl,
                                Tier: latest.Tier,
                                ScrapedAtUtc: lastSeenAt),
                            LastSeenAt: lastSeenAt,
                            FirstSeenAt: firstSeenAt);
                    })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }
}

using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoTrack.Infrastructure.Persistence;

public sealed class EfSearchRunRepository(AppDbContext db) : ISearchRunRepository, ISightingRepository
{
    public async Task<Guid> SaveAsync(SearchResult result, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var runAtUtc = result.RunAtUtc.ToOffset(TimeSpan.Zero);

        var run = new SearchRunEntity
        {
            Id = runId,
            RunAtUtc = runAtUtc,
            AreaOfLaw = result.AreaOfLaw,
            RequestedLocations = result.LocationOutcomes.Select(o => o.Location).ToList()
        };

        Dictionary<string, Solicitor> foundByKey = result.UniqueSolicitors
            .ToDictionary(FirmIdentity.BranchKey, StringComparer.OrdinalIgnoreCase);

        // This is needed to build sightings — which firms appeared in which location
        Dictionary<string, Dictionary<string, Solicitor>> foundByLocation = result.LocationOutcomes
            .Where(o => o.Status == LocationOutcomeStatus.Success)
            .ToDictionary(
                o => o.Location,
                o => o.Solicitors
                    .GroupBy(FirmIdentity.BranchKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var allKeys = foundByKey.Keys.ToList();

        // 1. Load all existing firms for these keys in one query.
        var existingFirms = await db.Firms
            .Where(f => allKeys.Contains(f.IdentityKey))
            .ToListAsync(ct);
        Dictionary<string, FirmEntity> existingFirmByKey = existingFirms.ToDictionary(f => f.IdentityKey, StringComparer.OrdinalIgnoreCase);

        // 2. Upsert firms: refresh latest attributes and LastSeenAt; create new ones.
        foreach (var key in allKeys)
        {
            var solicitorData = foundByKey[key];
            if (existingFirmByKey.TryGetValue(key, out var firm))
            {
                firm.LastSeenAt = runAtUtc;
                RefreshAttributes(firm, solicitorData);
            }
            else
            {
                firm = NewFirmEntity(solicitorData, key, runAtUtc);
                db.Firms.Add(firm);
                existingFirmByKey[key] = firm;
            }
        }

        // 3. Build a LocationOutcomeEntity for every requested location.
        foreach (var o in result.LocationOutcomes)
        {
            var loc = new LocationOutcomeEntity
            {
                Id = Guid.NewGuid(),
                SearchRunId = runId,
                Location = o.Location,
                RequestedUrl = o.RequestedUrl.ToString(),
                Status = o.Status,
                ErrorMessage = o.ErrorMessage
            };

            if (o.Status == LocationOutcomeStatus.Success &&
                foundByLocation.TryGetValue(o.Location, out var solicitorsByKey))
            {
                foreach (var (key, s) in solicitorsByKey)
                    loc.Sightings.Add(new SightingEntity
                    {
                        Id = Guid.NewGuid(),
                        FirmId = existingFirmByKey[key].Id,
                        ReviewCount = s.ReviewCount
                    });
            }

            run.Locations.Add(loc);
        }

        run.TotalLocations = result.LocationOutcomes.Count;
        run.TotalUniqueFirms = allKeys.Count;

        db.SearchRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return runId;
    }

    public async Task<StoredRun?> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await db.SearchRuns
            .Include(r => r.Locations)
                .ThenInclude(l => l.Sightings)
                    .ThenInclude(s => s.Firm)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        return run is null ? null : MapToStoredRun(run);
    }

    public async Task<IReadOnlyList<RunListItem>> ListAsync(CancellationToken ct) =>
        await db.SearchRuns
            .OrderByDescending(r => r.RunAtUtc)
            .Select(r => new RunListItem(r.Id, r.RunAtUtc, r.AreaOfLaw, r.TotalLocations, r.TotalUniqueFirms))
            .ToListAsync(ct);

    public async Task<Guid?> GetPreviousRunIdAsync(Guid runId, CancellationToken ct)
    {
        var runAt = await db.SearchRuns
            .Where(r => r.Id == runId)
            .Select(r => (DateTimeOffset?)r.RunAtUtc)
            .FirstOrDefaultAsync(ct);

        if (runAt is null) return null;

        return await db.SearchRuns
            .Where(r => r.RunAtUtc < runAt && r.Id != runId)
            .OrderByDescending(r => r.RunAtUtc)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
    }


    private static StoredRun MapToStoredRun(SearchRunEntity run) => new(
        RunId: run.Id,
        RunAtUtc: run.RunAtUtc,
        AreaOfLaw: run.AreaOfLaw,
        Locations: run.Locations.Select(l => MapToStoredLocation(l, run.RunAtUtc)).ToList());

    private static StoredLocation MapToStoredLocation(LocationOutcomeEntity l, DateTimeOffset runAtUtc) => new(
        Location: l.Location,
        RequestedUrl: l.RequestedUrl,
        Status: l.Status,
        ErrorMessage: l.ErrorMessage,
        Firms: l.Sightings.Select(s => MapToSolicitor(s, l.Location, runAtUtc)).ToList());

    private static Solicitor MapToSolicitor(SightingEntity s, string location, DateTimeOffset scrapedAt) => new(
        FirmName: s.Firm.FirmName,
        SearchedLocation: location,
        Address: s.Firm.Address,
        Town: s.Firm.Town,
        Postcode: s.Firm.Postcode,
        Phone: s.Firm.Phone,
        WebsiteUrl: s.Firm.WebsiteUrl,
        EnquiryUrl: s.Firm.EnquiryUrl,
        ProfileUrl: s.Firm.ProfileUrl,
        ReviewCount: s.ReviewCount,
        Description: s.Firm.Description,
        LogoUrl: s.Firm.LogoUrl,
        Tier: ListingTier.Featured,   // Tier is not persisted; default to Featured on read-back
        ScrapedAtUtc: scrapedAt);


    private static FirmEntity NewFirmEntity(Solicitor s, string key, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        IdentityKey = key,
        FirstSeenAt = now,
        LastSeenAt = now,
        FirmName = s.FirmName,
        Address = s.Address,
        Town = s.Town,
        Postcode = s.Postcode,
        Phone = s.Phone,
        WebsiteUrl = s.WebsiteUrl,
        EnquiryUrl = s.EnquiryUrl,
        ProfileUrl = s.ProfileUrl,
        Description = s.Description,
        LogoUrl = s.LogoUrl
    };

    private static void RefreshAttributes(FirmEntity firm, Solicitor s)
    {
        firm.FirmName = s.FirmName;
        firm.Address = s.Address;
        firm.Town = s.Town;
        firm.Postcode = s.Postcode;
        firm.Phone = s.Phone;
        firm.WebsiteUrl = s.WebsiteUrl;
        firm.EnquiryUrl = s.EnquiryUrl;
        firm.ProfileUrl = s.ProfileUrl;
        firm.Description = s.Description;
        firm.LogoUrl = s.LogoUrl;
    }

    // --- Phase 2 FULL: per-location confirmation window queries ---

    public async Task<IReadOnlyList<LocationRunSightings>> GetRecentLocationSightingsAsync(
        string location, DateTimeOffset upToInclusive, int count, CancellationToken ct)
    {
        // Step 1: find the eligible LocationOutcome IDs using explicit joins to avoid
        // DateTimeOffset navigation-property translation issues with SQLite.
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
                    s => MapToSolicitor(s, location, lo.SearchRun.RunAtUtc),
                    StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<IReadOnlyList<LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(
        string location, CancellationToken ct)
    {
        // Load all sightings for successful runs of this location via explicit joins.
        // Group and aggregate in memory to avoid SQLite DateTimeOffset aggregate limitations.
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
                RunAtUtc = run.RunAtUtc
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
                        Tier: ListingTier.Featured,
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
        // Load sightings with run info via explicit join; sort in memory to avoid DateTimeOffset ORDER BY issues.
        var rows = await (
            from s in db.Sightings
            join lo in db.LocationOutcomes on s.LocationOutcomeId equals lo.Id
            join run in db.SearchRuns on lo.SearchRunId equals run.Id
            where s.FirmId == firmId
            select new { RunAtUtc = run.RunAtUtc, lo.Location, s.ReviewCount }
        ).ToListAsync(ct);

        return rows
            .OrderBy(r => r.RunAtUtc)
            .Select(r => new ReviewPoint(r.RunAtUtc, r.Location, r.ReviewCount))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<LocationRunSightings>>> GetRecentSightingsPerLocationAsync(
        DateTimeOffset upTo, int count, CancellationToken ct)
    {
        // Step 1: load all eligible (outcomeId, location, runId, runAtUtc) for successful runs.
        // "Top K per group" is grouped in memory to stay provider-agnostic (Postgres + SQLite).
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
                            s => MapToSolicitor(s, kv.Key, outcome.RunAtUtc),
                            StringComparer.OrdinalIgnoreCase)))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<LocationFirmLastSeen>>> GetAllFirmLastSeenPerLocationAsync(
        CancellationToken ct)
    {
        // One query across all locations; group and aggregate in memory (same pattern as the
        // per-location method, extended to all locations at once).
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
                RunAtUtc = run.RunAtUtc
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
                                Tier: ListingTier.Featured,
                                ScrapedAtUtc: lastSeenAt),
                            LastSeenAt: lastSeenAt,
                            FirstSeenAt: firstSeenAt);
                    })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }
}


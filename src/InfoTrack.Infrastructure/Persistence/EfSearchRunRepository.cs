using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace InfoTrack.Infrastructure.Persistence;

public sealed class EfSearchRunRepository(AppDbContext db) : ISearchRunRepository
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
}

using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfoTrack.Infrastructure.Persistence;

public sealed class EfSearchRunRepository(AppDbContext db) : ISearchRunRepository
{
    public async Task<Guid> SaveAsync(SearchResult result, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            // Discard stale tracked entities so the next attempt re-reads from the DB
            db.ChangeTracker.Clear();
            try
            {
                return await SaveCoreAsync(result, ct);
            }
            // TODO: Leave as 3 attempts hardcoded for this MVP. Revisit later.
            catch (DbUpdateException ex) when (attempt < 2 && IsFirmUniqueViolation(ex))
            {
            }
        }
    }

    private async Task<Guid> SaveCoreAsync(SearchResult result, CancellationToken ct)
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
                        ReviewCount = s.ReviewCount,
                        Tier = s.Tier
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

    private static bool IsFirmUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

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
        Firms: l.Sightings.Select(s => FirmMapper.ToSolicitor(s, l.Location, runAtUtc)).ToList());

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

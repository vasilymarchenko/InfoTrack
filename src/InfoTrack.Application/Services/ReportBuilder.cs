using InfoTrack.Application.Ports;
using InfoTrack.Domain;

namespace InfoTrack.Application.Services;

public class ReportBuilder : IReportBuilder
{

    public SearchReport Build(SearchResult result)
    {
        var outcomes = result.LocationOutcomes;
        var unique = result.UniqueSolicitors;

        var summary = new RunSummary(
            TotalLocationsRequested: outcomes.Count,
            SuccessfulLocations: outcomes.Count(o => o.Status == LocationOutcomeStatus.Success),
            EmptyLocations: outcomes.Count(o => o.Status == LocationOutcomeStatus.Empty),
            UnavailableLocations: outcomes.Count(o => o.Status == LocationOutcomeStatus.Unavailable),
            ErrorLocations: outcomes.Count(o => o.Status == LocationOutcomeStatus.Error),
            TotalUniqueSolicitors: unique.Count,
            RunAtUtc: result.RunAtUtc);

        var locationSummaries = outcomes
            .Select(o => new LocationSummary(o.Location, o.Status, o.Solicitors.Count, o.ErrorMessage))
            .ToList();

        var topFirms = unique
            .Where(s => s.ReviewCount.HasValue)
            .GroupBy(s => FirmIdentity.NormaliseName(s.FirmName))
            .Select(g => g.MaxBy(s => s.ReviewCount)!)
            .OrderByDescending(s => s.ReviewCount)
            .Take(10)
            .Select(s => new FirmRanking(s.FirmName, s.SearchedLocation, s.ReviewCount))
            .ToList();

        var multiLocationFirms = unique
            .GroupBy(s => FirmIdentity.NormaliseName(s.FirmName))
            .Where(g => g.Select(s => s.SearchedLocation).Distinct().Count() >= 2)
            .Select(g => new MultiLocationFirm(
                g.Key,
                g.Select(s => s.SearchedLocation).Distinct().Order().ToList(),
                g.Select(s => s.SearchedLocation).Distinct().Count()))
            .OrderByDescending(f => f.LocationCount)
            .ToList();

        var contactability = BuildContactability(unique);

        var displaySolicitors = unique
            .GroupBy(s => $"{FirmIdentity.NormaliseName(s.FirmName)}|{s.SearchedLocation.Trim().ToLowerInvariant()}")
            .Select(g => g.MaxBy(s => s.ReviewCount ?? -1)!)
            .OrderByDescending(s => s.ReviewCount)
            .ToList();

        return new SearchReport(summary, locationSummaries, topFirms, multiLocationFirms, contactability, displaySolicitors);
    }

    private static Contactability BuildContactability(IReadOnlyList<Solicitor> firms)
    {
        int total = firms.Count;
        int withPhone = firms.Count(f => !string.IsNullOrWhiteSpace(f.Phone));
        int withWebsite = firms.Count(f => !string.IsNullOrWhiteSpace(f.WebsiteUrl));
        int withEither = firms.Count(f => !string.IsNullOrWhiteSpace(f.Phone) || !string.IsNullOrWhiteSpace(f.WebsiteUrl));

        return new Contactability(
            TotalFirms: total,
            WithPhone: withPhone,
            WithWebsite: withWebsite,
            WithPhoneOrWebsite: withEither,
            PercentWithPhone: total == 0 ? 0 : Math.Round(withPhone * 100.0 / total, 1),
            PercentWithWebsite: total == 0 ? 0 : Math.Round(withWebsite * 100.0 / total, 1),
            PercentWithPhoneOrWebsite: total == 0 ? 0 : Math.Round(withEither * 100.0 / total, 1));
    }

}

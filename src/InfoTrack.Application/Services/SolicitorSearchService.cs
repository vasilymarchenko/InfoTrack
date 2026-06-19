using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;

namespace InfoTrack.Application.Services;

public class SolicitorSearchService : ISolicitorSearchService
{
    private readonly ILocationResolver _resolver;
    private readonly IListingFetcher _fetcher;
    private readonly IListingParser _parser;
    private readonly IReportBuilder _reportBuilder;
    private readonly ISearchRunRepository _repository;
    private readonly int _maxParallelism;

    public SolicitorSearchService(
        ILocationResolver resolver,
        IListingFetcher fetcher,
        IListingParser parser,
        IReportBuilder reportBuilder,
        ISearchRunRepository repository,
        int maxParallelism = 4)
    {
        _resolver = resolver;
        _fetcher = fetcher;
        _parser = parser;
        _reportBuilder = reportBuilder;
        _repository = repository;
        _maxParallelism = maxParallelism;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        if (request.Locations == null || request.Locations.Count == 0)
            throw new ArgumentException("At least one location is required.", nameof(request));

        var locations = request.Locations
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var throttle = new SemaphoreSlim(_maxParallelism);
        var tasks = locations.Select(loc => FetchLocationAsync(loc, request.AreaOfLaw, throttle, ct));
        var outcomes = await Task.WhenAll(tasks);

        var uniqueSolicitors = Deduplicate(outcomes.SelectMany(o => o.Solicitors).ToList());

        var result = new SearchResult(
            RunAtUtc: DateTimeOffset.UtcNow,
            AreaOfLaw: request.AreaOfLaw,
            LocationOutcomes: outcomes,
            UniqueSolicitors: uniqueSolicitors);

        var report = _reportBuilder.Build(result);
        // SaveAsync propagates on failure — persistence is the point of Phase 2.
        var runId = await _repository.SaveAsync(result, ct);
        return new SearchResponse(result, report, runId);
    }

    private async Task<LocationOutcome> FetchLocationAsync(
        string location, string areaOfLaw, SemaphoreSlim throttle, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            var resolved = _resolver.Resolve(location);
            var fetch = await _fetcher.FetchAsync(resolved.Url, ct);

            if (fetch.IsNotFound)
                return new LocationOutcome(location, resolved.Url, LocationOutcomeStatus.Unavailable, Array.Empty<Solicitor>(), "Page not found.");

            if (fetch.IsError)
                return new LocationOutcome(location, resolved.Url, LocationOutcomeStatus.Error, Array.Empty<Solicitor>(), fetch.ErrorMessage);

            var solicitors = _parser.Parse(fetch.Html!, location);
            var status = solicitors.Count == 0 ? LocationOutcomeStatus.Empty : LocationOutcomeStatus.Success;
            return new LocationOutcome(location, resolved.Url, status, solicitors, null);
        }
        catch (Exception ex)
        {
            var resolved = _resolver.Resolve(location);
            return new LocationOutcome(location, resolved.Url, LocationOutcomeStatus.Error, Array.Empty<Solicitor>(), ex.Message);
        }
        finally
        {
            throttle.Release();
        }
    }

    private static IReadOnlyList<Solicitor> Deduplicate(IList<Solicitor> solicitors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Solicitor>();

        foreach (var s in solicitors)
        {
            if (seen.Add(FirmIdentity.BranchKey(s)))
                result.Add(s);
        }

        return result;
    }
}

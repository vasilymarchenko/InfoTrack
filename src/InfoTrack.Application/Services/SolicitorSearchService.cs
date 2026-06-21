using InfoTrack.Application.Configuration;
using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using Microsoft.Extensions.Options;

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
        IOptions<SearchServiceOptions> options)
    {
        _resolver = resolver;
        _fetcher = fetcher;
        _parser = parser;
        _reportBuilder = reportBuilder;
        _repository = repository;
        _maxParallelism = options.Value.MaxParallelism;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var locations = request.Locations
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (locations.Count == 0)
            throw new ArgumentException("At least one valid location is required.", nameof(request));

        var throttle = new SemaphoreSlim(_maxParallelism);
        var tasks = locations.Select(loc => FetchLocationAsync(loc, throttle, ct));
        var outcomes = await Task.WhenAll(tasks);

        var uniqueSolicitors = Deduplicate(outcomes.SelectMany(o => o.Solicitors).ToList());

        var result = new SearchResult(
            RunAtUtc: DateTimeOffset.UtcNow,
            AreaOfLaw: AreasOfLaw.Conveyancing,
            LocationOutcomes: outcomes,
            UniqueSolicitors: uniqueSolicitors);

        var report = _reportBuilder.Build(result);
        // SaveAsync propagates on failure — persistence is the point of Phase 2.
        var runId = await _repository.SaveAsync(result, ct);
        return new SearchResponse(result, report, runId);
    }

    private async Task<LocationOutcome> FetchLocationAsync(
        string location, SemaphoreSlim throttle, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            var resolved = _resolver.Resolve(location);
            var fetch = await _fetcher.FetchAsync(resolved.Url, ct);

            if (fetch.IsNotFound)
                return LocationOutcome.Unavailable(location, resolved.Url);

            if (fetch.IsError)
                return LocationOutcome.Failed(location, resolved.Url, fetch.ErrorMessage!);

            var solicitors = _parser.Parse(fetch.Html!, location);
            return LocationOutcome.Succeeded(location, resolved.Url, solicitors);
        }
        catch (Exception ex)
        {
            var resolved = _resolver.Resolve(location);
            return LocationOutcome.Failed(location, resolved.Url, ex.Message);
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

using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Application.Services;
using InfoTrack.Domain;
using InfoTrack.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace InfoTrack.Api;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/searches", SearchAsync)
            .WithName("PostSearch")
            .WithSummary("Run a conveyancing solicitor search across one or more locations.");

        group.MapGet("/searches", ListRunsAsync)
            .WithName("ListRuns")
            .WithSummary("List all past search runs, newest first.");

        group.MapGet("/searches/{id:guid}", GetRunAsync)
            .WithName("GetRun")
            .WithSummary("Re-open a stored run with its recomputed report.");

        group.MapGet("/searches/{id:guid}/changes", GetChangesAsync)
            .WithName("GetChanges")
            .WithSummary("Per-location-baseline change view with confidence. Each location is compared against its own most recent earlier successful run.");

        group.MapGet("/firms", GetFirmsAsync)
            .WithName("GetFirms")
            .WithSummary("Current-firms projection. Optional: status=active|provisional|gone, addedSince={date}.");

        group.MapGet("/firms/{id:guid}", GetFirmAsync)
            .WithName("GetFirm")
            .WithSummary("A single firm's current state plus review-count history.");

        group.MapGet("/firms/{id:guid}/history", GetFirmHistoryAsync)
            .WithName("GetFirmHistory")
            .WithSummary("Review-count history and overall trend for a firm.");

        group.MapGet("/locations", GetLocations)
            .WithName("GetLocations")
            .WithSummary("Return the configured default locations.");

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("Health")
            .WithSummary("Health check — returns 200 when the API is running.");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        SearchRequest request,
        ISolicitorSearchService searchService,
        CancellationToken ct)
    {
        if (request.Locations.Count == 0)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["locations"] = ["At least one location is required."]
                });

        var response = await searchService.SearchAsync(request, ct);
        return Results.Ok(response);
    }

    private static async Task<IResult> ListRunsAsync(
        ISearchRunRepository repository,
        CancellationToken ct)
    {
        var runs = await repository.ListAsync(ct);
        return Results.Ok(runs);
    }

    private static async Task<IResult> GetRunAsync(
        Guid id,
        ISearchRunRepository repository,
        IReportBuilder reportBuilder,
        CancellationToken ct)
    {
        var stored = await repository.GetAsync(id, ct);
        if (stored is null)
            return Results.Problem(statusCode: 404, title: "Run not found.", detail: $"No search run with id {id}.");

        var result = ToSearchResult(stored);
        var report = reportBuilder.Build(result);
        return Results.Ok(new SearchResponse(result, report, stored.RunId));
    }

    private static async Task<IResult> GetChangesAsync(
        Guid id,
        LocationChangeService changeService,
        CancellationToken ct)
    {
        try
        {
            var view = await changeService.BuildDefaultViewAsync(id, ct);
            return Results.Ok(view);
        }
        catch (KeyNotFoundException)
        {
            return Results.Problem(statusCode: 404, title: "Run not found.", detail: $"No search run with id {id}.");
        }
    }

    private static async Task<IResult> GetFirmsAsync(
        string? status,
        DateTimeOffset? addedSince,
        CurrentFirmsProjector projector,
        CancellationToken ct)
    {
        var all = await projector.BuildAsync(ct);

        IEnumerable<CurrentFirm> result = all;

        if (status is not null)
        {
            var parsed = status.ToLowerInvariant() switch
            {
                "active"      => (FirmStatus?)FirmStatus.Active,
                "provisional" => FirmStatus.ProvisionallyAbsent,
                "gone"        => FirmStatus.ConfirmedGone,
                _             => null
            };

            if (parsed is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Unknown status '{status}'. Valid values: active, provisional, gone."]
                });

            result = result.Where(f => f.RollupStatus == parsed.Value);
        }

        if (addedSince.HasValue)
            result = result.Where(f => f.FirstSeenAt >= addedSince.Value);

        return Results.Ok(result.ToList());
    }

    private static async Task<IResult> GetFirmAsync(
        Guid id,
        CurrentFirmsProjector projector,
        ReviewTrendService trendService,
        CancellationToken ct)
    {
        var firm = await projector.BuildForFirmAsync(id, ct);
        if (firm is null)
            return Results.Problem(statusCode: 404, title: "Firm not found.", detail: $"No firm with id {id}.");

        var history = await trendService.BuildAsync(id, ct);
        return Results.Ok(new { firm, history });
    }

    private static async Task<IResult> GetFirmHistoryAsync(
        Guid id,
        ReviewTrendService trendService,
        CancellationToken ct)
    {
        var history = await trendService.BuildAsync(id, ct);
        if (history.Points.Count == 0)
            return Results.Problem(statusCode: 404, title: "Firm not found.", detail: $"No firm with id {id}.");

        return Results.Ok(history);
    }

    private static IResult GetLocations(IOptions<ScraperOptions> options) =>
        Results.Ok(options.Value.DefaultLocations);

    private static SearchResult ToSearchResult(StoredRun run)
    {
        var outcomes = run.Locations.Select(l => new LocationOutcome(
            Location: l.Location,
            RequestedUrl: Uri.TryCreate(l.RequestedUrl, UriKind.Absolute, out var uri)
                ? uri : new Uri("about:blank"),
            Status: l.Status,
            Solicitors: l.Firms,
            ErrorMessage: l.ErrorMessage)).ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<Solicitor>();
        foreach (var s in outcomes.SelectMany(o => o.Solicitors))
            if (seen.Add(FirmIdentity.BranchKey(s)))
                unique.Add(s);

        return new SearchResult(run.RunAtUtc, run.AreaOfLaw, outcomes, unique);
    }
}

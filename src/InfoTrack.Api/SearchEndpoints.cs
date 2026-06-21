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

        group.MapGet("/searches/{id:guid}/diff", GetDiffAsync)
            .WithName("GetDiff")
            .WithSummary("Per-location diff between two runs. Omit 'against' to compare against the previous run.");

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

    private static async Task<IResult> GetDiffAsync(
        Guid id,
        Guid? against,
        ISearchRunRepository repository,
        RunComparer comparer,
        CancellationToken ct)
    {
        var subject = await repository.GetAsync(id, ct);
        if (subject is null)
            return Results.Problem(statusCode: 404, title: "Run not found.", detail: $"No search run with id {id}.");

        StoredRun baseline;

        if (against.HasValue)
        {
            var explicitBaseline = await repository.GetAsync(against.Value, ct);
            if (explicitBaseline is null)
                return Results.Problem(statusCode: 404, title: "Baseline run not found.", detail: $"No search run with id {against.Value}.");
            baseline = explicitBaseline;
        }
        else
        {
            var previousId = await repository.GetPreviousRunIdAsync(id, ct);
            if (previousId is null)
                return Results.Ok(new RunDiff(id, null, "No earlier run to compare against.", []));

            baseline = (await repository.GetAsync(previousId.Value, ct))!;
        }

        return Results.Ok(comparer.Compare(subject, baseline));
    }

    private static IResult GetLocations(IOptions<ScraperOptions> options) =>
        Results.Ok(options.Value.DefaultLocations);

    // Reconstructs a SearchResult from a stored run so the Phase 1 ReportBuilder can be reused.
    // Firms list is recomputed via de-dup to match Phase 1 behaviour.
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

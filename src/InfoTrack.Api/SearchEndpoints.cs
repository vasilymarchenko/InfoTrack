using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
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

    private static IResult GetLocations(IOptions<ScraperOptions> options) =>
        Results.Ok(options.Value.DefaultLocations);
}

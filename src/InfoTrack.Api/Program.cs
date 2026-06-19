using InfoTrack.Api;
using InfoTrack.Application.Ports;
using InfoTrack.Application.Services;
using InfoTrack.Infrastructure;
using InfoTrack.Infrastructure.Configuration;
using InfoTrack.Infrastructure.Parsing;
using InfoTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ScraperOptions>(
    builder.Configuration.GetSection(ScraperOptions.SectionName));

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();

builder.Services.AddHttpClient<IListingFetcher, HttpListingFetcher>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + '/');
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
});

builder.Services.AddSingleton<ILocationResolver>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
    return new LocationResolver(opts);
});

builder.Services.AddSingleton<IListingParser, SolicitorsComConveyancingParser>();

builder.Services.AddSingleton<IReportBuilder>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
    return new ReportBuilder(opts.CoverageGapThreshold);
});

var cs = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddScoped<ISearchRunRepository, EfSearchRunRepository>();
builder.Services.AddSingleton<RunComparer>();

// Scoped so it captures the scoped IListingFetcher (typed HttpClient) and ISearchRunRepository safely.
builder.Services.AddScoped<ISolicitorSearchService>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ScraperOptions>>().Value;
    return new SolicitorSearchService(
        sp.GetRequiredService<ILocationResolver>(),
        sp.GetRequiredService<IListingFetcher>(),
        sp.GetRequiredService<IListingParser>(),
        sp.GetRequiredService<IReportBuilder>(),
        sp.GetRequiredService<ISearchRunRepository>(),
        opts.MaxParallelism);
});

// CORS — not configured in Phase 1 (no browser client yet).
// Configured in Phase 3 when the frontend is introduced.
// builder.Services.AddCors(options => options.AddDefaultPolicy(policy => { ... }));

var app = builder.Build();

// Apply EF migrations on startup (for simplicity in this demo; consider more robust strategies for production apps).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapSearchEndpoints();

app.Run();

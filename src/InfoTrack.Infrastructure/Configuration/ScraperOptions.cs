namespace InfoTrack.Infrastructure.Configuration;

public sealed class ScraperOptions
{
    public const string SectionName = "Scraper";

    public string BaseUrl { get; set; } = "https://www.solicitors.com";

    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; InfoTrackBot/1.0; +https://infotrack.example)";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxParallelism { get; set; } = 4;

    public string[] DefaultLocations { get; set; } = Array.Empty<string>();

    public int CoverageGapThreshold { get; set; } = 1;
}

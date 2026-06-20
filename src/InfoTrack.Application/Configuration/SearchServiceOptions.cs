namespace InfoTrack.Application.Configuration;

public sealed class SearchServiceOptions
{
    public const string SectionName = "Search";

    public int MaxParallelism { get; set; } = 4;
}

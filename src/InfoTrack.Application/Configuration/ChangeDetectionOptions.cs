namespace InfoTrack.Application.Configuration;

public sealed class ChangeDetectionOptions
{
    public const string SectionName = "ChangeDetection";

    /// <summary>
    /// The confirmation window K. A change (new or absent firm) is only Confirmed when it holds
    /// across this many consecutive successful runs of the same location. Default 3, tuned to
    /// the observed per-scrape rotation coverage.
    /// </summary>
    public int ConfirmationWindow { get; set; } = 3;
}

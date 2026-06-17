namespace InfoTrack.Domain;

public record SearchResult(
    DateTimeOffset RunAtUtc,
    string AreaOfLaw,
    IReadOnlyList<LocationOutcome> LocationOutcomes,
    IReadOnlyList<Solicitor> UniqueSolicitors);

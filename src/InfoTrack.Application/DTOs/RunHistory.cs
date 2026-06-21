using InfoTrack.Domain;

namespace InfoTrack.Application.DTOs;

public sealed record StoredRun(
    Guid RunId,
    DateTimeOffset RunAtUtc,
    string AreaOfLaw,
    IReadOnlyList<StoredLocation> Locations);

public sealed record StoredLocation(
    string Location,
    string? RequestedUrl,
    LocationOutcomeStatus Status,
    string? ErrorMessage,
    IReadOnlyList<Solicitor> Firms);

public sealed record RunListItem(
    Guid RunId,
    DateTimeOffset RunAtUtc,
    string AreaOfLaw,
    int LocationCount,
    int TotalUniqueFirms);



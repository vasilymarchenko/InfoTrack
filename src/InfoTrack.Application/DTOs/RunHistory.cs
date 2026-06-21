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

// ComparabilityStatus is defined in ChangeDetection.cs (extended with NoBaseline for FULL).

public sealed record LocationDiff(
    string Location,
    ComparabilityStatus Comparability,
    IReadOnlyList<Solicitor> NewFirms,
    IReadOnlyList<Solicitor> AbsentFirms);

public sealed record RunDiff(
    Guid SubjectRunId,
    Guid? BaselineRunId,
    string? Message,
    IReadOnlyList<LocationDiff> Locations);

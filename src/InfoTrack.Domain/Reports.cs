namespace InfoTrack.Domain;

public record SearchReport(
    RunSummary Summary,
    IReadOnlyList<LocationSummary> LocationSummaries,
    IReadOnlyList<FirmRanking> TopFirmsByReviewCount,
    IReadOnlyList<MultiLocationFirm> MultiLocationFirms,
    Contactability Contactability);

public record RunSummary(
    int TotalLocationsRequested,
    int SuccessfulLocations,
    int EmptyLocations,
    int UnavailableLocations,
    int ErrorLocations,
    int TotalUniqueSolicitors,
    DateTimeOffset RunAtUtc);

public record LocationSummary(
    string Location,
    LocationOutcomeStatus Status,
    int SolicitorCount,
    string? ErrorMessage);

public record FirmRanking(
    string FirmName,
    string Location,
    int? ReviewCount);

public record MultiLocationFirm(
    string NormalisedFirmName,
    IReadOnlyList<string> Locations,
    int LocationCount);

public record Contactability(
    int TotalFirms,
    int WithPhone,
    int WithWebsite,
    int WithPhoneOrWebsite,
    double PercentWithPhone,
    double PercentWithWebsite,
    double PercentWithPhoneOrWebsite);

namespace InfoTrack.Domain;

public enum LocationOutcomeStatus
{
    Success,
    Empty,
    Unavailable,
    Error
}

public record LocationOutcome(
    string Location,
    Uri RequestedUrl,
    LocationOutcomeStatus Status,
    IReadOnlyList<Solicitor> Solicitors,
    string? ErrorMessage);

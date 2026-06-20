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
    string? ErrorMessage)
{
    public static LocationOutcome Succeeded(string location, Uri url, IReadOnlyList<Solicitor> solicitors) =>
        new(location, url, solicitors.Count == 0 ? LocationOutcomeStatus.Empty : LocationOutcomeStatus.Success, solicitors, null);

    public static LocationOutcome Failed(string location, Uri url, string reason) =>
        new(location, url, LocationOutcomeStatus.Error, Array.Empty<Solicitor>(), reason);

    public static LocationOutcome Unavailable(string location, Uri url) =>
        new(location, url, LocationOutcomeStatus.Unavailable, Array.Empty<Solicitor>(), "Page not found.");
}

namespace InfoTrack.Application.Ports;

public record FetchResult
{
    public bool IsSuccess { get; private init; }
    public string? Html { get; private init; }
    public bool IsNotFound { get; private init; }
    public bool IsError { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static FetchResult Success(string html) => new() { IsSuccess = true, Html = html };
    public static FetchResult NotFound() => new() { IsNotFound = true };
    public static FetchResult Error(string message) => new() { IsError = true, ErrorMessage = message };
}

public interface IListingFetcher
{
    Task<FetchResult> FetchAsync(Uri url, CancellationToken ct);
}

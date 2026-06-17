using System.Net;
using InfoTrack.Application.Ports;

namespace InfoTrack.Infrastructure;

// 404 → NotFound (no throw). Other HTTP errors → FetchResult.Error.
// Network failures bubble to SolicitorSearchService's per-location try/catch.
public sealed class HttpListingFetcher : IListingFetcher
{
    private readonly HttpClient _httpClient;

    public HttpListingFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return FetchResult.NotFound();

        if (!response.IsSuccessStatusCode)
            return FetchResult.Error($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");

        var html = await response.Content.ReadAsStringAsync(ct);
        return FetchResult.Success(html);
    }
}

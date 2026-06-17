using InfoTrack.Application.DTOs;

namespace InfoTrack.Application.Ports;

public interface ISolicitorSearchService
{
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct);
}

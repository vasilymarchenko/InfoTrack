using InfoTrack.Application.DTOs;
using InfoTrack.Domain;

namespace InfoTrack.Application.Ports;

/// <summary>Persistence port for search runs. Infrastructure implements; Application depends only on this interface.</summary>
public interface ISearchRunRepository
{
    Task<Guid> SaveAsync(SearchResult result, CancellationToken ct);
    Task<StoredRun?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<RunListItem>> ListAsync(CancellationToken ct);

    /// <summary>Returns the id of the most recent run whose timestamp is strictly earlier than the given run's.</summary>
    Task<Guid?> GetPreviousRunIdAsync(Guid runId, CancellationToken ct);
}

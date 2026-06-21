using InfoTrack.Application.DTOs;

namespace InfoTrack.Application.Ports;

/// <summary>
/// Read-model port for per-location firm-sighting queries used by the FULL change-detection
/// and projection services. Infrastructure implements alongside ISearchRunRepository.
/// </summary>
public interface ISightingRepository
{
    /// <summary>
    /// The most recent <paramref name="count"/> successful runs of <paramref name="location"/>
    /// with RunAtUtc &lt;= <paramref name="upToInclusive"/>, newest first.
    /// Each entry carries that run's firms keyed by FirmIdentity.BranchKey.
    /// </summary>
    Task<IReadOnlyList<LocationRunSightings>> GetRecentLocationSightingsAsync(
        string location, DateTimeOffset upToInclusive, int count, CancellationToken ct);

    /// <summary>
    /// Every firm ever sighted in <paramref name="location"/>: latest attributes plus
    /// the RunAtUtc of its most recent sighting there.
    /// </summary>
    Task<IReadOnlyList<LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(
        string location, CancellationToken ct);

    /// <summary>Distinct locations that have at least one successful run.</summary>
    Task<IReadOnlyList<string>> GetLocationsWithSuccessfulRunsAsync(CancellationToken ct);

    /// <summary>All review-count points for one firm, ordered by RunAtUtc ascending.</summary>
    Task<IReadOnlyList<ReviewPoint>> GetFirmReviewHistoryAsync(Guid firmId, CancellationToken ct);
}

using InfoTrack.Application.DTOs;
using InfoTrack.Application.Services;

namespace InfoTrack.Tests.Application.Services;

public class ReviewTrendServiceTests
{
    // ReviewTrendService's Classify is tested indirectly via BuildAsync; we wire a stub repo.
    // Because the logic is simple and pure, we test it through a minimal FakeRepository.

    private static ReviewPoint P(int reviewCount, int dayOffset = 0) =>
        new(DateTimeOffset.UtcNow.AddDays(dayOffset), "London", reviewCount);

    private static ReviewPoint PNull(int dayOffset) =>
        new(DateTimeOffset.UtcNow.AddDays(dayOffset), "London", null);

    // Helper: directly exercise the classify logic by building a service backed by a fake repo.

    private static async Task<TrendDirection> ClassifyAsync(params ReviewPoint[] points)
    {
        var firmId = Guid.NewGuid();
        var repo = new FakeSightingRepo(firmId, points);
        var svc = new ReviewTrendService(repo);
        var history = await svc.BuildAsync(firmId, CancellationToken.None);
        return history.OverallReviewTrend;
    }

    [Fact]
    public async Task Classify_Rising_WhenLastHigherThanFirst()
    {
        var trend = await ClassifyAsync(P(10, -5), P(15, -3), P(20, 0));
        Assert.Equal(TrendDirection.Rising, trend);
    }

    [Fact]
    public async Task Classify_Falling_WhenLastLowerThanFirst()
    {
        var trend = await ClassifyAsync(P(20, -5), P(15, -3), P(5, 0));
        Assert.Equal(TrendDirection.Falling, trend);
    }

    [Fact]
    public async Task Classify_Steady_WhenFirstAndLastEqual()
    {
        var trend = await ClassifyAsync(P(10, -5), P(12, -3), P(10, 0));
        Assert.Equal(TrendDirection.Steady, trend);
    }

    [Fact]
    public async Task Classify_Unknown_WhenFewerThanTwoNonNullPoints()
    {
        var trend = await ClassifyAsync(PNull(-5), PNull(-3), PNull(0));
        Assert.Equal(TrendDirection.Unknown, trend);
    }

    [Fact]
    public async Task Classify_Unknown_WhenOnlyOneNonNullPoint()
    {
        var trend = await ClassifyAsync(PNull(-5), P(10, -3), PNull(0));
        Assert.Equal(TrendDirection.Unknown, trend);
    }

    [Fact]
    public async Task Classify_IgnoresNulls_WhenCountingFirstAndLast()
    {
        // null, 10, null, 20 → first non-null=10, last non-null=20 → Rising
        var trend = await ClassifyAsync(PNull(-6), P(10, -4), PNull(-2), P(20, 0));
        Assert.Equal(TrendDirection.Rising, trend);
    }

    private sealed class FakeSightingRepo(Guid firmId, ReviewPoint[] points)
        : InfoTrack.Application.Ports.ISightingRepository
    {
        public Task<IReadOnlyList<InfoTrack.Application.DTOs.ReviewPoint>> GetFirmReviewHistoryAsync(
            Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InfoTrack.Application.DTOs.ReviewPoint>>(
                id == firmId ? points : []);

        public Task<IReadOnlyList<InfoTrack.Application.DTOs.LocationRunSightings>> GetRecentLocationSightingsAsync(string location, DateTimeOffset upToInclusive, int count, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<InfoTrack.Application.DTOs.LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(string location, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetLocationsWithSuccessfulRunsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, IReadOnlyList<InfoTrack.Application.DTOs.LocationRunSightings>>> GetRecentSightingsPerLocationAsync(DateTimeOffset upTo, int count, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, IReadOnlyList<InfoTrack.Application.DTOs.LocationFirmLastSeen>>> GetAllFirmLastSeenPerLocationAsync(CancellationToken ct) => throw new NotImplementedException();
    }
}

using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;

namespace InfoTrack.Application.Services;

/// <summary>Computes review-count history and the overall prominence trend for a firm.</summary>
public sealed class ReviewTrendService(ISightingRepository sightings)
{
    public async Task<FirmHistory> BuildAsync(Guid firmId, CancellationToken ct)
    {
        var points = await sightings.GetFirmReviewHistoryAsync(firmId, ct);
        var trend  = Classify(points);
        return new FirmHistory(firmId, points, trend);
    }

    private static TrendDirection Classify(IReadOnlyList<ReviewPoint> points)
    {
        var withCounts = points.Where(p => p.ReviewCount.HasValue).ToList();
        if (withCounts.Count < 2)
            return TrendDirection.Unknown;

        var delta = withCounts[^1].ReviewCount!.Value - withCounts[0].ReviewCount!.Value;
        return delta > 0 ? TrendDirection.Rising
             : delta < 0 ? TrendDirection.Falling
             : TrendDirection.Steady;
    }
}

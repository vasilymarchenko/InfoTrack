using InfoTrack.Application.DTOs;
using InfoTrack.Application.Services;
using InfoTrack.Domain;

namespace InfoTrack.Tests.Application.Services;

public class ChangeConfirmerTests
{
    private static readonly ChangeConfirmer Sut = new();
    private const int K = 3;

    private static LocationRunSightings MakeSet(params string[] keys) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow,
            keys.ToDictionary(k => k, k => MakeSolicitor(k), StringComparer.OrdinalIgnoreCase));

    private static Solicitor MakeSolicitor(string firmName) =>
        new(FirmName: firmName, SearchedLocation: "London", Address: "1 Test St",
            Town: null, Postcode: "SW1A1AA", Phone: null, WebsiteUrl: null,
            EnquiryUrl: null, ProfileUrl: null, ReviewCount: null,
            Description: null, LogoUrl: null, Tier: ListingTier.Featured,
            ScrapedAtUtc: DateTimeOffset.UtcNow);

    // --- ConfidenceForAbsent ---

    [Fact]
    public void ForAbsent_FirmMissingFromEntireKWindow_ReturnsConfirmed()
    {
        // K=3 sets, firm absent from all → Confirmed
        var sets = new[] { MakeSet("A", "B"), MakeSet("A", "B"), MakeSet("A", "B") };

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Confirmed, result);
    }

    [Fact]
    public void ForAbsent_FirmPresentInMiddleOfWindow_ReturnsProvisional()
    {
        // Firm present in recentSets[1] (one step back) — flicker; must not confirm.
        var sets = new[] { MakeSet("A"), MakeSet("A", "Gone"), MakeSet("A") };

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForAbsent_FirmPresentInOldestWindowEntry_ReturnsProvisional()
    {
        // Gone only in sets[0] and sets[1] but present in sets[2] → Provisional (reappearance resets).
        var sets = new[] { MakeSet("A"), MakeSet("A"), MakeSet("A", "Gone") };

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForAbsent_InsufficientHistory_ReturnsProvisional()
    {
        // Only 2 sets, K=3 → not enough distance
        var sets = new[] { MakeSet("A"), MakeSet("A") };

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForAbsent_FlickerThenReturn_NeverConfirmed()
    {
        // Pattern: absent, present, absent, present — firm never stays gone K in a row
        var sets = new[]
        {
            MakeSet("A"),           // sets[0]: absent
            MakeSet("A", "Gone"),   // sets[1]: present (flicker back)
            MakeSet("A"),           // sets[2]: absent
            MakeSet("A", "Gone"),   // sets[3]: present again
        };

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForAbsent_ExactlyKSets_FirmAbsentAll_ReturnsConfirmed()
    {
        var sets = Enumerable.Range(0, K).Select(_ => MakeSet("A")).ToList();

        var result = Sut.ConfidenceForAbsent("Gone", sets, K);

        Assert.Equal(ChangeConfidence.Confirmed, result);
    }

    // --- ConfidenceForNew ---

    [Fact]
    public void ForNew_CleanKPriorWindow_ReturnsConfirmed()
    {
        // K+1 sets: subject has "New", K prior sets don't → Confirmed
        var sets = new[] { MakeSet("A", "New"), MakeSet("A"), MakeSet("A"), MakeSet("A") };

        var result = Sut.ConfidenceForNew("New", sets, K);

        Assert.Equal(ChangeConfidence.Confirmed, result);
    }

    [Fact]
    public void ForNew_FirmPresentInPriorWindow_ReturnsProvisional()
    {
        // "New" was seen in sets[2] (prior) — it's sampling noise, not a new arrival
        var sets = new[] { MakeSet("A", "New"), MakeSet("A"), MakeSet("A", "New"), MakeSet("A") };

        var result = Sut.ConfidenceForNew("New", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForNew_InsufficientHistory_ReturnsProvisional()
    {
        // K sets total (need K+1 for new confirmation)
        var sets = Enumerable.Range(0, K).Select(i => i == 0 ? MakeSet("A", "New") : MakeSet("A")).ToList();

        var result = Sut.ConfidenceForNew("New", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }

    [Fact]
    public void ForNew_FirmInImmediatePriorRun_ReturnsProvisional()
    {
        // "New" in subject AND in the immediately preceding run → just resampled, not new
        var sets = new[] { MakeSet("A", "New"), MakeSet("A", "New"), MakeSet("A"), MakeSet("A") };

        var result = Sut.ConfidenceForNew("New", sets, K);

        Assert.Equal(ChangeConfidence.Provisional, result);
    }
}

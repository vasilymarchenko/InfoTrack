using InfoTrack.Application.Configuration;
using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Application.Services;
using InfoTrack.Domain;
using Microsoft.Extensions.Options;

namespace InfoTrack.Tests.Application.Services;

public class LocationChangeServiceTests
{
    private static IOptions<ChangeDetectionOptions> Opts(int k) =>
        Microsoft.Extensions.Options.Options.Create(new ChangeDetectionOptions { ConfirmationWindow = k });

    private static Solicitor MakeSolicitor(string name) =>
        new(FirmName: name, SearchedLocation: "London", Address: "1 St",
            Town: null, Postcode: "SW1A1AA", Phone: null, WebsiteUrl: null,
            EnquiryUrl: null, ProfileUrl: null, ReviewCount: null,
            Description: null, LogoUrl: null, Tier: ListingTier.Featured,
            ScrapedAtUtc: DateTimeOffset.UtcNow);

    // Keys are firm names for clarity; mirrors LocationChangeService which uses BranchKey in production.
    private static LocationRunSightings MakeSightings(params string[] firmNames) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow,
            firmNames.ToDictionary(n => n, n => MakeSolicitor(n), StringComparer.OrdinalIgnoreCase));

    private static StoredRun MakeRun(Guid runId, string location, params string[] firmNames) =>
        new(runId, DateTimeOffset.UtcNow, "Conveyancing",
            [new StoredLocation(location, null, LocationOutcomeStatus.Success, null,
                firmNames.Select(n => MakeSolicitor(n)).ToList())]);

    private static LocationChangeService MakeService(
        StoredRun subject, string location, IReadOnlyList<LocationRunSightings> sightings, int k) =>
        new(new FakeRunRepo(subject), new FakeSightingRepo(location, sightings), new ChangeConfirmer(), Opts(k));

    // ── Absence detection ──────────────────────────────────────────────────────

    [Fact]
    public async Task FirstMiss_IsProvisional()
    {
        // Firm "Gone" in baseline (recentSets[1]) but missing from subject (recentSets[0]).
        // K=2 needs two consecutive misses → Provisional.
        var id = Guid.NewGuid();
        var sightings = new[]
        {
            MakeSightings("Alpha"),         // [0] subject
            MakeSightings("Alpha", "Gone"), // [1] baseline: Gone present (last sighting)
        };

        var view = await MakeService(MakeRun(id, "London", "Alpha"), "London", sightings, k: 2)
            .BuildDefaultViewAsync(id, CancellationToken.None);

        var absent = Assert.Single(view.Locations).AbsentFirms;
        var firm = Assert.Single(absent);
        Assert.Equal("Gone", firm.Firm.FirmName);
        Assert.Equal(ChangeConfidence.Provisional, firm.Confidence);
    }

    [Fact]
    public async Task SecondConsecutiveMiss_IsConfirmed_WhenK2()
    {
        // Gone absent from subject AND from the prior run — 2 consecutive misses with K=2 → Confirmed.
        var id = Guid.NewGuid();
        var sightings = new[]
        {
            MakeSightings("Alpha"),         // [0] subject: Gone absent
            MakeSightings("Alpha"),         // [1] prior: Gone absent
            MakeSightings("Alpha", "Gone"), // [2] older: last sighting
        };

        var view = await MakeService(MakeRun(id, "London", "Alpha"), "London", sightings, k: 2)
            .BuildDefaultViewAsync(id, CancellationToken.None);

        var firm = Assert.Single(Assert.Single(view.Locations).AbsentFirms);
        Assert.Equal(ChangeConfidence.Confirmed, firm.Confidence);
    }

    [Fact]
    public async Task SecondConsecutiveMiss_IsStillProvisional_WhenK3()
    {
        // Two consecutive misses but K=3 requires three → still Provisional.
        var id = Guid.NewGuid();
        var sightings = new[]
        {
            MakeSightings("Alpha"),         // [0] subject
            MakeSightings("Alpha"),         // [1] prior: absent
            MakeSightings("Alpha", "Gone"), // [2] older: last sighting
            MakeSightings("Alpha", "Gone"), // [3] oldest (K+1 entry)
        };

        var view = await MakeService(MakeRun(id, "London", "Alpha"), "London", sightings, k: 3)
            .BuildDefaultViewAsync(id, CancellationToken.None);

        var firm = Assert.Single(Assert.Single(view.Locations).AbsentFirms);
        Assert.Equal(ChangeConfidence.Provisional, firm.Confidence);
    }

    [Fact]
    public async Task FirmAbsentForMultipleRuns_StillAppearsInReport()
    {
        // Regression for Bug 2: "Gone" absent from both subject and the immediate prior run.
        // It must still appear in AbsentFirms (previously dropped because baseline - subject = empty for it).
        var id = Guid.NewGuid();
        var sightings = new[]
        {
            MakeSightings("Alpha"),         // [0] subject: Gone absent
            MakeSightings("Alpha"),         // [1] prior:   Gone absent  ← was missing from old baseline diff
            MakeSightings("Alpha", "Gone"), // [2] older:   last sighting
        };

        var view = await MakeService(MakeRun(id, "London", "Alpha"), "London", sightings, k: 3)
            .BuildDefaultViewAsync(id, CancellationToken.None);

        Assert.Single(Assert.Single(view.Locations).AbsentFirms, f => f.Firm.FirmName == "Gone");
    }

    [Fact]
    public async Task AbsentFirmCarriesLastKnownSolicitorData()
    {
        // When a firm has been absent for multiple runs, the Solicitor snapshot should come from
        // the most recent run that actually saw it, not from a run where it was already gone.
        var id = Guid.NewGuid();
        var lastKnown = MakeSolicitor("Gone") with { ReviewCount = 42 };
        var absentSet = new LocationRunSightings(Guid.NewGuid(), DateTimeOffset.UtcNow,
            new Dictionary<string, Solicitor>(StringComparer.OrdinalIgnoreCase) { ["Alpha"] = MakeSolicitor("Alpha") });
        var lastSeenSet = new LocationRunSightings(Guid.NewGuid(), DateTimeOffset.UtcNow,
            new Dictionary<string, Solicitor>(StringComparer.OrdinalIgnoreCase)
            {
                ["Alpha"] = MakeSolicitor("Alpha"),
                ["Gone"]  = lastKnown,
            });

        var sightings = new[] { absentSet, absentSet, lastSeenSet };

        var view = await MakeService(MakeRun(id, "London", "Alpha"), "London", sightings, k: 2)
            .BuildDefaultViewAsync(id, CancellationToken.None);

        var absent = Assert.Single(Assert.Single(view.Locations).AbsentFirms);
        Assert.Equal(42, absent.Firm.ReviewCount);
    }

    // ── Fake infrastructure ────────────────────────────────────────────────────

    private sealed class FakeRunRepo(StoredRun run) : ISearchRunRepository
    {
        public Task<Guid> SaveAsync(SearchResult result, CancellationToken ct) => throw new NotImplementedException();
        public Task<StoredRun?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<StoredRun?>(run);
        public Task<IReadOnlyList<RunListItem>> ListAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeSightingRepo(string location, IReadOnlyList<LocationRunSightings> sightings)
        : ISightingRepository
    {
        public Task<IReadOnlyList<LocationRunSightings>> GetRecentLocationSightingsAsync(
            string loc, DateTimeOffset upTo, int count, CancellationToken ct)
        {
            IReadOnlyList<LocationRunSightings> result = loc.Equals(location, StringComparison.OrdinalIgnoreCase)
                ? sightings.Take(count).ToList()
                : [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(string loc, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetLocationsWithSuccessfulRunsAsync(CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<ReviewPoint>> GetFirmReviewHistoryAsync(Guid firmId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, IReadOnlyList<LocationRunSightings>>> GetRecentSightingsPerLocationAsync(
            DateTimeOffset upTo, int count, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<IReadOnlyDictionary<string, IReadOnlyList<LocationFirmLastSeen>>> GetAllFirmLastSeenPerLocationAsync(
            CancellationToken ct) =>
            throw new NotImplementedException();
    }
}

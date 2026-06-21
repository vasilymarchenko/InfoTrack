using InfoTrack.Application.Configuration;
using InfoTrack.Application.DTOs;
using InfoTrack.Application.Ports;
using InfoTrack.Application.Services;
using InfoTrack.Domain;
using Microsoft.Extensions.Options;

namespace InfoTrack.Tests.Application.Services;

public class CurrentFirmsProjectorTests
{
    private const int K = 3;

    private static IOptions<ChangeDetectionOptions> Opts(int k = K) =>
        Microsoft.Extensions.Options.Options.Create(new ChangeDetectionOptions { ConfirmationWindow = k });

    private static Solicitor MakeSolicitor(string name) =>
        new(FirmName: name, SearchedLocation: "London", Address: "1 St",
            Town: null, Postcode: "SW1A1AA", Phone: null, WebsiteUrl: null,
            EnquiryUrl: null, ProfileUrl: null, ReviewCount: null,
            Description: null, LogoUrl: null, Tier: ListingTier.Featured,
            ScrapedAtUtc: DateTimeOffset.UtcNow);

    private static LocationRunSightings MakeSet(params string[] firmNames) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow,
            firmNames.ToDictionary(
                n => FirmIdentity.BranchKey(MakeSolicitor(n)),
                n => MakeSolicitor(n),
                StringComparer.OrdinalIgnoreCase));

    private static LocationFirmLastSeen MakeLastSeen(string name, DateTimeOffset lastSeen) =>
        new(IdentityKey: FirmIdentity.BranchKey(MakeSolicitor(name)),
            FirmId: Guid.NewGuid(),
            Latest: MakeSolicitor(name),
            LastSeenAt: lastSeen,
            FirstSeenAt: lastSeen.AddDays(-10));

    [Fact]
    public async Task Build_ActiveFirm_WhenInLatestRun()
    {
        var now = DateTimeOffset.UtcNow;
        var recentSets = new[] { MakeSet("Active") }; // K=1 for this simple test
        var lastSeen   = new[] { MakeLastSeen("Active", now) };

        var repo = new FakeRepo(
            locations: ["London"],
            sightingsByLocation: new() { ["London"] = [recentSets[0]] },
            lastSeenByLocation: new() { ["London"] = lastSeen });

        var projector = new CurrentFirmsProjector(repo, new ChangeConfirmer(), Opts(1));
        var result = await projector.BuildAsync(CancellationToken.None);

        var firm = Assert.Single(result);
        Assert.Equal(FirmStatus.Active, firm.RollupStatus);
    }

    [Fact]
    public async Task Build_ProvisionallyAbsentFirm_WhenMissingFromLatestButWindowShort()
    {
        var now = DateTimeOffset.UtcNow;
        // K=3 but only 2 recent sets → not enough to confirm; firm was last seen in older set
        var set1 = MakeSet("Present");
        var set2 = MakeSet("Present");   // "Gone" absent from both
        var recentSets = new[] { set1, set2 };
        var lastSeen   = new[] { MakeLastSeen("Gone", now.AddDays(-5)) };

        var repo = new FakeRepo(
            locations: ["London"],
            sightingsByLocation: new() { ["London"] = recentSets },
            lastSeenByLocation: new() { ["London"] = lastSeen });

        var projector = new CurrentFirmsProjector(repo, new ChangeConfirmer(), Opts(3));
        var result = await projector.BuildAsync(CancellationToken.None);

        var firm = Assert.Single(result);
        Assert.Equal(FirmStatus.ProvisionallyAbsent, firm.RollupStatus);
    }

    [Fact]
    public async Task Build_ConfirmedGoneFirm_WhenAbsentFromEntireKWindow()
    {
        var now = DateTimeOffset.UtcNow;
        // K=3 sets, firm absent from all
        var recentSets = Enumerable.Range(0, 3).Select(_ => MakeSet("Present")).ToList();
        var lastSeen   = new[] { MakeLastSeen("Gone", now.AddDays(-10)) };

        var repo = new FakeRepo(
            locations: ["London"],
            sightingsByLocation: new() { ["London"] = recentSets },
            lastSeenByLocation: new() { ["London"] = lastSeen });

        var projector = new CurrentFirmsProjector(repo, new ChangeConfirmer(), Opts(3));
        var result = await projector.BuildAsync(CancellationToken.None);

        var firm = Assert.Single(result);
        Assert.Equal(FirmStatus.ConfirmedGone, firm.RollupStatus);
    }

    [Fact]
    public async Task Build_RollupPicksMostAliveStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var keyActive = FirmIdentity.BranchKey(MakeSolicitor("Firm"));
        var setWithFirm    = MakeSet("Firm");
        var setWithoutFirm = MakeSet("Other");

        // Location A: firm Active
        // Location B: firm ConfirmedGone (K=1 sets, firm absent)
        var repoLocA = new[] { setWithFirm };
        var repoLocB = Enumerable.Range(0, 3).Select(_ => setWithoutFirm).ToList();

        var firmId = Guid.NewGuid();
        var lastSeenA = new LocationFirmLastSeen(
            IdentityKey: keyActive, FirmId: firmId, Latest: MakeSolicitor("Firm"),
            LastSeenAt: now, FirstSeenAt: now.AddDays(-10));
        var lastSeenB = new LocationFirmLastSeen(
            IdentityKey: keyActive, FirmId: firmId, Latest: MakeSolicitor("Firm"),
            LastSeenAt: now.AddDays(-5), FirstSeenAt: now.AddDays(-10));

        var repo = new FakeRepo(
            locations: ["London", "Manchester"],
            sightingsByLocation: new()
            {
                ["London"]     = repoLocA,
                ["Manchester"] = repoLocB
            },
            lastSeenByLocation: new()
            {
                ["London"]     = new[] { lastSeenA },
                ["Manchester"] = new[] { lastSeenB }
            });

        var projector = new CurrentFirmsProjector(repo, new ChangeConfirmer(), Opts(3));
        var result = await projector.BuildAsync(CancellationToken.None);

        // Firm is active in London → rollup = Active (most alive wins)
        var firm = result.Single(f => f.FirmId == firmId);
        Assert.Equal(FirmStatus.Active, firm.RollupStatus);
    }

    private sealed class FakeRepo(
        IReadOnlyList<string> locations,
        Dictionary<string, IReadOnlyList<LocationRunSightings>> sightingsByLocation,
        Dictionary<string, IReadOnlyList<LocationFirmLastSeen>> lastSeenByLocation)
        : ISightingRepository
    {
        public Task<IReadOnlyList<string>> GetLocationsWithSuccessfulRunsAsync(CancellationToken ct) =>
            Task.FromResult(locations);

        public Task<IReadOnlyList<LocationRunSightings>> GetRecentLocationSightingsAsync(
            string location, DateTimeOffset upToInclusive, int count, CancellationToken ct)
        {
            sightingsByLocation.TryGetValue(location, out var sets);
            IReadOnlyList<LocationRunSightings> result = (sets ?? []).Take(count).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<LocationFirmLastSeen>> GetLocationFirmLastSeenAsync(
            string location, CancellationToken ct)
        {
            lastSeenByLocation.TryGetValue(location, out var data);
            return Task.FromResult<IReadOnlyList<LocationFirmLastSeen>>(data ?? []);
        }

        public Task<IReadOnlyList<ReviewPoint>> GetFirmReviewHistoryAsync(Guid firmId, CancellationToken ct) => throw new NotImplementedException();

        public Task<IReadOnlyDictionary<string, IReadOnlyList<LocationRunSightings>>> GetRecentSightingsPerLocationAsync(
            DateTimeOffset upTo, int count, CancellationToken ct)
        {
            IReadOnlyDictionary<string, IReadOnlyList<LocationRunSightings>> result =
                sightingsByLocation.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<LocationRunSightings>)kv.Value.Take(count).ToList(),
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<string, IReadOnlyList<LocationFirmLastSeen>>> GetAllFirmLastSeenPerLocationAsync(
            CancellationToken ct)
        {
            IReadOnlyDictionary<string, IReadOnlyList<LocationFirmLastSeen>> result =
                lastSeenByLocation.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value,
                    StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }
}

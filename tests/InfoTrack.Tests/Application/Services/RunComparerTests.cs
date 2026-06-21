using InfoTrack.Application.DTOs;
using InfoTrack.Application.Services;
using InfoTrack.Domain;

namespace InfoTrack.Tests.Application.Services;

public class RunComparerTests
{
    private static readonly RunComparer Sut = new();

    private static Solicitor MakeSolicitor(string firmName, string? postcode = null, string? phone = null) =>
        new(FirmName: firmName, SearchedLocation: "London", Address: "1 Test St",
            Town: null, Postcode: postcode, Phone: phone, WebsiteUrl: null,
            EnquiryUrl: null, ProfileUrl: null, ReviewCount: null,
            Description: null, LogoUrl: null, Tier: ListingTier.Featured,
            ScrapedAtUtc: DateTimeOffset.UtcNow);

    private static StoredLocation SuccessLocation(string loc, params Solicitor[] firms) =>
        new(loc, null, LocationOutcomeStatus.Success, null, firms);

    private static StoredLocation FailedLocation(string loc, LocationOutcomeStatus status) =>
        new(loc, null, status, "error", []);

    private static StoredRun MakeRun(params StoredLocation[] locations) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, "conveyancing", locations);

    [Fact]
    public void Compare_NewFirmInSubjectOnly_AppearsInNewFirms()
    {
        var newFirm = MakeSolicitor("Alpha Law", "SW1A1AA");
        var common  = MakeSolicitor("Beta Law",  "EC1A1BB");
        var subject  = MakeRun(SuccessLocation("London", common, newFirm));
        var baseline = MakeRun(SuccessLocation("London", common));

        var diff = Sut.Compare(subject, baseline);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.Comparable, loc.Comparability);
        Assert.Equal("Alpha Law", Assert.Single(loc.NewFirms).FirmName);
        Assert.Empty(loc.AbsentFirms);
    }

    [Fact]
    public void Compare_FirmInBaselineOnly_AppearsInAbsentFirms()
    {
        var absentFirm = MakeSolicitor("Gone Law", "W1A1AA");
        var common     = MakeSolicitor("Beta Law", "EC1A1BB");
        var subject  = MakeRun(SuccessLocation("London", common));
        var baseline = MakeRun(SuccessLocation("London", common, absentFirm));

        var diff = Sut.Compare(subject, baseline);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.Comparable, loc.Comparability);
        Assert.Empty(loc.NewFirms);
        Assert.Equal("Gone Law", Assert.Single(loc.AbsentFirms).FirmName);
    }

    [Fact]
    public void Compare_IdenticalRuns_EmptyNewAndAbsent()
    {
        var firm     = MakeSolicitor("Same Law", "N11AA");
        var subject  = MakeRun(SuccessLocation("London", firm));
        var baseline = MakeRun(SuccessLocation("London", firm));

        var diff = Sut.Compare(subject, baseline);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.Comparable, loc.Comparability);
        Assert.Empty(loc.NewFirms);
        Assert.Empty(loc.AbsentFirms);
    }

    [Theory]
    [InlineData(true)]   // extra location only in subject
    [InlineData(false)]  // extra location only in baseline
    public void Compare_LocationInOneRunOnly_MarkedNotRequested(bool extraInSubject)
    {
        var firm   = MakeSolicitor("Some Law", "SW1A1AA");
        var common = SuccessLocation("London", firm);
        var extra  = SuccessLocation("Manchester", firm);

        var subject  = extraInSubject  ? MakeRun(common, extra) : MakeRun(common);
        var baseline = !extraInSubject ? MakeRun(common, extra) : MakeRun(common);

        var diff = Sut.Compare(subject, baseline);

        var extra2 = diff.Locations.Single(l =>
            !l.Location.Equals("London", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ComparabilityStatus.NotRequested, extra2.Comparability);
        Assert.Empty(extra2.NewFirms);
        Assert.Empty(extra2.AbsentFirms);
    }

    // The ScrapeFailed guard: a non-Success location must NEVER produce a change claim,
    // even if the other run had firms there. Without this, a single scrape failure would
    // look like all firms vanished (mass-extinction false positive).
    [Theory]
    [InlineData(LocationOutcomeStatus.Empty)]
    [InlineData(LocationOutcomeStatus.Error)]
    [InlineData(LocationOutcomeStatus.Unavailable)]
    public void Compare_LocationNonSuccessInSubject_MarkedScrapeFailed(LocationOutcomeStatus failStatus)
    {
        var firm     = MakeSolicitor("Big Law", "SW1A1AA");
        var subject  = MakeRun(FailedLocation("London", failStatus));
        var baseline = MakeRun(SuccessLocation("London", firm));

        var diff = Sut.Compare(subject, baseline);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.ScrapeFailed, loc.Comparability);
        Assert.Empty(loc.NewFirms);
        Assert.Empty(loc.AbsentFirms);
    }

    [Theory]
    [InlineData(LocationOutcomeStatus.Empty)]
    [InlineData(LocationOutcomeStatus.Error)]
    [InlineData(LocationOutcomeStatus.Unavailable)]
    public void Compare_LocationNonSuccessInBaseline_MarkedScrapeFailed(LocationOutcomeStatus failStatus)
    {
        var firm     = MakeSolicitor("Big Law", "SW1A1AA");
        var subject  = MakeRun(SuccessLocation("London", firm));
        var baseline = MakeRun(FailedLocation("London", failStatus));

        var diff = Sut.Compare(subject, baseline);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.ScrapeFailed, loc.Comparability);
        Assert.Empty(loc.NewFirms);
        Assert.Empty(loc.AbsentFirms);
    }

    [Fact]
    public void Compare_FirmReappearsAfterAbsence_TreatedAsNewFirm()
    {
        // Run1 has firm; Run2 doesn't; Run3 has it again.
        // MVP has no debounce — reappearance is simply new in Run3 vs Run2.
        var firm = MakeSolicitor("Returning Law", "EC1A1BB");
        var run2 = MakeRun(SuccessLocation("London"));          // firm absent
        var run3 = MakeRun(SuccessLocation("London", firm));    // firm present again

        var diff = Sut.Compare(run3, run2);

        var loc = Assert.Single(diff.Locations);
        Assert.Equal(ComparabilityStatus.Comparable, loc.Comparability);
        Assert.Equal("Returning Law", Assert.Single(loc.NewFirms).FirmName);
        Assert.Empty(loc.AbsentFirms);
    }

    [Fact]
    public void Compare_RunIds_ThreadedThroughToResult()
    {
        var subjectId  = Guid.NewGuid();
        var baselineId = Guid.NewGuid();
        var firm = MakeSolicitor("Some Law", "SW1A1AA");
        var subject  = new StoredRun(subjectId,  DateTimeOffset.UtcNow, "conveyancing", [SuccessLocation("London", firm)]);
        var baseline = new StoredRun(baselineId, DateTimeOffset.UtcNow, "conveyancing", [SuccessLocation("London", firm)]);

        var diff = Sut.Compare(subject, baseline);

        Assert.Equal(subjectId,  diff.SubjectRunId);
        Assert.Equal(baselineId, diff.BaselineRunId);
        Assert.Null(diff.Message);
    }

    [Fact]
    public void Compare_MultipleLocations_LocationsOrderedAlphabetically()
    {
        var firm     = MakeSolicitor("Some Law", "SW1A1AA");
        var subject  = MakeRun(SuccessLocation("Manchester", firm), SuccessLocation("Birmingham", firm));
        var baseline = MakeRun(SuccessLocation("Manchester", firm), SuccessLocation("Birmingham", firm));

        var diff = Sut.Compare(subject, baseline);

        Assert.Equal(2, diff.Locations.Count);
        Assert.Equal("Birmingham", diff.Locations[0].Location);
        Assert.Equal("Manchester", diff.Locations[1].Location);
    }
}

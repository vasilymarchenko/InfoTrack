using System.Reflection;
using InfoTrack.Domain;
using InfoTrack.Infrastructure.Parsing;

namespace InfoTrack.Tests.Infrastructure.Parsing;

/// <summary>
/// End-to-end fixture tests. Fixtures are embedded resources — derive counts from the fixture
/// files, not from assumed knowledge (see Fixtures/README.md for locked baselines).
/// </summary>
public class SolicitorsComConveyancingParserTests
{
    private static readonly SolicitorsComConveyancingParser Parser = new();

    private static string LoadFixture(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"InfoTrack.Tests.Fixtures.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ── London — 75 solicitors, 1 banner (the banner-exclusion regression) ─────

    [Fact]
    public void Parse_LondonFixture_Returns75Solicitors()
    {
        var html = LoadFixture("conveyancing+london.html");
        var results = Parser.Parse(html, "london");
        Assert.Equal(75, results.Count);
    }

    [Fact]
    public void Parse_LondonFixture_SpotCheck_AspenMorrisSolicitors()
    {
        var html = LoadFixture("conveyancing+london.html");
        var results = Parser.Parse(html, "london");

        var firm = results.FirstOrDefault(s => s.FirmName == "Aspen Morris Solicitors");
        Assert.NotNull(firm);
        Assert.Equal("02083707750", firm.Phone);
        Assert.Equal("N14 6BP", firm.Postcode);
        Assert.Equal(112, firm.ReviewCount);
        Assert.NotNull(firm.WebsiteUrl);
        Assert.DoesNotContain("solicitors.com", firm.WebsiteUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_LondonFixture_NoBannerUrlLeaksIntoAnyFirmWebsite()
    {
        var html = LoadFixture("conveyancing+london.html");
        var results = Parser.Parse(html, "london");

        // The London fixture has one banner-block; ensure none of the 75 firms
        // received a URL that originates from outside their own list-item (banner exclusion check).
        foreach (var firm in results.Where(s => s.WebsiteUrl is not null))
        {
            Assert.True(
                Uri.TryCreate(firm.WebsiteUrl, UriKind.Absolute, out var uri),
                $"WebsiteUrl for {firm.FirmName} is not a valid URL: {firm.WebsiteUrl}");
            Assert.DoesNotContain("solicitors.com", uri!.Host, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Bradford — 23 solicitors ───────────────────────────────────────────────

    [Fact]
    public void Parse_BradfordFixture_Returns23Solicitors()
    {
        var html = LoadFixture("conveyancing+bradford.html");
        var results = Parser.Parse(html, "bradford");
        Assert.Equal(23, results.Count);
    }

    [Fact]
    public void Parse_BradfordFixture_MajorityHaveBDPostcodes()
    {
        // Bradford search returns local firms; most have BD postcodes but some may have
        // nearby-city addresses (e.g. a Leeds-based firm listed for Bradford coverage).
        var html = LoadFixture("conveyancing+bradford.html");
        var results = Parser.Parse(html, "bradford");

        var withPostcode = results.Where(s => s.Postcode is not null).ToList();
        Assert.NotEmpty(withPostcode);

        var bdCount = withPostcode.Count(s => s.Postcode!.StartsWith("BD", StringComparison.OrdinalIgnoreCase));
        Assert.True(bdCount >= withPostcode.Count / 2,
            $"Expected majority BD postcodes; got {bdCount} BD out of {withPostcode.Count} with postcodes");
    }

    [Fact]
    public void Parse_BradfordFixture_TierBreakdownIs4Featured19Standard()
    {
        var html = LoadFixture("conveyancing+bradford.html");
        var results = Parser.Parse(html, "bradford");

        Assert.Equal(4,  results.Count(s => s.Tier == ListingTier.Featured));
        Assert.Equal(19, results.Count(s => s.Tier == ListingTier.Standard));
    }

    [Fact]
    public void Parse_BradfordFixture_SpotCheck_MorrishIsFeaturedWithLogo()
    {
        var html = LoadFixture("conveyancing+bradford.html");
        var results = Parser.Parse(html, "bradford");

        var firm = results.FirstOrDefault(s => s.FirmName.Contains("Morrish"));
        Assert.NotNull(firm);
        Assert.Equal(ListingTier.Featured, firm.Tier);
        Assert.NotNull(firm.LogoUrl);
        Assert.NotNull(firm.EnquiryUrl);
    }

    [Fact]
    public void Parse_BradfordFixture_SpotCheck_SchofieldSweeneyIsStandard()
    {
        var html = LoadFixture("conveyancing+bradford.html");
        var results = Parser.Parse(html, "bradford");

        var firm = results.FirstOrDefault(s => s.FirmName.Contains("Schofield"));
        Assert.NotNull(firm);
        Assert.Equal(ListingTier.Standard, firm.Tier);
        Assert.Null(firm.LogoUrl);
        Assert.Null(firm.EnquiryUrl);
    }

    // ── 404 body — empty list, no exception ───────────────────────────────────

    [Fact]
    public void Parse_NotFoundFixture_ReturnsEmptyListWithoutThrowing()
    {
        var html = LoadFixture("conveyancing+notfound-404.html");
        var ex = Record.Exception(() =>
        {
            var results = Parser.Parse(html, "zzznowhere");
            Assert.Empty(results);
        });
        Assert.Null(ex);
    }

    // ── Guard ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NullOrEmptyHtml_ReturnsEmptyList(string? html)
    {
        var results = Parser.Parse(html!, "anywhere");
        Assert.Empty(results);
    }
}

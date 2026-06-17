using InfoTrack.Domain;
using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class ResultItemParserTests
{
    private static readonly DateTimeOffset ScrapedAt = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    // A realistic featured block shaped like the London fixture (Aspen Morris style).
    private const string FullBlock = """
        <div class="result-item">
            <span class="h2">Aspen Morris Solicitors<span class="rev-results"><div class="star-full"></div> (112)</span></span>
            <a rel="noindex" href="tel:02083707750">0208 370 7750</a>
            <div class="logo-holder mobile-hidden" id="SiD63429PiD0">
                <a href="#"><img src="/logos/aspen-morris-solicitors.jpg" alt="Aspen Morris Solicitors"></a>
            </div>
            <a href="/aspen-morris-solicitors.html" class="link-map">
                <address>141 High Street, Southgate, London N14 6BP</address>
            </a>
            <p>Aspen Morris Solicitors provide Conveyancing legal solutions in London.</p>
            <ul class="list-item">
                <li><a target="_blank" href="http://www.aspenmorris.com/" rel="nofollow">Website</a></li>
                <li><a href="/enquiry-form.asp?SiD=63429&amp;DiD=1">Email</a></li>
            </ul>
        </div>
        """;

    // Compact block shaped like the Bradford fixture (Schofield Sweeney style).
    private const string SmallBlock = """
        <div class="result-item item-small">
            <span class="h2">Schofield Sweeney<span class="rev-results">&nbsp;(40)</span></span>
            <a href="/schofield-sweeney.html" class="link-map"><address>Church Bank House, Church Bank, Bradford, Yorkshire BD1 4DY</address></a>
            <a class="tel" style="padding:0px 0px 0px 20px;" rel="noindex" href="tel:01274350800">01274 350 800</a>
        </div>
        """;

    // Minimal block: only a name, all optionals absent
    private const string NameOnlyBlock = """
        <div class="result-item">
            <span class="h2">Minimal Firm</span>
        </div>
        """;

    // Block with no name — should return null
    private const string NoNameBlock = """
        <div class="result-item">
            <address>10 Main St, London E1 1AA</address>
        </div>
        """;

    // ── Full block — all fields ────────────────────────────────────────────────

    [Fact]
    public void Parse_FullBlock_ExtractsAllFieldsAndTierIsFeatured()
    {
        var result = ResultItemParser.Parse(FullBlock, "london", ScrapedAt);

        Assert.NotNull(result);
        Assert.Equal("Aspen Morris Solicitors", result.FirmName);
        Assert.Equal("02083707750", result.Phone);
        Assert.Equal("N14 6BP", result.Postcode);
        // Address: "141 High Street, Southgate, London N14 6BP"
        // Town = segment before the postcode-containing segment = "Southgate"
        Assert.Equal("Southgate", result.Town);
        Assert.Equal(112, result.ReviewCount);
        Assert.Equal("http://www.aspenmorris.com/", result.WebsiteUrl);
        Assert.DoesNotContain("solicitors.com", result.WebsiteUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.EnquiryUrl);
        Assert.StartsWith("https://www.solicitors.com/enquiry-form.asp", result.EnquiryUrl);
        Assert.Equal("https://www.solicitors.com/aspen-morris-solicitors.html", result.ProfileUrl);
        Assert.Equal("https://www.solicitors.com/logos/aspen-morris-solicitors.jpg", result.LogoUrl);
        Assert.Contains("Aspen Morris Solicitors", result.Description);
        Assert.Equal("london", result.SearchedLocation);
        Assert.Equal(ScrapedAt, result.ScrapedAtUtc);
        Assert.Equal(ListingTier.Featured, result.Tier);
    }

    // ── Small block ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_SmallBlock_TierIsStandardAndCoreFieldsExtracted()
    {
        var result = ResultItemParser.Parse(SmallBlock, "bradford", ScrapedAt);

        Assert.NotNull(result);
        Assert.Equal("Schofield Sweeney", result.FirmName);
        Assert.Equal(ListingTier.Standard, result.Tier);
        Assert.Equal("01274350800", result.Phone);
        Assert.Equal("BD1 4DY", result.Postcode);
        Assert.Equal(40, result.ReviewCount);
        Assert.Equal("https://www.solicitors.com/schofield-sweeney.html", result.ProfileUrl);
        Assert.Null(result.LogoUrl);
        Assert.Null(result.WebsiteUrl);
        Assert.Null(result.EnquiryUrl);
    }

    // ── Name-only block ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_NameOnlyBlock_ReturnsSolicitorWithNullOptionals()
    {
        var result = ResultItemParser.Parse(NameOnlyBlock, "london", ScrapedAt);
        Assert.NotNull(result);
        Assert.Equal("Minimal Firm", result.FirmName);
        Assert.Null(result.Phone);
        Assert.Null(result.Postcode);
        Assert.Null(result.Town);
        Assert.Null(result.ReviewCount);
        Assert.Null(result.WebsiteUrl);
        Assert.Null(result.EnquiryUrl);
        Assert.Null(result.ProfileUrl);
        Assert.Null(result.LogoUrl);
        Assert.Null(result.Description);
        Assert.Equal(string.Empty, result.Address);
        Assert.Equal(ListingTier.Featured, result.Tier);
    }

    // ── No-name block ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(NoNameBlock)]
    [InlineData("")]
    public void Parse_BlockWithoutFirmName_ReturnsNull(string block) =>
        Assert.Null(ResultItemParser.Parse(block, "london", ScrapedAt));
}

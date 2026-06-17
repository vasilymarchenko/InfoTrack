using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class ResultSectionLocatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Locate_NullOrEmptyHtml_ReturnsEmpty(string? html) =>
        Assert.Equal(string.Empty, ResultSectionLocator.Locate(html!));

    [Fact]
    public void Locate_NoResultSection_ReturnsEmpty()
    {
        const string html = "<html><body><p>Page not found.</p></body></html>";
        Assert.Equal(string.Empty, ResultSectionLocator.Locate(html));
    }

    [Fact]
    public void Locate_ResultSectionWithSidebar_IncludesOnlyResultRegion()
    {
        const string html = """
            <div class="main">
                <div class="result-section"><div class="result-item">Firm A</div></div>
                <div class="sidebar">sidebar content</div>
            </div>
            """;

        var result = ResultSectionLocator.Locate(html);

        Assert.Contains("result-section", result);
        Assert.Contains("Firm A", result);
        Assert.DoesNotContain("sidebar content", result);
    }

    [Fact]
    public void Locate_ResultSectionWithoutSidebar_ReturnsToEndOfString()
    {
        const string html = """<div class="result-section"><div class="result-item">Firm A</div></div>""";
        var result = ResultSectionLocator.Locate(html);
        Assert.Contains("Firm A", result);
    }

    [Fact]
    public void Locate_HtmlComments_AreStripped()
    {
        const string html = """
            <div class="result-section">
                <!-- <div class="fake-div"> a stray div in a comment </div> -->
                <div class="result-item">Real Firm</div>
            </div>
            <div class="sidebar"></div>
            """;

        var result = ResultSectionLocator.Locate(html);

        Assert.Contains("Real Firm", result);
        Assert.DoesNotContain("fake-div", result);
        Assert.DoesNotContain("<!--", result);
    }

    [Fact]
    public void Locate_404Body_ReturnsEmpty()
    {
        const string html = """Page not found. Please visit our home page at <a href="http://www.solicitors.com">www.solicitors.com</a>""";
        Assert.Equal(string.Empty, ResultSectionLocator.Locate(html));
    }
}

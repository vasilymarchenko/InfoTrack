using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class HtmlScannerTests
{
    // ── Attr ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Attr_AttributePresent_ReturnsValue()
    {
        const string block = """<a href="/foo.html" class="link-map">text</a>""";
        Assert.Equal("/foo.html", HtmlScanner.Attr(block, "a", "link-map", "href"));
    }

    [Fact]
    public void Attr_NoCls_MatchesAnyTagOfThatType()
    {
        const string block = """<img src="/logos/firm.jpg" alt="firm">""";
        Assert.Equal("/logos/firm.jpg", HtmlScanner.Attr(block, "img", null, "src"));
    }

    public static IEnumerable<object?[]> AttrMatchNotFoundCases() =>
    [
        ["<p>No anchor here</p>", "a", null, "href"],
        ["""<a href="/foo.html" class="other-class">text</a>""", "a", "link-map", "href"],
        ["""<a class="link-map">no href</a>""", "a", "link-map", "href"],
    ];

    [Theory, MemberData(nameof(AttrMatchNotFoundCases))]
    public void Attr_MatchNotFound_ReturnsNull(string block, string tag, string? cls, string attribute) =>
        Assert.Null(HtmlScanner.Attr(block, tag, cls, attribute));

    [Fact]
    public void Attr_ClassContainsMatch_WorksForCompoundClasses()
    {
        const string block = """<div class="result-item item-small"><span>text</span></div>""";
        Assert.NotNull(HtmlScanner.Attr(block, "div", "result-item", "class"));
    }

    // ── LeadingText ─────────────────────────────────────────────────────────────

    [Fact]
    public void LeadingText_TextBeforeChildTag_ReturnsText()
    {
        const string block = """<span class="h2">Firm Name<span class="rev-results">(5)</span></span>""";
        Assert.Equal("Firm Name", HtmlScanner.LeadingText(block, "span", "h2"));
    }

    public static IEnumerable<object[]> LeadingTextNotFoundCases() =>
    [
        ["""<span class="h2"><span class="rev-results">(5)</span></span>""", "span", "h2"],
        ["<p>hello</p>", "span", "h2"],
        ["""<span class="h2">   <span>child</span></span>""", "span", "h2"],
    ];

    [Theory, MemberData(nameof(LeadingTextNotFoundCases))]
    public void LeadingText_MatchNotFound_ReturnsNull(string block, string tag, string cls) =>
        Assert.Null(HtmlScanner.LeadingText(block, tag, cls));

    // ── InnerText ───────────────────────────────────────────────────────────────

    [Fact]
    public void InnerText_PlainContent_ReturnsDecoded()
    {
        const string block = "<address>10 Main St, London E1 1AA</address>";
        Assert.Equal("10 Main St, London E1 1AA", HtmlScanner.InnerText(block, "address", null));
    }

    [Fact]
    public void InnerText_WithChildMarkup_ReturnsRawInner()
    {
        const string block = """<p>Some <strong>bold</strong> text.</p>""";
        Assert.Equal("Some <strong>bold</strong> text.", HtmlScanner.InnerText(block, "p", null));
    }

    public static IEnumerable<object?[]> InnerTextNotFoundCases() =>
    [
        ["<div>no address</div>", "address", null],
        ["<address>10 Main St", "address", null],
        ["""<span class="other">(330)</span>""", "span", "rev-results"],
    ];

    [Theory, MemberData(nameof(InnerTextNotFoundCases))]
    public void InnerText_MatchNotFound_ReturnsNull(string block, string tag, string? cls) =>
        Assert.Null(HtmlScanner.InnerText(block, tag, cls));

    [Fact]
    public void InnerText_WithClass_OnlyMatchesWhenClassPresent()
    {
        const string block = """<span class="rev-results">&nbsp;(330)</span>""";
        Assert.Equal("&nbsp;(330)", HtmlScanner.InnerText(block, "span", "rev-results"));
    }

    // ── Section ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Section_MatchingContainer_ReturnsInnerHtml()
    {
        const string block = """
            <ul class="list-item">
                <li><a href="/enquiry-form.asp?SiD=1">Email</a></li>
            </ul>
            """;
        var result = HtmlScanner.Section(block, "ul", "list-item");
        Assert.NotNull(result);
        Assert.Contains("enquiry-form.asp", result);
        Assert.DoesNotContain("<ul", result);
    }

    [Fact]
    public void Section_ContainerAbsent_ReturnsNull()
    {
        const string block = "<div><p>no list</p></div>";
        Assert.Null(HtmlScanner.Section(block, "ul", "list-item"));
    }

    [Fact]
    public void Section_IsolatesCorrectContainer_WhenMultiplePresent()
    {
        const string block = """
            <div class="logo-holder"><img src="/logos/firm.jpg"></div>
            <ul class="list-item"><li>Website</li></ul>
            """;
        var logoSection = HtmlScanner.Section(block, "div", "logo-holder");
        Assert.NotNull(logoSection);
        Assert.Contains("firm.jpg", logoSection);
        Assert.DoesNotContain("list-item", logoSection);
    }
}

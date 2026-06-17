using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class HtmlTextTests
{
    // ── Decode ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t\n  ")]
    public void Decode_NullEmptyOrWhitespace_ReturnsNull(string? value) =>
        Assert.Null(HtmlText.Decode(value));

    [Fact]
    public void Decode_PlainText_ReturnsTrimmed() =>
        Assert.Equal("hello", HtmlText.Decode("  hello  "));

    [Fact]
    public void Decode_HtmlEntities_DecodesCorrectly() =>
        Assert.Equal("A & B", HtmlText.Decode("A &amp; B"));

    [Fact]
    public void Decode_NonBreakingSpace_CollapsedToSpace() =>
        Assert.Equal("141 High Street", HtmlText.Decode("141&nbsp;High Street"));

    [Fact]
    public void Decode_MultipleSpaces_CollapsedToOne() =>
        Assert.Equal("a b c", HtmlText.Decode("a   b  c"));

    [Fact]
    public void Decode_NewlinesAndTabs_CollapsedToSpace() =>
        Assert.Equal("line one line two", HtmlText.Decode("line one\nline two"));

    // ── Absolutise ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Absolutise_NullOrEmptyInput_ReturnsNull(string? path) =>
        Assert.Null(HtmlText.Absolutise(path));

    [Fact]
    public void Absolutise_RelativePath_PrefixesSiteBase() =>
        Assert.Equal("https://www.solicitors.com/foo.html", HtmlText.Absolutise("/foo.html"));

    [Theory]
    [InlineData("https://www.example.com/")]
    [InlineData("http://www.example.com/")]
    public void Absolutise_AbsoluteUrl_PassedThrough(string url) =>
        Assert.Equal(url, HtmlText.Absolutise(url));

    [Fact]
    public void Absolutise_RelativePathWithEntities_DecodesAndPrefixes() =>
        Assert.Equal("https://www.solicitors.com/a-firm.html", HtmlText.Absolutise("/a-firm.html"));
}

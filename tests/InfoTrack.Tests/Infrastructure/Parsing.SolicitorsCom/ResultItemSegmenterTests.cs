using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class ResultItemSegmenterTests
{
    // ── Banner-exclusion regression (the bug this component fixes) ─────────────

    [Fact]
    public void Segment_BannerBetweenTwoItems_NeitherBlockContainsBanner()
    {
        const string region = """
            <div class="result-section">
                <div class="result-item"><span class="h2">Firm A</span></div>
                <div class="banner-block"><a href="http://banner.example.com">Ad</a></div>
                <div class="result-item"><span class="h2">Firm B</span></div>
            </div>
            """;

        var blocks = ResultItemSegmenter.Segment(region).ToList();

        Assert.Equal(2, blocks.Count);
        Assert.DoesNotContain("banner.example.com", blocks[0]);
        Assert.DoesNotContain("banner.example.com", blocks[1]);
        Assert.DoesNotContain("banner-block", blocks[0]);
        Assert.DoesNotContain("banner-block", blocks[1]);
    }

    [Fact]
    public void Segment_BannerBetweenItems_EachBlockContainsOnlyItsOwnFirm()
    {
        const string region = """
            <div class="result-section">
                <div class="result-item"><span class="h2">Firm A</span></div>
                <div class="banner-block"><span>Banner</span></div>
                <div class="result-item"><span class="h2">Firm B</span></div>
            </div>
            """;

        var blocks = ResultItemSegmenter.Segment(region).ToList();

        Assert.Contains("Firm A", blocks[0]);
        Assert.DoesNotContain("Firm B", blocks[0]);
        Assert.Contains("Firm B", blocks[1]);
        Assert.DoesNotContain("Firm A", blocks[1]);
    }

    // ── Basic block yielding ───────────────────────────────────────────────────

    [Fact]
    public void Segment_SingleItem_YieldsOneBlock()
    {
        const string region = """<div class="result-item"><span class="h2">Firm A</span></div>""";
        var blocks = ResultItemSegmenter.Segment(region).ToList();
        Assert.Single(blocks);
        Assert.Contains("Firm A", blocks[0]);
    }

    [Fact]
    public void Segment_MultipleItems_YieldsOneBlockEach()
    {
        const string region = """
            <div class="result-item"><span class="h2">Firm A</span></div>
            <div class="result-item"><span class="h2">Firm B</span></div>
            <div class="result-item"><span class="h2">Firm C</span></div>
            """;

        var blocks = ResultItemSegmenter.Segment(region).ToList();
        Assert.Equal(3, blocks.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("""<div class="sidebar"><p>nothing</p></div>""")]
    public void Segment_NoResultItems_YieldsNoBlocks(string region) =>
        Assert.Empty(ResultItemSegmenter.Segment(region));

    // ── Block content correctness ──────────────────────────────────────────────

    [Fact]
    public void Segment_EachBlock_StartsWithResultItemOpenTag()
    {
        const string region = """
            <div class="result-item"><span class="h2">Firm A</span></div>
            <div class="result-item item-small"><span class="h2">Firm B</span></div>
            """;

        foreach (var block in ResultItemSegmenter.Segment(region))
            Assert.StartsWith("<div", block.TrimStart());
    }

    [Fact]
    public void Segment_SmallItemClass_IsIncluded()
    {
        const string region = """<div class="result-item item-small"><span class="h2">Firm Small</span></div>""";
        var blocks = ResultItemSegmenter.Segment(region).ToList();
        Assert.Single(blocks);
        Assert.Contains("Firm Small", blocks[0]);
    }

    [Fact]
    public void Segment_TrailingItem_BlockEndsCorrectly()
    {
        const string region = """
            <div class="result-item"><span class="h2">Firm A</span></div>
            <div class="result-item"><span class="h2">Firm B</span></div>
            """;

        var blocks = ResultItemSegmenter.Segment(region).ToList();

        // Last block should contain only its firm and end with </div>
        Assert.Contains("Firm B", blocks[1]);
        Assert.DoesNotContain("Firm A", blocks[1]);
    }

    [Fact]
    public void Segment_NestedDivs_AreHandledCorrectly()
    {
        const string region = """
            <div class="result-item">
                <div class="inner"><div class="deep">nested</div></div>
            </div>
            <div class="result-item">
                <span class="h2">Firm B</span>
            </div>
            """;

        var blocks = ResultItemSegmenter.Segment(region).ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Contains("nested", blocks[0]);
        Assert.DoesNotContain("Firm B", blocks[0]);
    }
}

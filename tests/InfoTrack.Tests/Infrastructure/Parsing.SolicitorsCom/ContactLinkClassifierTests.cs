using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Tests.Infrastructure.Parsing.SolicitorsCom;

public class ContactLinkClassifierTests
{
    private const string EnquiryHref = """/enquiry-form.asp?SiD=12345&DiD=1""";

    // ── Website extraction ─────────────────────────────────────────────────────

    [Fact]
    public void Classify_ExternalWebsiteLink_ReturnsWebsiteUrl()
    {
        var slice = $"""<li><a href="http://www.example-firm.com/" target="_blank">Website</a></li>""";
        var (website, _) = ContactLinkClassifier.Classify(slice);
        Assert.Equal("http://www.example-firm.com/", website);
    }

    [Fact]
    public void Classify_SolicitorsComLink_IsRejected()
    {
        var slice = $"""<li><a href="https://www.solicitors.com/firm.html">Profile</a></li>""";
        var (website, _) = ContactLinkClassifier.Classify(slice);
        Assert.Null(website);
    }

    [Fact]
    public void Classify_NoExternalLinks_WebsiteIsNull()
    {
        var slice = $"""<li><a href="{EnquiryHref}">Email</a></li>""";
        var (website, _) = ContactLinkClassifier.Classify(slice);
        Assert.Null(website);
    }

    [Fact]
    public void Classify_FirmWithNoWebsiteLink_WebsiteIsNullNotBannerUrl()
    {
        // This is the key regression: if there is no website anchor in the list-item,
        // WebsiteUrl must be null — never a banner URL from outside this slice.
        var slice = $"""<li><a href="{EnquiryHref}">Email</a></li><li><a href="tel:01234567890">Call</a></li>""";
        var (website, _) = ContactLinkClassifier.Classify(slice);
        Assert.Null(website);
    }

    [Theory]
    [InlineData("http://www.lawfirm.co.uk/")]
    [InlineData("https://www.lawfirm.co.uk/")]
    public void Classify_ExternalWebsite_ReturnsUrl(string url)
    {
        var slice = $"""<li><a href="{url}" target="_blank">Website</a></li>""";
        var (website, _) = ContactLinkClassifier.Classify(slice);
        Assert.Equal(url, website);
    }

    // ── Enquiry extraction ─────────────────────────────────────────────────────

    [Fact]
    public void Classify_EnquiryFormLink_ReturnsAbsoluteEnquiryUrl()
    {
        var slice = $"""<li><a href="{EnquiryHref}">Email</a></li>""";
        var (_, enquiry) = ContactLinkClassifier.Classify(slice);
        Assert.Equal($"https://www.solicitors.com{EnquiryHref}", enquiry);
    }

    [Fact]
    public void Classify_NoEnquiryLink_EnquiryIsNull()
    {
        var slice = """<li><a href="http://www.firm.com/">Website</a></li>""";
        var (_, enquiry) = ContactLinkClassifier.Classify(slice);
        Assert.Null(enquiry);
    }

    // ── Combined ──────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_BothLinksPresent_ReturnsBoth()
    {
        var slice = $"""
            <li><a href="http://www.example-firm.com/" target="_blank">Website</a></li>
            <li><a href="{EnquiryHref}">Email</a></li>
            """;

        var (website, enquiry) = ContactLinkClassifier.Classify(slice);

        Assert.Equal("http://www.example-firm.com/", website);
        Assert.NotNull(enquiry);
        Assert.Contains("enquiry-form.asp", enquiry);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Classify_NullOrEmptySlice_ReturnsBothNull(string? slice)
    {
        var (website, enquiry) = ContactLinkClassifier.Classify(slice);
        Assert.Null(website);
        Assert.Null(enquiry);
    }
}

using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

internal static class ContactLinkClassifier
{
    private const string SiteHost = "solicitors.com";

    private static readonly Regex AnchorHref =
        new(@"href=""(https?://[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EnquiryHref =
        new(@"href=""(/enquiry-form\.asp[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static (string? WebsiteUrl, string? EnquiryUrl) Classify(string? listItemSlice)
    {
        if (string.IsNullOrEmpty(listItemSlice))
            return (null, null);

        string? websiteUrl = null;
        string? enquiryUrl = null;

        // Enquiry link uses a relative href
        var enquiryMatch = EnquiryHref.Match(listItemSlice);
        if (enquiryMatch.Success)
            enquiryUrl = HtmlText.Absolutise(enquiryMatch.Groups[1].Value);

        // Website: first external https?:// link that is not on the site host
        foreach (Match m in AnchorHref.Matches(listItemSlice))
        {
            var href = m.Groups[1].Value;
            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                continue;
            if (uri.Host.Contains(SiteHost, StringComparison.OrdinalIgnoreCase))
                continue;

            websiteUrl = HtmlText.Decode(href);
            break;
        }

        return (websiteUrl, enquiryUrl);
    }
}

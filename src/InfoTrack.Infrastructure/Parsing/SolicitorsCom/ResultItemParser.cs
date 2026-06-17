using System.Text.RegularExpressions;
using InfoTrack.Domain;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

internal static class ResultItemParser
{
    private static readonly Regex ReviewCountPattern =
        new(@"\(\s*([0-9]+)\s*\)", RegexOptions.Compiled);

    private static readonly Regex TelPattern =
        new(@"href=""tel:([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static Solicitor? Parse(string block, string searchedLocation, DateTimeOffset scrapedAt)
    {
        var rawName = HtmlScanner.LeadingText(block, "span", "h2");
        var name = HtmlText.Decode(rawName);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var rawAddress = HtmlText.Decode(
            HtmlScanner.InnerText(block, "address", null));

        var (town, postcode) = AddressParser.Parse(rawAddress);

        var profileUrl = HtmlText.Absolutise(
            HtmlScanner.Attr(block, "a", "link-map", "href"));

        var phone = ExtractPhone(block);

        var description = HtmlText.Decode(
            HtmlScanner.InnerText(block, "p", null));

        var reviewCount = ExtractReviewCount(block);

        var logoSection = HtmlScanner.Section(block, "div", "logo-holder");
        var logoUrl = HtmlText.Absolutise(
            HtmlScanner.Attr(logoSection ?? string.Empty, "img", null, "src"));

        var listItemSlice = HtmlScanner.Section(block, "ul", "list-item");
        var (websiteUrl, enquiryUrl) = ContactLinkClassifier.Classify(listItemSlice);

        var tier = DetectTier(block);

        return new Solicitor(
            FirmName: name,
            SearchedLocation: searchedLocation,
            Address: rawAddress ?? string.Empty,
            Town: town,
            Postcode: postcode,
            Phone: phone,
            WebsiteUrl: websiteUrl,
            EnquiryUrl: enquiryUrl,
            ProfileUrl: profileUrl,
            ReviewCount: reviewCount,
            Description: description,
            LogoUrl: logoUrl,
            Tier: tier,
            ScrapedAtUtc: scrapedAt);
    }

    // item-small is only in the opening tag's class attribute, so we look before the first '>'.
    private static ListingTier DetectTier(string block)
    {
        var tagEnd = block.IndexOf('>');
        return tagEnd > 0 && block[..tagEnd].Contains("item-small", StringComparison.OrdinalIgnoreCase)
            ? ListingTier.Standard
            : ListingTier.Featured;
    }

    private static string? ExtractPhone(string block)
    {
        var m = TelPattern.Match(block);
        return m.Success ? HtmlText.Decode(m.Groups[1].Value) : null;
    }

    private static int? ExtractReviewCount(string block)
    {
        var raw = HtmlScanner.InnerText(block, "span", "rev-results");
        if (raw is null)
            return null;

        var m = ReviewCountPattern.Match(raw);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }
}

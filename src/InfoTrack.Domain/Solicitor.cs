namespace InfoTrack.Domain;

public enum ListingTier
{
    /// <summary>Full card with logo, enquiry link and website link (<c>result-item</c>).</summary>
    Featured,
    /// <summary>Compact row with phone only (<c>result-item item-small</c>).</summary>
    Standard,
}

public record Solicitor(
    string FirmName,
    string SearchedLocation,
    string Address,
    string? Town,
    string? Postcode,
    string? Phone,
    string? WebsiteUrl,
    string? EnquiryUrl,
    string? ProfileUrl,
    int? ReviewCount,
    string? Description,
    string? LogoUrl,
    ListingTier Tier,
    DateTimeOffset ScrapedAtUtc);

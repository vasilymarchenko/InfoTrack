using InfoTrack.Domain;

namespace InfoTrack.Infrastructure.Persistence;

internal static class FirmMapper
{
    internal static Solicitor ToSolicitor(SightingEntity s, string location, DateTimeOffset scrapedAt) => new(
        FirmName: s.Firm.FirmName,
        SearchedLocation: location,
        Address: s.Firm.Address,
        Town: s.Firm.Town,
        Postcode: s.Firm.Postcode,
        Phone: s.Firm.Phone,
        WebsiteUrl: s.Firm.WebsiteUrl,
        EnquiryUrl: s.Firm.EnquiryUrl,
        ProfileUrl: s.Firm.ProfileUrl,
        ReviewCount: s.ReviewCount,
        Description: s.Firm.Description,
        LogoUrl: s.Firm.LogoUrl,
        Tier: ListingTier.Featured,   // Tier is not persisted; default to Featured on read-back
        ScrapedAtUtc: scrapedAt);
}

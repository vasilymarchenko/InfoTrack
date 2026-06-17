namespace InfoTrack.Domain;

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
    DateTimeOffset ScrapedAtUtc);

using InfoTrack.Domain;

namespace InfoTrack.Infrastructure.Persistence;

internal sealed class SearchRunEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset RunAtUtc { get; set; }
    public string AreaOfLaw { get; set; } = "";
    public List<string> RequestedLocations { get; set; } = [];
    public int TotalLocations { get; set; }
    public int TotalUniqueFirms { get; set; }
    public List<LocationOutcomeEntity> Locations { get; set; } = [];
}

internal sealed class LocationOutcomeEntity
{
    public Guid Id { get; set; }
    public Guid SearchRunId { get; set; }
    public string Location { get; set; } = "";
    public string? RequestedUrl { get; set; }
    public LocationOutcomeStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public SearchRunEntity SearchRun { get; set; } = null!;
    public List<SightingEntity> Sightings { get; set; } = [];
}

// One row per firm identity. Attributes reflect the latest observed values.
// FirstSeenAt is immutable after insert; LastSeenAt advances on every save that includes this firm.
internal sealed class FirmEntity
{
    public Guid Id { get; set; }
    public string IdentityKey { get; set; } = "";   // FirmIdentity.BranchKey — unique index
    public string FirmName { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public string? Phone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? EnquiryUrl { get; set; }
    public string? ProfileUrl { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public List<SightingEntity> Sightings { get; set; } = [];
}

// One row per (run × location × firm). ReviewCount and Tier are captured per sighting
// because they drive ranking and legitimately drift between runs.
internal sealed class SightingEntity
{
    public Guid Id { get; set; }
    public Guid LocationOutcomeId { get; set; }
    public Guid FirmId { get; set; }
    public int? ReviewCount { get; set; }
    public ListingTier Tier { get; set; }
    public LocationOutcomeEntity LocationOutcome { get; set; } = null!;
    public FirmEntity Firm { get; set; } = null!;
}

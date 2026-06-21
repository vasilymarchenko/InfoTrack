using InfoTrack.Domain;

namespace InfoTrack.Application.DTOs;

public enum ChangeConfidence { Provisional, Confirmed }

/// <summary>Extends the MVP enum with NoBaseline for the per-location default view.</summary>
public enum ComparabilityStatus { Comparable, NotRequested, ScrapeFailed, NoBaseline }

public sealed record ChangedFirm(Solicitor Firm, ChangeConfidence Confidence);

/// <summary>Per-location change result from the FULL per-location-baseline view.</summary>
public sealed record LocationChange(
    string Location,
    ComparabilityStatus Comparability,
    Guid? BaselineRunId,
    IReadOnlyList<ChangedFirm> NewFirms,
    IReadOnlyList<ChangedFirm> AbsentFirms);

public sealed record ChangeView(
    Guid SubjectRunId,
    DateTimeOffset SubjectRunAtUtc,
    IReadOnlyList<LocationChange> Locations);

// --- Current-firms projection ---

public enum FirmStatus { Active, ProvisionallyAbsent, ConfirmedGone }

public sealed record FirmLocationState(string Location, FirmStatus Status, DateTimeOffset LastSeenAt);

public sealed record CurrentFirm(
    Guid FirmId,
    Solicitor Latest,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    FirmStatus RollupStatus,
    IReadOnlyList<FirmLocationState> Locations);

// --- Review trend ---

public sealed record ReviewPoint(DateTimeOffset RunAtUtc, string Location, int? ReviewCount);

public enum TrendDirection { Rising, Steady, Falling, Unknown }

public sealed record FirmHistory(
    Guid FirmId,
    IReadOnlyList<ReviewPoint> Points,
    TrendDirection OverallReviewTrend);

// --- Supporting records used by the repository and confirmer ---

/// <summary>The firms seen in one successful run of a specific location, keyed by FirmIdentity.BranchKey.</summary>
public sealed record LocationRunSightings(
    Guid RunId,
    DateTimeOffset RunAtUtc,
    IReadOnlyDictionary<string, Solicitor> FirmsByKey);

/// <summary>The most-recently-seen state of a firm within a location, used by the projector.</summary>
public sealed record LocationFirmLastSeen(
    string IdentityKey,
    Guid FirmId,
    Solicitor Latest,
    DateTimeOffset LastSeenAt,
    DateTimeOffset FirstSeenAt);

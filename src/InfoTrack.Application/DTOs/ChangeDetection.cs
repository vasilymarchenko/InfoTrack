using InfoTrack.Domain;

namespace InfoTrack.Application.DTOs;

public enum ChangeConfidence { Provisional, Confirmed }

public enum ComparabilityStatus { Comparable, NotRequested, ScrapeFailed, NoBaseline }

public sealed record ChangedFirm(Solicitor Firm, ChangeConfidence Confidence);

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

public enum FirmStatus { Active, ProvisionallyAbsent, ConfirmedGone }

public sealed record FirmLocationState(string Location, FirmStatus Status, DateTimeOffset LastSeenAt);

public sealed record CurrentFirm(
    Guid FirmId,
    Solicitor Latest,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    FirmStatus RollupStatus,
    IReadOnlyList<FirmLocationState> Locations);

public sealed record ReviewPoint(DateTimeOffset RunAtUtc, string Location, int? ReviewCount);

public enum TrendDirection { Rising, Steady, Falling, Unknown }

public sealed record FirmHistory(
    Guid FirmId,
    IReadOnlyList<ReviewPoint> Points,
    TrendDirection OverallReviewTrend);

public sealed record LocationRunSightings(
    Guid RunId,
    DateTimeOffset RunAtUtc,
    IReadOnlyDictionary<string, Solicitor> FirmsByKey);

public sealed record LocationFirmLastSeen(
    string IdentityKey,
    Guid FirmId,
    Solicitor Latest,
    DateTimeOffset LastSeenAt,
    DateTimeOffset FirstSeenAt);

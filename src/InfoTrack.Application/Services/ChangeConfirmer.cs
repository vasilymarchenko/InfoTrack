using InfoTrack.Application.DTOs;

namespace InfoTrack.Application.Services;

/// <summary>
/// Pure, stateless service that assigns Provisional/Confirmed confidence to a reported change.
/// Takes already-fetched sighting windows — does no I/O. Register as singleton.
/// </summary>
public sealed class ChangeConfirmer
{
    /// <summary>
    /// Confidence for a firm that is absent from the subject run.
    /// Confirmed requires the firm to be missing from ALL K most recent successful runs
    /// (the "gone window" = recentSets[0..K-1], where recentSets[0] is the subject run).
    /// Any reappearance in the window → Provisional (flicker never confirms a disappearance).
    /// </summary>
    public ChangeConfidence ConfidenceForAbsent(
        string identityKey,
        IReadOnlyList<LocationRunSightings> recentSets,
        int k)
    {
        // Not enough distance yet.
        if (recentSets.Count < k)
            return ChangeConfidence.Provisional;

        // "gone window" = the K most recent successful runs including the subject run.
        for (var i = 0; i < k; i++)
            if (recentSets[i].FirmsByKey.ContainsKey(identityKey))
                return ChangeConfidence.Provisional;

        return ChangeConfidence.Confirmed;
    }

    /// <summary>
    /// Confidence for a firm that is new in the subject run.
    /// Confirmed requires the firm to be absent from ALL K successful runs BEFORE the subject run
    /// (the "clean prior window" = recentSets[1..K]).
    /// If the firm appeared in any prior run it was under-sampled, not genuinely new → Provisional.
    /// </summary>
    public ChangeConfidence ConfidenceForNew(
        string identityKey,
        IReadOnlyList<LocationRunSightings> recentSets,
        int k)
    {
        // Need at least K+1 entries (subject + K prior).
        if (recentSets.Count < k + 1)
            return ChangeConfidence.Provisional;

        // "clean prior window" = K runs before the subject (indices 1..K, newest first).
        for (var i = 1; i <= k; i++)
            if (recentSets[i].FirmsByKey.ContainsKey(identityKey))
                return ChangeConfidence.Provisional;

        return ChangeConfidence.Confirmed;
    }
}

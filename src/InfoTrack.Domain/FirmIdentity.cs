namespace InfoTrack.Domain;

public static class FirmIdentity
{
    /// <summary>Branch-level key: normalised name + postcode (fallback phone). Used for de-dup, persistence, and diffing.</summary>
    public static string BranchKey(Solicitor s)
    {
        var name = NormaliseName(s.FirmName);
        var postcode = NormalisePostcode(s.Postcode);
        var discriminator = postcode != "" ? postcode : NormalisePhone(s.Phone);
        return $"{name}|{discriminator}";
    }

    /// <summary>Name-only key used by multi-location grouping.</summary>
    public static string NameKey(Solicitor s) => NormaliseName(s.FirmName);

    // trim, lower-invariant, collapse internal whitespace
    public static string NormaliseName(string firmName) =>
        string.Join(" ", firmName.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // upper-invariant, remove all whitespace; null/empty -> ""
    public static string NormalisePostcode(string? p)
    {
        if (string.IsNullOrEmpty(p)) return "";
        return new string(p.ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    // digits only; null/empty -> ""
    public static string NormalisePhone(string? p)
    {
        if (string.IsNullOrEmpty(p)) return "";
        return new string(p.Where(char.IsDigit).ToArray());
    }
}

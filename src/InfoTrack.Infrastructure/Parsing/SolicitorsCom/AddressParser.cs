using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

internal static class AddressParser
{
    private static readonly Regex UkPostcode =
        new(@"\b([A-Z]{1,2}[0-9][A-Z0-9]?\s*[0-9][A-Z]{2})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NormaliseSpace = new(@"\s+", RegexOptions.Compiled);

    internal static (string? Town, string? Postcode) Parse(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return (null, null);

        var postcode = ExtractPostcode(address);
        var town = ExtractTown(address, postcode);
        return (town, postcode);
    }

    private static string? ExtractPostcode(string address)
    {
        var m = UkPostcode.Match(address);
        if (!m.Success)
            return null;

        return NormaliseSpace.Replace(m.Groups[1].Value, " ").ToUpperInvariant();
    }

    private static string? ExtractTown(string address, string? postcode)
    {
        var segments = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return null;

        var postcodeIdx = Array.FindIndex(segments, s => UkPostcode.IsMatch(s));
        if (postcodeIdx >= 1)
            return segments[postcodeIdx - 1];

        // Fallback: strip the postcode from the last segment and use what remains
        var last = postcode is null
            ? segments[^1]
            : UkPostcode.Replace(segments[^1], string.Empty).Trim();

        return string.IsNullOrWhiteSpace(last) ? null : last;
    }
}

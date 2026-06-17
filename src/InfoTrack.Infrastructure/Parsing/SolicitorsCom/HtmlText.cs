using System.Net;
using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

internal static class HtmlText
{
    private const string SiteBase = "https://www.solicitors.com";

    private static readonly Regex CollapseWs = new(@"\s+", RegexOptions.Compiled);

    internal static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        var decoded = CollapseWs.Replace(WebUtility.HtmlDecode(value), " ").Trim();
        return decoded.Length == 0 ? null : decoded;
    }

    internal static string? Absolutise(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var decoded = Decode(path);
        if (decoded is null)
            return null;

        return decoded.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? decoded
            : SiteBase + decoded;
    }
}

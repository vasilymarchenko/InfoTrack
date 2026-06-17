using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

// Yields depth-balanced result-item div blocks; sibling banner-block divs are excluded.
internal static class ResultItemSegmenter
{
    private static readonly Regex ResultItem =
        new(@"<div\s[^>]*class=""result-item", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static IEnumerable<string> Segment(string region)
    {
        if (string.IsNullOrEmpty(region))
            yield break;

        foreach (Match item in ResultItem.Matches(region))
        {
            var blockEnd = HtmlScanner.FindDivBlockEnd(region, item.Index);
            if (blockEnd < 0)
                continue;

            yield return region[item.Index..blockEnd];
        }
    }
}

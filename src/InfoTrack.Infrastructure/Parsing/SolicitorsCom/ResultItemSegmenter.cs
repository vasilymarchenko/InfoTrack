using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

// Yields depth-balanced result-item div blocks; sibling banner-block divs are excluded.
internal static class ResultItemSegmenter
{
    private static readonly Regex ResultItem =
        new(@"<div\s[^>]*class=""result-item", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenDiv  = new(@"<div[\s>]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CloseDiv = new(@"</div>",    RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static IEnumerable<string> Segment(string region)
    {
        if (string.IsNullOrEmpty(region))
            yield break;

        var itemMatches = ResultItem.Matches(region);

        foreach (Match item in itemMatches)
        {
            var blockEnd = FindBlockEnd(region, item.Index);
            if (blockEnd < 0)
                continue;

            yield return region[item.Index..blockEnd];
        }
    }

    private static int FindBlockEnd(string region, int start)
    {
        var depth = 0;
        var pos = start;

        while (pos < region.Length)
        {
            var nextOpen  = OpenDiv.Match(region, pos);
            var nextClose = CloseDiv.Match(region, pos);

            if (!nextOpen.Success && !nextClose.Success)
                break;

            int openIdx  = nextOpen.Success  ? nextOpen.Index  : int.MaxValue;
            int closeIdx = nextClose.Success ? nextClose.Index : int.MaxValue;

            if (openIdx < closeIdx)
            {
                depth++;
                pos = nextOpen.Index + nextOpen.Length;
            }
            else
            {
                depth--;
                pos = nextClose.Index + nextClose.Length;

                if (depth == 0)
                    return pos;
            }
        }

        return -1;
    }
}

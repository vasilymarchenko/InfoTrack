using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

// Narrows page HTML to the div.result-section block using depth-balanced div tracking.
// Comments are stripped before depth-counting so stray <div> inside them cannot skew the counter.
internal static class ResultSectionLocator
{
    private static readonly RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

    private static readonly Regex ResultSection = new(@"<div\s[^>]*class=""result-section""", Opts);
    private static readonly Regex HtmlComment   = new(@"<!--.*?-->", Opts);

    internal static string Locate(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var cleaned = HtmlComment.Replace(html, string.Empty);

        var sectionMatch = ResultSection.Match(cleaned);
        if (!sectionMatch.Success)
            return string.Empty;

        var blockEnd = HtmlScanner.FindDivBlockEnd(cleaned, sectionMatch.Index);
        return blockEnd < 0 ? string.Empty : cleaned[sectionMatch.Index..blockEnd];
    }
}

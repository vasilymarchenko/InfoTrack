using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

// Narrows page HTML to div.result-section .. div.sidebar. Strips HTML comments so stray
// div tags inside comments cannot skew the depth counter in ResultItemSegmenter.
internal static class ResultSectionLocator
{
    private static readonly RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

    private static readonly Regex ResultSection = new(@"class=""result-section""", Opts);
    private static readonly Regex Sidebar       = new(@"class=""sidebar""", Opts);
    private static readonly Regex HtmlComment   = new(@"<!--.*?-->", Opts);

    internal static string Locate(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var sectionMatch = ResultSection.Match(html);
        if (!sectionMatch.Success)
            return string.Empty;

        var sidebarMatch = Sidebar.Match(html, sectionMatch.Index);
        var end = sidebarMatch.Success ? sidebarMatch.Index : html.Length;

        var region = html[sectionMatch.Index..end];
        return HtmlComment.Replace(region, string.Empty);
    }
}

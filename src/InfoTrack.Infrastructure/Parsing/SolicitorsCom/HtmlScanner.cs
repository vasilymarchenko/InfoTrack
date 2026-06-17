using System.Text.RegularExpressions;

namespace InfoTrack.Infrastructure.Parsing.SolicitorsCom;

// String-scanning HTML helpers (no DOM). InnerText/Section assume tags are not self-nested;
// class matching is substring containment, not exact token equality.
internal static class HtmlScanner
{
    private static readonly RegexOptions Opts =
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

    internal static string? Attr(string block, string tag, string? cls, string attr)
    {
        var openTag = FindOpenTag(block, tag, cls, 0);
        if (openTag < 0)
            return null;

        return ReadAttr(block, openTag, attr);
    }

    internal static string? LeadingText(string block, string tag, string? cls)
    {
        var openTag = FindOpenTag(block, tag, cls, 0);
        if (openTag < 0)
            return null;

        var afterClose = block.IndexOf('>', openTag);
        if (afterClose < 0 || afterClose + 1 >= block.Length)
            return null;

        var textStart = afterClose + 1;
        var nextTag = block.IndexOf('<', textStart);
        if (nextTag < 0)
            nextTag = block.Length;

        var text = block[textStart..nextTag].Trim();
        return text.Length == 0 ? null : text;
    }

    internal static string? InnerText(string block, string tag, string? cls)
    {
        var openTag = FindOpenTag(block, tag, cls, 0);
        if (openTag < 0)
            return null;

        var afterClose = block.IndexOf('>', openTag);
        if (afterClose < 0)
            return null;

        var closing = FindClosingTag(block, tag, afterClose + 1);
        if (closing < 0)
            return null;

        var inner = block[(afterClose + 1)..closing].Trim();
        return inner.Length == 0 ? null : inner;
    }

    internal static string? Section(string block, string tag, string cls)
    {
        var openTag = FindOpenTag(block, tag, cls, 0);
        if (openTag < 0)
            return null;

        var afterClose = block.IndexOf('>', openTag);
        if (afterClose < 0)
            return null;

        var closing = FindClosingTag(block, tag, afterClose + 1);
        if (closing < 0)
            return null;

        return block[(afterClose + 1)..closing];
    }

    private static int FindOpenTag(string block, string tag, string? cls, int from)
    {
        var pattern = cls is null
            ? $@"<{Regex.Escape(tag)}[\s>]"
            : $@"<{Regex.Escape(tag)}\s[^>]*class=""[^""]*{Regex.Escape(cls)}";

        var m = Regex.Match(block[from..], pattern, Opts);
        return m.Success ? from + m.Index : -1;
    }

    private static string? ReadAttr(string block, int tagStart, string attr)
    {
        var tagEnd = block.IndexOf('>', tagStart);
        if (tagEnd < 0)
            return null;

        var tagBody = block[tagStart..(tagEnd + 1)];
        var m = Regex.Match(tagBody, $@"\b{Regex.Escape(attr)}=""([^""]*?)""", Opts);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static int FindClosingTag(string block, string tag, int from)
    {
        var pattern = $@"</{Regex.Escape(tag)}>";
        var m = Regex.Match(block[from..], pattern, Opts);
        return m.Success ? from + m.Index : -1;
    }
}

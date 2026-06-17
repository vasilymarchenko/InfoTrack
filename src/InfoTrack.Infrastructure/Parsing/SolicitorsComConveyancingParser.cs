using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using InfoTrack.Infrastructure.Parsing.SolicitorsCom;

namespace InfoTrack.Infrastructure.Parsing;

public sealed class SolicitorsComConveyancingParser : IListingParser
{
    public IReadOnlyList<Solicitor> Parse(string html, string searchedLocation)
    {
        if (string.IsNullOrEmpty(html))
            return [];

        var region = ResultSectionLocator.Locate(html);
        if (region.Length == 0)
            return [];

        var scrapedAt = DateTimeOffset.UtcNow;
        var results = new List<Solicitor>();

        foreach (var block in ResultItemSegmenter.Segment(region))
        {
            var solicitor = ResultItemParser.Parse(block, searchedLocation, scrapedAt);
            if (solicitor is not null)
                results.Add(solicitor);
        }

        return results;
    }
}

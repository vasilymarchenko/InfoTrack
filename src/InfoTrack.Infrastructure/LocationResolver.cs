using System.Text.RegularExpressions;
using InfoTrack.Application.Ports;
using InfoTrack.Domain;
using InfoTrack.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace InfoTrack.Infrastructure;

// Slug rules confirmed against the live site (see tests Fixtures/README.md): lowercased,
// whitespace collapsed, spaces → hyphens, then conveyancing+{slug}.html (a '+' separator 404s).
public sealed class LocationResolver : ILocationResolver
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly string _baseUrl;

    public LocationResolver(IOptions<ScraperOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _baseUrl = options.Value.BaseUrl.TrimEnd('/');
    }

    public ResolvedLocation Resolve(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            throw new ArgumentException("Location name must not be empty.", nameof(locationName));

        var name = locationName.Trim();
        var slug = Slugify(name);
        var url = new Uri($"{_baseUrl}/{AreasOfLaw.Conveyancing.ToLowerInvariant()}+{slug}.html"); // conveyancing is hardcoded for this MVP

        return new ResolvedLocation(name, slug, url);
    }

    private static string Slugify(string name)
    {
        var collapsed = Whitespace.Replace(name.ToLowerInvariant(), " ").Trim();
        return collapsed.Replace(' ', '-');
    }
}

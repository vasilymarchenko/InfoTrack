namespace InfoTrack.Application.Ports;

public record ResolvedLocation(string LocationName, string Slug, Uri Url);

public interface ILocationResolver
{
    ResolvedLocation Resolve(string locationName);
}

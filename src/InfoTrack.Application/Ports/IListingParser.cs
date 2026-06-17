using InfoTrack.Domain;

namespace InfoTrack.Application.Ports;

public interface IListingParser
{
    IReadOnlyList<Solicitor> Parse(string html, string searchedLocation);
}

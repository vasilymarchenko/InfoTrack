using InfoTrack.Domain;

namespace InfoTrack.Application.Ports;

public interface IReportBuilder
{
    SearchReport Build(SearchResult result);
}

namespace InfoTrack.Application.DTOs;

public record SearchRequest(
    IReadOnlyList<string> Locations,
    string AreaOfLaw = "Conveyancing");

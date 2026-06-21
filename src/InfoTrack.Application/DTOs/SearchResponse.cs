using InfoTrack.Domain;

namespace InfoTrack.Application.DTOs;

public record SearchResponse(SearchResult Result, SearchReport Report, Guid? RunId = null);

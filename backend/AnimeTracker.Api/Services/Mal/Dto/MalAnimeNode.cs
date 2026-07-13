namespace AnimeTracker.Api.Services.Mal.Dto;

/// <summary>Raw MAL anime node shape. Kept close to the wire format (e.g. dates
/// as unparsed strings — MAL returns partial dates like "2024" or "2024-01") —
/// parsing into domain types is a mapping-layer concern, not this client's.</summary>
public class MalAnimeNode
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public MalAlternativeTitles? AlternativeTitles { get; set; }
    public MalMainPicture? MainPicture { get; set; }
    public double? Mean { get; set; }
    public string? MediaType { get; set; } // tv, movie, ova, ona, special, music, unknown
    public string? Status { get; set; } // currently_airing, finished_airing, not_yet_aired
    public int? NumEpisodes { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public List<MalStudio>? Studios { get; set; }
    public MalBroadcast? Broadcast { get; set; }
    public int? Popularity { get; set; }
    public int? Rank { get; set; }
    public MalStartSeason? StartSeason { get; set; }
    public MalListStatus? MyListStatus { get; set; }
    public List<MalGenre>? Genres { get; set; }
    public string? Synopsis { get; set; }
    public string? Background { get; set; }
    public int? AverageEpisodeDuration { get; set; } // seconds
    public string? Source { get; set; } // e.g. manga, original, light_novel
    public List<MalRelatedAnimeEdge>? RelatedAnime { get; set; }
}

public class MalAlternativeTitles
{
    public string? En { get; set; }
}

/// <summary>MAL's authoritative season classification for an anime — the field
/// MAL itself uses to build its per-season listings. Can differ from the quarter
/// the start_date falls in (e.g. an early-June premiere filed under summer).</summary>
public class MalStartSeason
{
    public int Year { get; set; }
    public string Season { get; set; } = ""; // winter, spring, summer, fall
}

public class MalMainPicture
{
    public string? Medium { get; set; }
    public string? Large { get; set; }
}

public class MalStudio
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class MalBroadcast
{
    public string? DayOfTheWeek { get; set; } // JST, e.g. "mondays"
    public string? StartTime { get; set; } // JST, e.g. "23:30"
}

public class MalGenre
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>An entry in MAL's "related_anime" list — RelationType is a raw
/// wire value such as "prequel", "sequel", "side_story", "alternative_version".</summary>
public class MalRelatedAnimeEdge
{
    public MalAnimeNode Node { get; set; } = null!;
    public string? RelationType { get; set; }
}

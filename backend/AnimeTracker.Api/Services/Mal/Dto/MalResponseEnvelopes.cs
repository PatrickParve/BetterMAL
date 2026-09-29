namespace AnimeTracker.Api.Services.Mal.Dto;

public class MalPagedResponse<T>
{
    public List<T> Data { get; set; } = [];
    public MalPaging? Paging { get; set; }
}

public class MalPaging
{
    public string? Next { get; set; }
    public string? Previous { get; set; }
}

public class MalAnimeListEdge
{
    public MalAnimeNode Node { get; set; } = null!;

    /// <summary>Only populated by the ranking endpoint; null everywhere else
    /// (search, season, user list).</summary>
    public MalRankingInfo? Ranking { get; set; }
}

public class MalRankingInfo
{
    public int Rank { get; set; }
}

public class MalUserAnimeListEdge
{
    public MalAnimeNode Node { get; set; } = null!;
    public MalListStatus? ListStatus { get; set; }
}

/// <summary>The part of <c>GET users/@me?fields=anime_statistics</c> that setup reads:
/// how many entries the list holds (first-run-setup design D7).</summary>
public class MalUserResponse
{
    public MalAnimeStatistics? AnimeStatistics { get; set; }
}

public class MalAnimeStatistics
{
    /// <summary>Every entry on the list, whatever its status.</summary>
    public int? NumItems { get; set; }
}

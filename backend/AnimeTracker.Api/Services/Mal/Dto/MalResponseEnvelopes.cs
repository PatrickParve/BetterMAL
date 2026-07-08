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
}

public class MalUserAnimeListEdge
{
    public MalAnimeNode Node { get; set; } = null!;
    public MalListStatus? ListStatus { get; set; }
}

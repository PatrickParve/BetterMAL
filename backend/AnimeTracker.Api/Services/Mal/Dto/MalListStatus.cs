namespace AnimeTracker.Api.Services.Mal.Dto;

/// <summary>My list status for one anime, as MAL returns it (embedded in
/// animelist entries, or as the response of a my_list_status write).</summary>
public class MalListStatus
{
    public string? Status { get; set; } // watching, completed, on_hold, dropped, plan_to_watch
    public int? Score { get; set; }
    public int? NumEpisodesWatched { get; set; }
    public string? StartDate { get; set; }
    public string? FinishDate { get; set; }
    public int? NumTimesRewatched { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

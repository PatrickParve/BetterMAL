using System.Globalization;

namespace AnimeTracker.Api.Services.Mal.Dto;

/// <summary>Partial update for PATCH /anime/{id}/my_list_status. Only set fields
/// are sent — the MAL endpoint takes application/x-www-form-urlencoded, not JSON.
/// Exception: StartDate/FinishDate are always sent (empty when null) so cleared
/// dates propagate; see ToFormFields.</summary>
public class MalListStatusUpdate
{
    public string? Status { get; set; } // watching, completed, on_hold, dropped, plan_to_watch
    public int? NumWatchedEpisodes { get; set; }
    public int? Score { get; set; }
    public int? NumTimesRewatched { get; set; }
    public bool? IsRewatching { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? FinishDate { get; set; }

    public IEnumerable<KeyValuePair<string, string>> ToFormFields()
    {
        if (Status is not null) yield return new("status", Status);
        if (NumWatchedEpisodes is not null) yield return new("num_watched_episodes", NumWatchedEpisodes.Value.ToString(CultureInfo.InvariantCulture));
        if (Score is not null) yield return new("score", Score.Value.ToString(CultureInfo.InvariantCulture));
        if (NumTimesRewatched is not null) yield return new("num_times_rewatched", NumTimesRewatched.Value.ToString(CultureInfo.InvariantCulture));
        if (IsRewatching is not null) yield return new("is_rewatching", IsRewatching.Value ? "true" : "false");

        // Dates are always sent, even when null (as an empty string), so that a
        // locally cleared date actually clears it on MAL too — omitting the field
        // would leave MAL's previous date in place forever.
        yield return new("start_date", StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        yield return new("finish_date", FinishDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
    }
}

namespace AnimeTracker.Api.Models;

/// <summary>Singleton row (one ever, Id 1): the instant first-run setup
/// finished. An absent row, or a null <see cref="CompletedAt"/>, means setup has
/// not finished on this database. It is the only value setup stores about
/// itself (design D3) — what each step has left to do is read from the data
/// the steps have already written, so setup resumes without a work queue of its
/// own.</summary>
public class SetupState
{
    public int Id { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

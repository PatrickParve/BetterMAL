namespace AnimeTracker.Api.Models;

/// <summary>One score's This or That ranking in progress, and nothing derived
/// from it (add-this-or-that-ranking design.md D2/D4). The row holds only
/// facts: the order the score's anime are placed in and every answer given, in
/// the order given. The current pair, how many are placed and the finished
/// order are all rebuilt by replaying these against the score's anime as they
/// stand now (<c>ThisOrThatReplay</c>), so Undo is "drop the last answer" and a
/// change to my list needs no migration of anything stored. Deleted when the
/// ranking is saved, kept as it was, or replaced by a Start over.
///
/// There is no foreign key: the ids live in arrays, and an anime that has gone
/// from the database is simply one that has left the score. The record is not
/// part of the export file or the list backup.</summary>
public class ThisOrThatRun
{
    /// <summary>1-10: the score being ranked, and the key, so a score has at
    /// most one ranking in progress.</summary>
    public int Score { get; set; }

    /// <summary>The order the anime are placed in, shuffled at Start. An anime
    /// that joins the score later is appended; one that leaves stays in it, as
    /// a "ghost" whose earlier answers still count.</summary>
    public List<int> QueueIds { get; set; } = [];

    /// <summary>The answers, in the order given, as three parallel lists: the
    /// anime being placed, the anime it was shown with, and the outcome
    /// (<c>ThisOrThatOutcome</c>) from the first one's side. Always the same
    /// length; a row where they differ reads as no ranking.</summary>
    public List<int> AnswerChallengerIds { get; set; } = [];
    public List<int> AnswerOtherIds { get; set; } = [];
    public List<int> AnswerOutcomes { get; set; } = [];

    /// <summary>With the anime being placed, decides which side it is shown on.
    /// Stored rather than recomputed, so a reload or a restart keeps every
    /// anime on its side (design.md D6).</summary>
    public int Seed { get; set; }

    /// <summary>The concurrency token, bumped on every write: two requests that
    /// read the same row can't both land, and a tab behind the ranking is
    /// refused.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

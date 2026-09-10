namespace AnimeTracker.Api.Services.Transfer;

/// <summary>What an import did (design.md D13). Holds nothing else: a
/// choice or record that applied cleanly gets no line, a ranking that was
/// only reordered lists no anime, and there are no counts of what was
/// applied.</summary>
public record TransferImportReport(
    List<TransferReportAnime> RankingAdded,
    List<TransferReportAnime> RankingRemoved,
    List<TransferReportAnime> Fetched,
    List<TransferImportFailure> Failures);

public record TransferReportAnime(int AnimeId, string Title, string? EnglishTitle);

public enum TransferImportFailureSubject
{
    Anime,
    Series,
}

/// <summary><paramref name="What"/> names what could not be applied
/// (a chosen picture, a series title, a series picture, edit history, or the
/// ranking); <paramref name="Reason"/> says why.</summary>
public record TransferImportFailure(
    TransferImportFailureSubject Subject, int Id, string? Title, string? EnglishTitle, string What, string Reason);

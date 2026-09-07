using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Sync;

public enum HeldChangeKind
{
    Entry,
    Removal,
}

/// <summary>Whether a decision (accept or decline) was actually applied.
/// <see cref="NotHeld"/> means there was nothing held for that anime id — a
/// caller maps that to 404 rather than to a decision failure.
/// <see cref="Failed"/> only ever comes from a decline whose MyAnimeList read
/// failed (design.md D6.4): the item is left exactly as it was. Accepting
/// never reports Failed — a failed push still releases the hold and simply
/// leaves the item pending for the retry job (design.md D5).</summary>
public enum HeldChangeDecisionOutcome
{
    NotHeld,
    Applied,
    Failed,
}

public record HeldChangeDecisionResult(HeldChangeDecisionOutcome Outcome, string? Error = null);

/// <summary>How many of a bulk accept/decline actually applied versus how
/// many are still held afterwards (a per-item failure never stops the rest —
/// design.md D5/D6 "Accepting/Declining SHALL act on one item at a
/// time").</summary>
public record HeldChangeBulkResult(int Succeeded, int StillHeld);

/// <summary>The six pushed fields, either as they stand locally or as
/// MyAnimeList currently reports them (design.md D8) — the same fields
/// <see cref="AnimeTracker.Api.Services.Mal.MalStatusResolution.MatchesRemote"/>
/// compares. MyAnimeList's raw status is carried here uninterpreted (not
/// resolved against local), since this renders what MyAnimeList actually
/// holds rather than what would be kept.</summary>
public record HeldChangeValuesDto(
    WatchStatus Status,
    int EpisodesWatched,
    int? MyScore,
    DateOnly? StartedAt,
    DateOnly? CompletedAt,
    int RewatchCount)
{
    public static HeldChangeValuesDto FromEntry(UserAnimeEntry entry) => new(
        entry.Status, entry.EpisodesWatched, entry.MyScore, entry.StartedAt, entry.CompletedAt, entry.RewatchCount);
}

/// <summary>One activity-log row shown on a held item's review row, carrying
/// its own ChangeType/ChangeDetail/Timestamp exactly as "Latest updates"
/// already describes it (design.md D4) — mal-write-sync "described exactly
/// as the app's own history describes them".</summary>
public record HeldChangeRecentChangeDto(ActivityChangeType ChangeType, string? ChangeDetail, DateTimeOffset Timestamp);

/// <summary>One held item — an entry or a removal — carrying everything
/// mal-write-sync's "The review carries the context to judge each held
/// change" requires: which anime, what the unsent change was and when
/// (RecentChanges/AdditionalChangeCount), what would be pushed (LocalValues —
/// null for a removal, which has no field values, only the deletion itself),
/// and what MyAnimeList currently holds (RemoteValues, best-effort:
/// RemoteUnavailable true means the read failed rather than "MyAnimeList has
/// no entry", which is RemoteValues == null with RemoteUnavailable
/// false).</summary>
public record HeldChangeDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    HeldChangeKind Kind,
    DateTimeOffset HeldAt,
    HeldChangeValuesDto? LocalValues,
    List<HeldChangeRecentChangeDto> RecentChanges,
    int AdditionalChangeCount,
    HeldChangeValuesDto? RemoteValues,
    bool RemoteUnavailable);

/// <summary>Reviews and resolves every change held at startup (design.md
/// D1-D8a) — the settings-page counterpart to <see cref="IReconciliationService"/>'s
/// diff review, but per-item rather than one batch, since a held item's
/// process-boundary staleness has nothing to do with any other held item's.</summary>
public interface IHeldChangeService
{
    /// <summary>The current held set, each item's MyAnimeList side read
    /// fresh (design.md D8 — never cached). An item MyAnimeList already
    /// agrees with is cleared as a side effect and omitted rather than
    /// returned (design.md D8a) — this call can therefore change the
    /// database even though it looks like a read.</summary>
    Task<List<HeldChangeDto>> GetHeldAsync(CancellationToken ct = default);

    /// <summary>Releases the hold and pushes immediately (design.md D5/D7).
    /// A failed push is not reported as a failure here — the item simply
    /// stays pending, unheld, for the retry job.</summary>
    Task<HeldChangeDecisionResult> AcceptAsync(int animeId, CancellationToken ct = default);

    /// <summary>Discards the held change and adopts MyAnimeList's current
    /// value (design.md D6/D7). Re-reads MyAnimeList at decision time rather
    /// than trusting anything a prior GetHeldAsync render cached.</summary>
    Task<HeldChangeDecisionResult> DeclineAsync(int animeId, CancellationToken ct = default);

    /// <summary>Accepts every currently held item, one at a time; an item
    /// whose push fails counts toward StillHeld, not toward failing the
    /// call.</summary>
    Task<HeldChangeBulkResult> AcceptAllAsync(CancellationToken ct = default);

    /// <summary>Declines every currently held item, one at a time; an item
    /// whose MyAnimeList read fails counts toward StillHeld.</summary>
    Task<HeldChangeBulkResult> DeclineAllAsync(CancellationToken ct = default);
}

## Context

Three independent defects, all in the seam between what the app knows about an entry and what an external source says about it.

**Rewatching leaks away.** `WatchStatus.Rewatching` is local-only; `ToMalStatusString` pushes it as `watching`. `ReconciliationService` already carries a carve-out, but it guards the *comparison* only (`local.Status == Rewatching && remote.Status == Watching` counts as matching). The diff row it builds is `ToDiffEntry(..., remote)` — MAL's `watching` verbatim — so any other field differing raises a diff that, once accepted, writes Watching over the rewatch. `ResyncService` calls `edge.ListStatus.ApplyTo(entry, now)` with no carve-out at all, so every corrective re-sync flattens every rewatch on the list.

**Completed is being used as a display flag, and gated on a field that lies.** `UserAnimeEntryEditService.ApplyEpisodesWatched` computes `completionTarget = anime.AiringStatus == "currently_airing" ? airedSoFar : anime.TotalEpisodes` and completes the entry on reaching it; `ApplyStatus` mirrors that for an explicit edit, filling episodes to the aired count and deliberately skipping the finish date. `CompletedEntryReopenService` then undoes it on the next broadcast. The whole apparatus exists to keep caught-up shows out of one carousel. It writes `completed` to MAL for a show that has not finished, and leaves entries that *do* finish stuck without a finish date.

Every one of those rules — plus `RewatchingEligibility.AnimeHasFinishedAiring` — branches on `AnimeMetadata.AiringStatus`, MAL's own `currently_airing`/`finished_airing` field, which routinely still reads `currently_airing` weeks after a show ends. A run watched to its genuine final episode therefore does not complete, cannot be completed by hand, cannot be rewatched, and never asks for a score.

The app already holds a fact that does not lag. `ApplyEpisodesWatched` caps a save at `hasAired ? airedSoFar ?? anime.TotalEpisodes : 0` (`UserAnimeEntryEditService.cs:175`), so wherever AniList airing rows exist an entry can never be watched past what has aired. **Reaching the total is therefore itself proof that the whole run is out**, whatever MAL's status field claims. That observation is what this change builds the completion rules on.

**A known episode count is discarded.** `EpisodeScheduleRefreshService.RefreshOneCoreAsync` already calls `LookupByMalIdAsync` and `GetAiringScheduleAsync` for every my-list anime. Both hit AniList's `Media`, which carries `episodes`. Neither query selects it. So *Tensei shitara Ken deshita II* — MAL total null, AniList 12 — reads `watched/?`, cannot be completed, has a bar with no end, and raises no episode-count-released update.

Existing idioms this change follows: the `PictureUrl`/`MalPictureUrl` pair on `AnimeMetadata` (an effective value beside the source value it derives from), and `CompletedEntryReopenService`'s read-time evaluation (a rule applied on every read path plus the recurring pass, rather than a scheduled job).

## Goals / Non-Goals

**Goals:**

- A local Rewatching entry survives every path that reads a MyAnimeList list status back onto it, by one shared rule rather than one carve-out per path.
- Completed means every episode has aired and been watched. It is decided by episode counts, not by MAL's airing status, so a stale status can neither block a completion nor undo one.
- Being caught up on a run with more to come becomes a selection rule for one dashboard section, writing nothing.
- A Watching entry that already covers what turns out to be the full run is completed on the next read — the case where the total arrives after the viewing.
- AniList's episode total fills an unknown MAL total, on the requests already being made, and surfaces as an ordinary episode-count-released update.

**Non-Goals:**

- Displaying where a total came from. An AniList-sourced total renders as any other total.
- Widening AniList's role. It supplies episode timing (as today) and, new here, one integer. Nothing else.
- Backfilling anime outside my list. Only anime the airing sync visits can get an AniList total.
- Any migration pass over user entries. Legacy `Completed`-while-airing rows self-correct on read (D5).
- Changing the completion score prompt's mechanics. It already keys on the completion transition; its finish-date proxy simply stops distinguishing anything, and it now fires in the stale-status case, which is the intended effect rather than a change to the prompt itself.
- Making MAL's airing status reliable, or replacing it elsewhere. It keeps its existing roles (the aired-episode gate's fallback, the dashboard's airing badge, the re-open guard); this change only stops it being the sole arbiter of "the run is over".

## Decisions

### D1 — One status-resolution rule, placed where every read-back path already passes

Add `MalStatusResolution.ResolveAgainstLocal(WatchStatus? local, WatchStatus remote)` in `Services/Mal`, returning `Rewatching` when `local is Rewatching && remote is Watching` and `remote` otherwise.

Call it from **two** places, which between them cover every path:

1. Inside `MalListStatus.ApplyTo(target, now)` — the single update-in-place mapper `ResyncService` and any future read-back path uses. It has `target.Status` in hand, so it can resolve without a new parameter. `ToUserAnimeEntry` builds a fresh entry whose `Status` defaults to `Watching`, so the rule is inert on the create path, exactly as the spec requires.
2. Explicitly in `ReconciliationService`, which does *not* go through `ApplyTo` for the entry it writes: it builds the remote shape into a throwaway entry for diffing, then later copies diff columns onto the real one. It resolves against the **local** entry's status when building the diff row, and against the **live** entry's status again in `AcceptPendingDiffAsync`.

*Alternative rejected:* leave `ApplyTo` alone and repeat the check at each call site. That is what produced the current bug — `ReconciliationService` got the rule and `ResyncService` did not — and nothing would stop the next read-back path from repeating it.

*Alternative rejected:* thread a `preserveRewatching: true` flag through `ApplyTo`. A flag every caller must remember to pass is the same failure mode with extra ceremony.

### D2 — The held diff records the status the entry will end up with

`ToDiffEntry` takes the resolved status, not `remote.Status`. This is a correctness fix for the review screen (`PendingReconciliationDiffEntryDto.Status` is rendered on the settings page): asking the user to approve a change to "Watching" that will not happen is worse than not asking.

`AcceptPendingDiffAsync` re-resolves anyway, against the entry as it stands at accept time. The two guards are not redundant: the diff can be reviewed long after it was computed, and the entry may have become a rewatch in between.

### D3 — One shared "everything has aired" predicate, as a union

Add to `Services/Entries/AiredEpisodeGate.cs` (which already owns the sibling "has anything aired" question):

```csharp
public static bool EverythingHasAired(AnimeMetadata anime, int? episodesAired) =>
    anime.AiringStatus != "currently_airing"
    || (anime.TotalEpisodes is { } total && episodesAired is { } aired && aired >= total);
```

A **union**, deliberately, not a replacement of the status test:

- The first arm is exactly today's rule, so nothing that completes or rewatches today stops doing so. That matters because AniList data can be incomplete — a finished show missing a row would read `aired < total` and, under an aired-count-only rule, become uncompletable. This cannot regress.
- The second arm is the new capability: a stale `currently_airing` is overruled by airing data that has already run to the total.

`RewatchingEligibility.AnimeHasFinishedAiring` becomes a call to this, so `IsEligible` gains an `int? episodesAired` parameter — `ApplyStatus` already has `airedSoFar` in scope at both call sites.

*Alternative rejected:* drop the status arm and judge purely on `aired >= total`. Simpler to state, but it makes completion depend on AniList having complete data for every finished anime in the list, which it demonstrably does not, and would silently make some already-completable shows uncompletable.

*Alternative rejected:* infer "finished" from `AiredTo` having passed. It is MAL-sourced like the status and no fresher, and partial dates (`yyyy`, `yyyy-MM`) make the comparison unreliable.

### D4 — The completion target is always the total

In `UserAnimeEntryEditService`:

- `ApplyEpisodesWatched`: `completionTarget` becomes `anime.TotalEpisodes`, unconditionally. The aired-so-far branch is deleted.
- Both the automatic arm and `ApplyStatus`'s explicit arm gate on one predicate, `CanComplete(anime, airedSoFar) = hasAired && anime.TotalEpisodes is not null && EverythingHasAired(anime, airedSoFar)`. The explicit path throws on failure (`CannotCompleteUnknownEpisodeCountException`, or a new `CannotCompleteBeforeFullyAiredException`); the automatic path is silently inert, which is how it already behaves for an unknown total.
- `entry.CompletedAt ??= today` becomes unconditional, and `CannotCompleteUnknownAiredCountException` and the aired-so-far fill are deleted with the branch that used them.

The two paths cannot disagree, because they share the predicate *and* the target. And no separate "was this viewing legitimate?" check is needed anywhere: the episodes-watched cap already enforces it, so an entry standing at its total has watched only aired episodes by construction.

*Alternative rejected:* keep an aired-so-far completion target and merely add the stale-status escape. That preserves the two-meanings-of-Completed problem the rest of this change exists to remove.

### D5 — Re-open reads the total, not the aired count

`Completed && AiringStatus == "currently_airing" && !(TotalEpisodes is { } t && EpisodesWatched >= t)` → `Watching`.

Two changes from today's `airedSoFar > EpisodesWatched`:

- **It reads the total.** An entry that has watched the published total is never re-opened, so a stale `currently_airing` cannot repeatedly un-complete a run the user genuinely finished — the exact failure D3 exists to prevent, arriving here by a different door.
- **It no longer reads the aired count at all**, which removes the aired-so-far argument from this service's signature entirely.

The `currently_airing` guard is **kept**, and is load-bearing: MAL lists commonly hold entries marked `completed` with a partial episode count (people do this on MAL's own site). Dropping the guard would re-open every one of them on the next page read and push `watching` back to MAL — churning the user's list. Restricting to currently-airing anime confines the rule to entries that can actually have more to come.

Legacy entries the old auto-completion wrote (Completed at a partial count on an airing show) satisfy the new condition immediately and self-correct on the next read of any page showing them. No migration pass (was D5 in the previous revision).

*Alternative rejected:* `EpisodesWatched < TotalEpisodes` with no airing-status guard. Cleaner to state and airing-status-free, but it re-opens imported `completed`-at-0 entries on finished shows and pushes the change to MAL.

### D6 — Both directions in one service, keyed on counts

`ICompletedEntryReopenService` becomes `IAiringWatchStatusService` with a single `SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct)`:

| Condition | Result |
|---|---|
| `Completed`, `currently_airing`, and not `EpisodesWatched >= TotalEpisodes` | → `Watching` |
| `Watching`, `TotalEpisodes is { } t`, `EpisodesWatched >= t`, and `EverythingHasAired` | → `Completed`, `CompletedAt ??= today` |

The aired-count dictionary is still a parameter, but only the second rule reads it (through `EverythingHasAired`), and only for its second arm. Both rules keep the existing fresh-tracked-read re-check, activity row, `PendingSync`, sync scheduling, and `DbUpdateConcurrencyException` handling.

The completion direction is narrower than it first appears: the increment path already completes an entry the moment it reaches the total, so the only entries this catches are ones that reached a total *before it was known* — which is precisely what the AniList fallback (D10) now creates. That is its reason for existing, not a redundant safety net.

Callers pass every entry they loaded rather than a pre-filtered subset, since the two rules select different subsets. All four call sites (`MainDashboardService`, `MyListService`, `AnimeDetailService`, `EpisodeScheduleRefreshBackgroundService`) already have the full set and the aired counts in hand.

*Alternative rejected:* a second service beside the first. Two services applying opposite rules to overlapping sets, wired into the same four call sites, is an ordering hazard for no gain.

### D7 — The carousel filter is server-side and count-driven

`MainDashboardService` currently resolves `EpisodesAiredAsOfAsync(e.Anime, ...)` inside the DTO loop — one query per card. The filter has to run *before* the DTOs are built, so switch to the existing `EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata>, ...)` bulk overload once, then filter and project from the dictionary. That dictionary is also what `SettleAsync` (D6) needs, so one read serves both.

Filter: drop an entry where `aired is { } a && a > 0 && EpisodesWatched >= a`. Note what is **absent** — no airing-status condition. The question the section answers is "is there something to watch right now", and the aired count answers it directly; a stale status would only reintroduce the lag D3 exists to route around. `>=` rather than `==` so an entry ahead of the app's aired figure is not shown as if it had something to watch; `a > 0` so nothing is hidden on a zero.

An entry that has watched the full run never reaches this filter — it is Completed by D4 or D6 and is not in the Watching/Rewatching set at all — so the filter cannot hide a finished show behind a "caught up" judgement.

Server-side rather than client-side because the carousel's five-card bound, its arrow visibility, and its "already ordered on arrival" guarantee are all computed over the list the payload delivers.

### D8 — Three episode-count columns with a derived effective total

`AnimeMetadata` gains `MalTotalEpisodes` and `AniListTotalEpisodes` (both `int?`). `TotalEpisodes` stays the column every existing reader and EF projection uses, and becomes derived:

```
TotalEpisodes = MalTotalEpisodes ?? AniListTotalEpisodes
```

A shared `AnimeMetadata.ResolveTotalEpisodes()` re-derives it, called by whichever writer just touched a source column: `MalMappingExtensions.ApplyTo`/`ApplyLeanTo` (which now write `MalTotalEpisodes` instead of `TotalEpisodes`) and the AniList refresh. Both sources normalise `0` to null, as `ApplyTo` already does for MAL.

*Alternative rejected:* store the AniList total on `AnimeAiringSync`, where the rest of the AniList state lives. It matches the "AniList state kept apart from the MAL mirror" principle, but ~20 read sites project `TotalEpisodes` directly out of `AnimeMetadata` in EF queries, and a join or a `[NotMapped]` property would break every one of them.

*Alternative rejected:* two columns — effective plus AniList's — inferring MAL's as "effective unless it equals the AniList value". Ambiguous precisely when the two sources agree, which is the common case.

*Cost accepted:* `AnimeMetadata` is documented as the MAL mirror. It already isn't strictly one (`PictureUrl` holds a chosen override), and the same three-value shape is used here for the same reason. The entity comment is updated to say so.

### D9 — AniList's total rides the requests already being made

Add `episodes` to both existing GraphQL documents' `Media` selection — `LookupQuery` and the `Media(id:)` half of `ScheduleQuery` — and carry it on `AniListMediaLookup` and `AniListScheduleResult` as `int? Episodes`. No new query, no new round trip, and the schedule fetch's copy means an anime whose AniList id was resolved on an earlier run still gets a total on every refresh.

`RefreshOneCoreAsync` currently reads the anime `AsNoTracking`. It switches to a tracked read so it can write the two columns, taking `schedule.Episodes ?? lookup.Episodes` (schedule first, matching how `status` and `nextAiringAt` are already merged).

### D10 — The AniList refresh records one update kind, directly

Detection is `before is null && after is not null` on the *effective* total, recorded as `AnimeUpdateKinds.EpisodeCountReleased` through `IAnimeUpdateRecorder` — which `EpisodeScheduleRefreshService` already injects, and whose once-ever dedupe for becoming-known kinds means a repeated refresh cannot re-raise it.

Deliberately **not** routed through `IAnimeMetadataChangeDetector`: that snapshots relations, aired-from, and the broadcast slot and would diff them against a state AniList never wrote, and its `RecordAsync` also enqueues series rebuilds. The spec's "each path detects the kinds its own data can produce" is enforced by which recorder each path calls.

### D11 — Migration

One EF migration adding both columns, plus `UPDATE "AnimeMetadata" SET "MalTotalEpisodes" = "TotalEpisodes"`. Everything currently in `TotalEpisodes` came from MAL, so the derivation reproduces every existing value on the next write and immediately for every read.

## Risks / Trade-offs

- **A burst of MAL pushes on first load after deploy** — every legacy entry completed at a partial count on an airing show re-opens at once (D5) → bounded by currently-airing shows on the list; each goes through the existing per-entry debounce and the pending-sync retry sweep, which already tolerate a backlog.
- **AniList airing data can be incomplete for a finished show**, leaving `aired < total` → the union in D3 means the MAL-status arm still answers "yes" for such an anime, so nothing that completes today stops completing. This is the specific regression the union exists to prevent, and the reason an aired-count-only rule was rejected.
- **A wrong AniList total now completes an entry prematurely** (rather than merely mislabelling a bar) → accepted, for three reasons that together make it small and self-limiting. **Bounded:** precedence means the figure is only ever consulted while MAL publishes none, so the exposure is one window per anime, closed the moment MAL publishes a count. **Rare:** both sites split cours into separate media entries, so the obvious "AniList says 12, the show runs 24" case is usually AniList being *right* about a 12-episode entry; what remains is genuine extensions and recap/special counting differences. **Self-announcing:** when either source corrects the total, `ResolveTotalEpisodes()` re-derives, the re-open rule returns the entry to Watching on the next read, and it reappears in Currently watching with both transitions in the activity log — it does not degrade quietly. What does not unwind automatically is the finish date (never cleared automatically, by design) and a score saved at the completion prompt, including its ranking placement; both are a single edit in the entry editor.

  Note the failure is fixed by MAL publishing a **count**, not by its airing status flipping — a stale-status show whose total is wrong stays wrong until a count arrives. In practice the two arrive together, since a show MAL reports as finished almost always carries `num_episodes`. This is distinct from the incomplete-airing-rows case, which the D3 union already covers through its status arm.

- **Rejected safeguard:** letting an AniList-sourced total drive display and manual completion but not the automatic completion on "+". It would remove the residue above, at the cost of the completion score-and-rank prompt for exactly the shows the fallback exists for — the primary thing asked of this change. Recorded here so the trade-off is not silently re-litigated.
- **A show whose MAL status is stale *and* whose AniList rows are missing** completes neither automatically nor by hand → unchanged from today, and now strictly rarer, since either source alone is enough. Nothing regresses; the case simply is not fixed.
- **A caught-up card stays in the carousel until the next dashboard load** (D7 filters server-side; the frontend does not reload after a non-completing increment) → deliberate, and specified: removing the card under the control the user just pressed is worse. The `main-dashboard` delta makes it a scenario rather than an accident.
- **`AnimeMetadata` accrues a second derived-value pair** → contained by making `ResolveTotalEpisodes()` the only writer of `TotalEpisodes`, the same way the `wasOverridden` guard is the only writer of `PictureUrl`.
- **Frontend and backend gates drifting apart** — `EntryEditorOverlay` mirrors the completion and rewatching rules by hand, and now has a third condition to mirror → if missed, the server rejects with a reason and the existing `action-failure-notices` path shows it, so the failure mode is a clear message rather than a wrong write. The mirroring comment on both sides names the counterpart, as it already does for `hasAiredEpisodes`.

## Migration Plan

1. Schema first: add the two columns and backfill `MalTotalEpisodes` from `TotalEpisodes` (D11). Harmless on its own — nothing reads them yet.
2. Ship the MAL-mapping and AniList-client changes together, so both writers of the source columns land before the derivation matters.
3. Ship the shared predicate, the editing rules, the settle service, and the dashboard filter together — they are one behavioural rule split across four files, and shipping the carousel filter without the editing change would hide caught-up shows that are still being auto-completed at the aired count.
4. Rollback: the three groups are independent. Reverting group 3 restores the old completion behaviour with the columns in place and unread; the columns themselves are additive and can be left on a rollback.

## Open Questions

None outstanding. Three decisions that could have gone either way were settled before this design was finalised: that explicit completion of a run still to come is refused rather than kept as an escape hatch; that a caught-up entry is completed automatically rather than left for the user; and — after the observation that MAL's airing status routinely lags reality while the episodes-watched cap does not — that completion is decided by episode counts with the airing status kept only as one arm of a union (D3), rather than being replaced outright.

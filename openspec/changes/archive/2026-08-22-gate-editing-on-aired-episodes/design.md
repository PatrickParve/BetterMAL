## Context

Three facts about an anime already flow through the app, from two different sources:

- **Airing status** (`not_yet_aired` / `currently_airing` / `finished_airing`, or absent) comes from cached MyAnimeList metadata.
- **Aired-so-far episode count** comes exclusively from stored AniList rows, via `IEpisodeScheduleService.EpisodesAiredAsOfAsync`. Per the `episode-airing-data` capability it is **null-means-unknown**, never zero: an anime with no aired row reads as unknown, which is deliberately distinguishable from "nothing has aired yet".
- **Total episode count** comes from MyAnimeList and is itself often unknown.

The write path is single: every edit — the "+" control, the in-place count field, the entry editor, the completion-score prompt, the detail page's add actions — funnels through `PATCH /entries/{id}` into `UserAnimeEntryEditService.UpdateEntryAsync`. That service already caps episodes watched at `EpisodesAiredAsOfAsync(...) ?? anime.TotalEpisodes` and already refuses Completed when the total is unknown. The gap is that the `?? anime.TotalEpisodes` fallback hands an unaired anime its full published total as a ceiling, and that nothing constrains status, score, or rewatch count by what has aired.

On the read side the editable progress row is rendered by one shared component, `ProgressBar`, from three call sites that pass an increment handler (My List rows, the dashboard's currently-watching cards, the anime detail page). Two further call sites — the profile page's all-list bar and the series page's series-wide bar — pass no handler and are aggregates rather than entries.

## Goals / Non-Goals

**Goals:**

- One definition of "has any episode of this anime aired", computed the same way on both sides of the API.
- The API is the authority: every rule here is enforced in `UserAnimeEntryEditService` and returns a 400, whatever the client does.
- The UI never offers an action the API would reject — the progress row disappears rather than sitting there disabled, and blocked editor fields are visibly unavailable.
- An entry that already holds a now-disallowed value is never trapped: it can always be edited back toward an allowed state.

**Non-Goals:**

- Retroactively rewriting existing entries that violate the new rules. The rules constrain edits, not stored history. The one automatic status change introduced here — re-opening a Completed entry when a new episode airs — is a rule the user asked for explicitly, not a data cleanup.
- Gating the read-only aggregate progress bars (profile page, series page, "Followed shows airing"). None of them edits an entry.
- Changing how aired counts are fetched, stored, or refreshed. This change consumes `episode-airing-data`; it does not alter it.
- Changing the completion-score prompt's own behaviour. It cannot fire for an unaired anime, because reaching a total requires aired episodes.

## Decisions

### D1: "Has aired" is `aired >= 1` when the count is known, else `airingStatus != not_yet_aired`

The predicate is:

```
hasAired(anime) =
  airedSoFar is known  ->  airedSoFar >= 1
  airedSoFar is unknown ->  airingStatus != "not_yet_aired"
```

Both branches matter. The first is authoritative when AniList data exists: it catches an anime whose first episode has aired while MyAnimeList still says `not_yet_aired`, and — because `SeriesEntryDto.airedEpisodes` maps `not_yet_aired` to a literal `0` rather than null — it also reads a genuine zero correctly wherever a caller supplies one.

The second branch is the fallback that keeps the change from breaking the library. Most finished anime in a list have no stored AniList rows at all, so their aired count is unknown; treating unknown as "nothing aired" would strip the progress row from the bulk of My List. Falling back to airing status means only `not_yet_aired` blocks, and an anime with no recorded airing status at all stays permissive — the app cannot prove nothing has aired, so it does not act as if it had.

*Alternative considered:* gate purely on airing status. Rejected because it ignores AniList entirely, which is the app's designated source of episode timing, and would let a `currently_airing` show whose first episode has not actually aired yet accept an episode-watched edit.

*Alternative considered:* gate purely on the aired count, treating unknown as zero. Rejected for the library-wide breakage above, and because `episode-airing-data` is explicit that unknown and zero are different answers.

### D2: The predicate lives in two places, deliberately

The backend computes it inside `UserAnimeEntryEditService`, from the `AnimeMetadata` row and a `EpisodesAiredAsOfAsync` call it already makes. The frontend computes it in `utils/anime.ts` as a small exported helper reading `(airingStatus, episodesAired)` off whichever DTO it has.

The duplication is intentional and small: shipping a server-computed `canEdit` boolean on every DTO would put the same fact on six shapes and still need the client to know *which* rule each flag governed. A single helper mirroring a single service rule is easier to keep honest, and the server rejection is what actually protects the data. The helper carries a comment naming its backend counterpart, matching how `SERIES_TRAVERSAL_RELATIONS` already mirrors `SeriesRelations.cs`.

### D3: The progress row is removed, not disabled

Where nothing has aired, the whole progress row goes: bar, `watched/total` label, and "+" control. A disabled bar reading `0/12` on a show that does not exist yet is noise — the user asked for it to be gone, and the row's own cell collapses rather than leaving a hole.

`ProgressBar` itself is unchanged. Each of the three editing call sites decides whether to render it, so the read-only aggregate call sites are untouched by construction, and the component keeps one job.

On the anime detail page the status text beside the bar (`Not in my list`, `Plan to watch`, …) stays — it is the page's only statement of the entry's status, and it is not an edit control.

### D4: Completed on a currently-airing anime fills episodes watched to the aired count

Marking Completed already fills episodes watched to the total, mirroring MyAnimeList's own UI. The natural extension of "Completed means you have watched every episode out" is that on a currently-airing anime the fill target is the aired-so-far count instead of the eventual total — which is also the only value that would survive the episodes-watched ceiling.

This makes the eligibility rule fall out cleanly: Completed requires a known fill target. For a finished anime that target is the total (the existing `CannotCompleteUnknownEpisodeCountException` rule, unchanged). For a currently-airing anime it is the aired-so-far count, so Completed is refused when that count is unknown.

*Alternative considered:* treat "watched every episode out" as a precondition and reject Completed when `episodesWatched < airedSoFar`, rather than filling. Rejected because it would make the editor's Completed option fail depending on a number the editor does not show, where the fill is both predictable and consistent with what the finished-anime path already does.

Auto-completion from the progress row now mirrors the explicit edit: for a currently-airing anime it fires at the *aired-so-far count*, not the eventual total, so reaching the latest aired episode via the "+" control or the in-place count field completes the entry the same way choosing Completed from the editor would. (An earlier version of this decision kept auto-completion at the total only, to avoid silently completing an unfinished show — that concern no longer applies once completing a still-airing show sets no finish date and self-corrects via D6's re-open on the next episode, below.) The same fallback-to-Watching-not-Rewatching drop logic applies when a count is later lowered back below what's aired. A finished anime's auto-completion is unchanged: it still requires reaching the *known total*.

The completion-score prompt is suppressed for this "caught up" case: the frontend's "just entered Completed" check additionally requires a non-null `completedAt` in the response, so scoring is asked for only once the anime has actually finished — reusing the no-finish-date signal from below rather than adding a new field.

Completing a currently-airing anime sets no finish date. `CompletedAt ??= today` normally fires alongside every Completed transition, but here it would stamp a "finished" date on a show that hasn't actually finished — one that D6 will likely re-open the next time an episode airs, leaving a stale date sitting on a Watching entry in the meantime. The guard is simply `anime.AiringStatus != "currently_airing"`, applied identically at both places `CompletedAt` gets set (the explicit Completed edit here, and the auto-complete arm in `ApplyEpisodesWatched` for the rare case of watching the finale before MAL's airing status has caught up). The date is filled in normally the first time the entry is completed while the anime reads as finished — which may never happen for an entry the user never re-touches after the finale airs; that's accepted, not worked around.

This is also where `add-rewatching-status` and this change meet. That change's strict Completed rule sends a count drop on a Completed entry to Rewatching when the anime has finished airing, and to Watching otherwise — and "otherwise" is exactly the case this decision creates: a Completed entry on a *currently-airing* anime, reachable once its aired-so-far count is known. Lowering such an entry's count therefore lands in Watching, never Rewatching, since Rewatching's own eligibility rule refuses any anime that has not finished airing. Nothing here needs to re-implement that fallback — it already falls out of reusing the same eligibility check — but it is worth naming, since it is easy to read this decision as only ever producing a finished-anime Completed entry.

### D5: Blocked score and rewatch mean "cannot set a value", not "cannot touch the field"

For an anime with nothing aired, a score of 1–10 and a rewatch count above 0 are rejected; a score of "no score" and a rewatch count of 0 are always accepted. Without that asymmetry an entry that already carries a score — set before this change, or imported from MyAnimeList — could never be cleared, because every save of that field would be refused.

The editor reflects the same asymmetry: the score dropdown offers only "No score" and the rewatch input is pinned to 0, both marked unavailable, so the current value is visible and clearable but no new value can be chosen. The My List row's score dropdown is hidden for such a row, consistent with D3.

### D6: Re-opening a Completed entry happens on read, with a sweep as the backstop

An episode becomes aired by the passage of time alone. AniList supplies a show's whole schedule ahead of the broadcast, future episodes included, and each stored row carries its own air instant; the aired-so-far count is then just `MAX(episode) WHERE AirsAtUtc <= now`. Nothing polls, nothing checks, nothing has to run — the count is already higher the first time anything reads it after the stored instant passes. AniList is re-consulted only to *learn or correct a schedule*, never to discover that an episode aired.

The re-open condition inherits that property: `status == Completed && airingStatus == currently_airing && airedSoFar > episodesWatched` is derivable at any moment, and flips at an instant already known in advance. The only reason it does not resolve itself is an asymmetry — the aired count is computed on every read, while `Status` is a stored column that is pushed to MyAnimeList, and nothing recomputes a stored column.

So the flip is done in two places, from **one shared method**:

- **On read**, by the surfaces that display the entry — my list, the dashboard, and the anime detail page — so the entry is already Watching the first time it is looked at after the episode drops. Reads triggering writes is established here: `TopAnimeService.EnsureFreshAsync` refreshes and caches a ranking on the read path.

  On my list and the detail page this is free: both already resolve an aired count for every entry they render. The dashboard is the exception, and needs care in two ways. It filters `entries.Where(e => e.Status == Watching)` *before* resolving any aired count, so (a) the re-open must run on the full entry list ahead of that filter — run after it, a still-Completed entry is discarded before it can be re-opened, and would never reach Currently watching however often the page is opened — and (b) the Completed entries have no aired count resolved for them, so this path alone pays for one **bulk** lookup over the Completed-and-`currently_airing` entries. One query, not one per entry.
- **On the recurring airing tick**, as a backstop. `EpisodeScheduleRefreshBackgroundService` already wakes hourly; the sweep runs at the end of that tick, selecting Completed entries whose anime is `currently_airing` and resolving their aired counts through the **bulk** reader in one query. Without it, a show the user never opens would never have its re-opening pushed to MyAnimeList.

Either path sets the status to Watching, writes an `ActivityLog` row, and marks the entry `PendingSync`. `CompletedAt` is left as it is — the `list-editing` capability is explicit that a finish date is never cleared automatically, and MyAnimeList itself keeps the original finish date across rewatches.

Concurrent reads can race to perform the same flip. The operation is naturally idempotent (its condition is false once applied) and `UserAnimeEntry` already carries a concurrency token, so a loser catches `DbUpdateConcurrencyException` and continues with the value the winner wrote, rather than surfacing an error on a page render.

This only self-advances where AniList has already given us the row for the next episode. Where a show's next-episode instant is unknown, the count cannot move until a refresh fetches it — which is precisely what the `episode-airing-data` capability's three-day recheck cadence for incomplete data exists to do. Known date → the flip lands on the instant; unknown date → the refresh was the blocker either way.

The `currently_airing` restriction is load-bearing, not incidental. `episode-airing-data` states the aired count is *not* clamped by the MyAnimeList total, so a finished anime can legitimately read 13 aired against a cached total of 12; without the restriction such an entry would be bounced out of Completed on every read, forever.

*Alternative considered:* the sweep alone, with no read-path flip. Rejected because it makes what the user sees wait on a mechanism that exists for syncing — the status would sit visibly stale for up to an hour after an episode the app already knows has aired.

*Alternative considered:* derive the re-opened status at read time without persisting it. Rejected because status is a synced field: a status the user sees but MyAnimeList never receives is worse than writing it, and it would leave the stored and displayed values disagreeing.

*Alternative considered:* a dedicated background service for the sweep. Rejected as a second hourly timer doing a subset of the same job; it belongs where the airing data it depends on is refreshed.

### D7: Two DTOs and the editor target gain airing fields

`MyListItemDto`, `AnimeDetailDto`, and `SeriesEntryDto` already carry both airing status and an aired count. Three shapes do not:

- `CurrentlyWatchingItemDto` carries `episodesAired` and a `currentlyAiring` boolean, which cannot distinguish `finished_airing` from `not_yet_aired` when the aired count is unknown. It gains the raw `airingStatus` string. `currentlyAiring` stays — the blue aired fill is keyed off it and that rendering rule is unrelated.
- `TopAnimeItemDto` carries neither, and the Top anime page opens the entry editor. It gains both, with the aired counts resolved through the bulk reader for the whole 500-row ranking in a single query.
- `EntryEditorTarget` (frontend only) gains `airingStatus` and `episodesAired`, supplied by all five pages that open the editor, so the overlay can gate its own fields without a fetch of its own.

### D8: Rejections are 400s, distinguishable by message

Each new guard throws its own exception type from `Services/Entries/`, joining `CannotCompleteUnknownEpisodeCountException` in `EntriesController`'s mapping to `400 Bad Request` with a message naming the reason. The frontend does not branch on them — its own gating means it should never provoke one — but a rejected save must say why rather than failing opaquely.

## Risks / Trade-offs

**An anime with stale `not_yet_aired` metadata and no AniList rows loses its progress row.** → The aired-count branch of D1 overrides status the moment AniList has anything, and the detail page's on-demand "Refresh data" action refreshes both the MyAnimeList record and that anime's airing rows, so the user has a direct remedy on the page where they would notice.

**Entries that already violate the new rules keep values they can no longer be given.** → Intentional (Non-Goals). D5's clear-always-allowed asymmetry means every such entry can be walked back by hand, and nothing about the entry stops rendering.

**Re-opening writes from a page-render path (D6).** → The write is conditional, idempotent, guarded by the existing concurrency token, and needs no extra query because the aired count is already resolved on those reads. It schedules a debounced sync rather than calling MyAnimeList inline, so the repository contract's "never call the MAL client on a render path" still holds.

**MyAnimeList learns of a re-opening late for a show the user never opens.** → That is exactly what the hourly backstop sweep covers; the read path is what makes it immediate for anything actually being looked at.

**Re-opening writes a status change the user did not make, which then syncs to MyAnimeList.** → This is the requested behaviour, and it is logged as activity like every other change, so it is visible in the profile feed rather than silent.

**The predicate is implemented twice (D2).** → Bounded to one helper on each side, cross-referenced by comment, and covered by tests on both: the service tests assert the rejections, the helper is a pure function.

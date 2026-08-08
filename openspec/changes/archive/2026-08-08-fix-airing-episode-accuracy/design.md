## Context

`EpisodeScheduleService` is the single entry point behind every aired-count and schedule read (dashboard, airing grid, detail page, my-list, and the episode-watched cap in `UserAnimeEntryEditService`). It has two paths:

1. **Cached AniList data** — `IEpisodeScheduleCache`, a `ConcurrentDictionary<int, IReadOnlyList<EpisodeAiring>>` registered as a singleton. Purely in-memory.
2. **Weekly-cadence estimate** — `EstimateAiredFromCadence` / `EstimateOnLocalDate`, which take MAL's `AiredFrom` + `BroadcastTime` and project one episode every 7 days.

Path 2 runs whenever path 1 has nothing, and that is far more often than it looks:

- The cache is empty at every container start. `EpisodeScheduleRefreshBackgroundService` waits 20s, then walks the tracked list at 4.5s/show — a 50-show list takes ~4 minutes to warm. Every render in that window uses the estimate.
- `AniListClient` fetches only `airingAt_greater: now - 150 days`, one page of 50. Any date outside that window falls through to the estimate — which is exactly what the Airing page's past view does when you scroll back.
- `EpisodeScheduleService.ResolveOnLocalDate` explicitly falls through to the estimate for dates outside AniList's coverage window.

This explains every reported symptom. One Piece premiered 1999-10-20, so `daysSincePremiere / 7 + 1` yields a four-digit number that has nothing to do with reality, and it changes as the reference date changes — hence ~1,100 one week and 1,300 the next. A show on hiatus keeps accruing a phantom episode per week. "Episode 15" three weeks running is the reverse case: `Math.Min(episode, TotalEpisodes)` clamping the projection to a MAL total. And the "self-corrects, then drifts again" pattern is the cache warming after boot and being lost on the next restart.

Two decisions are already fixed by the requester:

- **AniList owns episode timing only.** Aired-so-far count, per-episode air instants, next-episode instant. MAL keeps `TotalEpisodes`, `AiringStatus`, and all static metadata.
- **Full history is backfilled**, and a past week AniList has no data for renders empty rather than guessing.

## Goals / Non-Goals

**Goals:**

- Every aired-episode count and schedule slot traces to a stored row that AniList confirmed, or is reported as unknown. No number is ever derived from elapsed time.
- Counts survive a container restart unchanged.
- The Airing page's past view is stable: the same past week renders identically today and next month.
- Refresh happens when data can actually have changed, not on a fixed timer, and an intermittently-running Docker container cannot silently skip a day.
- Existing bad data in the DB is corrected on first boot after deploy, not just going forward.

**Non-Goals:**

- Replacing MAL as the metadata source for anything other than episode timing. `TotalEpisodes` still gates completion (`CannotCompleteUnknownEpisodeCountException`) and still renders as the denominator in progress bars.
- Reconciling AniList's episode numbering against MAL's when they disagree (recap/special numbering). AniList's numbers are taken as given for timing.
- Any change to the weekly grid's layout, week navigation, or day-column rules — `airing-schedule`'s presentation requirements stand.
- Per-episode titles, thumbnails, or fillers. Only number + air instant.

## Decisions

### 1. A persisted table replaces the in-memory cache

New entity `EpisodeAiring`, keyed `(AnimeId, Episode)`:

| Column | Type | Notes |
| --- | --- | --- |
| `AnimeId` | `int` | MAL id; FK → `AnimeMetadata`, cascade delete |
| `Episode` | `int` | AniList's episode number |
| `AirsAtUtc` | `timestamptz` | AniList's `airingAt`, converted from unix seconds |
| `FetchedAt` | `timestamptz` | When this row was last written |

Composite PK `(AnimeId, Episode)`; secondary index `(AnimeId, AirsAtUtc)` for the week-range query.

*Why a table over the singleton dictionary:* the dictionary is the direct cause of the "self-corrects then drifts again" behaviour. It also cannot serve the past view, since it only ever holds the last fetched window. A table makes the read path a range query and removes the warm-up window entirely.

*Alternative rejected:* keeping the dictionary and only persisting a snapshot. Adds a serialization layer for no benefit over letting Postgres do the range query.

### 2. The table stores future scheduled episodes too, and "aired" is a time predicate

The requester's phrasing is "one row per confirmed-aired episode", but the same rows must feed the Airing page's *future* weeks and the currently-watching countdown. Storing only past episodes would force the future half of the UI back onto an estimate.

So: store every episode AniList reports, past and future. `AirsAtUtc <= now` is what makes a row *aired*. This keeps one source for all three reads:

- `EpisodesAiredAsOf(anime, now)` → `MAX(Episode) WHERE AnimeId = @id AND AirsAtUtc <= @now`, `null` when no rows exist at all.
- `NextAiringInstant(anime, after)` → `MIN(AirsAtUtc) WHERE AnimeId = @id AND AirsAtUtc > @after`.
- Week grid → all rows for the tracked set where `AirsAtUtc` falls in the week's local-date range.

A future row's `AirsAtUtc` is AniList's announced time and can move; overwrite semantics (decision 4) handle that.

### 3. The estimator is deleted, not demoted

`EstimateAiredFromCadence`, `EstimateOnLocalDate`, `EstimateLastLocalDate`, `HasKnownUpcomingEpisode`, and `MinimumEpisodeEstimate` are removed from `EpisodeScheduleService`. `IEpisodeScheduleCache` / `EpisodeScheduleCache` are deleted along with their DI registration.

*Why deletion over keeping it as a fallback:* a fallback that only fires when data is missing is indistinguishable, from the user's side, from the bug still being present — and "missing data" is the common case for exactly the shows that are wrong (long-runners, hiatus shows). Leaving it in means the 1,100→1,300 jump stays reachable by scrolling far enough back.

Consequence: `EpisodesAiredAsOf` returns `null` more often. Every caller already handles `null` — `MainDashboardService` passes it straight into the DTO, `UserAnimeEntryEditService` falls back to `anime.TotalEpisodes` for the cap, `AnimeDetailDto.FromEntity` takes it as nullable. The frontend must render "unknown" rather than 0 (see decision 9).

### 4. Overwrite is delete-then-insert per anime, in one transaction

A refresh for anime *X* replaces X's rows wholesale: `DELETE FROM EpisodeAiring WHERE AnimeId = X`, then insert everything the fetch returned, inside a single transaction.

*Why wholesale over upsert-by-key:* AniList renumbers episodes (a recap gets absorbed, a split cour restarts numbering). An upsert leaves orphaned rows from the old numbering behind, and those orphans are precisely the kind of stale record that produces a wrong `MAX(Episode)`. Wholesale replacement makes the table a faithful mirror of AniList's current answer.

**Guard:** a fetch that returns zero rows does *not* delete existing rows. AniList 404s, rate-limits, and transient GraphQL errors all surface as an empty list from `AniListClient`; treating that as "this show has no episodes" would wipe good data. Only a fetch that returned at least one row replaces anything.

### 5. Full-history paging, with the AniList id cached

`AniListClient.GetAiringScheduleAsync` changes in three ways:

- Drop the 150-day `airingAt_greater` floor when doing a full fetch; page from the beginning.
- Request `pageInfo { hasNextPage }` alongside `airingSchedules` and loop pages until exhausted. `perPage: 50` stays (AniList's max is 50 for this connection in practice).
- Resolve `Media(idMal:)` once and return AniList's `id`, `status`, and `nextAiringEpisode { episode airingAt }` in the same call.

Currently every refresh spends two calls per anime — an id lookup and a schedule fetch. The AniList id for a MAL id never changes, so it is stored (decision 6) and the lookup is skipped on subsequent refreshes. That roughly halves steady-state call volume, which pays for the extra paging during backfill.

Paging cost: One Piece ≈ 1,140 episodes ≈ 23 pages. Every other show in a typical list is 1–2 pages. The existing 4.5s inter-request pacing and the client's 429 back-off are kept and applied per *request*, not per anime.

### 6. AniList sync state lives in its own table, not on `AnimeMetadata`

New entity `AnimeAiringSync`, PK `AnimeId`:

| Column | Purpose |
| --- | --- |
| `AniListId` | Cached `Media.id`; `null` when AniList has no entry for this MAL id |
| `LastFetchedAt` | Last successful fetch for this anime |
| `NextAiringEpisodeAtUtc` | AniList's `nextAiringEpisode.airingAt`, or `null` |
| `HasCompleteData` | False when `nextAiringEpisode` is absent while the show is still airing |
| `NextRecheckAtUtc` | When this anime is next due out-of-band (decision 8) |

*Why not columns on `AnimeMetadata`:* that entity's doc comment declares it a "cached copy of a MAL anime record", and `MalMappingExtensions.ApplyTo` overwrites its fields wholesale on every MAL refresh. AniList state living there would either be clobbered or force a carve-out in the mapper. A separate table keeps the MAL mirror clean.

A `null` `AniListId` with a non-null `LastFetchedAt` records "AniList doesn't know this MAL id" so the id lookup isn't retried every pass.

### 7. Refresh triggers replace the fixed 20s + 6h loop

`EpisodeScheduleRefreshBackgroundService` becomes a **single hourly tick** that evaluates due work rather than a fixed-interval refresher. New singleton row `AiringRefreshState` holds `LastSuccessfulPassAt` and `BackfillCompletedAt`.

Each tick, in order:

1. **Backfill** — if `BackfillCompletedAt` is null, run the full-history pass over every tracked anime, then stamp it. Runs once ever.
2. **Daily pass** — if `LastSuccessfulPassAt` is null, or is more than 24h ago, or falls on an earlier local calendar day than today, refresh all my-list anime whose MAL `AiringStatus` is `currently_airing` or `not_yet_aired`, then stamp `LastSuccessfulPassAt`.
3. **Out-of-band rechecks** — refresh any anime whose `NextRecheckAtUtc <= now` that step 2 didn't already cover.
4. **Season boundary** — if `SeasonCalendar.GetSeasonFor(today)` differs from the season recorded at the last pass, force a pass regardless of step 2's outcome.

Boot is not special-cased: the tick runs immediately on startup, and the `LastSuccessfulPassAt`-not-today condition in step 2 is what makes a boot refresh happen. That is the same rule the requester specified for boot, expressed once instead of twice.

*Why elapsed-time over cron:* stated in the request — the container isn't always running, so a fixed time-of-day fires into a stopped process and the day is lost. Elapsed-time-since-last-success self-corrects whenever the container happens to be up.

**Add-to-list trigger:** `UserAnimeEntryEditService.UpdateEntryAsync` already knows `isNew`. When `isNew` and the anime's `AiringStatus` is `currently_airing` or `not_yet_aired`, it enqueues that anime for immediate refresh. This is fire-and-forget through a small trigger singleton (the same pattern as the existing `IImportTrigger` / `IResyncTrigger`) so the edit request doesn't block on an AniList round-trip.

**Manual refresh:** `MetadataRefreshController.RefreshOne` calls the airing refresh for that one anime synchronously after the MAL refresh, so the detail page's reload shows corrected numbers immediately. A failed AniList fetch is logged and does not fail the request — the MAL half already succeeded.

### 8. Incomplete-data recheck cadence

`NextRecheckAtUtc` is recomputed after every fetch for that anime:

- AniList reported `nextAiringEpisode.airingAt = T` → checkpoints are `T - 30d`, `T - 7d`, `T`. `NextRecheckAtUtc` = the earliest checkpoint still in the future.
- `now` is past `T` and no newer confirmed data arrived, **or** AniList reported no `nextAiringEpisode` while the show is still airing → `NextRecheckAtUtc = now + 3d`, and `HasCompleteData = false`.
- Show is `finished_airing` and its last stored episode is in the past → `NextRecheckAtUtc = null` (never due out-of-band).

Note the daily pass (step 2) already refreshes every airing/upcoming anime more often than every 3 days, so in normal operation the checkpoints never fire on their own. They earn their keep when the daily pass is failing for one specific anime — an AniList 404 or a rate-limit that only hit that show — because `NextRecheckAtUtc` is per-anime while `LastSuccessfulPassAt` is per-pass. This is the "no `nextAiringEpisode` → start rechecking immediately instead of waiting a month" behaviour the request asked for, expressed as a queue.

### 9. Read path and the unknown count

`EpisodeScheduleService` keeps its `IEpisodeScheduleService` interface unchanged so the seven call sites don't churn, but becomes a thin reader over `IEpisodeAiringRepository`. It stops being constructed per-anime-per-date:

`AiringScheduleService.GetWeekAsync` currently loops every my-list entry × 7 dates, calling `ResolveOnLocalDate` each time. That becomes **one** query — all rows for my-list anime with `AirsAtUtc` inside the week's local bounds — grouped into day-columns via `IBroadcastLocalTimeConverter.GetLocalDate`. The week's UTC bounds are derived by converting the local Monday 00:00 and the following Monday 00:00 through `Europe/Helsinki`, so DST-shifted weeks stay 7 local days.

The MAL-total clamp (`Math.Clamp(count, 0, total)`) is dropped. A confirmed AniList episode 13 for a show MAL still lists as 12 episodes is real data; clamping it is the mechanism behind the frozen "Episode 15". The `finished_airing` shortcut that returns `TotalEpisodes` directly is also dropped — a finished show's rows are complete, so `MAX(Episode)` is already the answer, and the shortcut would reintroduce a MAL value into an episode-count field.

Frontend: `HomePage.tsx` and `AiringPage.tsx` must render a null aired count as an explicit unknown marker (an em dash), never as `0`. The existing "slot with an unknown episode number" placeholder in the `airing-schedule` spec already covers the grid's case.

## Risks / Trade-offs

- **A tracked show AniList has no entry for disappears from the weekly grid entirely.** Previously it appeared with an estimated episode number. → The backfill logs every tracked anime that returned zero rows, with its title and MAL id, so coverage is visible in one place after first boot. If the list turns out to be long, re-adding MAL-broadcast-slot placement with the episode number omitted is a contained follow-up — the grid already supports episode-less slots.

- **AniList's historical coverage for pre-2010 long-runners may be sparse.** One Piece's early episodes may have no `airingSchedules` entries. → Weeks with no rows render empty, which is the requested and honest behaviour. The *current* count is unaffected: it reads `MAX(Episode) WHERE AirsAtUtc <= now`, which only needs the recent rows to be right.

- **Backfill is the heaviest AniList usage this app has made.** A 60-show list with one long-runner is roughly 90 requests. → Paced at 4.5s/request (~13 req/min against a 30 req/min limit) the worst case is ~7 minutes, run in the background at low priority, resumable because `BackfillCompletedAt` is only stamped after the whole pass succeeds. The 429 back-off already in `AniListClient` is retained.

- **Episode numbering disagreements between AniList and MAL become visible.** A progress bar can now read "13 / 12". → Accepted and out of scope per Non-Goals; the denominator stays MAL's because it also gates completion. Worth watching after deploy.

- **Deleting the estimator changes the episode-watched cap.** `UserAnimeEntryEditService` caps at `EpisodesAiredAsOf ?? TotalEpisodes`; with more nulls, more shows fall back to the total, which is a looser cap. → Strictly safer than the current behaviour, where a wrong estimate could *block* a legitimate edit (the frozen "Episode 15" case would cap a user at 15).

- **Delete-then-insert briefly empties a row set.** A concurrent read during the transaction could see no rows. → Postgres's default read-committed isolation means the delete isn't visible to other transactions until commit, so a concurrent reader sees either the old set or the new one.

## Migration Plan

1. One additive EF migration: `EpisodeAiring`, `AnimeAiringSync`, `AiringRefreshState`. Nothing dropped or altered. `db.Database.Migrate()` in `Program.cs` applies it on boot as it does today.
2. First boot after deploy: `BackfillCompletedAt` is null, so the hourly tick runs the full-history backfill immediately. Until it finishes, aired counts read as unknown rather than wrong — a deliberate choice over showing stale estimates during the window.
3. Rollback: revert the code and the DB keeps three unused tables. No data loss, since nothing existing is modified. Reverting *after* backfill and re-deploying re-runs it only if `AiringRefreshState` was also dropped.

## Open Questions

- Should the backfill be resumable at per-anime granularity (stamping `LastFetchedAt` as it goes and skipping already-fetched anime on restart) rather than all-or-nothing? Proposed: yes, since `AnimeAiringSync.LastFetchedAt` gives it for free — but the all-or-nothing `BackfillCompletedAt` stamp is what guarantees no anime is silently skipped. Both, then: skip anime already fetched this backfill, stamp completion only when every one has a row.
- `BroadcastLocalTimeConverter` hardcodes `Europe/Helsinki`. Unchanged by this design, but every week-boundary conversion now depends on it. Left as-is.

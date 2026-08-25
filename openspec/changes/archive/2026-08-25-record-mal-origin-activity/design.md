## Context

`ActivityLog` is the only chronological record of what happened to my list — `UserAnimeEntry` holds current state and nothing else. Both reporting surfaces read it through one repository: `ActivityLogRepository.GetRecentAsync` feeds `ProfileService.BuildActivityFeed` (the "Latest updates" box, a 400-row window collapsed to 20 rows), and `GetAllAsync` feeds `GetActivityHistoryAsync` (the full history overlay, collapsed only by episode-run and completion-score merging). A third reader, `GetEpisodeProgressInRangeAsync`, backs the recaps.

Two services write to it today. `UserAnimeEntryEditService` builds a `changes` list as it applies each field's business rules, then writes one row per change — or a single `Added` row when the entry is new — with a display-ready `ChangeDetail` string per change type. `CompletedEntryReopenService` writes a single `StatusChanged` row.

Three services change `UserAnimeEntry` and write nothing:

- **`InitialImportService.RunAsync`** only ever *creates* entries — it skips any anime already in `AnimeMetadata`, and for an anime whose metadata is cached but whose entry is missing it adds just the entry. It never touches an existing entry, so its whole recording story is "an entry appeared".
- **`ReconciliationService.AcceptPendingDiffAsync`** walks the stored diff and assigns all six user fields onto the local entry unconditionally, with no comparison of any kind. The compute step (`RunAsync`) knows which entries differ, but not which *fields* differ, and its `PendingReconciliationDiffEntry` records only the remote values.
- **`ResyncService.RunAsync`** calls `MalMappingExtensions.ApplyTo(MalListStatus?, UserAnimeEntry, …)`, which likewise overwrites all six fields in place, and separately upserts `AnimeMetadata` from a full detail fetch.

The last two therefore need a before/after comparison that does not exist anywhere today — the values are gone the moment `ApplyTo` runs.

`ActivityFeedComposer` parses `ChangeDetail` back into display phrases (`"Episode 7"`, `"Added as Watching"`, `"Watching -> Dropped"`, `"Score 8"`) and falls back to raw text for anything it does not recognise. It is stateless and never migrated, so anything written in a shape it does not parse degrades to the fallback rather than failing — which makes format drift silent, and worth designing against.

## Goals / Non-Goals

**Goals:**

- Every path that lands a change on `UserAnimeEntry` records it, at the granularity a local edit already records — one row per field that actually moved.
- The three MAL-origin paths produce rows that `ActivityFeedComposer`, `BuildActivityFeed`, and `CollapseEpisodeRuns` handle with no changes of their own.
- One place owns the `ChangeDetail` formats, so a second writer cannot drift away from what the composer parses.
- Recap figures are exactly what they are today, before and after the migration.
- No sync decision changes: the same entries are created, skipped, and overwritten as before, in the same order.
- Nothing recorded can push anything back to MyAnimeList.

**Non-Goals:**

- Changing the startup import's trigger, cadence, throttling, or opt-out; the weekly reconciliation cadence; the accept/decline flow; or the corrective re-sync's apply-immediately behaviour.
- Recording removals from a MAL-origin path. No sync path removes a local entry — reconciliation and re-sync only add and overwrite — so there is nothing to record.
- Recording metadata changes (English title, duration, source, broadcast, related anime). That is cache data, not my list; the log is about my relationship with an anime.
- Filtering the history by origin, or offering a "hide MAL changes" control. The marker is a label, not a control.
- Fixing the rewatch demotions this makes visible (see Risks). They are sync decisions.
- Reworking `PendingReconciliationDiff` into a per-field structure. It stays the pre-accept review shape; recording happens where a change reaches the entry.

## Decisions

### D1: Record at the point of application, in each path, rather than by observing the database

The alternative to touching three services is to detect changes centrally — an EF `SaveChanges` interceptor over `UserAnimeEntry`, or a Postgres trigger writing the log. Rejected for both reasons that matter here:

- **Origin is unknowable from the mutation.** An interceptor sees a changed entry, not who changed it; it would need an ambient "current sync path" the three services set anyway, which is the same three edits with a harder-to-follow control flow.
- **The mapping is not mechanical.** A status landing on `Completed` must be recorded as a completion rather than a status change (D4); a new entry must be one `Added` row rather than six field rows. Those are decisions about meaning, made where the meaning is known.

So: three call sites, each recording what it just applied, alongside the existing `SaveChangesAsync` that persists the change itself. The row goes into the same `SaveChanges` as the mutation it describes, so a failure never leaves a record of a change that did not happen.

### D2: One shared recorder, and one home for the `ChangeDetail` formats

Two new pieces in `Services/Entries/`:

- **`ActivityDetail`** — the `ChangeDetail` format strings, as static helpers (`Added(status)`, `Episode(n)`, `Completed`, `StatusChange(from, to)`, `Score(value)`, `RewatchCount(n)`, `StartDate(date)`, `FinishDate(date)`, `Removed`). `UserAnimeEntryEditService` and `CompletedEntryReopenService` are refactored to call these instead of interpolating inline. Producing the exact same strings is not an optimisation here, it is the correctness condition: `ActivityFeedComposer` parses them, and a MAL-origin row written as `"Episodes watched: 7"` would silently render as raw text and be excluded from the feed's genuine-increase check.
- **`EntryActivityRecorder`** — an `EntrySnapshot` readonly record struct over the six user fields, plus `Added(animeId, status, source, now)` and `Diff(animeId, before, after, source, now)` returning the rows for one entry's application.

`Diff` is pure and takes the snapshot by value, so a caller must capture *before* mutating — which is the whole subtlety of the reconciliation and re-sync paths, and is made unmissable by the signature.

*Alternative considered:* have `MalMappingExtensions.ApplyTo` return the changes it made. Rejected — it is also used to build brand-new entries where every field is trivially "changed", and turning a mapping helper into a change-tracker gives it two jobs.

### D3: `Source` is a column on the existing row, not a parallel structure

`ActivityLog` gains `ActivityChangeSource Source` — `BetterMal`, `MalStartupImport`, `MalReconciliation`, `MalResync` — with the same append-only, never-renumbered discipline `ActivityChangeType` carries, and the same `HasConversion<string>()` storage, so the column reads plainly in the database.

One log with an origin column, rather than a second "sync history" table, is what makes the read side free: the feed, the history, their ordering, their collapsing, and their filters all keep working on one table. A second table would need a merge at read time in two places and would still have to answer the same collapsing questions across both.

The migration adds the column `NOT NULL` with a default of `BetterMal`, which backfills every existing row correctly — they are all local edits — and, on Postgres 11+, without rewriting the table. `BetterMal` being both the default and the pre-existing truth is also what makes D6 behaviour-preserving.

### D4: The change-type mapping is chosen for how the feed reads, not for what changed literally

For an existing entry, comparing snapshot to applied values yields one row per changed field:

| Field | Change type | Detail |
|---|---|---|
| `EpisodesWatched` | `EpisodeIncremented` | `Episode {n}`, with `PreviousEpisodesWatched` set |
| `Status`, landing on `Completed` | `Completed` | `Completed` |
| `Status`, anything else | `StatusChanged` | `{from} -> {to}` |
| `MyScore` | `ScoreChanged` | `Score {n}` / `Score cleared` |
| `StartedAt` | `StartDateChanged` | `Start date {date}` / `Start date cleared` |
| `CompletedAt` | `FinishDateChanged` | `Finish date {date}` / `Finish date cleared` |
| `RewatchCount` | `RewatchCountChanged` | `Rewatch count {n}` |

Two of these rows are load-bearing:

- **Completion.** `BuildActivityFeed` keeps additions, episode increases, completions, score changes, rewatch-count changes, and removals, and drops every other status change. Recording a MAL-origin completion as `StatusChanged` would keep the single most interesting sync event — "MyAnimeList says I finished this" — out of "Latest updates" entirely. `ActivityChangeType.Completed` is not "the user pressed complete"; it is "this entry became completed", which is exactly what happened.
- **`PreviousEpisodesWatched`.** `IsGenuineIncrease` excludes decreases from the feed, and treats a row without the previous value as a best-effort increase. Setting it means a sync that *lowers* a count (a correction on MyAnimeList's side) is recorded in full history but stays out of "Latest updates", matching how a local decrease behaves.

A **new** entry records a single `Added` row naming the status it arrived with, exactly as `UserAnimeEntryEditService` does for a new entry, rather than six field rows against a zero-valued phantom. The feed's membership group then holds one row per anime, and the history reads "Added to list as Completed" rather than a wall of fields.

### D5: Rows for one application are written in the local path's order

`ActivityFeedComposer.FindCompletionScoreMerges` folds a `ScoreChanged` into an adjacent `Completed` when the score row is the *newer* of the two — resolved, for rows sharing a timestamp, by descending `Id`. `EntryActivityRecorder.Diff` therefore emits in the order the local edit service produces: episodes, then status/completion, then dates, then score, then rewatch count. The score row is added last, gets the higher identity, and a MAL-applied "finished it and scored it" collapses into one row exactly as the local equivalent does.

This is the mechanism the local path already relies on today — same `SaveChanges`, same insertion order, same outcome — so it introduces no new assumption about identity generation.

Timestamps: the import and the re-sync save per anime and stamp `DateTimeOffset.UtcNow` per item, so their rows spread naturally. `AcceptPendingDiffAsync` applies a whole diff under one `now`; every row from one accept therefore shares an instant, and `Id` descending orders them — which the repository already applies as its tie-break.

### D6: Recaps read `BetterMal` rows only, filtered in the repository

`GetEpisodeProgressInRangeAsync` gains `&& l.Source == ActivityChangeSource.BetterMal`. Both recap callers (`RecapService`, `RecapAvailabilityService`) go through it, so the rule holds for the figures and for the availability judgement without either service knowing about origins.

The filter belongs in the repository rather than in `RecapWatchLog` because availability is judged by a separate call that never reaches `RecapWatchLog`, and because the query is the cheaper place to drop rows.

Why exclude at all: a synced row's timestamp is when the sync ran, not when the episode was watched. A weekly reconciliation would smear a week's viewing onto one day, and the first corrective re-sync after this lands could drop months of drift into a single afternoon — visible as a spike in "Episodes watched" and "Time spent" for whatever period that afternoon falls in. Since every pre-existing row is `BetterMal` (D3), recap output is bit-identical before and after this change.

*Alternative considered:* count them, on the grounds that episodes watched are episodes watched. Rejected: it changes an existing surface's numbers as a side effect of a logging change, and attributes viewing to the wrong period. If date-accurate MAL progress is wanted later, it needs MyAnimeList's own update timestamps, not ours.

### D7: Three origins in the data, one marker on screen

`ActivityFeedItemDto` carries `Source` through verbatim; the two row renderers map anything other than `BetterMal` to one "via MAL" tag. The reader's question is whether *they* made the change here — which sync path applied it is a debugging question, answerable from the log, and not worth three labels on a crowded row.

The tag renders inside the existing meta line rather than as a new column, so no row grows and the five-whole-rows sizing rule the history overlay follows is untouched.

### D8: The baseline import is "no history exists yet", judged once per run

`InitialImportService.RunAsync` reads `db.ActivityLogs.AnyAsync()` once, before its loop, and skips recording for the whole run when nothing has been recorded.

*Alternative considered:* "the entry table was empty at run start". Rejected because the import is explicitly resumable — an interrupted first import restarts with several hundred entries already present, and would then record the remainder, producing exactly the partial flood the exemption exists to prevent.

*Alternative considered:* a persisted "initial import completed" marker, in the manner of `ReconciliationRunLog`. Rejected as new schema for a corner of a corner, but it is what would make the rule exact.

The accepted cost of the chosen rule: a user who connects MyAnimeList, never edits anything in the app, then adds an anime on MyAnimeList's own site will not get an addition recorded for it on the next startup import, because the log is still empty. It self-heals the moment anything is recorded, and no history is lost that would otherwise be shown — the anime is in the list either way.

### D9: What each path captures, concretely

- **`InitialImportService`** — both creation branches (the full-detail import and the metadata-already-cached entry backfill) record one `Added` row with `MalStartupImport`, gated by D8. Failures already `continue` before the entry exists, so a failed anime records nothing and is retried, unrecorded, next run.
- **`ReconciliationService.AcceptPendingDiffAsync`** — snapshot the local entry *before* the six assignments; after them, `Diff(…, MalReconciliation, now)`. The existing `PendingSync` skip stays exactly where it is and above the snapshot, so a skipped entry records nothing. A brand-new entry records `Added` with the status the diff carried.
- **`ResyncService.RunAsync`** — snapshot before `edge.ListStatus.ApplyTo(entry, now)`; new entries record `Added`; `MalResync` throughout. The `AnimeMetadata` upsert alongside it records nothing (Non-Goals). Its per-anime `try/catch` already skips to the next anime on failure, and the log rows sit inside the same per-anime `SaveChangesAsync`, so a failed anime records nothing.

Nothing in any of the three sets `PendingSync`, and `LastSyncedAt`/`PendingSync` are not diffed fields — they describe the sync, not my relationship with the anime.

### D10: Settings copy is copy, not a new mechanism

The Sync group's two buttons currently sit in a bare button row with no explanation at all, unlike the Data tools group, whose actions use `SettingsAction` (title, hint, run state, button). The explanations go in as static `hint` text through that same component where it fits, and as the group's existing hint paragraph shape where it does not — no tooltips, no expanders, nothing to press to read it, per the spec. The corrective re-sync's existing hint gains the two facts it omits: that it applies immediately with no review, and that it creates entries for anime not tracked locally.

## Risks / Trade-offs

**A large corrective re-sync writes a lot of rows at once** → The feed is bounded by design: `BuildActivityFeed` keeps one row per anime per field group and stops at 20, over a 400-row window. The full history does grow by however many fields genuinely drifted — which is the point of having a history. Nothing is written for an anime whose values already match, so a re-sync run on an already-consistent list writes nothing at all.

**The rewatch demotions become visible** → `ResyncService` overwrites a local `Rewatching` status with MyAnimeList's `watching`, and `AcceptPendingDiffAsync` does the same whenever another field of a `Rewatching` entry differs, both contradicting `mal-write-sync`'s "Reconciliation does not demote a rewatch" — which today constrains only the compute step. After this change each demotion writes a `StatusChanged` row, so the history will show `Rewatching -> Watching` rows that look like a new bug and are not. Not fixed here (it is a sync decision, and out of scope); worth its own change. Mitigating meanwhile: `StatusChanged` is excluded from "Latest updates", so this stays in the full history rather than filling the profile page.

**Format drift between the two writers breaks parsing silently** → D2 puts the formats in one place used by both, and `ActivityFeedComposer`'s fallback means the failure mode is a plainly-worded row rather than an exception. The tests assert the exact `ChangeDetail` strings for MAL-origin rows so drift fails loudly in CI instead.

**The migration runs against a table that only grows** → Adding a `NOT NULL` column with a constant default is metadata-only on Postgres 11+, so it does not rewrite. Rollback is dropping the column; every row that existed before the change is a `BetterMal` row, so nothing is lost by dropping it.

**Recording changes what a sync path does** → The rows are added to the same `DbContext` as the mutation and saved by the same call; no path's control flow, ordering, or skip conditions move. The tests for each path assert the applied entry values are unchanged from today, alongside the new rows.

## Migration Plan

One EF Core migration adding `ActivityLogs.Source` as `text NOT NULL DEFAULT 'BetterMal'`, matching the string conversion used for `ChangeType`. No backfill script: the default is the correct value for every existing row.

Deploy order is irrelevant — the backend writes and reads the column together, and the frontend treats a missing `source` as unmarked, so an older frontend against a newer backend simply shows no tags.

Rollback: drop the column. The only data lost is the origin of rows written after the deploy; the rows themselves, and every surface reading them, keep working.

## Open Questions

- Should the history overlay eventually offer an origin filter ("only my edits")? Deferred until the marker has been lived with — the collapsing rules may make it unnecessary.
- The rewatch demotions in `ResyncService` and `AcceptPendingDiffAsync` want a follow-up change extending the existing "does not demote a rewatch" rule from the compute step to the apply step.

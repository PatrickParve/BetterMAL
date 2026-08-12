## Context

Both activity surfaces render from the same `ActivityLog` table via `ProfileService`:

- `BuildActivityFeed` filters a 200-row window down to 20 "Latest updates" items (`ProfileDto.RecentActivity`).
- `CollapseEpisodeRuns` returns the whole log for the full-history overlay (`GET /api/profile/activity`).

Each row carries a `ChangeType` enum and a free-text `ChangeDetail` written by `UserAnimeEntryEditService` ("Added as Watching", "Episode 5", "Score 8", "Removed from list", "Watching -> OnHold", …). The frontend then prints `CHANGE_TYPE_LABELS[changeType]` and appends `— changeDetail`, which is where the duplication comes from: the detail almost always already contains the label's words.

Constraints that shape the design:

- The log is append-only and holds rows written by every earlier version of the app; `ChangeDetail` formats have varied, and pre-migration rows have no `PreviousEpisodesWatched`. Nothing may be rewritten or migrated.
- A completion and its score are not always one log write. The entry editor saves them in one request (identical `Timestamp`, two rows), while the completion score prompt (`CompletionScoreOverlay`) fires a second `PUT` seconds later (later `Timestamp`).
- The full history endpoint already returns every row in one payload, so the new search and date filters have a complete client-side dataset to work with.

## Goals / Non-Goals

**Goals:**

- One composed phrase per activity row, shared by "Latest updates" and the full history.
- Rewatch-count changes visible in "Latest updates".
- A finished anime reads as a single `Completed — Score N` row on both surfaces.
- "Latest updates" shows one row per anime per field group (its newest value), so re-edits don't flood it; the full history still shows every step.
- Title search and a from/to date range in the full-history overlay.
- No schema change, no migration, no regression for rows written before this change.

**Non-Goals:**

- Changing which change types "Latest updates" carries beyond adding rewatch-count changes (status and date changes stay history-only).
- Server-side search/pagination for the history endpoint.
- Reworking how edits are written or what `ActivityLog` stores.

## Decisions

### Compose the phrase server-side, expose it as `Summary`

`ActivityFeedItemDto` gains `string Summary`. `ProfileService` fills it; `ProfilePage` and `EditHistoryOverlay` render `item.summary` verbatim. `ChangeType` and `ChangeDetail` stay on the DTO (cheap, and `changeType` remains available for styling), but no longer drive row text — `CHANGE_TYPE_LABELS` in `frontend/src/utils/anime.ts` is deleted along with its two call sites.

*Why not compose in the frontend:* the merging this change introduces (a score folded into a completion) and the per-field collapsing both operate on groups of log rows, which only the server sees. Composing text in the frontend would mean re-deriving the grouping the server already did, in two components, and keeping the two surfaces' wording in sync by hand.

### One composer, both surfaces

A new `Services/Profile/ActivityFeedComposer.cs` (static, no dependencies) owns everything text- and grouping-related:

- `Summarize(ActivityLog log, int? mergedScore, EpisodeRun? run)` → the phrase.
- `FieldGroupOf(ActivityChangeType)` → `Progress | Score | RewatchCount | Membership`.
- The completion+score merge rule (below), so both builders apply it identically.

`ProfileService.BuildActivityFeed` and `CollapseEpisodeRuns` keep their distinct filtering/collapsing policies and call into the composer for text and grouping.

### Phrase table, parsed defensively from `ChangeDetail`

| Change type | Phrase | Source |
| --- | --- | --- |
| `Added` | `Added to list as Watching` | status parsed out of `"Added as {status}"`, humanized |
| `EpisodeIncremented` | `Episode 5` / `Episodes 4-8` | episode number parsed from `"Episode N"`; range from the collapsed run |
| `Completed` | `Completed` / `Completed — Score 8` | merged score, when one applies |
| `ScoreChanged` | `Score 8` / `Score cleared` | detail already in this form |
| `RewatchCountChanged` | `Rewatch count 2` | detail already in this form |
| `StatusChanged` | `Watching → On hold` | both sides parsed out of `"{from} -> {to}"`, humanized |
| `Removed` | `Removed from list` | constant |
| `StartDateChanged` / `FinishDateChanged` | `Start date 2026-01-02` / `Finish date cleared` | detail already in this form |

Every parser falls back to the raw `ChangeDetail` (or, if that is null, a plain humanized change-type name) when the stored text doesn't match the expected shape. That is what keeps rows written by older versions rendering sensibly without a migration.

Status names are humanized with a `WatchStatus` → display-text map mirroring the frontend's `STATUS_LABELS` (`OnHold` → "On hold", `PlanToWatch` → "Plan to watch"). `UserAnimeEntryEditService` keeps writing its current detail strings — nothing about the write path changes.

*Why parse rather than restructure the log:* adding typed columns (old/new value) would be cleaner for new rows and useless for every existing one, which is most of the table. Parsing with a fallback covers both at a fraction of the cost.

### Completion + score merge rule

A `ScoreChanged` row merges into a `Completed` row when, for the same anime:

1. the two rows are adjacent in that anime's own event stream (no other logged change for that anime between them), and
2. the score's timestamp is at or after the completion's and within **5 minutes** of it.

Both write paths land inside that: the editor writes both rows with the same `now`, and the completion prompt follows within seconds. The merged row keeps the completion's timestamp, `Id`, and `ChangeType`; the score row is dropped from the output on both surfaces.

The 5-minute window is a heuristic — a deliberate score change made minutes after finishing an anime will be absorbed into the completion row. That reads correctly anyway ("Completed — Score 8"), so the failure mode is benign.

*Ordering prerequisite:* `ActivityLogRepository` currently orders by `Timestamp` only, so the two rows of a single save have no defined order. Both query methods change to `OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id)`, making same-timestamp rows deterministic (newest-written first) — which the merge and the run-collapsing both depend on.

### "Latest updates": newest row per anime per field group

After type filtering and completion+score merging, the feed walks the window most-recent-first and keeps a row only if `(AnimeId, FieldGroup)` hasn't been seen yet. Groups:

- **Progress** — `EpisodeIncremented`, `Completed`
- **Score** — `ScoreChanged`
- **RewatchCount** — `RewatchCountChanged`
- **Membership** — `Added`, `Removed`

This subsumes the current "consecutive same-anime progress events collapse, completion supersedes the run" logic: a completion is the newest progress row for its anime, so it wins and the increments behind it are dropped without a separate adjacency check. The `IsGenuineIncrease` filter (episode *decreases* never reach the feed) stays as it is.

Grouping `Added` and `Removed` together means an add → remove → re-add cycle shows only the latest membership state, which is the point of the change.

*Window size:* the fetch window grows from 200 to 400 rows. Collapsing is strictly subtractive, so a long binge or an editing session that touches one anime repeatedly could otherwise leave the feed short of its 20 items. The query is a single indexed `ORDER BY … LIMIT`, so the extra rows are cheap.

*Why not a time-bounded rule:* "collapse only re-edits within N minutes" keeps older same-field rows in the feed, which means the feed's contents depend on how long ago each pair of edits happened — hard to predict while looking at it. The chosen rule states plainly: one row per anime per field, newest value.

### Full history: keep everything, merge only the completion

`CollapseEpisodeRuns` keeps its existing behavior (runs of genuine increases collapse into `Episodes a-b`; nothing else is dropped) and additionally applies the completion+score merge. The episode-range row that led into a completion stays as its own row above it, so no episode entry is lost:

```
Completed — Score 8        12:03
Episodes 9-12              12:02
Added to list as Watching  11:40
```

### History filtering runs in the browser

`EditHistoryOverlay` already fetches the entire history on open, so search and date filtering are local state (`search`, `fromDate`, `toDate`) with a `useMemo` over the fetched rows — no endpoint or query-parameter changes, no refetch per keystroke.

- **Search:** case-insensitive substring against both `animeTitle` and `animeEnglishTitle`.
- **Dates:** two `<input type="date">` controls. Each row's timestamp is reduced to its local calendar day (`en-CA` locale → `YYYY-MM-DD`, matching the input's own value format) and compared lexically, so both bounds are inclusive whole days in local time. An empty input leaves that side open.
- **Clear:** a control that resets all three, shown only while a filter is active. State is component-local, so reopening the overlay starts unfiltered.
- **Empty result:** the existing "No activity yet." message is joined by a "No history matches these filters." message for the filtered-to-nothing case.

The filter row sits between the overlay title and the list, inside the existing `modal--wide` layout; the list keeps its `scroll-y` treatment so only the rows scroll.

## Risks / Trade-offs

- **Legacy `ChangeDetail` strings don't parse** → every parser falls back to the raw detail text, so an unrecognized row still renders its stored words rather than an empty or wrong phrase.
- **The 5-minute merge window absorbs an unrelated score change** made shortly after a completion → the merged row still reads truthfully ("Completed — Score 8"), and the full history's own rows are what the user consults for the exact sequence. Accepted.
- **Per-field collapsing hides a genuine older change** — rescoring an anime today hides the score row from last month inside the same window → intended; the full history keeps every step, and that is where the change explicitly puts them.
- **Same-timestamp ordering was previously unspecified** → the new `ThenByDescending(l => l.Id)` fixes an ordering that was already arbitrary; existing rendered order can shift slightly for rows written in the same request, which is a correction rather than a regression.
- **Client-side history filtering scales with history size** → the payload is already sent in full today; filtering is an in-memory pass over a few thousand rows at most, memoized per keystroke.
- **`Summary` duplicates information already in the DTO** → accepted for a stable contract; `changeType` stays available for styling, and the frontend has no composition logic to drift.

## Migration Plan

No data migration and no schema change. Deploy is a normal backend + frontend build: the DTO gains a field (additive, existing clients unaffected), and the frontend starts reading it. Rollback is a plain revert — nothing written during the change's lifetime differs from what the current code writes.

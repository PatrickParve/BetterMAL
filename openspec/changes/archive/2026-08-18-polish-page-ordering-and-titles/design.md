## Context

This change touches four pages and two layers, but it is not one feature — it is six independent polish items that happen to share a release. The technical interest is in deciding, per item, **which layer owns the behaviour**, because this codebase has an established split that is easy to get wrong:

- **Server-owned ordering.** `MainDashboardService` and `ProfileService` already emit their sections pre-ordered (`OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)`, `BuildRewatchedSection`'s `OrderByDescending(RewatchCount)`). Sections whose order is fixed are ordered server-side; only sections with a user-facing sort control (`CurrentSeasonSection`) sort client-side.
- **Client-owned view state.** View controls that must survive back/forward use `useRestorableState`, which writes through to the current history entry's snapshot. `MyListPage` and `ProfilePage` already use it for every one of their controls; `CurrentSeasonSection` is the one control in the app still on plain `useState`, which is exactly the bug.
- **Derived-on-render header state.** `SeriesPage` deliberately derives its completion badge and score averages client-side from the entry arrays (`completionBadge`, `recomputeScores`) so an in-place row edit stays consistent without a refetch. Anything the header shows that an edit can change must stay on that path.

Two figures cross the layer boundary and so need DTO work: the series status string (`SeriesService.ComputeStatus`, mirrored by the `SeriesStatus` union and `SERIES_STATUS_CLASS` in `SeriesPage.tsx`) and the entries-completed stat (`SeriesStatsDto`).

The header work is a third kind of change again: pure presentation, governed by the `page-header-design` capability, which currently names seven pages and excludes the two this change is about.

## Goals / Non-Goals

**Goals:**

- Put each ordering rule in the layer that already owns that section's ordering, rather than adding a second sorting site.
- Bring the `CurrentSeasonSection` sort onto the same restoration mechanism every other view control in the app uses, rather than inventing persistence for it.
- Make the two title treatments conform to the existing page-header vocabulary instead of introducing a third header pattern.
- Keep every series-page figure that an in-place row edit can change on its existing derive-on-render path.
- Keep the new backend logic unit-testable through the seams the project already uses (`internal static` helpers, fake repositories).

**Non-Goals:**

- No change to how the dashboard, profile, or series payloads are fetched, cached, or invalidated. Ordering changes ride the existing reads.
- No redesign of the series hero beyond removing the title from it, and no change to the anime detail body. The score boxes, info grid, and synopsis are untouched.
- No new sort control anywhere. "Currently watching" gets a fixed order, not a user-selectable one.
- No change to the `page-header-design` capability's filter/sort control-height requirement, which is unrelated to titles.
- No visual change to the profile "Most rewatched" strip beyond the order of its tiles.

## Decisions

### 1. Currently-watching order is computed server-side, in an `internal static` helper

`MainDashboardService.GetDashboardAsync` already orders the watching entries before projecting them. The new rule (`EpisodesWatched` descending, then `Anime.Title` case-insensitively) replaces that `OrderBy` in place.

**Why server-side rather than sorting in `CurrentlyWatchingCarousel`:** the carousel is a bounded 5-card strip whose scroll position must survive an increment. Sorting client-side would re-run on every `setDashboard` patch, so incrementing a card could reorder the row under the user's cursor — directly contradicting the existing "Scroll position survives an increment" requirement. Ordering server-side means the order is fixed for the lifetime of a loaded payload.

**This gives the requested reorder timing for free, with no client-side sort suppression.** The rule the user asked for — never reorder while I am looking at the row, do reorder when I come back to the page — falls straight out of where the sort lives:

- `handleEpisodesWatchedChange` in `HomePage.tsx` patches `currentlyWatching` with `.map()`, which preserves array order. An increment therefore cannot move a card, because nothing on the client sorts.
- Every path back to the page re-issues `getDashboard`, so the server's new order arrives naturally: a reload and a fresh navbar visit both run `usePageData`'s fresh load, and a back/forward restore seeds from the snapshot and then runs `runLoad(false)` in the background, replacing the data in place.

The restore path is the one worth being explicit about, because it is where "don't reorder" and "do reorder" meet. `usePageData` paints the snapshot first (old order, no loading state), then swaps in the refreshed payload — so the user sees the row exactly as they left it and then watches it settle into the new order. That is the desired behaviour, and it is also why the spec's "already ordered on arrival" scenario is scoped to a *fresh* load: on a restore, an in-place reorder after first paint is correct, not a defect. `AnimeCard` is keyed by `animeId`, so React reorders the existing DOM nodes rather than remounting them, and no picture reloads.

**One exemption:** `HomePage` passes `onCompleted={reload}` to the carousel, so confirming a completion prompt reloads the dashboard mid-visit and the row can reorder then. That is not an in-place count edit — the completed anime leaves the section entirely, so the row is being rebuilt around a departing entry rather than nudged by a count change. The spec carves this out explicitly so the two rules do not read as contradictory.

**Why an `internal static` helper rather than an inline `OrderBy`:** `MainDashboardService` has no test file, and standing one up requires fakes for `IUserAnimeEntryRepository`, `IEpisodeScheduleService`, and `IBroadcastLocalTimeConverter` — a lot of scaffolding to assert a comparator. `SeriesService.BuildStats` set the precedent for this exact problem: it is `internal` with a comment saying so specifically so tests can exercise it. Extracting `internal static IEnumerable<UserAnimeEntry> OrderCurrentlyWatching(IEnumerable<UserAnimeEntry>)` lets `MainDashboardServiceOrderingTests` assert the rule against plain `UserAnimeEntry` objects with no fakes at all.

*Alternative considered:* sort in `HomePage.tsx` with a `useMemo`. Rejected for the reorder-on-increment problem above, and because it would split ordering across two layers for one section.

### 2. Tie-breaks sort on the stored title, not the display title

Both orderings tie-break on `Anime.Title` rather than the English-preferring display title. `BuildRewatchedSection` already documents this choice ("over the raw `Title` rather than display title so the order doesn't shift with title-preference display logic"), and the server does not have `pickDisplayTitle`'s preference logic anyway. Applying the same rule to the currently-watching order keeps one convention across both sections.

*Trade-off:* a user reading English titles sees a tie-break order that is alphabetical by a title they are not looking at. This is pre-existing app-wide behaviour, ties are rare, and diverging here would make the two sections inconsistent with each other for no gain.

### 3. `BuildRewatchedSection` drops the score tie-break rather than reordering around it

The change is one deleted `.ThenByDescending(e => e.MyScore ?? -1)` line. Rewatch count stays the primary key and title becomes the sole tie-break.

**Why remove rather than demote:** keeping score as a *later* key would be indistinguishable from removing it, since title is a total order over the entries — no two entries can tie on both rewatch count and title unless they are the same anime. Removing it is the honest expression of the rule and removes a line that would otherwise read as load-bearing.

The section's own comment block above the method documents the tie-break vocabulary and must be updated with it, or it will describe behaviour the code no longer has.

### 4. The `CurrentSeasonSection` sort moves to `useRestorableState` with no other change

One-line swap: `useState<SortKey>('popularity')` → `useRestorableState<SortKey>('currentSeasonSort', 'popularity')`.

`useRestorableState` seeds from the history entry's snapshot when restoring and falls back to the initial value on a fresh visit, which is precisely the two behaviours the spec requires. The hook reads `usePageState()`, a context available anywhere under the provider — it does not need to be called from a page component, so the sort can stay owned by the section that renders it rather than being lifted into `HomePage`.

**Why the key is `currentSeasonSort` and not `sort`:** snapshot keys are per-history-entry, not per-component, so two controls on one page sharing a key would clobber each other. The home page has no other view control today, but `MyListPage` already demonstrates the collision risk, and a specific key costs nothing.

*Alternative considered:* lifting the sort into `HomePage` and passing it down. Rejected — it moves state away from the only component that uses it purely to satisfy a hook that does not require it.

### 5. `Finished · sequel upcoming` is removed from the vocabulary, not relabelled

`ComputeStatus`'s final line becomes `return anyUpcoming ? "Ongoing" : "Finished";`. The `Upcoming` branch above it is unchanged, so a series with nothing aired yet still reads `Upcoming` rather than being swept into `Ongoing`.

This is deliberately a **removal** from the status vocabulary rather than a rename: the `SeriesStatus` union in `frontend/src/api/types.ts` loses its fourth member, and `SERIES_STATUS_CLASS` in `SeriesPage.tsx` loses its fourth entry. Narrowing the union is what makes the change safe — `SERIES_STATUS_CLASS` is typed `Record<SeriesStatus, string>`, so TypeScript fails the build if the map and the union disagree in either direction. Removing the union member without removing the map entry (or vice versa) is a compile error, not a silent bug.

**Why `Ongoing` and not a new pill state:** the user's framing is that a franchise with a sequel coming is not finished, and `Ongoing` already exists with the right connotation and the right colour treatment (`--status-watching`, green). The old string mapped to the `finished` CSS class, so the pill's colour changes from blue to green along with its text — which is the point.

*Edge case worth naming:* a series whose only members are finished and not-yet-aired now reads `Ongoing` while nothing is actually broadcasting. This is intended. The pill answers "is this franchise done?", not "is something airing this week?"

### 6. Extras-completed is a new DTO field, not derived client-side

`SeriesStatsDto` gains `int ExtrasCompleted`, computed in `BuildStats` as `extraAnime.Count(a => a.UserEntry?.Status == WatchStatus.Completed)` — the exact mirror of the existing `entriesCompleted` line one row above it.

**Why server-side when the page derives its badge client-side:** `BuildStats` already computes `EntriesCompleted` and `ExtrasCount` from the same member lists, so the extras figure is one `Count` beside its main-line twin. Deriving only half the stat on the client would split one figure across two layers.

**Why this does not break the in-place-edit path:** an entry editor save patches `series.extras` through `patchSeriesEntry`, which does not recompute `stats.entriesCompleted` today either — the existing main-line figure is equally stale until reload, and the completion *badge* (the figure users actually watch change) is derived on render and stays live. Adding `ExtrasCompleted` alongside `EntriesCompleted` inherits exactly the freshness the stat already has, introducing no new inconsistency.

*Alternative considered:* deriving both halves on the client from `series.mainLine`/`series.extras`, which would make the stat live under edits. Rejected as scope creep — it changes the freshness of an existing figure the user did not ask about, and the two halves would then disagree with `stats.mainLineCount`/`extrasCount` in the same grid.

### 7. Time watched / time left hide together, guarded against an unknown runtime

The render guard is on the pair, not on each stat, and it excludes the degenerate case:

```
timeLeft = max(0, mainLineRuntimeSeconds - myWatchedSeconds)
runtimeUnknown = mainLineRuntimeSeconds === 0 && hasUnknownEpisodeCounts
showTimeStats = timeLeft > 0 || runtimeUnknown
```

**Why the `runtimeUnknown` carve-out:** `formatRuntimeTotal` already renders that exact condition as the literal word "Unknown" for the runtime stat. Without the carve-out, a series whose runtime nobody knows would compute `timeLeft === 0` and silently hide a real, non-zero time-watched figure — hiding data because data is missing, which is the opposite of the intent. The carve-out reuses a condition the page already tests rather than inventing a new notion of "known".

**Why hide as a pair:** "Time watched: 4d 6h" standing alone next to no "Time left" reads as a rendering bug. The two are one thought, and the user's request named both.

This stays entirely in `SeriesPage.tsx`'s render — no DTO change, and the stat grid's `<div>`s are already individually conditional (`stats.extrasCount > 0`, `highestMalEntries.length > 0`), so this follows the established pattern in that grid.

### 8. Entries completed renders as one stat cell with two labelled figures

The `<dd>` holds a small two-row list (main line, then extras) rather than two `<div>` cells in the grid.

**Why one cell:** the grid is a `<dl>` of stat cells; two cells would put "Entries completed" and "Extras completed" at the same level as "Main series episodes" and "Extras episodes", which are genuinely independent stats. Completion is one question asked of two member sets — the reason the user asked for it in the first place is to see both halves *together*.

The extras row is omitted entirely when `stats.extrasCount === 0`, matching how the grid already omits the extras episodes/runtime cells in that case. A series with no extras therefore shows the stat exactly as it looks today.

### 9. The anime detail title adopts the existing page-header vocabulary; the series title stays beside the picture

Every other page's title already resolves to the same four properties: `margin: 0`, an explicit `font-size` of 24px (header rows with controls) or 28px (standalone header blocks), `font-weight: 600`, `letter-spacing: normal`.

- **Anime detail page** — its header row carries the related-entry links, so it is a controls-sharing row: **24px/600**, matching My List and Search. The existing `.anime-detail-page__top` flex row becomes the header wrapper's content, and the wrapper owns the spacing that fixes the "too high" complaint. `.anime-detail-page__top` also switches from `align-items: flex-start` to `align-items: center` — the old flex-start alignment relied on a hand-tuned `padding-top: 8px` on `.anime-detail-page__related` calibrated for the previous 28px title; once the title dropped to 24px that offset misaligned the related-entry links against it, so the row now centers its children instead of depending on a magic-number offset.
- **Series page** — an earlier version of this change extracted the `Series` label and `<h1>` out of the hero into a standalone header block above it, matching Profile/Settings. Seeing it rendered, that reads worse than the original: it separates the title from the poster it's naming and adds a redundant row. **Reverted** — the title stays exactly where it was, inside `.series-page__header-info` beside the poster, at its original **28px** (this page never carried `font-weight` or the other three properties as a deliberate omission; it's simply unchanged). The `page-header-design` capability's extension to nine pages therefore covers only the anime detail page, not the series page.

The anime detail `h1` rule moves onto the new `.anime-detail-page__header` wrapper (`.anime-detail-page__header h1`) so the header block owns the title's spacing exactly once, as the capability requires. The series page's `h1` rule is untouched.

### 10. No shared header component is introduced

Each page keeps its own header classes rather than extracting a `<PageHeader>` component.

The nine pages' headers differ in what they carry (controls, kind labels, subtitles, borders, nothing) and in their titles' size. The capability itself says the treatments "are not required to match". A component general enough to cover all nine would take enough props to be a styling passthrough, and the two pages this change touches are the last two to be brought into the pattern — there is no third caller coming. Consistency here is enforced by the spec and by the shared CSS values, not by a shared component.

## Risks / Trade-offs

**Narrowing the `SeriesStatus` union breaks any code still producing the old string** → This is the mitigation, not the risk. `SERIES_STATUS_CLASS` is a `Record<SeriesStatus, string>`, so `tsc -b` fails if the map and the union disagree. The only producer is `ComputeStatus`, which is changed in the same task. There is no persisted status value — it is computed per request — so no stored data carries the old string.

**Reordering "Currently watching" changes a row the user has muscle memory for** → Unavoidable and intended; it is the request. The order is now derived from a number the user controls, so it is predictable, and the carousel's scroll position still survives increments so day-to-day interaction is unchanged.

**Hiding "Time watched" on completed series removes a figure some users may want** → Explicitly requested, and the information is not lost: "Main series runtime" sits in the same grid and equals the time watched once the series is complete. The `runtimeUnknown` carve-out protects the one case where the two would genuinely differ.

**`Ongoing` for a series with nothing currently broadcasting could read oddly** → Accepted per decision 5. The personal badge beside the pill (`Completed` / `Caught up` / `N behind`) still reports where the user stands, and it is computed independently of the pill, so the header does not become ambiguous. Note that `completionBadge` gates its `Completed` state on `series.status === 'Finished'` — a series that flips from `Finished` to `Ongoing` under this change will stop showing the `Completed` badge and fall through to the `Caught up` / behind-count path. That is consistent (the series is no longer finished, so the user cannot have completed it), but it must be verified rather than assumed.

**Removing the scored-count from the chips loses the "computed over how many entries" context** → Accepted; the watch-order list below names every entry and its score, so the count is recoverable by looking at the thing it summarised. `SeriesAverageDto` keeps `scoredCount`/`totalCount` — only the rendering drops them — so restoring the suffix later needs no backend change.

## Migration Plan

No data migration, no API contract break for any external consumer (the API is consumed only by this repo's frontend), and no feature flag.

`SeriesStatsDto` gains a field, which is additive on the wire; the frontend's `SeriesStats` type gains it in the same change. Deploy is a single build of both projects — the frontend and backend must ship together because the `SeriesStatus` union narrowing assumes the backend has stopped emitting the old string. Rolling back is a revert of both.

Verification is `dotnet test` for the backend (per the memory note, compile via the `sdk:10.0` Docker image), `npm run build` for the frontend type-check (per the memory note, under nvm's Node 22), `npm run lint`, and a manual pass over the four pages, since the frontend has no test harness.

## Open Questions

None blocking. Three points were resolved with the user before this design was written: progress means raw episodes watched with the highest first (not a percentage); entries-completed renders as two labelled figures in one stat (not two cells or a combined total); and both titles move into dedicated header blocks (rather than being restyled in place).

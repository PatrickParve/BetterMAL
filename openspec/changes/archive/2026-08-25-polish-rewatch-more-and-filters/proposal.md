## Why

Six unrelated-looking annoyances share one root cause each, and all of them make the app quietly tell the user something untrue.

An in-progress rewatch is invisible to the profile's "Most rewatched" Series scope: entering **Rewatching** resets episodes-watched to 0 and only *increments* the rewatch count once a run finishes, so the hours currently being spent rewatching count for nothing until the run ends. The same reset makes the series page claim I am `N behind` on a franchise I have seen in full and am part-way through watching again — the one state where the badge is most obviously wrong.

The series page's More section throws its view state away on every navigation, so opening a group, going to an entry, and coming back drops the user back on the default "in my list" filter with the group they opened re-hidden; and even when they do open a group, the group's tiles render below the fold with nothing scrolling them into view. Meanwhile the extras a franchise actually has are incomplete: `pv` entries — Jujutsu Kaisen's among them — are linked to their show by MAL's `other` relation, which is traversed only for `music`, and only when the far end already happens to be cached, so a franchise's promotional videos are unreachable in practice.

My list forces one status at a time, so "what am I part-way through" (Watching + Rewatching + On hold) cannot be asked. And every week stepped through on the Airing page pushes a browser history entry, so leaving the page after browsing eight weeks means eight presses of Back.

## What Changes

**Rewatch counting**
- An entry marked **Rewatching** contributes its current episodes-watched as *rewatch* time in the profile's "Most rewatched" Series scope, on top of the completed runs its rewatch count already accounts for. An entry rewatching its first run — rewatch count still 0 — now appears in the scope at all, where today it is omitted entirely.
- A main-line entry marked **Rewatching** counts as **fully watched** wherever the series page and the series browser count my watched main-line episodes: the personal badge's precedence (so no more `N behind` on a franchise I am rewatching, and no `Unwatched`/`Dropped` misreads), the header progress bar and its watched figure, time watched, and the browser's My-progress sort. `Rewatching` also satisfies the badge's `Completed` rule alongside `Completed`, so starting a rewatch of a finished franchise no longer downgrades its badge.

**Series page — More section**
- The More section's view state — the "in my list" filter, per-group collapse, and per-group filter exemptions — becomes restorable page state, restored on back/forward exactly as the page's other view controls already are. A fresh visit still opens on the documented default.
- Opening a More group scrolls the page so that group's heading sits at the top of the viewport, stopping at the bottom of the page when the group is too near the end to reach the top.
- **`pv` extras join the series.** The `other` relation is traversed when exactly one of its ends is a `pv` entry, on the same terms as `music` today, and `pv` becomes its own More group with its own place in the fixed group order. `pv` entries are never main-line eligible.
- A bounded **probe budget** lets a build spend a small number of fetches learning the media type of `other` far ends that have no cached row at all, which is the only way an uncached `pv` (or `music`) entry can ever be recognised. A probe caches the row permanently, so each `other` far end is probed at most once across all builds; a build that exhausts the budget marks the series partial and finishes the job on the next visit. This closes the same hole for `music`, which today also only appears if something else happened to cache it.

**My list**
- The status filter tabs become multi-select: any combination of Watching, Rewatching, Completed, Plan to watch, On hold, and Dropped can be selected at once, and the list shows the union. **All** is exclusive — selecting it clears every other selection, and selecting any status clears All. Deselecting the last remaining status returns to All rather than showing an empty list.

**Airing page**
- Week navigation — the `‹ current ›` buttons and the month/year jump selectors — **replaces** the current history entry instead of pushing a new one, so Back leaves the Airing page for wherever the user came from regardless of how many weeks they stepped through. The week stays in the URL, so it is still shareable and still survives back-navigation from an anime's detail page, and the page holds its scroll position across a week change instead of jumping to the top.

## Capabilities

### New Capabilities

*(none — every change modifies an existing capability)*

### Modified Capabilities
- `profile-stats`: a member's rewatch time in the "Most rewatched by series" scope includes an in-progress rewatch's episodes-watched, and a member rewatching its first run is eligible for the scope.
- `series-page`: the personal badge's precedence and the page's watched-episode figures treat a `Rewatching` entry as fully watched; the More section's view state is restorable and opening a group scrolls it to the top; `other` edges are traversed for `pv` as well as `music`; `pv` is a More group and is main-line ineligible; a bounded probe budget resolves uncached `other` far ends.
- `series-browser`: the card's progress badge restates the series page's precedence inline, so it takes the same `Rewatching` clause; its My-progress sort takes the same watched-episode treatment.
- `library-views`: the my-list status filter becomes a multi-select with All as an exclusive option.
- `airing-schedule`: week navigation replaces the current history entry rather than pushing one, and holds scroll position.
- `page-state-restoration`: the series page's restorable state is stated to include its More-section controls.

## Impact

**Backend**
- `Services/Series/SeriesRelations.cs` — `IsTraversableMusicEdge` generalises to `IsTraversableOtherEdge` over a `{music, pv}` set; the incoming-edge LINQ predicate in `SeriesGraphBuilder` needs an EF-translatable equivalent.
- `Services/Series/SeriesGraphBuilder.cs` — probe pass for uncached `other` far ends with its own budget; `pv` added to main-line ineligibility; `ClassificationRevisedAt` bumped so stored series rebuild on next read.
- `Services/Series/SeriesMediaTypeOrder.cs` — `pv` gets its own group between Music and TV.
- `Services/Series/SeriesRankingIndex.cs` — `MemberRewatchSeconds` adds an in-progress rewatch's episodes; `ProgressBadge` and the main-line watched-episode sums treat `Rewatching` as fully watched.
- `Services/Series/SeriesService.cs` — the same `Rewatching` treatment for `myWatchedEpisodes`/`myWatchedSeconds` on the series DTO.
- `Services/Watching/WatchMath.cs` — a shared helper for "rewatch episodes including an in-progress run", so the profile and the series index cannot disagree.

**Frontend**
- `pages/SeriesPage.tsx` — More-section state via `useRestorableState`; scroll-to-heading on group open; `completionBadge` mirrors the backend's `Rewatching` clause.
- `pages/MyListPage.tsx` — `statusFilter` becomes a `WatchStatus[]`; tab rendering, grouping, and the focus-seed deep links follow.
- `pages/AiringPage.tsx` — `setSearchParams(..., { replace: true, state: { keepScroll: true } })`.
- `components/SeriesCard.tsx` / `api/types.ts` — no shape change; the badge values are already server-computed.

**Data**
- No schema change. Stored series rebuild themselves through the existing classification-revision mechanism.

**Non-goals** (flagged, deliberately not in scope)
- The score-reveal rules that treat `Completed`/`Dropped` as "settled" are not extended to `Rewatching`, even though a rewatched entry is equally settled.
- The profile's per-media-type "Most rewatched" scopes keep ranking by the integer rewatch count; a partial run has no place in an integer badge.
- `cm` (commercial) entries stay outside the series, and stay outside `other` traversal, though the traversal set is written so adding them later is a one-line change.

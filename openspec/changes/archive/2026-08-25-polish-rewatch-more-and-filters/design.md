## Context

Six user-reported annoyances across four pages. Three of them (the profile's rewatch time, the series badge's `N behind`, the My-list status tabs) are counting or selection rules; three (More-section state, the More-group scroll, `pv` extras) are series-page behaviour; one (Airing week navigation) is a history-management change. They share no code, but four of them share one underlying fact about the data model, which is worth stating once here:

**Entering `Rewatching` resets `EpisodesWatched` to 0, and `RewatchCount` only increases when a run *finishes*.** This is MAL's model and the app follows it (`profile-stats`: "entering Rewatching resets the count to 0 and the rewatch count is only increased once a run finishes"). Every figure derived from `EpisodesWatched` alone therefore reads an actively-rewatched entry as unwatched, and every figure derived from `RewatchCount` alone reads an in-progress rewatch as no rewatch at all. The profile's `Episodes`/`Days` stats already handle this correctly because they *sum* the two (`RewatchInclusiveEpisodes`). The series rewatch scope and the series progress badge each read only one half, and both are wrong for the same reason.

Relevant current state:

- `SeriesRankingIndex.MemberRewatchSeconds` returns 0 outright when `RewatchCount <= 0`, then multiplies `WatchMath.RewatchOnlyEpisodes(rewatchCount, total, watched)` by episode seconds.
- `SeriesPage.tsx`'s `completionBadge` and `SeriesRankingIndex.ProgressBadge` are a deliberate mirrored pair — `types.ts` says so explicitly ("the two must be changed together") — and `series-browser`'s spec restates `series-page`'s precedence inline, so all three move together.
- `SeriesRelations.IsTraversableMusicEdge` is the only `other`-relation escape hatch, gated on an *already-cached* media type; `SeriesGraphBuilder` batches one media-type lookup per node and drops far ends with no cached row. Related-anime edges are stored for every full fetch, but MAL's `related_anime` nodes carry no media type and no row is created for the far end, so an `other` far end is uncached unless something else fetched it.
- `SeriesPage`'s More-section state is three plain `useState` hooks with a comment saying they "reset on navigation rather than persisting" — a deliberate choice this change reverses.
- `MyListPage`'s `statusFilter` is a single `'All' | WatchStatus` in `useRestorableState`, consumed in one `useMemo` and one tab loop.
- `AiringPage` keeps the week in `?week=` via `setSearchParams`, which pushes. `useScrollRestoration` already supports an opt-in `{ state: { keepScroll: true } }` flag for exactly this shape of same-path query change.

## Goals / Non-Goals

**Goals:**
- One shared definition of "episodes this entry represents as a rewatch", used by both the profile scope and anything else that counts rewatch time.
- One shared clause — "a `Rewatching` main-line entry counts as fully watched" — applied consistently across the badge, the header progress bar, the time stats, and the browser's sort, so no two figures on the same screen disagree.
- `pv` extras that actually appear for a real franchise, not a rule change that is inert because the rows are uncached.
- Multi-select statuses without breaking the deep links that seed the page with a single status.
- Back on the Airing page leaves the Airing page.

**Non-Goals:**
- Extending "settled" (the score-reveal rules) to `Rewatching`. Defensible, but a separate decision with its own spoiler consequences.
- Changing the per-media-type "Most rewatched" scopes, which rank by an integer count.
- Admitting `cm` entries to a series.
- Persisting any of this across a reload — restorable state stays in-memory and session-scoped, per `page-state-restoration`.

## Decisions

### D1 — Rewatch time = completed runs + the current run, via one shared helper

`WatchMath` gains a primitive that adds an in-progress run to the completed ones:

```
RewatchEpisodesIncludingCurrentRun(rewatchCount, totalEpisodes, episodesWatched, status)
  = RewatchOnlyEpisodes(rewatchCount, totalEpisodes, episodesWatched)
  + (status == Rewatching ? episodesWatched : 0)
```

`MemberRewatchSeconds` calls it and drops its `RewatchCount <= 0` early return, replacing it with "return 0 when the result is 0" — which keeps the existing "a member not in my list or never rewatched contributes nothing" behaviour while admitting an entry on its first, still-running rewatch.

**Why here and not at the call site:** `WatchMath` exists precisely so the profile's own stats and the series index cannot restate the same fallbacks independently — its doc comment says so. A second definition of "what a rewatch is worth" is exactly the drift the file was created to prevent.

**Watch the fallback interaction.** `RewatchOnlyEpisodes` falls back to `episodesWatched` as the per-run baseline when no total is published. For an entry with no published total that is *currently rewatching*, `episodesWatched` is the current run's partial progress, not a full run — so a rewatch count of 2 with 3 episodes watched of an unpublished-length show yields `2*3 + 3 = 9`, understating it. This is the pre-existing fallback being wrong in a new way rather than a new bug (today it yields `2*3 = 6`), it only affects entries with no published episode count, and both figures are lower bounds. Not worth a second fallback ladder; noted so it is not mistaken for an oversight.

*Alternative rejected:* computing the in-progress run's contribution from `AiredEpisodes` rather than `EpisodesWatched`. That would credit time not actually spent.

### D2 — "A `Rewatching` main-line entry counts as fully watched" is one rule applied in four places

Define a single per-entry figure — **effective watched episodes** = `max(episodesWatched, airedEpisodes)` when the entry's status is `Rewatching`, and `episodesWatched` otherwise — and use it everywhere the series page and browser sum my watched main-line episodes:

1. the personal badge precedence (`completionBadge` / `ProgressBadge`),
2. the header progress bar's watched fill and its named watched figure,
3. `myWatchedSeconds` (time watched, and therefore time left),
4. `SeriesListItemDto.mainLineWatchedEpisodes`, which the browser's My-progress sort divides.

The entries-completed stat takes the same treatment for the same reason — a `Rewatching` entry counts as completed, since a rewatch can only follow a completed run — which keeps "6 of 6" beside a `Completed` badge instead of "5 of 6".

`max` rather than a bare `airedEpisodes`: a rewatch that has already run past what the app believes has aired should not be counted *down*, and it keeps the figure sane when `airedEpisodes` is null (fall back to `episodesWatched`).

The badge's precedence also gains one clause of its own: **rule (1) `Completed` accepts `Rewatching` alongside `Completed`.** Without it, starting a rewatch of a finished franchise downgrades its badge from `Completed` to `Caught up` — a strictly worse readout for an action that means "I finished this and I am watching it again". Rules (2) `Dropped` and (5) `Unwatched` need no clause of their own: both are decided from watched counts, which the effective figure already fixes.

*Alternative rejected:* fixing only the badge, as literally asked. The badge sits directly beside a progress bar reading the same underlying quantity; fixing one and not the other puts "Caught up" next to a half-full bar.

*Alternative rejected:* treating `Rewatching` as fully watched in the *episode-total* and *aired* figures too. Those describe the anime, not me; only the watched side of each pair moves.

### D3 — More-section view state moves to `useRestorableState`, unchanged in shape

`mineOnly`, `collapsedGroups`, and `unfilteredGroups` become `useRestorableState` with keys `moreMineOnly`, `moreCollapsedGroups`, `moreUnfilteredGroups`. The snapshot store holds live object references in memory and never serialises, so the `Set` and the `Record` survive as-is.

Two consequences follow from the existing hook rather than from new logic, and both are correct: a fresh visit still seeds the documented defaults (filter on, nothing collapsed, no exemptions), and the state is keyed by `location.key`, so two different series pages cannot share a More-section state.

The group keys `collapsedGroups`/`unfilteredGroups` are keyed by (`extrasGroupKey` = media type, index-disambiguated) are stable for a given series' extras, so a restored key set still matches the groups the restored page renders. A background refresh that changes the extras (an extra added to my list, a rebuild that adds a member) can leave a key that no longer matches a group — harmless: an unmatched key is simply never read.

*Alternative rejected:* persisting to `sessionStorage`. `page-state-restoration` states restoration state is in-memory and per-history-entry; a page-global persisted value would also break the "a fresh visit opens on defaults" rule.

### D4 — Scroll-on-open targets the group heading's document top, clamped by the browser

When `handleExtrasGroupHeadingClick` *opens* a group (never when it collapses one), the page scrolls so that group's heading is at the top of the viewport. There is no sticky or fixed app chrome (`index.css` confirms nothing sticky at page level), so the target is the heading element's `getBoundingClientRect().top + window.scrollY` with no offset.

Two mechanics matter:

- **After layout, not during the click handler.** The tiles the group is about to reveal are what make the page tall enough to reach the target, so the scroll must run after React has committed the expanded group — a layout effect keyed on the group that was just opened, not an imperative call in the handler.
- **The clamp is the browser's.** "If there's not enough anime to get it to the top then get as down as possible" is exactly what `window.scrollTo` already does when the target exceeds the maximum scroll offset. No manual `min()` against `scrollHeight - innerHeight` is needed, and doing it manually would race the same layout the effect is already waiting on.

Scrolling is instant rather than smooth, matching how the rest of the app moves the page (`useScrollRestoration` uses plain `scrollTo`), and so the position is reached before the user's next interaction rather than during an animation.

*Alternative rejected:* `element.scrollIntoView({ block: 'start' })`. Equivalent when it works, but it silently scrolls the nearest scrollable *ancestor*, and the series page has a horizontally-scrolling timeline container above the More section; targeting the window explicitly avoids depending on which ancestor the browser picks.

### D5 — `pv` traversal, and the probe that makes it non-inert

Three parts, in order of how much they change:

**(a) The rule.** `IsTraversableMusicEdge(a, b)` becomes `IsTraversableOtherEdge(a, b)`: traverse an `other` edge when exactly one of its two ends has a media type in `{music, pv}`. Stated over the two ends, as today, so the component is the same whichever end the build starts from. Both-ends-in-the-set and neither-in-the-set stay untraversed, so a `pv` never bridges to another `pv` and a `music`↔`pv` edge — a promo for a theme song — does not fuse two franchises through a promo. `cm` deliberately stays out; the set is a named constant so adding it is one line.

The EF-translated *incoming*-edge predicate in `SeriesGraphBuilder` cannot call a static helper, so it needs an inline equivalent. Use a `HashSet<string>` constant and `Set.Contains(r.Anime.MediaType) != Set.Contains(metadata.MediaType)` — the same concrete-`HashSet` trick `TraversalSet` already documents for EF translation, replacing today's inline `== "music"` pair.

**(b) `pv` is main-line ineligible**, joining `special` and `music` in the media-type filter. A promotional video is never a chapter of the story, and admitting one to a chain could hand the main line to it in a franchise whose real entries carry no sequel edges.

**(c) The probe.** Without this, (a) is inert for Jujutsu Kaisen and for most franchises: an `other` far end has no cached row, its media type is unknown, and the current rule declines to traverse *and* declines to spend a fetch — so the entry can never be recognised as a `pv` and never gets fetched. That is a closed loop.

A build therefore gets a **probe budget** separate from its member fetch budget: **4 on a visit-triggered build, 10 on an explicitly requested rebuild.** Each probe full-fetches one `other` far end with no cached row, purely to learn its media type; if it turns out to be in the traversal set the same fetch has already produced the full row needed to render it as a member, and if not, the row is now cached and every future build skips it for free without a probe. **Each `other` far end is therefore probed at most once, ever** — the cost is one-off per edge, not per build, which is what makes a budget this small workable.

Design points worth stating:

- **Separate budget, not the member budget.** A franchise with many `other` edges to commercials would otherwise starve real, story-related members that cannot be displayed at all without their fetch. The existing budget's documented priority ("fetches spent first on members with no cached row") depends on probes not competing with it.
- **A build that exhausts its probe budget marks the series partial**, so the existing "a partial series is rebuilt on the next visit, each visit starting from more cached data than the last" mechanism finishes the job with no background job. Convergence is guaranteed because probes never repeat.
- **A probe that 404s** is treated exactly as the member path treats it: the anime is gone from MAL, retrying will not help, and it does not mark the series partial.
- **This fixes `music` too.** The same closed loop applies to a franchise's theme songs today; the requirement's "an uncached `other` end costs nothing" scenario is replaced rather than kept.

*Alternative rejected:* traversing every `other` edge and filtering members afterwards. That fuses franchises through commercials and crossovers — the exact failure the current rule was written to prevent — and it spends member budget on entries that will be discarded.

*Alternative rejected:* reading the media type from AniList, which returns `format` inline with relations and would cost no MAL call. AniList does not catalogue PV entries at all, so it cannot answer the question being asked.

**(d) `pv` gets its own More group**, inserted into `SeriesMediaTypeOrder` between `music` and `tv`: Movie, OVA, ONA, Special, Music, **PV**, TV, Other. It sits with the other short-form non-story extras rather than in the `Other` catch-all, and ahead of TV/Other, which are the catch-alls. `mediaTypeLabel` already maps `pv` → `PV`, so no frontend label work.

**(e) Stored series must rebuild.** Bump `ClassificationRevisedAt`, which the `series-page` spec already defines as the mechanism by which a classification correction reaches stored series on their next read. Nothing else in this change needs a migration.

### D6 — `statusFilter` becomes `WatchStatus[]`, with `[]` meaning All

`[]` is All. This makes "All" a derived state rather than a seventh member of the value type, so there is no representable contradiction (`['All', 'Watching']`) to guard against.

Selection rules:
- Clicking a status toggles its membership.
- Clicking **All** sets `[]`.
- Deselecting the last selected status yields `[]` — i.e. it lands on All rather than on an empty list. Showing nothing is never what a user means by unticking their last filter.
- The All tab reads active exactly when the array is empty.

Consumers:
- The filter predicate becomes `statusFilters.length === 0 || statusFilters.includes(item.entry.status)`.
- The grouped view's `GROUP_ORDER.filter(...)` becomes `statusFilters.length === 0 || statusFilters.includes(status)`, preserving the documented group order rather than the selection order — the order is the app's, not the click sequence's.
- Tabs become `aria-pressed` toggle buttons rather than `role="tab"`/`aria-selected`. A tablist models exactly one selected tab; keeping that markup while allowing several selections would misreport the control to assistive technology. The All tab keeps `aria-pressed` too, for consistency within one row of controls.

**Restorable-state key renames to `statusFilters`.** The stored value's type changes from string to array, and a snapshot written before this change could otherwise feed a bare string into array code on a back-navigation within a live session. A new key makes that unreachable.

`focusSeed` (the deep-link seeds that open My list pre-filtered to Completed, Dropped, or Watching) returns a one-element array. Its behaviour is unchanged; those links land on a single selected status, which the user can now add to.

### D7 — Airing week navigation replaces, and holds scroll

`goToWeek` passes `{ replace: true, state: { keepScroll: true } }` to `setSearchParams`.

- `replace: true` means stepping through weeks never grows the history stack, so Back leaves the page — the whole ask. The week stays in `?week=`, so the URL is still shareable and the `airing-schedule` spec's existing "selected week survives back-navigation from an anime detail page" scenario still holds: that navigation is a *push* from the Airing entry and a *pop* back to it, untouched by this change.
- `keepScroll` is required, not incidental. A replace mints a fresh `location.key`, which `useScrollRestoration` reads as a non-restore and would answer with `scrollTo(0, 0)` — so without the flag, every week step would jerk the page to the top. The flag's existing implementation also seeds the new entry's snapshot with the current scroll position, so navigating away and back returns to where the user was rather than to the top.

The fresh `location.key` also means the new entry's `usePageData` cache starts empty and the week is fetched. That is unchanged from today (a push minted a fresh key too) and is correct — it is a different week's data.

*Alternative rejected:* moving the week out of the URL into `useRestorableState`. It removes the history entry too, but loses the shareable/bookmarkable URL and rewrites the spec's stated storage for the selected week, for no additional benefit.

## Risks / Trade-offs

- **[Probe budget spends MAL calls on entries that turn out to be commercials]** → Bounded at 4 per visit build, and each far end is probed at most once ever because the probe caches the row. The steady-state cost after a franchise's first few visits is zero.
- **[A franchise with many uncached `other` edges stays `partial` across several visits]** → It converges, because probes never repeat and each visit starts from strictly more cached data. `partial` already means "rebuild next visit" and already renders a complete-looking page.
- **[`pv` traversal fuses two franchises through a crossover promo]** → The exactly-one-end rule blocks `pv`↔`pv` and `music`↔`pv`, so a fusion requires a single `pv` with `other` edges to two different franchises' shows. That is the same residual risk `music` already carries and has not caused a problem; the `alternative_setting`/`character` exclusions remain the real defence against franchise fusion.
- **[The `Rewatching`-counts-as-watched rule hides genuine lateness]** → An entry marked `Rewatching` on a *currently airing* show could be genuinely behind on new episodes while rewatching old ones. The rule reads it as caught up. Accepted: the status the user chose says "I have seen this and am watching it again", and the alternative — the current behaviour — is wrong in the far more common case.
- **[Badge and score-reveal rules now disagree about what `Rewatching` means]** → The badge treats it as watched; the score-reveal rules still treat only `Completed`/`Dropped` as settled. Deliberate and flagged as a non-goal, so the inconsistency is recorded rather than discovered later.
- **[Multi-select makes an empty result set easy to reach]** → Not reachable by deselection (the last deselection lands on All), and any non-empty selection that matches nothing shows the page's existing empty state.
- **[Restoring More-section state surprises a user who expects a fresh default]** → This is what `page-state-restoration` already promises for every other view control on every other page; the More section was the outlier.

## Migration Plan

No data migration. `ClassificationRevisedAt` is bumped so every stored series rebuilds on its next read, picking up `pv` membership and the new group order without any user action. Rollback is a straight revert plus a further `ClassificationRevisedAt` bump to rebuild series back without `pv` members.

## Open Questions

None blocking. Two decisions are recorded above as deliberate non-goals rather than open questions: whether `Rewatching` should count as "settled" for score reveal, and whether `cm` entries should join a series.

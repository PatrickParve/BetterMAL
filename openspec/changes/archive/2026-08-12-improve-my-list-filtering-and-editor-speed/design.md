## Context

Two pieces of work on the same page, joined by the fact that the second makes the first worse if left alone: more filter controls mean more page-level state changes, and every page-level state change currently redraws every row.

**Why the editor is slow from my list.** `EntryEditorProvider` (`AppShell` root) holds the open editor in state and publishes `value={{ openEditor: setTarget }}` — a fresh object on every render. `CompletionPromptProvider` does the same with `{ increment, setEpisodesWatched }`, both re-created per render. Setting the target therefore re-renders the provider, invalidates the context value, and forces every consumer to re-render. `MyListPage` is a consumer, and it re-renders its whole list in the same commit that mounts the overlay: for each entry a status-striped row, a `<Link>`, a poster, a `ProgressBar` (with its own state and an `IncrementButton`), an 11-option `<select>`, a `ScoreValue`, and a button. On a library of a thousand-plus entries that is tens of thousands of elements reconciled before the overlay can paint — seconds of work. `AnimeDetailPage` consumes the same context but renders one entry, which is why the same overlay opens instantly there. `ContentFilterContext` already memoises its value; these two providers are the outliers.

The same mechanism makes every my-list edit expensive: `pendingIncrementId` / `pendingScoreId` are page state, and `setItems` patches one item into a new array, so a single score change re-renders all rows too.

**What the page can express today.** `MyListPage` offers five status tabs plus one `<select>` with three sort keys (alphabetical, MAL score, my score) and a fourth (airing status) that only appears under Plan to watch and silently resets to alphabetical when the status tab changes. Sorting is single-key; there is no way to break ties, filter by media type, find an entry by name, or see only what is still airing. Grouping is implied by the sort key: alphabetical groups by status, anything else flattens into a ranked list.

**Constraints.** `getMyList` returns the whole list in one call and `MyListItemDto` already carries `mediaType`, `airingStatus`, `malScore`, `totalEpisodes`, `episodesAired` and the full entry (score, episodes watched, dates), so everything here is client-side over data already in memory — no backend, endpoint, or DTO change. View controls are expected to survive back/forward via `useRestorableState`, and scroll position is restored per history entry by `useScrollRestoration`, which constrains how the list may be rendered (see D3).

## Goals / Non-Goals

**Goals:**

- Opening and closing the entry editor (and the completion prompt) costs nothing proportional to the page behind it.
- Editing one row redraws that row only.
- A filter bar on my list covering title text, media type, airing status and score, combined freely.
- Two-level sorting over ten keys with a direction toggle, deterministic ordering, and missing values always last.
- Grouping by status as an explicit choice rather than a side effect of the sort key.
- The page's default view looks exactly as it does today.

**Non-Goals:**

- No list virtualisation. It would cut first-paint cost on a huge list, but it needs a new dependency, breaks in-page find, and interacts badly with the per-history-entry scroll restoration this app already guarantees. Interaction cost is what the user reported; that is what this change fixes.
- No server-side filtering, sorting, or pagination — the list is already fully loaded.
- No saved filter presets, and no persistence of filter state beyond back/forward restore (D8).
- No change to the status tabs, the row layout, or any other page's filter controls (season and search keep theirs in the URL).
- No change to the completion-prompt or sync behaviour beyond the context-value fix.

## Decisions

### D1: Stabilise the two provider context values instead of splitting the contexts

`EntryEditorProvider` and `CompletionPromptProvider` both render `{children}` *and* their overlay as siblings. `{children}` keeps its element identity across a provider re-render, so React would already bail out of that subtree — the only reason the tree below re-renders is the changed context value. Wrapping the value in `useMemo`, and `increment`/`setEpisodesWatched` in `useCallback` with empty dependency lists (they close over nothing but `setPrompt`, which React guarantees is stable), makes the value permanently stable and the bail-out effective.

Alternative considered: splitting each provider into an actions context and a state context, the usual fix when a provider publishes both. Rejected — neither provider publishes any state through context (the overlay is rendered by the provider itself), so there is nothing to split; memoisation alone yields a value that never changes for the life of the app.

This fixes the overlay for *every* page at once — top anime, season, dashboard and detail all consume the same contexts.

### D2: A memoised `MyListRow` with referentially stable callbacks

D1 stops overlay opens from touching the page, but page-local state changes remain: a pending flag, a filter change, a patched item. Extracting the row into `MyListRow` wrapped in `React.memo` confines those to the rows that actually changed, and is what keeps the new filter controls cheap (typing in the find field is a page state change per keystroke).

For the memo to bite, the row's props must be stable:

- **Callbacks** — `onEdit`, `onIncrement`, `onSetWatched`, `onScoreChange`, each `useCallback`-wrapped and taking the `item` as an argument rather than being built per row. Their dependencies are all already stable: `openEditor` and `increment`/`setEpisodesWatched` after D1, `setItems` and `reload` from `usePageData`'s `useCallback`s.
- **The concurrency guard moves to a ref.** `pendingIncrementId`/`pendingScoreId` are read inside the handlers today (`if (pendingIncrementId !== null) return`), which would tie the handler identity to state that changes on every click. The guard becomes a ref written alongside the state; the state stays, but only as the source of the per-row `incrementPending` / `scorePending` props.
- **The item object** — `setItems` replaces one element and leaves the other element references intact, so a patched score re-renders one row.

Alternative considered: leaving the row inline and relying on D1 alone. Rejected — it fixes the reported symptom but leaves every score change and every keystroke in the new find field redrawing the whole list, which is the same defect under a different trigger.

### D3: One memoised derivation: filter → sort → group

`filteredItems` and `sortByKey` currently run on every render, and the grouped branch re-runs the sort once per status group. Replace with a single `useMemo` over `[items, statusFilter, query, typeFilter, airingFilter, scoreFilter, sortKey, sortDirection, tiebreakKey, airingStatusFirst, groupByStatus]` producing the final shape the JSX renders: either one ordered array, or the status-ordered groups each already sorted.

The text query is passed through `useDebouncedValue` (200 ms, the hook already used by the navbar search) so a keystroke does not re-derive over thousands of items; the input itself stays uncontrolled-feeling because its own value is immediate page state.

### D4: Filters as separate `useRestorableState` slots, arrays for the multi-selects

Each control gets its own `useRestorableState` key (`query`, `typeFilter`, `airingFilter`, `scoreFilter`, `sort`, `sortDirection`, `sortThen`, `groupByStatus`, plus the existing `statusFilter` and `airingStatusFirst`), matching how the page already stores `sort` and `statusFilter`, rather than one combined filter object. Separate slots keep each `setX` independent and make "clear filters" an explicit list of resets rather than an object spread that silently carries new keys.

The two multi-selects store `string[]`, not `Set` — the snapshot map holds live references, and an array is the shape that compares and serialises predictably. The derivation converts to a `Set` once inside the `useMemo`.

### D5: Comparators as direction-aware factories in `utils/anime.ts`, composed by the page

The page composes `[primary, tiebreak, byTitle]` and takes the first non-zero result. Each key maps to a factory `(direction) => comparator`, which keeps two rules structurally impossible to get wrong:

- **Missing values last in both directions.** A comparator that simply negates for ascending order would float `null` scores and undated entries to the top when flipped. Instead each factory is built from a shared `nullsLast(get, compare)` helper that resolves presence *before* the direction-sensitive comparison: both missing → 0, one missing → it goes last, otherwise compare and apply direction.
- **Determinism.** Title order is appended as the final comparator unconditionally, so a fully tied pair has one defined order and `Array.prototype.sort`'s stability is never load-bearing.

`compareByMalScoreDesc`, `compareByTitleAlphabetical` and `compareByAiringStatus` already live in `utils/anime.ts` and are shared with the season page; the new factories sit beside them and the existing exports keep their signatures so the other call sites are untouched.

Progress sorts on `episodesWatched / totalEpisodes`, treating an unknown total as missing (last). Type sorts on the display label so the order matches what the row shows.

### D6: A media-type label map, shared by the filter and the row

Rows render `item.mediaType.toUpperCase()` today, which prints `TV_SPECIAL`. A `MEDIA_TYPE_LABELS` map plus `mediaTypeLabel(raw)` in `utils/anime.ts` — falling back to a prettified form of any value not in the map, the way `RelatedAnimeOverlay` already handles unrecognised relation types — gives the filter its option labels and fixes the row in the same move. The filter's option list is derived from the media types actually present in the loaded list, so it can never offer a choice that returns nothing; it is computed in the same `useMemo` pass as the derivation, keyed on `items`.

### D7: A small checkbox popover component for the two multi-selects

Type and airing status both need "any combination of a handful of values". `components/FilterMultiSelect.tsx`: a button showing a summary label (`Type: All`, `Type: Movie`, `Type: 2 selected`) that toggles a panel of checkboxes with "All"/"None" shortcuts, closing on outside click via the existing `useClickOutside` hook and on Escape. The panel is absolutely positioned under its button at a z-index below the modal layer (which is 100), so an overlay always covers it.

Alternative considered: a native `<select multiple>`. Rejected — it renders as a fixed-height scrolling box, has no room for a summary label, and behaves badly for multi-selection on macOS. Alternative considered: filter chips that toggle in place (like the status tabs). Rejected — ten media types plus four airing statuses inline would dominate the page; the popover keeps the bar to one line at rest.

### D8: Filters are working state, not a preference

Every new control uses `useRestorableState`, so it survives back/forward but resets on a fresh visit to my list. This matches the page's existing `statusFilter`/`sort` and the "View-control selections are restored with the page" requirement. Deliberately *not* `localStorage` (as `ContentFilterContext` uses for hide-NSFW): hiding NSFW is a standing preference, whereas "show me only the movies I haven't scored" is a question asked once. A user who returns to my list expecting their whole library and instead finds yesterday's four filters still applied has a bug on their hands, not a feature.

### D9: Grouping becomes an explicit toggle

Today grouping is inferred: alphabetical groups, any other sort flattens. With ten sort keys that rule stops being predictable ("why did sorting by type ungroup my list?"), and it is the reason the airing sort has to reset when leaving Plan to watch. An explicit "Group by status" toggle, on by default, decouples the two: the sort key orders entries, the toggle decides whether they are grouped, and rank numbers follow the toggle (flat + non-alphabetical → ranks) rather than the sort key. The default combination — All, grouped, alphabetical — reproduces today's page exactly.

### D10: The bar reuses the page's existing control styling

No new visual language: the selects are the existing `.my-list-page__sort` treatment (`--bg`, `--border`, 6 px radius, 14 px), and the direction and grouping toggles are pill buttons matching `.my-list-page__tab`, using the `--accent-bg` / `--accent-border` active state the tabs already use. The bar is a `display: flex; flex-wrap: wrap; gap: 8px` container directly under the tabs, with the ordering cluster pushed right by `margin-left: auto` so filters and sorting read as two groups and collapse gracefully when they wrap. The count line sits under the bar in muted text at the page's small size.

Because the bar now owns the sort control, the per-group and per-header copies come out of the group headers, which leaves `.my-list-page__group-header` as a plain title line and makes the shared header-to-list spacing rule (`Consistent list placement`) trivially satisfied in both modes.

### D11: Airing badge visibility follows the active controls

The badge is Plan-to-watch-only today because that is the one group where "is this out yet?" matters unprompted. Once airing status is filterable and sortable everywhere, a user narrowing to "Currently airing" under Watching is asking about exactly that value and should see it on the rows. The rule becomes: show on Plan-to-watch rows always, and on every row while the airing filter has a selection or airing status is the primary sort key. The row receives this as a boolean prop computed once per render on the page, which keeps it out of the row's own logic and stable across rows.

## Risks / Trade-offs

- **The performance diagnosis is a code-reading, not a measurement** → Task 1.1 profiles the current page before any fix and after, on a realistically large list, so the fix is confirmed against a number rather than assumed. If the dominant cost turns out to be elsewhere (paint rather than reconciliation), the same profile says so before the rest of the work builds on it.
- **`React.memo` degrades silently** → a single accidentally-unstable prop (an inline arrow, a freshly built object) turns the memo into pure overhead with no error. Mitigated by keeping the row's props to the item plus primitives and `useCallback`-stable functions, and by re-profiling after the row extraction (task 2.6) rather than trusting it.
- **The pending-guard ref can drift from the pending state** → both are written in the same handler, and the ref is only ever read as a concurrency guard while the state is only ever read for rendering; neither is derived from the other.
- **More controls make the page busier** → the bar is two clusters that wrap, defaults are visually identical to today's page, and "Clear filters" is one click back to the known state.
- **Behaviour change for existing muscle memory**: choosing a score sort no longer flattens the list by itself, and switching status tabs no longer resets an airing sort → both are spelled out in the `library-views` delta, and the grouping toggle sits next to the sort controls where the effect is discoverable.
- **First paint of a very large list is unchanged** → out of scope by choice (Non-Goals); this change targets interaction cost, which is what was reported.

## Migration Plan

Frontend-only, no data or API change: build, ship, done. Rollback is a straight revert of the frontend commit — no persisted state is written that an older build would have to understand, since all new view state lives in the in-memory page-state snapshot.

## Open Questions

None. The two judgement calls that could have gone either way — filters as session state rather than a persisted preference (D8), and grouping as an explicit toggle rather than a derived one (D9) — are decided above.

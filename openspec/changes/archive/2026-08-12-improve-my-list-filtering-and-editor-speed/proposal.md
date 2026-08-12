## Why

Opening the edit overlay from a my-list row takes seconds, while the same overlay opens instantly from the anime detail page — because opening it re-renders the entire my-list page (every row, every progress bar, every 11-option score dropdown) in the same commit that mounts the overlay, and my list is the one page with hundreds of rows behind it. The same page is also the app's main working surface for a large library, and it can only be narrowed by watch status and ordered by one of three keys, with no way to ask for "my score, then MAL score", "how far I've watched", or "just the movies".

## What Changes

**Editor overlay opens immediately (my list, and everywhere else)**

- Opening or closing the entry editor no longer re-renders the page behind it, so it opens at the same speed on a 1,500-entry my list as on the anime detail page. The same applies to the completion-score prompt.
- A my-list row's own edits (score change, in-place episode count, increment) re-render that row alone rather than the whole list, so the list stays responsive while editing.

**My list filtering**

- A filter bar under the status tabs, holding every filter and sort control the page has. The status tabs stay as they are.
- **Find in list**: a text field that narrows to entries whose title (English or original) contains the typed text.
- **Type**: a multi-select — TV, Movie, OVA, ONA, Special, TV special, Music, Unknown, … — offering only the types actually present in the list. Any combination can be selected; none selected means all.
- **Airing status**: a multi-select — Finished airing, Currently airing, Not yet aired, Unknown — available under every status filter, not just Plan to watch.
- **Score**: Any / Rated / Unrated, for finding entries still missing a score.
- **Clear filters**, shown only while something is narrowed, restores the page's default view.
- A count line reports how many entries are shown out of the total whenever a filter is narrowing the list, and an empty result says so explicitly rather than reading "Nothing here yet."

**My list sorting**

- Sorting becomes two-level: a **Sort by** key and an optional **then by** tiebreaker, so "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.
- Sort keys: Alphabetical, My score, MAL score, Episodes watched, Progress, Total episodes, Airing status, Type, Start date, Finish date.
- A direction toggle flips the primary key between its natural order and the reverse. Entries missing the sorted value (no score, no date, unknown total) sort last in both directions, and title order settles any remaining tie so the list never reshuffles between renders.
- Airing-status sort is no longer restricted to Plan to watch, and switching status filters no longer silently resets the sort.
- **Group by status** becomes an explicit toggle instead of being implied by the alphabetical sort: on (the default) groups entries into the standard status order with the active sort applied inside each group; off produces one flat, rank-numbered list.
- With the airing filter or airing sort active, the airing-status badge appears on every row, not only on Plan to watch rows.

The page's default appearance — All, grouped by status, alphabetical — is unchanged.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `library-views`: replaces the single quick-filter control with a filter bar carrying find-in-list, type, airing-status and score filters plus a two-level sort with a direction toggle and an explicit group-by-status toggle; lifts the Plan-to-watch-only restriction on airing-status sort (and with it the reset-on-status-change rule); extends the airing badge to any row while an airing filter or sort is active; defines the rank rule against the grouping toggle rather than against the sort key; adds a filtered-count line and a distinct no-matches empty state.
- `list-editing`: adds a responsiveness requirement — opening and closing the entry editor and the completion prompt costs no work proportional to the page behind them, so the overlay opens at the same speed regardless of list size; and a row-scoped-update requirement for my-list edits.

## Impact

- **Frontend only.** No backend, API, or DTO change: `MyListItemDto` already carries `mediaType`, `airingStatus`, `malScore`, `totalEpisodes`, and the entry's score, episode count and dates, and my list is loaded in full, so every filter and sort runs client-side over data already on the page.
- **Modified — contexts**: `context/EntryEditorContext.tsx` and `context/CompletionPromptContext.tsx` (stable context values, so opening an overlay stops re-rendering every consumer).
- **Modified — my list**: `pages/MyListPage.tsx` / `MyListPage.css` (filter bar, two-level sort, grouping toggle, memoised derivation, stable row callbacks).
- **New — components**: a memoised `MyListRow`, and a shared checkbox-popover multi-select used by the type and airing filters.
- **Modified — shared utils**: `utils/anime.ts` gains a media-type display label (which also fixes rows currently rendering `TV_SPECIAL`) and the comparator set the two-level sort composes.
- **No change to** the my-list API call, page-state restoration behaviour (the new controls persist through back/forward like the existing ones), or any other page's rendering.

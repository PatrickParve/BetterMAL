## Context

`MyListPage.tsx` has a single `sort: SortKey` state (`'alphabetical' | 'malScore' | 'myScore'`) that drives two mutually-exclusive display modes, per an existing, deliberate design choice already documented in the code:

- `alphabetical` → entries render **grouped by status** (Watching → On hold → Plan to watch → Completed → Dropped), each group under its own `<h2>` title.
- anything else → entries render as **one flat cross-status ranked list** with `#N` rank numbers, mirroring how MAL's own list view behaves (grouping and score-ranking don't mix).

Today the sort `<select>` lives once, in the page header, next to the "My list" `<h1>`. Airing status (`not_yet_aired` / `currently_airing` / `finished_airing`) is already fetched on `MyListItemDto` and already rendered as a short badge, but only for Plan to watch rows (`airingStatusShortLabel`), since it's only meaningful there — a "Watching" or "Completed" entry's airing status isn't relevant to the user.

## Goals / Non-Goals

**Goals:**
- Add an "Airing status" sort option, available only when the Plan to watch group is what's being viewed.
- Move the sort control so it reads as scoped to a status title ("Watching", "Completed", "Plan to watch", …) instead of the page as a whole.
- Preserve the existing grouped-vs-ranked mode split; don't introduce per-group independent sorting, which would conflict with that invariant.

**Non-Goals:**
- No backend/API changes — airing status is already on the DTO.
- No persistence of sort choice across sessions/reloads (it isn't persisted today).
- No independent sort state per status group — sort stays a single page-level value; only its UI placement changes.

## Decisions

**1. Sort control moves to each visible group's heading row, not a fixed single spot.**
- Grouped (alphabetical) mode: each rendered `<h2>{STATUS_LABELS[status]}</h2>` becomes a flex row with the sort `<select>` alongside it. All instances are the same control bound to the same shared `sort`/`setSort` state — there's no independent per-group state. Selecting a non-alphabetical option from *any* instance flips the whole page into ranked mode, same as today.
- Ranked (flat) mode: since there's no group heading in this mode today, add one synthetic header line above the flat list, titled with the active filter (`STATUS_LABELS[statusFilter]`, or "All" when unfiltered), paired with the same sort control. This keeps "sort control next to a status title" true in both modes instead of only in grouped mode.
- The page header keeps just `<h1>My list</h1>`; the sort `<select>` is removed from there.
- *Alternative considered*: give each status group independent sort state (so "Watching" could be ranked by score while "Plan to watch" stays alphabetical). Rejected — it breaks the existing grouped/ranked mutual-exclusivity behavior called out in the current code comment, and duplicating that mode switch five times invites inconsistent states.

**2. "Airing status" sort option is gated to the Plan to watch context.**
- `SortKey` gains `'airingStatus'`. It's only included in the `<select>`'s options when `statusFilter === 'PlanToWatch'`, i.e. when the user has the Plan to watch tab active (the only place airing status is otherwise surfaced in this list). It is not offered from "All" or any other status tab.
- Ordering (via a new `compareByAiringStatus` helper in `utils/anime.ts`, alongside the existing comparators): **Finished airing → Currently airing → Not yet aired**. Rationale: this surfaces what's most immediately watchable first (finished = binge-ready right now with zero wait, currently airing = watchable now but catching up week to week, not yet aired = nothing to watch yet).
- Edge case: if `sort === 'airingStatus'` and the user switches the status filter away from `PlanToWatch`, reset `sort` to `'alphabetical'` in the tab's click handler. Otherwise the page would try to rank non-Plan-to-watch entries by a dimension it doesn't display, which is confusing.

**3. CSS**: reuse the existing `.my-list-page__sort` class for the `<select>` itself; add a `.my-list-page__group-header` flex row (space-between, centered) wrapping each `<h2>` + `<select>` pair, used both for real status groups and the synthetic ranked-mode header.

## Risks / Trade-offs

- [Repeating the same dropdown on every group heading in "All" + grouped mode looks redundant (up to 5 identical selects visible at once)] → Acceptable trade-off for directly matching the request ("same line as the status title"); the control is compact and each instance is genuinely on-topic for its section.
- [Silently resetting sort to alphabetical when leaving Plan to watch could surprise a user who picked Airing status] → Low impact: airing status isn't shown anywhere outside Plan to watch today, so there's nothing coherent to rank by there anyway.

## Open Questions

- Confirm the airing-status ordering (Finished → Currently → Not yet aired) matches what's actually useful — could instead prioritize "Currently airing" first if the intent is more about urgency/catching-up than binge-readiness. Flagging for review before implementation.

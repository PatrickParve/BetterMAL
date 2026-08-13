## Context

The series page (`frontend/src/pages/SeriesPage.tsx`, backed by `backend/AnimeTracker.Api/Services/Series/SeriesService.cs`) shipped across three prior changes (`add-series-page`, `redesign-series-page`, plus a favourite-ordering follow-up). This change is a polish pass on that shipped page, not a new capability — six independent fixes bundled into one change because they all touch the same file (`SeriesPage.tsx`) and the same requirement group (`series-page`).

Two of the six are pure bugs/gaps (episode-total fallback, missing rewatch surfacing). Two are layout/IA changes with no new data (rebuild button placement, collapse-control wording/suppression). Two are a real visual redesign of one section (timeline+main-series merge) and a behavior change to an existing anti-spoiler gate (highest-MAL title).

Relevant existing data already available and unused on this page:
- `AnimeMetadata`/`SeriesEntryDto.AiredEpisodes` — already computed server-side (`SeriesService.AiredEpisodesByAnimeIdAsync`) and already read by the page's progress bar, just not by the stats-box episode/runtime totals.
- `UserAnimeEntry.RewatchCount` — already synced from MAL, already flows through to the client on every `SeriesEntryDto.Entry` (it's a full `UserAnimeEntryDto`), already has a precedent UI treatment (`AnimeDetailPage.tsx`: `Rewatch count: {n}` shown only when non-zero) and a precedent tie-free ranking query (`ProfileService.BuildRewatchedSection`).
- `--mal`/`--mine` CSS custom properties and the chip pattern (`.series-extra-tile__chip--mal`/`--mine`, `.series-page__score-chip--mal`/`--mine`) — already the established blue/purple convention this change is asked to reuse rather than reinvent.

## Goals / Non-Goals

**Goals:**
- Fix the two places where the page shows less than it actually knows (episode/runtime lower bound, rewatch count).
- Collapse Timeline + Main series into one legible section without losing the drawn-to-scale gap behavior the existing ribbon already gets right.
- Make the highest-MAL-score stat consistent about not naming things the viewer hasn't watched.
- De-emphasize Rebuild and tidy the More-section collapse UI's edge cases.

**Non-Goals:**
- No change to how a series is built, persisted, or resolved (`SeriesGraphBuilder`, main-line classification, series identity) — this is a display-layer change only.
- No change to the More section's grouping-by-media-type structure or its >12-extras auto-collapse threshold.
- No per-episode rewatch tracking — `RewatchCount` stays a single per-anime counter, as MAL itself models it.
- No change to the global hide-scores toggle's own semantics; the highest-MAL-title change adds a second, independent gate rather than modifying the toggle.

## Decisions

### 1. Episode/runtime totals fall back to `AiredEpisodes`, not zero
`SeriesService.EpisodesAndRuntime` (`SeriesService.cs:401-420`) changes its unknown-total branch: instead of contributing nothing and only flipping `hasUnknown`, it adds `anime.AiredEpisodes ?? 0` episodes and `(AiredEpisodes ?? 0) * EpisodeSeconds(a)` runtime seconds, still flipping `hasUnknown`. `AiredEpisodes` here means the same per-entry aired-so-far figure already exposed on `SeriesEntryDto` (finished → total, airing → schedule-clamped count, not-yet-aired → 0, unknown → null) — this is a private computation inside `BuildStats`, so it needs the same aired-episodes map `BuildStats`'s caller already threads through for `mainLineAiredEpisodes`, passed into `EpisodesAndRuntime` alongside the anime list.
- **Alternative considered**: compute the fallback client-side (frontend adds `stats.mainLineAiredEpisodes` to `stats.mainLineEpisodeTotal` at render time). Rejected — `mainLineAiredEpisodes` sums only entries with a *known* total (per its own doc comment, to keep it ≤ the total), so it can't be reused for exactly the entries this fix targets (unknown-total entries) without duplicating the aired-episodes-per-entry map on the client. Doing it server-side in the same pass that already owns both numbers is simpler and keeps `MainLineEpisodeTotal` self-consistent with `HasUnknownEpisodeCounts`.
- The `+` lower-bound marker (`formatEpisodeTotal`/`formatRuntimeTotal` in `SeriesPage.tsx`) needs no change — `hasUnknown` still flips whenever any entry lacks a real total, so a fallback-inflated total still reads e.g. `"75+ ep"` instead of the old bare `"Unknown"`, and still reads `"Unknown"` only in the genuine all-entries-unaired-with-nothing-broadcast-yet case (fallback sum stays 0).

### 2. Rewatch: series-wide tied-top stat + per-row indicator, no schema change
`SeriesStatsDto` (`SeriesDto.cs`) gains one field: `List<int> MostRewatchedAnimeIds`, computed in `BuildStats` via the existing `TiedTopIds` helper over `orderedMembers.Where(m => m.Anime.UserEntry?.RewatchCount is > 0)` keyed by `RewatchCount` — the exact same pattern already used for `HighestMalScoreAnimeIds` and `MyHighestScoreAnimeIds` (series-wide: main line + extras, ties all listed, watch-order tie-break since `TiedTopIds` is called with no `tieBreakKey`). Empty list when nobody's rewatched anything.
- Frontend renders it in the Series stats grid exactly like the existing Highest MAL score / My favourite tie-list boxes (`<ul className="series-page__tie-list">`), titled "Most rewatched", showing each tied entry's title + `RewatchCount` (e.g. `"×3"`), and the whole `<div>` is omitted when the list is empty — same guard pattern as `highestMalEntries.length > 0`.
- Per-row: `SeriesEntryRow` and `SeriesExtraTile` each gain a small rewatch badge (e.g. `↻ 3`) rendered only when `entry.entry?.rewatchCount` is truthy, styled as a muted inline chip near the status text — no new prop needed, `entry.entry.rewatchCount` is already in the payload today. This is the "shown separately, per entry" half of the ask; the stats-grid tie list is the "what's most rewatched in the series" half.
- **Alternative considered**: scope the tie list to main-line entries only, matching "Main series episodes"/"Main series runtime" being main-line-scoped. Rejected for consistency — Highest MAL score and My favourite are already series-wide (main line + extras), and a rewatched OVA or movie is exactly the kind of thing worth surfacing here; scoping rewatch differently from its two sibling tie-list stats would be an unexplained inconsistency on the same stats grid.

### 3. Timeline and Main series merge into one component
`SeriesTimeline.tsx` is repurposed into the page's sole main-line section, replacing both the current ribbon and the `<ol>` of `SeriesEntryRow`s. It keeps the part of the current implementation that already works — a single flex row on a time axis, entries as `flexGrow: max(1, durationDays)` blocks and gaps as `flexGrow: max(1, gapDays)` spacers, the longest-gap marker — and replaces only the *block content*:
- Each block becomes a card: poster picture, watch-order rank (`#N`), title (linked), the same meta line `SeriesEntryRow` already builds (type · year · episodes · aired/watched labels via the existing exported `airedFigureLabel`/`watchedFigureLabel` helpers — reused as-is, not reimplemented), my list status, and an Edit button that calls the same `onEdit` callback `SeriesPage` already wires up.
- Scores render as a numeric chip pair — `.series-extra-tile__chip--mal`/`--mine`-style, i.e. `ScoreValue`'s formatted number in the page's existing `--mal`/`--mine` colour variables — replacing the independently height-scaled vertical tick bars (`scoreBarHeight`, `.series-timeline__score-bar`) that the proposal calls out as unclear. This directly reuses the "same way as in More" chip pattern rather than inventing a third score presentation.
- The watched/aired fill track stays (it's the one part of the old block that already reads clearly), now drawn along the bottom of the wider card instead of a thin strip.
- Undated entries move from a second "No air date" row into a visually distinct trailing lane on the same axis: same card, but with a dashed/muted border and an explicit "No air date" tag where the year would go — "still there," per the ask, but unambiguous that it has no chronological position, rather than a bare label floating above a same-styled block.
- `SeriesEntryRow.tsx`'s component and exported helpers are **not** deleted — `SeriesExtraTile.tsx` still uses it for the More section's poster-tile counterpart, and `airedFigureLabel`/`watchedFigureLabel` are reused by the new timeline card. Only the `<ol>` of `<SeriesEntryRow>` rows in `SeriesPage.tsx`'s "Main series" section goes away, folded into the merged timeline.
- **Alternative considered**: keep the two sections separate but visually link them (e.g. shared hover state). Rejected — the proposal and user ask are explicit that this is one chronology being shown twice; a merge is what's being asked for, not a cross-reference.

### 4. Highest MAL score masks the title independently of the hide-scores toggle
In the Highest MAL score tie-list (`SeriesPage.tsx:623-637`), each entry's title/link renders only when `isCompletedAndScored(entry)` (the same helper already gating the score's `completed` prop) is true; otherwise the list item renders a neutral placeholder (e.g. "Not yet watched" / "•••", no `<Link>`) in place of the title, with no anime id, title, or picture reaching the DOM for that entry. This check is independent of the global hide-scores toggle — it fires even when the toggle is off, since the concern here is "don't tell me which entry is best before I've seen it," not score privacy.
- **Alternative considered**: gate title-hiding on the hide-scores toggle too (only mask when `hidden && !isCompletedAndScored`). Rejected — the ask is specifically about not naming an unwatched entry, which is a spoiler concern that exists whether or not the user has chosen to hide scores from onlookers; conflating the two would mean turning hide-scores off (a privacy choice) also spoils which entry is MAL's highest-rated, an unrelated side effect.
- Scope stays the existing series-wide `HighestMalScoreAnimeIds` (unchanged) — only the per-entry title render gains the gate.

### 5. Rebuild control moves into the Series stats section
The standalone `.series-page__rebuild-row` (currently its own full-width flex row between the hero header and Series stats) is removed; the Rebuild button and the partial/truncated notice text move into the Series stats section's `<h2>` row as a small secondary action aligned to the right of "Series stats" (button text/state logic unchanged — `handleRebuild`, `MAX_REBUILD_ROUNDS`, the `rebuildCount` label are untouched). This keeps Rebuild reachable at a predictable, low-traffic-cost location without a dedicated banner competing with the hero.
- **Alternative considered**: put it in the hero header itself (next to the external links). Rejected — the header is explicitly the page's "read the series in one place" summary per the existing header requirement; an admin/maintenance action doesn't belong inside that block, only just outside it.

### 6. More-section collapse: suppress when fully watched, singularize when one group
Two independent conditions computed in `SeriesPage.tsx` alongside the existing `extrasGroups`/`anyExtrasGroupOpen`:
- `allExtrasCompleted = series.extras.length > 0 && series.extras.every(e => e.entry?.status === 'Completed')`. When true: every group renders permanently expanded (no per-group toggle button/caret, no "+N more" hint, `collapsedGroups` state simply isn't consulted), and the all-groups control is not rendered at all. This reuses the plain `status === 'Completed'` check already used for `statusClass`/`STATUS_LABELS` elsewhere on these rows — not `isGroupCompleted`'s finished-airing-scoped variant, since "I've completed every extra" should mean literally that, not "every extra that happened to finish airing."
- Wording: the all-groups control (only rendered when `!allExtrasCompleted`) reads `extrasGroups.length > 1 ? (anyExtrasGroupOpen ? 'Collapse all' : 'Expand all') : (anyExtrasGroupOpen ? 'Collapse' : 'Expand')`.
- **Alternative considered**: hide the More section's collapse *state* but keep the toggle button visible (disabled). Rejected as more confusing than omitting the control — a disabled button inviting a click that does nothing is worse than no button when there's nothing left to hide.

## Risks / Trade-offs

- **[Risk]** The timeline merge is the largest visual change in this batch — widening blocks into full info cards changes the ribbon's horizontal density, so a long franchise's row may need more horizontal scroll than before. → **Mitigation**: keep the existing `MIN_BLOCK_WIDTH`-style floor and the container's own horizontal scroll (`series-timeline__scroll`), already required by the shipped spec ("scroll within its own container"); size the new card's minimum width empirically against a large real franchise (e.g. One Piece's main line) during implementation.
- **[Risk]** Falling back to `AiredEpisodes` for unknown-total entries could read as more precise than it is if a caller drops the `hasUnknown` flag. → **Mitigation**: `hasUnknown` is untouched by this change (still flips on any unknown-total entry) and the frontend's existing `+`-suffix formatting already keys off it, so no frontend change is needed there beyond the value itself changing.
- **[Risk]** Masking the highest-MAL title regardless of the hide-scores toggle is a deliberate UX divergence from that toggle's usual "everything MAL-score-related is gated by one switch" mental model. → **Mitigation**: this is a conscious, explicitly-requested product decision (see Decision 4), not an oversight — call it out in the spec delta scenario text so it's unambiguous on review.
- **[Trade-off]** Reusing `SeriesEntryRow`'s exported helpers but not its rendering means the merged timeline card duplicates some JSX structure that `SeriesEntryRow`/`SeriesExtraTile` also have (picture, meta line, status, edit button). Accepted — the three presentations (row, tile, axis-card) now have different enough layout constraints (fixed-width table row vs. grid tile vs. variable-width axis-flex card) that a shared sub-component would need as many layout props as it saves lines.

## Migration Plan

- Backend: additive `SeriesStatsDto.MostRewatchedAnimeIds` field and the `EpisodesAndRuntime` fallback change deploy together; no migration, no schema change (`RewatchCount` column already exists and is populated). Existing stored `Series`/`SeriesMember` rows need no rebuild — stats are computed at read time (per the existing "series' score averages... computing them at read time" precedent), so the fix applies to every series on its very next page load.
- Frontend: `SeriesTimeline.tsx` is rewritten in place (same import site in `SeriesPage.tsx`, just dropping the separate `<ol>` of `SeriesEntryRow`s); `SeriesPage.css`/`SeriesTimeline.css` get the bulk of the CSS churn. No route or URL change, no `usePageData` cache-key change.
- Rollback: each of the six changes is independently revertable (different files/functions), but ship as one PR per the existing change scope; no feature flag needed since nothing here is risky at the data layer.

## Open Questions

- Exact minimum card width for the merged timeline on narrow viewports/large franchises — left to implementation-time visual tuning against real data rather than fixed here.
- Whether the rewatch badge on rows/tiles should show the raw count (`↻3`) or a word ("Rewatched 3×") — left to implementation as a small copy choice, doesn't affect data flow or requirements.

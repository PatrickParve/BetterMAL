## Why

Browsing surfaces are missing the one number they are most often browsed by — a card on the season and search pages shows type and episode count but no MAL score — and a series result's badge line renders in a line box too tight for its own glyphs, so text is clipped along its top edge.

On the series page the More section has two problems of its own: music entries never reach it at all (MAL links a franchise's music videos with the `other` relation, which series traversal deliberately never follows, so "Idol" ends up as its own two-member series instead of an Oshi no Ko extra, and Bleach's and Cyberpunk: Edgerunners' songs are missing entirely), and the section's visibility rules are fixed rather than chosen — entries already in my list are pinned open by a rule the user cannot turn off, while everything else is governed by an extras-count threshold.

## What Changes

**Search and season browsing**

- Season and search cards show the anime's MAL score at the bottom-right of the card, on the same line as, and aligned with, the existing `TYPE · N ep` meta.
- While the global hide-scores toggle is on, that card score is omitted entirely — no value, no placeholder, and no per-score reveal control. This is a deliberate exception to the app-wide "a hidden score keeps its slot and offers a reveal control" rule, and it applies even when "Always show MAL scores for completed and dropped shows" is enabled.
- Text in a search result — the results-page card's series badge line, and the type-ahead dropdown's title and badge line — is no longer clipped by its own line box at either end of the app's fluid root font size.

**Series page**

- **BREAKING (series composition):** a series now admits music entries reached over an `other` relation, where exactly one end of that relation is a music entry. Music members stay ineligible for the main line, so they land in the More section's Music group. Every stored series is rebuilt once on its next read to pick this up, and music-only series (e.g. `Idol` + its cover) are absorbed into the franchise they belong to.
- The More section gains an "In my list" filter control beside the expand/collapse-all control. It is **on by default**, so opening a series page shows only the extras that are in my list, whatever their status.
- **BREAKING (More section behaviour):** the rules that decided visibility for the user are removed — extras with watch progress are no longer force-shown inside a collapsed group, the "more than twelve extras starts collapsed" threshold is gone, and a series whose extras are all Completed no longer loses its collapse controls. Collapse all now collapses everything, including entries in my list.
- The "Highest MAL score" stat reveals its entry's title, link, and score once that entry is Completed **or Dropped** in my list, rather than only when it is completed and scored.
- More tiles are wider, so a `TV special · 2003 · 99 ep` meta line fits without truncating at the grid's narrowest column.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `series-page`: music entries reachable over an `other` relation join the series as extras; the More section's default view, filter control, and collapse semantics change; the Highest MAL score stat reveals on Dropped as well as Completed; More tiles reserve enough width for their meta line.
- `season-browser`: a season card additionally shows the anime's MAL score, aligned with its type/episode line.
- `navigation-and-search`: a search results card additionally shows the anime's MAL score in the same slot the season card uses; series result text is not clipped by its line box.
- `score-visibility`: a hidden MAL score on a season or search browse card is omitted entirely rather than replaced by a reveal control, and the always-show-completed setting does not apply there.

## Impact

- **Backend:** `SeriesRelations` / `SeriesGraphBuilder` traversal (music-bearing `other` edges, in both edge directions), `SeriesGraphBuilder.ClassificationRevisedAt` bumped to force a one-time rebuild of every stored series; `SeriesGraphBuilderTests`.
- **Frontend:** `AnimeCard` (`AnimeCardMeta` gains the score slot) and its CSS; `SearchPage`, `SeasonPage` (pass `malScore`); `SearchBar.css`, `SeriesBadge.css` (line boxes); `SeriesPage.tsx` (More section state model, stats reveal rule) and `SeriesPage.css` (extras grid track width).
- **Data:** no schema change. Series membership rows are rewritten by the forced rebuild; music-only `Series` rows are deleted as they are absorbed.
- **No API contract change** — `AnimeBrowseItemDto` already carries `malScore`.

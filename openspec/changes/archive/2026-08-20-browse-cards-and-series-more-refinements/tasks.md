## 1. Music entries join their franchise (backend)

- [x] 1.1 Add the music-aware edge rule to `SeriesRelations`: alongside `IsTraversable(relationType)`, a predicate that accepts `other` when exactly one endpoint's media type is `music` (both-music and neither-music `other` edges rejected), with a comment recording why `other` is otherwise never traversed.
- [x] 1.2 In `SeriesGraphBuilder.TraverseAsync`, batch-load the cached `MediaType` of the current node's `other`-relation neighbours (one query per visited node, ids from `metadata.RelatedAnime`), and admit outgoing `other` neighbours per 1.1 — treating a neighbour with no cached row as not traversable and spending no fetch budget on it.
- [x] 1.3 Extend the incoming-edge query (`db.AnimeRelatedAnime.Where(r => r.RelatedAnimeId == animeId …)`) with the `other` branch: every incoming `other` source when the current member is music, and only sources whose cached media type is `music` when it is not.
- [x] 1.4 Bump `SeriesGraphBuilder.ClassificationRevisedAt` to the ship date so `SeriesService.NeedsBuild` rebuilds every stored series once on its next read.
- [x] 1.5 Add `SeriesGraphBuilderTests` cases: a `tv` show `other`-linked to a `music` entry admits it as an extra; building from the music entry produces the same member set as building from the show; an `other` link to a `cm`/`pv`/`tv` entry is still not traversed; a `music`–`music` `other` link is not traversed; an `other` neighbour with no cached row is skipped without spending fetch budget or marking the build partial.
- [x] 1.6 Run the backend tests (`docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet test"` after rsyncing the backend out of `~/Documents`, per the local build notes).
- [x] 1.7 With the dev stack running, open `/series/52034` (Oshi no Ko) and confirm `Idol` and its cover appear in a Music group in More; check `Bleach: Sennen Kessen-hen` and `Cyberpunk: Edgerunners` likewise, and confirm the previously music-only series (`SELECT * FROM "Series" s JOIN "SeriesMembers" m …` for anime 55016) no longer exists as its own series.

## 2. More section: filter and collapse controls (frontend)

- [x] 2.1 Replace `SeriesPage`'s More-section state with `mineOnly` (default `true`), `collapsedGroups` (all expanded by default), and `unfilteredGroups`; delete `EXTRAS_COLLAPSE_THRESHOLD`, the `collapseInitialised` initialisation block, `hasWatchProgress`, and `allExtrasCompleted`.
- [x] 2.2 Compute each group's visible tiles as `collapsed ? [] : (mineOnly && !unfiltered ? items.filter(e => e.entry != null) : items)` — membership, not status, so Dropped/Plan-to-watch count like Completed.
- [x] 2.3 Add the "In my list" toggle beside the all-groups control: `aria-pressed`, sets `mineOnly`, clears `unfilteredGroups`, and expands every group so its effect is always visible.
- [x] 2.4 Rewrite the all-groups control: label `Collapse (all)` only when nothing is hidden anywhere, otherwise `Expand (all)`; expand sets `mineOnly = false` + clears overrides + expands every group; collapse collapses every group (including in-list tiles) and clears overrides. Keep the existing single-group "without the word all" wording rule.
- [x] 2.5 Make the per-group "+N more" control appear whenever a group renders fewer tiles than it holds — expanding a collapsed group, or adding a filtered group to `unfilteredGroups` — and always render the group heading with its total count even when it shows no tiles.
- [x] 2.6 Style the new toggle in `SeriesPage.css` consistent with the existing `series-page__toggle-all` control, and lay the two controls out side by side in the More header.
- [x] 2.7 Verify in the running app: opening a series with many extras shows only in-list extras expanded; Expand all reveals everything and flips the label; Collapse all hides everything including in-list tiles; the "In my list" toggle round-trips; "+N more" affects one group only; editing an extra into my list from a revealed tile keeps it visible under the filter.

## 3. Series stats and tile width (frontend)

- [x] 3.1 Replace `isCompletedAndScored` in `SeriesPage.tsx` with `isScoreRevealableStatus(entry.entry?.status)` for the Highest MAL score box, feeding the same predicate to the inner `ScoreValue`'s `completed` prop; update the surrounding comment to the new rationale (settled status, score not required).
- [x] 3.2 Widen `.series-page__extras-grid`'s track to `minmax(172px, 1fr)` and update its comment with the measured worst case (`TV special · 2003 · 99 ep` ≈ 167px including padding and letter-spacing).
- [x] 3.3 Verify in the running app that a 2-digit-episode `TV special`/`Special` tile shows its full meta line at the narrowest column the grid produces, that landscape tiles still span two tracks and render whole, and that a dropped highest-MAL entry now names itself in the stats.

## 4. MAL score on browse cards (frontend)

- [x] 4.1 Extend `AnimeCardMeta` with a `malScore: number | null` prop, rendering the score as a trailing `.anime-card__meta-score.score--mal` span with `toFixed(2)`, only when `!hidden` (from `useScoreVisibility()`) and the score is non-null — no `ScoreValue`, so no reveal control can appear.
- [x] 4.2 Turn `.anime-card__meta` into a `justify-content: space-between` flex row in `AnimeCard.css`, with a comment stating that the score is omitted rather than slot-reserved here and why nothing can reflow.
- [x] 4.3 Pass `malScore={item.malScore}` from `SeasonPage` and `SearchPage`; leave the search page's series cards rendering `SeriesBadge` in that slot unchanged.
- [x] 4.4 Verify in the running app: scores appear bottom-right on both pages aligned with the type/episode text; an unscored anime shows nothing there; toggling the navbar hide switch removes every card score with no eye icons and no shift in the type/episode text; enabling "Always show MAL scores for completed and dropped shows" does not reopen them on these two pages.

## 5. Search result line boxes (frontend)

- [x] 5.1 `SearchBar.css`: `.search-bar__result-title` `line-height` 20px → 22px, `.search-bar__result` `height` 52px → 56px, and rewrite the two derivation comments to state the constraint (glyph content area is 21px at the 18px root font) rather than only the arithmetic.
- [x] 5.2 `SeriesBadge.css`: `.series-badge__pill` `line-height` 14px → 16px, updating its comment (the badge line stays inside the card's inherited line box, so a series card is still no taller than the anime cards beside it).
- [x] 5.3 Verify at both ends of the fluid root font (window ~1100px → 16px root, ~1512px → 18px root): no clipped glyphs on dropdown titles, dropdown badge lines, or results-page badge lines; dropdown rows stay uniform in height and the dropdown does not resize while typing; a series card in the results grid is no taller than the anime cards in its row.

## 6. Checks

- [x] 6.1 `cd frontend && PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run lint` and `… npm run build` both clean.
- [x] 6.2 Backend build/test clean via the .NET 10 SDK image.
- [x] 6.3 Re-read the four delta specs against the implementation and confirm every scenario is satisfied (or the spec is corrected to match a deliberate change of mind).

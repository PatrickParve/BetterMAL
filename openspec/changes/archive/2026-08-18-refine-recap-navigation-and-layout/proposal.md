## Why

Recaps shipped behind a single entry point — a "Recap a period" button buried in My list that navigates away to a page most of the app never links to. The recap page itself leads with stats and hot takes and buries the top 10 beneath them, so the thing a recap is actually for arrives last. And the two things a period recap makes you curious about — *which* season or year I enjoyed most, and which one I actually sank the most hours into — are answered by one ranking and not the other.

## What Changes

- **Recap becomes a primary destination.** A Recap link joins the nav bar, opening the current year under **What aired** — a live "what came out this year" view available from anywhere, with no picker in the way.
- **My list's period button stops navigating.** The "Recap a period" control keeps its picker but now applies the chosen period as a scope to the list itself, showing exactly the anime that aired in (or that I watched during) that period. The scope chip gains a link to the full recap for the same period, so the recap page stays one click away. **BREAKING** for anyone with a bookmarked expectation that this button opens `/recap`.
- **The period button moves in with the status tabs**, so period is chosen from the same row as Watching/Completed/Dropped rather than reading as a separate feature.
- **The recap page is re-laid out**: the top anime of the period leads the page, with the stat block beside it on the left rather than above it. Rankings follow, then hot takes.
- **The season and year rankings sit side by side** when a period produces both, each capped at five rows with its own "See all" overlay — today only the year ranking caps and only the year ranking has an overlay.
- **A third ranking is added: most time watched** — the seasons (or years) of the period ordered by the hours I spent on their anime, capped at five with a "See all" overlay of its own. Distinct from the existing rankings, which order by how much I *liked* a period, not how much of it I consumed.
- **Hot takes go from three to five.**
- **The "Anime" stat is relabelled** to say what it counts (the entries the period includes) and is presented alongside Completed and Dropped so the tiles stop reading as near-duplicates of each other.

## Capabilities

### New Capabilities

None — every change extends behaviour already specified under `list-recaps`.

### Modified Capabilities

- `list-recaps`: adds a nav-bar entry point with a defined default period; changes My list's period control from a navigation to an in-place list scope and relocates it into the status-tab row; reorders the recap page so the top anime and stats lead; caps the season ranking at five with a "See all" overlay and pairs the two rankings side by side; adds a time-watched ranking of the period's seasons or years; raises hot takes from three to five; restates the stat block's entry-count stat and adds a dropped count.

## Impact

- **Frontend**: `components/Navbar/Navbar.tsx` (+ `.css`) for the nav entry; `pages/MyListPage.tsx` (+ `.css`) for the scope-instead-of-navigate behaviour, button placement, and the scope chip's recap link; `pages/RecapPage.tsx` (+ `.css`) for the layout reorder, the paired rankings, the new ranking block, and the stat tiles; `components/RecapPickerOverlay.tsx` for a confirm that hands back a scope rather than a recap route; `components/YearRankingOverlay.tsx` generalised to serve all three rankings; `api/types.ts` for the new DTO fields.
- **Backend**: `Services/Recap/RecapDto.cs` (dropped count, time-watched ranking rows), `RecapStatsBuilder.cs` (dropped count, five hot takes), `RecapRankingBuilder.cs` (time-watched ranking), `RecapService.cs` (wiring and the ranking's own eligibility gate). No new endpoints, no schema or cache changes — the recap response grows two fields.
- **Tests**: `RecapStatsBuilderTests`, `RecapRankingBuilderTests`, `RecapControllerTests` extend to cover the new stat, the new ranking, and the raised hot-take count.

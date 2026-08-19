## Why

The recap page works but reads as a stack of loosely related blocks: two headings compete to name the same thing ("Recap", then the period below it), the stat block sits ahead of the top 10 that it describes, only the winning row of each ranking is illustrated, and its rows are the one place in the app where hovering an anime gives no feedback. Moving between periods also costs two interactions with a `<select>` for what is almost always a step of one season or one year, and a season recap dead-ends rather than offering the season page it is obviously about.

Separately, the recap's hot takes and the profile page's opinion-divergence lists claim to apply the same rule but do not: the recap shows its five most divergent picks whatever their divergence, so a period of unremarkable agreement still produces five "hot takes". The two surfaces should agree on what counts as a hot take.

## What Changes

- **One title, not two.** The page's `<h1>` names the period directly — "Fall 2019 recap", "2022 recap", "2011–2020 recap" — and the separate period heading below the controls is removed. The stat block, which no longer sits under that heading, gains its own "Stats" heading.
- **Stats move to the right** of the top 10 rather than leading it, and each stat tile picks up a hover highlight so the tile under the pointer is identifiable. Below the layout's breakpoint the two still stack, top 10 first.
- **Anime rows glow on hover.** Top-10 rows, hot-take rows, and ranking rows adopt the app's standard accent hover treatment — the same tinted background, accent border, and elevation used by the anime cards on my list, the season page, and top anime.
- **Every ranked season and year is illustrated.** The season and year rankings show the three highest-scored anime for every row, not just the leader — inline and in the "See all" overlay, on the recap page and on the profile page's Favourite seasons / Favourite years. The two time-watched rankings keep their leader-only posters.
- **Rankings group by level.** The season ranking and "Seasons by time watched" share a left column; the year ranking and "Years by time watched" share a right column, so a period's season story and year story each read top to bottom. Narrow displays stack season-first as before.
- **Hot takes apply the profile page's rule.** An included anime is a hot take only when it clears the profile's divergence threshold and its like/dislike gates; qualifying hot takes are ordered by the size of the divergence, largest first. Periods with no qualifying anime keep the existing "no hot takes for this period" note rather than showing near-agreements as hot takes.
- **Period stepper arrows.** A season recap gains previous/next arrows that move one season (crossing year boundaries); a yearly recap gains arrows that move one year. Multi-year recaps get no arrows — a two-ended range has no single step.
- **A season recap links to its season page**, opening `/season` on the same year and season.

## Capabilities

### New Capabilities

None — this change refines behaviour already covered by existing specs.

### Modified Capabilities

- `list-recaps`: the page title composes mode and period into one heading; the stat block moves right of the top 10, gains a heading, and gains per-tile hover feedback; anime rows gain the app's standard hover highlight; the season and year rankings illustrate every row rather than only the leader; the four rankings regroup into a season column and a year column; hot takes gate on the profile page's divergence rule instead of showing the five most divergent unconditionally; season and yearly recaps gain period stepper arrows; a season recap offers a link to its season page.
- `profile-stats`: Favourite seasons and Favourite years illustrate every ranked row with three posters instead of only the top-ranked one.

## Impact

- **Backend** — `RecapRankingBuilder` populates `TopPosters` on every season/year ranking row (the profile page's favourites read from the same builder, so both surfaces move together); `RecapStatsBuilder.BuildHotTakes` applies the divergence threshold and like/dislike gates that currently live as private constants in `ProfileService`, which means promoting those constants and the gate predicate to a shared home alongside `ScoreDivergence`. Existing tests in `RecapStatsBuilderTests` that assert five unconditional hot takes and in `RecapRankingBuilder`'s suite that assert leader-only posters will need updating.
- **Frontend** — `RecapPage.tsx` and `RecapPage.css` carry the title, stats, ranking-layout, hover, arrow, and season-link work; `RankingSection.css` and `RankingOverlay.css` carry the row hover and the multi-row poster layout; `ProfilePage` inherits the poster change with no code edit. `api/types.ts` doc comments about "empty for every row but the leader" need correcting for the score rankings.
- **No API shape change** — `RecapRankingPosterDto` and every DTO field stay as they are; only which rows carry posters changes.

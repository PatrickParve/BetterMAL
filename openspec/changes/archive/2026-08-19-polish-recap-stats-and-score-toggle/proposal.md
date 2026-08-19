## Why

Three loose ends are left over from the recap work. In a yearly recap the season ranking and "Seasons by time watched" sit side by side, but only the first has posters, so its rows are half again as tall as its column-mate's and the two lists no longer read as a pair. The stat block answers what I finished in a period but not what I am still in the middle of from it. And the recap has no rating distribution at all, though the profile page has had one from the start — where its counts and percentages run together in one string, so neither column lines up down the block.

Separately, two behaviours are underspecified. Seasons and years that tie on their weighted score fall back to "more scored anime, then oldest first", which stops short: two groups with the same score *and* the same number of scored anime are ordered by nothing meaningful, even though one may be carrying two 10s where the other carries one. And the navbar's hide/unhide control is a plain text button that reads as a link — nothing about it says the app is in one of two states, or which.

## What Changes

- **Ranking rows are one height.** Every row of every ranking — on the recap page and the profile page, inline and in the "See all" overlay — occupies the same minimum height whether or not it carries posters, so a poster-less ranking beside an illustrated one reads as its equal rather than as a compressed list.
- **A "Currently watching" stat.** The recap stat block gains a tile counting the anime from the recap's period that I am currently watching — from that season, that year, or that range of years, according to the recap shown. It counts on when the anime *aired*, in every mode and under both time filters, since an unfinished entry has no completion date to attribute.
- **The recap gets a rating distribution.** Under the stat block, a recap shows how many of its anime I gave each score, as the same bar-per-score block the profile page has, computed over the period's included entries. It carries no mean-score line of its own — the stat block already reports the period's mean directly above it — and it is sized for the narrower column it sits in.
- **The distribution's columns line up.** On both pages, each row's count and its percentage share become two separately-aligned columns instead of one run-together string, so counts read down against counts and percentages down against percentages.
- **Ties in the season and year rankings resolve all the way down.** When two seasons or two years have the same weighted score — the whole number, every decimal of it, not merely the two decimals shown — the one with more scored anime ranks first, as now. When they also have the same number of scored anime, they are compared score by score from 10 down to 1, and the first score at which one holds more anime than the other puts it ahead. Only when even those are identical does the tie break on recency — **the newer season or year first**, reversing today's oldest-first fallback. This holds wherever these rankings appear: the recap page's season and year rankings and the profile page's Favourite seasons and Favourite years.
- **The score toggle becomes a switch.** The navbar's "Hide scores"/"Show scores" button is replaced by a two-state switch: a track labelled with the word it governs, and a knob that slides from one side to the other over it, carrying an open eye while scores are shown and a closed eye while they are hidden. The slide and the eye's open/close are animated, and the animation is dropped for users who ask for reduced motion.

## Capabilities

### New Capabilities

None — every change refines behaviour already covered by an existing spec.

### Modified Capabilities

- `list-recaps`: the stat block gains a currently-watching count; the recap gains a rating distribution below its stats; ranking rows take a uniform minimum height regardless of posters; the season and year rankings' tie-break gains a score-by-score comparison and reverses its final chronological fallback to newest-first.
- `profile-stats`: the score distribution's count and share become separately aligned columns; Favourite seasons and Favourite years inherit the same extended tie-break, including the newest-first fallback; the favourites' ranking rows take the same uniform row height.
- `score-visibility`: the global hide toggle is presented as an animated two-state switch with an open/closed eye, rather than as a text button.

## Impact

- **Backend** — `RecapStatsBuilder.Build` gains a currently-watching count, which needs the period's aired-attributed entries as well as the entries the selected filter includes; `RecapService` already computes that set for `airedCount` and can pass the list rather than discard it. `RecapStatsDto` gains one field. `RecapRankingBuilder`'s `BuildSeasonRanking`/`BuildYearRanking` take the extended tie-break, which the profile page's favourites inherit unchanged since `ProfileService` ranks through the same builder. Existing ordering assertions in `RecapRankingBuilderTests` and `ProfileServiceFavouriteSeasonsAndYearsTests` that depend on the oldest-first fallback will need updating.
- **Frontend** — the profile page's distribution markup moves into a shared component so the recap can render the same block, and both pick up the column split; `RecapPage` renders it below the stat tiles and computes its buckets from the `items` it already holds, so no distribution DTO is added for the recap. `RankingSection.css` and `RankingOverlay.css` carry the uniform row height. `Navbar.tsx`/`Navbar.css` carry the switch; `ScoreVisibilityContext` is unchanged, since only the control's presentation changes, not the state it drives.
- **No API shape change beyond one added stat field** — the recap's rating distribution is derived client-side from `RecapRowDto.myScore`, which the response already carries for every included entry.
- **Unaffected** — `navigation-and-search`'s navbar-order requirement still holds: the switch occupies the same slot between the search field and Profile, and still gives the hover feedback that spec requires of navigation controls.

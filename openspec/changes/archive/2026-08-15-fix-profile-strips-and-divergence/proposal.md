## Why

Three things on the profile page are visibly wrong.

The poster strips promise ten tiles across and deliver nine and a bit: each tile's `flex` basis is `calc((100% - 9 * 10px) / 10)`, but the tiles carry a 1px border under the default `content-box` sizing, so ten tiles are 20px wider than the strip they're supposed to fill. With exactly ten entries the strip is also non-scrollable by design, so the tenth tile is simply cut off with no way to reach it. The **Latest updates** feed has the same class of problem vertically: a 340px cap against 58px rows and 8px gaps leaves 18px past the fifth row, so the sixth row peeks in as a sliver.

The opinion-divergence lists are lopsided to the point of being broken: "I liked it, they didn't" holds a single anime. The rule requires a MAL average below 7 — but MAL averages are compressed (across the 458 rated anime in this list: mean 7.83, SD 0.75) while personal scores use the full range (mean 7.22, SD 1.44). A rule with the same absolute gap on both sides can never be symmetric against two scales that different.

## What Changes

**Poster strips fit ten tiles**
- The tiles in **My top anime**, **Top series**, and **Most rewatched** size with `border-box`, so their 1px borders sit inside the computed basis and ten tiles plus nine gaps equal the strip's visible width exactly — the tenth tile is whole, and the strip's 8px padding stays free for the hover scale.

**Latest updates shows five whole rows**
- The feed shows exactly five rows and no sliver of a sixth, at any window width. The row height is derived from the feed's own height rather than fixed, so the rows grow to fill the box instead of the box growing dead space — which at today's box height makes rows ~60px and posters ~44×60 instead of 56px/41×56, the size bump the sliver was hiding.
- The feed keeps filling its box to the bottom edge, as it does today.

**Opinion divergence rules are rebuilt**
- Both lists compare *standardized* scores: each score is measured in standard deviations from its own scale's mean, computed over the anime I've rated that also have a MAL average. A title qualifies when the two sides differ by at least 1.0 SD.
- A label gate keeps each list honest about its own claim: "They liked it, I didn't" also requires my score ≤ 5 and MAL ≥ 7.5; "I liked it, they didn't" also requires my score ≥ 8 and MAL ≤ 7.5. The 5/8 boundaries are MAL's own score labels — 5 is "Average" and below it lies everything worse, 8 is "Very Good" and above it everything better, leaving 6 ("Fine") and 7 ("Good") as a neutral band that belongs in neither list. The 7.5 boundary splits MAL's community scale between its "Good" and "Very Good" labels.
- On the current list this yields 22 and 24 entries (from 16 and 1), topped by *The First Slam Dunk* (me 3, MAL 8.70) and *Kanojo, Okarishimasu 5th Season* (me 9, MAL 6.31).
- Both lists are uncapped and ordered by divergence, strongest first; each box shows ten rows and scrolls to the rest, with its scrollbar beside the rows like the other profile lists.
- Divergence needs a population to normalize against: with fewer than 10 rated pairs, or a zero spread on either scale, both lists are empty rather than showing noise.

**BREAKING**: none — no API shape changes, no stored data changes. The two divergence lists' membership changes for every user of the page.

## Capabilities

### New Capabilities

None — this fixes existing profile-page behaviour.

### Modified Capabilities

- `profile-stats`: replaces the opinion-divergence membership rules with standardized-divergence rules plus label gates, and requires the lists be ordered by divergence, height-capped, and scrollable; requires the poster strips to actually fit ten tiles across their visible width; requires the Latest updates feed to show whole rows only, with its row height derived from the box height; extends the "scrollbars sit beside content" requirement to cover the divergence lists.

## Impact

**Backend**
- `Services/Profile/ProfileService.cs` — `BuildOpinionDivergence` computes the two scales' mean and standard deviation over the rated pairs and filters/orders on standardized divergence; new guard for small or zero-spread populations.

**Frontend**
- `pages/ProfilePage.css` — `box-sizing` on the three strip-tile rules; `.activity-feed` becomes a size container with a derived row height, and its rows/posters size from it; `.divergence-list` gains a ten-row cap.
- `pages/ProfilePage.tsx` — the divergence lists get the shared `scroll-y` treatment.

**Not affected**
- The full edit-history overlay keeps its own row sizing; the divergence rows keep the 56px height they have today.

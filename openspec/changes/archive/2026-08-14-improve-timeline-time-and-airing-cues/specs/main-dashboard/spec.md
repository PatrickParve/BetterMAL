## MODIFIED Requirements

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section on the home page containing anime in my list that belong to the current viewing season, filterable by popularity, MAL score, alphabetical, and my score. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime rather than treated as most popular.

An anime SHALL be included when it is in my list and either its MAL airing status is `currently_airing`, or it has already premiered and its start date falls in the current season quarter. "Has already premiered" SHALL mean its start date is on or before today in local terms. An anime with no recorded start date SHALL be included only while MAL reports it as currently airing. A current-season anime that has finished airing — a movie, a short, or a completed TV run — SHALL therefore remain in the section for the rest of that season. A current-season anime that has not premiered yet SHALL be excluded until its start date passes.

Season membership SHALL be derived from the anime's own start date rather than from a cached season listing, so that the section's contents do not depend on which season pages have been browsed.

Each card in this section SHALL show an **airing progress bar** rather than a watched/total progress bar. The bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as the primary fill, in a blue colour distinct from the site's purple accent, and SHALL label it `aired/total`. When the total episode count is unknown the label SHALL show `aired/?`; when the aired count cannot be determined the label SHALL show `?/total` and the primary fill SHALL be empty rather than showing a fabricated value.

When the total episode count is unknown, the blue fill SHALL be determined by whether the run is over. When MAL reports the anime as finished airing, the blue fill SHALL span the full track — the show has broadcast everything it is going to, even though no episode count is published. Otherwise, when the aired count is known, the blue fill SHALL span exactly half the track regardless of how many episodes have aired — a fixed "progress so far, end unknown" marker rather than a proportion of a total that does not exist. When the run is not finished and neither the total nor the aired count is known, the blue fill SHALL be empty.

The dashboard payload backing this section SHALL carry, per anime, whether MAL reports it as finished airing.

When I have watching progress on that anime — episodes watched greater than zero — the bar SHALL additionally render my watched progress as a fill in the site's purple accent colour, layered on top of the aired fill within the same track, measured against the same total episode count, so that the accent extent reads as how far I have watched and the blue extent reads as how far the show has broadcast. Both fills SHALL be clamped so neither can exceed the width of the track. When episodes watched is zero, no accent fill SHALL be rendered. When the total episode count is unknown, the purple fill SHALL be measured within the blue extent against the aired count instead, so that being caught up on every aired episode covers the whole blue extent and the purple fill never exceeds it; when the aired count is also unknown, my watched count SHALL serve as its own measure so a watched title still reads as covered.

When an episode is incremented elsewhere on the home page for an anime that also appears in this section, that anime's purple fill here SHALL update to match without a page reload.

This bar — the `aired/total`-labelled bar specified here, with no editable count — SHALL apply only to the home page's followed-shows-airing section. Broadcast progress itself is not exclusive to this section: the anime detail page draws its own aired fill behind my watched fill while an anime is currently airing, per the anime-detail capability, and the series page does the same for its own progress figures. The watched/total progress bar SHALL remain unchanged everywhere else it is used, including My List and the currently-watching carousel.

#### Scenario: Filtering current season
- **WHEN** I choose a sort/filter (popularity, MAL score, alphabetical, or my score)
- **THEN** the current-season cards reorder accordingly

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort the current-season section by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** those unranked anime appear last rather than at the top

#### Scenario: Finished current-season title stays listed
- **WHEN** a my-list movie or short whose start date falls in the current season quarter has already premiered and finished airing
- **THEN** it still appears in the section, with its aired bar full

#### Scenario: Completed TV run stays listed
- **WHEN** a my-list TV anime whose start date falls in the current season quarter finishes its run mid-season and its MAL status flips to finished airing
- **THEN** it remains in the section for the rest of the season rather than disappearing

#### Scenario: Unpremiered current-season title excluded
- **WHEN** a my-list anime's start date falls in the current season quarter but is still in the future
- **THEN** it does not appear in the section

#### Scenario: Anime from an earlier season excluded
- **WHEN** a my-list anime finished airing in a previous season
- **THEN** it does not appear in the section

#### Scenario: Membership does not depend on browsing history
- **WHEN** the section renders and the current season's browse page has never been opened
- **THEN** premiered current-season titles are listed just the same, because membership comes from each anime's own start date

#### Scenario: Bar shows broadcast progress
- **WHEN** a followed-shows-airing card renders for an anime where 5 of 12 episodes have aired
- **THEN** the blue aired fill spans 5/12 of the track and the label reads `5/12`

#### Scenario: Watched progress layered in the accent colour
- **WHEN** a followed-shows-airing card renders for an anime where 5 of 12 episodes have aired and I have watched 3
- **THEN** a purple fill spanning 3/12 of the track is drawn on top of the blue fill, leaving the blue visible from 3/12 to 5/12

#### Scenario: No watching progress
- **WHEN** a followed-shows-airing card renders for an anime I have not started (episodes watched is zero)
- **THEN** only the blue aired fill is drawn and no purple fill appears

#### Scenario: Caught up with the broadcast
- **WHEN** a followed-shows-airing card renders for an anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither fill extends past the aired portion of the track

#### Scenario: Progress bar with unknown total
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total episode count, and 3 episodes have aired
- **THEN** the blue fill spans half the track and the label reads `3/?`

#### Scenario: Unknown total, many episodes aired
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total episode count, and 40 episodes have aired
- **THEN** the blue fill still spans exactly half the track rather than more

#### Scenario: Unknown total with watching progress
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, 10 episodes have aired, and I have watched 5
- **THEN** the purple fill covers half of the blue half-track extent, and watching all 10 would cover the whole blue extent

#### Scenario: Unknown total, watched a quarter of what aired
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, 8 episodes have aired, and I have watched 2
- **THEN** the purple fill covers a quarter of the blue half-track extent — the accent is proportional to episodes watched out of episodes aired, not a fixed fraction of the blue fill

#### Scenario: Unknown total, watched ahead of the aired estimate
- **WHEN** a followed-shows-airing card's anime is still airing, has an unknown total, the aired count is estimated at 4, and I have watched 5
- **THEN** the purple fill covers the whole blue half-track extent and does not spill past it

#### Scenario: Finished run with no published episode count
- **WHEN** a followed-shows-airing card's anime is reported by MAL as finished airing and has no published total episode count
- **THEN** the blue fill spans the full track rather than half or none

#### Scenario: Finished movie I have watched
- **WHEN** a followed-shows-airing card's anime is a finished movie with neither a published total nor a determinable aired count, and I have watched it
- **THEN** the blue fill spans the full track, the purple fill covers it entirely, and the label reads `?/?`

#### Scenario: Unknown aired count
- **WHEN** a followed-shows-airing card's anime has a known total but no determinable aired-episode count
- **THEN** its label shows `?/total`, the blue fill is empty, and any purple watched fill is still drawn

#### Scenario: Neither count known and still airing
- **WHEN** a followed-shows-airing card's anime is still airing and has neither a total episode count nor a determinable aired count
- **THEN** its label shows `?/?` and the blue fill is empty rather than half-filled

#### Scenario: Increment elsewhere on the page updates this bar
- **WHEN** I increment an episode from the currently-watching carousel for an anime that also appears in the followed-shows-airing section
- **THEN** that anime's purple fill in the followed-shows-airing section grows to match, without a page reload

#### Scenario: Other views keep the watched progress bar
- **WHEN** I view My List or the currently-watching carousel
- **THEN** the episode bar there still shows watched/total with no aired fill and no purple overlay

#### Scenario: The detail page draws its own aired fill
- **WHEN** I open the detail page of a currently airing anime
- **THEN** its progress bar shows an aired fill behind my watched fill, per the anime-detail capability, rather than this section's `aired/total`-labelled bar

## MODIFIED Requirements

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section on the home page containing only anime in my list that are airing this season, filterable by popularity, MAL score, alphabetical, and my score. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime rather than treated as most popular.

Each card in this section SHALL show an **airing progress bar** rather than a watched/total progress bar. The bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as the primary fill, in a blue colour distinct from the site's purple accent, and SHALL label it `aired/total`. When the total episode count is unknown the label SHALL show `aired/?`; when the aired count cannot be determined the label SHALL show `?/total` and the primary fill SHALL be empty rather than showing a fabricated value.

When I have watching progress on that anime — episodes watched greater than zero — the bar SHALL additionally render my watched progress as a fill in the site's purple accent colour, layered on top of the aired fill within the same track, measured against the same total episode count, so that the accent extent reads as how far I have watched and the blue extent reads as how far the show has broadcast. Both fills SHALL be clamped so neither can exceed the width of the track. When episodes watched is zero, no accent fill SHALL be rendered.

This bar SHALL apply only to the home page's followed-shows-airing section. The watched/total progress bar SHALL remain unchanged everywhere else it is used, including My List, the anime detail page, and the currently-watching carousel.

#### Scenario: Filtering current season
- **WHEN** I choose a sort/filter (popularity, MAL score, alphabetical, or my score)
- **THEN** the current-season cards reorder accordingly

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort the current-season section by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** those unranked anime appear last rather than at the top

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
- **WHEN** a followed-shows-airing card's anime has an unknown total episode count
- **THEN** its bar shows `aired/?`

#### Scenario: Unknown aired count
- **WHEN** a followed-shows-airing card's anime has no determinable aired-episode count
- **THEN** its label shows `?/total`, the blue fill is empty, and any purple watched fill is still drawn

#### Scenario: Other views keep the watched progress bar
- **WHEN** I view My List, an anime detail page, or the currently-watching carousel
- **THEN** the episode bar there still shows watched/total with no aired fill and no purple overlay

## ADDED Requirements

### Requirement: Aired-episode count for followed airing shows
The dashboard data for the followed-shows-airing section SHALL include, per anime, the number of episodes that have aired as of the current instant. The count SHALL be derived from the same episode-schedule source of truth that backs "Airing today" and the next-episode countdown: exact per-episode air dates when they are cached, and the bounded weekly-cadence estimate otherwise. An episode SHALL be counted as aired only once its broadcast instant has passed. The count SHALL never exceed the anime's total episode count when that count is known, and SHALL be reported as unknown rather than guessed when the anime has neither cached per-episode dates nor enough schedule data (start date and broadcast time) to place episodes on a timeline.

#### Scenario: Aired count from cached per-episode dates
- **WHEN** the aired count is computed for an anime whose per-episode air dates are cached and whose episodes 1 through 4 have broadcast instants in the past while episode 5 is in the future
- **THEN** the reported aired count is 4

#### Scenario: Aired count from the weekly estimate
- **WHEN** the aired count is computed for an anime with no cached per-episode dates but with a known start date and broadcast time, three broadcast slots having passed since it premiered
- **THEN** the reported aired count is 3

#### Scenario: Today's episode has not aired yet
- **WHEN** the aired count is computed on a day this anime broadcasts, before its broadcast time has passed in local terms
- **THEN** today's episode is not counted as aired

#### Scenario: Finished show counts every episode
- **WHEN** the aired count is computed for an anime that has finished airing with a known total episode count
- **THEN** the reported aired count equals the total episode count

#### Scenario: Aired count is unknown
- **WHEN** the aired count is computed for an anime with no cached per-episode dates and no start date or no broadcast time
- **THEN** the aired count is reported as unknown rather than as zero or an estimate

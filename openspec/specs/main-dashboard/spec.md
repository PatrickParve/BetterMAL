# main-dashboard Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Currently watching horizontal carousel
The system SHALL show a "Currently watching" section on the main page as a horizontal row of cards navigable with left/right arrows. At most 5 cards SHALL be visible at once; the visible row SHALL be bounded to the width of 5 cards rather than growing with the number of entries. When there are more than 5 entries, the row SHALL scroll as a bounded list: it stops at the first card when scrolling left and at the last card when scrolling right, and SHALL NOT wrap around from the last card to the first or from the first card to the last. Each entry SHALL be rendered exactly once. The left/right arrows SHALL be shown only when the row has more entries than fit on screen (i.e. scrolling is actually possible); when all cards already fit, no arrows are shown. Clicking a card's picture or title SHALL open that anime's detail page. Each card SHALL have a plus control next to its episode count that increments episodes-watched, applying the started-date and debounced-sync logic; clicking the card's picture or title navigates and does not increment.

The 5-card bound SHALL be exact: at every resting scroll position, no part of a 6th card SHALL be visible. Whenever a card sits flush against the row's left edge — the row scrolled fully left, or scrolled by whole cards with the arrows — exactly that card and the 4 after it SHALL be visible, and the next card SHALL be entirely outside the visible strip. When the row is scrolled fully right, the last card SHALL likewise sit flush against the right edge with exactly the 4 cards before it visible and no sliver of a further card at either edge. The space the row reserves for edge-card hover treatments SHALL NOT count toward the visible strip's card budget.

The row's scroll position SHALL be preserved when a card's episodes-watched changes: incrementing from the plus control SHALL update the card in place and leave the row scrolled exactly where it was, for any number of entries.

Each card SHALL show the standard watched/total progress bar directly in front of its episode count, on the same line, so the card reads as `[bar] watched/total [+]`. The bar SHALL be the same watched/total bar used on My List and the anime detail page — the accent fill measured as episodes watched out of total episodes — and SHALL show an empty track when the total episode count is unknown, with the count still reading `watched/?`. Incrementing from the plus control SHALL update the bar's fill together with the count.

The progress row — the bar, the `watched/total` count, and the plus control — SHALL NOT be part of the card's navigation link: clicking anywhere within that row SHALL NOT navigate to the detail page. The count itself SHALL be directly editable in place, per the "Inline editable episode count" requirement.

The row SHALL reserve enough space inside its scrollable area for hover treatments on the edge cards, so that hovering the first or last card's plus control (or the card itself) renders the full hover state without any part being clipped by the row's scroll boundary, both when the row is scrolled fully left and fully right.

#### Scenario: At most five cards visible
- **WHEN** the Currently watching section renders with more than 5 entries
- **THEN** at most 5 cards are visible at once and the remaining entries are reached by scrolling

#### Scenario: No sliver of a sixth card at rest
- **WHEN** the row has more than 5 entries and is scrolled fully left
- **THEN** exactly 5 cards are visible and no part of the 6th card's picture is visible past the fifth

#### Scenario: No sliver after an arrow click
- **WHEN** the row has more than 5 entries and I click the right arrow so the row advances by one card
- **THEN** the newly leftmost card sits flush at the left edge, 5 cards are visible, and no part of the card before it or the card after them is visible

#### Scenario: Flush at the right end
- **WHEN** the row has more than 5 entries and I scroll or click right until it stops
- **THEN** the last card sits flush at the right edge with the 4 cards before it visible, and no sliver of an earlier card shows at the left edge

#### Scenario: Scrolling stops at the end
- **WHEN** the row has more than 5 entries and I scroll right to the last card
- **THEN** the row stops there and does not continue back to the first card

#### Scenario: Scrolling stops at the start
- **WHEN** the row has more than 5 entries and I scroll left to the first card
- **THEN** the row stops there and does not continue back to the last card

#### Scenario: Each entry appears once
- **WHEN** the row has more than 5 entries and I scroll from the first card to the last
- **THEN** each currently-watching anime is shown exactly one time, with no duplicated copies of the list

#### Scenario: Scroll position survives an increment
- **WHEN** the row has more than 5 entries, I have scrolled to a later card, and I click that card's plus control
- **THEN** the count and bar update in place and the row stays scrolled where it was rather than jumping back to the first card

#### Scenario: Navigating the carousel
- **WHEN** the row has more entries than fit and I click the left or right arrow on the Currently watching row
- **THEN** the row scrolls to reveal more currently-watching cards

#### Scenario: Arrows hidden when everything fits
- **WHEN** there are 5 or fewer currently-watching cards and they all fit within the visible width
- **THEN** no left/right arrows are shown

#### Scenario: Opening a card
- **WHEN** I click a currently-watching card's picture or title
- **THEN** I am taken to that anime's detail page without changing its episode count

#### Scenario: Incrementing from the plus control
- **WHEN** I click the plus control next to a card's episode count
- **THEN** its episodes-watched increases by one and the started-date and debounced-sync rules are applied

#### Scenario: Progress bar in front of the count
- **WHEN** a currently-watching card renders for an anime where I have watched 3 of 12 episodes
- **THEN** a progress bar filled to 3/12 is shown immediately before the `3/12` count, with the plus control after it

#### Scenario: Progress bar with unknown total
- **WHEN** a currently-watching card renders for an anime with an unknown total episode count
- **THEN** its track is empty and the count reads `watched/?`

#### Scenario: Bar follows an increment
- **WHEN** I click the plus control on a currently-watching card
- **THEN** the bar's fill grows along with the incremented count without a page reload

#### Scenario: Clicking the progress bar does not navigate
- **WHEN** I click the progress bar or the empty space around it on a currently-watching card
- **THEN** I stay on the main page and am not taken to the anime's detail page

#### Scenario: Clicking the count does not navigate
- **WHEN** I click the `watched/total` count on a currently-watching card
- **THEN** I stay on the main page and the count becomes editable instead of navigating

#### Scenario: Plus control fully visible on the last card
- **WHEN** the row is scrolled fully to the right and I hover the last card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's right edge

#### Scenario: Plus control fully visible on the first card
- **WHEN** the row is scrolled fully to the left and I hover the first card's plus control
- **THEN** the whole control is visible, with no part of it cut off by the row's left edge

### Requirement: Next-episode countdown on currently-watching cards
The system SHALL show, on each currently-watching card, a countdown to the next episode in the form "Next ep: in X days, Y h", computed from the anime's cached broadcast schedule converted to local time.

#### Scenario: Showing the countdown
- **WHEN** a currently-watching card is rendered for an airing anime with a known broadcast schedule
- **THEN** it shows the time remaining until the next episode as "Next ep: in X days, Y h"

#### Scenario: No known next episode
- **WHEN** a currently-watching anime has no known upcoming broadcast (e.g. it has finished airing)
- **THEN** the card omits the next-episode countdown rather than showing a stale value

### Requirement: Airing today filtered to my list in local time
The system SHALL show an "Airing today" section as a list column (not a grid) containing only anime in my list, filtered by broadcast day converted from JST to local (Finland) time, where each row links to the anime's detail page.

Each row SHALL be laid out as a thumbnail image beside two stacked lines of text. The first line SHALL read `time : Ep N`, where the episode number is the one the episode-schedule service resolves for that anime on that local date. The second line SHALL be the anime's display title.

The row thumbnail SHALL be a poster at the same 2:3 aspect ratio used by anime cards elsewhere in the app, and SHALL be large enough to read as the row's own picture rather than an inline icon — clearly taller than the two lines of text beside it, so the poster, not the text, sets the row's height.

The two text lines SHALL be aligned to the top of the row rather than centred against the thumbnail: the `time : Ep N` line SHALL begin at the top of the thumbnail, with the title starting directly beneath it and the remaining space falling below the text.

The title SHALL be clamped to at most two lines, with an ellipsis (`…`) marking a title cut short, so no single row can grow unbounded in height. The ellipsis SHALL appear only when the title genuinely overflows at the section's rendered width — a title that fits SHALL be shown in full, with no ellipsis and no truncation at a fixed character count.

When the episode number cannot be resolved for that date, the first line SHALL show the time alone rather than a placeholder or a guessed episode number.

The dashboard payload backing this section SHALL carry the resolved episode number per row, or an explicit "unknown" when the schedule service cannot determine it.

#### Scenario: Local-day airing filter
- **WHEN** the main page loads
- **THEN** "Airing today" lists only my-list anime whose broadcast day, converted to local time, is today

#### Scenario: Row layout
- **WHEN** an "Airing today" row renders for an anime whose episode 4 airs at 19:30 local time
- **THEN** the row shows its poster thumbnail beside `19:30 : Ep 4` with the anime's title on the line below

#### Scenario: Poster anchors the row
- **WHEN** an "Airing today" row renders
- **THEN** its thumbnail is a 2:3 poster taller than the two text lines beside it, and the poster's height sets the row's height

#### Scenario: Text starts at the top of the row
- **WHEN** an "Airing today" row renders with a one-line title
- **THEN** the `time : Ep N` line sits at the top of the row level with the top of the poster, the title sits directly below it, and the leftover space is below the title rather than split above and below the text

#### Scenario: Long title clamped
- **WHEN** an "Airing today" row's title is too long to fit on two lines at the section's width
- **THEN** the title is cut off at the end of the second line with an ellipsis rather than wrapping further

#### Scenario: Short title shown in full
- **WHEN** an "Airing today" row's title fits within two lines at the section's width
- **THEN** it is shown in full with no ellipsis

#### Scenario: Unknown episode number
- **WHEN** an "Airing today" row's anime airs today but its episode number cannot be resolved
- **THEN** the first line shows only the local broadcast time, and the title still appears beneath it

#### Scenario: Nothing airing today
- **WHEN** no my-list anime air today in local time
- **THEN** the section shows a small message indicating nothing is airing today

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section on the home page containing anime in my list that belong to the current viewing season, filterable by popularity, MAL score, alphabetical, and my score. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime rather than treated as most popular.

An anime SHALL be included when it is in my list and either its MAL airing status is `currently_airing`, or it has already premiered and its start date falls in the current season quarter. "Has already premiered" SHALL mean its start date is on or before today in local terms. An anime with no recorded start date SHALL be included only while MAL reports it as currently airing. A current-season anime that has finished airing — a movie, a short, or a completed TV run — SHALL therefore remain in the section for the rest of that season. A current-season anime that has not premiered yet SHALL be excluded until its start date passes.

Season membership SHALL be derived from the anime's own start date rather than from a cached season listing, so that the section's contents do not depend on which season pages have been browsed.

Each card in this section SHALL show an **airing progress bar** rather than a watched/total progress bar. The bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as the primary fill, in a blue colour distinct from the site's purple accent, and SHALL label it `aired/total`. When the total episode count is unknown the label SHALL show `aired/?`; when the aired count cannot be determined the label SHALL show `?/total` and the primary fill SHALL be empty rather than showing a fabricated value.

When the total episode count is unknown, the blue fill SHALL be determined by whether the run is over. When MAL reports the anime as finished airing, the blue fill SHALL span the full track — the show has broadcast everything it is going to, even though no episode count is published. Otherwise, when the aired count is known, the blue fill SHALL span exactly half the track regardless of how many episodes have aired — a fixed "progress so far, end unknown" marker rather than a proportion of a total that does not exist. When the run is not finished and neither the total nor the aired count is known, the blue fill SHALL be empty.

The dashboard payload backing this section SHALL carry, per anime, whether MAL reports it as finished airing.

When I have watching progress on that anime — episodes watched greater than zero — the bar SHALL additionally render my watched progress as a fill in the site's purple accent colour, layered on top of the aired fill within the same track, measured against the same total episode count, so that the accent extent reads as how far I have watched and the blue extent reads as how far the show has broadcast. Both fills SHALL be clamped so neither can exceed the width of the track. When episodes watched is zero, no accent fill SHALL be rendered. When the total episode count is unknown, the purple fill SHALL be measured within the blue extent against the aired count instead, so that being caught up on every aired episode covers the whole blue extent and the purple fill never exceeds it; when the aired count is also unknown, my watched count SHALL serve as its own measure so a watched title still reads as covered.

When an episode is incremented elsewhere on the home page for an anime that also appears in this section, that anime's purple fill here SHALL update to match without a page reload.

This bar SHALL apply only to the home page's followed-shows-airing section. The watched/total progress bar SHALL remain unchanged everywhere else it is used, including My List, the anime detail page, and the currently-watching carousel.

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
- **WHEN** I view My List, an anime detail page, or the currently-watching carousel
- **THEN** the episode bar there still shows watched/total with no aired fill and no purple overlay

### Requirement: Aired-episode count for followed airing shows
The dashboard data for the followed-shows-airing section SHALL include, per anime, the number of episodes that have aired as of the current instant. The count SHALL be derived from the same episode-schedule source of truth that backs "Airing today" and the next-episode countdown: exact per-episode air dates when they are cached, and the bounded weekly-cadence estimate otherwise. An episode SHALL be counted as aired only once its broadcast instant has passed. The count SHALL never exceed the anime's total episode count when that count is known, and SHALL be reported as unknown rather than guessed when the anime has neither cached per-episode dates nor enough schedule data (start date and broadcast time) to place episodes on a timeline.

The weekly-cadence estimate SHALL stop accruing episodes at the anime's last air date when one is recorded, so that a show which has finished broadcasting does not keep gaining an estimated episode every week. When the last air date is not recorded, the estimate SHALL continue from the premiere as before.

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

#### Scenario: Estimate stops at the last air date
- **WHEN** the aired count is estimated from the weekly cadence for an anime with no published total episode count whose recorded last air date was ten weeks ago
- **THEN** the count reflects the episodes up to that last air date and does not grow by a further ten

#### Scenario: Aired count is unknown
- **WHEN** the aired count is computed for an anime with no cached per-episode dates and no start date or no broadcast time
- **THEN** the aired count is reported as unknown rather than as zero or an estimate

### Requirement: Dashboard section title dividers
The system SHALL render a thin horizontal divider rule directly beneath the title of each dashboard section on the main page — "Currently watching", "Airing today", and "Followed shows airing" — visually separating the section heading from its content. The divider SHALL span the width of the section's content area.

#### Scenario: Divider under each section title
- **WHEN** the main page renders the "Currently watching", "Airing today", and "Followed shows airing" sections
- **THEN** each section's title is underlined by a thin horizontal divider rule separating the heading from the section content

#### Scenario: Divider spans the section width
- **WHEN** a dashboard section title divider is rendered
- **THEN** the divider spans the width of that section's content area


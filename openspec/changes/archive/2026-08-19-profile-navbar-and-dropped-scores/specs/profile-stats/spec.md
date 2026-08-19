## ADDED Requirements

### Requirement: All-list episode progress
The profile page SHALL show a progress bar reporting how many episodes I have watched against how many episodes the anime in my whole list hold in total, so the `Episodes` stat has something to be measured against. It SHALL be rendered with the app's existing episode progress-bar treatment — a filled track with a `watched / total` label — and SHALL be read-only: it SHALL offer no increment control and no editable watched field, since it summarises the list rather than any one entry.

The figure SHALL be computed from local data with no live API call, over **every** list entry regardless of status — Plan to watch included — with these rules:

- An entry whose anime has **no published total episode count** SHALL be excluded from both sides of the figure. A still-airing or unknown-length show has no total to progress toward, and counting its watched episodes against a total that omits it would report progress above what the denominator can explain.
- An entry's contribution to the watched side SHALL be its episodes watched clamped to its anime's total, so a stored over-count can never push the bar past 100%.
- Rewatches SHALL NOT multiply an entry's contribution: an entry contributes at most its anime's total episode count however many times I have rewatched it.

Because entries are excluded, the section SHALL state how many entries the figure covers out of my total entry count, so the exclusion is visible rather than silent. When no entry in my list has a published total episode count, the section SHALL say so plainly rather than render a bar against a zero total or a percentage that is not a number.

#### Scenario: Progress across the whole list
- **WHEN** the profile page loads
- **THEN** it shows a progress bar whose label reads my total episodes watched against the total episodes of the anime in my list, computed from local data

#### Scenario: Unknown episode counts are excluded
- **WHEN** my list holds an anime with no published total episode count
- **THEN** neither its watched episodes nor any total for it are counted in the bar

#### Scenario: The exclusion is visible
- **WHEN** some of my entries have no published total episode count
- **THEN** the section states how many entries the figure covers out of my total entry count

#### Scenario: Plan-to-watch counts toward the total
- **WHEN** my list holds a plan-to-watch anime with a published episode count
- **THEN** its episodes are counted in the total and contribute nothing to the watched side, so the bar reflects it as unwatched

#### Scenario: An over-count cannot exceed the total
- **WHEN** an entry's stored episodes watched exceeds its anime's total episode count
- **THEN** it contributes only that anime's total to the watched side and the bar does not exceed 100%

#### Scenario: Rewatches do not inflate progress
- **WHEN** I have rewatched an anime several times
- **THEN** it contributes at most its total episode count to the watched side, exactly as a single completed watch does

#### Scenario: The bar is read-only
- **WHEN** I click or focus the progress bar's label
- **THEN** nothing becomes editable and no increment control is offered

#### Scenario: Nothing to measure
- **WHEN** no anime in my list has a published total episode count
- **THEN** the section says so rather than rendering a bar against a zero total

### Requirement: Favourite seasons and years
The profile page SHALL show a **Favourite seasons** ranking and a **Favourite years** ranking, covering my whole list rather than any selected period, so my best-liked seasons and years are reachable without opening a recap.

Each anime SHALL be attributed to the season and the year its air date falls in — the same air-date attribution the recap page's rankings use. An entry whose anime has no air date SHALL be attributed to neither and SHALL be excluded from both rankings. Only seasons and years holding at least one anime I scored SHALL be ranked; a season or year I scored nothing from SHALL be omitted rather than shown as empty or zero.

Both rankings SHALL rank on the same Bayesian weighted average the recap page's season and year rankings use, with the same trust thresholds — 5 for a season, 20 for a year — and the same global mean `C`, my mean score across every scored entry in my list. A season's or year's weighted score on the profile page SHALL therefore equal the score a recap covering that same season or year reports for it, so the two surfaces can never disagree. Ties SHALL be broken by the number of scored anime, then chronologically, so the order is stable across reloads.

Each ranked row SHALL show its rank, its name (season and year, or the year), how many of my anime it was computed over, and the weighted score it was ranked on. The top-ranked season and the top-ranked year SHALL each additionally show the posters of my three highest-scored anime from it — fewer when fewer qualify — and ranks below first SHALL show no posters. Following a row SHALL open the recap for that season or year.

At most five rows SHALL be shown in each ranking. When more than five qualify, that ranking SHALL offer a control that opens an overlay listing every qualifying season or year in rank order — the same overlay treatment the recap page's rankings use.

The two rankings SHALL be presented alongside one another rather than stacked, so my best season and best year are comparable at a glance. On a display too narrow for two columns, they SHALL stack with Favourite seasons first.

When neither ranking has a single qualifying group — nothing I scored carries an air date — the section SHALL say so plainly rather than render two empty rankings.

#### Scenario: Ranking my seasons and years
- **WHEN** the profile page loads and I have scored anime across several seasons and years
- **THEN** it shows a Favourite seasons ranking and a Favourite years ranking, each best first

#### Scenario: Whole list, not a period
- **WHEN** I have scored anime that aired in 2009 and in 2023
- **THEN** both years are candidates for the Favourite years ranking, with no period selection needed

#### Scenario: Agreement with the recap page
- **WHEN** I compare fall 2019's weighted score in the profile's Favourite seasons ranking against the score a recap covering fall 2019 reports
- **THEN** the two are equal

#### Scenario: A thin season is pulled toward the global mean
- **WHEN** one season holds a single anime I scored 10 and another holds eight anime averaging 8.5, and my global mean is 7.4
- **THEN** the eight-anime season ranks above the single-anime one

#### Scenario: Groups I scored nothing from are omitted
- **WHEN** I have watched anime that aired in 2013 but scored none of them
- **THEN** 2013 appears in neither the Favourite years ranking nor its overlay

#### Scenario: Anime with no air date are excluded
- **WHEN** my list holds a scored anime with no air date
- **THEN** it contributes to no season and no year in either ranking

#### Scenario: Five at a time with an overlay for the rest
- **WHEN** more than five years qualify
- **THEN** the five best-ranked are shown and a control opens an overlay listing every qualifying year in rank order

#### Scenario: Five or fewer
- **WHEN** only four seasons qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Only the leader is illustrated
- **WHEN** a ranking shows five rows
- **THEN** only the first shows the posters of my three highest-scored anime from it

#### Scenario: Fewer than three posters available
- **WHEN** the top-ranked season holds only two scored anime
- **THEN** two posters are shown

#### Scenario: Opening a ranked row
- **WHEN** I follow a row of either ranking
- **THEN** the recap page opens on that season or that year

#### Scenario: The two rankings sit side by side
- **WHEN** the profile page is shown on a wide display
- **THEN** Favourite seasons and Favourite years are presented next to each other rather than one above the other

#### Scenario: Narrow display stacks them
- **WHEN** the profile page is shown on a display too narrow for two columns
- **THEN** Favourite seasons is shown first and Favourite years below it

#### Scenario: Nothing to rank
- **WHEN** nothing I have scored carries an air date
- **THEN** the section says so rather than rendering two empty rankings

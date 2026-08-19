## MODIFIED Requirements

### Requirement: Favourite seasons and years
The profile page SHALL show a **Favourite seasons** ranking and a **Favourite years** ranking, covering my whole list rather than any selected period, so my best-liked seasons and years are reachable without opening a recap.

Each anime SHALL be attributed to the season and the year its air date falls in — the same air-date attribution the recap page's rankings use. An entry whose anime has no air date SHALL be attributed to neither and SHALL be excluded from both rankings. Only seasons and years holding at least one anime I scored SHALL be ranked; a season or year I scored nothing from SHALL be omitted rather than shown as empty or zero.

Both rankings SHALL rank on the same Bayesian weighted average the recap page's season and year rankings use, with the same trust thresholds — 5 for a season, 20 for a year — and the same global mean `C`, my mean score across every scored entry in my list. A season's or year's weighted score on the profile page SHALL therefore equal the score a recap covering that same season or year reports for it, so the two surfaces can never disagree. Ties SHALL be broken by the number of scored anime, then chronologically, so the order is stable across reloads.

Each ranked row SHALL show its rank, its name (season and year, or the year), how many of my anime it was computed over, and the weighted score it was ranked on. **Every** ranked row SHALL additionally show the posters of my three highest-scored anime from that season or year — fewer when fewer qualify — rather than only the top-ranked row, so each row is illustrated, both inline and in the overlay. Following a row SHALL open the recap for that season or year.

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

#### Scenario: Every row is illustrated
- **WHEN** a ranking shows five rows
- **THEN** each of the five shows the posters of my three highest-scored anime from that season or year

#### Scenario: The overlay is illustrated too
- **WHEN** I open the overlay listing every qualifying season or year
- **THEN** each of its rows shows posters under the same rule as the inline rows

#### Scenario: Fewer than three posters available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters

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

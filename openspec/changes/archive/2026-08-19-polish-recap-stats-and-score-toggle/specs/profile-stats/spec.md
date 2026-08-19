## MODIFIED Requirements

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count and that score's share of all my rated anime.

Each bar's length SHALL be that score's count relative to the largest count across all score values, so the most-common score's bar fills the full width of its track and every other bar is drawn in proportion to it. A score with a count of zero SHALL render an empty track.

Each row SHALL show, after its count, that score's share of all my rated anime as a percentage — the count for that score divided by the total number of anime I have rated. A share that rounds to zero but comes from a non-zero count SHALL be shown as less than one percent rather than as zero.

The count and the share SHALL occupy two separate columns of fixed width, each aligned within itself, rather than being run together into one string: every row's count SHALL line up with every other row's count, and every row's share with every other row's share, whatever their number of digits. Neither column SHALL be sized to its content, since a wider value in one row would then steal width from that row's own bar track and distort which bar reads as longest.

When I have rated no anime at all, every track SHALL render empty and no share SHALL be shown as a nonsensical value.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value, that score's share as a percentage, and the overall mean score

#### Scenario: The most-common score fills its track
- **WHEN** one score has more anime than any other score
- **THEN** that score's bar fills the full width of its track

#### Scenario: Bars are proportional to the largest
- **WHEN** one score has half as many anime as the most-common score
- **THEN** its bar fills half the track width

#### Scenario: Share shown alongside the count
- **WHEN** a score accounts for 30% of all the anime I've rated
- **THEN** its row shows that score's count followed by 30%

#### Scenario: Counts line up down the block
- **WHEN** one score's count is 7 and another's is 143
- **THEN** the two counts are aligned with each other in a single column rather than sitting at different horizontal positions

#### Scenario: Percentages line up down the block
- **WHEN** one score's share is 4% and another's is 27%
- **THEN** the two percentages are aligned with each other in a single column, independently of the counts beside them

#### Scenario: Bar tracks are unaffected by a wide value
- **WHEN** one row's count and share are far wider than another's
- **THEN** both rows' bar tracks are the same width, so their bars remain directly comparable

#### Scenario: A very small share
- **WHEN** a score has at least one anime but its share rounds down to zero percent
- **THEN** its row shows less than one percent rather than zero percent

#### Scenario: Nothing rated yet
- **WHEN** I have rated no anime
- **THEN** every bar renders as an empty track and no share is shown as an invalid number

### Requirement: Favourite seasons and years
The profile page SHALL show a **Favourite seasons** ranking and a **Favourite years** ranking, covering my whole list rather than any selected period, so my best-liked seasons and years are reachable without opening a recap.

Each anime SHALL be attributed to the season and the year its air date falls in — the same air-date attribution the recap page's rankings use. An entry whose anime has no air date SHALL be attributed to neither and SHALL be excluded from both rankings. Only seasons and years holding at least one anime I scored SHALL be ranked; a season or year I scored nothing from SHALL be omitted rather than shown as empty or zero.

Both rankings SHALL rank on the same Bayesian weighted average the recap page's season and year rankings use, with the same trust thresholds — 5 for a season, 20 for a year — and the same global mean `C`, my mean score across every scored entry in my list. A season's or year's weighted score on the profile page SHALL therefore equal the score a recap covering that same season or year reports for it, so the two surfaces can never disagree.

Ties SHALL be resolved by the identical sequence the recap page's rankings apply, as set out in the `list-recaps` capability: the weighted score at full precision rather than at the two decimals displayed, so a higher score is never overruled; then, only for scores that are exactly equal, the number of scored anime; then a score-by-score comparison from 10 down to 1 where the group holding more anime at the highest differing score ranks first; and finally recency — the newer season or year ahead of the older. The order SHALL be stable across reloads, and a season's or year's rank relative to another SHALL be the same here as on a recap covering both.

Each ranked row SHALL show its rank, its name (season and year, or the year), how many of my anime it was computed over, and the weighted score it was ranked on. **Every** ranked row SHALL additionally show the posters of my three highest-scored anime from that season or year — fewer when fewer qualify — rather than only the top-ranked row, so each row is illustrated, both inline and in the overlay. Following a row SHALL open the recap for that season or year. Rows SHALL follow the shared ranking-row height the `list-recaps` capability defines, so a row with fewer posters than another still stands the same height.

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

#### Scenario: Equal scores resolved by coverage then by score
- **WHEN** two years' weighted scores are exactly equal, they were computed over the same number of scored anime, and one holds two 10s where the other holds one
- **THEN** the year with two 10s ranks first, exactly as it would on a recap covering both years

#### Scenario: A higher score is never overruled
- **WHEN** two years both display a weighted score of 7.80 but one's underlying score is higher past the second decimal
- **THEN** the higher-scoring year ranks first, here and on a recap covering both

#### Scenario: Wholly identical years fall back to the newer
- **WHEN** two years' weighted scores are exactly equal, they hold the same number of scored anime, and they hold the same number of my anime at every score from 10 down to 1
- **THEN** the newer year is ranked ahead of the older one

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
- **THEN** that row shows two posters, and the row stands at the same height as the rows showing three

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

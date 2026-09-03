## MODIFIED Requirements

### Requirement: Favourite seasons and years
The profile page SHALL show a **Favourite seasons** ranking and a **Favourite years** ranking, covering my whole list rather than any selected period, so my best-liked seasons and years are reachable without opening a recap.

Each anime SHALL be attributed to the season and the year its air date falls in — the same air-date attribution the recap page's rankings use. An entry whose anime has no air date SHALL be attributed to neither and SHALL be excluded from both rankings. Only seasons and years holding at least one anime I scored SHALL be ranked; a season or year I scored nothing from SHALL be omitted rather than shown as empty or zero.

Both rankings SHALL rank on the same Bayesian weighted average the recap page's season and year rankings use, with the same trust thresholds — 5 for a season, 20 for a year — and the same global mean `C`, my mean score across every scored entry in my list. A season's or year's weighted score on the profile page SHALL therefore equal the score a recap covering that same season or year reports for it, so the two surfaces can never disagree.

Ties SHALL be resolved by the identical sequence the recap page's rankings apply, as set out in the `list-recaps` capability: the weighted score at full precision rather than at the two decimals displayed, so a higher score is never overruled; then, only for scores that are exactly equal, the number of scored anime; then a score-by-score comparison from 10 down to 1 where the group holding more anime at the highest differing score ranks first; and finally recency — the newer season or year ahead of the older. The order SHALL be stable across reloads, and a season's or year's rank relative to another SHALL be the same here as on a recap covering both.

Each ranked row SHALL show its rank, its name (season and year, or the year), how many of my anime it was computed over, and the weighted score it was ranked on. **Every** ranked row SHALL additionally show the posters of three of my anime from that season or year — fewer when fewer qualify — rather than only the top-ranked row, so each row is illustrated, both inline and in the overlay. Which three, and in what order, SHALL follow the rule the `list-recaps` capability's "Top three posters for every ranked season and year" sets out: my ranking's order, best-ranked leftmost, so two anime I scored the same are separated by where I placed them rather than by their titles. Following a row SHALL open the recap for that season or year. Rows SHALL follow the shared ranking-row height the `list-recaps` capability defines, so a row with fewer posters than another still stands the same height.

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
- **THEN** each of the five shows the posters of three of my anime from that season or year

#### Scenario: My ranking picks and orders the three
- **WHEN** a ranked year holds five anime I scored 9 and none higher
- **THEN** the three I rank highest are shown, best-ranked leftmost, rather than the three whose titles come first alphabetically

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

### Requirement: Favourites rankings can be ranked by how many of a score they hold
Each of the profile page's two favourites rankings — **Favourite years** and **Favourite seasons** — SHALL offer a control that changes what the ranking ranks on, so a year or season can be found by how many anime of a given score it holds rather than only by its weighted average.

The control SHALL sit directly beneath its ranking's title and above its rows, and SHALL belong to that ranking alone: the two rankings SHALL hold independent selections, and changing one SHALL NOT change the other. It SHALL read as **All**, followed by the words **With most:** and one button per score, ordered **10 down to 1**.

A score SHALL be offered only when at least one group in that ranking holds at least one anime I scored it, so no offered button can produce an empty ranking. **All** SHALL always be offered and SHALL be the selection on a fresh visit; under it the ranking is exactly what it is today — the Bayesian weighted average, its tie-break sequence, its five-row cap, its "See all" overlay, and its posters, all unchanged.

Selecting a score N SHALL re-rank that ranking by **how many anime I scored N** each group holds, most first, and SHALL omit every group holding none. Groups tied on that count SHALL be ordered by the identical sequence the unfiltered ranking applies — the weighted score at full precision, then the number of scored anime, then a score-by-score comparison from 10 down to 1, then the newer group ahead of the older — so the order is stable across reloads and never arbitrary. Ranks SHALL be numbered from one down the re-ranked order rather than carrying over the positions the groups held under **All**.

A row's name, its height, and where following it leads SHALL be unchanged under a score selection. Its supporting figures SHALL name the count it was ranked on alongside how many of my anime the group was computed over, so the row states why it placed where it did. Its **posters** SHALL be drawn from the selected score alone — the three anime of that score the group holds that I rank highest, best-ranked leftmost, per the `list-recaps` capability's "Top three posters for every ranked season and year" — so a row's illustration agrees with the figure it was ranked on rather than showing the group's highest-scored anime regardless of the selection. Returning to **All** SHALL return every row's posters to the group's three best overall.

The five-row cap and the "See all" overlay SHALL apply to the re-ranked ranking: at most five rows are shown, the overlay lists every group the selection keeps, in the selected order, and the overlay SHALL name the selection it is showing. A selection that leaves five or fewer groups SHALL offer no overlay control, exactly as an unfiltered ranking of that size does not.

Each score button SHALL wear the colour its score carries in the recap page's rating distribution — the same score-tier colours, so a score is the same colour wherever it is shown — on pointer hover, on keyboard focus, and while it is the selection. At rest and unselected it SHALL be neutral. The **All** button SHALL wear its section's own colour family — the year family in Favourite years, the season family in Favourite seasons — rather than a score tier, since it names no score. No state SHALL change a button's size, so the control row SHALL NOT reflow as the pointer moves along it.

Every button in the control SHALL occupy **the same width**, whatever its label: a one-digit score, a two-digit score, and **All** SHALL be equally wide, with each label centred in its button, so the strip reads as an even row rather than stepping in and out with the digit count. The shared width SHALL be a floor rather than a fixed size, so a label that would not fit is never clipped.

A selection SHALL be part of the profile page's restorable state per the `page-state-restoration` capability: it SHALL be restored on a back/forward navigation and SHALL open on **All** on a fresh visit. A restored selection naming a score the reloaded ranking no longer offers SHALL fall back to **All** rather than showing an empty ranking.

The same control SHALL be offered by the recap page's own season and year rankings, per the `list-recaps` capability, with the presentation rules above applying identically there. Only the mechanism that carries the selection differs: the recap page carries its selections in the page URL alongside its period, where this page carries its own in restorable state.

#### Scenario: The control is offered under each title
- **WHEN** the profile page shows Favourite years and Favourite seasons
- **THEN** each ranking shows an All button and a "With most:" row of score buttons between its title and its rows

#### Scenario: All is the default and changes nothing
- **WHEN** I open the profile page without touching the control
- **THEN** All is selected and both rankings are ranked exactly as they are by weighted average

#### Scenario: Ranking by how many 10s
- **WHEN** I select 10 in Favourite years
- **THEN** the years are re-ranked with the year holding the most anime I scored 10 first, numbered from one

#### Scenario: Groups holding none of the score are dropped
- **WHEN** I select 9 and one of the years shown under All holds no anime I scored 9
- **THEN** that year is not shown in the ranking or in its "See all" overlay

#### Scenario: A tie on the count falls back to the ranking's own order
- **WHEN** two seasons each hold three anime I scored 8 and 8 is selected
- **THEN** they are ordered by weighted score, then scored count, then score-by-score, then the newer first — the same sequence All uses

#### Scenario: Only scores that exist are offered
- **WHEN** nothing in my list is scored 1
- **THEN** no 1 button is offered in either ranking

#### Scenario: The two rankings are independent
- **WHEN** I select 10 in Favourite years
- **THEN** Favourite seasons is still on All

#### Scenario: A row states what it was ranked on
- **WHEN** a year is shown under a selection of 10
- **THEN** its row states how many anime I scored 10 it holds, alongside how many of my anime it was computed over

#### Scenario: A row is illustrated by the selected score
- **WHEN** I select 8 in Favourite years and a shown year holds anime I scored 10, 9, and 8
- **THEN** that row's posters are the 8s I rank highest, not its 10s and 9s

#### Scenario: All restores the group's own best
- **WHEN** I return Favourite years to All
- **THEN** every row shows the three anime I rank highest from that year again, whatever their scores

#### Scenario: The overlay follows the selection
- **WHEN** I select 8 in Favourite seasons and open its "See all" overlay
- **THEN** the overlay lists every season holding an 8, in the selected order, names the selection it is showing, and illustrates each row with that season's 8s

#### Scenario: No overlay when the selection leaves few groups
- **WHEN** a selected score is held by only three years
- **THEN** all three are shown and no "See all" control is offered

#### Scenario: Buttons wear their score's colour
- **WHEN** I hover the 10 button, then the 9 button, then the 8 button
- **THEN** each highlights in the colour its score carries in the recap page's rating distribution

#### Scenario: The selected button keeps its colour
- **WHEN** 9 is selected and the pointer is elsewhere
- **THEN** the 9 button still reads as the selection, drawn in its score's colour

#### Scenario: All wears its section's family
- **WHEN** I hover the All button in Favourite seasons
- **THEN** it highlights in the season family's colour rather than in a score tier's

#### Scenario: One-digit and two-digit buttons match
- **WHEN** a ranking offers buttons for 10 down to 1
- **THEN** the 1 button is exactly as wide as the 10 button, and both are as wide as All

#### Scenario: Labels are centred
- **WHEN** I look along the row of score buttons
- **THEN** each numeral sits centred in its button rather than against one edge

#### Scenario: The control row does not reflow
- **WHEN** I move the pointer along a ranking's score buttons
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: A selection survives back-navigation
- **WHEN** I select 10 in Favourite years, follow a row into a recap, and go back
- **THEN** 10 is still selected and the ranking is still ranked by it

#### Scenario: A fresh visit opens on All
- **WHEN** I select 10 and then reach the profile page again from the navigation bar
- **THEN** both rankings are back on All

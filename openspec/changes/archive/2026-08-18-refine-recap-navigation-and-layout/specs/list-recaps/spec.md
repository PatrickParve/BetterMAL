## ADDED Requirements

### Requirement: Recap entry point in the navigation bar
The primary navigation SHALL offer a Recap destination alongside the app's other top-level pages, reachable from anywhere without first opening a picker. Following it SHALL open the recap page on the yearly mode for the current calendar year under the **What aired** time filter, so the default recap answers "what came out this year".

The nav entry SHALL be marked as the active destination whenever the recap page is open, whatever period that page is currently showing. When the default period would include no entries under **What aired**, the recap page's existing fallback rules SHALL apply rather than the nav entry choosing a different period.

#### Scenario: Opening a recap from the nav bar
- **WHEN** I follow the Recap entry in the navigation bar
- **THEN** the recap page opens on the current year under **What aired**, without an intervening picker

#### Scenario: Nav entry reflects the open page
- **WHEN** I am on the recap page showing fall 2019
- **THEN** the Recap nav entry is shown as the active destination

#### Scenario: Nothing aired this year yet
- **WHEN** the current year holds no entries under **What aired** but does hold entries I completed
- **THEN** the recap falls back to **What I watched** under the existing fallback rule rather than reporting the year empty

### Requirement: Scoping my list to a period from my list
My list SHALL offer a period control that narrows the list in place to the anime of a chosen period, rather than navigating away to the recap page. Choosing a period SHALL apply that period's included set — the same set the recap of that period would cover, under the same period mode, time filter, and media type — as a scope on the list, leaving every other list filter, sort, and grouping control untouched and still usable on top of it.

The control SHALL sit in the same row as the status filter tabs. The applied scope SHALL be shown as a dismissible indicator naming the period, the time filter, and the media type, and dismissing it SHALL return the list to its unscoped contents.

The scope indicator SHALL offer a link to the full recap of that same period, filter, and media type, so the recap page remains one step away from a scoped list.

#### Scenario: Choosing a period scopes the list
- **WHEN** I choose fall 2019 from my list's period control
- **THEN** my list narrows to the anime that recap includes, and I stay on my list

#### Scenario: The period control sits with the status tabs
- **WHEN** my list is shown
- **THEN** the period control is presented in the same row as the status filter tabs

#### Scenario: List controls still apply over a scope
- **WHEN** a period scope is applied and I then filter to Completed and sort by score
- **THEN** the list shows the completed anime of that period ordered by score

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** my list returns to its full contents with my other filters unchanged

#### Scenario: Opening the recap from a scoped list
- **WHEN** my list is scoped to fall 2019 narrowed to TV and I follow the scope indicator's recap link
- **THEN** the recap page opens on fall 2019 with the same media-type narrowing

### Requirement: Recap page section order
The recap page SHALL present its sections in a fixed order, leading with the period's top anime rather than with its statistics:

1. The top anime of the period, with the stat block beside it, positioned to the left of the top anime on a display wide enough for the two to sit side by side. On a narrow display the stat block SHALL fall above the top anime rather than being hidden or truncated.
2. The rankings — season, year, and most time watched.
3. The hot takes.

The period controls, mode tabs, and time filter SHALL remain above all of these, so the period can be changed without scrolling past the recap.

#### Scenario: Top anime leads the page
- **WHEN** a recap renders for a period holding entries
- **THEN** the top anime is the first content below the period controls, with the stat block to its left

#### Scenario: Narrow display stacks the two
- **WHEN** the recap is shown on a display too narrow for a side-by-side layout
- **THEN** the stat block is shown above the top anime, with both fully readable

#### Scenario: Hot takes come last
- **WHEN** a recap shows stats, a top anime list, rankings, and hot takes
- **THEN** the hot takes appear after every ranking

### Requirement: Most time watched ranking
A recap that qualifies for a season or year ranking SHALL additionally rank the same groups by how much time I spent watching their anime, largest first, so a period I consumed the most of is visible separately from the one I scored highest. The groups ranked SHALL match the score ranking's level: seasons on a yearly recap, and both seasons and years on a multi-year recap.

Time watched for a group SHALL be the total runtime of the episodes I watched across the included anime attributed to that group, computed on the same basis as the recap's **Time spent** stat. Only groups with more than zero time watched SHALL be ranked; a group whose anime I have watched no episodes of SHALL be omitted rather than shown as zero. Each ranked row SHALL show the group's name, the time watched, and how many episodes that covers. Ties SHALL be broken by episodes watched, then chronologically, so the order is stable across reloads.

Unlike the score rankings, this ranking SHALL NOT require a group's anime to be scored — time watched is a fact about what I watched, not about what I rated.

#### Scenario: Ranking a year's seasons by time
- **WHEN** a 2022 yearly recap is set to **What aired**
- **THEN** its seasons are ranked by total time watched, largest first, each row naming the time and the episode count

#### Scenario: Unscored anime still count toward time
- **WHEN** I watched a full season of anime in summer 2021 but scored none of it
- **THEN** summer 2021 appears in the time-watched ranking despite being absent from the score ranking

#### Scenario: Groups with nothing watched are omitted
- **WHEN** a season's included anime are all entries I have watched no episodes of
- **THEN** that season does not appear in the time-watched ranking

#### Scenario: Not shown where the score rankings are not
- **WHEN** a recap's time filter is **What I watched**, or a season recap is shown
- **THEN** no time-watched ranking is offered, matching the score rankings' own gate

## MODIFIED Requirements

### Requirement: Recap stat block
Every recap SHALL report the following stats, computed over the entries the selected period and time filter include, and recomputed whenever either changes:

- **In this period** — how many entries the period includes in total, under whatever time filter is selected. The label SHALL name the period rather than the medium, so this stat is not read as a count of a particular status.
- **Completed** — how many included entries have status Completed.
- **Dropped** — how many included entries have status Dropped, so the difference between the period's total and its completed count is accounted for rather than left unexplained.
- **Mean score** — the average of my scores across included entries I have scored, to two decimals. Unscored entries SHALL be left out of the average rather than counted as zero.
- **Episodes watched** — the sum of episodes watched across included entries whose media type is not `movie`.
- **Movies watched** — how many included entries whose media type is `movie` have at least one episode watched.
- **Time spent** — the total runtime of the episodes watched across all included entries, movies included.
- **Hot takes** — see the hot takes requirement.

Time spent SHALL use each anime's cached average episode duration where one is known, and SHALL otherwise fall back to the same assumed per-episode runtime the profile and series pages already use, so the three surfaces never disagree about the same anime.

When no included entry has a score, the mean score SHALL be reported as unavailable rather than as zero.

#### Scenario: Stats reflect the included set
- **WHEN** a recap renders for a period and time filter
- **THEN** every stat is computed only over the entries that selection includes

#### Scenario: Stats update with the filter
- **WHEN** I switch the time filter on a yearly recap
- **THEN** every stat recomputes for the newly included entries without reloading the page

#### Scenario: The period total is distinguishable from the completed count
- **WHEN** a period includes 42 entries of which 31 are completed and 4 dropped
- **THEN** the stat block reports 42 in this period, 31 completed, and 4 dropped, each labelled for what it counts

#### Scenario: Movies are excluded from the episode count
- **WHEN** the included set holds both a 12-episode TV series I finished and two films I watched
- **THEN** episodes watched reports 12 and movies watched reports 2

#### Scenario: Unscored entries do not drag the mean down
- **WHEN** the included set holds five entries of which two are unscored
- **THEN** the mean score is the average of the three scores I gave, not an average over five

#### Scenario: No scores in the period
- **WHEN** no included entry has a score
- **THEN** the mean score is shown as unavailable rather than as 0

#### Scenario: Runtime falls back for anime with no cached duration
- **WHEN** an included anime has no cached average episode duration
- **THEN** its contribution to time spent uses the app's standing per-episode assumption, matching what the profile and series pages report for the same anime

### Requirement: Hot takes
Every recap SHALL report up to five **hot takes** — the included entries whose score I gave diverges furthest from MAL's community score — ordered by the size of that divergence, largest first. Only entries that have both my score and a MAL score SHALL be eligible. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than five included entries are eligible, the recap SHALL show those that are; when none are, it SHALL say so rather than render an empty block.

#### Scenario: Five hot takes
- **WHEN** a recap's included set holds many entries scored by both me and MAL
- **THEN** the five most divergent are shown, largest divergence first, each naming both scores

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than five eligible entries
- **WHEN** only two included entries have both my score and a MAL score
- **THEN** two hot takes are shown

#### Scenario: No eligible entries
- **WHEN** no included entry has both scores
- **THEN** the recap states that there are no hot takes for this period

### Requirement: Season ranking
A multi-year or yearly recap under the **What aired** filter SHALL rank the anime seasons its period covers, best first, so the user can see which season they enjoyed most. A yearly recap SHALL rank the four seasons of that year; a multi-year recap SHALL rank every season its years cover. Only seasons holding at least one included anime that I scored SHALL be ranked; a season I watched nothing from SHALL be omitted rather than shown as empty or zero.

Each ranked season SHALL show its name and year, how many of its anime I scored, and the score it was ranked on. The ranking SHALL NOT be shown on a season recap (its period is a single season) nor under the **What I watched** filter, where the included set is not organised by air date.

At most five seasons SHALL be shown at once. When more than five qualify, the ranking SHALL offer a control that opens an overlay listing every qualifying season in rank order — the same treatment the year ranking already gives its own overflow.

#### Scenario: Ranking the seasons of a year
- **WHEN** a 2022 yearly recap is set to **What aired** and I scored anime from three of its four seasons
- **THEN** those three seasons are ranked best first and the fourth is omitted

#### Scenario: Ranking across many years
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired**
- **THEN** the five best-ranked seasons are shown and a control opens an overlay listing every qualifying season, each labelled with its season and year

#### Scenario: Five or fewer seasons
- **WHEN** only four seasons qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Not shown on a season recap
- **WHEN** a season recap is shown
- **THEN** no season ranking is offered

#### Scenario: Not shown under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no season ranking is offered

### Requirement: Year ranking
A multi-year recap under the **What aired** filter SHALL rank the years its period covers, best first, on the same basis as the season ranking. Only years holding at least one included anime that I scored SHALL be ranked.

At most five years SHALL be shown at once. When more than five qualify, the recap SHALL offer a control that opens an overlay listing every qualifying year in rank order. The ranking SHALL NOT be shown on a yearly or season recap, whose period is a single year or less.

When a recap shows both a season ranking and a year ranking, the two SHALL be presented alongside one another rather than stacked, so a period's best season and best year are comparable at a glance. On a display too narrow for that, the two SHALL stack with the season ranking first.

#### Scenario: Ranking a decade
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired** and I scored anime from eight of those years
- **THEN** the five best-ranked years are shown, and a control opens an overlay listing all eight

#### Scenario: Five or fewer years
- **WHEN** only four years qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Years with nothing watched are omitted
- **WHEN** I scored nothing that debuted in 2013 within the selected range
- **THEN** 2013 does not appear in the ranking or in the overlay

#### Scenario: Not shown on a yearly recap
- **WHEN** a yearly recap is shown
- **THEN** no year ranking is offered

#### Scenario: Both rankings sit side by side
- **WHEN** a multi-year recap shows both a season ranking and a year ranking on a wide display
- **THEN** the two are presented next to each other rather than one above the other

#### Scenario: Narrow display stacks the rankings
- **WHEN** that same recap is shown on a display too narrow for two columns
- **THEN** the season ranking is shown first and the year ranking below it

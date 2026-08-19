# list-recaps Specification

## Purpose
The list-recaps capability generates a retrospective recap of the anime in a user's list over a period they choose — a single season, a calendar year, or a span of years — reachable from the main navigation or by scoping my list in place without leaving it. Depending on the selected time filter, a recap covers either what the user actually watched or completed in that window, or what aired during it, and it is always attributed to a single period so nothing double-counts. Each recap reports summary stats, the biggest divergences between the user's scores and MAL's community scores, a top-10 ranked list narrowable by score basis and media type, and Bayesian-weighted rankings of the best seasons and years (plus how much time was spent watching each), so a user can see at a glance what stood out during that stretch of their watching history and jump from there into a scoped view of their full list.

## Requirements

### Requirement: Recap period modes
The system SHALL provide a recap of anime in my list over a chosen period, in exactly three period modes that the user switches between without leaving the recap:

- **Multi-year** — a start year and an end year, selected as whole years with no month or day. The period runs from 1 January of the start year through 31 December of the end year, inclusive of both.
- **Yearly** — one calendar year, 1 January through 31 December.
- **Season** — one year plus one of winter, spring, summer, or fall, using MAL's fixed quarter convention (winter = January–March, spring = April–June, summer = July–September, fall = October–December).

Switching mode SHALL keep the recap on screen and recompute it for the new period rather than returning the user to an empty state. The selected mode and period SHALL be carried in the page URL, so a recap can be linked to, reloaded, and restored on back-navigation with the same period it was showing.

#### Scenario: Selecting a multi-year period
- **WHEN** I choose the multi-year mode and set the period to 2011 through 2020
- **THEN** the recap covers 1 January 2011 through 31 December 2020 inclusive

#### Scenario: Selecting a single year
- **WHEN** I choose the yearly mode and select 2022
- **THEN** the recap covers 1 January 2022 through 31 December 2022

#### Scenario: Selecting a season
- **WHEN** I choose the season mode and select fall 2019
- **THEN** the recap covers 1 October 2019 through 31 December 2019

#### Scenario: A multi-year period of one year
- **WHEN** I set a multi-year period whose start year and end year are the same
- **THEN** the recap covers that single calendar year rather than reporting an invalid period

#### Scenario: Reversed multi-year bounds
- **WHEN** a multi-year period arrives with its end year before its start year
- **THEN** the two bounds are treated as an inclusive range in ascending order rather than producing an empty recap

#### Scenario: Switching mode keeps the recap open
- **WHEN** I switch from a yearly recap to a season recap
- **THEN** the recap re-renders for the newly selected period without navigating away

#### Scenario: Recap period survives a reload
- **WHEN** I reload the page, or return to a recap with the browser's back button
- **THEN** the same mode, period, and time filter are shown as when I left

### Requirement: Recap page title names its period
The recap page SHALL carry exactly one heading naming what is being recapped, composing the selected period and the word "recap" into a single title — a season recap titled by its season and year, a yearly recap by its year, and a multi-year recap by its inclusive year range. The page SHALL NOT additionally repeat the period as a second heading below the controls.

The title SHALL update whenever the mode or period changes, without the page navigating away, and SHALL be present whether or not the period holds any entries, so an empty period is still identified by name.

#### Scenario: A season recap's title
- **WHEN** a season recap for fall 2019 is shown
- **THEN** the page's single heading names fall 2019 as the period being recapped

#### Scenario: A yearly recap's title
- **WHEN** a yearly recap for 2022 is shown
- **THEN** the page's single heading names 2022 as the period being recapped

#### Scenario: A multi-year recap's title
- **WHEN** a multi-year recap covers 2011 through 2020
- **THEN** the page's single heading names that inclusive range as the period being recapped

#### Scenario: The period is named once, not twice
- **WHEN** any recap is shown
- **THEN** the period appears in the page's title and is not repeated as a separate heading above the recap's content

#### Scenario: The title follows the period
- **WHEN** I change the period or switch mode
- **THEN** the title re-renders for the newly selected period in place

#### Scenario: An empty period is still named
- **WHEN** a selected period includes no entries under either time filter
- **THEN** the title still names that period alongside the message that there is nothing to recap for it

### Requirement: Period stepper arrows
A season recap and a yearly recap SHALL each offer a previous and a next control beside their period selection, stepping the period by one unit of the current mode: one season for a season recap, crossing into the adjacent year at the boundaries of the calendar, and one year for a yearly recap. Stepping SHALL change the period exactly as selecting it from the period controls does, leaving the mode, the time filter, the ranking basis, and the media-type narrowing untouched, and SHALL be carried in the page URL like any other period change.

A multi-year recap SHALL NOT offer these controls, since a two-ended range has no single step.

A control that would step outside the range of periods the recap's own period controls offer SHALL be presented as unavailable and SHALL NOT act.

#### Scenario: Stepping a season
- **WHEN** a fall 2019 season recap is shown and I use the next control
- **THEN** the recap moves to winter 2020, keeping its ranking basis and media-type narrowing

#### Scenario: Stepping across a year boundary
- **WHEN** a winter 2020 season recap is shown and I use the previous control
- **THEN** the recap moves to fall 2019

#### Scenario: Stepping a year
- **WHEN** a 2022 yearly recap is shown and I use the previous control
- **THEN** the recap moves to 2021, keeping its time filter

#### Scenario: Multi-year has no stepper
- **WHEN** a multi-year recap is shown
- **THEN** no previous or next control is offered

#### Scenario: Stepping is addressable
- **WHEN** I step to another period and then reload the page
- **THEN** the recap is shown for the period I stepped to

#### Scenario: The end of the range
- **WHEN** the selected period is the earliest or latest the period controls offer
- **THEN** the control that would step past it is shown as unavailable and does nothing

### Requirement: Season recap links to its season page
A season recap SHALL offer a control that opens the season browser on that same season and year, so the anime that aired that season — not only the ones in my list — are one step away. The control SHALL NOT be offered on a yearly or multi-year recap, whose period is not a single season.

#### Scenario: Opening the season page from a recap
- **WHEN** a fall 2019 season recap is shown and I follow its season-page control
- **THEN** the season browser opens on fall 2019

#### Scenario: Not offered outside season mode
- **WHEN** a yearly or multi-year recap is shown
- **THEN** no season-page control is offered

### Requirement: Dynamic time filter
The multi-year and yearly recaps SHALL offer a time filter with two options that change which of my entries the period includes, with every stat, the top 10, and the rankings recomputed to match the selection:

- **What I watched** — entries whose status is Completed or Dropped and whose completion date falls inside the period.
- **What aired** — entries whose anime started airing inside the period, whatever the completion date, limited to entries whose status is Completed, Dropped, On hold, or Watching.

Neither option SHALL include Plan-to-watch entries. The season recap SHALL NOT offer the filter: it always selects on what aired in that season, under the same status rule as the **What aired** option above.

An option that would include no entries for the selected period SHALL be shown as unavailable and SHALL NOT be selectable; a single matching entry is enough to make it selectable. When the selected option becomes unavailable because the period changed, the recap SHALL fall back to the other option rather than showing an empty recap under an impossible selection.

#### Scenario: Watch-history selection
- **WHEN** the yearly recap for 2022 is set to **What I watched**
- **THEN** it includes exactly the entries I completed or dropped between 1 January and 31 December 2022, and no others

#### Scenario: Aired-in-period selection
- **WHEN** the yearly recap for 2022 is set to **What aired**
- **THEN** it includes exactly the completed, dropped, on-hold, and watching entries whose anime started airing in 2022, whenever I watched them

#### Scenario: Plan to watch is never counted
- **WHEN** either time filter is selected
- **THEN** no Plan-to-watch entry appears in the recap under either option

#### Scenario: An option with no entries is disabled
- **WHEN** I select a period in which I finished nothing but which several anime I watched debuted in
- **THEN** **What I watched** is shown as unavailable and cannot be selected, while **What aired** remains selectable

#### Scenario: One entry is enough
- **WHEN** a period holds exactly one entry under an option
- **THEN** that option is selectable and the recap renders in full for that single entry

#### Scenario: Falling back when the selection becomes unavailable
- **WHEN** I change the period while **What I watched** is selected, and the new period has no completed or dropped entries but does have anime that aired in it
- **THEN** the recap switches to **What aired** rather than reporting the period empty

#### Scenario: Season recap has no filter
- **WHEN** a season recap is shown
- **THEN** no time-filter control is offered, and the recap selects on what aired that season

### Requirement: Period attribution by start date
Under the **What aired** selection, an anime SHALL be attributed to the period containing the date it started airing, even when it continued airing into later periods. An anime SHALL NOT appear in more than one period of the same mode.

An anime with no known start date SHALL be excluded from any **What aired** selection, and SHALL be excluded from the season and year rankings, since it cannot be placed on the calendar. Such an entry SHALL still be included in a **What I watched** selection whose period contains its completion date.

#### Scenario: A show spanning two years
- **WHEN** an anime started airing on 30 December 2022 and continued into 2023, and the yearly recap for 2022 is set to **What aired**
- **THEN** it is included in the 2022 recap and does not appear in the 2023 recap

#### Scenario: A show starting at the end of a multi-year range
- **WHEN** an anime started airing on 30 December 2020 and a multi-year recap covers 2011 through 2020 under **What aired**
- **THEN** that anime is included in the recap

#### Scenario: Unknown start date under aired selection
- **WHEN** an included-status entry's anime has no recorded start date and the selection is **What aired**
- **THEN** it is left out of the recap and out of the season and year rankings

#### Scenario: Unknown start date under watch-history selection
- **WHEN** that same entry was completed inside the period and the selection is **What I watched**
- **THEN** it is included in the recap's stats and top 10

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

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT change the tile's size or position, so hovering across the block never reflows it.

#### Scenario: The stat block is labelled
- **WHEN** a recap renders its stats
- **THEN** the block carries a heading naming it, styled as the page's other section headings are

#### Scenario: A hovered tile is identifiable
- **WHEN** I move the pointer over one stat tile
- **THEN** that tile is visibly distinguished from the tiles around it

#### Scenario: Hover does not reflow the block
- **WHEN** I move the pointer across the stat tiles
- **THEN** no tile changes size or position as it gains or loses the highlight

### Requirement: Recap rows highlight on hover
Every row of the recap page that links to an anime, a season, or a year — the top-10 rows, the hot-take rows, and the rows of all four rankings, inline and in the "See all" overlay alike — SHALL adopt the app's standard accent hover treatment: the same tinted background, accent border, and elevation that the anime cards on my list, the season page, and the top-anime page already use, applied on both pointer hover and keyboard focus.

The treatment SHALL be the same one those pages use rather than a recap-specific variant, so a hovered row reads identically wherever it appears. It SHALL NOT change a row's size or position.

#### Scenario: A hovered top-10 row
- **WHEN** I move the pointer over a row of the recap's top 10
- **THEN** that row takes the same accent highlight an anime card takes on my list

#### Scenario: A hovered hot take
- **WHEN** I move the pointer over a hot-take row
- **THEN** that row takes the same accent highlight

#### Scenario: A hovered ranking row
- **WHEN** I move the pointer over a row of any of the recap's rankings, or a row of a "See all" overlay
- **THEN** that row takes the same accent highlight

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach one of these rows by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Highlighting does not reflow the list
- **WHEN** I move the pointer down a list of rows
- **THEN** no row changes size or position as it gains or loses the highlight

### Requirement: Hot takes
Every recap SHALL report up to five **hot takes** — the included entries where my score and MAL's community score disagree sharply enough, and in a definite enough direction, to count as a disagreement rather than as ordinary variation. An included entry SHALL be eligible only when it has both my score and a MAL score and it satisfies one of the two rules the profile page's opinion-divergence lists apply:

- **They liked it, I didn't** — the divergence runs at least one standard deviation in MAL's direction, my score is at or below the "average" boundary of the personal scale, and MAL's community score is at or above the community-like boundary.
- **I liked it, they didn't** — the divergence runs at least one standard deviation in my direction, my score is at or above the "very good" boundary of the personal scale, and MAL's community score is at or below that same community boundary.

The thresholds and boundaries SHALL be the ones the profile page already uses, drawn from a single shared definition rather than restated, so an anime that is a hot take in a recap is one the profile page also names, and one the profile page does not name never appears as a hot take.

Qualifying hot takes SHALL be ordered by the size of the divergence, largest first, with ties broken by title case-insensitively so the order is stable across reloads. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than five included entries qualify, the recap SHALL show those that do; when none qualify — including when my list holds too few scored pairs for the normalisation to be computed at all — it SHALL say so rather than present near-agreements as hot takes.

#### Scenario: Five hot takes
- **WHEN** a recap's included set holds more than five entries that satisfy either rule
- **THEN** the five with the largest divergence are shown, largest first, each naming both scores

#### Scenario: Ordered by the size of the disagreement
- **WHEN** several entries qualify by different margins
- **THEN** they are ordered by how far apart the two scores are, largest first

#### Scenario: A near-agreement is not a hot take
- **WHEN** an included entry has both scores but its divergence falls short of one standard deviation
- **THEN** it is not shown as a hot take, however few other entries qualify

#### Scenario: A high score MAL also liked is not a hot take
- **WHEN** I scored an anime 9 and MAL's community score for it is 8.4
- **THEN** it is not shown as a hot take, since neither rule's like/dislike boundaries are crossed

#### Scenario: Agreement with the profile page
- **WHEN** an anime appears in one of the profile page's opinion-divergence lists and falls inside a recap's included set
- **THEN** that anime is eligible to appear as a hot take in that recap

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than five qualify
- **WHEN** only two included entries satisfy either rule
- **THEN** two hot takes are shown

#### Scenario: Nothing qualifies
- **WHEN** no included entry satisfies either rule
- **THEN** the recap states that there are no hot takes for this period

#### Scenario: Too few scored pairs to normalise
- **WHEN** my whole list holds too few entries scored by both me and MAL for the shared normalisation to be computed
- **THEN** the recap states that there are no hot takes for this period rather than ranking on an uncomputable divergence

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis, with ties broken by title case-insensitively so the order is stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero. When the included set holds fewer than ten entries, the recap SHALL show all of them.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: Stable tie order
- **WHEN** several anime share the same score at the cut line
- **THEN** they are ordered by title, and the same order is shown on every reload

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

### Requirement: Top 10 ranking basis control
The recap SHALL let the top 10 be ranked by my score or by MAL's community score whenever the included set is an aired-in-period selection — that is, on a season recap, and on a multi-year or yearly recap whose time filter is **What aired**. Under the **What I watched** filter the control SHALL NOT be offered and the ranking SHALL be by my score.

MAL scores shown in the recap SHALL follow the app's existing MAL-score visibility rules, so a hidden score stays hidden here as it does elsewhere.

#### Scenario: Switching to MAL's ranking
- **WHEN** a season recap's top 10 is switched to MAL's score
- **THEN** the ten are re-ranked by MAL's community score

#### Scenario: Basis control hidden under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no ranking-basis control is offered and the top 10 is ranked by my score

#### Scenario: Basis control available on a season recap
- **WHEN** a season recap is shown
- **THEN** the ranking-basis control is offered, since a season recap always selects on what aired

#### Scenario: Hidden MAL scores stay hidden
- **WHEN** MAL scores are hidden by the global toggle and a recap row carries one
- **THEN** that score is hidden in the recap under the same rules as on every other page

### Requirement: Top 10 media-type control
The recap SHALL let the top 10 be narrowed to one media type — TV, movie, OVA, ONA, special, and so on — offering all types alongside only those types actually present in the included set, so a control is never offered for a type the period holds none of. Narrowing by media type SHALL re-rank the top 10 within that type. The stat block SHALL continue to describe the whole included set rather than the narrowed one.

#### Scenario: Narrowing to films
- **WHEN** I narrow a recap's top 10 to movies
- **THEN** only films from the included set are ranked, and the ten (or fewer) best of them are shown

#### Scenario: Absent types are not offered
- **WHEN** the included set holds no ONA entries
- **THEN** no ONA option is offered

#### Scenario: Stats are unaffected by the type control
- **WHEN** the top 10 is narrowed to movies
- **THEN** the stat block still reports mean score, counts, and time spent for the whole included set

### Requirement: Opening the period in my list
When the included set — after any media-type narrowing — holds more than ten anime, the recap SHALL offer a control that opens the my-list page already scoped to that same period, time filter, and media type, showing every one of those anime rather than the top ten. When the set holds ten or fewer, the control SHALL NOT be offered, since the recap is already showing all of them.

#### Scenario: More than ten in the period
- **WHEN** a recap's included set holds 40 anime
- **THEN** a control is offered that opens my list scoped to those 40

#### Scenario: Exactly ten in the period
- **WHEN** a recap's included set holds exactly ten anime
- **THEN** no such control is offered

#### Scenario: Narrowing below the threshold
- **WHEN** narrowing by media type brings the set to eight anime
- **THEN** the control is no longer offered

#### Scenario: The scope carries across
- **WHEN** I open my list from a recap of fall 2019 narrowed to TV
- **THEN** my list opens showing exactly the TV anime that recap included

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

The year ranking's placement relative to the season ranking is governed by the "Rankings grouped into a season column and a year column" requirement.

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

### Requirement: Rankings grouped into a season column and a year column
Where a multi-year recap shows more than one ranking, the rankings SHALL be laid out in two columns grouped by the level they rank: the season ranking and the seasons-by-time-watched ranking in one column, the year ranking and the years-by-time-watched ranking in the other, with the season column leading. A period's season story therefore reads down one column and its year story down the other, rather than the score rankings occupying one row and the time rankings another.

Within a column, the score ranking SHALL come before the time-watched ranking. A column whose rankings are both absent SHALL NOT be rendered as an empty column; where only one column has any ranking to show, that column SHALL occupy the layout on its own.

A yearly recap has no year-level rankings, so this two-column grouping does not apply to it. Where a yearly recap shows both the season ranking and the seasons-by-time-watched ranking, the two SHALL instead be laid out side by side in separate columns — the season ranking leading — rather than stacked one above the other, so a period's best season and most-watched season are directly comparable. Where only one of the two has rows, it SHALL occupy the layout on its own.

On a display too narrow for two columns, the rankings SHALL stack in a single column: for a multi-year recap, season rankings before year rankings; for a yearly recap, the season ranking before seasons-by-time-watched.

#### Scenario: A multi-year recap's four rankings
- **WHEN** a multi-year recap under **What aired** shows a season ranking, a year ranking, and both time-watched rankings
- **THEN** the season ranking and seasons-by-time-watched sit in the leading column, and the year ranking and years-by-time-watched in the other

#### Scenario: Order within a column
- **WHEN** a column shows both a score ranking and a time-watched ranking
- **THEN** the score ranking is shown above the time-watched one

#### Scenario: A yearly recap has no year column
- **WHEN** a yearly recap under **What aired** shows only the season-level rankings
- **THEN** no empty year column is rendered beside them

#### Scenario: A yearly recap's two rankings sit side by side
- **WHEN** a yearly recap under **What aired** shows both a season ranking and a seasons-by-time-watched ranking
- **THEN** the two are presented in separate columns next to each other, the season ranking leading, rather than one stacked above the other

#### Scenario: Narrow display stacks the columns
- **WHEN** a recap showing both columns is displayed too narrow for two columns
- **THEN** the rankings stack in one column with the season rankings before the year rankings

#### Scenario: Narrow display stacks a yearly recap's two rankings
- **WHEN** a yearly recap showing both the season ranking and seasons-by-time-watched is displayed too narrow for two columns
- **THEN** the two stack in one column with the season ranking first

### Requirement: Bayesian ranking of seasons and years
The season and year rankings SHALL rank on a Bayesian weighted average rather than a raw mean, so a season carrying one 10 does not outrank a season of many strong scores. The weighted score SHALL be computed as:

`W = (v / (v + m)) * R + (m / (v + m)) * C`

where `R` is the mean of my scores for the anime attributed to that season or year, `v` is how many of them I scored, `C` is my mean score across every scored entry in my whole list (not merely the recap period), and `m` is the count at which a group is treated as fully trusted — **5** for a season and **20** for a year.

Because `C` is drawn from the whole list, the same season SHALL rank identically whether it is reached through a yearly recap or a multi-year one covering the same year. Ties on `W` SHALL be broken by the number of scored anime, then chronologically, so the order is stable.

#### Scenario: A thin season is pulled toward the global mean
- **WHEN** one season holds a single anime I scored 10 and another holds eight anime averaging 8.5, and my global mean is 7.4
- **THEN** the eight-anime season ranks above the single-anime one

#### Scenario: A well-covered season keeps its average
- **WHEN** a season holds well over five scored anime
- **THEN** its weighted score sits close to its raw mean

#### Scenario: Consistent across recap modes
- **WHEN** I compare fall 2019's rank in a 2019 yearly recap against its rank in a 2011–2020 multi-year recap
- **THEN** the same weighted score is reported for that season in both

#### Scenario: Different trust thresholds
- **WHEN** a season and a year each hold ten scored anime
- **THEN** the season is treated as past its trust threshold while the year is not, and the year is pulled further toward the global mean

### Requirement: Top three posters for every ranked season and year
Every row of the season ranking and of the year ranking SHALL display the posters of my three highest-scored anime from that season or year, so each ranked row is illustrated rather than only the leader. Where a group holds fewer than three scored anime, those available SHALL be shown. This SHALL hold for the rows shown inline and for every row of the "See all" overlay alike.

The same rule SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, since both surfaces rank the same groups on the same basis.

The two time-watched rankings are not covered by this requirement. The seasons-by-time-watched ranking shows no posters on any row, including the leading one. The years-by-time-watched ranking continues to illustrate its leading row alone, with posters picked by episodes watched.

#### Scenario: Every ranked season is illustrated
- **WHEN** a season ranking shows five seasons, each holding at least three scored anime
- **THEN** all five rows show three posters, not only the first

#### Scenario: Every ranked year is illustrated
- **WHEN** a year ranking shows five years
- **THEN** all five rows show the posters of my three highest-scored anime from that year

#### Scenario: Fewer than three available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters

#### Scenario: The overlay matches the inline list
- **WHEN** I open the "See all" overlay of a season or year ranking
- **THEN** every row in it shows posters under the same rule as the inline rows

#### Scenario: Seasons by time watched shows no posters
- **WHEN** the seasons-by-time-watched ranking is shown
- **THEN** no row, including the leading one, shows posters

#### Scenario: Years by time watched keeps leader-only posters
- **WHEN** the years-by-time-watched ranking is shown
- **THEN** only its leading row shows posters

### Requirement: Empty periods are stated plainly
When a selected period includes no entries under either time filter, the recap SHALL say so plainly and offer the period controls so another period can be chosen, rather than rendering empty stats, a zero mean, or a blank top 10. When a period includes at least one entry, the recap SHALL render in full — stats, top 10, and any applicable rankings — however small the set.

#### Scenario: A period with nothing in it
- **WHEN** I select a period in which I watched nothing and nothing I have watched aired
- **THEN** the recap states that there is nothing to recap for that period, and the period controls stay available

#### Scenario: A period with one entry
- **WHEN** a period includes exactly one entry
- **THEN** the recap renders its stats, a top 10 holding that one anime, and any applicable rankings

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

1. The top anime of the period, with the stat block beside it, positioned to the **right** of the top anime on a display wide enough for the two to sit side by side. On a narrow display the two SHALL stack, with the top anime first and the stat block below it rather than hidden or truncated.
2. The rankings — season, year, and most time watched — laid out per the "Rankings grouped into a season column and a year column" requirement.
3. The hot takes.

The period controls, mode tabs, time filter, and period stepper SHALL remain above all of these, so the period can be changed without scrolling past the recap.

#### Scenario: Top anime leads the page
- **WHEN** a recap renders for a period holding entries
- **THEN** the top anime is the first content below the period controls, with the stat block to its right

#### Scenario: Narrow display stacks the two
- **WHEN** the recap is shown on a display too narrow for a side-by-side layout
- **THEN** the top anime is shown first with the stat block below it, both fully readable

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

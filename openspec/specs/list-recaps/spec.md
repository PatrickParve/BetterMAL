# list-recaps Specification

## Purpose
TBD - created by change add-list-recaps. Update Purpose after archive.

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

- **Mean score** — the average of my scores across included entries I have scored, to two decimals. Unscored entries SHALL be left out of the average rather than counted as zero.
- **Anime counted** — how many entries the period includes in total.
- **Completed** — how many included entries have status Completed.
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
Every recap SHALL report up to three **hot takes** — the included entries whose score I gave diverges furthest from MAL's community score — ordered by the size of that divergence, largest first. Only entries that have both my score and a MAL score SHALL be eligible. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than three included entries are eligible, the recap SHALL show those that are; when none are, it SHALL say so rather than render an empty block.

#### Scenario: Three hot takes
- **WHEN** a recap's included set holds many entries scored by both me and MAL
- **THEN** the three most divergent are shown, largest divergence first, each naming both scores

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than three eligible entries
- **WHEN** only two included entries have both my score and a MAL score
- **THEN** two hot takes are shown

#### Scenario: No eligible entries
- **WHEN** no included entry has both scores
- **THEN** the recap states that there are no hot takes for this period

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

#### Scenario: Ranking the seasons of a year
- **WHEN** a 2022 yearly recap is set to **What aired** and I scored anime from three of its four seasons
- **THEN** those three seasons are ranked best first and the fourth is omitted

#### Scenario: Ranking across many years
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired**
- **THEN** every season in that range holding at least one scored anime is ranked, each labelled with its season and year

#### Scenario: Not shown on a season recap
- **WHEN** a season recap is shown
- **THEN** no season ranking is offered

#### Scenario: Not shown under watch history
- **WHEN** a yearly recap's time filter is **What I watched**
- **THEN** no season ranking is offered

### Requirement: Year ranking
A multi-year recap under the **What aired** filter SHALL rank the years its period covers, best first, on the same basis as the season ranking. Only years holding at least one included anime that I scored SHALL be ranked.

At most five years SHALL be shown at once. When more than five qualify, the recap SHALL offer a control that opens an overlay listing every qualifying year in rank order. The ranking SHALL NOT be shown on a yearly or season recap, whose period is a single year or less.

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

### Requirement: Top three posters for the leading season and year
The top-ranked season, and the top-ranked year where a year ranking is shown, SHALL each display the posters of my three highest-scored anime from that season or year, so the winner is illustrated rather than only named. Where fewer than three qualify, those available SHALL be shown. Ranks below first SHALL NOT show posters.

#### Scenario: Winning season shows three posters
- **WHEN** a season ranking is shown and its top season holds at least three scored anime
- **THEN** that season's row shows the posters of my three highest-scored anime from it

#### Scenario: Winning year shows three posters
- **WHEN** a year ranking is shown
- **THEN** its top year's row shows the posters of my three highest-scored anime from it

#### Scenario: Fewer than three available
- **WHEN** the top season holds only two scored anime
- **THEN** two posters are shown

#### Scenario: Only the leader is illustrated
- **WHEN** a ranking shows five entries
- **THEN** only the first shows posters

### Requirement: Empty periods are stated plainly
When a selected period includes no entries under either time filter, the recap SHALL say so plainly and offer the period controls so another period can be chosen, rather than rendering empty stats, a zero mean, or a blank top 10. When a period includes at least one entry, the recap SHALL render in full — stats, top 10, and any applicable rankings — however small the set.

#### Scenario: A period with nothing in it
- **WHEN** I select a period in which I watched nothing and nothing I have watched aired
- **THEN** the recap states that there is nothing to recap for that period, and the period controls stay available

#### Scenario: A period with one entry
- **WHEN** a period includes exactly one entry
- **THEN** the recap renders its stats, a top 10 holding that one anime, and any applicable rankings

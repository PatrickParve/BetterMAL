## MODIFIED Requirements

### Requirement: Dynamic time filter
The multi-year and yearly recaps SHALL offer a time filter with two options that change which of my entries the period includes, with every stat, the top 10, and the rankings recomputed to match the selection:

- **What I watched** — entries I watched anything of inside the period. An entry SHALL qualify when **either** of the following holds:
  - its status is Completed or Dropped and its **completion date** falls inside the period; **or**
  - the **activity log** records episode progress for it inside the period, whatever its status and whatever its recorded dates.
- **What aired** — entries whose anime started airing inside the period, whatever the completion date, limited to entries whose status is Completed, Dropped, On hold, or Watching.

A start date SHALL NOT be required for either option. An entry with no recorded start date SHALL qualify for **What I watched** exactly as one with a start date does, on its completion date or its logged progress.

The logged-progress arm is what lets **What I watched** mean what its name says. Without it, an anime I am partway through has no completion date and so falls out of every period, and a rewatch is invisible because the entry's completion date still points at the original viewing. With it, an anime I watched episodes of in a period belongs to that period whether or not I finished anything there.

An anime SHALL be counted **once** per period however many of its viewings the period holds. An entry that qualifies on both its completion date and its logged progress, or whose logged progress in the period spans several viewings, SHALL appear once in **In this period**, once in the mean score, once in the top 10, and once in any ranking. Only the stats that measure viewing volume — Episodes watched and Time spent — SHALL count every viewing; see the recap stat block.

Because logged progress and completion dates place an anime independently, an anime MAY appear in more than one **What I watched** period: one for the period it was first watched in, and one for each later period holding a logged rewatch. This is the intended reading of a watch-history filter and does not conflict with the single-period attribution **What aired** applies.

The activity log holds history only from the point this app began tracking. For periods before that, an entry qualifies on its completion date alone, and viewing the app never recorded SHALL NOT be estimated from start dates, air dates, or stored rewatch counts.

Neither option SHALL include Plan-to-watch entries. The season recap SHALL NOT offer the filter: it always selects on what aired in that season, under the same status rule as the **What aired** option above.

An option that would include no entries for the selected period SHALL be shown as unavailable and SHALL NOT be selectable; a single matching entry is enough to make it selectable. Availability SHALL be judged under the same two-armed rule the selection itself applies, so an option holding only logged-progress entries is offered rather than reported empty. When the selected option becomes unavailable because the period changed, the recap SHALL fall back to the other option rather than showing an empty recap under an impossible selection.

#### Scenario: Watch-history selection
- **WHEN** the yearly recap for 2022 is set to **What I watched**
- **THEN** it includes the entries I completed or dropped between 1 January and 31 December 2022, together with every entry the activity log records episode progress for in that window

#### Scenario: A missing start date changes nothing
- **WHEN** an entry I completed inside the period has no recorded start date
- **THEN** it is included exactly as one with a start date is

#### Scenario: A show I am partway through
- **WHEN** the activity log records me watching episodes of a series I have not finished during 2026, and the 2026 recap is set to **What I watched**
- **THEN** that series is included in the recap

#### Scenario: A rewatch places an anime in a later period
- **WHEN** I completed a series in 2022 and the activity log records me rewatching it in 2024
- **THEN** it appears in the 2022 recap and again in the 2024 recap under **What I watched**

#### Scenario: An anime spanning the new year lands in both periods
- **WHEN** the activity log records me watching a series' episodes in December 2023 and further episodes in January 2024
- **THEN** it appears in both the 2023 and the 2024 recap, each counting the episodes watched in that year

#### Scenario: Counted once per period
- **WHEN** a period holds both an anime's completion date and logged progress for it
- **THEN** In this period counts it once, the mean score averages its score once, and it occupies one place in the top 10

#### Scenario: A multi-year range holding two viewings
- **WHEN** a 2020–2024 recap covers both my first watch of a series and a later rewatch of it
- **THEN** the series occupies one place in the recap's count, mean score, top 10 and rankings

#### Scenario: Nothing is estimated before tracking began
- **WHEN** a 2015 recap is set to **What I watched** and an entry has no completion date in 2015 and no logged progress there
- **THEN** it is left out, and no viewing is inferred from its start date, its air date, or its rewatch count

#### Scenario: Aired-in-period selection
- **WHEN** the yearly recap for 2022 is set to **What aired**
- **THEN** it includes exactly the completed, dropped, on-hold, and watching entries whose anime started airing in 2022, whenever I watched them

#### Scenario: Plan to watch is never counted
- **WHEN** either time filter is selected
- **THEN** no Plan-to-watch entry appears in the recap under either option

#### Scenario: An option with no entries is disabled
- **WHEN** I select a period in which I finished nothing, logged no episodes, but which several anime I watched debuted in
- **THEN** **What I watched** is shown as unavailable and cannot be selected, while **What aired** remains selectable

#### Scenario: Logged progress alone makes the option available
- **WHEN** a period holds no completion dates but the activity log records episode progress inside it
- **THEN** **What I watched** is selectable and the recap renders for those entries

#### Scenario: One entry is enough
- **WHEN** a period holds exactly one entry under an option
- **THEN** that option is selectable and the recap renders in full for that single entry

#### Scenario: Falling back when the selection becomes unavailable
- **WHEN** I change the period while **What I watched** is selected, and the new period has no completed or dropped entries, no logged progress, but does have anime that aired in it
- **THEN** the recap switches to **What aired** rather than reporting the period empty

#### Scenario: Season recap has no filter
- **WHEN** a season recap is shown
- **THEN** no time-filter control is offered, and the recap selects on what aired that season

### Requirement: Period attribution by start date
Under the **What aired** selection, an anime SHALL be attributed to the period containing the date it started airing, even when it continued airing into later periods. Under that selection an anime SHALL NOT appear in more than one period of the same mode.

Single-period attribution is a rule of the **What aired** selection alone. Under **What I watched** an anime MAY appear in several periods of the same mode, because it may genuinely have been watched in several — see the dynamic time filter requirement.

An anime with no known start date SHALL be excluded from any **What aired** selection, and SHALL be excluded from the season and year rankings, since it cannot be placed on the calendar. Such an entry SHALL still be included in a **What I watched** selection whose period contains its completion date or its logged episode progress.

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

#### Scenario: Multi-period appearance is confined to the watch-history filter
- **WHEN** an anime has logged viewings in two different years
- **THEN** it appears in both years' **What I watched** recaps, and still in only one year's **What aired** recap

### Requirement: Recap stat block
Every recap SHALL report the following stats, computed over the entries the selected period and time filter include, and recomputed whenever either changes:

- **In this period** — how many entries the period includes in total, under whatever time filter is selected. The label SHALL name the period rather than the medium, so this stat is not read as a count of a particular status.
- **Completed** — how many included entries have status Completed.
- **Dropped** — how many included entries have status Dropped, so the difference between the period's total and its completed count is accounted for rather than left unexplained.
- **Currently watching** — how many anime from the recap's period I am currently watching: from that season, that year, or that range of years, according to the recap shown. See the air-date rule below.
- **Mean score** — the average of my scores across included entries I have scored, to two decimals. Unscored entries SHALL be left out of the average rather than counted as zero.
- **Episodes watched** — the episodes I watched in the period across included entries whose media type is not `movie`. See the per-period episode rule below.
- **Movies watched** — how many included entries whose media type is `movie` have at least one episode watched.
- **Time spent** — the total runtime of the episodes counted by **Episodes watched**, with movies included.
- **Hot takes** — see the hot takes requirement.

**Currently watching** SHALL count entries with status Watching whose anime's air-start date falls inside the recap's period, in every mode and under **both** time filters — never on the completion-date attribution the **What I watched** filter uses. An entry still in progress has no completion date, so a filter-scoped count would report zero under **What I watched** for a period I am demonstrably still watching anime from. This stat therefore answers where the anime came from rather than when I finished something, and under **What I watched** it is the one tile scoped to the period's airing rather than to my watch history; the completed, dropped, and currently-watching counts consequently need not sum to **In this period**. An entry whose anime has no known air date SHALL be counted for no period.

The three status counts — Completed, Dropped, and Currently watching — SHALL be presented together, so what I finished, abandoned, and am still watching read as one group.

**The per-period episode count.** Under **What I watched**, an included entry's episodes SHALL be the episode progress the activity log records for it **inside the period**: the sum of its logged increases there. A recorded decrease SHALL contribute nothing and SHALL NOT reduce the figure.

Because a rewatch resets an entry's episode count to zero and counts back up, a period holding several viewings of one anime yields the sum of them all from this rule alone. An anime watched twice inside a period therefore contributes both viewings to Episodes watched and Time spent while still occupying a single place in the period's count, mean score, top 10 and rankings. No separate rewatch multiplier SHALL be applied to a logged figure — doing so would count the rewatch the log already holds a second time.

An entry included on its **completion date** for which the activity log holds no progress inside the period — a viewing that predates tracking — SHALL instead contribute its stored episodes watched, plus one further complete run of the anime for every recorded rewatch, measured by the anime's published total episode count or by its own episodes watched when no total is published. This is the only place a stored rewatch count is read, and it applies only where there is no log to read instead: the rewatch carries no date of its own, so the period the anime was finished in is the only period that can hold it.

Under **What aired**, and on every season recap, an entry SHALL contribute its stored episodes watched with no rewatch multiplier and no log lookup. A rewatch is not something that aired in the period, and counting it would inflate a figure about the period's broadcast.

**Movies watched** SHALL remain a count of films rather than of viewings: a film watched twice inside a period counts once. It is a followable stat, and the count it shows must equal the number of anime my list holds when I follow it. The runtime of the extra viewings still reaches **Time spent**, so no watched time is lost by counting the film once.

Time spent SHALL use each anime's cached average episode duration where one is known, and SHALL otherwise fall back to the same assumed per-episode runtime the profile and series pages already use, so the three surfaces never disagree about the same anime. Time spent SHALL be computed over the same episode counts Episodes watched reports, so dividing one by the other always yields a plausible per-episode runtime.

When no included entry has a score, the mean score SHALL be reported as unavailable rather than as zero.

#### Scenario: Stats reflect the included set
- **WHEN** a recap renders for a period and time filter
- **THEN** every stat is computed only over the entries that selection includes, save for Currently watching, which is computed under its own air-date rule

#### Scenario: Stats update with the filter
- **WHEN** I switch the time filter on a yearly recap
- **THEN** every stat recomputes for the newly included entries without reloading the page

#### Scenario: The period total is distinguishable from the completed count
- **WHEN** a period includes 42 entries of which 31 are completed and 4 dropped
- **THEN** the stat block reports 42 in this period, 31 completed, and 4 dropped, each labelled for what it counts

#### Scenario: Counting what I am still watching from a season
- **WHEN** a Fall 2024 season recap is shown and I am currently watching three anime that started airing in Fall 2024
- **THEN** the stat block reports 3 currently watching

#### Scenario: Counting what I am still watching from a year
- **WHEN** a 2024 yearly recap is shown and I am currently watching five anime that started airing anywhere in 2024
- **THEN** the stat block reports 5 currently watching

#### Scenario: Counting across a multi-year range
- **WHEN** a 2020–2024 recap is shown
- **THEN** currently watching counts every anime I am watching whose air-start date falls anywhere in those five years

#### Scenario: Currently watching survives the watch-history filter
- **WHEN** a 2024 yearly recap is set to **What I watched** and I am currently watching two anime that started airing in 2024
- **THEN** the stat block still reports 2 currently watching, rather than 0

#### Scenario: An entry with no air date counts nowhere
- **WHEN** I am currently watching an anime whose air date is unknown
- **THEN** it is counted toward no period's currently-watching stat

#### Scenario: Only the period's own episodes count
- **WHEN** a series was at episode 20 when 2024 began and the log records it reaching episode 29 during 2024, under **What I watched**
- **THEN** it contributes 9 episodes to the 2024 recap, not 29

#### Scenario: An in-progress show contributes what I watched of it
- **WHEN** the log records me going from episode 0 to episode 9 of a series I am still watching during the period, under **What I watched**
- **THEN** Episodes watched counts those 9 episodes and Time spent counts their runtime

#### Scenario: A dropped show with no completion date still counts what I watched
- **WHEN** the log records episode progress during the period on a series I later dropped without recording a finish date
- **THEN** that progress is counted in Episodes watched and Time spent

#### Scenario: Two viewings in one period count twice for volume and once for rank
- **WHEN** the log records me watching a 12-episode series through and then rewatching it, both inside the period, under **What I watched**
- **THEN** Episodes watched counts 24 for it, Time spent counts the runtime of 24 episodes, and it occupies one place in In this period, the mean score, and the top 10

#### Scenario: An episode-count correction does not subtract
- **WHEN** the log records me lowering an entry's episode count inside the period
- **THEN** that decrease contributes nothing and does not reduce the period's Episodes watched

#### Scenario: A pre-tracking viewing falls back to stored counts
- **WHEN** an entry is included on a completion date in a period the activity log does not reach, and it has a published total of 12 and a rewatch count of 1
- **THEN** it contributes 24 episodes to that period

#### Scenario: The stored rewatch count is not read twice
- **WHEN** an entry has both logged progress inside the period and a stored rewatch count
- **THEN** its contribution is the logged progress alone, with no rewatch multiplier applied on top

#### Scenario: Rewatches do not count under the aired filter
- **WHEN** a 2024 recap is set to **What aired** and holds a 12-episode series I have rewatched once
- **THEN** Episodes watched counts 12 for it

#### Scenario: A film watched twice is still one film
- **WHEN** the included set holds a film I watched twice inside the period
- **THEN** Movies watched counts it once, and Time spent counts both viewings

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

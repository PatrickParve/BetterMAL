## ADDED Requirements

### Requirement: Recap rating distribution
Every recap SHALL show, below its stat block, how many of the period's anime I gave each score from 10 down to 1, rendered as the same bar-per-score block the profile page's all-anime score distribution uses: a row per score value carrying that score's bar, its count, and its share of the period's scored anime.

The block SHALL be computed over the entries the selected period and time filter include — the same set every other stat is computed over — and SHALL recompute whenever either changes. It SHALL NOT be narrowed by the top 10's media-type control, which is scoped to the top 10 alone.

Each bar's length SHALL be that score's count relative to the largest count across the period's score values, so the period's most-common score fills the full width of its track. A score no included anime received SHALL render an empty track. Shares SHALL be computed against the period's scored anime rather than my whole list, and a share that rounds to zero from a non-zero count SHALL be shown as less than one percent.

The recap's block SHALL NOT repeat the mean score, which the stat block directly above it already reports for the same set. It MAY be rendered at a smaller size than the profile page's to suit the narrower column it occupies, provided it uses the same row structure and column alignment.

#### Scenario: Distribution of a period's scores
- **WHEN** a recap renders for a period whose included anime I have scored
- **THEN** it shows a bar, a count, and a share for each score from 10 down to 1, computed over that period's included entries

#### Scenario: The period's most-common score fills its track
- **WHEN** one score has more of the period's anime than any other
- **THEN** that score's bar fills the full width of its track and the others are drawn in proportion to it

#### Scenario: Shares are of the period, not the whole list
- **WHEN** a period holds 20 scored anime of which 5 scored 8
- **THEN** the 8 row reports a 25% share, whatever proportion 8s make up across my whole list

#### Scenario: The distribution follows the time filter
- **WHEN** I switch a yearly recap's time filter
- **THEN** the distribution recomputes for the newly included entries alongside the rest of the stats

#### Scenario: The media-type control does not narrow it
- **WHEN** I narrow the top 10 to films only
- **THEN** the distribution continues to cover every included anime of the period

#### Scenario: No mean line on the recap
- **WHEN** a recap renders its distribution
- **THEN** the block carries no mean-score line of its own, the stat block above it having already reported the period's mean

#### Scenario: Nothing scored in the period
- **WHEN** no included entry of the period has a score
- **THEN** every track renders empty and no share is shown as an invalid number

### Requirement: Ranking rows share a single height
Every row of every ranking the recap page renders — the season ranking, the year ranking, and both time-watched rankings, inline and in the "See all" overlay alike — SHALL occupy the same minimum height, whether or not that row carries posters. A ranking that shows no posters SHALL therefore stand the same height as one that does, so two rankings placed side by side read as a matched pair rather than as one full list beside one compressed list.

The shared height SHALL be the height a poster-carrying row already resolves to, so applying this rule changes no illustrated row's size. The same rule SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included.

#### Scenario: A yearly recap's two season rankings match
- **WHEN** a yearly recap shows the season ranking beside seasons-by-time-watched, the first illustrated with posters and the second not
- **THEN** the rows of the two rankings are the same height

#### Scenario: Illustrated rows are unchanged
- **WHEN** a ranking whose rows carry three posters is rendered
- **THEN** its rows stand at the same height they did before this rule applied

#### Scenario: A partly illustrated ranking is even
- **WHEN** the years-by-time-watched ranking shows posters on its leading row only
- **THEN** every row of that ranking, illustrated or not, is the same height

#### Scenario: The overlay matches the inline list
- **WHEN** I open a ranking's "See all" overlay
- **THEN** its rows follow the same shared height as the inline rows

## MODIFIED Requirements

### Requirement: Recap stat block
Every recap SHALL report the following stats, computed over the entries the selected period and time filter include, and recomputed whenever either changes:

- **In this period** — how many entries the period includes in total, under whatever time filter is selected. The label SHALL name the period rather than the medium, so this stat is not read as a count of a particular status.
- **Completed** — how many included entries have status Completed.
- **Dropped** — how many included entries have status Dropped, so the difference between the period's total and its completed count is accounted for rather than left unexplained.
- **Currently watching** — how many anime from the recap's period I am currently watching: from that season, that year, or that range of years, according to the recap shown. See the air-date rule below.
- **Mean score** — the average of my scores across included entries I have scored, to two decimals. Unscored entries SHALL be left out of the average rather than counted as zero.
- **Episodes watched** — the sum of episodes watched across included entries whose media type is not `movie`.
- **Movies watched** — how many included entries whose media type is `movie` have at least one episode watched.
- **Time spent** — the total runtime of the episodes watched across all included entries, movies included.
- **Hot takes** — see the hot takes requirement.

**Currently watching** SHALL count entries with status Watching whose anime's air-start date falls inside the recap's period, in every mode and under **both** time filters — never on the completion-date attribution the **What I watched** filter uses. An entry still in progress has no completion date, so a filter-scoped count would report zero under **What I watched** for a period I am demonstrably still watching anime from. This stat therefore answers where the anime came from rather than when I finished something, and under **What I watched** it is the one tile scoped to the period's airing rather than to my watch history; the completed, dropped, and currently-watching counts consequently need not sum to **In this period**. An entry whose anime has no known air date SHALL be counted for no period.

The three status counts — Completed, Dropped, and Currently watching — SHALL be presented together, so what I finished, abandoned, and am still watching read as one group.

Time spent SHALL use each anime's cached average episode duration where one is known, and SHALL otherwise fall back to the same assumed per-episode runtime the profile and series pages already use, so the three surfaces never disagree about the same anime.

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

### Requirement: Bayesian ranking of seasons and years
The season and year rankings SHALL rank on a Bayesian weighted average rather than a raw mean, so a season carrying one 10 does not outrank a season of many strong scores. The weighted score SHALL be computed as:

`W = (v / (v + m)) * R + (m / (v + m)) * C`

where `R` is the mean of my scores for the anime attributed to that season or year, `v` is how many of them I scored, `C` is my mean score across every scored entry in my whole list (not merely the recap period), and `m` is the count at which a group is treated as fully trusted — **5** for a season and **20** for a year.

Because `C` is drawn from the whole list, the same season SHALL rank identically whether it is reached through a yearly recap or a multi-year one covering the same year.

Groups SHALL be ordered by the following comparisons, each applied only when every comparison before it has tied:

1. **Weighted score, highest first**, compared at full precision — the whole computed value, every decimal of it, not the two decimals the row displays. A group whose weighted score is higher by any amount SHALL rank above one whose score is lower, and no comparison below SHALL ever overrule that. Two groups SHALL be treated as tied only when their weighted scores are exactly equal, which the comparisons below then resolve.
2. **Number of scored anime, most first.**
3. **Score by score, from 10 down to 1**: at the highest score where the two groups hold a different number of my anime, the group holding more ranks first. Two groups agreeing at every score from 10 down to 9 but where one holds three 8s to the other's two are therefore separated at 8, and the comparison continues to 1 if needed.
4. **Recency, newest first** — the later year, or the later season, ahead of the earlier one.

This ordering SHALL be identical wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, and SHALL be stable across reloads.

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

#### Scenario: A higher score is never overruled
- **WHEN** two years both display a weighted score of 7.80 but one's underlying score is higher past the second decimal
- **THEN** the higher-scoring year ranks first, whatever their scored counts or score histograms

#### Scenario: The better-covered group wins an exactly equal score
- **WHEN** two years' weighted scores are exactly equal but one was computed over 40 scored anime and the other over 25
- **THEN** the 40-anime year ranks first

#### Scenario: Equal score and equal coverage resolved score by score
- **WHEN** two years' weighted scores are exactly equal, each computed over 50 scored anime, and the first holds two 10s where the second holds one
- **THEN** the first year ranks ahead, on the strength of its extra 10

#### Scenario: The comparison continues below the top scores
- **WHEN** two seasons' weighted scores are exactly equal, they hold the same number of scored anime, and they hold identical numbers of 10s and 9s but one holds more 8s than the other
- **THEN** the season with more 8s ranks first

#### Scenario: Wholly identical groups fall back to recency
- **WHEN** two years' weighted scores are exactly equal, they hold the same number of scored anime, and they hold the same number of my anime at every score from 10 down to 1
- **THEN** the newer year ranks ahead of the older one

### Requirement: Recap page section order
The recap page SHALL present its sections in a fixed order, leading with the period's top anime rather than with its statistics:

1. The top anime of the period, with the stat block beside it — and the period's rating distribution directly below that stat block — positioned to the **right** of the top anime on a display wide enough for the two to sit side by side. On a narrow display these SHALL stack, with the top anime first and the stat block, then the distribution, below it rather than hidden or truncated.
2. The rankings — season, year, and most time watched — laid out per the "Rankings grouped into a season column and a year column" requirement.
3. The hot takes.

The period controls, mode tabs, time filter, and period stepper SHALL remain above all of these, so the period can be changed without scrolling past the recap.

#### Scenario: Top anime leads the page
- **WHEN** a recap renders for a period holding entries
- **THEN** the top anime is the first content below the period controls, with the stat block to its right

#### Scenario: The distribution sits under the stats
- **WHEN** a recap renders its stat block
- **THEN** the period's rating distribution is shown directly below it, above the rankings

#### Scenario: Narrow display stacks the two
- **WHEN** the recap is shown on a display too narrow for a side-by-side layout
- **THEN** the top anime is shown first with the stat block and its distribution below it, all fully readable

#### Scenario: Hot takes come last
- **WHEN** a recap shows stats, a top anime list, rankings, and hot takes
- **THEN** the hot takes appear after every ranking

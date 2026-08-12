## ADDED Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a series as the connected component of the stored related-anime graph, traversing only story relations: `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and `alternative_version`. Every other relation MAL reports — including `alternative_setting`, `character`, `other`, and any unrecognized relation string — SHALL be stored as it already is but SHALL NOT be traversed, so shows that merely share a universe or a cast never merge into one series.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

An anime SHALL belong to at most one series.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: Alternative-setting relations do not merge series
- **WHEN** an anime is related to another only by `alternative_setting` or `character`
- **THEN** the other anime is not a member of its series

#### Scenario: Reverse edges keep the component connected
- **WHEN** anime A stores a `sequel` relation to anime B, and B has never been full-fetched and stores no relations of its own
- **THEN** B is still a member of A's series

### Requirement: Main line and extras
Within a series the system SHALL identify a main line: the largest connected chain over `sequel`/`prequel` relations among the members — ties broken in favour of the chain containing the earliest-aired member — with members whose media type is `special` or `music` excluded from it. Every other member of the series SHALL be an extra.

Extras SHALL be grouped by media type in the fixed display order Movie, OVA, ONA, Special, Music, TV, Other, and ordered by aired-from date within each group.

#### Scenario: Seasons and story movies are main line
- **WHEN** a series contains three TV seasons and a movie, all linked by sequel relations
- **THEN** all four are main-line entries

#### Scenario: Specials are extras even when MAL calls them sequels
- **WHEN** a member whose media type is `special` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: Side stories and music videos are extras
- **WHEN** a series contains a side story, an OVA run, and a music video
- **THEN** none of them are main-line entries, and they appear grouped by media type

#### Scenario: Arriving from a spin-off does not make it the main line
- **WHEN** a series is built starting from the second season of a spin-off whose sequel chain is shorter than the parent series' chain
- **THEN** the parent series' chain is the main line and the spin-off's entries are extras

### Requirement: Watch order and series root
The system SHALL order main-line entries by aired-from date ascending, with entries lacking a date placed last and MAL id breaking ties, and SHALL present that ordering as the series' watch order, numbered from 1.

The series root SHALL be the first entry in that ordering. The series SHALL take its title and its main picture from the root.

#### Scenario: Watch order follows release order
- **WHEN** I open a series whose entries aired in 2013, 2015, a movie in 2016, and 2019
- **THEN** the main-line list is numbered 1–4 in that chronological order

#### Scenario: Series picture and title come from the first entry
- **WHEN** I open a series whose earliest main-line entry is its first season
- **THEN** the page's main picture and the series title are that first season's

### Requirement: Series persistence and identity
The system SHALL persist each derived series with a stable identifier and its member set, recording for each member whether it is main line and its position within its list, plus the time the series was built.

When a newly computed component overlaps one or more already-stored series, the system SHALL keep the stored series with the largest overlap — preserving its identifier — delete the others, and replace its member set wholesale, so a newly announced entry folds into the existing series rather than creating a competing one.

The system SHALL NOT store the series' score averages, computing them at read time instead, so editing a score never leaves a stale average behind.

#### Scenario: Identity survives a rebuild
- **WHEN** a series is rebuilt after a new sequel is announced
- **THEN** the series keeps the identifier it had before, now with the new entry as a member

#### Scenario: Two stored series absorbed into one
- **WHEN** a build's component covers the members of two separately stored series
- **THEN** one series remains, holding every member, and the other stored series is deleted

#### Scenario: Editing a score changes the average immediately
- **WHEN** I change my score on one entry and reopen the series page
- **THEN** my series averages reflect the new score with no rebuild

### Requirement: Bounded series builds
A series build SHALL be bounded by two limits: at most 60 members, and at most 8 live MAL full-detail fetches on a visit-triggered build or 20 on an explicitly requested rebuild. Fetches SHALL be spent first on members that have no cached metadata row at all, since those cannot be displayed otherwise; a member with a lean cached row SHALL be included without a fetch even though its own relations cannot be expanded.

A build that exhausts its fetch budget SHALL mark the series partial; a build that reaches the member cap SHALL mark it truncated. A partial series SHALL be rebuilt on the next visit, so successive visits — each starting from more cached data than the last — complete it without any background job.

Concurrent builds of the same series SHALL collapse into one, matching the single-flight behaviour of the app's other visit-triggered refreshes.

#### Scenario: Build stops at the fetch budget
- **WHEN** I open the series page for a franchise with 20 members the app has never fetched
- **THEN** the page returns after at most 8 live MAL fetches, and the series is marked partial

#### Scenario: A partial series completes over later visits
- **WHEN** I reopen a series that was left partial
- **THEN** it is rebuilt, spends its budget on members still missing, and eventually stops being partial

#### Scenario: Lean members cost no fetch
- **WHEN** a member's metadata was cached by season or top-anime browsing
- **THEN** it is included in the series without spending a fetch

#### Scenario: Concurrent opens fetch once
- **WHEN** two requests for the same series arrive while it is being built
- **THEN** one build runs and both requests are served from it

### Requirement: Series read endpoint and freshness
The system SHALL expose a read endpoint that resolves a series from any member's anime id, building it when no series is stored for that anime, when the stored series is partial, or when it was built more than 30 days ago, and serving the stored series otherwise without any MAL call.

The system SHALL expose a rebuild endpoint that forces recomputation with the larger fetch budget.

When the component derived for an anime contains only that anime, the system SHALL report that it belongs to no series rather than storing a one-member series.

#### Scenario: Cached series is served without fetching
- **WHEN** I open a complete series that was built yesterday
- **THEN** the page renders from stored data and no MAL request is made

#### Scenario: Stale series rebuilds on visit
- **WHEN** I open a series last built 40 days ago
- **THEN** it is rebuilt before the page renders

#### Scenario: Resolving from any member
- **WHEN** I open the series page from the third season's anime id
- **THEN** I get the same series I would get from the first season's id

#### Scenario: Anime with no series
- **WHEN** the series endpoint is called for an anime whose story relations resolve to nothing else
- **THEN** it reports that no series exists rather than returning a series of one

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

The status pill SHALL read `Ongoing` when any member is currently airing, `Upcoming` when no member has finished airing and at least one has not yet aired, and `Finished` otherwise — with `Finished · sequel upcoming` when a finished series has a member that has not yet aired.

#### Scenario: Ongoing series
- **WHEN** I open a series whose latest season is currently airing
- **THEN** the pill reads "Ongoing"

#### Scenario: Finished series with an announced sequel
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Finished · sequel upcoming"

#### Scenario: Year span
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

### Requirement: Series score averages
The series page SHALL show four averages, each with the count it was computed over (e.g. `8.42 · 5 of 6 scored`), rendered to two decimals:

- the MAL average across main-line entries and across all entries, computed as the unweighted mean of the entries that have a MAL score;
- my average across main-line entries and across all entries, computed as the unweighted mean of my scores on entries I have scored, where a score of 0 means unscored and is excluded.

Averages SHALL NOT be weighted by episode count, so a movie counts the same as a season. When no entry in a group has a score, the page SHALL show "No score" rather than a zero. Every MAL average SHALL honour the global hide-scores toggle exactly as MAL scores do elsewhere.

#### Scenario: Both MAL averages shown
- **WHEN** I open a series with four main-line entries and three extras
- **THEN** the page shows one MAL average over the four main-line entries and one over all seven

#### Scenario: My averages exclude unscored entries
- **WHEN** I have scored three of a series' five main-line entries
- **THEN** my main-series average is the mean of those three scores and is labelled as covering 3 of 5

#### Scenario: Unscored series
- **WHEN** I have scored none of a series' entries
- **THEN** my averages read "No score" rather than 0.00

#### Scenario: Hidden MAL averages
- **WHEN** the hide-scores toggle is on
- **THEN** the MAL averages are blurred like every other MAL score, with the value absent from the rendered output

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. Watched time SHALL count watched episodes only and SHALL NOT multiply by rewatch count.

The page SHALL additionally show the longest gap between consecutive main-line entries with the two entries it falls between, the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans.

#### Scenario: Runtime of the main series
- **WHEN** I open a series whose main line totals 62 episodes averaging 24 minutes
- **THEN** the page shows the main-line episode total and a runtime of "1d 0h 48min"

#### Scenario: Extras counted separately
- **WHEN** a series has 5 specials beyond its main line
- **THEN** their episode count and runtime appear as their own figures, not inside the main-line totals

#### Scenario: Unknown episode counts are marked
- **WHEN** a main-line entry is currently airing with an unpublished total episode count
- **THEN** the runtime total is presented as a lower bound rather than an exact figure

#### Scenario: My progress through the series
- **WHEN** I have watched 38 of a series' 62 main-line episodes
- **THEN** the page shows 38/62 with a progress bar, how many entries I have completed, my time watched, and the time left to finish

#### Scenario: Longest gap between entries
- **WHEN** a series' longest wait between consecutive main-line entries was from 2015 to 2019
- **THEN** the page shows that gap along with the two entries it sits between

### Requirement: Main series and More sections
The series page SHALL list the main line as numbered rows in watch order, and every extra under a More section grouped by media type. Each row SHALL show the entry's picture, title, media type, year, episode count, MAL score, my score, and my list status, SHALL link to that anime's detail page, and SHALL offer an edit control that opens the app's shared entry editor.

An edit saved from a series row SHALL update that row in place without reloading the page.

#### Scenario: Main line in watch order
- **WHEN** I open a series with four main-line entries
- **THEN** they are listed 1–4 in watch order with picture, title, type, year, episodes, MAL score, my score, and my status

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them grouped by media type under their own headings

#### Scenario: Editing from a row
- **WHEN** I use a row's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the row and the series averages reflect the new score without a page reload

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

### Requirement: Score comparison strip
The series page SHALL show a per-entry comparison of MAL scores against my scores across the main line, so a dip or a rise across the series is visible at a glance.

Because the comparison encodes scores as graphical magnitudes, which a blur would not conceal, the MAL side of the comparison SHALL NOT be rendered at all while the hide-scores toggle is on; a short note SHALL take its place, and my own scores SHALL continue to be shown.

#### Scenario: Comparing across the series
- **WHEN** I open a series where MAL rates the third season lowest and I rate it highest
- **THEN** the strip shows both scores per entry and the divergence is visible

#### Scenario: Hidden scores are not encoded graphically
- **WHEN** the hide-scores toggle is on
- **THEN** the MAL side of the strip is absent from the rendered output entirely, replaced by a note, while my scores still render

### Requirement: Rebuild control and incomplete-series feedback
The series page SHALL offer a Rebuild control that forces the series to be recomputed with the larger fetch budget and shows that work is in progress while it runs.

When a series is partial or truncated, the page SHALL say so plainly next to that control, rather than silently presenting an incomplete series as complete.

#### Scenario: Rebuilding a series
- **WHEN** I use the Rebuild control
- **THEN** the series is recomputed, progress is visible while it runs, and the page shows the updated members

#### Scenario: Partial series is disclosed
- **WHEN** a build stopped at its fetch budget
- **THEN** the page states that some entries could not be loaded yet, alongside the Rebuild control

#### Scenario: Truncated series is disclosed
- **WHEN** a series hit the member cap
- **THEN** the page states that the series was too large to show in full

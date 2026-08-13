# series-page Specification

## Purpose
TBD - created by archiving change add-series-page. Update Purpose after archive.

## Requirements

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
Within a series the system SHALL identify a main line: the largest connected chain over `sequel`/`prequel` relations among the members — ties broken in favour of the chain containing the earliest-aired member — with members whose media type is `special` or `music` excluded from it, and members tagged as a recap of another member (a `summary`/`full_story` relation to it) excluded regardless of media type. Every other member of the series SHALL be an extra.

A recap tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit recap tag is checked independently.

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

#### Scenario: A recap special stays an extra even when it bridges two seasons
- **WHEN** a `tv_special` recaps one season (`summary`/`full_story`) and also carries a `sequel`/`prequel` edge into the next season
- **THEN** it is an extra, not a main-line entry, and the two real seasons it bridges are still main line

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
A series build SHALL be bounded by two limits: at most 400 members, and at most 8 live MAL full-detail fetches on a visit-triggered build or 20 on an explicitly requested rebuild. Fetches SHALL be spent first on members that have no cached metadata row at all, since those cannot be displayed otherwise; remaining budget SHALL be spent expanding members with a lean cached row (no relations of its own), since an unexpanded lean member can hide a real season from the series or from main-line classification.

The member limit SHALL be a safety ceiling against a runaway component rather than a working limit: it SHALL be set high enough that no real franchise reaches it, so that reaching it means the traversal has gone wrong and the truncation notice is meaningful.

A build that exhausts its fetch budget SHALL mark the series partial; a build that reaches the member cap SHALL mark it truncated. A partial series SHALL be rebuilt on the next visit, so successive visits — each starting from more cached data than the last — complete it without any background job.

A series stored as truncated SHALL NOT be rebuilt automatically on every visit, since a component genuinely over the cap would then re-traverse and spend fetch budget on every visit indefinitely. It SHALL pick up a raised cap on the next explicitly requested rebuild or the next staleness-triggered rebuild.

Concurrent builds of the same series SHALL collapse into one, matching the single-flight behaviour of the app's other visit-triggered refreshes.

#### Scenario: Large franchise is not truncated
- **WHEN** I open the series page for a franchise with more than 60 members, such as One Piece with its movies and specials
- **THEN** every member of its component is included, and the series is not marked truncated

#### Scenario: Build stops at the fetch budget
- **WHEN** I open the series page for a franchise with 20 members the app has never fetched
- **THEN** the page returns after at most 8 live MAL fetches, and the series is marked partial

#### Scenario: A partial series completes over later visits
- **WHEN** I reopen a series that was left partial
- **THEN** it is rebuilt, spends its budget on members still missing, and eventually stops being partial

#### Scenario: Lean members are expanded within budget
- **WHEN** a member's metadata was cached by season or top-anime browsing and fetch budget remains
- **THEN** it is re-fetched so its own relations can extend the series and inform main-line classification

#### Scenario: Lean members are still included when budget runs out
- **WHEN** a member's metadata was cached by season or top-anime browsing and no fetch budget remains
- **THEN** it is included in the series without spending a fetch, using only what other members' relations say about it

#### Scenario: A truncated series does not refetch on every visit
- **WHEN** I open a series that was stored as truncated and make no rebuild request
- **THEN** it is served from stored data without spending fetch budget

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

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL read `Ongoing` when any member is currently airing, `Upcoming` when no member has finished airing and at least one has not yet aired, and `Finished` otherwise — with `Finished · sequel upcoming` when a finished series has a member that has not yet aired.

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Caught up` — every main-line entry that has finished airing (if any) is marked Completed in my list, every currently-airing main-line entry has me watching at least as many episodes as it has broadcast so far, and at least one main-line entry has finished airing or is currently airing.
3. `N behind` — the conditions of (2) hold except that currently-airing main-line entries have broadcast episodes I have not watched. N SHALL be the total of those unwatched broadcast episodes across the main line.
4. No badge — when a main-line entry that has finished airing is not marked Completed in my list, or when no main-line entry has finished airing or is currently airing (nothing has aired yet to be caught up on or behind on).

`Caught up` SHALL therefore never be claimed while episodes of a running main-line season sit unwatched. An entry that has not finished airing SHALL count against the badge only to the extent it has actually broadcast — it cannot be completed yet, but the episodes it has already aired are episodes I could have watched. An entry that has not aired at all SHALL NOT count against the badge, and an entry that is not in my list SHALL count as zero episodes watched.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all; the "every main-line entry that has finished airing is marked Completed" condition is vacuously satisfied in that case; rather than showing no badge for the entire run, it holds the currently-airing entry itself to the same behind-count check as any other currently-airing main-line entry, so a franchise with only ever one continuous entry can still show `Caught up` or `N behind`.

When a currently-airing main-line entry's broadcast episode count is unknown, the page SHALL show no badge rather than claiming `Caught up` or inventing a behind count, since it cannot tell whether I am current.

#### Scenario: Ongoing series
- **WHEN** I open a series whose latest season is currently airing
- **THEN** the pill reads "Ongoing"

#### Scenario: Finished series with an announced sequel
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Finished · sequel upcoming"

#### Scenario: Year span
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

#### Scenario: Completed series is badged
- **WHEN** I have marked every main-line entry of a fully finished series as Completed
- **THEN** the header shows a "Completed" badge next to the status pill

#### Scenario: Caught up on an ongoing series
- **WHEN** I have completed every main-line entry that has finished airing, and I have watched all 8 episodes the currently airing season has broadcast so far
- **THEN** the header shows a "Caught up" badge rather than "Completed"

#### Scenario: Behind on an airing season is not "caught up"
- **WHEN** I have completed every main-line entry that has finished airing, and the currently airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the header shows a "3 behind" badge and does not show "Caught up"

#### Scenario: An airing season I have not started at all
- **WHEN** I have completed every earlier main-line entry and the currently airing season, with 8 episodes broadcast, is not in my list
- **THEN** the header shows an "8 behind" badge

#### Scenario: Caught up when the next entry has not aired yet
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the header shows a "Caught up" badge

#### Scenario: Unfinished series is not badged
- **WHEN** one main-line entry that finished airing is not marked Completed in my list
- **THEN** no personal badge is shown

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far
- **THEN** no personal badge is shown, rather than "Caught up"

### Requirement: Series external links
The series page SHALL offer links out to MyAnimeList, AniList, and SeriesGraph for the series, matching the links the anime detail page offers for a single anime.

All three links SHALL target the series root — the first entry in watch order — since that is the entry under which each site indexes the franchise.

The AniList link SHALL use the root's known AniList id when one is stored, and SHALL otherwise fall back to an AniList title search for the root's display title. The SeriesGraph link SHALL use a title search for the root's display title.

#### Scenario: Links target the first entry
- **WHEN** I open a series whose first entry in watch order is its first season
- **THEN** the MyAnimeList link opens that first season's MAL page, not the page of whichever member I arrived from

#### Scenario: AniList link without a stored id
- **WHEN** the series root has no stored AniList id
- **THEN** the AniList link opens an AniList search for the root's title rather than a broken link

### Requirement: Series score averages
The series page SHALL show, for each of MAL's score and mine, an average across main-line entries and an average across all entries, each with the count it was computed over (e.g. `8.42 · 5 of 6 scored`), rendered to two decimals:

- the MAL average, computed as the unweighted mean of the entries that have a MAL score;
- my average, computed as the unweighted mean of my scores on entries I have scored, where a score of 0 means unscored and is excluded.

Averages SHALL NOT be weighted by episode count, so a movie counts the same as a season. When no entry in a group has a score, the page SHALL show "No score" rather than a zero.

When a series has no extras, the two across-all-entries averages SHALL NOT be rendered at all, since they are computed over exactly the same member set as the main-line averages and would duplicate them.

Every MAL average SHALL honour the global hide-scores toggle exactly as MAL scores do elsewhere. An average SHALL be shown in full rather than blurred only when, in this order of precedence:

1. every main-line entry that has finished airing is marked Completed in my list, **and no main-line entry is currently airing** — in which case both MAL averages SHALL be shown, unconditionally; or
2. every entry in that average's group that has finished airing is marked Completed in my list, **and** no member of the series is currently airing.

A member of the series that is currently airing SHALL therefore suppress the reveal of both MAL averages, whether or not I have scored it — unless it's a spin-off/extra airing after the main line has otherwise completely finished, in which case rule 1 still applies. A main-line entry that is itself currently airing always suppresses rule 1, since the main line has not actually finished in that case — only rule 2 can apply, and it will not, since a currently-airing member fails its own "no member of the series is currently airing" condition too. Otherwise the average SHALL remain blurred behind its reveal control.

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
- **WHEN** the hide-scores toggle is on and I have not completed the series
- **THEN** the MAL averages are blurred like every other MAL score, with the value absent from the rendered output

#### Scenario: A series with no extras shows only the main-series averages
- **WHEN** I open a series where every member is main line
- **THEN** the across-all-entries averages are not rendered, and only the main-series MAL and my averages are shown

#### Scenario: An airing main-line member suppresses the reveal
- **WHEN** the hide-scores toggle is on, I have completed every main-line entry that has finished airing, and the newest main-line season is currently airing
- **THEN** both MAL averages stay blurred, even though I'm caught up on everything that's aired so far

#### Scenario: Completing the main series always reveals both MAL averages
- **WHEN** the hide-scores toggle is on, I have completed every main-line entry that has finished airing, and a spin-off special is currently airing
- **THEN** both the main-series and the across-all-entries MAL averages are shown in full

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. An entry whose total episode count is unknown SHALL still contribute its known aired-so-far episode count toward that lower bound, rather than contributing nothing, whenever an aired count is known for it — so a still-airing entry with no announced total makes the lower bound tighter instead of forcing the whole stat to read as wholly unknown. Watched time SHALL count watched episodes only and SHALL NOT multiply by rewatch count.

While any member of the series is currently airing, my progress SHALL be shown with the same broadcast-progress bar the home page uses — episodes aired so far as the primary fill, my watched episodes layered on top of it — so it is visible how much of what has aired I have seen. When no member is airing, my progress SHALL use the plain watched-against-total bar, since aired and total are then the same figure.

The progress figures SHALL be named rather than left to be inferred from a bare `x/y` label: the page SHALL state my watched episode count, the episodes aired so far, and the main-line total as three separately named figures, each keyed to the colour of the fill it describes. The aired figure SHALL be shown only while a member of the series is currently airing, since it is otherwise the same number as the total, and the total SHALL carry its lower-bound marker here exactly as it does in the episode total.

Episodes aired SHALL be summed over exactly those main-line entries whose total episode count is known, counting an entry that has finished airing as its full total, a currently airing entry as the episodes it has aired so far, and an entry that has not yet aired as none — so the aired figure can never exceed the total the page shows.

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans.

The page SHALL additionally show which entry or entries are tied for the most rewatches across the series (main line and extras alike), naming each tied entry and its rewatch count. This stat SHALL NOT be shown at all when no member of the series has been rewatched, rather than showing a stat naming zero-rewatch entries.

Where several entries tie for the highest MAL score, for my highest score, or for the most rewatches, the page SHALL list every tied entry rather than picking one. Tied highest-MAL entries and tied most-rewatched entries SHALL be listed in watch order. Tied favourites SHALL be listed in my saved favourite order, with entries I have not ordered following in watch order.

The highest MAL score SHALL be shown in full rather than blurred when the entry holding it is one I have both completed and scored, since I already know that score. Until then, the page SHALL withhold that entry's title and link entirely — not only its score — so an unwatched entry is never named by this stat; this withholding applies regardless of the hide-scores toggle's own state, since it protects against spoiling which entry is best rather than against exposing a score value.

#### Scenario: Runtime of the main series
- **WHEN** I open a series whose main line totals 62 episodes averaging 24 minutes
- **THEN** the page shows the main-line episode total and a runtime of "1d 0h 48min"

#### Scenario: Extras counted separately
- **WHEN** a series has 5 specials beyond its main line
- **THEN** their episode count and runtime appear as their own figures, not inside the main-line totals

#### Scenario: Unknown episode counts are marked
- **WHEN** a main-line entry is currently airing with an unpublished total episode count
- **THEN** the runtime total is presented as a lower bound rather than an exact figure

#### Scenario: An unpublished total still counts what has aired
- **WHEN** a main-line entry is currently airing with no published total episode count but 1,100 episodes are known to have aired for it
- **THEN** the main-line episode total includes those 1,100 aired episodes rather than contributing zero for that entry, and the total is still marked as a lower bound

#### Scenario: My progress through the series
- **WHEN** I have watched 38 of a series' 62 main-line episodes and nothing is airing
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, my time watched, and the time left to finish

#### Scenario: Broadcast progress while a season is airing
- **WHEN** a series' latest season is currently airing, 12 of its episodes have aired, and earlier seasons total 50 episodes
- **THEN** my progress shows a bar whose aired fill covers 62 episodes with my watched episodes layered on top of it, and the watched, aired, and total figures are each named beside it

#### Scenario: The aired figure is only shown while something is airing
- **WHEN** I open a series where no member is currently airing
- **THEN** the progress readout names my watched count and the total, and does not repeat the total as a separate aired figure

#### Scenario: Tied highest MAL scores list every entry
- **WHEN** two seasons of a series share the same highest MAL score
- **THEN** both are listed under the highest MAL score, in watch order

#### Scenario: Highest MAL score of a completed entry is not blurred
- **WHEN** the hide-scores toggle is on and the highest-MAL-scored entry is one I have completed and scored
- **THEN** its MAL score is shown in full

#### Scenario: An unwatched entry's title is withheld from Highest MAL score
- **WHEN** the entry holding the series' highest MAL score is not one I have both completed and scored
- **THEN** the page shows no title or link for that entry in the Highest MAL score stat, regardless of whether the hide-scores toggle is on or off

#### Scenario: Tied favourites list every entry
- **WHEN** I have given the same highest score to three entries of a series
- **THEN** all three are listed as my favourite

#### Scenario: Most rewatched entry is shown
- **WHEN** one entry in a series has a rewatch count of 3 and no other member has a higher rewatch count
- **THEN** the page shows a "Most rewatched" stat naming that entry and its count of 3

#### Scenario: Tied most-rewatched entries list every entry
- **WHEN** two entries in a series share the same highest rewatch count
- **THEN** both are listed under "Most rewatched", in watch order

#### Scenario: No rewatch stat when nothing has been rewatched
- **WHEN** no member of a series has a rewatch count above zero
- **THEN** the page shows no "Most rewatched" stat at all

### Requirement: Favourite ordering within a series
When several entries tie for my highest score in a series, the page SHALL let me order them by hand, so that which of them is really my favourite is recorded rather than decided by watch order.

That order SHALL be persisted per series alongside the series' membership, SHALL survive a series rebuild, and SHALL be discarded for an entry only when that entry stops being a member of the series.

Reordering SHALL be offered only when two or more entries are tied. An entry I have never ordered SHALL rank after every entry I have.

An ordering that fails to save SHALL leave the page showing the order that is actually stored, rather than a local order the server does not have.

#### Scenario: Reordering tied favourites
- **WHEN** three entries tie for my highest score and I move the third to the top
- **THEN** it is listed first as my favourite, and it is still listed first when I reload the page

#### Scenario: Favourite order survives a rebuild
- **WHEN** I have ordered my tied favourites and then use the Rebuild control
- **THEN** the entries that are still members keep the order I gave them

#### Scenario: No reordering without a tie
- **WHEN** one entry alone holds my highest score in a series
- **THEN** no reorder controls are shown

#### Scenario: A failed save does not stick
- **WHEN** I reorder my favourites and the save fails
- **THEN** the page returns to the previously stored order

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by media type, so the extras read as a different kind of thing from the chronological main line.

Each tile SHALL carry the information a main-line card carries — picture, title, year, episode count, MAL score, my score, and my list status, with the media type carried by its group heading — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned.

Each More group SHALL show its entry count in its heading. When a series has more than twelve extras every group SHALL start collapsed, and otherwise every group SHALL start expanded.

Each More group SHALL be collapsible, and the section SHALL offer one control that expands or collapses every group at once — unless every extra in the series is already marked Completed in my list, in which case no group offers a collapse/expand control, every group SHALL always render fully expanded, and the one-control affordance SHALL NOT be offered at all, since there is nothing left worth hiding.

That one control, when offered, SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and SHALL read "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

A collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them as poster tiles under a collapsible group per media type, each heading carrying its count

#### Scenario: A large More section starts collapsed
- **WHEN** I open a series with twenty extras
- **THEN** every More group starts collapsed, no tiles are rendered, and one control expands them all

#### Scenario: A small More section starts open
- **WHEN** I open a series with four extras
- **THEN** their groups start expanded

#### Scenario: Editing from a tile
- **WHEN** I use a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the tile and the series stats reflect the new score without a page reload

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

#### Scenario: Collapse control suppressed once everything is watched
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** every More group renders fully expanded with no per-group toggle and no all-groups control

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one media-type group and at least one is not Completed
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more media-type groups and at least one extra is not Completed
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

### Requirement: Series timeline ribbon
The series page SHALL present the main line as one chronological list of cards, one per entry, in watch order — including an entry with no air date yet, such as an announced but unscheduled next season, shown inline in its correct sequence position rather than set apart from the dated entries around it. This section SHALL be the page's only presentation of the main line — there SHALL NOT be a separate, non-chronological list of main-line entries elsewhere on the page.

Every card SHALL be the same fixed size regardless of how long that entry ran, and every pair of adjacent cards SHALL be separated by the same fixed spacing regardless of how long the real wait between them was — a variable-width, aspect-ratio-locked poster reads as inconsistent image sizing rather than as a duration or gap signal, so neither a card's width nor the space around it varies with real elapsed time.

A year ruler SHALL run above the cards, scrolling together with them so a year's label stays aligned with the card it belongs to. The ruler SHALL label only a year in which some main-line entry actually aired; a year that falls entirely between two entries, with nothing airing, SHALL NOT be labelled, so the ruler never implies activity that did not happen. Each labelled year SHALL be positioned at the point on its own card where that year begins, so a season that itself spans several calendar years shows a label for each of those years across its own card, and a year whose boundary falls right as one season ends and the next begins is attributed to the earlier season's card rather than being duplicated or stranded between them. When no main-line entry has an air date, the ruler SHALL be omitted entirely.

Each card SHALL show its picture, title, media type, year (or an explicit no-date indicator for an entry with none), episode count, my list status, and SHALL link to that anime's detail page and offer an edit control that opens the app's shared entry editor. A card's title SHALL reserve the same vertical space regardless of whether it wraps to one line or two, so a short title does not throw the rest of that card's layout out of alignment with its row neighbours. A card for a currently-airing entry SHALL carry a distinct "airing" indicator rather than restating in text how many episodes have broadcast so far, since that count is already shown as a graphical fill on the card. A card for an entry I have started but not completed SHALL additionally state my watched episode count against that entry's total. A card SHALL show its entry's rewatch count when it is greater than zero.

Each card SHALL be filled to show how much of that entry I have watched, and SHALL show broadcast progress behind my own fill while that entry is airing.

Each card SHALL carry a paired score readout — MAL's score for that entry and mine — rendered as numeric values in the same colour convention and visual treatment the More section's tiles already use for their score chips (blue for MAL, purple for mine), rather than as an independently height-scaled graphical bar per entry. The MAL side SHALL follow the same hide-scores behaviour as every other MAL score on the page — blurred with a reveal control while the hide-scores toggle is on, shown in full when the entry is one I have completed and scored and the "always show completed scores" setting is on; my own score SHALL always be shown.

Main-line entries with no air date SHALL be shown in their correct watch-order position among the dated cards rather than set apart in their own lane, carrying an explicit no-date indicator so it reads unambiguously as dateless rather than as a dated card with missing information.

The timeline SHALL scroll within its own container when it does not fit, rather than making the page scroll sideways.

#### Scenario: Main line in watch order
- **WHEN** I open a series with four main-line entries
- **THEN** they are shown in watch order on the timeline, each the same size, showing its picture, title, type, year, episodes, MAL score, my score, and my status

#### Scenario: Cards are the same size and evenly spaced regardless of duration
- **WHEN** a series has one entry that ran for a single cour and another that ran continuously for several years
- **THEN** both cards render at the same size, and the spacing between every pair of cards on the timeline is the same

#### Scenario: A year with nothing airing is not labelled
- **WHEN** a series has a two-year stretch with nothing airing between two seasons
- **THEN** neither of those two years appears on the year ruler

#### Scenario: A year is attributed to the season that was airing
- **WHEN** one season ends and the next begins within the same calendar year
- **THEN** that year's label appears once, on the earlier season's card

#### Scenario: An airing card is marked rather than captioned
- **WHEN** a main-line entry is currently airing
- **THEN** its card carries an airing indicator, and its episode count reads as its total rather than restating in text how many have broadcast so far

#### Scenario: A partly-watched card states my position
- **WHEN** I have watched 5 episodes of a 24-episode entry and have not completed it
- **THEN** its card states my 5 against that entry's 24

#### Scenario: My progress on each card
- **WHEN** I have completed the first season, watched half the second, and not started the third
- **THEN** the first card is fully filled, the second half filled, and the third empty

#### Scenario: Scores shown the same way as the More section
- **WHEN** I open a series where MAL rates the third season lowest and I rate it highest
- **THEN** that card shows both scores as numeric chips in the app's blue-for-MAL/purple-for-mine convention, the same treatment the More section's tiles use

#### Scenario: A MAL score hides and reveals like everywhere else
- **WHEN** the hide-scores toggle is on and a card's entry is not one I have completed and scored
- **THEN** that card's MAL chip is blurred with a reveal control, the same as a MAL score anywhere else on the page

#### Scenario: An unreleased next season is shown in sequence
- **WHEN** a main-line entry has been announced with no air date yet
- **THEN** its card appears in its correct watch-order position among the dated cards, carrying a no-date indicator, rather than being set apart from them

#### Scenario: A rewatched main-line entry shows its count
- **WHEN** a main-line entry has a rewatch count of 2
- **THEN** its card shows a rewatch indicator reading 2

### Requirement: Score and progress colour language
The series page SHALL use one colour convention throughout: MAL's figures and broadcast progress SHALL use the app's blue — the colour the airing-progress bar already fills with for episodes aired — and my own figures and my watched progress SHALL use the app's purple accent. This SHALL apply to the score averages, the per-entry score bars on the timeline, and the progress fills alike, so which side of a figure is "the world" and which is "me" is readable without labels.

The score averages SHALL be rendered as compact chips within the header rather than as full-width panels, each naming what it averages, its value, and the count it was computed over, and each honouring its existing reveal rules under the hide-scores toggle.

#### Scenario: MAL and my averages are colour-keyed
- **WHEN** I open a series
- **THEN** the MAL average chips carry the same colour as the aired fill of the progress bar, and my average chips carry the same colour as my watched fill

#### Scenario: Averages sit in the header
- **WHEN** I open a series
- **THEN** the score averages appear as compact chips inside the header block rather than as a row of full-width panels below it

#### Scenario: Chips still honour hidden scores
- **WHEN** the hide-scores toggle is on and the series' reveal rules do not apply
- **THEN** the MAL average chips are blurred exactly as the panels were, with the value absent from the rendered output

### Requirement: Rebuild control and incomplete-series feedback
The series page SHALL offer a Rebuild control that forces the series to be recomputed and shows that work is in progress while it runs.

A single use of that control SHALL continue recomputing in rounds — each spending the rebuild fetch budget — for as long as the series is still partial and each round adds members, so that a franchise needing more fetches than one round allows is completed by one user action rather than by repeated clicking. It SHALL stop as soon as the series is no longer partial, as soon as a round adds no members, on error, at a fixed maximum number of rounds, or when the page is navigated away from. Progress SHALL be visible between rounds, including how many entries are known so far.

Rounds SHALL NOT run concurrently against the same series.

When a series is partial or truncated, the page SHALL say so plainly next to that control, rather than silently presenting an incomplete series as complete.

#### Scenario: Rebuilding a series
- **WHEN** I use the Rebuild control
- **THEN** the series is recomputed, progress is visible while it runs, and the page shows the updated members

#### Scenario: One click completes a large franchise
- **WHEN** I use the Rebuild control on a franchise that needs more fetches than one round's budget allows
- **THEN** rounds continue automatically until the series is no longer partial, with the entry count updating as it goes

#### Scenario: A round that adds nothing stops the loop
- **WHEN** a rebuild round leaves the series partial but adds no members, because a member keeps failing to fetch
- **THEN** no further rounds run and the page reports that some entries could not be loaded yet

#### Scenario: Partial series is disclosed
- **WHEN** a build stopped at its fetch budget
- **THEN** the page states that some entries could not be loaded yet, alongside the Rebuild control

#### Scenario: Truncated series is disclosed
- **WHEN** a series hit the member cap
- **THEN** the page states that the series was too large to show in full

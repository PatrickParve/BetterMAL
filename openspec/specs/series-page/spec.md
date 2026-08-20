# series-page Specification

## Purpose
TBD - created by archiving change add-series-page. Update Purpose after archive.

## Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a series as the connected component of the stored related-anime graph, traversing only story relations: `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and `alternative_version`. Every other relation MAL reports — including `alternative_setting`, `character`, and any unrecognized relation string — SHALL be stored as it already is but SHALL NOT be traversed, so shows that merely share a universe or a cast never merge into one series.

The `other` relation SHALL be traversed in exactly one case: when precisely one of its two ends is an anime whose media type is `music`. MAL links a franchise's opening/ending/image songs to the show they belong to with `other` and nothing else, so a franchise's music entries are otherwise unreachable — they either vanish from the series entirely or form their own music-only series. A music end SHALL be recognised only from an already-cached media type: an `other` relation whose far end has no cached metadata row SHALL NOT be traversed and SHALL NOT spend fetch budget, since the system cannot tell a song from a commercial without fetching, and `other` also links commercials, promos, and crossovers.

`other` relations where neither end is a music entry, and where both ends are music entries, SHALL NOT be traversed. Because the rule is stated over the relation's two ends rather than over the direction it is stored in, a series built from the song and a series built from the show SHALL produce the same component.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

An anime SHALL belong to at most one series.

Every series stored under the previous traversal rules SHALL be rebuilt once, on its next read, so a franchise's music entries join it without the user having to request a rebuild.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: Alternative-setting relations do not merge series
- **WHEN** an anime is related to another only by `alternative_setting` or `character`
- **THEN** the other anime is not a member of its series

#### Scenario: Reverse edges keep the component connected
- **WHEN** anime A stores a `sequel` relation to anime B, and B has never been full-fetched and stores no relations of its own
- **THEN** B is still a member of A's series

#### Scenario: A franchise's song joins the franchise
- **WHEN** a TV series stores an `other` relation to a `music` entry — its opening theme's music video — and that entry has a cached metadata row
- **THEN** the music entry is a member of that series

#### Scenario: The song's own series is the show's series
- **WHEN** a series is built starting from that music entry instead of from the show
- **THEN** the same component is produced, with the show and its seasons as members, rather than a music-only series

#### Scenario: A music-only series is absorbed
- **WHEN** a music entry and its cover version are stored as their own two-member series, and the franchise the song belongs to is rebuilt under these rules
- **THEN** both are members of the franchise's series and the music-only series no longer exists

#### Scenario: Non-music `other` relations still do not merge series
- **WHEN** a show stores an `other` relation to a commercial, a promotional video, or a crossover short
- **THEN** that anime is not pulled into the show's series

#### Scenario: An uncached `other` end costs nothing
- **WHEN** a member stores an `other` relation to an anime with no cached metadata row
- **THEN** the relation is not traversed, no MAL fetch is spent on it, and the build is not marked partial on its account

### Requirement: Main line and extras
Within a series the system SHALL identify a main line: the connected chain over `sequel`/`prequel` relations among the members holding the most main-line-eligible members — ties broken in favour of the chain containing the earliest-aired eligible member — reduced to just its eligible members. Every other member of the series SHALL be an extra.

A member SHALL be main-line-eligible unless it is any of:
- a member whose media type is `special` or `music`;
- a recap of another member — a `summary`/`full_story` relation to it;
- side content of another member — an outgoing `parent_story` relation to another member, or an incoming `side_story` relation from another member.

Chains SHALL be formed over every member, eligible or not, and ranked afterwards by their eligible members only. Excluding an ineligible member from the ranking SHALL NOT split the chain it sits in, so a recap or side entry that bridges two seasons still keeps those seasons in one chain while never being main line itself.

When any chain holds an eligible member whose media type is `tv`, only such chains SHALL be ranked. A long-running show with no separately-listed seasons is a one-node chain, and without this restriction a handful of side movies that chain to each other could out-count it; when no chain holds an eligible `tv` member — a movie-only or ONA-only franchise — every chain SHALL be ranked.

Because eligibility, not raw chain size, decides the ranking, a franchise whose members carry no `sequel`/`prequel` relations at all — every member its own one-node chain — SHALL still resolve to its actual show rather than to whichever promotional short happens to have aired first.

A recap or side-content tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit tags are checked independently.

Where no member of the series is eligible at all, the system SHALL fall back to the unreduced chain, so a specials-only or side-story-only franchise still has a main line to render.

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

#### Scenario: A member tagged as another member's side story is an extra
- **WHEN** a member declares a `parent_story` relation to another member, or another member declares a `side_story` relation to it
- **THEN** it is an extra, not a main-line entry, even when its media type would otherwise allow it

#### Scenario: A side entry that bridges two seasons does not split the main line
- **WHEN** a side-story member carries `sequel`/`prequel` edges linking two seasons that have no direct edge to each other
- **THEN** both seasons remain in one main-line chain and the side entry itself is an extra

#### Scenario: A lone TV show outranks a chain of side movies
- **WHEN** a franchise's only `tv` member has no `sequel`/`prequel` relations of its own while several of its movies chain to each other
- **THEN** the `tv` member is the main line and the movies are extras

#### Scenario: A franchise with no sequel relations is led by its show, not its promo
- **WHEN** a franchise's members are linked only by `parent_story` relations — a show plus concept and character shorts that each name it as their parent story, with no `sequel`/`prequel` relation anywhere
- **THEN** the show is the sole main-line entry and the shorts are extras, regardless of the shorts having aired years earlier

#### Scenario: Arriving from a spin-off does not make it the main line
- **WHEN** a series is built starting from the second season of a spin-off whose sequel chain is shorter than the parent series' chain
- **THEN** the parent series' chain is the main line and the spin-off's entries are extras

#### Scenario: A franchise of only side entries still renders a main line
- **WHEN** every member of a series is ineligible — all specials, recaps, or side content of one another
- **THEN** the largest chain is used unreduced rather than leaving the series with no main line

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
The system SHALL expose a read endpoint that resolves a series from any member's anime id, building it when no series is stored for that anime, when the stored series is partial, when it was built more than 30 days ago, or when it was built before the current main-line classification rules took effect, and serving the stored series otherwise without any MAL call.

The system SHALL record the point at which its main-line classification rules last changed, and SHALL treat every series built before that point as needing a rebuild, so a correction to classification reaches already-stored series on their next read rather than requiring the user to identify and rebuild each affected series by hand. A classification-triggered rebuild SHALL be identical to any other build — the same traversal rules, fetch budget, single-flight collapsing, and series identity.

The system SHALL expose a rebuild endpoint that forces recomputation with the larger fetch budget.

When the component derived for an anime contains only that anime, the system SHALL report that it belongs to no series rather than storing a one-member series.

#### Scenario: Cached series is served without fetching
- **WHEN** I open a complete series that was built yesterday
- **THEN** the page renders from stored data and no MAL request is made

#### Scenario: Stale series rebuilds on visit
- **WHEN** I open a series last built 40 days ago
- **THEN** it is rebuilt before the page renders

#### Scenario: A series built under superseded classification rules rebuilds on visit
- **WHEN** I open a series that was built before the current classification rules took effect
- **THEN** it is rebuilt before the page renders, and its main line and root reflect the current rules

#### Scenario: A re-classified series keeps its identity
- **WHEN** a classification-triggered rebuild moves a member off the main line and changes the series root
- **THEN** the series keeps its stored identifier and its members keep their favourite ranks

#### Scenario: Resolving from any member
- **WHEN** I open the series page from the third season's anime id
- **THEN** I get the same series I would get from the first season's id

#### Scenario: Anime with no series
- **WHEN** the series endpoint is called for an anime whose story relations resolve to nothing else
- **THEN** it reports that no series exists rather than returning a series of one

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL read `Ongoing` when any member is currently airing **or** when a member has not yet aired, `Upcoming` when no member has finished airing and at least one has not yet aired, and `Finished` otherwise. `Finished` SHALL therefore be reserved for a series with nothing left to come: a series whose aired members have all finished but which has an announced, not-yet-aired member SHALL read `Ongoing`, not `Finished`. The pill SHALL have no `Finished · sequel upcoming` state.

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

#### Scenario: A series with an announced sequel is ongoing
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Ongoing"

#### Scenario: Finished means nothing is left to come
- **WHEN** every member of a series has finished airing and no member is unaired
- **THEN** the pill reads "Finished"

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
The series page SHALL show, for each of MAL's score and mine, an average across main-line entries and an average across all entries, rendered to two decimals (e.g. `8.42`):

- the MAL average, computed as the unweighted mean of the entries that have a MAL score;
- my average, computed as the unweighted mean of my scores on entries I have scored, where a score of 0 means unscored and is excluded.

Averages SHALL NOT be weighted by episode count, so a movie counts the same as a season. When no entry in a group has a score, the page SHALL show "No score" rather than a zero.

A score chip SHALL show its average and its label and nothing else. It SHALL NOT append the count of entries the average was computed over — the `N of M scored` suffix — to either the MAL chips or my chips, since the per-entry scores it summarises are already listed in full in the watch order below it and the suffix crowds the figure the chip exists to show. The count SHALL NOT reappear as a tooltip, a title attribute, or any other rendered form of the same figure.

When a series has no extras, the two across-all-entries averages SHALL NOT be rendered at all, since they are computed over exactly the same member set as the main-line averages and would duplicate them.

Every MAL average SHALL honour the global hide-scores toggle exactly as MAL scores do elsewhere. An average SHALL be shown in full rather than blurred only when, in this order of precedence:

1. every main-line entry that has finished airing is marked **Completed or Dropped** in my list, **and no main-line entry is currently airing** — in which case both MAL averages SHALL be shown, unconditionally; or
2. every entry in that average's group that has finished airing is marked **Completed or Dropped** in my list, **and** no member of the series is currently airing.

Completed and Dropped SHALL count identically in both rules, on the same grounds as the `score-visibility` capability's always-show setting: a finished-airing entry I have dropped is one I have settled, so it can no longer be spoiled by the group's average. An entry that has finished airing and is **not in my list at all** SHALL NOT satisfy either rule — the rules ask what I decided about an entry, and an absent entry carries no decision.

A member of the series that is currently airing SHALL therefore suppress the reveal of both MAL averages, whether or not I have scored it — unless it's a spin-off/extra airing after the main line has otherwise completely finished, in which case rule 1 still applies. A main-line entry that is itself currently airing always suppresses rule 1, since the main line has not actually finished in that case — only rule 2 can apply, and it will not, since a currently-airing member fails its own "no member of the series is currently airing" condition too. Otherwise the average SHALL remain blurred behind its reveal control.

#### Scenario: Both MAL averages shown
- **WHEN** I open a series with four main-line entries and three extras
- **THEN** the page shows one MAL average over the four main-line entries and one over all seven

#### Scenario: My averages exclude unscored entries
- **WHEN** I have scored three of a series' five main-line entries
- **THEN** my main-series average is the mean of those three scores, with the two unscored entries excluded from it

#### Scenario: No scored-count suffix on any chip
- **WHEN** I open a series page and look at the MAL and Mine score chips
- **THEN** each shows only its label and its average, with no "N of M scored" count appended

#### Scenario: Unscored series
- **WHEN** I have scored none of a series' entries
- **THEN** my averages read "No score" rather than 0.00

#### Scenario: Hidden MAL averages
- **WHEN** the hide-scores toggle is on and I have neither completed nor dropped every finished-airing entry of the series
- **THEN** the MAL averages are blurred like every other MAL score, with the value absent from the rendered output

#### Scenario: A dropped main-line entry does not suppress the reveal
- **WHEN** the hide-scores toggle is on, the always-show setting is on, and a series' main line holds three finished-airing entries of which I completed two and dropped the third, with nothing currently airing
- **THEN** both MAL averages are shown in full, because a dropped entry counts as settled exactly like a completed one

#### Scenario: A finished-airing entry missing from my list still suppresses the reveal
- **WHEN** the hide-scores toggle is on and a series' main line holds a finished-airing entry that is not in my list at all
- **THEN** the MAL averages stay blurred, since that entry is neither completed nor dropped

#### Scenario: A series with no extras shows only the main-series averages
- **WHEN** I open a series where every member is main line
- **THEN** the across-all-entries averages are not rendered, and only the main-series MAL and my averages are shown

#### Scenario: An airing main-line member suppresses the reveal
- **WHEN** the hide-scores toggle is on, I have completed or dropped every main-line entry that has finished airing, and the newest main-line season is currently airing
- **THEN** both MAL averages stay blurred, even though I'm caught up on everything that's aired so far

#### Scenario: Settling the main series always reveals both MAL averages
- **WHEN** the hide-scores toggle is on, I have completed or dropped every main-line entry that has finished airing, and a spin-off special is currently airing
- **THEN** both the main-series and the across-all-entries MAL averages are shown in full

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

The entries-completed stat SHALL cover the extras as well as the main line, as two separately labelled figures within one stat: how many main-line entries I have completed out of the main-line total, and how many extras I have completed out of the extras total. The two SHALL NOT be summed into a single figure, so which half of the series is unfinished stays visible. When the series has no extras, the extras figure SHALL be omitted and the stat SHALL show the main-line figure alone rather than an "0 of 0".

Time watched and time left SHALL be shown only while there is time left to watch. When time left computes to zero — I have watched at least as much of the main line as its runtime accounts for — neither stat SHALL be rendered, since "0min left" alongside a time watched that equals the runtime restates what the entries-completed and progress figures already say. Both SHALL be withheld together: the page SHALL NOT show time watched with time left hidden, or the reverse.

This withholding SHALL NOT apply when the main-line runtime is itself unknown — a zero runtime total that the page already marks as unknown rather than as an exact figure. A zero time left derived from a runtime nobody knows reports missing data, not a series I have finished, so both stats SHALL still be shown in that case.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. An entry whose total episode count is unknown SHALL still contribute its known aired-so-far episode count toward that lower bound, rather than contributing nothing, whenever an aired count is known for it — so a still-airing entry with no announced total makes the lower bound tighter instead of forcing the whole stat to read as wholly unknown. Watched time SHALL count watched episodes only and SHALL NOT multiply by rewatch count.

While any member of the series is currently airing, my progress SHALL be shown with the same broadcast-progress bar the home page uses — episodes aired so far as the primary fill, my watched episodes layered on top of it — so it is visible how much of what has aired I have seen. When no member is airing, my progress SHALL use the plain watched-against-total bar, since aired and total are then the same figure.

The progress figures SHALL be named rather than left to be inferred from a bare `x/y` label: the page SHALL state my watched episode count, the episodes aired so far, and the main-line total as three separately named figures, each keyed to the colour of the fill it describes. The aired figure SHALL be shown only while a member of the series is currently airing, since it is otherwise the same number as the total, and the total SHALL carry its lower-bound marker here exactly as it does in the episode total.

Episodes aired SHALL be summed over exactly those main-line entries whose total episode count is known, counting an entry that has finished airing as its full total, a currently airing entry as the episodes it has aired so far, and an entry that has not yet aired as none — so the aired figure can never exceed the total the page shows.

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans.

The page SHALL additionally show which entry or entries are tied for the most rewatches across the series (main line and extras alike), naming each tied entry and its rewatch count. This stat SHALL NOT be shown at all when no member of the series has been rewatched, rather than showing a stat naming zero-rewatch entries.

Where several entries tie for the highest MAL score, for my highest score, or for the most rewatches, the page SHALL list every tied entry rather than picking one. Tied highest-MAL entries and tied most-rewatched entries SHALL be listed in watch order. Tied favourites SHALL be listed in my saved favourite order, with entries I have not ordered following in watch order.

The highest MAL score SHALL be shown in full rather than blurred when the entry holding it is one I have marked **Completed** or **Dropped** in my list. Both statuses settle my relationship with that entry — dropping a show is as much a decision about it as finishing one — so neither leaves a viewing ahead of me that naming the series' best entry could spoil. My having scored that entry SHALL NOT be required: a dropped entry frequently carries no score of mine, and withholding the stat until one exists would hide it indefinitely. Until the entry reaches one of those two statuses, the page SHALL withhold that entry's title and link entirely — not only its score — so an entry I have not settled is never named by this stat; this withholding applies regardless of the hide-scores toggle's own state, since it protects against spoiling which entry is best rather than against exposing a score value.

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

#### Scenario: Entries completed covers extras too
- **WHEN** I open a series where I have completed 5 of 6 main-line entries and 2 of its 3 extras
- **THEN** the entries-completed stat shows "5 of 6" for the main line and "2 of 3" for the extras as two labelled figures, rather than one combined "7 of 9"

#### Scenario: A series with no extras shows one figure
- **WHEN** I open a series whose every member is main line
- **THEN** the entries-completed stat shows only the main-line figure, with no extras figure beside it

#### Scenario: A finished series hides the time stats
- **WHEN** I open a series whose main line I have watched in full, so no time is left
- **THEN** neither "Time left" nor "Time watched" is shown

#### Scenario: A part-watched series keeps both time stats
- **WHEN** I open a series with main-line episodes I have not yet watched
- **THEN** both "Time watched" and "Time left" are shown

#### Scenario: An unknown runtime is not mistaken for a finished series
- **WHEN** I open a series whose main-line runtime total is unknown, so it reports zero time left without my having watched it through
- **THEN** both "Time watched" and "Time left" are still shown, because the zero reflects a runtime nobody knows rather than a series I have finished

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
- **WHEN** the hide-scores toggle is on and the highest-MAL-scored entry is one I have completed
- **THEN** its title, link, and MAL score are shown in full

#### Scenario: Highest MAL score of a dropped entry is shown
- **WHEN** the highest-MAL-scored entry of a series is one I have marked Dropped, and I never gave it a score of my own
- **THEN** the stat names that entry, links to it, and shows its MAL score, exactly as it would for a completed entry

#### Scenario: An unsettled entry's title is withheld from Highest MAL score
- **WHEN** the entry holding the series' highest MAL score is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
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

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading.

The More section SHALL offer two controls: an "in my list" filter and an expand/collapse-all control. Which extras are visible SHALL be governed by those controls alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

The "in my list" filter SHALL be on when a series page is opened: every group renders expanded, showing only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — and hiding every extra that is not in my list. It SHALL be a two-state control that reports which state it is in. Turning it on SHALL restore that filtered view; turning it off SHALL show every extra.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, turning the filter off. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own, and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group showing fewer tiles than its entry count SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them as poster tiles under a collapsible group per media type, each heading carrying its count

#### Scenario: A tile's episode count is not truncated away
- **WHEN** a portrait-pictured extra is a `TV special` that aired in 2003 and has 99 episodes
- **THEN** its tile shows `TV special · 2003 · 99 ep` in full at the grid's narrowest column width

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are longer still than that worst case
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: Opening a series shows only my own extras
- **WHEN** I open a series with twenty extras, four of which are in my list — one Watching, one Completed, one Dropped, one Plan to watch
- **THEN** every group is expanded showing only those four tiles, the sixteen extras not in my list are hidden, and the all-groups control reads "Expand all"

#### Scenario: Expanding shows everything
- **WHEN** I activate the all-groups control from that state
- **THEN** all twenty extras are shown, the "in my list" filter reads as off, and the control now reads "Collapse all"

#### Scenario: Collapsing hides my own extras too
- **WHEN** every extra is shown and I activate the "Collapse all" control
- **THEN** no tiles are rendered in any group, including the extras in my list, and the control reads "Expand all" again

#### Scenario: Filtering back to my list
- **WHEN** every extra is shown and I turn the "in my list" filter on
- **THEN** each group shows only its extras that are in my list, and the all-groups control reads "Expand all"

#### Scenario: A group with nothing of mine in it
- **WHEN** the filter is on and a group holds no extras that are in my list
- **THEN** that group shows its heading and entry count with no tiles, and offers a control naming how many are hidden

#### Scenario: Revealing one group's hidden extras
- **WHEN** I activate that group's hidden-count control
- **THEN** that group shows all of its tiles while every other group keeps showing only my own extras

#### Scenario: Controls are offered whatever my statuses
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** the "in my list" filter and the all-groups control are both still offered, and collapsing hides those completed extras

#### Scenario: A large More section is not treated differently
- **WHEN** I open a series with twenty extras and one with four extras
- **THEN** both open with their groups expanded and filtered to the extras in my list, rather than one of them starting collapsed

#### Scenario: Editing from a tile
- **WHEN** I use a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the tile and the series stats reflect the new score without a page reload

#### Scenario: An extra added to my list from its tile stays visible
- **WHEN** the filter is on, I add an extra to my list from a revealed tile, and the section re-renders
- **THEN** that extra is now one of the tiles the filter keeps

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one media-type group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more media-type groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

### Requirement: Landscape artwork is shown whole on the series page
An entry whose picture is landscape — its intrinsic width greater than its intrinsic height — SHALL have that picture shown whole wherever the series page renders it: the page header's picture, a main-line timeline card's picture, and a More tile's picture. No part of a landscape picture SHALL be cropped away to fill a portrait box.

In the page header, the picture SHALL keep the width it already has and take whatever height its own proportions give it at that width, so the title, status pill, personal badge, year span, links, score averages, and progress beside it keep their existing positions.

On a timeline card and on a More tile, the card's picture area SHALL keep the height it has for portrait artwork, so every card in a row still lines its picture, title, and footer up with its neighbours. The landscape picture SHALL be fitted whole inside that area rather than cropped to fill it, and the card carrying it MAY be wider than its portrait neighbours so that the fitted picture is shown at a useful size rather than reduced to a sliver of the card's height.

A card's extra width SHALL be a consequence of its artwork's orientation alone. It SHALL NOT vary with how long the entry ran, how long the wait before it was, its episode count, its scores, or my progress on it, and SHALL take only one widened size rather than a size computed per image, so a wider card can never be read as a duration or magnitude signal.

This treatment SHALL apply only to landscape artwork. A portrait picture, a square picture, and the placeholder shown when an entry has no picture SHALL keep their existing boxes and their existing card widths unchanged.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL render the existing portrait boxes until then and adopt the landscape treatment once a picture is known to be landscape. The page SHALL NOT request, store, or wait on any additional data to make this decision.

#### Scenario: A landscape header picture is not cropped
- **WHEN** I open a series whose root entry's picture is wider than it is tall
- **THEN** the header shows that whole picture at its own proportions, and the title, pill, badge, year span, links, averages, and progress beside it are positioned exactly as on any other series page

#### Scenario: A landscape timeline card shows its whole picture
- **WHEN** a main-line entry's picture is wider than it is tall
- **THEN** its timeline card shows the whole picture, the card is wider than its portrait neighbours, and its picture area, title, chips, and footer still line up with theirs

#### Scenario: A landscape More tile shows its whole picture
- **WHEN** an extra's picture is wider than it is tall
- **THEN** its More tile shows the whole picture and is wider than the portrait tiles in its group, while its rows stay aligned with them

#### Scenario: Card width still says nothing about duration
- **WHEN** a series has one entry that ran a single cour and another that ran for several years, both with portrait pictures
- **THEN** both cards render at the same width, and the only cards that differ in width anywhere on the page are those whose own artwork is landscape

#### Scenario: Portrait artwork is untouched
- **WHEN** I open a series in which every picture is taller than it is wide
- **THEN** the header picture, every timeline card, and every More tile render exactly as they do today

### Requirement: Series timeline ribbon
The series page SHALL present the main line as one chronological list of cards, one per entry, in watch order — including an entry with no air date yet, such as an announced but unscheduled next season, shown inline in its correct sequence position rather than set apart from the dated entries around it. This section SHALL be the page's only presentation of the main line — there SHALL NOT be a separate, non-chronological list of main-line entries elsewhere on the page.

Every card SHALL be the same fixed size regardless of how long that entry ran, and every pair of adjacent cards SHALL be separated by the same fixed spacing regardless of how long the real wait between them was — a variable-width, aspect-ratio-locked poster reads as inconsistent image sizing rather than as a duration or gap signal, so neither a card's width nor the space around it varies with real elapsed time. The single exception SHALL be a card whose own artwork is landscape, which the landscape-artwork requirement widens to one alternative size for that reason alone; every card of a given orientation SHALL still be the same size as every other card of that orientation, whatever their durations.

Elapsed time on a card SHALL be stated in words rather than implied by position on a scale the cards do not have. There SHALL NOT be a year ruler above the cards, and there SHALL NOT be a connector or any other element between cards stating the wait between them. Instead, each dated card SHALL state its own air range — the month and year it started and the month and year it ended — abbreviating to a single date for an entry that aired on one day. A dated entry that is still broadcasting SHALL state its start month and year and SHALL be marked as still running by its airing indicator rather than by an invented end date.

A calendar year in which no main-line entry aired SHALL never be presented anywhere on the timeline.

Each card SHALL show its picture, title, media type, air range (or an explicit no-date indicator for an entry with none), episode count, my list status, and SHALL link to that anime's detail page and offer an edit control that opens the app's shared entry editor. A card's title SHALL reserve the same vertical space regardless of whether it wraps to one line or two, and a card's air range and episode count SHALL each occupy the same reserved space on every card whatever their content, so no card's layout falls out of alignment with its row neighbours. A card for a currently-airing entry SHALL carry a distinct "airing" indicator rather than restating in text how many episodes have broadcast so far, since that count is already shown as a graphical fill on the card. A card for an entry I have started but not completed SHALL additionally state my watched episode count against that entry's total. A card SHALL show its entry's rewatch count when it is greater than zero.

The airing indicator SHALL name the state in words — a label reading "airing" — rather than relying on chrome alone to carry the meaning, and SHALL be drawn in the page's airing colour rather than in its broadcast colour, so a currently-airing card can never read as a card that is merely selected or focused. It SHALL sit inline in the card's air-range line, in the same position an undated card's no-date indicator occupies, and SHALL NOT be drawn over the poster art — a mark over the artwork can be camouflaged by a poster of a similar colour, while card chrome cannot. Because the indicator is real text, it SHALL be announced by assistive technology without a separate visually-hidden equivalent.

The currently-airing card SHALL additionally be marked at card level by rendering its existing border in the page's airing colour. That card-level mark SHALL NOT surround the card with a glow, SHALL NOT pulse or otherwise animate, and SHALL NOT change the card's size or shift its contents relative to any other card — so it needs no extra room outside the card, and the timeline's own scrolling container cannot clip it. The airing indicator itself SHALL remain visible while the card is hovered or focused; the card-level border MAY take the same hover and focus treatment as any other card, since the indicator and not the border carries the meaning.

Timeline cards SHALL NOT carry a status-coloured edge or border; my list status on a card SHALL be conveyed by its footer text alone. Card chrome SHALL therefore vary only to mark an entry as currently airing or as having no air date, so a coloured card reads unambiguously as one of those two things rather than as one of several list statuses.

Each card SHALL be filled to show how much of that entry I have watched, and SHALL show broadcast progress behind my own fill while that entry is airing.

Each card SHALL carry a paired score readout — MAL's score for that entry and mine — rendered as numeric values in the same colour convention and visual treatment the More section's tiles already use for their score chips (blue for MAL, purple for mine), rather than as an independently height-scaled graphical bar per entry. The MAL side SHALL follow the same hide-scores behaviour as every other MAL score on the page — blurred with a reveal control while the hide-scores toggle is on, shown in full when the entry is one I have completed and scored and the "always show completed scores" setting is on; my own score SHALL always be shown.

Main-line entries with no air date SHALL be shown in their correct watch-order position among the dated cards rather than set apart in their own lane, carrying an explicit no-date indicator so it reads unambiguously as dateless rather than as a dated card with missing information.

The timeline SHALL scroll within its own container when it does not fit, rather than making the page scroll sideways, and SHALL do so without showing a scrollbar — the row SHALL remain scrollable by drag, wheel, or trackpad, but SHALL NOT display a scrollbar track or thumb.

#### Scenario: Main line in watch order
- **WHEN** I open a series with four main-line entries
- **THEN** they are shown in watch order on the timeline, each the same size, showing its picture, title, type, air range, episodes, MAL score, my score, and my status

#### Scenario: Cards are the same size and evenly spaced regardless of duration
- **WHEN** a series has one entry that ran for a single cour and another that ran continuously for several years
- **THEN** both cards render at the same size, and the spacing between every pair of cards on the timeline is the same

#### Scenario: A card states its own air range
- **WHEN** a main-line entry aired from April 2013 to June 2013
- **THEN** its card states that range rather than a bare start year

#### Scenario: A still-airing card states its start and is marked as airing
- **WHEN** a main-line entry started in April 2026 and is still broadcasting
- **THEN** its card's air-range line states April 2026 followed by the airing indicator, with no invented end date

#### Scenario: A multi-year gap is never presented as labelled empty years
- **WHEN** a series has a two-year stretch with nothing airing between two seasons
- **THEN** neither of those years appears anywhere on the timeline

#### Scenario: An airing card is labelled rather than badged over its poster
- **WHEN** a main-line entry is currently airing
- **THEN** its card carries an "airing" label in the page's airing colour within its air-range line, no airing badge is drawn over its poster, and its episode count reads as its total rather than restating in text how many have broadcast so far

#### Scenario: The airing card is not made to look selected
- **WHEN** a main-line entry is currently airing and no card is hovered, focused, or otherwise selected
- **THEN** its card carries no glow around it and nothing on it animates, and its card-level mark is its own border drawn in the airing colour rather than in the page's broadcast colour

#### Scenario: The airing indicator survives hover
- **WHEN** I hover a currently-airing card
- **THEN** its airing label is still visible, and neither the card's size nor the position of its contents changes

#### Scenario: The airing indicator is read as text
- **WHEN** a currently-airing card is read by a screen reader
- **THEN** it is announced as airing from the card's own visible text, with no separate visually-hidden duplicate of that announcement

#### Scenario: The airing indicator is never clipped
- **WHEN** a currently-airing entry's card is the first or last card in the row
- **THEN** its airing label and its coloured border both render in full, uncut by the timeline's own scrolling container

#### Scenario: A long timeline scrolls without a visible scrollbar
- **WHEN** a series has enough main-line entries that the row overflows its container
- **THEN** the row can still be scrolled horizontally, but no scrollbar track is shown beneath it

#### Scenario: Cards carry no status colour
- **WHEN** I open a series with entries I am watching, have completed, and have dropped
- **THEN** none of those cards carries a status-coloured edge, and each states its status in its footer text

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

#### Scenario: Two landscape cards are the same width as each other
- **WHEN** a series has two main-line entries with landscape artwork, one that ran for one cour and one that ran for several years
- **THEN** both of their cards render at the same widened size as each other, and every portrait card on the timeline renders at the usual size

### Requirement: Score and progress colour language
The series page SHALL use one colour convention throughout: MAL's figures and broadcast progress SHALL use the app's blue — the colour the airing-progress bar already fills with for episodes aired — and my own figures and my watched progress SHALL use the app's purple accent. This SHALL apply to the score averages, the per-entry score bars on the timeline, and the progress fills alike, so which side of a figure is "the world" and which is "me" is readable without labels.

The two score colours SHALL be the app-wide score colour roles defined by the `score-presentation` capability, not a page-local convention: the same blue and purple SHALL carry the same meaning on every other view that renders a MAL score or my score. Broadcast and watched progress fills SHALL follow those same two roles here.

The page SHALL additionally use green to mark that something is on air right now. Green SHALL mark that state only — it SHALL NOT be used for any figure, score, or progress fill, so it never competes with blue-for-MAL/broadcast or purple-for-mine. Broadcast progress on a currently-airing card SHALL therefore stay blue while the card's airing indicator is green: blue measures how much has broadcast, green says it is still broadcasting. Green SHALL remain specific to this page's airing cues and SHALL NOT become part of the app-wide score language.

The score averages SHALL be rendered as compact chips within the header rather than as full-width panels, each naming what it averages, its value, and the count it was computed over, and each honouring its existing reveal rules under the hide-scores toggle.

#### Scenario: MAL and my averages are colour-keyed
- **WHEN** I open a series
- **THEN** the MAL average chips carry the same colour as the aired fill of the progress bar, and my average chips carry the same colour as my watched fill

#### Scenario: Green marks airing and nothing else
- **WHEN** I open a series whose latest season is currently airing
- **THEN** that card's airing indicator is green, while its broadcast fill stays the page's blue and its score chips stay blue and purple

#### Scenario: Averages sit in the header
- **WHEN** I open a series
- **THEN** the score averages appear as compact chips inside the header block rather than as a row of full-width panels below it

#### Scenario: Chips still honour hidden scores
- **WHEN** the hide-scores toggle is on and the series' reveal rules do not apply
- **THEN** the MAL average chips are blurred exactly as the panels were, with the value absent from the rendered output

#### Scenario: The same colours carry the same meaning elsewhere
- **WHEN** I leave the series page for the anime detail page, My List, or the profile page
- **THEN** MAL scores there carry the same blue and my scores the same purple as the series page's chips

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

### Requirement: Series builds can be triggered in the background by search
The system SHALL support building a series in the background, triggered by a search that found no stored series for its top-ranked anime match (see the `navigation-and-search` capability), in addition to the existing trigger of opening a series page.

A background-triggered build SHALL be identical in every other respect to a visit-triggered one: the same traversal rules, the same visit fetch budget, the same partial/truncated marking, and the same single-flight collapsing — so a background build and a user opening that series page at the same moment SHALL result in one build, not two.

A background build SHALL be best-effort: a failure SHALL be logged and dropped, leaving no stored series and affecting nothing the user is doing.

An anime whose story relations resolve to nothing else SHALL still store no series, exactly as on a visit-triggered build.

#### Scenario: A search-triggered build stores the series
- **WHEN** a search schedules a build for an anime belonging to an unbuilt franchise
- **THEN** the series is built and stored just as it would be by opening its series page

#### Scenario: Background build and page visit collapse into one
- **WHEN** a background build for a series is in progress and I open that series' page
- **THEN** one build runs and the page is served from it

#### Scenario: A background build failure is contained
- **WHEN** a background build fails part-way through
- **THEN** the failure is logged, no partial result is presented to the user, and nothing the user is doing is interrupted

#### Scenario: A lone anime still stores no series
- **WHEN** a background build runs for an anime whose story relations resolve to nothing else
- **THEN** no series is stored, matching the visit-triggered behaviour

### Requirement: Series builds can be triggered by the profile's Top series read
The system SHALL schedule background series builds for anime in my list that belong to no stored series, triggered by reading the profile page's Top series section — in addition to the existing page-visit and search triggers.

The number of builds scheduled by one such read SHALL be bounded, so that opening the profile page on a store with thousands of unbuilt anime schedules a small batch rather than the whole catalogue. Repeat reads SHALL continue where earlier ones left off rather than re-scheduling the same anime, so the section's coverage improves visit by visit.

Scheduling SHALL never delay, alter, or fail the response: the section SHALL be served from whatever is already stored, and a scheduling or build failure SHALL be invisible to it.

A background build triggered this way SHALL be identical in every other respect to a visit-triggered one — the same traversal rules, fetch budget, partial/truncated marking, and single-flight collapsing.

#### Scenario: Reading Top series schedules missing builds
- **WHEN** I open the profile page and some anime in my list belong to no stored series
- **THEN** background builds are scheduled for a bounded batch of them, and the section renders immediately from what is already stored

#### Scenario: Coverage improves across visits
- **WHEN** I open the profile page again after an earlier read scheduled its batch
- **THEN** the newly built series are ranked, and the next batch of still-unbuilt anime is scheduled

#### Scenario: A scheduling failure is invisible
- **WHEN** scheduling or a scheduled build fails
- **THEN** the profile page is unaffected and the failure is logged rather than surfaced

### Requirement: Build all series from my list
The system SHALL provide an action on the Settings page that builds a series for every anime in my list that belongs to no stored series, and rebuilds every series that was built before the current main-line classification rules took effect, so the profile page's Top series ranking can be completed and corrected on demand rather than only filling in over time.

The action SHALL run in the background and SHALL NOT block the request that starts it. The request that starts it SHALL report the run as in flight, without waiting for the background run to begin, so that a single press is enough for the Settings page to show progress and disable the control. While it runs, the system SHALL report progress as the number of targets processed out of the total, and the Settings page SHALL show that progress and refresh it while the run is in flight. After a run finishes, its final counts SHALL remain visible until another run starts.

The action's control SHALL be disabled while a run is in flight, so one run cannot be started on top of another.

A target already covered by an earlier build in the same run — because building one anime's franchise also stores its other members — SHALL be counted as processed without being built again, so progress reflects real remaining work and no franchise is built once per member.

A failure on one target SHALL be logged and SHALL NOT abort the run; remaining targets SHALL still be processed. A failure that ends the whole run SHALL be reported as a failed run with the counts it reached, and SHALL leave the control usable again, so a run that dies before or during target resolution never leaves the page reporting a build that is not happening.

Individual builds SHALL use the same traversal rules, fetch budget, partial/truncated marking, and single-flight collapsing as every other build, so a bulk run racing a user opening a series page results in one build, not two.

#### Scenario: Building every missing series
- **WHEN** I use the "Build all series from my list" action
- **THEN** the request returns immediately and a background run builds a series for each anime in my list that has none

#### Scenario: One press shows progress
- **WHEN** I press the action once
- **THEN** the page reports the run as in flight and begins showing progress without a second press

#### Scenario: Out-of-date series are rebuilt too
- **WHEN** a run starts and some of my list's anime belong to series built before the current classification rules
- **THEN** those series are rebuilt by the run, not skipped as already covered

#### Scenario: Progress is visible while it runs
- **WHEN** a run is in flight
- **THEN** the Settings page shows how many targets have been processed out of the total, updating as the run proceeds

#### Scenario: Final counts stay after it finishes
- **WHEN** a run completes
- **THEN** the Settings page reports the run as complete with its final counts

#### Scenario: The action cannot be double-started
- **WHEN** a run is in flight
- **THEN** the action's control is disabled

#### Scenario: One build covers a whole franchise
- **WHEN** a run builds a series and later reaches another anime that build already stored as an up-to-date member
- **THEN** that target is counted as processed without being built again

#### Scenario: One failing target does not stop the run
- **WHEN** building one target fails
- **THEN** the failure is logged and the run continues with the remaining targets

#### Scenario: A run that fails outright is reported as failed
- **WHEN** a run fails before or during target resolution
- **THEN** the page reports a failed run rather than a run still in progress, and the control becomes usable again

#### Scenario: A bulk build and a page visit collapse into one
- **WHEN** a bulk run is building a series and I open that series' page at the same moment
- **THEN** one build runs and the page is served from it

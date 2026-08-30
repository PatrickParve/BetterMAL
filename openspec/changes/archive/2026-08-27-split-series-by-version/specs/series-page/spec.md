## MODIFIED Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a component of the stored related-anime graph by traversing story relations — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version` — together with `alternative_setting`, and SHALL then partition that component into one series per telling as the `series-versions` capability defines. `alternative_version` and `alternative_setting` are traversed so that every telling of a franchise is **discovered** by one build; they never merge two tellings into one series.

Every other relation MAL reports — including `character`, `adaptation`, and any unrecognized relation string — SHALL be stored as it already is and SHALL NOT be traversed, so shows that merely share a cast never merge into one series. Such a relation SHALL still reach the series page, as a related entry under "Every relation of a main-line entry is shown in More".

The `other` relation SHALL be traversed in exactly one case: when precisely one of its two ends is an anime whose media type is one of a fixed **companion-media set** — `music` and `pv`. MAL links a franchise's opening/ending/image songs and its promotional videos to the show they belong to with `other` and nothing else, so a franchise's music and PV entries are otherwise unreachable — they either vanish from the series entirely or form their own companion-only series. `cm` (commercial) SHALL NOT be in the companion-media set.

`other` relations where neither end is in the companion-media set, and where both ends are in it, SHALL NOT be traversed — so a promo for a theme song never fuses two franchises, and one promotional video never chains to another. Because the rule is stated over the relation's two ends rather than over the direction it is stored in, a series built from the companion entry and a series built from the show SHALL produce the same component.

Recognising an `other` edge's far end requires that end's media type, which MAL does not carry on the relation itself. When an `other` edge's far end has **no cached metadata row at all**, the system SHALL spend a bounded **probe** on it — a fetch made solely to learn its media type — within the probe budget the bounded-builds requirement defines. A probe SHALL cache that anime's row, so each `other` far end SHALL be probed at most once across all builds, and every later build SHALL decide that edge from cache at no cost. A far end whose probe reveals a media type in the companion-media set SHALL be admitted as a member using the row the probe already produced; any other media type SHALL leave the edge untraversed thereafter without further cost. An `other` edge whose far end is still unprobed because the budget is exhausted SHALL NOT be traversed on that build.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

A story relation an external source **contradicts** — one end asserts it, the other end was fetched and does not, and AniList knows both anime and relates them not at all — SHALL NOT be traversed. Such an edge is one anime's unreciprocated claim about another that no other source supports, and traversing it silently admits an unrelated anime to the franchise. The edge SHALL remain stored and SHALL remain visible on the detail page; only its power to pull a member into a series is withdrawn. An edge that is merely unreciprocated, with no external source to settle it, SHALL still be traversed, so a franchise never shrinks on the strength of missing information alone.

An anime SHALL belong to at most one series **per telling**, and SHALL appear at most once on any one series page. It MAY belong to more than one series, as `series-versions` defines.

Every series stored under the previous traversal, partitioning or grouping rules SHALL be rebuilt once, on its next read, so this change's corrections reach an already-stored series without the user having to request a rebuild.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: An alternative version does not merge two tellings
- **WHEN** an anime is related to another by `alternative_version` and both are main-line-eligible
- **THEN** the two are traversed into one component but stored as two series, one per telling

#### Scenario: An alternative setting does not merge two tellings
- **WHEN** an anime is related to another only by `alternative_setting`
- **THEN** the two are traversed into one component and stored as two series, rather than the relation being ignored altogether

#### Scenario: Character relations still do not merge series
- **WHEN** an anime is related to another only by `character`
- **THEN** the other anime is not a member of its series, and is shown as a related entry instead

#### Scenario: Reverse edges keep the component connected
- **WHEN** anime A stores a `sequel` relation to anime B, and B has never been full-fetched and stores no relations of its own
- **THEN** B is still a member of A's series

#### Scenario: A contradicted edge does not admit a member
- **WHEN** MAL reports `17965 --sequel--> 39360`, 39360 has been fetched and stores no relations at all, and AniList knows both and relates them not at all
- **THEN** 39360 is not a member of 17965's series

#### Scenario: An unreciprocated edge with no external verdict still connects
- **WHEN** anime A stores a `sequel` relation to anime B, B stores nothing back, and AniList does not know one of them
- **THEN** B remains a member of A's series, exactly as before this change

#### Scenario: A franchise's song joins the franchise
- **WHEN** a TV series stores an `other` relation to a `music` entry — its opening theme's music video — and that entry has a cached metadata row
- **THEN** the music entry is a member of that series

#### Scenario: A franchise's promotional video joins the franchise
- **WHEN** a TV series stores an `other` relation to an entry whose media type is `pv`
- **THEN** that promotional video is a member of that series

#### Scenario: The song's own series is the show's series
- **WHEN** a series is built starting from that music entry instead of from the show
- **THEN** the same component is produced, with the show and its seasons as members, rather than a music-only series

#### Scenario: A music-only series is absorbed
- **WHEN** a music entry and its cover version are stored as their own two-member series, and the franchise the song belongs to is rebuilt under these rules
- **THEN** both are members of the franchise's series and the music-only series no longer exists

#### Scenario: One promotional video does not chain to another
- **WHEN** two `pv` entries store an `other` relation to each other
- **THEN** that relation is not traversed, and neither pulls the other into its series

#### Scenario: A promo for a theme song does not fuse two franchises
- **WHEN** a `pv` entry stores an `other` relation to a `music` entry
- **THEN** that relation is not traversed, since both of its ends are companion media

#### Scenario: Commercials and crossovers still do not merge series
- **WHEN** a show stores an `other` relation to a commercial or a crossover short
- **THEN** that anime is not pulled into the show's series, and is shown as a related entry instead

#### Scenario: An uncached `other` end is probed once
- **WHEN** a member stores an `other` relation to an anime with no cached metadata row, and probe budget remains
- **THEN** that anime is fetched once, its media type decides whether the edge is traversed, and no later build spends a probe on that edge again

#### Scenario: A probed commercial is remembered as untraversable
- **WHEN** a probe reveals an `other` far end to be a commercial, and the series is built again later
- **THEN** the edge is skipped from cache with no fetch, and the commercial is not a member

#### Scenario: A stored series re-derives itself after a rules change
- **WHEN** a series stored under the previous rules is next read
- **THEN** it is rebuilt under the current rules, without the user pressing Rebuild

### Requirement: Main line and extras
Within a series the system SHALL identify a main line: the connected chain over `sequel`/`prequel` relations among the members holding the most main-line-eligible members — ties broken in favour of the chain containing the earliest-aired eligible member — reduced to just its eligible members. Every other member of the series SHALL be an extra.

Main-line classification SHALL run per telling, over that telling's own member set, so the main line is never handed to a chain belonging to another telling of the same franchise.

A member SHALL be main-line-eligible unless it is any of:
- a member whose media type is `special`, `music`, or `pv`;
- a recap of another member — a `summary`/`full_story` relation to it;
- side content of another member — an outgoing `parent_story` relation to another member, or an incoming `side_story` relation from another member;
- a **boundary member** — an anchor of another telling, held by this series only so that it can be shown as an alternative version or setting.

A promotional video is never a chapter of a story, so a `pv` member SHALL never be main line however MAL relates it — including in a franchise whose real entries carry no `sequel`/`prequel` relations at all, where a chain of promos could otherwise out-rank the show.

Chains SHALL be formed over every member, eligible or not, and ranked afterwards by their eligible members only. Excluding an ineligible member from the ranking SHALL NOT split the chain it sits in, so a recap or side entry that bridges two seasons still keeps those seasons in one chain while never being main line itself.

When any chain holds an eligible member whose media type is `tv`, only such chains SHALL be ranked. A long-running show with no separately-listed seasons is a one-node chain, and without this restriction a handful of side movies that chain to each other could out-count it; when no chain holds an eligible `tv` member — a movie-only or ONA-only franchise — every chain SHALL be ranked.

Because eligibility, not raw chain size, decides the ranking, a franchise whose members carry no `sequel`/`prequel` relations at all — every member its own one-node chain — SHALL still resolve to its actual show rather than to whichever promotional short happens to have aired first.

A recap or side-content tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit tags are checked independently.

Where no member of the series is eligible at all, the system SHALL fall back to the unreduced chain, so a specials-only or side-story-only franchise still has a main line to render.

Extras SHALL be grouped by their **relation to the main line** rather than by media type, in the fixed display order Alternative version, Alternative setting, Prequel, Sequel, Parent story, Side story, Full story, Summary, Spin-off, Character, Adaptation, Other, and ordered by aired-from date within each group.

An extra's group SHALL be resolved so that it belongs to exactly one:

1. Where the extra carries a relation, in either direction, to any main-line member of this series, its group SHALL be the highest-precedence such relation in the display order above. Version relations rank first precisely so a boundary member that also carries a sequel edge still reads as an alternative version.
2. Where it does not — an extra reached only through another extra — it SHALL inherit the group of the extra that reaches it, resolved breadth-first outward from the main line, ties broken by the lower MAL id. A special of a side story therefore reads as Side story.
3. Failing both, its group SHALL be Other.

The relation SHALL be read **directionally, from the extra's side**: `M --summary--> X` makes X a Summary, while `X --summary--> M` makes X the Full story; `M --side_story--> X` makes X a Side story, while `X --side_story--> M` makes X the Parent story. The same holds for `sequel`/`prequel`.

#### Scenario: Sequels and story movies are main line
- **WHEN** a series contains three TV seasons and a movie, all linked by sequel relations
- **THEN** all four are main-line entries

#### Scenario: Specials are extras even when MAL calls them sequels
- **WHEN** a member whose media type is `special` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: A promotional video is never main line
- **WHEN** a member whose media type is `pv` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: Extras are grouped by relation, not media type
- **WHEN** a series has two specials that recap its first season and one OVA that is a side story of its second
- **THEN** the two specials appear under a "Summary" group and the OVA under a "Side story" group, rather than under "Special" and "OVA"

#### Scenario: Direction decides the group
- **WHEN** an extra declares `full_story` to a main-line member, and another extra is the target of a main-line member's `summary`
- **THEN** the first is grouped under "Full story" and the second under "Summary"

#### Scenario: A version relation outranks a sequel edge
- **WHEN** a boundary member carries both an `alternative_version` relation and a `sequel` relation to main-line members
- **THEN** it appears under "Alternative version"

#### Scenario: An extra of an extra inherits its group
- **WHEN** a special's only relation is to an extra that is grouped under "Side story"
- **THEN** that special is grouped under "Side story" too

#### Scenario: Side stories and music videos are extras
- **WHEN** a series contains a side story, an OVA run, and a music video
- **THEN** none of them are main-line entries, and they appear grouped by their relation to the main line

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

#### Scenario: Another telling's chain cannot take the main line
- **WHEN** the 2003 telling's sequel chain is longer than Brotherhood's
- **THEN** Brotherhood's page still shows Brotherhood's own chain as its main line

#### Scenario: A franchise of only side entries still renders a main line
- **WHEN** every member of a series is ineligible — all specials, recaps, or side content of one another
- **THEN** the largest chain is used unreduced rather than leaving the series with no main line

### Requirement: Watch order and series root
The system SHALL order main-line entries in **story order**: a topological ordering over the `sequel`/`prequel` edges among the main-line members, so an entry always precedes the entries it is a prequel to. The system SHALL present that ordering as the series' watch order, numbered from 1.

Aired-from date SHALL be a tie-break, not the ordering: where two entries are unconstrained relative to each other — neither reachable from the other over the chain — the earlier aired-from date SHALL come first, entries lacking a date SHALL be placed last, and MAL id SHALL break the remaining tie. An entry with no chain edge at all SHALL therefore be placed purely by its aired date, interleaved with the chain.

The main line is deliberately **story order, not release order**. A prequel film released after the season it precedes belongs before that season in a watch order, and ordering by air date puts it after — the chain edges the main line is already classified from state the correct order and were previously discarded at the ordering step.

Where the chain edges contain a cycle, so that no topological order exists, the system SHALL break the cycle in favour of aired-from order and produce a stable ordering rather than failing the build. Extras remain ordered by aired-from date within their **relation group**, as specified under "Main line and extras".

Watch order and root SHALL be computed per telling, over that telling's own main line. Each telling SHALL therefore have its own root, and SHALL take its title and its main picture from that root, so two tellings of one franchise never share a header.

#### Scenario: A prequel film released later still sorts first
- **WHEN** a series contains Jujutsu Kaisen (aired 2020-10-03) and Jujutsu Kaisen 0 (aired 2021-12-24), and MAL states `40748 --prequel--> 48561` on both ends
- **THEN** Jujutsu Kaisen 0 is watch-order 1 and Jujutsu Kaisen is watch-order 2

#### Scenario: Chain order beats air date
- **WHEN** two main-line entries are linked by a `sequel`/`prequel` edge whose direction disagrees with their aired-from dates
- **THEN** the chain edge decides their order

#### Scenario: Unconstrained entries fall back to air date
- **WHEN** two main-line entries have no chain path between them
- **THEN** the one that aired first is ordered first, with MAL id breaking a remaining tie

#### Scenario: An entry with no chain edge is placed by date
- **WHEN** a main-line entry carries no `sequel`/`prequel` edge to any other member
- **THEN** it is placed among the others by its aired-from date

#### Scenario: Ordinary series are unaffected
- **WHEN** I open a series whose entries aired in 2013, 2015, a movie in 2016, and 2019, each chaining to the next
- **THEN** the main-line list is numbered 1–4 in that same order

#### Scenario: A cyclic chain still renders
- **WHEN** MAL's relations put two main-line entries in a `sequel`/`prequel` cycle
- **THEN** the series still renders a numbered main line, ordered by aired-from date where the cycle is broken

#### Scenario: Series picture and title come from the first entry
- **WHEN** I open a series whose earliest story-order main-line entry is its first season
- **THEN** the page's main picture and the series title are that first season's

#### Scenario: Each telling has its own root
- **WHEN** I open each of a franchise's two tellings
- **THEN** each page shows its own telling's first entry as its picture and title, not the other's

### Requirement: Series persistence and identity
The system SHALL persist each derived series with a stable identifier and its member set, recording for each member whether it is main line, its position within its list, its relation group, and whether that series is the member's primary one, plus the time the series was built.

A member SHALL be recorded per series rather than per anime, so an anime shared between two tellings holds one record in each. Exactly one of an anime's records SHALL be marked primary, as the `series-versions` capability defines.

When a build's partition produces several series, all of them SHALL be persisted together. Newly derived series SHALL be matched to already-stored series by descending member overlap, each stored series claimed at most once; a claimed series keeps its identifier, and an unclaimed stored series overlapping the component is deleted after surrendering any chosen title or picture the claiming series lacks. A newly announced entry therefore folds into the existing series rather than creating a competing one, and a franchise that splits into tellings keeps one of the stored identifiers rather than discarding it.

The system SHALL NOT store the series' score averages, computing them at read time instead, so editing a score never leaves a stale average behind.

#### Scenario: Identity survives a rebuild
- **WHEN** a series is rebuilt after a new sequel is announced
- **THEN** the series keeps the identifier it had before, now with the new entry as a member

#### Scenario: Two stored series absorbed into one
- **WHEN** a build's component covers the members of two separately stored series and produces one telling
- **THEN** one series remains, holding every member, and the other stored series is deleted

#### Scenario: A shared member holds a record in each series
- **WHEN** an OVA is a member of two tellings
- **THEN** two membership records exist for it, each carrying its own main-line flag, position and favourite rank, and exactly one marked primary

#### Scenario: Editing a score changes the average immediately
- **WHEN** I change my score on one entry and reopen the series page
- **THEN** my series averages reflect the new score with no rebuild

### Requirement: Bounded series builds
A series build SHALL be bounded by three limits: at most 400 members, at most 8 live MAL full-detail fetches on a visit-triggered build or 20 on an explicitly requested rebuild, and a separate **probe budget** of at most 4 fetches on a visit-triggered build or 10 on an explicitly requested rebuild. Fetches SHALL be spent first on members that have no cached metadata row at all, since those cannot be displayed otherwise; remaining budget SHALL be spent expanding members with a lean cached row (no relations of its own), since an unexpanded lean member can hide a real season from the series or from main-line classification.

The limits SHALL apply to the **component** a build traverses, which spans every telling of a franchise, and SHALL be shared across the series that component is partitioned into. Traversal SHALL be breadth-first from the seed, so the telling the build was seeded from is completed before budget is spent on distant ones.

The probe budget SHALL be separate from the member fetch budget and SHALL be spent only on resolving the media type of an `other` edge's uncached far end, per the composition requirement. A probe SHALL NOT consume member fetch budget and member fetches SHALL NOT consume probe budget, so a franchise carrying many `other` edges to commercials can never starve the fetches that real, story-related members need in order to be displayed at all.

Related entries — the non-traversed relations shown in More — SHALL cost neither budget and SHALL NOT count toward the member cap, since they are rendered from stored relation rows rather than fetched.

Because a probe caches the row it fetches, no `other` far end is probed more than once across all builds, and the probe budget's steady-state cost for a franchise the user revisits SHALL be zero.

The member limit SHALL be a safety ceiling against a runaway component rather than a working limit: it SHALL be set high enough that no real franchise reaches it, so that reaching it means the traversal has gone wrong and the truncation notice is meaningful.

A build that exhausts either its fetch budget or its probe budget SHALL mark every series it stores partial; a build that reaches the member cap SHALL mark them truncated. A partial series SHALL be rebuilt on the next visit, so successive visits — each starting from more cached data than the last — complete it without any background job. Because probes never repeat, a series left partial by an exhausted probe budget SHALL converge over successive visits rather than re-probing indefinitely.

A series stored as truncated SHALL NOT be rebuilt automatically on every visit, since a component genuinely over the cap would then re-traverse and spend fetch budget on every visit indefinitely. It SHALL pick up a raised cap on the next explicitly requested rebuild or the next staleness-triggered rebuild.

Concurrent builds of the same series SHALL collapse into one, matching the single-flight behaviour of the app's other visit-triggered refreshes.

#### Scenario: Large franchise is not truncated
- **WHEN** I open the series page for a franchise with more than 60 members, such as One Piece with its movies and specials
- **THEN** every member of its component is included, and the series is not marked truncated

#### Scenario: Build stops at the fetch budget
- **WHEN** I open the series page for a franchise with 20 members the app has never fetched
- **THEN** the page returns after at most 8 live MAL fetches, and the series is marked partial

#### Scenario: The seeded telling is completed first
- **WHEN** I open one telling of a franchise whose other tellings hold many uncached members
- **THEN** budget is spent on the telling I opened before the distant ones, and its page renders complete

#### Scenario: Related entries cost nothing
- **WHEN** a series' main-line entries carry forty `character` relations to anime with no cached rows
- **THEN** all forty are shown as related entries, no fetch is spent on them, and the series is not marked partial on their account

#### Scenario: Probes do not eat the member fetch budget
- **WHEN** a build probes four uncached `other` far ends and also fetches members with no cached row
- **THEN** the member fetches available are still the full 8, unreduced by the probes

#### Scenario: A build stops at the probe budget and is marked partial
- **WHEN** I open a series whose members carry ten `other` edges to anime with no cached row
- **THEN** at most four are probed on that visit and the series is marked partial

#### Scenario: Probing converges over visits
- **WHEN** I reopen that same series on later visits
- **THEN** the remaining far ends are probed a few at a time, none is probed twice, and the series eventually stops being partial

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

Where the anime is a member of more than one series, the endpoint SHALL resolve to its **primary** series, as the `series-versions` capability defines. A telling reached from another telling's page SHALL be addressed by its own root's anime id, which resolves to that telling.

The system SHALL record the point at which its main-line classification rules last changed, and SHALL treat every series built before that point as needing a rebuild, so a correction to classification reaches already-stored series on their next read rather than requiring the user to identify and rebuild each affected series by hand. A classification-triggered rebuild SHALL be identical to any other build — the same traversal rules, fetch budget, single-flight collapsing, and series identity.

The system SHALL expose a rebuild endpoint that forces recomputation with the larger fetch budget.

When the component derived for an anime contains only that anime, the system SHALL report that it belongs to no series rather than storing a one-member series. An anime whose only relation is a version relation SHALL NOT fall under this rule, since the telling it is an alternative of is a member of its series.

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

#### Scenario: A shared member resolves to its primary
- **WHEN** I open the series page from the anime id of an OVA shared between two tellings
- **THEN** I get its primary series, and that page lists the other telling as an alternative version

#### Scenario: Anime with no series
- **WHEN** the series endpoint is called for an anime whose story relations resolve to nothing else
- **THEN** it reports that no series exists rather than returning a series of one

#### Scenario: A lone alternative version does have a series
- **WHEN** the series endpoint is called for an anime whose only relation is an `alternative_version`
- **THEN** it returns that anime's own series, holding it and the telling it is an alternative of

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by their relation to the main line, so the extras read as a different kind of thing from the chronological main line and each group states how its entries stand to the franchise.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned. Because groups no longer share a media type, each tile's media type SHALL be legible on the tile itself.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading, counting the entries the media-type filter currently admits.

The More section SHALL offer three section-wide controls: an "in my list" filter, an expand/collapse-all control, and the media-type filter buttons its own requirement defines. Which extras are visible SHALL be governed by those controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

The "in my list" filter SHALL be on when a series page is opened: every group renders expanded, showing only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — and hiding every extra that is not in my list. It SHALL be a two-state control that reports which state it is in, per "A More group's heading opens that group in full". Turning it on SHALL restore that filtered view across every group; turning it off SHALL show every extra.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, turning the filter off. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own from its heading, per "A More group's heading opens that group in full", and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group that is showing at least one tile while the filter hides the rest SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows. A group showing no tiles at all — because it is collapsed, or because nothing in it is in my list — SHALL NOT offer that control: its heading opens it, and its entry count is already in the heading.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More by relation
- **WHEN** a series has two recap specials, one side-story OVA, and one alternative version
- **THEN** the More section shows them as poster tiles under a collapsible "Summary", "Side story" and "Alternative version" group, each heading carrying its count

#### Scenario: Media type is legible on the tile
- **WHEN** one "Side story" group holds an OVA, a movie and a special
- **THEN** each tile states its own media type

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
- **THEN** that group shows its heading and entry count with no tiles and no hidden-count control, and its heading opens it in full

#### Scenario: Revealing one group's hidden extras
- **WHEN** the filter is on, a group is showing the extras of mine it holds while hiding others, and I activate that group's hidden-count control
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
- **WHEN** every member of a series is main line and its main-line entries carry no untraversed relations
- **THEN** the More section is not shown

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one relation group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more relation groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

### Requirement: The More section's view state is restored with the page
The More section's four view controls — the "in my list" filter, the media-type filter, each group's collapsed state, and each group's exemption from the "in my list" filter — SHALL be part of the series page's restorable state, restored on back/forward navigation exactly as every other page's view controls are, per the `page-state-restoration` capability.

Returning to a series page by back/forward navigation SHALL therefore show the More section as it was left: a group opened in full is still open, a collapsed group is still collapsed, the media types I selected are still selected, and the "in my list" control still reports the state it reported when the page was left.

A fresh visit — a link, a typed URL, a reload — SHALL still open the section on its documented default: the "in my list" filter on, no media type selected, every group expanded, and no group exempted. The state SHALL NOT be persisted beyond the browser tab's application session, and SHALL NOT be shared between two different series' pages.

A group key held in restored state that matches no group the restored page renders — because the series' extras changed between the two renders — SHALL be ignored rather than treated as an error. A restored media type that no extra of the series carries SHALL likewise be ignored.

#### Scenario: An opened group is still open on return
- **WHEN** I open a More group in full, open one of its extras, and navigate back
- **THEN** that group is still showing all of its tiles, and the "in my list" control still reads as off

#### Scenario: The filter is not reset by a round trip
- **WHEN** I turn the "in my list" filter off, open an entry, and navigate back
- **THEN** the filter is still off and every extra is still shown

#### Scenario: Selected media types survive a round trip
- **WHEN** I select "Movie" and "OVA", open an entry, and navigate back
- **THEN** both are still selected and the section shows the same tiles it showed before

#### Scenario: A collapsed group is still collapsed on return
- **WHEN** I collapse a group, navigate away, and navigate back
- **THEN** that group renders no tiles

#### Scenario: A fresh visit still opens on the default
- **WHEN** I reach a series page by following a link rather than by navigating back
- **THEN** the "in my list" filter is on, no media type is selected, every group is expanded, and no group is exempted

#### Scenario: Two series do not share More-section state
- **WHEN** I open one series and expand its More section, then open a different series
- **THEN** the second series' More section opens on the default, unaffected by the first

### Requirement: Build all series from my list
The system SHALL provide an action on the Settings page that builds a series for every anime in my list that belongs to no stored series, and rebuilds every series that was built before the current main-line classification rules took effect, so the profile page's Top series ranking can be completed and corrected on demand rather than only filling in over time.

A target SHALL be considered covered only when it holds a **primary** membership in an up-to-date stored series, so an anime that is only a shared or boundary member of some other telling is still built into its own.

The action SHALL run in the background and SHALL NOT block the request that starts it. The request that starts it SHALL report the run as in flight, without waiting for the background run to begin, so that a single press is enough for the Settings page to show progress and disable the control. While it runs, the system SHALL report progress as the number of targets processed out of the total, and the Settings page SHALL show that progress and refresh it while the run is in flight. After a run finishes, its final counts SHALL remain visible until another run starts.

The action's control SHALL be disabled while a run is in flight, so one run cannot be started on top of another.

A target already covered by an earlier build in the same run — because building one anime's franchise also stores its other members, and every other telling of it — SHALL be counted as processed without being built again, so progress reflects real remaining work and no franchise is built once per member.

A failure on one target SHALL be logged and SHALL NOT abort the run; remaining targets SHALL still be processed. A failure that ends the whole run SHALL be reported as a failed run with the counts it reached, and SHALL leave the control usable again, so a run that dies before or during target resolution never leaves the page reporting a build that is not happening.

Individual builds SHALL use the same traversal rules, partitioning, fetch budget, partial/truncated marking, and single-flight collapsing as every other build, so a bulk run racing a user opening a series page results in one build, not two.

#### Scenario: Building every missing series
- **WHEN** I use the "Build all series from my list" action
- **THEN** the request returns immediately and a background run builds a series for each anime in my list that has none

#### Scenario: One press shows progress
- **WHEN** I press the action once
- **THEN** the page reports the run as in flight and begins showing progress without a second press

#### Scenario: Out-of-date series are rebuilt too
- **WHEN** a run starts and some of my list's anime belong to series built before the current classification rules
- **THEN** those series are rebuilt by the run, not skipped as already covered

#### Scenario: One build covers every telling
- **WHEN** a run builds a franchise with two tellings and later reaches an anime of the second telling
- **THEN** that target is counted as processed without being built again

#### Scenario: A boundary-only membership does not count as covered
- **WHEN** an anime in my list is stored only as a boundary member of another telling's series
- **THEN** the run builds its own series rather than skipping it

#### Scenario: Progress is visible while it runs
- **WHEN** a run is in flight
- **THEN** the Settings page shows how many targets have been processed out of the total, updating as the run proceeds

#### Scenario: Final counts stay after it finishes
- **WHEN** a run completes
- **THEN** the Settings page reports the run as complete with its final counts

#### Scenario: The action cannot be double-started
- **WHEN** a run is in flight
- **THEN** the action's control is disabled

#### Scenario: One failing target does not stop the run
- **WHEN** building one target fails
- **THEN** the failure is logged and the run continues with the remaining targets

#### Scenario: A run that fails outright is reported as failed
- **WHEN** a run fails before or during target resolution
- **THEN** the page reports a failed run rather than a run still in progress, and the control becomes usable again

#### Scenario: A bulk build and a page visit collapse into one
- **WHEN** a bulk run is building a series and I open that series' page at the same moment
- **THEN** one build runs and the page is served from it

## ADDED Requirements

### Requirement: The More section offers media-type filter buttons
Above the More section the page SHALL offer one button per media type present among that series' extras and related entries — TV, Movie, OVA, ONA, Special, Music, PV, and any other type those entries carry — as a multi-select set.

Selecting a type SHALL narrow every group to the entries of that type. Selecting several SHALL show the entries of any selected type. Selecting none SHALL narrow nothing, which is the state a freshly opened page is in.

The buttons SHALL state which of them are selected. A button whose type no entry carries SHALL NOT be offered.

The type filter SHALL compose with the "in my list" filter and with each group's collapsed or opened state rather than replacing them: an entry is shown when its type is admitted **and** the other controls admit it. Each group's heading count SHALL report the entries the type filter admits.

While at least one type is selected, a group left with no admitted entries SHALL NOT be rendered at all, since a column of empty headings across a dozen relation groups tells the reader nothing.

The type buttons SHALL NOT narrow the main-line timeline. The main line is a numbered watch order, and hiding one of its entries would make the numbering misstate the series.

#### Scenario: One type narrows every group
- **WHEN** I select "Movie"
- **THEN** every group shows only its movies, and groups holding no movie are not rendered

#### Scenario: Several types are additive
- **WHEN** I select "Movie" and "OVA"
- **THEN** every group shows its movies and its OVAs

#### Scenario: Deselecting the last type restores everything
- **WHEN** I deselect the only selected type
- **THEN** every group shows what the other controls admit, as on a freshly opened page

#### Scenario: The timeline is untouched
- **WHEN** I select "Movie" on a series whose main line is four TV seasons
- **THEN** the timeline still shows all four, numbered 1–4

#### Scenario: Only present types are offered
- **WHEN** a series' extras and related entries hold no music entry
- **THEN** no "Music" button is offered

#### Scenario: The type filter composes with the list filter
- **WHEN** the "in my list" filter is on and I select "OVA"
- **THEN** the groups show only OVAs that are in my list

#### Scenario: Counts follow the type filter
- **WHEN** a group holds six entries of which two are movies and I select "Movie"
- **THEN** that group's heading reports two

### Requirement: Every relation of a main-line entry is shown in More
The More section SHALL additionally show, as **related entries**, every anime a main-line member relates to by a relation the series traversal does not follow — `character`, `adaptation`, a non-companion `other`, and any unrecognized relation string — whether or not that anime is in my list.

Related entries SHALL be read in both directions, exactly as an anime's own relation set is read, and SHALL be grouped and ordered by the same rules extras are: the relation group named by the relation, read directionally from the related entry's side.

A group SHALL be named by the relation and never by the related entry's media type. A commercial reaches the page over a non-companion `other` relation and SHALL therefore appear under Other alongside every other `other` relation, rather than in a group of its own; media type is what the type filter buttons select on.

A related entry SHALL be rendered from what is already stored for that relation — its title, its picture and its media type — enriched with its cached metadata and my list entry when those exist, and SHALL NOT require a MyAnimeList fetch to be shown. It SHALL link to that anime's detail page and SHALL offer the same edit control every other tile offers.

A related entry SHALL NOT be a member of the series: it SHALL NOT enter any average, any stat, any episode or runtime total, any member count, or the member cap, and SHALL NOT appear on the series' card in the browser. It is shown, not counted.

An anime that is already a member of the series SHALL NOT also be listed as a related entry, so nothing appears twice on the page.

Because these entries are read from what is stored rather than fetched, they SHALL be complete on the first read of a series and SHALL NOT mark it partial.

#### Scenario: A character relation reaches the page
- **WHEN** a main-line entry carries a `character` relation to an anime not in my list
- **THEN** that anime is shown under a "Character" group in the More section

#### Scenario: A commercial reaches the page without joining the series
- **WHEN** a main-line entry carries an `other` relation to a commercial
- **THEN** the commercial is shown under "Other", and the series' averages, stats and member count are unchanged by it

#### Scenario: Nothing is fetched to show it
- **WHEN** a main-line entry relates to an anime with no cached metadata row
- **THEN** it is still shown, from the stored relation's title, picture and media type, with no MyAnimeList request

#### Scenario: A cached related entry shows its full detail
- **WHEN** a related entry's anime has a cached metadata row and is in my list
- **THEN** its tile shows its scores, episode count and my status like any other tile, and offers the same edit control

#### Scenario: A member is not repeated as a related entry
- **WHEN** an anime is both a member of the series and the target of an untraversed relation from a main-line entry
- **THEN** it is shown once, as a member

#### Scenario: Related entries do not make a series partial
- **WHEN** a series' only unresolved data is the metadata of its related entries
- **THEN** the series is not marked partial and is not rebuilt on the next visit for that reason

### Requirement: An alternative version's tile opens that version's series
A tile in the Alternative version or Alternative setting group SHALL open that telling's own series page rather than the anime's detail page, since a telling is a franchise and the page that describes it is its series page.

The tile SHALL address that page by the anchor's **anime id**, the same way every other link to a series page in the app addresses one. The series read endpoint builds a series for an anime that has none, so a telling that has not been stored yet is built by the visit rather than being an unreachable link; and because an anchor always carries a version relation to another member, its component is never the one-member case the endpoint reports as belonging to no series. The tile SHALL therefore have no dead state and SHALL need no fallback target.

Every other tile in the More section SHALL keep opening the anime's detail page.

#### Scenario: Crossing to the other telling
- **WHEN** I open Brotherhood's series page and activate the Fullmetal Alchemist (2003) tile in its Alternative version group
- **THEN** the 2003 telling's series page opens

#### Scenario: An unstored telling is built by the visit
- **WHEN** the alternative version shown has no stored series and I activate its tile
- **THEN** its series is built and its page renders, as it would on any first visit to a series

#### Scenario: Other groups are unaffected
- **WHEN** I activate a tile in the Side story group
- **THEN** that anime's detail page opens, as before

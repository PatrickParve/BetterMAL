## MODIFIED Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a series as the connected component of the stored related-anime graph, traversing **story relations only** — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off` — together with the narrow companion-media case of `other` defined below. The `series-versions` capability governs how that component becomes a series.

`alternative_version` and `alternative_setting` SHALL NOT be traversed. They SHALL be followed **one hop** from each member, after the component is complete, solely to find that component's **version neighbours**, which the `series-versions` capability classifies. Following them SHALL NOT admit anything reachable only through a version neighbour, and SHALL NOT merge or split a component.

Every other relation MAL reports — including `character`, `adaptation`, and any unrecognized relation string — SHALL be stored as it already is and SHALL NOT be traversed, so shows that merely share a cast never merge into one series. Such a relation SHALL still reach the series page, as a related entry under "Every relation of a main-line entry is shown in More".

The `other` relation SHALL be traversed in exactly one case: when precisely one of its two ends is an anime whose media type is one of a fixed **companion-media set** — `music` and `pv`. MAL links a franchise's opening/ending/image songs and its promotional videos to the show they belong to with `other` and nothing else, so a franchise's music and PV entries are otherwise unreachable — they either vanish from the series entirely or form their own companion-only series. `cm` (commercial) SHALL NOT be in the companion-media set.

`other` relations where neither end is in the companion-media set, and where both ends are in it, SHALL NOT be traversed — so a promo for a theme song never fuses two franchises, and one promotional video never chains to another. Because the rule is stated over the relation's two ends rather than over the direction it is stored in, a series built from the companion entry and a series built from the show SHALL produce the same component.

Recognising an `other` edge's far end requires that end's media type, which MAL does not carry on the relation itself. When an `other` edge's far end has **no cached metadata row at all**, the system SHALL spend a bounded **probe** on it — a fetch made solely to learn its media type — within the probe budget the bounded-builds requirement defines. A probe SHALL cache that anime's row, so each `other` far end SHALL be probed at most once across all builds, and every later build SHALL decide that edge from cache at no cost. A far end whose probe reveals a media type in the companion-media set SHALL be admitted as a member using the row the probe already produced; any other media type SHALL leave the edge untraversed thereafter without further cost. An `other` edge whose far end is still unprobed because the budget is exhausted SHALL NOT be traversed on that build.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

A story relation an external source **contradicts** — one end asserts it, the other end was fetched and does not, and AniList knows both anime and relates them not at all — SHALL NOT be traversed. Such an edge is one anime's unreciprocated claim about another that no other source supports, and traversing it silently admits an unrelated anime to the franchise. The edge SHALL remain stored and SHALL remain visible on the detail page; only its power to pull a member into a series is withdrawn. An edge that is merely unreciprocated, with no external source to settle it, SHALL still be traversed, so a franchise never shrinks on the strength of missing information alone.

An anime SHALL belong to at most one **story component**, and SHALL appear at most once on any one series page. It MAY additionally be a version-neighbour extra of other series, as `series-versions` defines.

Every series stored under the previous traversal, partitioning or grouping rules SHALL be rebuilt once, on its next read, so this change's corrections reach an already-stored series without the user having to request a rebuild.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: A version relation neither merges nor splits
- **WHEN** two main-line-eligible anime are related by `alternative_version` and also joined by a `sequel` chain through a third
- **THEN** all three are members of one series, and the version relation changes only how they are shown

#### Scenario: An alternative version with no story path is not a member
- **WHEN** an anime is related to a member only by `alternative_version` and shares no story relation with the component
- **THEN** it is not part of that component, and is handled as a version neighbour

#### Scenario: Nothing is admitted through a version neighbour
- **WHEN** a version neighbour carries `sequel` relations to two anime of its own
- **THEN** neither of those anime is a member of this series

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

Main-line classification SHALL run over the series' own **story-component** members. A version neighbour SHALL never be main line, and SHALL never bridge two chains, since it is not part of the component the chains are formed over.

A member SHALL be main-line-eligible unless it is any of:
- a member whose media type is `special`, `music`, or `pv`;
- a recap of another member — a `summary`/`full_story` relation to it;
- side content of another member — an outgoing `parent_story` relation to another member, or an incoming `side_story` relation from another member.

A promotional video is never a chapter of a story, so a `pv` member SHALL never be main line however MAL relates it — including in a franchise whose real entries carry no `sequel`/`prequel` relations at all, where a chain of promos could otherwise out-rank the show.

Chains SHALL be formed over every member, eligible or not, and ranked afterwards by their eligible members only. Excluding an ineligible member from the ranking SHALL NOT split the chain it sits in, so a recap or side entry that bridges two seasons still keeps those seasons in one chain while never being main line itself.

When any chain holds an eligible member whose media type is `tv`, only such chains SHALL be ranked. A long-running show with no separately-listed seasons is a one-node chain, and without this restriction a handful of side movies that chain to each other could out-count it; when no chain holds an eligible `tv` member — a movie-only or ONA-only franchise — every chain SHALL be ranked.

Because eligibility, not raw chain size, decides the ranking, a franchise whose members carry no `sequel`/`prequel` relations at all — every member its own one-node chain — SHALL still resolve to its actual show rather than to whichever promotional short happens to have aired first.

A recap or side-content tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit tags are checked independently.

Where no member of the series is eligible at all, the system SHALL fall back to the unreduced chain, so a specials-only or side-story-only franchise still has a main line to render.

Extras SHALL be grouped by their **relation to the main line** rather than by media type, in the fixed display order Alternative version, Alternative setting, Prequel, Sequel, Parent story, Side story, Full story, Summary, Spin-off, Character, Adaptation, Other, and ordered by aired-from date within each group.

An extra's group SHALL be resolved so that it belongs to exactly one:

1. Where the extra carries a relation, in either direction, to any main-line member of this series, its group SHALL be the highest-precedence such relation in the display order above. Version relations rank first precisely so a version neighbour that also carries a sequel edge still reads as an alternative version.
2. Where it does not — an extra reached only through another extra — it SHALL inherit the group of the extra that reaches it, resolved breadth-first outward from the main line, ties broken by the lower MAL id. A special of a side story therefore reads as Side story. A **version neighbour** SHALL NOT pass its group on by inheritance, and SHALL NOT be reached by it, so a story extra is never labelled an alternative version merely for sitting next to one.
3. Failing both, its group SHALL be Other.

The relation SHALL be read **directionally, from the extra's side**: `M --summary--> X` makes X a Summary, while `X --summary--> M` makes X the Full story; `M --side_story--> X` makes X a Side story, while `X --side_story--> M` makes X the Parent story. The same holds for `sequel`/`prequel`.

#### Scenario: Sequels and story movies are main line
- **WHEN** a series contains three TV seasons and a movie, all linked by sequel relations
- **THEN** all four are main-line entries

#### Scenario: A sequel is not demoted by a version relation
- **WHEN** a series' second season carries an `alternative_setting` relation to a special of its own and a `prequel` relation to the first season
- **THEN** it is a main-line entry, not an extra grouped under Sequel

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
- **WHEN** a version neighbour carries both an `alternative_version` relation and a `sequel` relation to main-line members
- **THEN** it appears under "Alternative version"

#### Scenario: An extra of an extra inherits its group
- **WHEN** a special's only relation is to an extra that is grouped under "Side story"
- **THEN** that special is grouped under "Side story" too

#### Scenario: A version neighbour does not pass its group on
- **WHEN** an extra's only relation is to a version neighbour grouped under "Alternative setting"
- **THEN** that extra is grouped under "Other" rather than under "Alternative setting"

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

#### Scenario: A separate telling's chain cannot take the main line
- **WHEN** Brotherhood is a version neighbour of the Fullmetal Alchemist (2003) series and its own chain is longer
- **THEN** the 2003 series' main line is still its own chain

#### Scenario: A franchise of only side entries still renders a main line
- **WHEN** every member of a series is ineligible — all specials, recaps, or side content of one another
- **THEN** the largest chain is used unreduced rather than leaving the series with no main line

### Requirement: Watch order and series root
The system SHALL order main-line entries in **story order**: a topological ordering over the `sequel`/`prequel` edges among the main-line members, so an entry always precedes the entries it is a prequel to. The system SHALL present that ordering as the series' watch order, numbered from 1.

Where the main line contains a **version slot** — main-line entries that are alternative versions of one another, per the `series-versions` capability — the ordering SHALL be computed with each slot contracted to a single position, so every alternative of a slot takes the same position and a branch entry is ordered around the slot rather than always after it. The rendered numbering SHALL be computed over the entries actually shown for the picked alternative, so it never skips a number.

Aired-from date SHALL be a tie-break, not the ordering: where two entries are unconstrained relative to each other — neither reachable from the other over the chain — the earlier aired-from date SHALL come first, entries lacking a date SHALL be placed last, and MAL id SHALL break the remaining tie. An entry with no chain edge at all SHALL therefore be placed purely by its aired date, interleaved with the chain.

The main line is deliberately **story order, not release order**. A prequel film released after the season it precedes belongs before that season in a watch order, and ordering by air date puts it after — the chain edges the main line is already classified from state the correct order and were previously discarded at the ordering step.

Where the chain edges contain a cycle, so that no topological order exists — including a cycle introduced by contracting a version slot — the system SHALL break the cycle in favour of aired-from order and produce a stable ordering rather than failing the build. Extras remain ordered by aired-from date within their **relation group**, as specified under "Main line and extras".

The root SHALL be the first entry of the series' watch order, and the series SHALL take its title and its main picture from that root. The root SHALL NOT change with the picked alternative, so a series' header is stable however the watch order is filtered.

#### Scenario: A prequel film released later still sorts first
- **WHEN** a series contains Jujutsu Kaisen (aired 2020-10-03) and Jujutsu Kaisen 0 (aired 2021-12-24), and MAL states `40748 --prequel--> 48561` on both ends
- **THEN** Jujutsu Kaisen 0 is watch-order 1 and Jujutsu Kaisen is watch-order 2

#### Scenario: Chain order beats air date
- **WHEN** two main-line entries are linked by a `sequel`/`prequel` edge whose direction disagrees with their aired-from dates
- **THEN** the chain edge decides their order

#### Scenario: Alternatives share a position
- **WHEN** two main-line entries form a version slot
- **THEN** they occupy one position in the watch order rather than two consecutive ones

#### Scenario: The header does not move with the picker
- **WHEN** I switch the picked alternative of a version slot
- **THEN** the series' title and main picture are unchanged

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

#### Scenario: Two separate tellings have their own roots
- **WHEN** I open each of two series joined only by a version relation
- **THEN** each page shows its own first entry as its picture and title, not the other's

### Requirement: Series persistence and identity
The system SHALL persist each derived series with a stable identifier and its member set, recording for each member whether it is main line, its position within its list, its relation group, whether it is a story-component member or a version neighbour, whether that series is the member's primary one, and — for a main-line entry — its version slot and its branch, plus the time the series was built.

A member SHALL be recorded per series rather than per anime, so an anime that is a version neighbour of two series holds one record in each. Exactly one of an anime's records SHALL be marked primary, as the `series-versions` capability defines.

A build SHALL persist the seed's series alone. Its component SHALL be matched to the stored series it overlaps most, computing overlap over **story-component members only**; that series keeps its identifier, and every other stored series overlapping the component is deleted after surrendering any chosen title or picture the survivor lacks. A newly announced entry therefore folds into the existing series rather than creating a competing one, and the fragments left by an earlier rules change collapse back into one series rather than persisting.

The system SHALL NOT store the series' score averages, computing them at read time instead, so editing a score never leaves a stale average behind.

#### Scenario: Identity survives a rebuild
- **WHEN** a series is rebuilt after a new sequel is announced
- **THEN** the series keeps the identifier it had before, now with the new entry as a member

#### Scenario: Two stored series absorbed into one
- **WHEN** a build's component covers the members of two separately stored series
- **THEN** one series remains, holding every member, and the other stored series is deleted

#### Scenario: A version neighbour holds its own record
- **WHEN** an anime is a version neighbour of two series
- **THEN** two membership records exist for it, each carrying its own relation group and favourite rank, and neither is primary where it holds a story-component membership elsewhere

#### Scenario: A neighbour's stored series is not absorbed
- **WHEN** a rebuilt series holds another series' root as a version-neighbour extra
- **THEN** that other series is not deleted, since overlap is computed over story-component members only

#### Scenario: Editing a score changes the average immediately
- **WHEN** I change my score on one entry and reopen the series page
- **THEN** my series averages reflect the new score with no rebuild

### Requirement: Bounded series builds
A series build SHALL be bounded by three limits: at most 400 members, at most 8 live MAL full-detail fetches on a visit-triggered build or 20 on an explicitly requested rebuild, and a separate **probe budget** of at most 4 fetches on a visit-triggered build or 10 on an explicitly requested rebuild. Fetches SHALL be spent first on members that have no cached metadata row at all, since those cannot be displayed otherwise; remaining budget SHALL be spent expanding members with a lean cached row (no relations of its own), since an unexpanded lean member can hide a real season from the series or from main-line classification.

The limits SHALL apply to the **story component** a build traverses, and SHALL be spent there alone. Because version relations are not traversed, no budget can be spent on a separate telling before the seed's own series is complete.

The version-neighbour hop SHALL spend **no** fetch and **no** probe budget: a neighbour is classified from its cached relation rows, and a neighbour with no cached row is shown as a related entry rather than fetched.

The probe budget SHALL be separate from the member fetch budget and SHALL be spent only on resolving the media type of an `other` edge's uncached far end, per the composition requirement. A probe SHALL NOT consume member fetch budget and member fetches SHALL NOT consume probe budget, so a franchise carrying many `other` edges to commercials can never starve the fetches that real, story-related members need in order to be displayed at all.

Related entries — the non-traversed relations shown in More — SHALL cost neither budget and SHALL NOT count toward the member cap, since they are rendered from stored relation rows rather than fetched.

Because a probe caches the row it fetches, no `other` far end is probed more than once across all builds, and the probe budget's steady-state cost for a franchise the user revisits SHALL be zero.

The member limit SHALL be a safety ceiling against a runaway component rather than a working limit: it SHALL be set high enough that no real franchise reaches it, so that reaching it means the traversal has gone wrong and the truncation notice is meaningful.

A build that exhausts either its fetch budget or its probe budget SHALL mark the series it stores partial; a build that reaches the member cap SHALL mark it truncated. A partial series SHALL be rebuilt on the next visit, so successive visits — each starting from more cached data than the last — complete it without any background job. Because probes never repeat, a series left partial by an exhausted probe budget SHALL converge over successive visits rather than re-probing indefinitely.

A series stored as truncated SHALL NOT be rebuilt automatically on every visit, since a component genuinely over the cap would then re-traverse and spend fetch budget on every visit indefinitely. It SHALL pick up a raised cap on the next explicitly requested rebuild or the next staleness-triggered rebuild.

Concurrent builds of the same series SHALL collapse into one, matching the single-flight behaviour of the app's other visit-triggered refreshes.

#### Scenario: Large franchise is not truncated
- **WHEN** I open the series page for a franchise with more than 60 members, such as One Piece with its movies and specials
- **THEN** every member of its component is included, and the series is not marked truncated

#### Scenario: Build stops at the fetch budget
- **WHEN** I open the series page for a franchise with 20 members the app has never fetched
- **THEN** the page returns after at most 8 live MAL fetches, and the series is marked partial

#### Scenario: No budget is spent outside the component
- **WHEN** I open a series whose version neighbours hold many uncached entries of their own
- **THEN** every fetch is spent inside the seed's own component and the page renders complete

#### Scenario: Classifying neighbours costs nothing
- **WHEN** a series has six version neighbours with cached rows
- **THEN** all six are classified with no fetch and no probe

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

### Requirement: The More section's view state is restored with the page
The More section's four view controls — the "in my list" filter, the media-type filter, each group's collapsed state, and each group's exemption from the "in my list" filter — together with the **alternative picked for each version slot** SHALL be part of the series page's restorable state, restored on back/forward navigation exactly as every other page's view controls are, per the `page-state-restoration` capability.

Returning to a series page by back/forward navigation SHALL therefore show the page as it was left: a group opened in full is still open, a collapsed group is still collapsed, the media types I selected are still selected, the "in my list" control still reports the state it reported when the page was left, and the route I picked is still picked.

A fresh visit — a link, a typed URL, a reload — SHALL still open the section on its documented default: the "in my list" filter on, no media type selected, every group expanded, no group exempted, and every version slot on the default alternative the `series-versions` capability defines.

A group key held in restored state that matches no group the restored page renders — because the series' extras changed between the two renders — SHALL be ignored rather than treated as an error. A restored media type that no extra of the series carries SHALL likewise be ignored, as SHALL a restored pick naming an anime that is no longer an alternative of any slot.

The state SHALL NOT be persisted beyond the browser tab's application session, and SHALL NOT be shared between two different series' pages.

#### Scenario: An opened group is still open on return
- **WHEN** I open a More group in full, open one of its extras, and navigate back
- **THEN** that group is still showing all of its tiles, and the "in my list" control still reads as off

#### Scenario: The filter is not reset by a round trip
- **WHEN** I turn the "in my list" filter off, open an entry, and navigate back
- **THEN** the filter is still off and every extra is still shown

#### Scenario: Selected media types survive a round trip
- **WHEN** I select "Movie" and "OVA", open an entry, and navigate back
- **THEN** both are still selected and the section shows the same tiles it showed before

#### Scenario: A picked route survives a round trip
- **WHEN** I pick a route other than the default, open one of its entries, and navigate back
- **THEN** that route is still picked and the same main-line entries are shown

#### Scenario: A collapsed group is still collapsed on return
- **WHEN** I collapse a group, navigate away, and navigate back
- **THEN** that group renders no tiles

#### Scenario: A fresh visit still opens on the default
- **WHEN** I reach a series page by following a link rather than by navigating back
- **THEN** the "in my list" filter is on, no media type is selected, every group is expanded, no group is exempted, and every version slot is on its default alternative

#### Scenario: A stale pick is ignored
- **WHEN** restored state names a picked anime that the rebuilt series no longer holds as an alternative
- **THEN** the slot opens on its default rather than failing

#### Scenario: Two series do not share More-section state
- **WHEN** I open one series and expand its More section, then open a different series
- **THEN** the second series' More section opens on the default, unaffected by the first

### Requirement: Build all series from my list
The system SHALL provide an action on the Settings page that builds a series for every anime in my list that belongs to no stored series, and rebuilds every series that was built before the current main-line classification rules took effect, so the profile page's Top series ranking can be completed and corrected on demand rather than only filling in over time.

A target SHALL be considered covered only when it holds a **primary** membership in an up-to-date stored series, so an anime stored only as another series' version neighbour is still built into its own.

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

#### Scenario: One build covers a whole franchise
- **WHEN** a run builds a franchise and later reaches another anime of the same story component
- **THEN** that target is counted as processed without being built again

#### Scenario: A version-neighbour membership does not count as covered
- **WHEN** an anime in my list is stored only as another series' version neighbour and has story relations of its own
- **THEN** the run builds its own series rather than skipping it

#### Scenario: A folded-in neighbour is covered by its host
- **WHEN** an anime in my list has no story relations of its own and is already a version-neighbour extra of a stored series
- **THEN** it is counted as covered, since no series of its own can exist

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

### Requirement: A More tile's link target is decided by the series
Every tile in the More section SHALL carry, from the server, whether it opens a series page or an anime detail page, and the client SHALL use that answer rather than inferring one from the tile's relation group.

A tile SHALL open a **series page**, addressed by that anime's own id, exactly when the anime is a version neighbour that has story relations of its own — the case in which a series of its own exists or would be built. The series read endpoint builds a series for an anime that has none, so such a tile is never dead and needs no fallback target.

Every other tile — a story extra, a folded-in version neighbour with no story relations of its own, and every related entry — SHALL open that anime's **detail page**.

A tile SHALL NEVER navigate to the series page it is rendered on.

#### Scenario: Crossing to a separate telling
- **WHEN** I open the Fullmetal Alchemist (2003) series page and activate the Brotherhood tile in its Alternative version group
- **THEN** Brotherhood's series page opens

#### Scenario: A folded-in alternative opens its anime page
- **WHEN** I open the Clannad series page and activate the Clannad Movie tile in its Alternative version group
- **THEN** Clannad Movie's detail page opens

#### Scenario: A special grouped as an alternative setting opens its anime page
- **WHEN** I open the Clannad series page and activate the Clannad: After Story - Mou Hitotsu no Sekai, Kyou-hen tile in its Alternative setting group
- **THEN** that anime's detail page opens, rather than the series page I am already on

#### Scenario: An unstored telling is built by the visit
- **WHEN** the version neighbour shown has no stored series and I activate its tile
- **THEN** its series is built and its page renders, as it would on any first visit to a series

#### Scenario: Other groups are unaffected
- **WHEN** I activate a tile in the Side story group
- **THEN** that anime's detail page opens, as before

### Requirement: Series figures follow the picked route
Where a series' main line holds one or more version slots, the page's figures SHALL take two different member scopes.

The **score averages** — MAL's and mine, across the main line and across all entries, as "Series score averages" defines — SHALL be computed over **every** main-line entry, every alternative included, whatever is picked. They SHALL NOT change when the picker does, so the figure that describes the franchise stays stable.

Every other main-line figure "Series stats" defines — the main-line episode total, the main-line runtime total, episodes aired, my watched episodes and watched time, my rewatched time, entries completed, the lower-bound marker on an unknown episode count, whether the main line is settled by me, and the longest gap — SHALL be computed over the **trunk plus the picked alternatives and their branches**, so that time left describes the route I chose rather than counting every retelling of the same story.

Those figures SHALL be recomputed for each admissible combination of picks and delivered with the series, so switching a picker changes them without a further request and without the client re-deriving them. The number of combinations SHALL be capped; beyond the cap, slots after the first SHALL keep their default alternative's figures while the picker still changes which entries are shown.

The figures a series carries outside its own page — the browser card, the profile's Top series, and search — SHALL use the default combination.

A series whose main line has no version slot SHALL be unaffected: every figure covers its whole main line, exactly as before.

#### Scenario: Averages hold still across a switch
- **WHEN** I switch between two routes of a series
- **THEN** the MAL and my score averages are unchanged

#### Scenario: Totals follow the route
- **WHEN** I switch from a four-entry route to a three-entry route
- **THEN** the main-line episode total, the runtime total and the time left all change to describe the route now picked

#### Scenario: Time left describes one route
- **WHEN** a series holds four alternative retellings of the same story on its main line
- **THEN** its time left counts the picked route once, not all four

#### Scenario: Switching costs no request
- **WHEN** I switch the picked alternative
- **THEN** the figures update without another series request

#### Scenario: The card uses the default
- **WHEN** I look at that series' card in the series browser
- **THEN** its figures are those of the default combination

#### Scenario: A series without a slot is unchanged
- **WHEN** a series' main line holds no alternative versions
- **THEN** every figure covers its whole main line as before

## REMOVED Requirements

### Requirement: An alternative version's tile opens that version's series
**Reason**: The rule sent every Alternative version and Alternative setting tile to `/series/<animeId>` unconditionally, inferring the target from the tile's display group. A relation group says how MAL relates an anime, not whether a series of its own exists; on the live database it sends Clannad's Kyou-hen tile to the page it is rendered on. Replaced by "A More tile's link target is decided by the series", which carries the answer from the server.

**Migration**: None for stored data. Tiles that genuinely open a separate telling still do; tiles for a folded-in alternative version now open its detail page, which is where it belongs since no series of its own exists.

# series-page Specification

## Purpose
TBD - created by archiving change add-series-page. Update Purpose after archive.
## Requirements
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
2. Where it carries no relation to the main line at all, but does carry a version relation, in either direction, to any other member of this series, its group SHALL be that version relation's group — Alternative version ahead of Alternative setting where it carries both. An alternative version of a side story is an alternative version, and SHALL NOT fall to Other merely because the entry it retells is itself an extra.
3. Where it carries neither — an extra reached only through another extra — it SHALL inherit the group of the extra that reaches it, resolved breadth-first outward from the main line, ties broken by the lower MAL id. A special of a side story therefore reads as Side story. The walk SHALL travel relations other than the version relations, and SHALL be seeded from extras grouped by rule 1 alone: a **version neighbour** SHALL NOT pass its group on by inheritance and SHALL NOT be reached by it, and neither SHALL an extra grouped by rule 2 — so a story extra is never labelled an alternative version merely for sitting next to one.
4. Failing all three, its group SHALL be Other.

A version relation SHALL count for grouping under rules 1 and 2 even though it is never traversed. Both ends of such a relation are frequently members of one series — a recap movie trilogy retelling the TV run it sits beside, a theatrical cut of a side story — and the relation is what names the entry, whether or not the series traversal follows it. Grouping SHALL NOT depend on whether the entry it names was reached as a story-component member or as a version neighbour.

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
- **WHEN** a version neighbour carries both an `alternative_version` relation and a `sequel` relation to main-line members
- **THEN** it appears under "Alternative version"

#### Scenario: An extra of an extra inherits its group
- **WHEN** a special's only relation is to an extra that is grouped under "Side story"
- **THEN** that special is grouped under "Side story" too

#### Scenario: A version neighbour does not pass its group on
- **WHEN** an extra's only relation is to a version neighbour grouped under "Alternative setting"
- **THEN** that extra is grouped under "Other" rather than under "Alternative setting"

#### Scenario: A retelling inside its own series reads as an alternative version
- **WHEN** a movie that is a member of the series declares an `alternative_version` relation to a main-line member, a `sequel` relation to another extra, and a `side_story` relation to a third
- **THEN** it is grouped under "Alternative version", the version relation counting even though the traversal never followed it

#### Scenario: An alternative version of an extra is still an alternative version
- **WHEN** an entry's only relation to this series is an `alternative_version` to a member that is an extra rather than main line
- **THEN** it is grouped under "Alternative version" rather than under "Other"

#### Scenario: A relation to the main line outranks a version relation to an extra
- **WHEN** an extra declares a `side_story` relation to a main-line member and an `alternative_version` relation to another extra
- **THEN** it stays grouped under "Side story", and the extra it versions is grouped under "Alternative version"

#### Scenario: An alternative version of an extra does not pass its group on either
- **WHEN** a special's only relation is to an extra that was grouped under "Alternative version" by its version relation to another extra
- **THEN** that special is grouped under "Other" rather than under "Alternative version"

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
The system SHALL order main-line entries in **story order**: a topological ordering over the `sequel`/`prequel` edges among the main-line members, so an entry always precedes the entries it is a prequel to. The system SHALL present that ordering as the series' watch order, rendering the main-line cards in it. The page SHALL NOT print a position number on a card: the sequence of the cards already carries the order, and a number badge on every card only restates it.

Where the main line contains a **version slot** — main-line entries that are alternative versions of one another, per the `series-versions` capability — the ordering SHALL be computed with each slot contracted to a single position, so every alternative of a slot takes the same position and a branch entry is ordered around the slot rather than always after it. The entries actually shown for the picked alternative SHALL be rendered as one continuous sequence, with no gap left where an unpicked branch's entries would have been.

Aired-from date SHALL be a tie-break, not the ordering: where two entries are unconstrained relative to each other — neither reachable from the other over the chain — the earlier aired-from date SHALL come first, entries lacking a date SHALL be placed last, and MAL id SHALL break the remaining tie. An entry with no chain edge at all SHALL therefore be placed purely by its aired date, interleaved with the chain.

The main line is deliberately **story order, not release order**. A prequel film released after the season it precedes belongs before that season in a watch order, and ordering by air date puts it after — the chain edges the main line is already classified from state the correct order and were previously discarded at the ordering step.

Where the chain edges contain a cycle, so that no topological order exists — including a cycle introduced by contracting a version slot — the system SHALL break the cycle in favour of aired-from order and produce a stable ordering rather than failing the build. Extras remain ordered by aired-from date within their **relation group**, as specified under "Main line and extras".

The root SHALL be the first entry of the series' watch order, and the series SHALL take its title and its main picture from that root. The root SHALL NOT change with the picked alternative, so a series' header is stable however the watch order is filtered.

#### Scenario: A prequel film released later still sorts first
- **WHEN** a series contains Jujutsu Kaisen (aired 2020-10-03) and Jujutsu Kaisen 0 (aired 2021-12-24), and MAL states `40748 --prequel--> 48561` on both ends
- **THEN** Jujutsu Kaisen 0 is first in the watch order and Jujutsu Kaisen is second

#### Scenario: Chain order beats air date
- **WHEN** two main-line entries are linked by a `sequel`/`prequel` edge whose direction disagrees with their aired-from dates
- **THEN** the chain edge decides their order

#### Scenario: No position numbers on the cards
- **WHEN** I open any series
- **THEN** no main-line card carries a watch-order number, and the cards' left-to-right sequence is the watch order

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
- **THEN** the main-line cards are shown in that same order, left to right, and none of them carries a number

#### Scenario: A cyclic chain still renders
- **WHEN** MAL's relations put two main-line entries in a `sequel`/`prequel` cycle
- **THEN** the series still renders its main line, ordered by aired-from date where the cycle is broken

#### Scenario: Series picture and title come from the first entry
- **WHEN** I open a series whose earliest story-order main-line entry is its first season
- **THEN** the page's main picture and the series title are that first season's

#### Scenario: Two separate tellings have their own roots
- **WHEN** I open each of two series joined only by a version relation
- **THEN** each page shows its own first entry as its picture and title, not the other's

### Requirement: A series is identified by its root entry's MAL id
A series' identifier SHALL be the MyAnimeList id of its root entry — the earliest main-line member, the same entry whose title and picture the series falls back to. The identifier SHALL be derived from the stored series rather than assigned by the store, so that two installations that build the same franchise from the same relation data hold the same identifier for it without exchanging anything, and so that rebuilding a store from empty reproduces the identifiers it had.

The system SHALL hold exactly one identifier for a series. It SHALL NOT store a second identity alongside it, and the identifier a series is stored under, the identifier its membership records refer to, and the identifier its API responses carry SHALL be the same value.

Where the root entry changes — because a rebuild reveals an earlier main-line entry, or because a merge's surviving series takes a component rooted elsewhere — the identifier SHALL change with it. The series' chosen title, chosen picture and membership SHALL survive that change: what moves is the number, not the series.

A series' identifier and its root entry's anime identifier SHALL therefore be the same number, and a surface needing either SHALL read the one value.

#### Scenario: The identifier is the root's MAL id
- **WHEN** a series is built whose earliest main-line entry is the anime with MAL id 1735
- **THEN** the series is stored under identifier 1735, and its membership records refer to it by that identifier

#### Scenario: The same franchise gets the same identifier on another machine
- **WHEN** the same franchise is built from an empty store on a second installation
- **THEN** it is identified by the same number, with nothing exchanged between the two

#### Scenario: A rebuild from empty reproduces the identifiers
- **WHEN** every stored series is deleted and rebuilt from the same relation data
- **THEN** each series is identified by the number it had before

#### Scenario: An older entry moves the identifier
- **WHEN** a rebuild finds a main-line entry earlier than the current root
- **THEN** the series is identified by that entry's MAL id from then on, and keeps its members, its chosen title and its chosen picture

#### Scenario: One identifier, not two
- **WHEN** a series is read through any surface
- **THEN** it reports a single identifier, which is its root entry's MAL id

### Requirement: Series persistence and identity
The system SHALL persist each derived series under the identifier its root entry's MAL id gives it, together with its member set, recording for each member whether it is main line, its position within its list, its relation group, whether it is a story-component member or a version neighbour, whether that series is the member's primary one, and — for a main-line entry — its version slot and its branch, plus the time the series was built.

A member SHALL be recorded per series rather than per anime, so an anime that is a version neighbour of two series holds one record in each. Exactly one of an anime's records SHALL be marked primary, as the `series-versions` capability defines.

A build SHALL persist the seed's series alone. Its component SHALL be matched to the stored series it overlaps most, computing overlap over **story-component members only**; that series keeps its members, its chosen title and its chosen picture, and takes the identifier its root implies — which is the identifier it already had unless the rebuild moved its root. Every other stored series overlapping the component is deleted after surrendering any chosen title or picture the survivor lacks. A newly announced entry therefore folds into the existing series rather than creating a competing one, and the fragments left by an earlier rules change collapse back into one series rather than persisting.

Where a build's surviving series must take an identifier a stored series still holds, that stored series SHALL be one this same build deletes, and the deletion and the re-identification SHALL take effect together: a build SHALL NOT leave a franchise with no stored series, and SHALL NOT leave two series claiming one identifier.

The system SHALL NOT store the series' score averages, computing them at read time instead, so editing a score never leaves a stale average behind.

#### Scenario: Identity survives a rebuild
- **WHEN** a series is rebuilt after a new sequel is announced
- **THEN** the series keeps its members, its choices and the identifier its unchanged root gives it, now with the new entry as a member

#### Scenario: A rebuild that re-roots keeps everything but the number
- **WHEN** a rebuild adds a main-line entry earlier than the series' current root
- **THEN** the series is stored under the new root's MAL id, still holding every member and its chosen title and picture, and no series remains under the old identifier

#### Scenario: Two stored series absorbed into one
- **WHEN** a build's component covers the members of two separately stored series
- **THEN** one series remains, holding every member, and the other stored series is deleted

#### Scenario: A merge takes the identifier of the component's root
- **WHEN** a build's component covers two stored series and its root belongs to the one with the smaller overlap
- **THEN** the larger-overlap series survives with its choices, is stored under that root's MAL id, and the other is deleted — with no moment in which both hold that identifier

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

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL take one of four values, chosen by this precedence:

1. `Airing` — a **main-line** entry of the series is currently airing.
2. `Ongoing` — no main-line entry is currently airing, and some member of the series is currently airing.
3. `Upcoming` — no member has finished airing and at least one has not yet aired.
4. `Ongoing` — some member has not yet aired.
5. `Finished` — otherwise.

`Airing` SHALL therefore be reserved for a series with something of its main line on the air right now, and `Ongoing` for a series with nothing of its main line on the air but something still to come — either an announced, not-yet-aired member, or a member outside the main line that is currently airing. A series whose main line has finished but whose OVA or special is currently broadcasting SHALL read `Ongoing`, not `Airing` and not `Finished`.

`Finished` SHALL continue to be reserved for a series with nothing left to come: a series whose aired members have all finished but which has an announced, not-yet-aired member SHALL read `Ongoing`, not `Finished`. The pill SHALL have no `Finished · sequel upcoming` state.

The four values SHALL be visually distinguishable from one another, each carrying its own colour rather than two of them sharing one. `Airing` SHALL carry the same colour this page already uses to mark an entry as on the air now, so the header pill and the timeline's on-air marking agree.

The progress bar and progress readout beneath the header SHALL treat `Airing` exactly as they treat `Ongoing`: both values SHALL select the broadcast progress bar and show the aired-episode figure, since both describe a series that is still running.

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked **Completed or Rewatching** in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past — some later aired main-line entry has any watched episodes — does not count; the badge describes where I stand today, not history.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

A main-line entry marked **Rewatching** SHALL count as **fully watched** throughout this precedence — as the greater of its own episodes-watched figure and its aired-episode figure, rather than as its current in-progress count. Entering Rewatching resets episodes-watched to zero, so without this rule a franchise I have seen in full and am part-way through watching again would read `N behind`, `Unwatched`, or `Dropped` on the strength of a reset counter. `Rewatching` also satisfies rule (1) alongside `Completed`, so starting a rewatch of a finished franchise SHALL NOT downgrade its badge from `Completed`.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count — only watch status and watched-episode counts — so a currently-airing entry's unknown broadcast count SHALL NOT block them; it SHALL only be able to produce no badge once evaluation reaches rules (3)/(4). An entry that has not aired at all SHALL NOT count toward any of these figures, and an entry that is not in my list SHALL count as zero episodes watched.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Caught up` SHALL keep the colour that already marks an entry as on the air now. `N behind` SHALL keep its existing warning colour. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. All five SHALL remain visually distinct from one another and from the status pill's own four colours.

The status pill and the personal badge SHALL each render at a consistent, uniform size regardless of their label's length, so the two sit beside each other as evenly sized controls rather than ragged text of varying width.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all, so rule (1) (which requires at least one finished-airing main-line entry) never applies to it; it falls through to rules (2)–(5) exactly as a multi-entry franchise would, and can still show `Caught up` or `N behind` against its own currently-airing broadcast count.

#### Scenario: A series with a season on the air
- **WHEN** I open a series whose latest main-line season is currently airing
- **THEN** the pill reads "Airing"

#### Scenario: Only an extra is on the air
- **WHEN** every main-line entry of a series has finished airing and one of its OVAs is currently airing
- **THEN** the pill reads "Ongoing", not "Airing" and not "Finished"

#### Scenario: A series with an announced sequel is ongoing
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Ongoing"

#### Scenario: A main-line season on the air outranks an announced sequel
- **WHEN** one main-line season of a series is currently airing and a further season has been announced but has not aired
- **THEN** the pill reads "Airing"

#### Scenario: A first season airing before anything has finished
- **WHEN** a series' only aired member is a main-line entry that is currently airing, and no member has finished airing
- **THEN** the pill reads "Airing", not "Upcoming"

#### Scenario: Finished means nothing is left to come
- **WHEN** every member of a series has finished airing and no member is unaired
- **THEN** the pill reads "Finished"

#### Scenario: Nothing has aired yet
- **WHEN** no member of a series has finished airing, none is currently airing, and at least one has not yet aired
- **THEN** the pill reads "Upcoming"

#### Scenario: Airing and Ongoing are told apart at a glance
- **WHEN** I compare a series reading "Airing" with one reading "Ongoing"
- **THEN** the two pills carry different colours, and the "Airing" pill carries the same colour the page's timeline uses to mark an entry as on the air now

#### Scenario: An airing series keeps the broadcast progress bar
- **WHEN** I open a series whose pill reads "Airing"
- **THEN** the header's progress bar shows broadcast progress behind my watched progress and the readout states the aired-episode figure, exactly as it does for an "Ongoing" series

#### Scenario: Year span
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

#### Scenario: Completed series is badged
- **WHEN** I have marked every main-line entry of a fully finished series as Completed
- **THEN** the header shows a "Completed" badge next to the status pill, in the app's Completed-status colour

#### Scenario: Rewatching a finished franchise does not lose the Completed badge
- **WHEN** every member of a series has finished airing, I have completed every main-line entry, and I then mark its first season Rewatching with two episodes watched
- **THEN** the header still shows "Completed", not "Caught up" and not a behind count

#### Scenario: A rewatch in progress does not read as behind
- **WHEN** a series' three finished main-line seasons total 36 broadcast episodes, I have completed the second and third, and the first is marked Rewatching with two of its twelve episodes watched
- **THEN** the header does not show "10 behind"; the rewatching season counts as fully watched

#### Scenario: A rewatch in progress does not read as unwatched
- **WHEN** a series' only main-line entry has finished airing and is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Unwatched"

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

#### Scenario: A partially watched finished entry shows a behind count, not silence
- **WHEN** a series' only main-line entry has finished airing with 12 episodes, I have watched 5, and I have neither completed nor dropped it
- **THEN** the header shows a "7 behind" badge — the same behind-count treatment a currently-airing entry gets, not "no badge"

#### Scenario: A dropped entry with nothing watched after it
- **WHEN** I marked one main-line entry Dropped and have never watched any main-line entry released after it
- **THEN** the header shows a "Dropped" badge, in the app's Dropped-status colour

#### Scenario: A dropped entry I later resumed
- **WHEN** I marked an early main-line entry Dropped but have since watched episodes of a later main-line entry
- **THEN** the header does not show "Dropped"; it shows whatever "Caught up"/"N behind" the combined watched-versus-aired figures produce

#### Scenario: A rewatch after a drop counts as watching past it
- **WHEN** I marked an early main-line entry Dropped and a later main-line entry is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Dropped", because the rewatching entry counts as watched

#### Scenario: Nothing watched at all shows Unwatched
- **WHEN** at least one main-line entry has finished or is currently airing and I have watched none of the main line, with no entry marked Dropped
- **THEN** the header shows an "Unwatched" badge, in the app's Plan-to-watch colour, rather than no badge

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge, but only once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far, and the series is not already "Dropped" or "Unwatched" by those rules
- **THEN** no personal badge is shown, rather than "Caught up"

#### Scenario: The status pill and personal badge are evenly sized
- **WHEN** I compare a series showing "Airing" and "3 behind" against one showing "Finished" and "Completed"
- **THEN** both pills and both badges render at the same consistent size, regardless of how much shorter or longer their labels are

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

Wherever this page counts **my watched main-line episodes** — the progress bar's watched fill, the named watched figure beside it, and therefore time left — a main-line entry marked **Rewatching** SHALL count as fully watched, as the greater of its own episodes-watched figure and its aired-episode figure. This is the same rule the header's personal badge applies, so the badge and the progress figures directly beside it can never disagree about the same entry. It governs only the watched side of each pair: the episode total, the aired figure, and the runtime describe the anime rather than me, and SHALL be unchanged by a rewatch in progress. It does not multiply anything by a rewatch count: a rewatched entry still counts **once** in these figures. Time watched is defined separately below and does count rewatch runs; time left continues to subtract only these once-counted figures, so a rewatch in progress can never drive it negative or make a half-watched franchise read as finished.

The entries-completed stat SHALL cover the extras as well as the main line, as two separately labelled figures within one stat: how many main-line entries I have completed out of the main-line total, and how many extras I have completed out of the extras total. The two SHALL NOT be summed into a single figure, so which half of the series is unfinished stays visible. When the series has no extras, the extras figure SHALL be omitted and the stat SHALL show the main-line figure alone rather than an "0 of 0". An entry marked Rewatching SHALL count as completed in this stat, since a rewatch can only follow a completed run.

**Time watched SHALL count rewatches.** It SHALL be the main line's once-counted watched time — the figure the paragraph above governs — plus the time spent rewatching that main line: for each main-line entry, one further complete run of it for every recorded rewatch, plus the episodes watched so far in a run still in progress. This is the definition the profile page's watch time already uses, so the two pages SHALL NOT describe the same hours differently. A twelve-episode season I have completed, rewatched twice, and am two episodes into rewatching a third time SHALL therefore contribute 38 episodes of time watched, not 12.

Rewatch time SHALL be counted over the **main line only**, matching every other figure in this stat box and matching what time left subtracts. Time watched MAY exceed the main line's runtime as a result, which is correct rather than an error; time left SHALL NOT be derived from it.

The two time figures SHALL be presented as **one stat**, in the two-labelled-rows shape the entries-completed stat already uses: a single stat titled **Time**, holding a row labelled `Watched:` and a row labelled `Left:`. They SHALL NOT be rendered as two separate stat cells, since they are two halves of one question about the same series.

Each row SHALL keep its own rule for whether it appears, unchanged. The **Watched** row SHALL appear whenever time watched is non-zero, including on a series with nothing left to watch — a finished franchise that has been rewatched is precisely the case where the figure says something the entries-completed and progress figures do not. The **Left** row SHALL appear only while there is time left: when it computes to zero — I have watched at least as much of the main line as its runtime accounts for — it SHALL NOT be rendered, since "0min left" restates what the badge and the progress bar already say. A Time stat holding one qualifying row SHALL show that row alone rather than an empty or zeroed counterpart; when neither row qualifies, the Time stat SHALL NOT be rendered at all.

That withholding of the Left row SHALL NOT apply when the main-line runtime is itself unknown — a zero runtime total that the page already marks as unknown rather than as an exact figure. A zero time left derived from a runtime nobody knows reports missing data, not a series I have finished, so the Left row SHALL still be shown in that case.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. An entry whose total episode count is unknown SHALL still contribute its known aired-so-far episode count toward that lower bound, rather than contributing nothing, whenever an aired count is known for it — so a still-airing entry with no announced total makes the lower bound tighter instead of forcing the whole stat to read as wholly unknown. Time left SHALL be derived from once-counted watched episodes only and SHALL NOT multiply by rewatch count; time watched SHALL count rewatch runs exactly as defined above.

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
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, and one Time stat carrying my watched and left figures

#### Scenario: A rewatching entry keeps the progress bar full
- **WHEN** a series' 62 main-line episodes are all watched and one 12-episode season is marked Rewatching with two episodes watched
- **THEN** the progress bar still reads 62 of 62 watched, matching the "Completed" badge beside it, rather than dropping to 52

#### Scenario: A rewatching entry counts as a completed entry
- **WHEN** I have completed five of a series' six main-line entries and marked the sixth Rewatching
- **THEN** the entries-completed stat reads "6 of 6"

#### Scenario: The episode total is unmoved by a rewatch
- **WHEN** a main-line entry is marked Rewatching
- **THEN** the main-line episode total, the aired figure, and the runtime are exactly what they were before the rewatch began

#### Scenario: Entries completed covers extras too
- **WHEN** I open a series where I have completed 5 of 6 main-line entries and 2 of its 3 extras
- **THEN** the entries-completed stat shows "5 of 6" for the main line and "2 of 3" for the extras as two labelled figures, rather than one combined "7 of 9"

#### Scenario: A series with no extras shows one figure
- **WHEN** I open a series whose every member is main line
- **THEN** the entries-completed stat shows only the main-line figure, with no extras figure beside it

#### Scenario: The two time figures share one stat
- **WHEN** I open a part-watched series
- **THEN** the stats box holds one "Time" stat with a `Watched:` row and a `Left:` row inside it, rather than two separate "Time watched" and "Time left" stats

#### Scenario: A finished series keeps the Time stat with only its watched row
- **WHEN** I open a series whose main line I have watched in full, so no time is left
- **THEN** the Time stat is shown with its `Watched:` row alone, and no `Left:` row

#### Scenario: A completed rewatch adds to time watched
- **WHEN** a series' main line totals 62 episodes averaging 24 minutes, I have completed all of it, and one 12-episode season carries a rewatch count of 2
- **THEN** the Time stat's watched row covers 86 episodes rather than 62, and no left row is shown

#### Scenario: A rewatch in progress counts the episodes watched so far
- **WHEN** a 12-episode season I completed is marked Rewatching with two episodes watched and a rewatch count of 2
- **THEN** that entry contributes 38 episodes to time watched — its original run, its two completed rewatches, and the two episodes of the run in progress

#### Scenario: A rewatch does not move time left
- **WHEN** I have watched 38 of a series' 62 main-line episodes and then rewatch a completed 12-episode season twice
- **THEN** the Time stat's left row is exactly what it was before the rewatches, while its watched row has grown by those 24 episodes

#### Scenario: Extras' rewatches stay out of the figure
- **WHEN** an extra (not a main-line member) carries a rewatch count of 3
- **THEN** the series' time watched is unchanged by it

#### Scenario: A part-watched series keeps both time rows
- **WHEN** I open a series with main-line episodes I have not yet watched
- **THEN** the Time stat shows both its `Watched:` and its `Left:` row

#### Scenario: An unknown runtime is not mistaken for a finished series
- **WHEN** I open a series whose main-line runtime total is unknown, so it reports zero time left without my having watched it through
- **THEN** the Time stat still shows both rows, because the zero reflects a runtime nobody knows rather than a series I have finished

#### Scenario: A series I have not started shows no watched row
- **WHEN** I open a series with no main-line entry in my list
- **THEN** the Time stat shows its `Left:` row alone, carrying the whole main-line runtime, with no `Watched:` row

#### Scenario: Neither row qualifies
- **WHEN** a series has nothing watched and an exactly-known main-line runtime of zero
- **THEN** no Time stat is rendered at all

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

### Requirement: Series figures follow the picked route
Where a series' main line holds one or more version slots, the page's figures SHALL take two different member scopes.

The **score averages** — MAL's and mine, across the main line and across all entries, as "Series score averages" defines — SHALL be computed over **every** main-line entry, every alternative included, whatever is picked. They SHALL NOT change when the picker does, so the figure that describes the franchise stays stable.

Every other main-line figure "Series stats" defines — the main-line episode total, the main-line runtime total, episodes aired, my watched episodes and watched time, my rewatched time, entries completed, the lower-bound marker on an unknown episode count, whether the main line is settled by me, and the longest gap — SHALL be computed over the **trunk plus the picked alternatives and their branches**, so that time left describes the route I chose rather than counting every retelling of the same story.

Those figures SHALL be delivered with the series for each admissible combination of picks, so switching a picker changes them without a further request. The number of combinations SHALL be capped; beyond the cap, slots after the first SHALL keep their default alternative's figures while the picker still changes which entries are shown.

The delivered figures describe the series as the server last read it. Where an edit made on the page has since changed one of them, the page SHALL show the edited value on **every** route rather than the delivered one, per "The stats box stays live after an in-place edit".

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

#### Scenario: An edited figure survives a switch
- **WHEN** I edit a member's status and then switch the picked alternative
- **THEN** the route's figures account for that edit rather than reverting to the values delivered with the series

#### Scenario: The card uses the default
- **WHEN** I look at that series' card in the series browser
- **THEN** its figures are those of the default combination

#### Scenario: A series without a slot is unchanged
- **WHEN** a series' main line holds no alternative versions
- **THEN** every figure covers its whole main line as before

### Requirement: Favourite ordering within a series
When several entries tie for my highest score in a series, the page SHALL let me order them by hand, so that which of them is really my favourite is recorded rather than decided by watch order.

The order SHALL be the same order the `anime-ranking` capability's ranking holds for that score: entries tied for my highest score in this series SHALL be listed in ascending rank order, exactly as the ranking editor would arrange them. This order is therefore not stored per series and is not scoped to series membership — it SHALL survive a series rebuild and a route switch untouched, and it SHALL NOT be discarded by anything this capability does. Reordering it here SHALL be reflected in the ranking editor and in every other ordering the `anime-ranking` capability's "Every by-my-score ordering reads the ranking" requirement lists, and a reorder made there SHALL likewise be reflected here.

Reordering SHALL be offered only when two or more entries are tied. An entry with no rank in the ranking (never hand-ordered) SHALL rank after every ranked entry of the tie.

Moving one tied entry past its neighbour SHALL reposition that entry to sit immediately beside the neighbour it was moved past, within the ranking's shared score tier — not merely trade rank numbers with it while leaving every anime between them, of this series or any other, undisturbed. The tie itself SHALL be re-derived as my scores change: scoring a further entry to the current top score SHALL add it to the list, placed by its own rank among the others, without a reload; raising one entry above the tie SHALL leave that entry listed alone.

Reordering SHALL be offered, SHALL take effect, and SHALL persist on whichever route of the series is picked, not only on the default one.

An ordering that fails to save SHALL leave the page showing the order that is actually stored, rather than a local order the server does not have.

#### Scenario: Reordering tied favourites
- **WHEN** three entries tie for my highest score and I move the third to the top
- **THEN** it is listed first as my favourite, and it is still listed first when I reload the page

#### Scenario: A distant favourite is pulled adjacent, not swapped in place
- **WHEN** two tied favourites sit far apart in the ranking's shared score tier, with other anime between them, and I move the lower one above the higher one
- **THEN** it is repositioned to sit immediately beside the other in the ranking, rather than trading rank numbers with it and leaving the anime between them where they were

#### Scenario: Favourite order survives a rebuild
- **WHEN** I have ordered my tied favourites and then use the Rebuild control
- **THEN** the entries that are still members keep the order I gave them

#### Scenario: No reordering without a tie
- **WHEN** one entry alone holds my highest score in a series
- **THEN** no reorder controls are shown

#### Scenario: A new tie appears without a reload
- **WHEN** one entry holds my highest score and I give a second entry the same score
- **THEN** both are listed as my favourite, ordered by their rank, with reorder controls, without reloading the page

#### Scenario: A newly tied entry is placed by its own rank
- **WHEN** two tied favourites are already ordered and a third entry joins the tie
- **THEN** the third is inserted among the other two according to its own rank in the ranking, rather than simply appended after them

#### Scenario: Reordering while a non-default route is picked
- **WHEN** I pick a route other than the default and reorder my tied favourites
- **THEN** the new order is shown immediately and survives a reload

#### Scenario: A reorder here moves the same ranking everywhere else
- **WHEN** I reorder tied favourites from a series page
- **THEN** the same new order is reflected in the ranking editor and in every other place ordered by my score

#### Scenario: A failed save does not stick
- **WHEN** I reorder my favourites and the save fails
- **THEN** the page returns to the previously stored order

### Requirement: The stats box stays live after an in-place edit
Every figure the series page derives from its members SHALL describe the page's **current** member data, without a reload and on whichever route is picked.

When an entry editor save from a timeline card or a More tile changes a member's score, status, episodes watched, or rewatch count, the page SHALL recompute, for the picked route: the four score averages, my highest-scored entries, the most-rewatched entries, both entries-completed figures, time watched, time left, the progress bar and its named watched figure, and the header's personal badge. It SHALL NOT keep showing the figures the server last delivered for any of them.

Switching the picked route SHALL NOT discard an edit's effect. The figures for the newly picked route SHALL be derived from the same current member data, so no figure reverts to its pre-edit value by switching away from a route and back to it.

Reordering my tied favourites SHALL take effect on whichever route is picked, not only on the default one.

The recomputed figures SHALL cover exactly the members the server's own figures cover — the main-line entries the picked route shows, and the series' real extras. **Related entries** — anime shown in More because a main-line member relates to them, without being members of the series — SHALL NOT enter any average or any stat, exactly as they do not on the server. An edit SHALL therefore never move a figure onto a different member basis than the one it was first delivered on.

Figures no edit can change — the studios and genres the series spans, the longest gap, the main-line and extras episode and runtime totals, the aired-episode figures, and the member counts — MAY continue to be taken from the figures delivered for the picked route.

#### Scenario: Scoring a second entry to the same top score lists both as my favourite
- **WHEN** one entry holds my highest score of 9, and I score a second entry 9 from its card
- **THEN** both are listed as my favourite immediately, with reorder controls, without reloading the page

#### Scenario: Raising one entry above a tie narrows the list
- **WHEN** three entries tie for my highest score and I raise one of them to 10
- **THEN** only that entry is listed as my favourite

#### Scenario: An edit on a non-default route updates the stats box
- **WHEN** I switch to a non-default route and then mark one of its entries Completed with a score
- **THEN** my favourite, entries completed, time watched, time left and the progress bar all update, exactly as they would on the default route

#### Scenario: Reordering favourites on a non-default route
- **WHEN** I switch to a non-default route and move a tied favourite to the top
- **THEN** it is listed first, and it is still first after the save

#### Scenario: Switching routes does not resurrect a pre-edit figure
- **WHEN** I edit an entry's score, switch to another route, and switch back
- **THEN** every figure still reflects the edit rather than the value the server last delivered

#### Scenario: Recording a rewatch updates the rewatch and time figures
- **WHEN** I set a main-line entry's rewatch count to 1 from its card
- **THEN** the "Most rewatched" stat names that entry with a count of 1 and time watched grows by one run of it, without a reload

#### Scenario: Completing an entry updates entries completed
- **WHEN** I mark the last unfinished main-line entry Completed
- **THEN** the entries-completed stat reads the full main-line count and the header badge reads "Completed"

#### Scenario: Related entries stay out of the recomputed averages
- **WHEN** a series' More section shows related entries alongside its real extras and I edit one of the real extras' scores
- **THEN** the "Everything" averages are recomputed over the main line and the real extras only, on the same member basis the server delivered them

### Requirement: A More group's heading opens that group in full

Each More group's heading SHALL be the control that opens that group, with collapse as its off state. Activating a heading SHALL show **every** extra in that group — including the extras not in my list — unless the group is already showing every one of them, in which case it SHALL collapse the group so that none of its tiles is rendered.

Opening a group this way SHALL exempt that group, and only that group, from the "in my list" filter: every other group SHALL keep showing exactly what it was showing. An exempted group SHALL stay exempt until the filter is turned back on.

While the "in my list" filter is off, a group has nothing to be exempted from, so a heading SHALL simply expand and collapse its group.

The "in my list" control SHALL report itself as **on** only while the filter is in force across every group — that is, while it is on and no group has been opened in full. Opening any group in full SHALL therefore make that control read as off, so the section never reports itself as filtered while showing a group whole.

The "in my list" control SHALL govern **what an expanded group shows**, and SHALL NOT change any group's collapsed state in either direction. Activating it while it reads as off SHALL turn the filter on and drop every group's exemption, so every expanded group returns to showing only the extras in my list. Activating it while it reads as on SHALL show every extra of every expanded group. Because the section opens with its groups collapsed, coupling this control to collapse would make turning the filter on hide everything; the expand/collapse-all control is the one that changes collapse.

#### Scenario: Opening a group that holds nothing of mine

- **WHEN** the filter is on, a group holds seven extras of which none is in my list, and I activate that group's heading
- **THEN** all seven of its tiles are shown, every other group keeps showing what it was showing, and the "in my list" control now reads as off

#### Scenario: The heading collapses a group it has opened

- **WHEN** I activate the heading of a group that is showing all of its extras
- **THEN** that group renders no tiles at all

#### Scenario: Opening a group that holds some of mine

- **WHEN** the filter is on, an expanded group holds six extras of which three are in my list, and I activate that group's heading
- **THEN** all six of its tiles are shown, rather than the three the filter was showing

#### Scenario: Opening one group leaves the others alone

- **WHEN** I open one group in full while the filter is on
- **THEN** every other group is unchanged — an expanded one still shows only the extras in my list with its own hidden-count control if it has one, and a collapsed one is still collapsed

#### Scenario: Turning the filter back on re-filters without collapsing

- **WHEN** a group has been opened in full and I activate the "in my list" control, which reads as off
- **THEN** every group's exemption is dropped and every expanded group shows only the extras in my list, no group's collapsed state has changed, and the control reads as on again

#### Scenario: Turning the filter off does not collapse anything

- **WHEN** the filter reads as on, some groups are expanded and some are collapsed, and I activate the "in my list" control
- **THEN** each expanded group now shows every one of its extras, each collapsed group is still collapsed, and the control reads as off

#### Scenario: The heading is a plain toggle while the filter is off

- **WHEN** the "in my list" filter is off and I activate a group's heading twice
- **THEN** that group collapses and then shows all of its extras again

#### Scenario: A collapsed group offers no hidden-count control

- **WHEN** a group is collapsed
- **THEN** it shows its heading and entry count alone, with no control naming how many tiles are hidden

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by their relation to the main line, so the extras read as a different kind of thing from the chronological main line and each group states how its entries stand to the franchise.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned. Because groups no longer share a media type, each tile's media type SHALL be legible on the tile itself.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading, counting the entries the media-type filter currently admits.

The More section SHALL offer three section-wide controls: an "in my list" control, an expand/collapse-all control, and the media-type filter buttons its own requirement defines. Which extras are visible SHALL be governed by those controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

**Every group SHALL render collapsed when a series page is opened.** The section SHALL therefore open as a column of relation-group headings, each carrying its count, with no tiles rendered at all — a franchise with a dozen relation groups is not made to fill the page before the reader has asked for any of it. A group opens from its own heading, from the expand/collapse-all control, from the "in my list" control, or by selecting a media type it holds, per the media-type filter requirement.

The "in my list" filter SHALL be on when a series page is opened. While it is on, an expanded group SHALL show only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — hiding every extra that is not.

**Activating the "in my list" control SHALL show my entries.** It SHALL turn the filter on, drop every per-group exemption from it, and open every group holding at least one extra of mine that the media-type filter admits, so that what the section shows afterwards is exactly my own extras — narrowed to the selected media types when any are selected, and across every group when none are. A group holding none of mine SHALL be left collapsed, so the section is not padded with headings that would show nothing. When no group holds an extra of mine at all, every group SHALL stay collapsed.

Activating the control again SHALL collapse every group, returning the section to its headings alone with the filter still on — the same "show it / put it away" pair the expand/collapse-all control offers for everything.

The control SHALL report which of those two states the section is in: it SHALL read as on only while the filter is in force, no group is exempt from it, and at least one group is open. A freshly opened series page — filter on, every group collapsed — SHALL therefore read as off, so the first press does something visible rather than nothing; and opening one group in full from its heading SHALL make it read off, per "A More group's heading opens that group in full".

Turning the filter **off** is done by the expand/collapse-all control, which shows everything, or by a group's heading, which exempts that one group; the "in my list" control itself never turns the filter off.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, expanding them all and turning the filter off. It SHALL therefore read "Expand" on a freshly opened series page, whose groups are all collapsed. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own from its heading, per "A More group's heading opens that group in full", and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group that is showing at least one tile while the filter hides the rest SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows. A group showing no tiles at all — because it is collapsed, or because nothing in it is in my list — SHALL NOT offer that control: its heading opens it, and its entry count is already in the heading.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More by relation
- **WHEN** a series has two recap specials, one side-story OVA, and one alternative version
- **THEN** the More section shows a collapsible "Summary", "Side story" and "Alternative version" group, each heading carrying its count, and their poster tiles once opened

#### Scenario: Media type is legible on the tile
- **WHEN** one "Side story" group holds an OVA, a movie and a special
- **THEN** each tile states its own media type

#### Scenario: A tile's episode count is not truncated away
- **WHEN** a portrait-pictured extra is a `TV special` that aired in 2003 and has 99 episodes
- **THEN** its tile shows `TV special · 2003 · 99 ep` in full at the grid's narrowest column width

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are longer still than that worst case
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: Opening a series shows the headings alone
- **WHEN** I open a series with twenty extras across five relation groups, four of the extras being in my list
- **THEN** all five groups are collapsed with their counts in their headings, no tile is rendered, the all-groups control reads "Expand all", and the "in my list" control reads as off

#### Scenario: Pressing "in my list" shows my extras
- **WHEN** I then activate the "in my list" control
- **THEN** every group holding at least one of those four extras opens showing exactly those, the groups holding none of mine stay collapsed, and the control reads as on

#### Scenario: Pressing it again puts them away
- **WHEN** the section is showing my extras and I activate the "in my list" control again
- **THEN** every group collapses, no tile is rendered, and the control reads as off

#### Scenario: With a media type selected, only that type of mine is shown
- **WHEN** I select "Movie" and then activate the "in my list" control
- **THEN** the groups holding a movie of mine open showing only those movies, and no entry of another type and no movie that is not in my list is shown

#### Scenario: Nothing of mine anywhere
- **WHEN** none of a series' extras is in my list and I activate the "in my list" control
- **THEN** every group stays collapsed and no tile is rendered

#### Scenario: Opening one group shows every extra in it
- **WHEN** I activate the heading of one collapsed group
- **THEN** that group shows every one of its extras and reads as exempt from the filter, while the other groups keep the state they were in

#### Scenario: Expanding shows everything
- **WHEN** I activate the all-groups control on a freshly opened series
- **THEN** all twenty extras are shown, the "in my list" control reads as off, and the control now reads "Collapse all"

#### Scenario: Collapsing hides my own extras too
- **WHEN** every extra is shown and I activate the "Collapse all" control
- **THEN** no tiles are rendered in any group, including the extras in my list, and the control reads "Expand all" again

#### Scenario: Filtering back to my list
- **WHEN** every extra is shown and I activate the "in my list" control
- **THEN** each group holding an extra of mine shows only those extras, a group holding none of mine is collapsed, and the all-groups control reads "Expand all"

#### Scenario: A group with nothing of mine in it
- **WHEN** the filter is on, a group is expanded, and it holds no extras that are in my list
- **THEN** that group shows its heading and entry count with no tiles and no hidden-count control, and its heading opens it in full

#### Scenario: Revealing one group's hidden extras
- **WHEN** the filter is on, an expanded group is showing the extras of mine it holds while hiding others, and I activate that group's hidden-count control
- **THEN** that group shows all of its tiles while every other group keeps showing only my own extras

#### Scenario: Controls are offered whatever my statuses
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** the "in my list" control and the all-groups control are both still offered, and expanding then collapsing hides those completed extras

#### Scenario: A large More section is not treated differently
- **WHEN** I open a series with twenty extras and one with four extras
- **THEN** both open with every group collapsed and the filter on, rather than one of them starting expanded

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
- **WHEN** a series has extras in only one relation group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more relation groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

### Requirement: The More section offers media-type filter buttons
Above the More section the page SHALL offer one button per media type present among that series' extras and related entries — TV, Movie, OVA, ONA, Special, Music, PV, and any other type those entries carry — as a multi-select set.

Selecting a type SHALL narrow every group to the entries of that type. Selecting several SHALL show the entries of any selected type. Selecting none SHALL narrow nothing, which is the state a freshly opened page is in.

**Selecting a type SHALL open every group that holds at least one entry of that type**, so the entries it admits are actually rendered rather than merely counted in a heading. Because the section opens with every group collapsed, a type filter that only narrowed the groups would tell the reader which relation group holds a music entry without ever showing the entry itself. A group opened this way SHALL be opened exactly as the expand/collapse-all control opens one: its collapsed state becomes expanded and stays that way until something collapses it. It SHALL NOT be exempted from the "in my list" filter — only a group heading grants that exemption. Deselecting a type SHALL NOT collapse anything.

The type filter SHALL compose with the "in my list" filter and with each group's collapsed or opened state rather than replacing them: an entry is shown when its type is admitted **and** the other controls admit it. Each group's heading count SHALL report the entries the type filter admits.

While at least one type is selected, a group left with no admitted entries SHALL NOT be rendered at all, since a column of empty headings across a dozen relation groups tells the reader nothing.

The type buttons SHALL NOT narrow the main-line timeline. The main line is a watch order whose left-to-right sequence is its meaning, and hiding one of its entries would misstate the series.

#### Scenario: One type narrows every group
- **WHEN** I select "Movie"
- **THEN** every group holding a movie is opened and shows its movies, and groups holding no movie are not rendered

#### Scenario: Selecting a type opens the collapsed groups holding it
- **WHEN** every group is collapsed, as on a freshly opened series page, and I select "Music"
- **THEN** every group holding a music entry is expanded and renders the music entries the other controls admit, rather than showing its heading alone

#### Scenario: Several types are additive
- **WHEN** I select "Movie" and then "OVA"
- **THEN** every group shows its movies and its OVAs, and the groups holding an OVA are opened as well

#### Scenario: Deselecting the last type restores everything
- **WHEN** I deselect the only selected type
- **THEN** every group shows what the other controls admit, and the groups the type filter opened stay open

#### Scenario: The timeline is untouched
- **WHEN** I select "Movie" on a series whose main line is four TV seasons
- **THEN** the timeline still shows all four, in watch order

#### Scenario: Only present types are offered
- **WHEN** a series' extras and related entries hold no music entry
- **THEN** no "Music" button is offered

#### Scenario: The type filter composes with the list filter
- **WHEN** the "in my list" filter is on and I select "OVA"
- **THEN** the groups holding OVAs are opened and show only the OVAs that are in my list, each offering its hidden-count control if it hides others

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

### Requirement: The More section's view state is restored with the page
The More section's four view controls — the "in my list" filter, the media-type filter, each group's collapsed state, and each group's exemption from the "in my list" filter — together with the **alternative picked for each version slot** SHALL be part of the series page's restorable state, restored on back/forward navigation exactly as every other page's view controls are, per the `page-state-restoration` capability.

Returning to a series page by back/forward navigation SHALL therefore show the page as it was left: a group opened in full is still open, a collapsed group is still collapsed, the media types I selected are still selected, the "in my list" control still reports the state it reported when the page was left, and the route I picked is still picked.

A fresh visit — a link, a typed URL, a reload — SHALL still open the section on its documented default: the "in my list" filter on, no media type selected, **every group collapsed**, no group exempted, and every version slot on the default alternative the `series-versions` capability defines.

A group key held in restored state that matches no group the restored page renders — because the series' extras changed between the two renders — SHALL be ignored rather than treated as an error. A restored media type that no extra of the series carries SHALL likewise be ignored, as SHALL a restored pick naming an anime that is no longer an alternative of any slot.

The state SHALL NOT be persisted beyond the browser tab's application session, and SHALL NOT be shared between two different series' pages.

#### Scenario: An opened group is still open on return
- **WHEN** I open a More group in full, open one of its extras, and navigate back
- **THEN** that group is still showing all of its tiles, and the "in my list" control still reads as off

#### Scenario: The filter is not reset by a round trip
- **WHEN** I turn the "in my list" filter off, expand a group, open an entry, and navigate back
- **THEN** the filter is still off and that group still shows every one of its extras

#### Scenario: Selected media types survive a round trip
- **WHEN** I select "Movie" and "OVA", open an entry, and navigate back
- **THEN** both are still selected and the section shows the same tiles it showed before

#### Scenario: A picked route survives a round trip
- **WHEN** I pick a route other than the default, open one of its entries, and navigate back
- **THEN** that route is still picked and the same main-line entries are shown

#### Scenario: An expanded group is still expanded on return
- **WHEN** I expand a group, navigate away, and navigate back
- **THEN** that group still renders its tiles

#### Scenario: A fresh visit still opens on the default
- **WHEN** I reach a series page by following a link rather than by navigating back
- **THEN** the "in my list" filter is on, no media type is selected, every group is collapsed, no group is exempted, and every version slot is on its default alternative

#### Scenario: A stale pick is ignored
- **WHEN** restored state names a picked anime that the rebuilt series no longer holds as an alternative
- **THEN** the slot opens on its default rather than failing

#### Scenario: Two series do not share More-section state
- **WHEN** I open one series and expand its More section, then open a different series
- **THEN** the second series' More section opens with every group collapsed, unaffected by the first

### Requirement: Opening a More group scrolls it to the top of the viewport
When a More group is opened from its heading, the page SHALL scroll so that group's heading sits at the top of the viewport, so the tiles that were just revealed are what fills the screen rather than remaining below the fold.

When the group is too near the end of the page for its heading to reach the top — the page cannot scroll that far — the page SHALL scroll as far as it can, leaving the heading as high as the page allows rather than not scrolling at all.

The scroll SHALL happen after the revealed tiles have been laid out, since those tiles are what makes the page tall enough to reach the target.

Collapsing a group from its heading SHALL NOT scroll the page: the user is dismissing content, not asking to be moved.

The "in my list" control and the expand/collapse-all control SHALL NOT scroll the page either. They act on every group at once, so there is no single group to scroll to.

#### Scenario: An opened group is brought to the top
- **WHEN** I activate the heading of a group part-way down a long series page
- **THEN** the page scrolls so that group's heading is at the top of the viewport, with its tiles below it

#### Scenario: The last group scrolls as far as the page allows
- **WHEN** I activate the heading of the final group, whose tiles do not fill a screen
- **THEN** the page scrolls to its bottom, leaving that heading as high as it can go rather than staying where it was

#### Scenario: Collapsing does not move the page
- **WHEN** I activate the heading of a group that is showing all of its tiles
- **THEN** the group collapses and the page does not scroll

#### Scenario: The section-wide controls do not scroll
- **WHEN** I activate "Expand all" or the "in my list" control
- **THEN** every group updates and the page's scroll position is unchanged

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

### Requirement: A timeline card's footer always shows its rewatch indicator
A main-line timeline card whose entry has been rewatched SHALL show its rewatch indicator in full, whatever the length of the rest of its footer. The card's fixed width SHALL be wide enough that a card carrying a list status, a watched-episode figure, and a rewatch indicator shows all three at once for an entry with an ordinary episode count, rather than sizing to the shortest case and cutting the indicator off in the long one.

Where the footer's content still cannot fit — an entry with a four-digit episode count on both sides of its watched figure, for instance — the **status text SHALL be what shortens**, by truncation, and the rewatch indicator SHALL remain whole and visible. The indicator SHALL NOT be shrunk, wrapped, faded, or pushed outside the card's visible area by a longer status line.

Widening SHALL apply equally to every portrait main-line card, so cards remain the same size as one another and the timeline's per-duration-neutrality holds: a wider card SHALL still not be readable as a longer or more important entry. A landscape card SHALL remain the one alternative width and SHALL stay wider than a portrait card by the same relationship it has today. A card's picture SHALL keep its existing proportions at the new width, and the picture, title, chips, and footer of every card in a row SHALL still line up with their neighbours'.

#### Scenario: A four-digit episode count keeps the rewatch indicator
- **WHEN** a main-line card shows an entry I have rewatched whose watched figure is `1141/1141`
- **THEN** the card still shows its rewatch indicator in full, with the status text truncated ahead of it if room runs out

#### Scenario: An ordinary entry fits without truncation
- **WHEN** a main-line card shows a rewatched entry with a two-digit episode count
- **THEN** its status, watched figure, and rewatch indicator all render in full with nothing truncated

#### Scenario: Every portrait card is still the same size
- **WHEN** a series' main line holds entries of very different lengths
- **THEN** every portrait card renders at the same widened width as every other, and the spacing between cards is unchanged

#### Scenario: Landscape cards stay the wider exception
- **WHEN** a main line holds both portrait and landscape artwork
- **THEN** the landscape cards are still wider than the portrait ones, all landscape cards match one another, and every card's picture, title, chips, and footer still line up across the row

#### Scenario: The row still scrolls rather than the page
- **WHEN** the widened cards no longer fit the container
- **THEN** the timeline row scrolls within its own container without a visible scrollbar, exactly as it does today

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

### Requirement: The series page carries picture and title controls beside Rebuild
The series page SHALL carry a **Choose picture** control and a **Choose title** control for the series it is showing.

Both SHALL sit in the **Series stats** heading row, alongside the **Rebuild** control, and SHALL be presented as that row's controls are rather than as links. They SHALL NOT sit in the page's header block among the external links to MyAnimeList, AniList and SeriesGraph — a control that changes what the series is SHALL be grouped with the other such control rather than with links that navigate away. The header block SHALL therefore hold the picture, title, status, year span and external links only, and nothing SHALL be left in its place where the two controls were.

**Rebuild** SHALL keep its position at the end of that group, adjacent to the partial- and truncated-series notices that qualify it, so those notices still read as belonging to it.

**Choose picture** SHALL be rendered only when the series has more than one picture to choose between, and SHALL open the series picture picker the `artwork-selection` capability defines. A series with a single picture available SHALL show no control at all, rather than a control that opens onto one image. Its absence SHALL NOT disturb the position of the remaining controls in the row.

**Choose title** SHALL always be rendered, since a title can always be trimmed even when only one is offered. It SHALL open a picker listing every title offered by the `series-identity` capability, together with a text field for a trimmed title, and SHALL refuse to submit a title that capability's rule rejects.

Both controls SHALL affect the series only. Neither SHALL change any member anime's own picture or title, and neither SHALL cause anything to be written to MyAnimeList.

Choosing a picture or a title SHALL take effect on the page without a reload, and SHALL be reflected on every other surface that shows this series the next time it is read.

#### Scenario: A multi-picture series offers the control
- **WHEN** I open a series whose main-line members between them offer six distinct pictures
- **THEN** the Series stats row shows a "Choose picture" control, and clicking it opens a picker of those six pictures

#### Scenario: A single-picture series shows no picture control
- **WHEN** I open a series whose main-line members offer exactly one distinct picture between them
- **THEN** no "Choose picture" control is shown, and "Choose title" and "Rebuild" still sit together in the Series stats row

#### Scenario: The title control is always available
- **WHEN** I open any series
- **THEN** the Series stats row shows a "Choose title" control

#### Scenario: The controls sit with Rebuild, not with the links
- **WHEN** I open any series
- **THEN** "Choose picture" and "Choose title" appear in the Series stats heading row next to "Rebuild", and the page header block below the title shows only the MyAnimeList, AniList and SeriesGraph links

#### Scenario: Rebuild keeps its notices
- **WHEN** I open a series that could not be built in full
- **THEN** the "Some entries couldn't be loaded yet" notice still reads alongside the Rebuild control in that same row

#### Scenario: A chosen picture applies immediately
- **WHEN** I pick a picture from the series picker
- **THEN** the header's picture changes to it without a page reload

#### Scenario: Choosing does not touch the members
- **WHEN** I choose a picture for a series
- **THEN** no main-line member's own displayed picture changes, and nothing is pushed to MyAnimeList

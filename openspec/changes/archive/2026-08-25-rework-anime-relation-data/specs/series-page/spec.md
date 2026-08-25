## MODIFIED Requirements

### Requirement: Series composition from the relation graph
The system SHALL derive a series as the connected component of the stored related-anime graph, traversing only story relations: `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and `alternative_version`. Every other relation MAL reports — including `alternative_setting`, `character`, and any unrecognized relation string — SHALL be stored as it already is but SHALL NOT be traversed, so shows that merely share a universe or a cast never merge into one series.

The `other` relation SHALL be traversed in exactly one case: when precisely one of its two ends is an anime whose media type is `music`. MAL links a franchise's opening/ending/image songs to the show they belong to with `other` and nothing else, so a franchise's music entries are otherwise unreachable — they either vanish from the series entirely or form their own music-only series. A music end SHALL be recognised only from an already-cached media type: an `other` relation whose far end has no cached metadata row SHALL NOT be traversed and SHALL NOT spend fetch budget, since the system cannot tell a song from a commercial without fetching, and `other` also links commercials, promos, and crossovers.

`other` relations where neither end is a music entry, and where both ends are music entries, SHALL NOT be traversed. Because the rule is stated over the relation's two ends rather than over the direction it is stored in, a series built from the song and a series built from the show SHALL produce the same component.

Traversal SHALL be undirected: from a member the system SHALL follow both that anime's own relation rows and relation rows pointing at it, so a member whose own relations have never been fetched still connects the component.

A story relation an external source **contradicts** — one end asserts it, the other end was fetched and does not, and AniList knows both anime and relates them not at all — SHALL NOT be traversed. Such an edge is one anime's unreciprocated claim about another that no other source supports, and traversing it silently admits an unrelated anime to the franchise. The edge SHALL remain stored and SHALL remain visible on the detail page; only its power to pull a member into a series is withdrawn. An edge that is merely unreciprocated, with no external source to settle it, SHALL still be traversed, so a franchise never shrinks on the strength of missing information alone.

An anime SHALL belong to at most one series.

Every series stored under the previous traversal or ordering rules SHALL be rebuilt once, on its next read, so both a franchise's music entries and this change's corrections reach an already-stored series without the user having to request a rebuild.

#### Scenario: Sequels and prequels form one series
- **WHEN** a series is built from an anime whose relations chain through two sequels and one prequel
- **THEN** all four anime are members of the same series

#### Scenario: Alternative-setting relations do not merge series
- **WHEN** an anime is related to another only by `alternative_setting` or `character`
- **THEN** the other anime is not a member of its series

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

#### Scenario: A stored series re-derives itself after a rules change
- **WHEN** a series stored under the previous rules is next read
- **THEN** it is rebuilt under the current rules, without the user pressing Rebuild

### Requirement: Watch order and series root
The system SHALL order main-line entries in **story order**: a topological ordering over the `sequel`/`prequel` edges among the main-line members, so an entry always precedes the entries it is a prequel to. The system SHALL present that ordering as the series' watch order, numbered from 1.

Aired-from date SHALL be a tie-break, not the ordering: where two entries are unconstrained relative to each other — neither reachable from the other over the chain — the earlier aired-from date SHALL come first, entries lacking a date SHALL be placed last, and MAL id SHALL break the remaining tie. An entry with no chain edge at all SHALL therefore be placed purely by its aired date, interleaved with the chain.

The main line is deliberately **story order, not release order**. A prequel film released after the season it precedes belongs before that season in a watch order, and ordering by air date puts it after — the chain edges the main line is already classified from state the correct order and were previously discarded at the ordering step.

Where the chain edges contain a cycle, so that no topological order exists, the system SHALL break the cycle in favour of aired-from order and produce a stable ordering rather than failing the build. Extras remain ordered by aired-from date within their media-type group, as specified under "Main line and extras".

The series root SHALL be the first entry in that ordering. The series SHALL take its title and its main picture from the root.

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

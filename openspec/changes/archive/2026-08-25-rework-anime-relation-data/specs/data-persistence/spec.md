## ADDED Requirements

### Requirement: Related-anime edges are indexed in both directions
The stored related-anime edge table SHALL be indexed by the related anime's id, not only by the owning anime's id, so the edges pointing *at* a given anime can be found without scanning the table.

Reverse lookups were previously confined to series builds, which run rarely; presenting an anime's relations in both directions puts one on every detail-page read, and the series graph builder already issues one per traversal step.

#### Scenario: Reverse lookup is indexed
- **WHEN** the system queries for every relation edge whose target is a given anime
- **THEN** the query is served by an index on the related-anime id rather than a full scan

#### Scenario: Existing forward access is unchanged
- **WHEN** an anime's own relation rows are loaded by owner id
- **THEN** they are served as before

### Requirement: Newly discovered relation edges are recorded as events
When a full-detail refresh replaces an anime's stored relation set, the system SHALL compare the incoming set against the set it replaces and SHALL persist one event per relation edge that was not present before, recording at minimum: the owning anime's MAL id, the related anime's MAL id, the relation type, and the instant the edge was first observed.

The comparison SHALL use the pre-replacement snapshot the refresh already loads; no additional query and no separate polling loop SHALL be introduced for it.

An edge that already existed SHALL NOT produce an event, so a routine tiered refresh of unchanged data writes nothing. An anime being cached for the very first time SHALL NOT emit events for its whole initial relation set, since none of those relations are news.

These events exist so that a future view of new releases related to anime in my list can be answered from stored data rather than from its own scan of MyAnimeList. No user-facing view reads them in this change, and historical edges discovered before this change SHALL NOT be backfilled.

#### Scenario: A newly announced sequel is recorded
- **WHEN** a tiered refresh of an anime returns a `sequel` relation its stored set did not contain
- **THEN** an event is persisted naming that anime, the sequel's id, the relation type, and the discovery instant

#### Scenario: Unchanged relations write nothing
- **WHEN** a refresh returns exactly the relation set already stored
- **THEN** no events are written

#### Scenario: First-time caching is not a discovery
- **WHEN** an anime with no cached row at all is fetched and its relations are stored for the first time
- **THEN** no discovery events are written for that initial set

#### Scenario: A removed relation is not an event
- **WHEN** a refresh no longer reports a relation the stored set contained
- **THEN** the stale relation is removed as it is today and no discovery event is written

### Requirement: AniList relation edges are stored, not fetched at read time
The system SHALL persist the relation edges AniList reports for an anime — at minimum the anime's MAL id, the related anime's MAL id as AniList reports it, and AniList's relation type — so that adjudicating a contested MyAnimeList edge is a local join rather than a live AniList call during a page read.

An AniList relation whose far end has no MAL id SHALL NOT be stored; it cannot be matched against a MAL edge and is not evidence about one.

This record SHALL be stored separately from the cached MyAnimeList metadata, so a MAL refresh — which replaces the MAL relation set wholesale — never clobbers it, mirroring how AniList airing sync state is already kept apart.

#### Scenario: Adjudication reads stored data
- **WHEN** a detail page or series build needs to know whether AniList supports a contested MAL edge
- **THEN** the answer comes from stored AniList relation rows, with no AniList request made during that read

#### Scenario: A MAL refresh preserves AniList relations
- **WHEN** an anime's MAL relation set is replaced by a full-detail fetch
- **THEN** its stored AniList relation rows are unchanged

#### Scenario: A relation with no MAL id is skipped
- **WHEN** AniList reports a relation whose far media has no MAL id
- **THEN** no row is stored for it

### Requirement: Whether AniList's relations are known is recorded per anime
The per-anime AniList sync record SHALL additionally hold the instant that anime's relation data was last fetched from AniList, nullable, where null means its relations have never been fetched.

This is what makes a contradiction verdict possible: "AniList relates these two anime not at all" is only meaningful when AniList's relation set for that anime is known to have been fetched. Absent this, an anime with no stored AniList relation rows is indistinguishable from one that has simply never been looked up, and an unfetched anime would contradict every edge pointing at it.

Together with the existing nullable AniList identifier — null with a non-null fetch instant meaning "AniList has no entry for this anime" — the record distinguishes three states an adjudication must tell apart: relations known, no such anime on AniList, and never looked up.

#### Scenario: Never looked up does not contradict
- **WHEN** an anime's AniList relations have never been fetched
- **THEN** no MAL edge involving it is judged contradicted

#### Scenario: Fetched and empty does contradict
- **WHEN** an anime's AniList relations were fetched, AniList resolved the anime, and no stored AniList relation links it to the far end of a contested MAL edge
- **THEN** that edge is judged contradicted

#### Scenario: Absent from AniList does not contradict
- **WHEN** an anime's AniList lookup resolved to no AniList entry at all
- **THEN** no MAL edge involving it is judged contradicted, since AniList holds no opinion about it

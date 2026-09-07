# data-persistence Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

The record SHALL hold **three** episode-count values rather than one, mirroring how it already holds three picture-related values:

- the **effective total**, which is the figure every surface of the app reads and the one the field above describes;
- **MyAnimeList's own reported total**, written by every MyAnimeList sync and null where MyAnimeList publishes none; and
- **AniList's reported total**, written by the AniList airing sync and null where AniList has no entry for the anime or states no total.

The effective total SHALL be derived from the other two rather than written independently: it SHALL equal MyAnimeList's reported total where that is non-null, and AniList's otherwise. Every writer of either source column SHALL re-derive it in the same write, so the three values can never disagree and neither source can blank a figure the other supplied. No separate flag recording which source is in force SHALL be stored — the pair of source columns is that record.

The record SHALL hold **three** picture-related values rather than one:

- the **displayed picture**, which is the picture every surface of the app renders for that anime;
- **MAL's own main picture**, always overwritten by any MAL sync; and
- the **picture set**, the list of picture URLs MAL publishes for the anime, in MAL's order, stored as a list on the record itself rather than as separate rows.

The record SHALL also hold a **picture fetch timestamp**, distinct from `last_synced_at`, which is null until the picture set has been fetched. It exists so that "never asked MAL for pictures" is distinguishable from "asked, and MAL publishes one picture".

An anime SHALL be considered to have a chosen picture exactly when its displayed picture differs from MAL's main picture. No separate flag SHALL be stored for it.

Introducing the two source columns SHALL leave every stored anime reading exactly as it did: each existing record's MyAnimeList reported total SHALL be set to the total it already holds, and its AniList reported total SHALL be null, which re-derives the same effective total it had before.

#### Scenario: Storing cached metadata
- **WHEN** anime metadata is fetched from MAL
- **THEN** an AnimeMetadata record is created or updated with the fields above and its sync timestamps

#### Scenario: Unknown total episodes
- **WHEN** an anime's total episode count is not known to either source
- **THEN** all three episode-count values are stored as null

#### Scenario: An AniList-supplied total
- **WHEN** MyAnimeList publishes no total for an anime and AniList reports 12
- **THEN** the record's MyAnimeList reported total is null, its AniList reported total is 12, and its effective total is 12

#### Scenario: MyAnimeList's total takes precedence
- **WHEN** a record holds an AniList reported total of 12 and a MyAnimeList sync writes 13
- **THEN** its MyAnimeList reported total is 13, its AniList reported total is still 12, and its effective total is 13

#### Scenario: The migration leaves every anime unchanged
- **WHEN** the two source columns are introduced on a database of existing anime
- **THEN** every anime's MyAnimeList reported total holds the total it already had, its AniList reported total is null, and its effective total is unchanged

#### Scenario: English title when available
- **WHEN** MAL provides an English alternative title for an anime
- **THEN** it is stored on the AnimeMetadata record; when MAL provides none, the field is null

#### Scenario: Picture values on a freshly synced record
- **WHEN** an anime is synced from MAL and no picture has been chosen for it
- **THEN** its displayed picture and MAL's main picture hold the same URL

#### Scenario: Never-fetched pictures are marked as such
- **WHEN** an anime has never had its picture set fetched
- **THEN** its picture fetch timestamp is null and its picture set is empty

### Requirement: UserAnimeEntry entity
The system SHALL persist a UserAnimeEntry per anime in my list containing at minimum: status (watching, on hold, plan to watch, completed, dropped), episodes watched, my score, started date, completed date, rewatch count, `pending_sync` flag, and `last_synced_at`.

#### Scenario: Storing my relationship to an anime
- **WHEN** an anime is in my list
- **THEN** a UserAnimeEntry stores my status, progress, score, dates, rewatch count, and sync state

### Requirement: Pending entry removal record
The system SHALL persist, for every entry removed from my list whose MAL removal has not yet succeeded, a record identifying the anime and when the removal was requested. The record SHALL survive process restarts, SHALL be created in the same transaction that deletes the UserAnimeEntry, and SHALL be deleted once the removal has been pushed to MAL successfully.

Because it must outlive the entry it refers to, the record SHALL NOT depend on a UserAnimeEntry row existing. Re-adding an anime to my list while its removal is still pending SHALL discard the pending removal, so a queued delete can never erase an entry the user has since recreated.

#### Scenario: Removal is durable
- **WHEN** an entry is removed from my list and the application restarts before the MAL push succeeds
- **THEN** the pending removal record is still present and the removal is retried

#### Scenario: Record cleared on success
- **WHEN** an anime's removal is pushed to MAL successfully
- **THEN** its pending removal record is deleted

#### Scenario: Re-adding cancels a pending removal
- **WHEN** an anime is added back to my list while its removal is still pending
- **THEN** the pending removal record is discarded and no delete is pushed for it

### Requirement: ActivityLog entity
The system SHALL persist an ActivityLog of timestamped change records (status changes, episode increments, score changes, additions, completions, removals) sufficient to reconstruct a chronological feed. Episode-change records SHALL also store the previous episodes-watched value so the direction of the change (increase vs decrease) can be determined at read time.

A removal record SHALL survive the deletion of the UserAnimeEntry it describes, so the history of an anime that was removed from my list is not lost along with the entry.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

#### Scenario: Recording a removal
- **WHEN** an anime is removed from my list
- **THEN** a timestamped ActivityLog row recording the removal is written and is retained after the entry is deleted

### Requirement: OAuthToken entity
The system SHALL persist OAuth tokens (access token, refresh token, expiry) in Postgres rather than only in memory or the container filesystem.

#### Scenario: Token durability
- **WHEN** tokens are obtained or refreshed
- **THEN** they are written to the OAuthToken store in Postgres

### Requirement: Local database is the source of truth
The system SHALL serve all normal page reads from Postgres so that no page read blocks on a live MAL API call.

#### Scenario: Page read after initial sync
- **WHEN** any page loads during normal use
- **THEN** its data is read from Postgres and no synchronous live API call is required to render it

### Requirement: Extended anime metadata for the detail page
The system SHALL also persist, per AnimeMetadata record, the fields the single-anime detail page needs: genres, synopsis text, background text, average episode duration, source (e.g. manga, original, light novel), and references to related prequel and sequel anime (each stored as a MAL id and title) when MAL provides them.

#### Scenario: Storing detail-page metadata
- **WHEN** anime metadata is fetched from MAL via a full detail fetch
- **THEN** the AnimeMetadata record also stores its genres, synopsis, background, average episode duration, source, and any prequel/sequel relations

#### Scenario: No related anime
- **WHEN** an anime has no prequel or sequel
- **THEN** no related-anime references are stored and the detail page shows no prequel/sequel links

### Requirement: Persisted reconciliation diff pending review
The system SHALL persist the most recent full-reconciliation run's computed differences until they are accepted or cancelled, so a pending review survives a backend restart.

#### Scenario: Pending diff survives restart
- **WHEN** the backend restarts while a reconciliation diff is awaiting review
- **THEN** the same pending diff is still available for review after restart

#### Scenario: Diff cleared after resolution
- **WHEN** a pending diff is accepted or cancelled
- **THEN** it is no longer stored as awaiting review

### Requirement: Persisted manual top-anime selections
The system SHALL persist which anime the user has manually chosen to occupy the remaining "My top anime" slots, so the profile page can honor a manual choice instead of the default next-highest auto-fill.

#### Scenario: Saving a manual top-anime choice
- **WHEN** I manually choose which tied next-highest-scored anime fill the remaining top-10 slots
- **THEN** that selection is persisted and survives restarts

#### Scenario: No manual selection stored
- **WHEN** I have never made a manual top-anime selection
- **THEN** no selection is stored and the profile page falls back to the default auto-fill

### Requirement: EpisodeAiring entity
The system SHALL persist an EpisodeAiring record per episode per anime containing at minimum: the anime's MAL id, the episode number, the episode's air instant as an absolute point in time, and the instant the record was last written. The anime id and episode number together SHALL identify the record uniquely, so an anime cannot hold two rows for the same episode number.

Records SHALL be removed when their anime's metadata record is removed.

The store SHALL support querying an anime's records by air-instant range and querying the highest episode number whose air instant precedes a given instant, without loading the anime's whole history.

#### Scenario: Storing an episode's air instant
- **WHEN** airing data is fetched for an anime
- **THEN** one EpisodeAiring record is stored per returned episode, holding its number, its air instant, and the write timestamp

#### Scenario: One row per episode number
- **WHEN** airing data is stored for an episode number that already has a record for that anime
- **THEN** the anime still holds exactly one record for that episode number

#### Scenario: Records removed with the anime
- **WHEN** an anime's metadata record is removed
- **THEN** its EpisodeAiring records are removed with it

#### Scenario: Range query for a week
- **WHEN** the schedule for one week is read
- **THEN** the records whose air instants fall in that range are retrieved without loading records outside it

### Requirement: AnimeAiringSync entity
The system SHALL persist, per anime whose airing data has been fetched, a record containing at minimum: the AniList identifier resolved for that anime (nullable, where null means AniList has no entry for it), the instant of the last successful fetch, the next-episode air instant AniList last reported (nullable), whether the anime's airing data is considered complete, and the instant at which the anime is next due for a recheck (nullable, where null means never due).

This record SHALL be stored separately from the cached MyAnimeList metadata record, so that refreshing MyAnimeList metadata does not overwrite it.

#### Scenario: Recording a resolved AniList identifier
- **WHEN** an anime's AniList identifier is resolved for the first time
- **THEN** it is stored and reused by later fetches for that anime

#### Scenario: Recording that AniList has no entry
- **WHEN** AniList has no entry matching an anime's MAL id
- **THEN** a record is stored with a null AniList identifier and a last-fetch instant, distinguishing "known absent" from "never looked up"

#### Scenario: MyAnimeList refresh does not clobber sync state
- **WHEN** an anime's cached MyAnimeList metadata is refreshed
- **THEN** its AniList identifier, last-fetch instant, next-episode instant, completeness flag, and recheck due instant are unchanged

### Requirement: AiringRefreshState record
The system SHALL persist a single record holding at minimum: the instant of the last successful airing-refresh pass, the instant the one-time full-history backfill was recorded complete (nullable, where null means not yet complete), and the viewing season the last pass ran in.

#### Scenario: Recording a successful pass
- **WHEN** an airing-refresh pass completes successfully
- **THEN** the record's last-successful-pass instant and viewing season are updated

#### Scenario: Backfill completion survives a restart
- **WHEN** the application restarts after the backfill was recorded complete
- **THEN** the record still shows the backfill as complete and no backfill is run

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

### Requirement: A stored series is keyed on its root entry's MAL id
The system SHALL store each series under the MyAnimeList id of its root entry, and SHALL NOT store any second series identifier alongside it. A membership record SHALL refer to its series by that same value, so that a stored series and its membership records are meaningful without the store that produced them.

The identifier SHALL NOT be generated by the store. A value the store mints for itself — a sequence, an identity column, a random key — differs between installations for the same franchise and cannot be reproduced by rebuilding, which is what a stored per-series choice needs in order to be reunited with its series elsewhere.

Where a rebuild changes a series' root, the stored series SHALL come to rest under the new root's id with its chosen title, chosen picture and membership intact, and the store SHALL NOT end that rebuild holding the series under both identifiers, under neither, or with membership records pointing at an identifier no series holds.

#### Scenario: The stored key is the root's MAL id
- **WHEN** a series rooted at the anime with MAL id 1735 is persisted
- **THEN** the series row is keyed 1735, and each of its membership rows refers to series 1735

#### Scenario: No second identifier is stored
- **WHEN** a stored series is inspected
- **THEN** it carries one identifier, and no separate stored root-anime column duplicating it

#### Scenario: A re-rooting leaves the store consistent
- **WHEN** a rebuild moves a series' root and completes
- **THEN** exactly one series row exists for that franchise, keyed on the new root, and every one of its membership rows refers to it

#### Scenario: A re-rooting interrupted leaves no half-written series
- **WHEN** a rebuild that moves a series' root fails partway through persisting
- **THEN** the store holds the series exactly as it was before the rebuild, with its chosen title and picture

### Requirement: The series rekey migration preserves every stored series and choice
The migration that makes the root entry's MAL id the stored series identifier SHALL rewrite the stored series and their memberships in place. It SHALL NOT rebuild, re-derive or discard any series, and SHALL NOT require any chosen title or chosen picture to be picked again.

Every stored series SHALL come out keyed on the root anime id it already recorded, every membership record SHALL follow its series, and the counts of series, memberships, and series carrying a chosen title or picture SHALL be unchanged by the migration.

The rewrite SHALL be correct where a series' root anime id is equal to a **different** series' current identifier, which is the case for stored data today; the migration SHALL NOT depend on the two id ranges being disjoint.

#### Scenario: Nothing is lost
- **WHEN** the migration runs against a store of 255 series and 1600 memberships, 105 of the series carrying a chosen title or picture
- **THEN** it leaves 255 series, 1600 memberships and 105 carried choices

#### Scenario: Each series lands on its recorded root
- **WHEN** the migration completes
- **THEN** every series is keyed on the root anime id its row recorded beforehand, and no membership refers to an identifier no series holds

#### Scenario: A root that collides with another series' old identifier
- **WHEN** a series' recorded root anime id equals another series' identifier before the migration
- **THEN** both series survive the migration, each keyed on its own root, with their memberships and choices intact

### Requirement: Series presentation overrides are persisted on the series
The system SHALL persist, on each Series record, an optional **chosen title** and an optional **chosen picture URL**, both null by default.

They SHALL live on the Series row rather than on any member, so that a rebuild which changes which member is the root, and so changes the series' identifier, does not change or clear the chosen identity: the choices are carried to the series' new identifier rather than re-derived.

They SHALL survive a rebuild of the series — a rebuild that keeps the series' row simply never writes these fields, and a rebuild that must re-key the row carries them across unchanged.

#### Scenario: Defaults are null
- **WHEN** a series is built for the first time
- **THEN** its chosen title and chosen picture are both null and its identity is derived from its root member

#### Scenario: Choices persist across restarts
- **WHEN** a series is given a chosen title and picture and the application restarts
- **THEN** both are still stored and still applied

#### Scenario: A rebuild does not clear them
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are still stored on the kept series row

#### Scenario: A re-rooting does not clear them
- **WHEN** a series with a chosen title and picture is rebuilt and its root changes
- **THEN** both are stored on the series under its new identifier

### Requirement: The picture-column migration leaves every anime unchosen
The migration that introduces the picture columns SHALL copy each anime's existing picture into the new MAL-main-picture column, so that on completion every stored anime has a displayed picture equal to its MAL picture and is therefore recorded as having no chosen picture.

No anime SHALL be left in a state where it appears to have a chosen picture as a result of the migration, and no page's rendering SHALL change when the migration is applied.

Picture sets and picture fetch timestamps SHALL be left empty and null respectively, since no picture set has in fact been fetched for any anime at that point.

#### Scenario: Every row starts unchosen
- **WHEN** the migration runs against an existing database
- **THEN** every anime's MAL main picture equals its displayed picture, and no anime is recorded as having a chosen picture

#### Scenario: Rendering is unchanged
- **WHEN** the migration has been applied and the app is opened
- **THEN** every page shows exactly the artwork it showed before

#### Scenario: Nothing is marked as fetched
- **WHEN** the migration has been applied
- **THEN** every anime's picture fetch timestamp is null


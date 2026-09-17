# data-persistence Specification

## Purpose
The data-persistence capability defines what this app keeps durable in Postgres and why: the anime, entry, activity-log, OAuth-token, ranking, series, and episode-airing tables, and the related-anime and AniList relation-confidence data alongside them. It also sets what each migration must preserve when the schema changes — every displayed picture and choice, the ranking's own modification time, existing series and their presentation overrides — and that every database carries its own device identifier. Local Postgres is the single source of truth the rest of the app reads from.
## Requirements

### Requirement: The chosen-picture migration preserves every displayed picture and stamps every existing choice
The migration that introduces the chosen-picture column SHALL derive each anime's chosen picture from the state that already records one: an anime whose displayed picture differs from MAL's main picture SHALL have that displayed picture copied into its chosen-picture column, and every other anime SHALL be left with no chosen picture.

Every anime's displayed picture SHALL be exactly what it was before the migration, since copying the value it already holds re-derives the same displayed picture. No page's rendering SHALL change when the migration is applied, and no anime SHALL be left needing to be re-picked.

Every choice that exists when the migration runs — each anime chosen picture it has just recorded, and each series chosen title and chosen picture already stored — SHALL be stamped with the migration's own run time. It follows that a choice made before the migration is always older than any choice made after it on that database, and that no recorded time is absent for a choice that exists.

The migration SHALL be reversible without loss of any displayed picture: removing the new columns SHALL leave the displayed picture and MAL's main picture exactly as the migration found them.

#### Scenario: An overridden anime becomes a stored choice
- **WHEN** the migration runs against an anime whose displayed picture differs from MAL's main picture
- **THEN** that displayed picture is stored as the anime's chosen picture, its displayed picture is unchanged, and its choice is stamped with the migration's run time

#### Scenario: An unoverridden anime gets no choice
- **WHEN** the migration runs against an anime whose displayed picture equals MAL's main picture, or which has no picture at all
- **THEN** no chosen picture is stored for it and no time is recorded for it

#### Scenario: Existing series choices are stamped
- **WHEN** the migration runs against a database holding series with chosen titles and chosen pictures
- **THEN** each of those choices carries the migration's run time, and series with no choice carry no time

#### Scenario: Rendering is unchanged
- **WHEN** the migration has been applied and the app is opened
- **THEN** every page shows exactly the artwork it showed before

### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

The record SHALL hold **three** episode-count values rather than one, in the same shape as its picture-related values:

- the **effective total**, which is the figure every surface of the app reads and the one the field above describes;
- **MyAnimeList's own reported total**, written by every MyAnimeList sync and null where MyAnimeList publishes none; and
- **AniList's reported total**, written by the AniList airing sync and null where AniList has no entry for the anime or states no total.

The effective total SHALL be derived from the other two rather than written independently: it SHALL equal MyAnimeList's reported total where that is non-null, and AniList's otherwise. Every writer of either source column SHALL re-derive it in the same write, so the three values can never disagree and neither source can blank a figure the other supplied. No separate flag recording which source is in force SHALL be stored — the pair of source columns is that record.

The record SHALL hold **four** picture-related values rather than one:

- the **displayed picture**, which is the picture every surface of the app renders for that anime;
- the **chosen picture**, which is the picture chosen for that anime and is null when none has been chosen;
- **MAL's own main picture**, always overwritten by any MAL sync; and
- the **picture set**, the list of picture URLs MAL publishes for the anime, in MAL's order, stored as a list on the record itself rather than as separate rows.

The displayed picture SHALL be derived from the chosen picture and MAL's main picture rather than written independently: it SHALL equal the chosen picture where that is non-null, and MAL's main picture otherwise. Every writer of either of those two SHALL re-derive it in the same write, so the three can never disagree — the same shape the episode-count values above already use.

The record SHALL also hold a **chosen-picture timestamp**, which records when the chosen picture was last set or cleared and is null when it has never been either.

The record SHALL also hold a **picture fetch timestamp**, distinct from `last_synced_at`, which is null until the picture set has been fetched. It exists so that "never asked MAL for pictures" is distinguishable from "asked, and MAL publishes one picture".

An anime SHALL be considered to have a chosen picture exactly when its chosen-picture column is non-null. Chosen-ness SHALL NOT be inferred from the displayed picture differing from MAL's main picture.

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
- **THEN** its chosen picture is null and its displayed picture and MAL's main picture hold the same URL

#### Scenario: A chosen picture is stored, not inferred
- **WHEN** a picture is chosen for an anime
- **THEN** its chosen-picture column holds that URL, its chosen-picture timestamp holds the time of the choice, and its displayed picture is re-derived to that URL

#### Scenario: MAL moves under a chosen picture
- **WHEN** a MAL sync writes a new main picture for an anime whose chosen picture is non-null
- **THEN** MAL's main picture column holds the new URL, the chosen picture and its timestamp are untouched, and the displayed picture is still the chosen one

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

Every record SHALL carry an **identifier** that is unique among all records in the database. The system SHALL assign it when it creates the record, before the record is first stored, and it SHALL never change once stored. The identifier is the only property of a record that SHALL identify that record outside this database. A record that is stored while already carrying an identifier SHALL keep that identifier.

Every record SHALL also carry a **local number**, which the database assigns when the record is stored. The local number is meaningful only within that database: it SHALL NOT leave it, and it SHALL NOT be used to identify a record anywhere else. Surfaces reading the log SHALL order records that share a timestamp by their local number, so those records read in the order they were stored on this database.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

#### Scenario: Recording a removal
- **WHEN** an anime is removed from my list
- **THEN** a timestamped ActivityLog row recording the removal is written and is retained after the entry is deleted

#### Scenario: A new record gets its own identifier
- **WHEN** any change is recorded
- **THEN** its record carries an identifier that no other record in the database carries

#### Scenario: A record stored with an identifier keeps it
- **WHEN** a record that already carries an identifier is stored
- **THEN** it is stored with that same identifier, and this database gives it a new local number

#### Scenario: Two records cannot share an identifier
- **WHEN** a record is stored with an identifier that another stored record already carries
- **THEN** storing it fails and the log is left unchanged

#### Scenario: Records sharing a timestamp read in the order they were stored
- **WHEN** one save records several changes at the same timestamp
- **THEN** surfaces reading the log treat them in the order they were stored on this database

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

### Requirement: The ranking's stored order carries one last-modified time
The system SHALL store the ranking's order as one position per anime. Alongside it, it SHALL store a single **ranking last-modified time**, which belongs to the ranking as a whole rather than to any position. This time SHALL be held on a single bookkeeping record that belongs to no anime. No per-position time SHALL be stored.

Every write of the stored order SHALL set the ranking last-modified time in the same transaction as the order itself. The order can then never be stored without its time, and the time can never advance without the order. This SHALL hold for every write:
- a reorder in the ranking editor
- a reorder on a series page
- the automatic placement that follows a saved score

It SHALL NOT depend on whether the written order differs from the one it replaces, nor on which of these caused the write.

An absent ranking last-modified time SHALL mean the ranking has never been arranged on this database.

The stored position SHALL remain the only thing that orders the ranking. No ordering SHALL be read from the time.

#### Scenario: A reorder stamps the ranking
- **WHEN** I reorder a score in the ranking editor and the change is saved
- **THEN** the ranking last-modified time is the time of that save

#### Scenario: A score placement stamps the ranking
- **WHEN** I save a new score for an anime and it is placed last among the hand-ordered anime of that score
- **THEN** the ranking last-modified time is the time of that placement

#### Scenario: An unchanged order is still stamped
- **WHEN** the stored order is written with exactly the order it already held
- **THEN** the ranking last-modified time still advances to the time of that write

#### Scenario: A ranking never arranged has no time
- **WHEN** no order has ever been written on a database
- **THEN** no ranking last-modified time is stored there

#### Scenario: Positions carry no time of their own
- **WHEN** the stored ranking is inspected
- **THEN** each anime in it carries only its position, and exactly one time is stored for the whole ranking

### Requirement: The stored ranking can be replaced exactly
The system SHALL provide a way to replace the stored ranking order with a given list of anime and a given time, in one transaction. Afterwards the stored order SHALL be exactly that list:
- each listed anime SHALL hold the position the list gives it
- no anime missing from the list SHALL hold a stored position

The ranking last-modified time SHALL become the given time, not the time the replacement ran. The replacement SHALL NOT compare the given time with the stored one; deciding which ranking is newer belongs to the caller.

This replacement SHALL be distinct from the slot-preserving merge used by the ranking editor, the series page and score placement. That merge SHALL keep behaving exactly as it does: anime it is not given keep their slots.

A list naming an anime the store holds no record of SHALL be rejected, and both the stored order and its time SHALL be left exactly as they were. Where the list names an anime more than once, its first occurrence SHALL decide its position.

An empty list SHALL leave the stored order empty, with the given time recorded. An emptied ranking therefore still records when it was emptied.

#### Scenario: Anime missing from the list are dropped
- **WHEN** the stored order is `[A, B, C]` and it is replaced with `[C, A]`
- **THEN** the stored order is `[C, A]` and `B` holds no position

#### Scenario: The merge still keeps slots it is not given
- **WHEN** the stored order is `[A, B, C]` and the editor's merge is given `[C, A]`
- **THEN** the stored order is `[C, B, A]`

#### Scenario: The given time is stored, not the time of the replacement
- **WHEN** the stored order is replaced with a list and a time three days in the past
- **THEN** the ranking last-modified time is that time from three days ago

#### Scenario: An unknown anime is rejected
- **WHEN** a replacement list names an anime the store holds no record of
- **THEN** the replacement fails, and the stored order and the ranking last-modified time are unchanged

#### Scenario: A repeated anime takes its first position
- **WHEN** the stored order is replaced with `[A, B, A]`
- **THEN** the stored order is `[A, B]`

#### Scenario: An empty replacement records its time
- **WHEN** the stored order is replaced with an empty list and a time `T`
- **THEN** no anime holds a stored position, and the ranking last-modified time is `T`

### Requirement: The portability migration keeps the ranking's time and identifies every log record
The migration that introduces the ranking last-modified time SHALL set that time from the latest per-position time stored when it runs, and only then remove the per-position times. An existing ranking therefore arrives carrying the time it was last written, rather than no time at all. A database holding no ranking positions SHALL be left with no ranking last-modified time.

The same migration SHALL give every existing activity-log record an identifier distinct from every other record's. For activity-log records, it SHALL NOT:
- change any record's timestamp, local number, change type or detail
- add or remove any record

For the ranking, it SHALL NOT change any position, nor add or remove one.

Reversing the migration SHALL restore a per-position time on every position, equal to the ranking last-modified time. That is the state every position was in before the migration ran.

#### Scenario: The current ranking keeps its time
- **WHEN** the migration runs against 455 stored positions that all hold the time 2026-09-09 07:59:51 UTC
- **THEN** the ranking last-modified time is 2026-09-09 07:59:51 UTC, and the same 455 anime hold the same positions

#### Scenario: An empty ranking gets no time
- **WHEN** the migration runs against a database holding no ranking positions
- **THEN** no ranking last-modified time is stored

#### Scenario: Every existing record gets its own identifier
- **WHEN** the migration runs against a log of 913 records
- **THEN** there are still 913 records, each carrying an identifier no other record carries, with its timestamp, local number, change type and detail unchanged

#### Scenario: The feed reads the same
- **WHEN** the migration has been applied and the profile page is opened
- **THEN** Latest updates and the full edit history show exactly the rows they showed before, in the same order, including every merged "Completed — Score" row

#### Scenario: Reversing restores the per-position time
- **WHEN** the migration is reversed on a database whose ranking last-modified time is `T`
- **THEN** every stored position carries the time `T` again

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
The system SHALL persist, on each Series record, an optional **chosen title** and an optional **chosen picture URL**, both null by default, each with its own **timestamp** recording when that choice was last set or cleared and null when it has never been either.

They SHALL live on the Series row rather than on any member, so that a series' identity is independent of which member happens to be its root, and so that a rebuild that changes the root does not change the chosen identity.

They SHALL survive a rebuild of the series — the rebuild simply never writes these fields on the row it keeps. Where a rebuild does move a choice — carrying it across a re-root, or adopting it from a series being absorbed — it SHALL move the choice and its timestamp together, and SHALL NOT restamp either as a change of its own.

#### Scenario: Defaults are null
- **WHEN** a series is built for the first time
- **THEN** its chosen title, chosen picture and both timestamps are null, and its identity is derived from its root member

#### Scenario: Choices persist across restarts
- **WHEN** a series is given a chosen title and picture and the application restarts
- **THEN** both are still stored, still applied, and still carry the times they were chosen

#### Scenario: A rebuild does not clear them
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are still stored on the kept series row, with their original timestamps

### Requirement: The picture-column migration leaves every anime unchosen
The migration that introduces the picture columns SHALL copy each anime's existing picture into the new MAL-main-picture column, so that on completion every stored anime has a displayed picture equal to its MAL picture and no anime has a chosen picture stored for it.

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

### Requirement: Each database carries one device identifier
The system SHALL store a single **device identifier** per database. It SHALL be held on a bookkeeping record that belongs to no anime. It identifies the installation the database belongs to, for example in the header of an export file.

The identifier SHALL be generated by the application and never configured:
- The first time the application starts on a database that holds none, it SHALL create one.
- On every later start, it SHALL leave the existing identifier untouched.
- Nothing else SHALL write it.

The identifier therefore survives restarts and rebuilds of the application, and SHALL NOT change for as long as the database exists.

Two databases created separately SHALL hold different identifiers.

Creating the identifier SHALL change no other stored value.

#### Scenario: An upgraded database gets its identifier on first start
- **WHEN** the application starts for the first time on an existing database that holds no device identifier
- **THEN** the database holds exactly one device identifier, and every other stored value is unchanged

#### Scenario: Nothing has to be set
- **WHEN** no device name, label or identifier is configured anywhere
- **THEN** the database still holds a device identifier once the application has started

#### Scenario: A restart keeps the identifier
- **WHEN** the application is restarted, or rebuilt and started again, on the same database
- **THEN** the device identifier is the one it held before

#### Scenario: Two databases hold different identifiers
- **WHEN** the application has started on each of two separately created databases
- **THEN** the two device identifiers differ


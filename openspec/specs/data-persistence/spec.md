# data-persistence Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

#### Scenario: Storing cached metadata
- **WHEN** anime metadata is fetched from MAL
- **THEN** an AnimeMetadata record is created or updated with the fields above and its sync timestamps

#### Scenario: Unknown total episodes
- **WHEN** an anime's total episode count is not known
- **THEN** the total episodes field is stored as null

#### Scenario: English title when available
- **WHEN** MAL provides an English alternative title for an anime
- **THEN** it is stored on the AnimeMetadata record; when MAL provides none, the field is null

### Requirement: UserAnimeEntry entity
The system SHALL persist a UserAnimeEntry per anime in my list containing at minimum: status (watching, on hold, plan to watch, completed, dropped), episodes watched, my score, started date, completed date, rewatch count, `pending_sync` flag, and `last_synced_at`.

#### Scenario: Storing my relationship to an anime
- **WHEN** an anime is in my list
- **THEN** a UserAnimeEntry stores my status, progress, score, dates, rewatch count, and sync state

### Requirement: ActivityLog entity
The system SHALL persist an ActivityLog of timestamped change records (status changes, episode increments, score changes, additions, completions) sufficient to reconstruct a chronological feed. Episode-change records SHALL also store the previous episodes-watched value so the direction of the change (increase vs decrease) can be determined at read time.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

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


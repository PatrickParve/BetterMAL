## ADDED Requirements

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

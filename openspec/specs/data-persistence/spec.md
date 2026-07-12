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


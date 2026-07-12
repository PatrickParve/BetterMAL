## MODIFIED Requirements

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

### Requirement: ActivityLog entity
The system SHALL persist an ActivityLog of timestamped change records (status changes, episode increments, score changes, additions, completions) sufficient to reconstruct a chronological feed. Episode-change records SHALL also store the previous episodes-watched value so the direction of the change (increase vs decrease) can be determined at read time.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

### Requirement: Extended anime metadata for the detail page
The system SHALL also persist, per AnimeMetadata record, the fields the single-anime detail page needs: genres, synopsis text, background text, average episode duration, source (e.g. manga, original, light novel), and references to related prequel and sequel anime (each stored as a MAL id and title) when MAL provides them.

#### Scenario: Storing detail-page metadata
- **WHEN** anime metadata is fetched from MAL via a full detail fetch
- **THEN** the AnimeMetadata record also stores its genres, synopsis, background, average episode duration, source, and any prequel/sequel relations

#### Scenario: No related anime
- **WHEN** an anime has no prequel or sequel
- **THEN** no related-anime references are stored and the detail page shows no prequel/sequel links

# initial-import Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Background full-list import after authorization
The system SHALL, after the one-time authorization, run a background job that fetches the full list via `GET /v2/users/@me/animelist`, paginated at roughly 100 entries per page.

#### Scenario: Paginated list fetch
- **WHEN** the initial import runs
- **THEN** it pages through the entire `@me` animelist rather than assuming a single response

### Requirement: Progressive per-entry insertion
The system SHALL fetch full anime metadata for each list entry and insert it into Postgres as each fetch completes, not batched at the end, so the UI can render partial data while the sync runs.

#### Scenario: Rendering during import
- **WHEN** the import has completed some but not all entries
- **THEN** the already-imported anime are present in Postgres and viewable while remaining ones continue loading

### Requirement: Conservative request pacing
The system SHALL pace import requests conservatively (roughly one request per second) so a library of a few hundred anime completes in low minutes.

#### Scenario: Paced fetching
- **WHEN** the import fetches metadata for many entries
- **THEN** requests are spaced out rather than issued in an unthrottled burst

### Requirement: Resumable import
The system SHALL make the import resumable by skipping anime already present in AnimeMetadata and continuing, rather than restarting from zero, if the app or PC restarts mid-sync.

#### Scenario: Restart mid-sync
- **WHEN** the import is interrupted and restarts
- **THEN** anime already stored are skipped and the import continues with the remaining entries

### Requirement: Visible import progress indicator
The system SHALL show a visible progress indicator during the first sync (for example "142 / 380 synced").

#### Scenario: Showing progress
- **WHEN** the initial import is running
- **THEN** the UI displays a count of synced entries versus the total

### Requirement: Corrective full re-sync upsert
The system SHALL provide a re-sync action that re-fetches the entire MAL list and upserts existing records — refreshing cached metadata and updating each UserAnimeEntry's status, episodes watched, and score from MAL — rather than skipping anime already present in AnimeMetadata. Entries with unsynced local edits (`pending_sync` = true) SHALL be left untouched so a re-sync never overwrites changes not yet pushed to MAL.

#### Scenario: Re-sync corrects previously mis-imported entries
- **WHEN** entries were imported before with wrong status/score/episodes and I run the re-sync
- **THEN** each entry's status, episodes watched, and score are updated to match MAL

#### Scenario: Re-sync preserves pending local edits
- **WHEN** an entry has `pending_sync` = true and I run the re-sync
- **THEN** that entry's user data is left unchanged


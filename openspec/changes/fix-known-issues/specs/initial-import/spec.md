## ADDED Requirements

### Requirement: Corrective full re-sync upsert
The system SHALL provide a re-sync action that re-fetches the entire MAL list and upserts existing records — refreshing cached metadata and updating each UserAnimeEntry's status, episodes watched, and score from MAL — rather than skipping anime already present in AnimeMetadata. Entries with unsynced local edits (`pending_sync` = true) SHALL be left untouched so a re-sync never overwrites changes not yet pushed to MAL.

#### Scenario: Re-sync corrects previously mis-imported entries
- **WHEN** entries were imported before with wrong status/score/episodes and I run the re-sync
- **THEN** each entry's status, episodes watched, and score are updated to match MAL

#### Scenario: Re-sync preserves pending local edits
- **WHEN** an entry has `pending_sync` = true and I run the re-sync
- **THEN** that entry's user data is left unchanged

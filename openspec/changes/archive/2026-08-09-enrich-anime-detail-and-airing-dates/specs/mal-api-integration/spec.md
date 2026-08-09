## ADDED Requirements

### Requirement: Full detail fetches persist every related-anime edge
A full-detail anime fetch SHALL persist every entry MyAnimeList returns in `related_anime`, keeping each entry's related anime id, title, picture, relation type as MAL reports it, and MAL's ordering within that relation. The system SHALL NOT discard relations it has no dedicated button for. MAL's `related_anime` response does not include the related anime's media type (its node shape has no field-selection support beyond id/title/picture), so media type is not part of this stored record.

Relation types SHALL be stored as the raw MAL wire value (e.g. `prequel`, `sequel`, `side_story`, `parent_story`, `alternative_version`, `summary`, `spin_off`, `character`, `other`), so an unrecognized upstream relation is stored rather than dropped — mirroring how the other raw MAL vocabulary fields are handled.

A full-detail fetch SHALL replace the anime's stored related-anime entries wholesale, so a relation MAL no longer reports does not linger. A lean listing fetch (season, top-anime, search) SHALL NOT write or clear related-anime entries.

#### Scenario: Storing all relations
- **WHEN** the system fetches full detail for an anime whose `related_anime` contains a prequel, a sequel, two side stories, and a summary
- **THEN** all five entries are stored with their relation types, ids, and titles

#### Scenario: Unrecognized relation type
- **WHEN** MAL returns a relation type the app has no dedicated handling for
- **THEN** the entry is stored with that raw relation value rather than being dropped

#### Scenario: Relation removed upstream
- **WHEN** an anime's stored relations include one that MAL no longer reports and full detail is fetched again
- **THEN** the stale relation is removed from storage

#### Scenario: Lean fetch leaves relations alone
- **WHEN** an anime with stored relations is refreshed through a season, top-anime, or search listing fetch
- **THEN** its stored relations are neither written nor cleared

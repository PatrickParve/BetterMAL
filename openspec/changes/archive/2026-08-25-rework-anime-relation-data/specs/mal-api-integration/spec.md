## MODIFIED Requirements

### Requirement: Full detail fetches persist every related-anime edge
A full-detail anime fetch SHALL persist every entry MyAnimeList returns in `related_anime`, keeping each entry's related anime id, title, picture, relation type as MAL reports it, MAL's ordering within that relation, and the related anime's media type. The system SHALL NOT discard relations it has no dedicated button for.

MAL's `related_anime` node **does** support nested field selection for `media_type` (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request it and store the value MAL returns. An earlier statement in this specification that the media type is unavailable from this response was incorrect; it is available and is already requested, and no separate fetch of a related anime SHALL be made to learn it.

Relation types SHALL be stored as the raw MAL wire value (e.g. `prequel`, `sequel`, `side_story`, `parent_story`, `alternative_version`, `summary`, `spin_off`, `character`, `other`), so an unrecognized upstream relation is stored rather than dropped — mirroring how the other raw MAL vocabulary fields are handled. In particular, the stored value SHALL NOT be normalized or inverted at write time: reading an anime's relations in both directions is a read-time derivation, and rewriting an edge on the way in would destroy the record of which end MAL actually asserted it, which is exactly the signal used to judge whether the edge can be trusted.

A full-detail fetch SHALL replace the anime's stored related-anime entries wholesale, so a relation MAL no longer reports does not linger. A lean listing fetch (season, top-anime, search) SHALL NOT write or clear related-anime entries.

#### Scenario: Storing all relations
- **WHEN** the system fetches full detail for an anime whose `related_anime` contains a prequel, a sequel, two side stories, and a summary
- **THEN** all five entries are stored with their relation types, ids, titles, and media types

#### Scenario: Media type comes from the same response
- **WHEN** a full-detail fetch returns `related_anime` entries
- **THEN** each entry's `node.media_type` is stored alongside it, with no additional request for any related anime

#### Scenario: Unrecognized relation type
- **WHEN** MAL returns a relation type the app has no dedicated handling for
- **THEN** the entry is stored with that raw relation value rather than being dropped

#### Scenario: Edges are stored on the side MAL asserted them
- **WHEN** MAL reports `A --sequel--> B` and reports nothing about A on B's own record
- **THEN** one edge is stored, owned by A, and no inverted `prequel` edge is written onto B

#### Scenario: Relation removed upstream
- **WHEN** an anime's stored relations include one that MAL no longer reports and full detail is fetched again
- **THEN** the stale relation is removed from storage

#### Scenario: Lean fetch leaves relations alone
- **WHEN** an anime with stored relations is refreshed through a season, top-anime, or search listing fetch
- **THEN** its stored relations are neither written nor cleared

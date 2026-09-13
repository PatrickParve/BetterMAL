## MODIFIED Requirements

### Requirement: A chosen picture survives every MAL sync
No MyAnimeList sync of any kind SHALL overwrite or clear a chosen picture. This SHALL hold for a full-detail fetch, a lean listing refresh from the Season, Year, Top or Search paths, and the weekly reconciliation alike.

Each of those paths SHALL still record MAL's own main picture, so the choice can be cleared back to it at any time.

An anime with **no** chosen picture SHALL continue to follow MAL: when MAL changes its main picture, that anime's displayed picture SHALL change with it.

#### Scenario: A full refresh does not revert a choice
- **WHEN** an anime with a chosen picture is refreshed from MAL
- **THEN** its displayed picture is still the chosen one, and MAL's own main picture is updated behind it

#### Scenario: A season listing does not revert a choice
- **WHEN** an anime with a chosen picture appears in a season listing refresh
- **THEN** its displayed picture is unchanged

#### Scenario: An unchosen anime follows MAL
- **WHEN** MAL changes the main picture of an anime for which nothing has been chosen
- **THEN** that anime's displayed picture becomes the new MAL picture

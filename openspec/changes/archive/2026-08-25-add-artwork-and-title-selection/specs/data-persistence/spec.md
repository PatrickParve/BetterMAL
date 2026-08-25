## MODIFIED Requirements

### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

The record SHALL hold **three** picture-related values rather than one:

- the **displayed picture**, which is the picture every surface of the app renders for that anime;
- **MAL's own main picture**, always overwritten by any MAL sync; and
- the **picture set**, the list of picture URLs MAL publishes for the anime, in MAL's order, stored as a list on the record itself rather than as separate rows.

The record SHALL also hold a **picture fetch timestamp**, distinct from `last_synced_at`, which is null until the picture set has been fetched. It exists so that "never asked MAL for pictures" is distinguishable from "asked, and MAL publishes one picture".

An anime SHALL be considered to have a chosen picture exactly when its displayed picture differs from MAL's main picture. No separate flag SHALL be stored for it.

#### Scenario: Storing cached metadata
- **WHEN** anime metadata is fetched from MAL
- **THEN** an AnimeMetadata record is created or updated with the fields above and its sync timestamps

#### Scenario: Unknown total episodes
- **WHEN** an anime's total episode count is not known
- **THEN** the total episodes field is stored as null

#### Scenario: English title when available
- **WHEN** MAL provides an English alternative title for an anime
- **THEN** it is stored on the AnimeMetadata record; when MAL provides none, the field is null

#### Scenario: Picture values on a freshly synced record
- **WHEN** an anime is synced from MAL and no picture has been chosen for it
- **THEN** its displayed picture and MAL's main picture hold the same URL

#### Scenario: Never-fetched pictures are marked as such
- **WHEN** an anime has never had its picture set fetched
- **THEN** its picture fetch timestamp is null and its picture set is empty

## ADDED Requirements

### Requirement: Series presentation overrides are persisted on the series
The system SHALL persist, on each Series record, an optional **chosen title** and an optional **chosen picture URL**, both null by default.

They SHALL live on the Series row rather than on any member, so that a series' identity is independent of which member happens to be its root, and so that a rebuild that changes the root does not change the chosen identity.

They SHALL survive a rebuild of the series, in the same way `SeriesMember.FavouriteRank` does — by being fields the rebuild simply never writes on the row it keeps.

#### Scenario: Defaults are null
- **WHEN** a series is built for the first time
- **THEN** its chosen title and chosen picture are both null and its identity is derived from its root member

#### Scenario: Choices persist across restarts
- **WHEN** a series is given a chosen title and picture and the application restarts
- **THEN** both are still stored and still applied

#### Scenario: A rebuild does not clear them
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are still stored on the kept series row

### Requirement: The picture-column migration leaves every anime unchosen
The migration that introduces the picture columns SHALL copy each anime's existing picture into the new MAL-main-picture column, so that on completion every stored anime has a displayed picture equal to its MAL picture and is therefore recorded as having no chosen picture.

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

## ADDED Requirements

### Requirement: The chosen-picture migration preserves every displayed picture and stamps every existing choice
The migration that introduces the chosen-picture column SHALL derive each anime's chosen picture from the state that already records one: an anime whose displayed picture differs from MAL's main picture SHALL have that displayed picture copied into its chosen-picture column, and every other anime SHALL be left with no chosen picture.

Every anime's displayed picture SHALL be exactly what it was before the migration, since copying the value it already holds re-derives the same displayed picture. No page's rendering SHALL change when the migration is applied, and no anime SHALL be left needing to be re-picked.

Every choice that exists when the migration runs — each anime chosen picture it has just recorded, and each series chosen title and chosen picture already stored — SHALL be stamped with the migration's own run time. It follows that a choice made before the migration is always older than any choice made after it on that database, and that no recorded time is absent for a choice that exists.

The migration SHALL be reversible without loss of any displayed picture: removing the new columns SHALL leave the displayed picture and MAL's main picture exactly as the migration found them.

#### Scenario: An overridden anime becomes a stored choice
- **WHEN** the migration runs against an anime whose displayed picture differs from MAL's main picture
- **THEN** that displayed picture is stored as the anime's chosen picture, its displayed picture is unchanged, and its choice is stamped with the migration's run time

#### Scenario: An unoverridden anime gets no choice
- **WHEN** the migration runs against an anime whose displayed picture equals MAL's main picture, or which has no picture at all
- **THEN** no chosen picture is stored for it and no time is recorded for it

#### Scenario: Existing series choices are stamped
- **WHEN** the migration runs against a database holding series with chosen titles and chosen pictures
- **THEN** each of those choices carries the migration's run time, and series with no choice carry no time

#### Scenario: Rendering is unchanged
- **WHEN** the migration has been applied and the app is opened
- **THEN** every page shows exactly the artwork it showed before

## MODIFIED Requirements

### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

The record SHALL hold **three** episode-count values rather than one, in the same shape as its picture-related values:

- the **effective total**, which is the figure every surface of the app reads and the one the field above describes;
- **MyAnimeList's own reported total**, written by every MyAnimeList sync and null where MyAnimeList publishes none; and
- **AniList's reported total**, written by the AniList airing sync and null where AniList has no entry for the anime or states no total.

The effective total SHALL be derived from the other two rather than written independently: it SHALL equal MyAnimeList's reported total where that is non-null, and AniList's otherwise. Every writer of either source column SHALL re-derive it in the same write, so the three values can never disagree and neither source can blank a figure the other supplied. No separate flag recording which source is in force SHALL be stored — the pair of source columns is that record.

The record SHALL hold **four** picture-related values rather than one:

- the **displayed picture**, which is the picture every surface of the app renders for that anime;
- the **chosen picture**, which is the picture chosen for that anime and is null when none has been chosen;
- **MAL's own main picture**, always overwritten by any MAL sync; and
- the **picture set**, the list of picture URLs MAL publishes for the anime, in MAL's order, stored as a list on the record itself rather than as separate rows.

The displayed picture SHALL be derived from the chosen picture and MAL's main picture rather than written independently: it SHALL equal the chosen picture where that is non-null, and MAL's main picture otherwise. Every writer of either of those two SHALL re-derive it in the same write, so the three can never disagree — the same shape the episode-count values above already use.

The record SHALL also hold a **chosen-picture timestamp**, which records when the chosen picture was last set or cleared and is null when it has never been either.

The record SHALL also hold a **picture fetch timestamp**, distinct from `last_synced_at`, which is null until the picture set has been fetched. It exists so that "never asked MAL for pictures" is distinguishable from "asked, and MAL publishes one picture".

An anime SHALL be considered to have a chosen picture exactly when its chosen-picture column is non-null. Chosen-ness SHALL NOT be inferred from the displayed picture differing from MAL's main picture.

Introducing the two source columns SHALL leave every stored anime reading exactly as it did: each existing record's MyAnimeList reported total SHALL be set to the total it already holds, and its AniList reported total SHALL be null, which re-derives the same effective total it had before.

#### Scenario: Storing cached metadata
- **WHEN** anime metadata is fetched from MAL
- **THEN** an AnimeMetadata record is created or updated with the fields above and its sync timestamps

#### Scenario: Unknown total episodes
- **WHEN** an anime's total episode count is not known to either source
- **THEN** all three episode-count values are stored as null

#### Scenario: An AniList-supplied total
- **WHEN** MyAnimeList publishes no total for an anime and AniList reports 12
- **THEN** the record's MyAnimeList reported total is null, its AniList reported total is 12, and its effective total is 12

#### Scenario: MyAnimeList's total takes precedence
- **WHEN** a record holds an AniList reported total of 12 and a MyAnimeList sync writes 13
- **THEN** its MyAnimeList reported total is 13, its AniList reported total is still 12, and its effective total is 13

#### Scenario: The migration leaves every anime unchanged
- **WHEN** the two source columns are introduced on a database of existing anime
- **THEN** every anime's MyAnimeList reported total holds the total it already had, its AniList reported total is null, and its effective total is unchanged

#### Scenario: English title when available
- **WHEN** MAL provides an English alternative title for an anime
- **THEN** it is stored on the AnimeMetadata record; when MAL provides none, the field is null

#### Scenario: Picture values on a freshly synced record
- **WHEN** an anime is synced from MAL and no picture has been chosen for it
- **THEN** its chosen picture is null and its displayed picture and MAL's main picture hold the same URL

#### Scenario: A chosen picture is stored, not inferred
- **WHEN** a picture is chosen for an anime
- **THEN** its chosen-picture column holds that URL, its chosen-picture timestamp holds the time of the choice, and its displayed picture is re-derived to that URL

#### Scenario: MAL moves under a chosen picture
- **WHEN** a MAL sync writes a new main picture for an anime whose chosen picture is non-null
- **THEN** MAL's main picture column holds the new URL, the chosen picture and its timestamp are untouched, and the displayed picture is still the chosen one

#### Scenario: Never-fetched pictures are marked as such
- **WHEN** an anime has never had its picture set fetched
- **THEN** its picture fetch timestamp is null and its picture set is empty

### Requirement: Series presentation overrides are persisted on the series
The system SHALL persist, on each Series record, an optional **chosen title** and an optional **chosen picture URL**, both null by default, each with its own **timestamp** recording when that choice was last set or cleared and null when it has never been either.

They SHALL live on the Series row rather than on any member, so that a series' identity is independent of which member happens to be its root, and so that a rebuild that changes the root does not change the chosen identity.

They SHALL survive a rebuild of the series — the rebuild simply never writes these fields on the row it keeps. Where a rebuild does move a choice — carrying it across a re-root, or adopting it from a series being absorbed — it SHALL move the choice and its timestamp together, and SHALL NOT restamp either as a change of its own.

#### Scenario: Defaults are null
- **WHEN** a series is built for the first time
- **THEN** its chosen title, chosen picture and both timestamps are null, and its identity is derived from its root member

#### Scenario: Choices persist across restarts
- **WHEN** a series is given a chosen title and picture and the application restarts
- **THEN** both are still stored, still applied, and still carry the times they were chosen

#### Scenario: A rebuild does not clear them
- **WHEN** a series with a chosen title and picture is rebuilt
- **THEN** both are still stored on the kept series row, with their original timestamps

### Requirement: The picture-column migration leaves every anime unchosen
The migration that introduces the picture columns SHALL copy each anime's existing picture into the new MAL-main-picture column, so that on completion every stored anime has a displayed picture equal to its MAL picture and no anime has a chosen picture stored for it.

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

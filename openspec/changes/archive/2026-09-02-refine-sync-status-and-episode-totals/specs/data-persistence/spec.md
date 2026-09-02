## MODIFIED Requirements

### Requirement: AnimeMetadata cached entity
The system SHALL persist an AnimeMetadata record per anime containing at minimum: MAL id, title, English title (when MAL provides one), picture, MAL score, type, airing status, total episodes (nullable, where null means unknown/still airing), aired-from/to dates, studio, broadcast day and time (JST), popularity rank, `last_synced_at`, and `last_score_synced_at`.

The record SHALL hold **three** episode-count values rather than one, mirroring how it already holds three picture-related values:

- the **effective total**, which is the figure every surface of the app reads and the one the field above describes;
- **MyAnimeList's own reported total**, written by every MyAnimeList sync and null where MyAnimeList publishes none; and
- **AniList's reported total**, written by the AniList airing sync and null where AniList has no entry for the anime or states no total.

The effective total SHALL be derived from the other two rather than written independently: it SHALL equal MyAnimeList's reported total where that is non-null, and AniList's otherwise. Every writer of either source column SHALL re-derive it in the same write, so the three values can never disagree and neither source can blank a figure the other supplied. No separate flag recording which source is in force SHALL be stored — the pair of source columns is that record.

The record SHALL hold **three** picture-related values rather than one:

- the **displayed picture**, which is the picture every surface of the app renders for that anime;
- **MAL's own main picture**, always overwritten by any MAL sync; and
- the **picture set**, the list of picture URLs MAL publishes for the anime, in MAL's order, stored as a list on the record itself rather than as separate rows.

The record SHALL also hold a **picture fetch timestamp**, distinct from `last_synced_at`, which is null until the picture set has been fetched. It exists so that "never asked MAL for pictures" is distinguishable from "asked, and MAL publishes one picture".

An anime SHALL be considered to have a chosen picture exactly when its displayed picture differs from MAL's main picture. No separate flag SHALL be stored for it.

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
- **THEN** its displayed picture and MAL's main picture hold the same URL

#### Scenario: Never-fetched pictures are marked as such
- **WHEN** an anime has never had its picture set fetched
- **THEN** its picture fetch timestamp is null and its picture set is empty

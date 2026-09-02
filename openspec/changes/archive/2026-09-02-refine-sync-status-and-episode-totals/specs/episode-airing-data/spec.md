## MODIFIED Requirements

### Requirement: AniList is the sole source of episode timing
The system SHALL derive every per-episode air instant, every aired-so-far episode count, and every next-episode instant exclusively from AniList. No MyAnimeList value SHALL feed an episode-timing field. MyAnimeList SHALL remain the **primary** source for the anime's total episode count, its airing status, and all static metadata (title, synopsis, genres, cover art, studio, score, rank) — with the single exception of a total episode count MyAnimeList does not publish, which AniList may supply per "AniList supplies a total episode count MyAnimeList does not have".

The system SHALL NOT compute an episode number or an aired-episode count from elapsed time since a start date, from a weekly broadcast cadence, or from any other projection. A value that cannot be read from stored AniList data SHALL be reported as unknown. A total episode count SHALL likewise never be projected — neither from the number of stored airing rows, nor from a next-episode number, nor from anything else. It is reported only where a source states it outright.

#### Scenario: Episode timing comes from AniList
- **WHEN** an aired-episode count, a per-episode air time, or a next-episode instant is produced for any view
- **THEN** its value traces to stored AniList airing data and to no MyAnimeList field

#### Scenario: Static metadata still comes from MyAnimeList
- **WHEN** an anime's title, synopsis, genres, cover art, studio, or airing status is displayed
- **THEN** the value comes from the cached MyAnimeList record, unchanged by this capability

#### Scenario: A published total still comes from MyAnimeList
- **WHEN** MyAnimeList publishes a total episode count for an anime
- **THEN** that figure is the one displayed, whatever AniList reports

#### Scenario: No projection when data is absent
- **WHEN** an aired-episode count is requested for an anime that has no stored airing rows
- **THEN** the count is reported as unknown, and is not estimated from the anime's start date, broadcast day, or broadcast time

#### Scenario: A total is never counted from airing rows
- **WHEN** an anime has 7 stored airing rows and neither MyAnimeList nor AniList states a total
- **THEN** its total episode count is reported as unknown rather than as 7

## ADDED Requirements

### Requirement: AniList supplies a total episode count MyAnimeList does not have

Where MyAnimeList publishes no total episode count for an anime and AniList states one, the system SHALL use AniList's figure as that anime's total episode count. Every surface that reads a total — the progress bar and `watched/total` label, the completion rules of the `list-editing` capability, the series and profile totals — SHALL read the resulting figure without distinguishing where it came from, and no provenance SHALL be displayed.

**Precedence.** MyAnimeList's figure SHALL win whenever it has one. AniList's SHALL be used only while MyAnimeList's is absent. The system SHALL record each source's own reported figure separately from the effective total it resolves, so that:

- a later MyAnimeList figure supersedes an AniList one, even where the two differ;
- a MyAnimeList refresh that still reports no total SHALL NOT blank a total already supplied by AniList;
- a later AniList figure replaces an earlier AniList one, and never displaces a MyAnimeList figure;
- either source retracting its figure leaves the other's standing.

**Where it is read.** The figure SHALL be taken from the AniList media lookup and airing-schedule fetch the system already makes for an anime, adding no request of its own. It follows that a total arrives this way only for anime the airing sync visits — the anime in my list — and that an anime AniList has no entry for, or for which AniList states no total either, keeps an unknown total and continues to read `watched/?`.

**Boundary.** This SHALL be the only anime field AniList supplies. AniList SHALL NOT be read as a source for airing status, premiere or end dates, broadcast slot, title, or any other cached metadata, all of which remain MyAnimeList's alone.

#### Scenario: AniList fills an unknown total

- **WHEN** an anime in my list has no MyAnimeList total, AniList reports 12, and its airing data is refreshed
- **THEN** its total episode count reads 12 and its progress shows `watched/12`

#### Scenario: MyAnimeList's total wins

- **WHEN** MyAnimeList publishes 13 for an anime and AniList reports 12
- **THEN** the anime's total episode count reads 13

#### Scenario: A later MyAnimeList total supersedes AniList's

- **WHEN** an anime's total was supplied by AniList as 12 and a later MyAnimeList refresh publishes 13
- **THEN** the anime's total episode count reads 13

#### Scenario: A MyAnimeList refresh does not blank an AniList total

- **WHEN** an anime's total was supplied by AniList as 12 and a later MyAnimeList refresh again reports no total
- **THEN** the anime's total episode count still reads 12

#### Scenario: AniList revises its own figure

- **WHEN** an anime's total was supplied by AniList as 12, MyAnimeList still publishes none, and a later AniList refresh reports 13
- **THEN** the anime's total episode count reads 13

#### Scenario: Neither source knows

- **WHEN** MyAnimeList publishes no total for an anime and AniList states none either
- **THEN** the anime's total episode count stays unknown and its progress reads `watched/?`

#### Scenario: No extra request is made

- **WHEN** an anime's AniList airing data is refreshed
- **THEN** its total episode count is read from the responses that refresh already requests, with no additional AniList request

#### Scenario: AniList supplies nothing else

- **WHEN** AniList reports an airing status, a start date, or a title that differs from MyAnimeList's
- **THEN** none of them is written to the anime's cached record

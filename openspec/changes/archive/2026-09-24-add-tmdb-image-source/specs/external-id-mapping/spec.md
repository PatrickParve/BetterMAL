## ADDED Requirements

### Requirement: MAL ids are mapped to TMDB and IMDb ids from a third-party list
The system SHALL keep, per MyAnimeList id, the matching TMDB and IMDb identifiers. It SHALL take them from the community-maintained Fribb anime-lists mapping file (`anime-list-mini.json`), fetched with a plain HTTP GET and no authentication. TMDB offers no lookup by MyAnimeList id, and this file is the source of the mapping.

From each entry of the file the system SHALL keep only these four values:
- the TMDB TV id (`themoviedb_id.tv`)
- the TMDB season number the entry corresponds to (`season.tmdb`), kept only alongside a TMDB TV id
- the TMDB movie ids (`themoviedb_id.movie`, a list)
- the IMDb ids (`imdb_id`, a list)

Everything else in an entry SHALL be discarded. That includes its type, its AniList, AniDB and other site ids, its TheTVDB ids and its episode offsets. The downloaded file itself SHALL NOT be kept once a sync has read it.

An IMDb id SHALL be kept only when it is `tt` followed by digits. Empty strings and any other value SHALL be dropped.

The system SHALL ignore an entry with no MyAnimeList id. An entry whose MyAnimeList id carries none of the four values above SHALL leave no mapping for that id.

When the same MyAnimeList id appears in more than one entry, the first entry SHALL be used and the later ones ignored.

The mapping SHALL be keyed on the MyAnimeList id alone, whether or not the app holds any other record of that anime.

#### Scenario: A TV season is kept with its TMDB season
- **WHEN** the file maps MAL id 16498 to TMDB TV id 1429, TMDB season 1, and IMDb id `tt2560140`
- **THEN** the mapping for 16498 holds TV id 1429, season 1, no movie ids, and IMDb id `tt2560140`

#### Scenario: A movie is kept with its movie ids
- **WHEN** the file maps MAL id 40456 to TMDB movie ids `[635302]` and IMDb id `tt11032374`, with a TheTVDB season but no TMDB season
- **THEN** the mapping for 40456 holds movie ids `[635302]`, no TV id, no season, and IMDb id `tt11032374`

#### Scenario: Everything else is discarded
- **WHEN** an entry also carries a type, AniList, AniDB and Kitsu ids, a TheTVDB id and an episode offset
- **THEN** none of those values is stored anywhere

#### Scenario: An empty IMDb value is dropped
- **WHEN** an entry's IMDb list is `["tt1092390", ""]`
- **THEN** only `tt1092390` is kept

#### Scenario: The first duplicate wins
- **WHEN** two entries carry the same MAL id with different TMDB ids
- **THEN** the mapping for that MAL id holds the first entry's values

#### Scenario: An entry with no MAL id is ignored
- **WHEN** an entry carries TMDB and IMDb ids but no MAL id
- **THEN** it contributes nothing to the mapping

#### Scenario: A MAL id with nothing to map
- **WHEN** an entry carries a MAL id but no TMDB id and no valid IMDb id
- **THEN** no mapping is stored for that MAL id

### Requirement: The mapping is refreshed weekly on the existing elapsed-time tick
The system SHALL refresh the mapping when no refresh has ever succeeded, or when the last successful refresh is more than 7 days old.

It SHALL check this on the same periodic tick that evaluates airing-data refreshes. That tick runs as soon as the application starts, then hourly while it runs. The system SHALL NOT add a separate scheduler for the mapping.

The time of the last successful refresh SHALL be stored. A restart SHALL therefore neither reset the weekly clock nor skip a refresh that fell due while the application was stopped.

A failed refresh SHALL leave the previous mapping in place and SHALL NOT count as a successful refresh. It SHALL be retried no sooner than 6 hours after the failed attempt.

The mapping refresh and the airing-data work on the same tick SHALL NOT affect each other: neither SHALL delay the other, and a failure in one SHALL NOT stop the other.

#### Scenario: First start
- **WHEN** the application starts and no mapping refresh has ever succeeded
- **THEN** the mapping file is downloaded and the mapping stored on that first tick

#### Scenario: Started within the week
- **WHEN** the application starts three days after the last successful refresh
- **THEN** no download is made

#### Scenario: Started after a long stop
- **WHEN** the application starts ten days after the last successful refresh
- **THEN** the mapping is refreshed on the first tick

#### Scenario: Falling due while running
- **WHEN** the application has run continuously and more than seven days have passed since the last successful refresh
- **THEN** the mapping is refreshed on the next hourly tick, without a restart

#### Scenario: A failed download keeps the old mapping
- **WHEN** the download fails
- **THEN** the stored mapping and the time of the last successful refresh are unchanged, and no further attempt is made for six hours

#### Scenario: Airing work is unaffected
- **WHEN** the mapping refresh fails on a tick
- **THEN** that tick's airing-data work still runs

### Requirement: A refresh replaces the mapping as a whole, guarded against a damaged file
A successful refresh SHALL make the stored mapping match the file. MyAnimeList ids new to the file SHALL be added. Changed ids SHALL be updated. Ids the file no longer maps SHALL be removed. All of this SHALL happen in one transaction, so no reader ever sees a half-applied refresh.

The system SHALL abandon a refresh when either of the following holds:
- the file cannot be downloaded, read or parsed
- the file would leave fewer than half as many mapped MyAnimeList ids as are currently stored

An abandoned refresh SHALL leave the stored mapping untouched and SHALL be recorded as a failed attempt.

#### Scenario: A changed mapping is updated
- **WHEN** the file now maps a MAL id to a different TMDB TV id than the one stored
- **THEN** after the refresh the stored mapping for that MAL id holds the new TV id

#### Scenario: A vanished mapping is removed
- **WHEN** the file no longer maps a MAL id that was mapped before
- **THEN** after the refresh no mapping remains for it

#### Scenario: A damaged file changes nothing
- **WHEN** the downloaded file is truncated and fails to parse
- **THEN** the stored mapping is unchanged and the attempt is recorded as failed

#### Scenario: A suspiciously small file changes nothing
- **WHEN** 8,000 MAL ids are mapped and the new file would map only 2,000
- **THEN** the refresh is abandoned and the 8,000 stored mappings remain

### Requirement: The mapping sync calls nothing but the mapping file
Refreshing the mapping SHALL make one request only, the download of the mapping file. It SHALL NOT call TMDB, IMDb or AniList, and SHALL NOT fetch any image. Images are fetched only when a page needs them (`tmdb-artwork`).

#### Scenario: A refresh fetches no images
- **WHEN** the mapping refreshes and newly maps 500 of the anime in my list
- **THEN** only the mapping file is downloaded, and no TMDB request is made as a result

### Requirement: A custom mapping file supplements and corrects the synced mapping
The system SHALL read a custom id-mapping file that I edit by hand, and SHALL apply it on top of the synced mapping wherever the mapping is read. That covers the IMDb links on the detail and series pages, and the TMDB sets an anime or a series draws from (`tmdb-artwork`).

The file SHALL hold a JSON array of entries shaped like the source file's. An entry names a MyAnimeList id (`mal_id`) and any of: the TMDB TV id (`themoviedb_id.tv`), the TMDB movie ids (`themoviedb_id.movie`), the TMDB season number (`season.tmdb`, meaningful only with a TV id) and the IMDb ids (`imdb_id`). An entry SHALL also accept two fields of its own:
- `override`, false unless set
- `note`, free text for whoever edits the file, quoted in log messages and used for nothing else

An entry's values SHALL be checked as the source file's are: an IMDb id is `tt` and digits, and a season counts only with a TV id. Comments and trailing commas SHALL be tolerated, since the file is edited by hand.

An entry provides up to two groups: its TMDB ids with the season, and its IMDb ids. For each group it provides:
- **By default**, the entry SHALL apply only while the synced mapping holds nothing for that group. Once the synced mapping holds something for it, the synced values SHALL apply and the entry's SHALL be ignored, so a source that catches up needs no change to the file.
- **With `override` true**, the entry SHALL apply whatever the synced mapping holds, so that a value the source has wrong can be corrected.

A group the entry does not provide SHALL be read from the synced mapping. A MyAnimeList id with no synced mapping at all SHALL take its whole mapping from the entry.

The system SHALL NOT write the file's entries into the synced mapping, and no refresh SHALL remove or change them.

The file SHALL be re-read when it changes, with no restart. A missing file SHALL mean no entries. When the file cannot be parsed, the entries last read SHALL stay in force, and the cause SHALL be logged. An entry that is invalid, or that repeats an earlier entry's MyAnimeList id, SHALL be skipped and logged, and the other entries SHALL still apply.

After each successful refresh the system SHALL log every entry that has become unnecessary, meaning that applying it would change nothing because the synced mapping already holds the same values, so that I know I can remove it.

#### Scenario: A missing id is filled
- **WHEN** the synced mapping holds nothing for MAL id 60568 and the file gives TMDB TV id 280564, season 1 and IMDb id `tt38754770`
- **THEN** that anime's IMDb link and its TMDB sets come from the file

#### Scenario: The source catches up
- **WHEN** a later refresh gives MAL id 60568 its own TMDB TV id
- **THEN** the synced ids apply and the file's entry is ignored, with no change to the file

#### Scenario: One group at a time
- **WHEN** the synced mapping holds only an IMDb id for an anime and the file gives it a TV id
- **THEN** the anime uses the file's TV id and the synced IMDb id

#### Scenario: An override corrects a wrong season
- **WHEN** the synced mapping says MAL id 11741 is TMDB TV 45845 season 1, and an override entry says season 2
- **THEN** the anime draws its Season scope from season 2

#### Scenario: An override changes only the groups it names
- **WHEN** an override entry gives a TV id and season but no IMDb id
- **THEN** the anime keeps its synced IMDb ids

#### Scenario: A refresh leaves the file alone
- **WHEN** a refresh runs, whether or not the source lists a MAL id that has an entry in the file
- **THEN** no entry is removed or changed, and the anime's mapping is the same as before the refresh

#### Scenario: An edit applies without a restart
- **WHEN** I add an entry to the file while the application runs
- **THEN** the entry applies within seconds, with no restart

#### Scenario: A typo keeps the last good entries
- **WHEN** I save the file with a syntax error
- **THEN** the entries last read stay in force, and the log names the problem

#### Scenario: A bad entry loses only itself
- **WHEN** one entry has an IMDb id that is not `tt` and digits and the file's other entries are valid
- **THEN** the other entries apply, and the log names the one skipped

#### Scenario: A missing file is fine
- **WHEN** there is no custom file
- **THEN** the mapping is exactly the synced mapping

#### Scenario: An unnecessary entry is reported
- **WHEN** a refresh gives a MAL id the same TMDB ids that its default entry supplies
- **THEN** the log says the entry is no longer needed, naming the MAL id and its note


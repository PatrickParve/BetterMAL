# episode-airing-data Specification

## Purpose
TBD - created by change fix-airing-episode-accuracy. Update Purpose after archive.
## Requirements
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

### Requirement: Persisted per-episode airing rows
The system SHALL store one row per episode per anime, holding the anime id, the episode number, and that episode's air instant as reported by AniList. Rows SHALL persist across application restarts.

The stored set SHALL include episodes whose air instant is in the future as well as those already past, so that a single stored record backs the past view, the upcoming view, and the next-episode countdown. An episode SHALL be treated as aired only once its stored air instant has passed.

#### Scenario: Rows survive a restart
- **WHEN** the application restarts after airing rows have been stored
- **THEN** aired counts and schedule slots read the same values as before the restart, with no warm-up period during which they differ

#### Scenario: Future episodes are stored
- **WHEN** AniList reports an episode scheduled to air next week
- **THEN** a row is stored for it, and it is not counted as aired until its stored air instant has passed

#### Scenario: Aired count is the highest past episode
- **WHEN** an anime has stored rows for episodes 1 through 8 where episodes 1 through 5 have air instants in the past and 6 through 8 in the future
- **THEN** the aired-so-far count is 5

#### Scenario: Aired count is not clamped by the MyAnimeList total
- **WHEN** an anime has a stored row for episode 13 whose air instant has passed, while its cached MyAnimeList total episode count is 12
- **THEN** the aired-so-far count is 13

### Requirement: Aired-episode counts are readable in bulk
The system SHALL expose an aired-so-far episode count for many anime at once, resolved from stored airing rows in a single database read rather than one read per anime, so a whole-list surface can resolve every count it needs without issuing a query per entry.

The bulk read SHALL return the same value the single-anime read returns for each anime — the highest episode number whose stored air instant is at or before the given instant — and SHALL apply no clamping, no projection, and no estimation, exactly as the single-anime read does. An anime with no aired row SHALL be reported as unknown rather than as zero, keeping "nothing stored" distinguishable from "nothing aired yet".

Requesting counts for no anime SHALL be a valid call returning no counts, without touching the database.

#### Scenario: Many counts, one read
- **WHEN** aired counts are requested for two hundred anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's aired count is read on its own and again as part of a bulk request at the same instant
- **THEN** both reads report the same count

#### Scenario: An anime with no stored rows is unknown
- **WHEN** a bulk request includes an anime that has no stored airing rows
- **THEN** that anime is reported as unknown rather than as zero

#### Scenario: An empty request is valid
- **WHEN** aired counts are requested for an empty set of anime
- **THEN** no counts are returned and no query is issued

### Requirement: A local date's episode is readable in bulk
The system SHALL expose, for many anime at once, the episode airing on a given local date — the same answer the single-anime read gives, resolved from stored airing rows in a single database read rather than one read per anime — so a whole-list surface can decide what airs today without issuing a query per entry.

The bulk read SHALL report, per anime, the local time of day and episode number of that anime's earliest stored row falling within the given local date, using the same local-day boundaries the single-anime read uses, so that a day shifted by daylight saving is covered identically by both. An anime with no stored row on that date SHALL be absent from the result rather than present with an empty value, keeping "nothing airs" distinguishable from "an episode airs at midnight". No episode number SHALL be projected from a broadcast cadence, exactly as for the single-anime read.

Requesting the date's episodes for no anime SHALL be a valid call returning nothing, without touching the database.

#### Scenario: Many dates, one read
- **WHEN** the episode airing today is requested for six hundred anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's episode for a local date is read on its own and again as part of a bulk request for the same date
- **THEN** both reads report the same local time and episode number

#### Scenario: An anime with nothing on that date is absent
- **WHEN** a bulk request includes an anime with no stored row falling on the requested local date
- **THEN** that anime is absent from the result

#### Scenario: Two rows on one date
- **WHEN** an anime has two stored rows falling within the same local date
- **THEN** the bulk read reports the earlier of the two, as the single-anime read does

#### Scenario: An empty request is valid
- **WHEN** the date's episodes are requested for an empty set of anime
- **THEN** the call succeeds, returns nothing, and issues no database read

### Requirement: Next airing instants are readable in bulk
The system SHALL expose the next stored air instant for many anime at once, resolved in a single database read rather than one read per anime, so a surface showing a countdown per card can resolve every one of them without a query apiece.

The bulk read SHALL return, per anime, the earliest stored air instant strictly after the given instant — the same value the single-anime read returns. An anime with no future stored row SHALL be absent from the result rather than present with a placeholder instant, keeping "nothing scheduled" distinguishable from any real instant.

Requesting next instants for no anime SHALL be a valid call returning nothing, without touching the database.

#### Scenario: Many instants, one read
- **WHEN** the next airing instant is requested for forty anime at once
- **THEN** they are resolved in a single database read

#### Scenario: Bulk agrees with single
- **WHEN** an anime's next airing instant is read on its own and again as part of a bulk request at the same instant
- **THEN** both reads report the same instant

#### Scenario: An anime with no future row is absent
- **WHEN** a bulk request includes an anime whose stored rows all lie in the past
- **THEN** that anime is absent from the result

#### Scenario: An empty request is valid
- **WHEN** next instants are requested for an empty set of anime
- **THEN** the call succeeds, returns nothing, and issues no database read

### Requirement: A refresh replaces an anime's stored rows wholesale
When a refresh for one anime returns airing data, the system SHALL replace that anime's entire stored row set with the returned data in a single atomic operation, rather than merging the new data into the existing rows.

A refresh that returns no airing data — because AniList has no entry for that anime, or because the request failed — SHALL leave the existing stored rows untouched.

#### Scenario: Renumbered episodes leave no stale rows
- **WHEN** a refresh returns an episode numbering that differs from what is stored, such that an episode number present before is absent from the new data
- **THEN** the previously stored row for that episode number is gone after the refresh, and the aired count reflects only the new numbering

#### Scenario: A failed fetch preserves stored data
- **WHEN** a refresh for an anime with stored rows returns no airing data
- **THEN** the anime's stored rows are unchanged and its aired count is unaffected

#### Scenario: Replacement is atomic
- **WHEN** a read for an anime's airing rows occurs while that anime's rows are being replaced
- **THEN** the read observes either the complete previous row set or the complete new one, never a partial set

### Requirement: One-time full-history backfill
The system SHALL perform a one-time backfill, on first start after this capability is deployed, that re-fetches the complete airing history for every anime in my list and overwrites the stored rows for each. The backfill SHALL page through AniList's full schedule for an anime rather than fetching only a recent window, so that a long-running series has rows for its whole run.

The backfill SHALL be recorded as complete only once every targeted anime has been fetched, and SHALL resume rather than restart if the application stops partway. Once recorded complete, it SHALL NOT run again.

The backfill SHALL log each targeted anime for which AniList returned no airing data, identified by title and id, so that coverage gaps are visible.

#### Scenario: Existing bad data is corrected
- **WHEN** the backfill runs for an anime whose stored airing data was previously wrong
- **THEN** its stored rows are replaced with AniList's data and its aired count reflects the corrected rows

#### Scenario: Long-running series gets full history
- **WHEN** the backfill runs for a series with more episodes than fit in a single AniList response page
- **THEN** it pages until AniList reports no further pages, and rows are stored for every episode returned

#### Scenario: Backfill resumes after an interruption
- **WHEN** the application restarts while the backfill is partway through
- **THEN** the backfill continues with the anime it has not yet fetched rather than re-fetching those already done, and is recorded complete only after all of them are fetched

#### Scenario: Backfill runs only once
- **WHEN** the application starts after the backfill has been recorded complete
- **THEN** no backfill runs

#### Scenario: Coverage gaps are logged
- **WHEN** the backfill targets an anime for which AniList returns no airing data
- **THEN** that anime's title and id are written to the log

### Requirement: Elapsed-time refresh rather than fixed-schedule polling
The system SHALL trigger a refresh of airing data for my-list anime that are currently airing or not yet aired based on the time elapsed since the last successful refresh, evaluated periodically while the application is running, rather than at a fixed time of day or on a fixed polling interval.

A refresh SHALL be triggered when there has been no successful refresh, when the last successful refresh was more than roughly a day ago, or when the last successful refresh fell on an earlier local calendar day than today. This SHALL cover the case of the application starting after having been stopped, without requiring a separate start-up rule.

The system SHALL NOT refresh airing data on a fixed interval independent of whether the data could have changed.

#### Scenario: Start after being stopped overnight
- **WHEN** the application starts and the last successful airing refresh was on an earlier local calendar day
- **THEN** a refresh of all currently-airing and not-yet-aired my-list anime is triggered

#### Scenario: Start after a refresh already ran today
- **WHEN** the application starts and a successful airing refresh already ran earlier the same local calendar day
- **THEN** no refresh is triggered by the start alone

#### Scenario: Day rolls over while running
- **WHEN** the application has been running continuously and roughly a day has passed since the last successful refresh
- **THEN** a refresh is triggered without requiring a restart

#### Scenario: Missed calendar day self-corrects
- **WHEN** the application was not running for an entire calendar day and then starts
- **THEN** a refresh is triggered, rather than the missed day being skipped

### Requirement: Event-driven refresh triggers
The system SHALL additionally refresh airing data on each of the following events, independent of the elapsed-time schedule:

- An anime is added to my list — that one anime is refreshed immediately, whatever its airing status. A finished anime is included, so its full episode history is fetched on add rather than waiting for the one-time backfill or a manual full refresh.
- The on-demand refresh action is triggered on an anime's detail page — that one anime is refreshed immediately.
- The current viewing season changes at a Winter/Spring/Summer/Fall boundary — all currently-airing and not-yet-aired my-list anime are refreshed.

Refreshing on add SHALL NOT delay the response to the add request.

The add trigger SHALL fire only when the add creates a new list entry; editing an existing entry (status, episodes watched, score, rewatch count) SHALL NOT trigger an airing refresh.

#### Scenario: Adding an airing anime
- **WHEN** an anime whose airing status is currently airing is added to my list
- **THEN** its airing data is fetched immediately, and the add request completes without waiting for that fetch

#### Scenario: Adding an upcoming anime
- **WHEN** an anime whose airing status is not yet aired is added to my list
- **THEN** its airing data is fetched immediately

#### Scenario: Adding a finished anime
- **WHEN** an anime that has finished airing is added to my list
- **THEN** its airing data is fetched immediately, giving it its full stored episode history without waiting for a backfill pass

#### Scenario: Adding an anime with no known airing status
- **WHEN** an anime whose airing status is not recorded is added to my list
- **THEN** its airing data is fetched immediately, rather than the add being skipped for lack of a status

#### Scenario: Editing an existing entry
- **WHEN** the status, episodes watched, score, or rewatch count of an anime already in my list is changed
- **THEN** no airing refresh is triggered by that edit

#### Scenario: Season boundary
- **WHEN** the current viewing season changes to a new Winter, Spring, Summer, or Fall quarter
- **THEN** all currently-airing and not-yet-aired my-list anime have their airing data refreshed

### Requirement: Recheck cadence for anime with incomplete airing data
The system SHALL track, per anime, when it is next due for a recheck, derived from AniList's reported next airing episode.

When AniList reports a next airing episode at instant T, the anime SHALL be due for recheck at one month before T, at one week before T, and on the day of T — whichever of those is next in the future.

When the anime is still airing and AniList reports no next airing episode at all, the system SHALL treat its data as incomplete immediately and set it due for recheck every three days, rather than waiting for a month-out checkpoint.

When the anime is still airing past its reported next-episode instant and no newer airing data has arrived, the system SHALL likewise set it due for recheck every three days.

An anime that has finished airing and whose last stored episode is in the past SHALL have no recheck due.

#### Scenario: Checkpoints before a known next episode
- **WHEN** AniList reports a next airing episode two months from now
- **THEN** the anime is set due for recheck one month before that instant

#### Scenario: No next airing episode reported
- **WHEN** AniList reports no next airing episode for an anime that is still airing
- **THEN** the anime is treated as having incomplete data immediately and is set due for recheck in three days, not in one month

#### Scenario: Past the expected instant with no new data
- **WHEN** an anime's reported next-episode instant has passed and a recheck returns no newer airing data while the anime is still airing
- **THEN** it is set due for recheck three days later

#### Scenario: Due anime is rechecked
- **WHEN** an anime's recheck due time has passed and the elapsed-time refresh has not already covered it
- **THEN** its airing data is refreshed

#### Scenario: Finished anime is not rechecked
- **WHEN** an anime has finished airing and its last stored episode aired in the past
- **THEN** it has no recheck due time and is not refreshed by the recheck queue

### Requirement: AniList request pacing
The system SHALL pace AniList requests to stay within AniList's published rate limit, and SHALL back off and retry when AniList reports the limit has been exceeded. A failure to fetch one anime SHALL NOT abort the refresh of the remaining anime in the pass.

The system SHALL store the AniList identifier resolved for an anime so that later refreshes for that anime do not repeat the identifier lookup. An anime AniList has no entry for SHALL be recorded as such, so the lookup is not retried on every pass.

#### Scenario: Rate limit reached
- **WHEN** AniList reports that the request rate limit has been exceeded
- **THEN** the system waits and retries rather than dropping the anime from the pass

#### Scenario: One anime fails
- **WHEN** the fetch for one anime in a refresh pass fails
- **THEN** the failure is logged and the pass continues with the remaining anime

#### Scenario: Identifier lookup is not repeated
- **WHEN** an anime whose AniList identifier was already resolved is refreshed again
- **THEN** no identifier lookup request is made and only the schedule request is issued

#### Scenario: Anime unknown to AniList
- **WHEN** AniList has no entry matching an anime's MyAnimeList id
- **THEN** that fact is recorded, and later refresh passes do not re-issue the identifier lookup for it

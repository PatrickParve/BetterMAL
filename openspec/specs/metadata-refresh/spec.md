# metadata-refresh Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: No live API calls on page render
The system SHALL never call the MAL API live during a page render; cached metadata refresh SHALL happen only via scheduled background work or explicit on-demand actions.

#### Scenario: Rendering does not trigger a refresh
- **WHEN** a page renders cached anime data
- **THEN** no live MAL API call is made as part of that render

### Requirement: Scheduled tiered staleness refresh for my-list anime
The system SHALL run a scheduled background refresh limited to anime with a corresponding UserAnimeEntry (my list), selecting refresh candidates by staleness tier measured from that anime's last **full-detail** fetch:

| Tier | Refresh interval |
| --- | --- |
| Currently airing, or not yet aired | 1 day |
| Finished, aired within the last year | 3 days |
| Finished, aired 1–2 years ago | 14 days |
| Finished, aired more than 2 years ago | 28 days |

Each scheduled refresh SHALL be a full-detail fetch, not a score-only one, and SHALL update everything a full-detail fetch writes — related anime, airing status, episode counts, ranks, genres, synopsis — together with the last-full-detail-fetch timestamp. Relation data in particular SHALL NOT be write-once: an anime whose relations were correct when first imported SHALL pick up a newly-announced sequel on its tier without any user action.

A full-detail fetch and a single-field fetch cost the same one request against MAL's per-request rate limit, so refreshing everything on these tiers SHALL NOT increase the number of API calls the job makes.

An anime that has never had a full-detail fetch SHALL be treated as maximally stale and SHALL sort ahead of every tiered candidate.

#### Scenario: Airing anime refreshed daily
- **WHEN** a my-list anime is currently airing and its last full-detail fetch is older than 1 day
- **THEN** the scheduled job refreshes it

#### Scenario: A tiered refresh updates relations
- **WHEN** the scheduled job refreshes an anime whose MAL relations have changed since its last fetch
- **THEN** its stored relation set matches MAL's and its last-full-detail-fetch timestamp advances

#### Scenario: Recently finished anime refreshed every few days
- **WHEN** a my-list anime finished airing within the last year and its last full-detail fetch is older than 3 days
- **THEN** the scheduled job refreshes it

#### Scenario: Older anime refreshed on their longer tiers
- **WHEN** a my-list anime finished airing three years ago and its last full-detail fetch is newer than 28 days
- **THEN** the scheduled job does not refresh it on that pass

#### Scenario: Never-fetched anime go first
- **WHEN** the job selects candidates and some my-list anime have never had a full-detail fetch
- **THEN** those are selected before anime that are merely past their tier interval

#### Scenario: Non-my-list anime excluded from the tiers
- **WHEN** the scheduled job selects refresh candidates
- **THEN** anime without a corresponding UserAnimeEntry are never included, regardless of staleness

### Requirement: Nightly batch cap ordered by staleness
The system SHALL cap the nightly refresh job at a fixed number of API calls per run (e.g. 500) and SHALL process the most-stale eligible candidates first across all tiers, carrying any overflow to the next night automatically without extra bookkeeping.

#### Scenario: Most-stale candidates processed first
- **WHEN** the nightly job selects candidates within the batch cap
- **THEN** candidates are ordered by longest time since last refresh first

#### Scenario: Overflow carries to the next night
- **WHEN** more candidates are eligible than the nightly batch cap allows
- **THEN** the remainder are simply refreshed on a subsequent night since they remain the most-stale candidates

### Requirement: Cheap, spread-out refresh requests
The system SHALL pace and cap its scheduled refresh work by **request count**, not by field selection: MyAnimeList's rate limit is per request, so asking for one field and asking for the full detail record cost exactly the same against it, and requesting less buys nothing.

Refreshes SHALL be spread across the refresh window in small batches under the existing request pacer rather than bursting, and SHALL remain subject to the per-run batch cap.

The system SHALL NOT select a reduced field set for the purpose of making a refresh cheaper. Field selection SHALL be decided solely by what the system needs to store.

#### Scenario: Refresh requests are the unit of cost
- **WHEN** the scheduled job refreshes an anime
- **THEN** it issues one request for that anime's full detail record, counting one against the pacer and the batch cap

#### Scenario: Pacing and cap unchanged by the fuller payload
- **WHEN** the job runs a full pass under the new full-detail fetches
- **THEN** the request pacing, tick interval, and per-day cap are the same as they were under score-only fetches

#### Scenario: Refreshes are spread, not bursted
- **WHEN** many anime are due at once
- **THEN** their requests are spread across the window under the pacer rather than issued back-to-back

### Requirement: On-demand single-anime refresh
The system SHALL offer an on-demand refresh action for a single anime that, at the moment it is requested, performs one MyAnimeList API call for that one anime and additionally re-fetches that one anime's airing data from AniList. Neither call SHALL touch any other anime.

When the MyAnimeList call succeeds but the AniList fetch fails, the action SHALL still report success, update the cached record and sync timestamps, and log the AniList failure.

#### Scenario: Refreshing one anime on demand
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** the system makes one MyAnimeList API call for that anime, re-fetches that anime's airing data from AniList, and updates its cached record and sync timestamps

#### Scenario: Scope stays at one anime
- **WHEN** the user triggers refresh on a single anime's detail page
- **THEN** no other anime's cached record or airing data is fetched or modified

#### Scenario: AniList unavailable during an on-demand refresh
- **WHEN** the user triggers refresh, the MyAnimeList call succeeds, and the AniList fetch fails
- **THEN** the action reports success with updated metadata, the anime's stored airing rows are left unchanged, and the AniList failure is logged

### Requirement: Lean, visit-triggered refresh for browsed anime
The system SHALL refresh Season-page and Top-Anime-page listings by re-fetching only when the user visits the current season, the upcoming season, or a Top Anime ranking list on a local calendar day after that listing's last fetch, requesting only lean listing fields (title, picture, episode count, type, MAL score, rank/popularity) rather than full anime details. A season, or a ranking list, never visited SHALL never be proactively fetched.

Each selectable Top Anime ranking list — All, TV, Movie, OVA, Special, Popularity, and Favourite — SHALL be treated as its own listing for this purpose, with its own cached rows and its own last-fetched time, in the same way each (year, season) pair is its own listing. Fetching one list SHALL NOT mark any other list as fetched, and SHALL NOT invalidate another list's cache.

#### Scenario: First visit of a new local day
- **WHEN** I open the current season, the upcoming season, or a Top Anime ranking list and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches that listing's lean fields live and updates the cache

#### Scenario: Same-day revisit serves cache
- **WHEN** I reopen a listing already fetched earlier the same local day
- **THEN** it is served from Postgres without a live re-fetch

#### Scenario: Lean refresh preserves existing rich fields
- **WHEN** a lean listing refresh updates an AnimeMetadata row that already has full detail fields populated
- **THEN** only the lean fields are updated and the existing rich detail fields are left unchanged

#### Scenario: One ranking list's fetch does not cover another
- **WHEN** the All ranking list was fetched earlier today and I select the Movie list for the first time today
- **THEN** the Movie list is fetched live and cached under its own last-fetched time, leaving the All list's cached rows untouched

#### Scenario: Unvisited listing stays unfetched
- **WHEN** a season, or a Top Anime ranking list, has never been visited
- **THEN** no background job fetches it on its behalf, and visiting a different ranking list does not fetch it either

### Requirement: Visit-triggered live fetches collapse concurrent duplicates
Every read endpoint that can live-fetch from MyAnimeList as a side effect of being visited — the single-anime detail read and the Top Anime ranking read, alongside the season listing that already does this — SHALL admit at most one such fetch per subject at a time. Concurrent requests for the same subject SHALL wait on the in-flight fetch rather than starting a second one.

After acquiring its turn, a waiting request SHALL re-evaluate the condition that triggers the fetch (the row is missing or detail-incomplete; the ranking list has not been fetched on the current local day) and SHALL serve the now-updated cache when the condition no longer holds. A subject SHALL be identified by what is being fetched — the anime id, the specific ranking list, the (year, season) pair — so fetches for different subjects still run independently. Two different Top Anime ranking lists are two different subjects.

A failed fetch SHALL release the turn without marking the subject as fetched, so the next request retries.

#### Scenario: Two overlapping detail reads make one MAL call
- **WHEN** two `GET /api/anime/{id}` requests for the same uncached anime arrive at the same time
- **THEN** one full-detail MAL fetch is made, the second request waits for it and then serves the freshly cached row, and only one set of writes reaches the database

#### Scenario: Two overlapping top-anime reads make one MAL call
- **WHEN** two `GET /api/top-anime` requests for the same ranking list arrive at the same time on a day that list has not been fetched yet
- **THEN** one ranking fetch is made and both requests are served from its result

#### Scenario: Different ranking lists still fetch in parallel
- **WHEN** `GET /api/top-anime` requests for two different ranking lists arrive at the same time, neither fetched yet today
- **THEN** neither waits on the other, since they are different subjects

#### Scenario: Different anime still fetch in parallel
- **WHEN** detail reads for two different uncached anime arrive at the same time
- **THEN** neither waits on the other, since they are different subjects

#### Scenario: A failed fetch is retried by the next request
- **WHEN** a visit-triggered fetch fails and another request for the same subject arrives afterwards
- **THEN** that request attempts the fetch again rather than being treated as already refreshed

### Requirement: Full detail fetch reserved for import, tiered refresh, and detail view
The system SHALL only fetch full anime-detail fields (genres, synopsis, background, studio, aired dates, broadcast schedule, related anime) via initial import, the scheduled tiered my-list refresh, opening that specific anime's own detail page, or its manual refresh action — never as a side effect of a season or Top Anime listing refresh, and never on behalf of an anime the user has not opened.

Opening a detail page SHALL trigger a full-detail fetch when, and only when, that anime has never had one or its last one is older than its own staleness tier — the same tier ladder the scheduled job uses. The system SHALL NOT infer detail-completeness from the presence of any particular field, and SHALL NOT use dated migration cutoffs to decide it: an anime that genuinely has no genres, or genuinely has no related anime, SHALL be recognised as fetched and SHALL NOT re-fetch on every visit.

Because the trigger is a timestamp comparison, an anime already fetched within its tier SHALL serve from cache with no extra bookkeeping, and an anime whose relation rows are stale in a way no field can reveal SHALL still be refreshed once its tier elapses.

A related anime's media type is reported directly by MAL's `related_anime` data (via nested field selection) as part of that full-detail fetch, so no separate fetch of a related anime is ever made purely to learn its media type.

#### Scenario: Browsing a season does not trigger detail fetches
- **WHEN** the Season or Top Anime listing refreshes
- **THEN** no full anime-detail call is made for any anime in that listing as a result

#### Scenario: First visit fetches
- **WHEN** I open the detail page of an anime that has never had a full-detail fetch
- **THEN** a full-detail fetch is made and the page renders from it

#### Scenario: Same-tier revisit serves cache
- **WHEN** I open the detail page of an anime whose last full-detail fetch is inside its staleness tier
- **THEN** no fetch is made and the page is served from cache

#### Scenario: A stale detail page refreshes itself
- **WHEN** I open the detail page of a currently-airing anime last full-fetched two days ago
- **THEN** a full-detail fetch is made, picking up any relation, episode-count, or status change

#### Scenario: An anime with genuinely no relations does not refetch every visit
- **WHEN** I repeatedly open the detail page of an anime MAL reports with no related anime at all
- **THEN** it is fetched once and then served from cache until its tier elapses, rather than being treated as incomplete because its relation set is empty

#### Scenario: Related-anime media types come from the owning anime's own fetch
- **WHEN** an anime's detail page is opened and its full-detail fetch runs
- **THEN** every related anime's media type is resolved from that same fetch, and no additional MAL call is made for any related anime

### Requirement: A failed visit-triggered fetch is reported, not hidden
When a visit-triggered full-detail fetch is attempted and fails, the system SHALL report that failure to the caller alongside whatever cached data it can serve, rather than silently serving the thin cached row as though it were complete.

The read SHALL still succeed where any cached row exists — a failed refresh SHALL NOT turn a viewable page into an error — but the response SHALL carry a flag stating that fresh data was attempted and could not be obtained.

This exists because the failure is otherwise indistinguishable from a correct empty answer: a lean row served after a failed fetch renders with no relations and no prequel, sequel, or More controls, exactly as an anime that genuinely has none would. A MyAnimeList burst-throttle that outlasts the client's backoff attempts produces precisely that on a first visit.

The failure SHALL NOT mark the anime as fetched, so the next request retries.

#### Scenario: A failed first-visit fetch is flagged
- **WHEN** I open the detail page of an uncached anime and the MAL fetch fails after its retries
- **THEN** the response is flagged as having failed to refresh, rather than presenting the empty result as this anime's relation data

#### Scenario: A successful fetch is not flagged
- **WHEN** a visit-triggered fetch succeeds
- **THEN** the response carries no refresh-failure flag

#### Scenario: Cached data is still served
- **WHEN** a visit-triggered refresh of an already-cached anime fails
- **THEN** the cached record is still returned, with the refresh-failure flag set

#### Scenario: A failure is retried on the next visit
- **WHEN** a visit-triggered fetch fails and I open the same page again
- **THEN** the fetch is attempted again rather than being treated as already done


## MODIFIED Requirements

### Requirement: Full detail fetch reserved for import, tiered refresh, and detail view
The system SHALL only fetch full anime-detail fields (genres, synopsis, background, studio, aired dates, broadcast schedule, related anime) via initial import, the scheduled tiered my-list refresh, opening that specific anime's own detail page, or its manual refresh action — never as a side effect of a season or Top Anime listing refresh, and never on behalf of an anime the user has not opened.

Opening a detail page SHALL trigger a full-detail fetch when, and only when, that anime has never had one or its last one is older than its own staleness tier — the same tier ladder the scheduled job uses. The system SHALL NOT infer detail-completeness from the presence of any particular field, and SHALL NOT use dated migration cutoffs to decide it: an anime that genuinely has no genres, or genuinely has no related anime, SHALL be recognised as fetched and SHALL NOT re-fetch on every visit.

Because the trigger is a timestamp comparison, an anime already fetched within its tier SHALL serve from cache with no extra bookkeeping, and an anime whose relation rows are stale in a way no field can reveal SHALL still be refreshed once its tier elapses.

A related anime's media type is reported directly by MAL's `related_anime` data (via nested field selection) as part of that full-detail fetch, so no separate fetch of a related anime is ever made purely to learn its media type.

An anime's **picture set** SHALL be carried by these same full-detail fetches whenever the anime is in my list, and SHALL NOT have a staleness tier of its own. A picture set is therefore exactly as fresh as its anime's last full detail, and no fresher. No full-detail fetch SHALL be made for the purpose of refreshing a picture set that already exists.

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

#### Scenario: A tiered refresh keeps picture sets current
- **WHEN** the scheduled tiered refresh fetches full detail for a my-list anime
- **THEN** that anime's picture set is refreshed from the same response, at no additional request

#### Scenario: A present picture set never provokes a fetch
- **WHEN** a my-list anime's picture set has already been fetched and its full detail is inside its tier
- **THEN** no fetch of any kind is made for it

## ADDED Requirements

### Requirement: Visit-triggered picture backfill for rows fetched before pictures were kept
The system SHALL provide a visit-triggered fetch that populates an anime's picture set without waiting for that anime's next full-detail refresh. It SHALL run only for an anime that is in my list and whose picture set has never been fetched.

This exists because the picture set rides the full-detail fetch, and a my-list anime already inside its staleness tier will not be full-fetched again for up to its whole tier — as much as 28 days for a long-finished anime. Without a backfill, the picture picker would be unavailable on most of the list for weeks after this capability ships, for no reason the user could see.

The backfill SHALL make one MAL request per anime, SHALL be paced and single-flighted exactly as every other visit-triggered fetch is, and SHALL NOT mark the anime as picture-fetched when it fails, so the next visit retries.

The backfill SHALL NOT be run as a batch over the list, on a schedule, or for any anime nobody has opened.

#### Scenario: Backfilling a fresh my-list row
- **WHEN** I open the detail page of a my-list anime whose full detail is fresh and whose picture set has never been fetched
- **THEN** one picture fetch is made for it and its set is stored

#### Scenario: A second visit does not re-fetch
- **WHEN** I return to that anime's detail page
- **THEN** no picture fetch is made

#### Scenario: The backfill is not a sweep
- **WHEN** the app runs for a day with no detail or series page opened
- **THEN** no picture backfill has been performed for any anime

#### Scenario: A failed backfill leaves the anime unfetched
- **WHEN** a picture backfill fails
- **THEN** the anime is not recorded as picture-fetched and the next visit tries again

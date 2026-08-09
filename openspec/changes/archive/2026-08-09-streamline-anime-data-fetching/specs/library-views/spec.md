## MODIFIED Requirements

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL re-fetch the Top Anime ranking's lean listing fields the first time it is visited on a local calendar day after its last fetch, serving it from cache on same-day revisits. If the ranking has never been visited, it SHALL never be proactively fetched.

At most one ranking refresh SHALL be in flight at a time: a second request arriving while a refresh is running SHALL wait for it and then serve the refreshed cache, rather than starting a second ranking fetch. A fetch that fails SHALL NOT count as the day's fetch — the next visit retries.

#### Scenario: New-day visit
- **WHEN** I open the Top Anime page and it has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches the ranking live and updates the cache

#### Scenario: Same-day revisit
- **WHEN** I reopen the Top Anime page again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for the ranking arrives while its refresh is already running
- **THEN** no additional MAL fetch is started, and the second request is served from the refreshed cache once the first completes

#### Scenario: A failed fetch does not consume the day
- **WHEN** the ranking refresh fails and I open the Top Anime page again the same day
- **THEN** the refresh is retried, because only a successful fetch marks the ranking as fetched for that day

#### Scenario: Never visited stays unfetched
- **WHEN** the Top Anime ranking has never been visited
- **THEN** no background job fetches it

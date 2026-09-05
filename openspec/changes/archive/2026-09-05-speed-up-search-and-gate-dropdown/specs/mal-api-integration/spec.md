## ADDED Requirements

### Requirement: Live search responses are briefly cached and shared
The system SHALL hold each successful live anime-search response (`GET /v2/anime?q=`) in memory for a short period — at most one minute — keyed by the query it was made for, and SHALL serve a repeat of the same query from that held response instead of making a second request. This exists because outbound MAL requests are paced (see "Burst throttling and 403 handling"), so a repeated query costs not only a network round trip but a place in that queue.

The type-ahead and the full search results page SHALL make the *same* live search for a given query — the same candidate count, so the same held response serves both. Submitting a query the type-ahead has already searched for SHALL therefore make no live request at all.

A failed live search SHALL NOT be held: a network blip or transient MAL error SHALL be retried on the next search for that query rather than being remembered for the rest of the period.

The held responses SHALL be bounded in number, so a session of many distinct searches cannot grow the app's memory without limit.

Nothing SHALL be persisted from a held response that would not be persisted from a fresh one, so a held response cannot put stale data into storage.

A live search whose caller has abandoned it — a type-ahead request the user has typed past or dismissed — SHALL be abandoned in turn rather than recorded as a live-search failure, and SHALL give up its place in the request pacing so whatever is queued behind it proceeds immediately. Every other failure SHALL keep degrading as it does today, with the caller falling back to locally stored anime.

#### Scenario: A repeated query makes no second request
- **WHEN** the same query is searched twice within the holding period
- **THEN** the second search is answered from the held response and no request reaches MAL

#### Scenario: Submitting reuses the type-ahead's search
- **WHEN** I type a query into the type-ahead and then submit it
- **THEN** the results page is served from the response the type-ahead's live search already fetched, without making a live request of its own

#### Scenario: A failure is not held
- **WHEN** a live search fails and the same query is searched again
- **THEN** a fresh request is made rather than the failure being repeated from memory

#### Scenario: Held responses are bounded
- **WHEN** many distinct queries are searched in one session
- **THEN** the number of held responses stays bounded, older ones being discarded

#### Scenario: An abandoned search is not a failure
- **WHEN** the client abandons a type-ahead request while its live search is waiting to be dispatched
- **THEN** the search is abandoned rather than logged as a live-search failure, and the next paced request proceeds without waiting for it

#### Scenario: A real failure still degrades
- **WHEN** a live search fails for a reason other than its caller abandoning it
- **THEN** the caller falls back to locally stored anime exactly as it does today

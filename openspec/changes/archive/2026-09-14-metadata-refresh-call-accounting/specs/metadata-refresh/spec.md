## MODIFIED Requirements

### Requirement: Scheduled tiered staleness refresh for my-list anime
The system SHALL run a scheduled background refresh over anime with a corresponding UserAnimeEntry (my list), together with the narrow adjacent set defined by "Scheduled refresh of unaired list-adjacent anime", selecting refresh candidates by staleness tier measured from that anime's last **full-detail** fetch, or from a later attempt that found MyAnimeList no longer has the anime (see "A refresh candidate MyAnimeList does not have waits out its tier"):

| Tier | Refresh interval |
| --- | --- |
| Currently airing | 1 day |
| Not yet aired, premiere date known and within 30 days, or already passed | 1 day |
| Not yet aired, premiere date known and more than 30 days away | 7 days |
| Not yet aired, premiere date unknown | 3 days |
| Finished, aired within the last year | 3 days |
| Finished, aired 1–2 years ago | 14 days |
| Finished, aired more than 2 years ago | 28 days |

Each scheduled refresh SHALL be a full-detail fetch, not a score-only one, and SHALL update everything a full-detail fetch writes — related anime, airing status, episode counts, ranks, genres, synopsis — together with the last-full-detail-fetch timestamp. Relation data in particular SHALL NOT be write-once: an anime whose relations were correct when first imported SHALL pick up a newly-announced sequel on its tier without any user action.

A full-detail fetch and a single-field fetch cost the same one request against MAL's per-request rate limit, so refreshing everything on these tiers SHALL NOT increase the number of API calls the job makes.

An anime that has not aired SHALL be measured against its **premiere date**, not its finish date, and SHALL move onto the daily tier as that date comes within 30 days — including once the date has passed while MyAnimeList still reports the anime as not yet aired, since that flip to currently-airing is itself what the daily tier is watching for. An unaired anime whose premiere date is known and further out SHALL sit on the weekly tier: what changes for such an anime — its episode count, its status, the date itself — changes a handful of times across a production rather than daily, and a week's latency is well inside the window any surface reporting those changes displays.

An unaired anime with **no** known premiere date SHALL be refreshed every 3 days, more often than one whose date is merely distant. The missing date is itself the most valuable fact still outstanding about such an anime, and there is no signal to time the wait against — unlike a distant premiere, which announces when it is worth looking more closely.

An anime that has never had a full-detail fetch, and has no recorded not-found attempt, SHALL be treated as maximally stale and SHALL sort ahead of every tiered candidate.

#### Scenario: Airing anime refreshed daily
- **WHEN** a my-list anime is currently airing and its last full-detail fetch is older than 1 day
- **THEN** the scheduled job refreshes it

#### Scenario: An imminent premiere is polled daily
- **WHEN** a not-yet-aired anime premieres in 12 days and its last full-detail fetch is older than 1 day
- **THEN** the scheduled job refreshes it

#### Scenario: A distant premiere is polled weekly
- **WHEN** a not-yet-aired anime premieres in eight months and its last full-detail fetch is 3 days old
- **THEN** the scheduled job does not refresh it on that pass

#### Scenario: An unaired anime with no premiere date is polled every three days
- **WHEN** a not-yet-aired anime has no known premiere date and its last full-detail fetch is older than 3 days
- **THEN** the scheduled job refreshes it

#### Scenario: A distant premiere is not polled on the unknown-date tier
- **WHEN** a not-yet-aired anime premieres in eight months and its last full-detail fetch is 4 days old
- **THEN** the scheduled job does not refresh it on that pass

#### Scenario: A premiere date that has passed keeps the daily tier
- **WHEN** an anime's premiere date was four days ago and MyAnimeList still reports it as not yet aired
- **THEN** it is refreshed on the daily tier until that status changes

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
- **WHEN** the job selects candidates and some my-list anime have never had a full-detail fetch and have no recorded not-found attempt
- **THEN** those are selected before anime that are merely past their tier interval

#### Scenario: A never-fetched anime MyAnimeList does not have stops going first
- **WHEN** a my-list anime has never had a full-detail fetch and its last attempt got a not-found answer
- **THEN** it is not selected again until its tier interval has passed since that attempt

#### Scenario: Non-my-list anime excluded from the tiers unless adjacent and unaired
- **WHEN** the scheduled job selects refresh candidates
- **THEN** an anime without a corresponding UserAnimeEntry is included only where it qualifies for the adjacent set, and a finished anime outside my list is never included, regardless of staleness

### Requirement: Announcement resolution shares the refresh job's pacing and cap

The MyAnimeList calls made to resolve newly-discovered relation edges SHALL be made by the same scheduled background work that performs the tiered refresh, SHALL be paced the same way, and SHALL count against the same per-day call cap.

Resolution SHALL run before the staleness batch on each pass, so a large backlog of stale anime never starves news of its calls, and SHALL itself be capped per pass so a large burst of discoveries never consumes a pass entirely.

A resolution call that fails SHALL NOT count its discovery as resolved, and SHALL be retried on a later pass, with one exception. When MyAnimeList answers that it has no such anime, the discovery SHALL be marked resolved, SHALL announce nothing, and SHALL be logged with the anime's id. Without that exception, an anime MyAnimeList has deleted would cost one call on every pass, forever. Every other failure, including one that ends the pass (see "A pass ends when MyAnimeList is down or pushing back"), SHALL still leave the discovery for a later pass.

#### Scenario: Resolution calls count against the daily cap

- **WHEN** a pass resolves discoveries and then refreshes stale anime
- **THEN** both sets of calls count against the one per-day cap, which is not raised to accommodate resolution

#### Scenario: A stale backlog does not starve resolution

- **WHEN** a pass begins with both unresolved discoveries and more stale anime than its batch allows
- **THEN** the discoveries are resolved first and the staleness batch takes what remains of the pass

#### Scenario: A discovery burst does not consume a pass

- **WHEN** far more discoveries are pending than one pass's resolution allowance
- **THEN** the remainder are resolved on later passes rather than in one burst

#### Scenario: A discovery naming an anime MyAnimeList does not have resolves without news

- **WHEN** the resolution fetch for a discovery's anime gets a not-found answer
- **THEN** the discovery is marked processed, no announcement update is recorded, the pass continues with the next discovery, and no later pass fetches that anime for it again

#### Scenario: Any other failed resolution is retried

- **WHEN** the resolution fetch for a discovery's anime fails for any reason other than a not-found answer
- **THEN** the discovery is left unprocessed and is attempted again on a later pass

### Requirement: Per-day batch cap ordered by staleness, spread across 10-minute passes
The system SHALL cap the scheduled refresh job at a fixed number of API calls per UTC day (e.g. 500), spent across passes that run every 10 minutes rather than in one nightly burst, and SHALL process the most-stale eligible candidates first across all tiers, carrying any overflow to the next day automatically without extra bookkeeping.

The cap SHALL count **attempts**, not successes. Every MyAnimeList call the job starts, for announcement resolution or for the staleness batch, SHALL count once against the cap whether it succeeds or fails. That includes a call made earlier in a pass that later fails for an unrelated reason. An anime SHALL count once however many times its request is retried beneath the job, for example by the client's own 403 backoff. A discovery or candidate the job considers without calling MyAnimeList SHALL NOT count.

This exists because a cap that counts only successes never fills while MyAnimeList is failing, which is exactly when the job most needs a limit.

For ordering, an anime's staleness SHALL be measured from the later of its last full-detail fetch and its last recorded not-found attempt.

#### Scenario: Most-stale candidates processed first
- **WHEN** a pass selects candidates within the remaining daily cap
- **THEN** candidates are ordered by longest time since their last full-detail fetch or recorded not-found attempt, whichever is later, first

#### Scenario: Overflow carries to the next day
- **WHEN** more candidates are eligible than the daily batch cap allows
- **THEN** the remainder are simply refreshed on a subsequent day since they remain the most-stale candidates

#### Scenario: A failed call counts against the cap
- **WHEN** a pass makes a refresh or resolution call and that call fails
- **THEN** the day's call count goes up by one, exactly as it would for a successful call

#### Scenario: A retried request counts once
- **WHEN** the client retries a throttled request several times before it finally succeeds or fails
- **THEN** that anime counts as one call against the cap

#### Scenario: A discovery resolved without a call costs nothing
- **WHEN** a pass marks a discovery processed because its anime was already fully fetched or already in my list
- **THEN** the day's call count does not change

#### Scenario: Failures that reach the cap stop the job for the day
- **WHEN** failed calls bring the day's count to the cap
- **THEN** the job makes no further MyAnimeList calls until the next UTC day

## ADDED Requirements

### Requirement: A pass ends when MyAnimeList is down or pushing back
The scheduled job SHALL end the rest of a pass at the first MyAnimeList call that fails in a way that means MyAnimeList is down or pushing back:

- no response was received;
- the request timed out (the host shutting down is not a timeout);
- MyAnimeList answered with a 5xx status or a 429;
- MyAnimeList answered 403 after the client's own throttling retries were used up.

When such a failure happens during announcement resolution, that pass SHALL make no staleness-batch calls either.

The failed call SHALL count against the day's cap. It SHALL NOT mark any anime or discovery as failed, resolved or refreshed. Work the pass completed before the failure SHALL be kept.

The job SHALL NOT add a delay, a backoff, or any record that MyAnimeList is unavailable beyond ending that pass. The next scheduled pass is the retry, so while MyAnimeList stays down each pass makes at most one attempt.

The pass that follows one ended this way SHALL NOT attempt the anime whose call ended it, for either announcement resolution or the staleness batch, and SHALL otherwise run normally. That anime SHALL be eligible again on the pass after. This SHALL be the only thing the job carries from one pass to the next, and it MAY be forgotten when the app restarts. It exists because a failure that marks nothing leaves its anime first in the queue. Without the skip, one anime that MyAnimeList always fails with a server error, while every other request succeeds, would end every pass and stop the job for good.

A failure of any other kind, apart from a not-found answer (see "A refresh candidate MyAnimeList does not have waits out its tier"), SHALL skip only that anime or discovery for the pass, and the pass SHALL continue with the next one.

#### Scenario: MyAnimeList unreachable
- **WHEN** a pass starts with discoveries and due anime waiting and MyAnimeList gives no response
- **THEN** the pass makes exactly one attempt, the day's call count goes up by 1, and the next pass tries again with one attempt of its own, on a different anime

#### Scenario: One anime that always fails does not stall the job
- **WHEN** MyAnimeList answers every request normally except for the stalest due anime, which always gets a 500
- **THEN** the pass that tries that anime ends, the next pass skips it and refreshes the other due anime, and the pass after that tries it again

#### Scenario: The skipped anime is skipped for one pass only
- **WHEN** a pass ended on an anime's failed call and the following pass skipped that anime without itself ending early
- **THEN** the pass after that includes the anime as a candidate again

#### Scenario: A request that times out
- **WHEN** a refresh request times out while the host is still running
- **THEN** the pass ends and makes no further calls

#### Scenario: A server error mid-batch
- **WHEN** five anime are due, there are no discoveries, and the second refresh gets a 503
- **THEN** the first anime is refreshed, the other three are not attempted, and the day's call count goes up by 2

#### Scenario: Throttling that outlasts the client's retries
- **WHEN** a refresh request still gets a 403 after the client's throttling retries
- **THEN** the pass ends and makes no further calls

#### Scenario: Too many requests
- **WHEN** a refresh request gets a 429
- **THEN** the pass ends and makes no further calls

#### Scenario: A failed announcement fetch skips the refresh batch
- **WHEN** a pass's announcement resolution fetch gets a 503 while anime are due for refresh
- **THEN** that pass makes no refresh calls, and the discovery stays unprocessed

#### Scenario: An outage marks nothing
- **WHEN** a refresh request fails with no response, a timeout, a 5xx, a 429 or a 403
- **THEN** no failed attempt is recorded for that anime, its last full-detail fetch is unchanged, and it is still due on the next pass

#### Scenario: Any other failure skips only that anime
- **WHEN** a refresh request fails with a 400 while more anime are due
- **THEN** that anime is skipped for this pass and stays due, and the pass continues with the next anime

### Requirement: A refresh candidate MyAnimeList does not have waits out its tier
When the scheduled staleness batch fetches an anime and MyAnimeList answers that it has no such anime, the system SHALL:

- continue the pass with the next candidate;
- record when that attempt failed;
- treat that attempt like a refresh when deciding whether the anime is due and where it sits in the queue, so it is not tried again until its tier's interval has passed since the attempt;
- leave the anime's last-full-detail-fetch timestamp untouched, so no other surface reads the anime as freshly fetched, or as fetched at all when it never was.

Moving such an anime to the back of the queue alone would not be enough: most passes have fewer due candidates than their batch allows, so it would still be tried on every pass.

The tier SHALL be the one the anime's cached fields already select. A later successful full-detail fetch of that anime, by any path, SHALL clear the recorded failure. Only a not-found answer SHALL record a failed attempt. A failure that ends the pass SHALL NOT.

The recorded failure SHALL change only which anime the scheduled job selects. It SHALL NOT change when a visit-triggered fetch is made.

#### Scenario: A deleted anime among due refreshes
- **WHEN** five anime are due, there are no discoveries, and MyAnimeList answers not-found for the third
- **THEN** the other four are refreshed, the day's call count goes up by 5, and the third is not a candidate on the next pass

#### Scenario: A not-found anime is not treated as fetched
- **WHEN** a refresh gets a not-found answer for an anime that has never had a full-detail fetch
- **THEN** its last-full-detail-fetch timestamp is still unset afterwards

#### Scenario: A not-found anime waits out its tier
- **WHEN** a finished anime on the 3-day tier got a not-found answer two days ago
- **THEN** it is not a candidate, and it becomes one again once more than three days have passed since that attempt

#### Scenario: An anime MyAnimeList returns again clears its failure
- **WHEN** an anime with a recorded not-found attempt becomes due again and MyAnimeList now returns it
- **THEN** it is refreshed normally, its last full-detail fetch advances, and its recorded failure is cleared

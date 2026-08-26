## MODIFIED Requirements

### Requirement: Scheduled tiered staleness refresh for my-list anime
The system SHALL run a scheduled background refresh over anime with a corresponding UserAnimeEntry (my list), together with the narrow adjacent set defined by "Scheduled refresh of unaired list-adjacent anime", selecting refresh candidates by staleness tier measured from that anime's last **full-detail** fetch:

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

An anime that has never had a full-detail fetch SHALL be treated as maximally stale and SHALL sort ahead of every tiered candidate.

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
- **WHEN** the job selects candidates and some my-list anime have never had a full-detail fetch
- **THEN** those are selected before anime that are merely past their tier interval

#### Scenario: Non-my-list anime excluded from the tiers unless adjacent and unaired
- **WHEN** the scheduled job selects refresh candidates
- **THEN** an anime without a corresponding UserAnimeEntry is included only where it qualifies for the adjacent set, and a finished anime outside my list is never included, regardless of staleness

### Requirement: Full detail fetch reserved for import, tiered refresh, and detail view
The system SHALL only fetch full anime-detail fields (genres, synopsis, background, studio, aired dates, broadcast schedule, related anime) via initial import, the scheduled tiered refresh, the one-time resolution of a newly-discovered relation edge, opening that specific anime's own detail page, or its manual refresh action — never as a side effect of a season or Top Anime listing refresh.

Outside those paths the system SHALL NOT fetch on behalf of an anime the user has not opened. The two paths that reach an unopened anime are bounded: the scheduled tiered refresh reaches one only through the adjacent set, and resolution reaches one exactly once, because a franchise announced a new entry.

Opening a detail page SHALL trigger a full-detail fetch when, and only when, that anime has never had one or its last one is older than its own staleness tier — the same tier ladder the scheduled job uses. The system SHALL NOT infer detail-completeness from the presence of any particular field, and SHALL NOT use dated migration cutoffs to decide it: an anime that genuinely has no genres, or genuinely has no related anime, SHALL be recognised as fetched and SHALL NOT re-fetch on every visit.

Because the trigger is a timestamp comparison, an anime already fetched within its tier SHALL serve from cache with no extra bookkeeping, and an anime whose relation rows are stale in a way no field can reveal SHALL still be refreshed once its tier elapses.

A related anime's media type is reported directly by MAL's `related_anime` data (via nested field selection) as part of that full-detail fetch, so no separate fetch of a related anime is ever made purely to learn its media type.

An anime's **picture set** SHALL be carried by these same full-detail fetches whenever the anime is in my list, and SHALL NOT have a staleness tier of its own. A picture set is therefore exactly as fresh as its anime's last full detail, and no fresher. No full-detail fetch SHALL be made for the purpose of refreshing a picture set that already exists.

#### Scenario: Browsing a season does not trigger detail fetches
- **WHEN** the Season or Top Anime listing refreshes
- **THEN** no full anime-detail call is made for any anime in that listing as a result

#### Scenario: A newly-related anime is fetched once
- **WHEN** a refresh discovers a relation edge to an anime with no cached record
- **THEN** one full-detail fetch is made for that anime, and no further fetch is made for it unless it qualifies for the adjacent set or the user opens it

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

### Requirement: Scheduled refresh of unaired list-adjacent anime

The scheduled tiered refresh SHALL additionally include anime that have **not aired** and are connected by a same-story relation to at least one list entry whose status is not Dropped, even though those anime have no list entry of their own. Such anime SHALL be refreshed on the same not-yet-aired tiers as any other unaired anime.

An anime SHALL qualify only while MyAnimeList reports it as not yet aired, or reports no airing status for it at all. An anime that has started airing, or has finished, SHALL leave the adjacent set immediately unless it has a list entry of its own — so the set drains itself as its members premiere and can never accumulate finished anime.

The relations that connect an anime to the adjacent set SHALL be the same same-story relations a series is built from, read in both directions.

This exists because an announced anime is by definition not in my list. Without it, such an anime is fetched once when its announcement is resolved and never again, so its episode count and premiere date could never be observed becoming known.

The candidate query MAY include an anime whose only connecting edge external adjudication has contradicted; over-including there costs one request and nothing more, and the surfaces that read the resulting updates apply the stricter test themselves.

#### Scenario: An announced sequel keeps being refreshed

- **WHEN** an anime with no list entry has not aired and is the sequel of an entry I am watching, and its last full-detail fetch is older than its tier
- **THEN** the scheduled job refreshes it

#### Scenario: A dropped franchise stops being refreshed on its behalf

- **WHEN** the only entry connecting an unaired anime to my list is set to Dropped
- **THEN** that anime is no longer selected as an adjacent candidate

#### Scenario: An adjacent anime leaves the set when it starts airing

- **WHEN** an adjacent anime with no list entry of its own starts airing
- **THEN** it is no longer selected as a refresh candidate

#### Scenario: A finished anime outside my list is never adjacent

- **WHEN** an anime with no list entry finished airing and is related to an entry I am watching
- **THEN** it is never selected as a refresh candidate

#### Scenario: A shared-universe link does not make an anime adjacent

- **WHEN** an unaired anime's only connection to my list is a character or alternative-setting relation
- **THEN** it is not selected as a refresh candidate

### Requirement: Announcement resolution shares the refresh job's pacing and cap

The MyAnimeList calls made to resolve newly-discovered relation edges SHALL be made by the same scheduled background work that performs the tiered refresh, SHALL be paced the same way, and SHALL count against the same per-day call cap.

Resolution SHALL run before the staleness batch on each pass, so a large backlog of stale anime never starves news of its calls, and SHALL itself be capped per pass so a large burst of discoveries never consumes a pass entirely.

A resolution call that fails SHALL NOT count its discovery as resolved, and SHALL be retried on a later pass.

#### Scenario: Resolution calls count against the daily cap

- **WHEN** a pass resolves discoveries and then refreshes stale anime
- **THEN** both sets of calls count against the one per-day cap, which is not raised to accommodate resolution

#### Scenario: A stale backlog does not starve resolution

- **WHEN** a pass begins with both unresolved discoveries and more stale anime than its batch allows
- **THEN** the discoveries are resolved first and the staleness batch takes what remains of the pass

#### Scenario: A discovery burst does not consume a pass

- **WHEN** far more discoveries are pending than one pass's resolution allowance
- **THEN** the remainder are resolved on later passes rather than in one burst

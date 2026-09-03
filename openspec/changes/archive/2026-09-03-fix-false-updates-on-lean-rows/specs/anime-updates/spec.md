## ADDED Requirements

### Requirement: A premiere date released is news only while an anime has not finished airing

The system SHALL record a **premiere-date-released** update only for an anime MyAnimeList reports as *not yet aired* or *currently airing*. A premiere date becoming known for an anime that has finished airing SHALL record nothing.

The reasoning is the one already applied to the three schedule-change kinds under "Schedule changes are recorded only while an anime has not finished airing": a premiere date arriving for a show that finished years ago is MyAnimeList's records reaching the system, not a date anyone is waiting for. Only a show still ahead of, or in the middle of, its broadcast has a premiere anyone can act on.

The gate SHALL be evaluated once, when the update is recorded, against the airing status the same detection just wrote — the same write-time treatment the announcement gate receives — and SHALL NOT be re-evaluated afterwards. A reveal recorded while a show had not finished airing SHALL survive that show finishing.

**Episode count released** SHALL NOT come under this gate. A premiere date for a finished show reports an event already in the past that the anime's own record already displays; an episode count is a fact about what there is to watch, and remains news for a finished show whose total was unknown — which is exactly the case the AniList-supplied total exists to fill.

#### Scenario: A premiere date arriving for a long-finished show is not news

- **WHEN** a refresh writes a premiere date for an anime that finished airing in 2008 and whose stored premiere date was unknown
- **THEN** no update is recorded

#### Scenario: An unaired show's premiere date is news

- **WHEN** a refresh writes a premiere date for a not-yet-aired anime whose stored premiere date was unknown
- **THEN** a premiere-date-released update is recorded

#### Scenario: An episode count arriving for a finished show is still news

- **WHEN** a refresh writes a total episode count for an anime that has finished airing and whose stored count was unknown
- **THEN** an episode-count-released update is recorded

#### Scenario: A reveal survives its own show finishing

- **WHEN** a premiere-date-released update is recorded for a currently-airing anime and that anime later finishes airing
- **THEN** the update remains recorded and continues to be shown

### Requirement: Reveals recorded against already-finished anime are retired

On first deployment of the gate above, the system SHALL clear the **premiere-date-released** kind from every update already recorded for an anime that had already finished airing when that update was detected, and SHALL delete an update left covering no kind at all.

Retirement SHALL be bounded to that pairing, so an update recorded while its anime had not finished airing is kept whatever the anime's status is now, and no other kind is touched on any update.

Retiring a reveal SHALL NOT cause it to be recorded again: the anime's record holds the revealed date by then, so no later detection finds it moving from unknown.

#### Scenario: A false reveal is cleared

- **WHEN** the change is deployed over a database holding a premiere-date-released update recorded for an anime that finished airing before that update was detected
- **THEN** that update no longer appears in the Updates section or in the history

#### Scenario: A reveal that was legitimate is kept

- **WHEN** the same deployment finds a premiere-date-released update recorded while its anime had not finished airing
- **THEN** that update is left exactly as recorded

#### Scenario: An update covering more than one kind keeps its other kinds

- **WHEN** a retired premiere-date reveal shares its update with an episode-count release
- **THEN** the update survives, covering the episode-count release alone

#### Scenario: A retired reveal does not come back

- **WHEN** the anime whose premiere-date reveal was retired is refreshed again
- **THEN** no premiere-date-released update is recorded for it

## MODIFIED Requirements

### Requirement: Every path that writes anime data detects the updates it can

Detection SHALL happen wherever the data it reads is written: episode count, premiere date and broadcast slot wherever cached anime metadata is written from a MyAnimeList fetch, whether that fetch is a full-detail one or a **lean listing** one (season browsing, Top-Anime rankings, and reconciliation caching a list entry); episode count again wherever an AniList-supplied total is written, which is the airing-data refresh; and moved episodes wherever per-episode airing rows are replaced.

Each path SHALL detect the kinds its own data can produce and no others. The airing-data refresh writes an episode count and per-episode airing rows, so it detects episode-count-released and episodes-moved; it SHALL NOT be routed through the MyAnimeList-metadata detection, which would diff fields AniList never supplied. A lean listing write detects the fields it writes — the episode count every lean write carries, and the premiere date season browsing writes so a listing can be classified to its premiere season — and SHALL NOT diff relations, which a lean write never touches.

A manually-triggered refresh SHALL therefore produce the same updates the scheduled one would, and no future refresh path can be added that silently skips detection. A path that writes a detected field without detecting is not merely incomplete: it absorbs the change, so the next path that does detect finds the new value already stored and reports nothing.

Writing an anime's metadata, or its per-episode airing rows, for the **first** time SHALL record nothing: a first observation is not a value becoming known or moving, and there is no earlier state to compare against.

For anime metadata, "the first time" SHALL mean the anime's first **full-detail** fetch, not the first time a row existed for it. A lean listing write caches an anime without the detail-only fields — premiere date, airing status, broadcast slot — so a row that has never been fully fetched holds no observation of them to compare against, and the fetch that first supplies them SHALL record nothing, exactly as an insert does. Where a lean listing write is itself the first write for an anime, it SHALL likewise record nothing.

Whether an anime has ever been fully fetched SHALL be read from the timestamp of its last full-detail fetch, captured before the fetch being detected stamps it, and SHALL NOT be inferred from whether any particular field is populated — the same rule `metadata-refresh` applies when deciding whether a detail page needs a fetch.

#### Scenario: A manual refresh produces news

- **WHEN** I refresh a single anime by hand and that fetch writes a previously-unknown episode count
- **THEN** an episode-count update is recorded, exactly as the scheduled refresh would have recorded it

#### Scenario: The airing refresh detects its own kinds only

- **WHEN** an airing-data refresh writes an AniList total and replaces an anime's airing rows
- **THEN** it may record episode-count-released and episodes-moved updates, and records no other kind

#### Scenario: A first fetch records no release

- **WHEN** an anime with no cached record is fetched and its record is created carrying an episode count and a premiere date
- **THEN** no episode-count or premiere-date update is recorded for it

#### Scenario: A first airing fetch records no move

- **WHEN** an anime's per-episode airing rows are stored for the first time
- **THEN** no episodes-moved update is recorded for it

#### Scenario: A lean row's first full fetch records no release

- **WHEN** an anime cached only by a lean listing write, and never fully fetched, has its first full-detail fetch write a premiere date and an episode count it had neither of
- **THEN** no update is recorded for it

#### Scenario: A lean row's first full fetch still discovers its relations

- **WHEN** that same first full-detail fetch stores the anime's relation set for the first time
- **THEN** each edge is recorded as a discovery and considered for an announcement as normal

#### Scenario: A lean listing write detects the count it reveals

- **WHEN** a season-browse listing writes a total episode count for an already-fully-fetched anime whose stored count was unknown
- **THEN** an episode-count-released update is recorded

#### Scenario: A lean listing write detects the premiere it moves

- **WHEN** a season-browse listing writes a different premiere date for an already-fully-fetched anime that has not finished airing
- **THEN** a premiere-date-changed update is recorded, reporting the date it moved from

#### Scenario: A lean listing write to a never-fetched row records nothing

- **WHEN** a Top-Anime listing writes an episode count for an anime that has never had a full-detail fetch
- **THEN** no update is recorded

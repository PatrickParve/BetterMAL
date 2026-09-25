## MODIFIED Requirements

### Requirement: Relation discoveries are recorded only on my own entries, and never from a first full fetch

A newly-appeared relation edge SHALL be recorded as a discovery only when **both** hold of the anime whose relation set changed:

- it is a list entry of my own whose status is not Dropped; and
- it had been fully fetched before the fetch that produced the change.

An anime's **first** full-detail fetch SHALL record no discoveries at all. Its whole relation set arriving at once is the system finally looking, not links newly appearing, and this SHALL hold whether the anime had no cached row or only a lean listing row.

A new anime appearing in the relation set of an anime that is *not* my own entry SHALL record no discovery, so an announcement can only ever originate one relation step from my list.

Recording a discovery SHALL capture, on the discovery itself, whether the **newly-related** anime had ever been fully fetched at that moment — the fact the announcement gate is judged on later. It SHALL be read from committed data, before the discovery's own unit of work is saved and before anything the recording triggers can fetch that anime, and SHALL treat an anime with no cached row at all as never fully fetched. This is the only moment at which the question can be answered about the past: afterwards, any fetch of that anime by any part of the system leaves it indistinguishable from an anime that was always known.

The comparison that finds newly-appeared edges SHALL still run for every anime whose relation set is written, whatever this requirement records: its other consumer — the background series rebuild that carries a new member onto the series page without waiting for a visit — SHALL be unaffected by these rules. That rebuild fetches members the system has no row for, so it SHALL NOT be queued for an anime before that anime's discoveries have captured their verdict.

#### Scenario: A new sequel on my own show is a discovery

- **WHEN** a refresh of an anime I have on Plan to watch, already fully fetched, adds a relation edge to an anime the system has never fetched
- **THEN** a discovery is recorded for that edge, carrying the verdict that the newly-related anime had never been fully fetched

#### Scenario: A discovery records what was true before the rebuild ran

- **WHEN** that same refresh queues the series rebuild that goes on to fetch the newly-related anime seconds later
- **THEN** the discovery's recorded verdict still says the anime had never been fully fetched

#### Scenario: A new edge to an anime already known records that it was known

- **WHEN** a refresh of one of my entries adds a relation edge to an anime the system had fully fetched weeks earlier
- **THEN** a discovery is recorded for that edge, carrying the verdict that the newly-related anime had already been fully fetched

#### Scenario: A first full fetch discovers nothing

- **WHEN** an anime cached only by a lean listing write, and never fully fetched, has its first full-detail fetch store its whole relation set
- **THEN** no discovery is recorded for any of its edges

#### Scenario: A first full fetch still rebuilds the series

- **WHEN** that same first full-detail fetch stores relation edges the system had not seen
- **THEN** the anime's series is queued for a background rebuild exactly as it is today

#### Scenario: A new link on a sequel that is not mine is not a discovery

- **WHEN** a refresh of an anime that is not my own entry — the sequel of one of my entries, say — adds a relation edge to a further anime
- **THEN** no discovery is recorded, so nothing two steps from my list is ever announced

#### Scenario: A new link on a dropped entry is not a discovery

- **WHEN** a refresh of an entry of mine that I have set to Dropped adds a relation edge
- **THEN** no discovery is recorded

### Requirement: An announcement is recorded only for an anime that has not finished airing

An announcement SHALL be recorded only for an anime the system had **never fully fetched** and holds **no list entry** of mine for, of any status. An anime the system had already fully fetched, or that I already track, SHALL record nothing however newly the relation edge naming it appeared.

The two conditions SHALL be established at different moments, because they can be falsified by different things:

- **Never fully fetched** SHALL be read from the verdict the discovery recorded when the edge appeared, and SHALL NOT be re-derived from the anime's cached record at resolution time. Between a discovery and its resolution the system fetches anime for its own reasons — the series rebuild the same detection queues, a visit to the anime's own page, an import — and any such fetch makes an anime that was genuinely new indistinguishable from one that was always known. A discovery recorded before this verdict was captured SHALL fall back to reading the anime's cached record, as it does today.
- **Holds no list entry of mine** SHALL be established at resolution time, from my list as it stands then. Only I can add an anime to my list, so this condition is not falsified by the system's own work, and an anime I have added in the meantime is one I already know about. An announcement recorded for an anime that holds a list entry of mine at that moment would in any case be a false announcement by "Announcements about my own entries are not news".

Before recording an announcement, the system SHALL additionally establish the newly-related anime's airing status, and SHALL record the announcement only when that status is *not yet aired* or *currently airing*. A newly-related anime that has finished airing SHALL record nothing.

Both gates SHALL be evaluated once, when the announcement is recorded, and SHALL NOT be re-evaluated afterwards: an anime announced before it aired keeps its announcement once it starts airing.

MAL adds missing relation edges to long-finished anime routinely; such an edge is a correction to relation data reaching the system, not an announcement of a new show. Taken with the never-fully-fetched condition, an announcement means exactly one thing: something the system had never seen appeared in the relations of an anime on my list.

#### Scenario: A newly-announced sequel is recorded

- **WHEN** a discovery on one of my entries names an anime that had never been fully fetched when the edge appeared, holds no list entry of mine, and has not yet aired
- **THEN** an announcement update is recorded for it

#### Scenario: A rebuild fetching the anime first does not suppress the announcement

- **WHEN** the series rebuild triggered by that same new edge fetches the newly-related anime before the discovery is resolved
- **THEN** the announcement update is still recorded, because the gate reads the verdict the discovery captured, not the record the rebuild wrote

#### Scenario: An anime already fully fetched is not announced

- **WHEN** a discovery names an anime the system had already fully fetched when the edge appeared
- **THEN** no announcement update is recorded, and the discovery is marked processed

#### Scenario: An anime on my own list is not announced

- **WHEN** a discovery names an anime I have been watching for weeks
- **THEN** no announcement update is recorded for it

#### Scenario: An anime I add before the discovery is resolved is not announced

- **WHEN** a discovery names an anime that was new to the system when the edge appeared, and I add it to my list before the discovery is resolved
- **THEN** no announcement update is recorded for it, and the discovery is marked processed

#### Scenario: A newly-linked old anime is not announced

- **WHEN** a discovery names an anime that finished airing in 2005
- **THEN** no announcement update is recorded

#### Scenario: An announcement survives its own premiere

- **WHEN** an anime announced three weeks ago starts airing
- **THEN** its announcement update remains recorded and continues to be shown

### Requirement: Newly-discovered relations are resolved before they become news

The system SHALL mark each recorded relation discovery as processed once it has been considered for an announcement. Processing one SHALL:

- read whether the newly-related anime had ever been fully fetched from the verdict the discovery recorded, falling back to the anime's cached record only for a discovery recorded before that verdict was captured, and establish whether I hold a list entry for it;
- where neither is true, ensure it has a cached record carrying an airing status — fetching it from MyAnimeList where it has none, or where its only row is a lean listing one carrying no airing status, and spending no call where its cached record already carries full detail — and apply the airing-status gate above;
- where either is true, record nothing and spend no MyAnimeList call; and
- mark the discovery processed whether or not an announcement resulted.

Where several discoveries name the same newly-related anime, the anime SHALL be treated as never fully fetched when **any** of them recorded it so. A discovery that was genuinely news does not stop being news because a later edge to the same anime was not, and the once-per-anime limit on announcements already prevents a duplicate card.

A processing attempt that fails to obtain the anime's record SHALL leave the discovery unprocessed, so the next pass retries it, except when MyAnimeList answers that it has no such anime. That discovery SHALL be marked processed and SHALL announce nothing, since an anime MyAnimeList has deleted can never produce news and retrying it would cost a call on every pass forever. A discovery resolved without a MyAnimeList call SHALL be subject to none of these failure paths, and SHALL NOT end the pass. Processing SHALL be idempotent: a discovery already processed SHALL never be considered again.

Where more than one discovery names the same newly-related anime, the system SHALL record one announcement for it, not one per discovery.

Discoveries recorded before this capability existed SHALL be marked processed and SHALL announce nothing.

#### Scenario: A discovery with no cached anime is fetched

- **WHEN** a discovery names a related anime with no cached record
- **THEN** that anime is fetched once, cached, and then considered for an announcement

#### Scenario: A discovery on a lean row is fetched for its airing status

- **WHEN** a discovery names an anime whose only cached row came from a season listing and carries no airing status
- **THEN** that anime is fetched once so the airing-status gate has something to read

#### Scenario: An announceable anime already cached in full spends no call

- **WHEN** a discovery recorded its anime as never fully fetched, and something has since cached that anime's full detail
- **THEN** the airing-status gate is applied to the cached record, no MyAnimeList call is made, and the announcement is recorded if the gate passes

#### Scenario: A discovery that cannot announce spends no call

- **WHEN** a discovery recorded its anime as already fully fetched
- **THEN** no MyAnimeList call is made for it and the discovery is marked processed

#### Scenario: Mixed verdicts for one anime still announce

- **WHEN** two unprocessed discoveries name the same anime, one recorded when it was new to the system and one recorded after it had been fetched
- **THEN** one announcement update is recorded for it

#### Scenario: A failed resolution is retried

- **WHEN** processing a discovery fails to fetch the related anime for any reason other than MyAnimeList answering that it has no such anime
- **THEN** the discovery is left unprocessed and is attempted again on the next pass

#### Scenario: A discovery naming an anime MyAnimeList does not have is closed

- **WHEN** processing a discovery gets a not-found answer from MyAnimeList for the related anime
- **THEN** the discovery is marked processed, no announcement update is recorded, and it is never considered again

#### Scenario: Two discoveries of the same anime announce once

- **WHEN** two anime in my list both gain a relation edge to the same newly-announced anime in one refresh pass
- **THEN** one announcement update is recorded for that anime

#### Scenario: Discoveries predating the feature announce nothing

- **WHEN** the capability is first deployed over a database already holding relation discoveries
- **THEN** those discoveries are marked processed and the updates log starts empty

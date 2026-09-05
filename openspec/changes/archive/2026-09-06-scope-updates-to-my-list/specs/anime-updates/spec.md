## ADDED Requirements

### Requirement: Nothing is recorded for an anime outside my list and its direct relations

Before recording any update, the system SHALL establish that the anime it concerns is **either**:

- a list entry of my own whose status is not Dropped; **or**
- connected, by one relation step in either direction, to at least one such entry — by any relation MyAnimeList reports that external adjudication has not contradicted.

An anime that is neither SHALL have nothing recorded for it, in any kind, from any writer.

This SHALL be the same test the read side applies under "Updates are shown for my own entries and for their franchises", so the two can never disagree about which updates exist. It SHALL be evaluated against the state at the moment of recording; an anime becoming eligible later SHALL NOT cause anything to be recorded retroactively.

The gate SHALL apply to recording only. Anime metadata SHALL continue to be cached exactly as it is today — season browsing, the Top-Anime rankings and search still cache everything they need, and only the detection of news falls silent for an anime outside the list and its direct relations.

#### Scenario: A stranger's episode count is not recorded

- **WHEN** a refresh writes a previously-unknown episode count for an anime that is not my entry and holds no relation to any non-Dropped entry of mine
- **THEN** no update is recorded, and the anime is cached as normal

#### Scenario: Browsing a season records nothing

- **WHEN** I browse a season page and its listings are cached
- **THEN** no update and no relation discovery is recorded for any anime in the season that is not my entry or directly linked to one

#### Scenario: Opening a stranger's page records nothing

- **WHEN** I open the detail page of an anime unrelated to my list and it is fetched and cached
- **THEN** no update is recorded for it

#### Scenario: A directly linked anime is recorded

- **WHEN** a refresh writes a previously-unknown premiere date for an unaired anime that is the sequel of an entry I am watching
- **THEN** a premiere-date-released update is recorded for it

#### Scenario: A dropped entry records nothing

- **WHEN** a refresh writes a previously-unknown episode count for an anime whose only connection to my list is an entry I have set to Dropped
- **THEN** no update is recorded

#### Scenario: A contradicted link does not make an anime eligible

- **WHEN** an anime that is not my entry holds only a relation edge external adjudication has contradicted to a non-Dropped entry of mine, and a refresh changes its premiere date
- **THEN** no update is recorded

#### Scenario: Eligibility gained later does not backfill

- **WHEN** an anime had a premiere-date change while it was neither my entry nor linked to one, and I add it to my list afterwards
- **THEN** nothing appears for that earlier change, because nothing was recorded for it

### Requirement: Relation discoveries are recorded only on my own entries, and never from a first full fetch

A newly-appeared relation edge SHALL be recorded as a discovery only when **both** hold of the anime whose relation set changed:

- it is a list entry of my own whose status is not Dropped; and
- it had been fully fetched before the fetch that produced the change.

An anime's **first** full-detail fetch SHALL record no discoveries at all. Its whole relation set arriving at once is the system finally looking, not links newly appearing, and this SHALL hold whether the anime had no cached row or only a lean listing row.

A new anime appearing in the relation set of an anime that is *not* my own entry SHALL record no discovery, so an announcement can only ever originate one relation step from my list.

The comparison that finds newly-appeared edges SHALL still run for every anime whose relation set is written, whatever this requirement records: its other consumer — the background series rebuild that carries a new member onto the series page without waiting for a visit — SHALL be unaffected by these rules.

#### Scenario: A new sequel on my own show is a discovery

- **WHEN** a refresh of an anime I have on Plan to watch, already fully fetched, adds a relation edge to an anime the system has never fetched
- **THEN** a discovery is recorded for that edge

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

### Requirement: Rows recorded before the relevance rules are retired

On first deployment of the rules above, the system SHALL delete:

- every update recorded for an anime that is neither a non-Dropped list entry of mine nor connected by one non-contradicted relation step to such an entry;
- every announcement recorded for an anime that **already** held a list entry of mine, of any status, at the moment the announcement was recorded, since such an anime was known to the system before it was ever announced; and
- every relation discovery whose anime is not a non-Dropped list entry of mine.

An announcement recorded for an anime that held no list entry of mine at the time, and that I added to my list only afterward, SHALL NOT be retired by this rule: it was genuine news when it was recorded, and adding the anime later does not make it stale.

Retirement SHALL apply the same relevance test the recording gate applies, including the exclusion of relation edges external adjudication has contradicted, rather than any looser approximation of it.

A retired row SHALL NOT come back: the rules above prevent it from ever being recorded again.

Relation discoveries SHALL be safe to delete outright, since the announcement resolver is their only reader; the relation edges the series page is built from live elsewhere and SHALL NOT be touched.

#### Scenario: An invisible update is deleted

- **WHEN** the change is deployed over a database holding an update for an anime with no list entry of mine and no relation to one
- **THEN** that update is deleted, having never been visible in the menu

#### Scenario: A false announcement about my own entry is deleted

- **WHEN** the same deployment finds an announcement recorded for an anime that already held a list entry of mine at the time the announcement was recorded
- **THEN** that announcement is deleted

#### Scenario: An announcement followed by adding the anime is kept

- **WHEN** the same deployment finds an announcement recorded for an anime that held no list entry of mine at the time, and I added it to my list only afterward
- **THEN** that announcement is left exactly as recorded

#### Scenario: An eligible update is kept

- **WHEN** the same deployment finds an update for an anime that is my own non-Dropped entry, or is directly linked to one by an edge nothing has contradicted
- **THEN** that update is left exactly as recorded

#### Scenario: Stale discoveries are cleared

- **WHEN** the same deployment finds relation discoveries recorded on anime that are not non-Dropped entries of mine
- **THEN** those discoveries are deleted, and the relation edges the series page reads are unchanged

## MODIFIED Requirements

### Requirement: What the system records as an update

Subject to "Nothing is recorded for an anime outside my list and its direct relations", the system SHALL record an update when, and only when, one of the following becomes true of an anime. Each condition below describes what qualifies as news; whether it is recorded at all is settled first by that gate.

**Facts becoming known — recorded once each:**

- **Announced** — an anime the system has never fully fetched, and holds no list entry of mine for, appears for the first time in the relation set of a non-Dropped list entry of mine.
- **Episode count released** — an anime's stored total episode count moves from unknown to a known number, whether that number came from MyAnimeList or, per the `episode-airing-data` capability, from AniList where MyAnimeList publishes none. The two are the same event and SHALL be recorded identically; the update SHALL NOT name or otherwise distinguish which source supplied the figure.
- **Premiere date released** — an anime's stored premiere date moves from unknown to a known date.

**Schedule moving — recorded every time it happens:**

- **Premiere date changed** — an anime's stored premiere date moves from one known date to a different known date.
- **Broadcast slot changed** — an anime's stored weekly broadcast day or time changes.
- **Episodes moved** — the stored air date of one or more episodes an anime has not yet aired changes.

An update SHALL carry the anime it concerns, the moment it was detected, and which of the kinds it covers.

Episode count released and episodes moved SHALL be the only kinds AniList data can give rise to. No other kind SHALL be recorded from an AniList value — not an announcement, not a premiere date becoming known, not a premiere or broadcast-slot change — since AniList supplies none of those fields to begin with.

A change to an anime's score, rank, popularity, synopsis, background, studio, genres, source, or picture SHALL NOT be an update. Those are metadata drift; none of them changes what there is to watch or when.

A stored value moving from a known value to unknown SHALL NOT be an update, in any of the kinds. A total episode count moving between two known values SHALL NOT be an update either, whichever source each figure came from — only its move from unknown to known is news.

#### Scenario: An episode count arrives

- **WHEN** a refresh writes a total episode count for a qualifying anime whose stored count was unknown
- **THEN** an episode-count-released update is recorded for that anime

#### Scenario: An episode count arrives from AniList

- **WHEN** an airing-data refresh writes an AniList-supplied total episode count for a qualifying anime MyAnimeList publishes no total for, and whose stored count was unknown
- **THEN** an episode-count-released update is recorded for that anime, indistinguishable from one raised by a MyAnimeList refresh

#### Scenario: A count already known is not news again

- **WHEN** an airing-data refresh reads an AniList total for an anime whose stored count is already known
- **THEN** no update is recorded, whether AniList's figure matches the stored one or differs from it

#### Scenario: AniList's other values are not news

- **WHEN** an airing-data refresh reads AniList's airing status or start date alongside the episode count
- **THEN** no announcement, premiere-date or broadcast-slot update is recorded from them

#### Scenario: A premiere date arrives

- **WHEN** a refresh writes a premiere date for a qualifying anime whose stored premiere date was unknown
- **THEN** a premiere-date-released update is recorded for that anime

#### Scenario: A premiere is delayed

- **WHEN** a refresh changes a qualifying unaired anime's stored premiere date from 5 October to 12 October
- **THEN** a premiere-date-changed update is recorded for that anime

#### Scenario: A broadcast slot moves

- **WHEN** a refresh changes a qualifying airing anime's stored broadcast day from Mondays to Saturdays, or its broadcast time within the same day
- **THEN** a broadcast-slot-changed update is recorded for that anime

#### Scenario: An upcoming episode is pushed back

- **WHEN** an airing-data refresh moves a qualifying anime's next unaired episode from one date to a later one
- **THEN** an episodes-moved update is recorded for that anime

#### Scenario: A value going missing is not news

- **WHEN** a refresh leaves an anime whose episode count was 12 with an unknown count
- **THEN** no update is recorded

#### Scenario: A score change is not news

- **WHEN** a refresh changes an anime's MAL score, rank, synopsis, studio or picture and nothing else
- **THEN** no update is recorded

### Requirement: An announcement is recorded only for an anime that has not finished airing

An announcement SHALL be recorded only for an anime the system had **never fully fetched** and holds **no list entry** of mine for, of any status. Both conditions SHALL be established *before* the resolver fetches the anime, since that fetch would otherwise leave every anime looking already-known. An anime the system had already fully fetched, or that I already track, SHALL record nothing however newly the relation edge naming it appeared.

Before recording an announcement, the system SHALL additionally establish the newly-related anime's airing status by fetching it, and SHALL record the announcement only when that status is *not yet aired* or *currently airing*. A newly-related anime that has finished airing SHALL record nothing.

Both gates SHALL be evaluated once, when the announcement is recorded, and SHALL NOT be re-evaluated afterwards: an anime announced before it aired keeps its announcement once it starts airing.

MAL adds missing relation edges to long-finished anime routinely; such an edge is a correction to relation data reaching the system, not an announcement of a new show. Taken with the never-fully-fetched condition, an announcement means exactly one thing: something the system had never seen appeared in the relations of an anime on my list.

#### Scenario: A newly-announced sequel is recorded

- **WHEN** a discovery on one of my entries names an anime that has never been fully fetched, holds no list entry of mine, and has not yet aired
- **THEN** an announcement update is recorded for it

#### Scenario: An anime already fully fetched is not announced

- **WHEN** a discovery names an anime the system had already fully fetched before the discovery was processed
- **THEN** no announcement update is recorded, and the discovery is marked processed

#### Scenario: An anime on my own list is not announced

- **WHEN** a discovery names an anime I have been watching for weeks
- **THEN** no announcement update is recorded for it

#### Scenario: A newly-linked old anime is not announced

- **WHEN** a discovery names an anime that finished airing in 2005
- **THEN** no announcement update is recorded

#### Scenario: An announcement survives its own premiere

- **WHEN** an anime announced three weeks ago starts airing
- **THEN** its announcement update remains recorded and continues to be shown

### Requirement: Newly-discovered relations are resolved before they become news

The system SHALL mark each recorded relation discovery as processed once it has been considered for an announcement. Processing one SHALL:

- establish, before any fetch, whether the newly-related anime had ever been fully fetched and whether I hold a list entry for it;
- where neither is true, ensure it has a cached record — fetching it from MyAnimeList where it has none, or where its only row is a lean listing one carrying no airing status — and apply the airing-status gate above;
- where either is true, record nothing and spend no MyAnimeList call; and
- mark the discovery processed whether or not an announcement resulted.

A processing attempt that fails to obtain the anime's record SHALL leave the discovery unprocessed, so the next pass retries it. Processing SHALL be idempotent: a discovery already processed SHALL never be considered again.

Where more than one discovery names the same newly-related anime, the system SHALL record one announcement for it, not one per discovery.

Discoveries recorded before this capability existed SHALL be marked processed and SHALL announce nothing.

#### Scenario: A discovery with no cached anime is fetched

- **WHEN** a discovery names a related anime with no cached record
- **THEN** that anime is fetched once, cached, and then considered for an announcement

#### Scenario: A discovery on a lean row is fetched for its airing status

- **WHEN** a discovery names an anime whose only cached row came from a season listing and carries no airing status
- **THEN** that anime is fetched once so the airing-status gate has something to read

#### Scenario: A discovery that cannot announce spends no call

- **WHEN** a discovery names an anime the system had already fully fetched
- **THEN** no MyAnimeList call is made for it and the discovery is marked processed

#### Scenario: A failed resolution is retried

- **WHEN** processing a discovery fails to fetch the related anime
- **THEN** the discovery is left unprocessed and is attempted again on the next pass

#### Scenario: Two discoveries of the same anime announce once

- **WHEN** two anime in my list both gain a relation edge to the same newly-announced anime in one refresh pass
- **THEN** one announcement update is recorded for that anime

#### Scenario: Discoveries predating the feature announce nothing

- **WHEN** the capability is first deployed over a database already holding relation discoveries
- **THEN** those discoveries are marked processed and the updates log starts empty

### Requirement: Every path that writes anime data detects the updates it can

Detection SHALL happen wherever the data it reads is written: episode count, premiere date and broadcast slot wherever cached anime metadata is written from a MyAnimeList fetch, whether that fetch is a full-detail one or a **lean listing** one (season browsing, Top-Anime rankings, and reconciliation caching a list entry); episode count again wherever an AniList-supplied total is written, which is the airing-data refresh; and moved episodes wherever per-episode airing rows are replaced.

Each path SHALL detect the kinds its own data can produce and no others. The airing-data refresh writes an episode count and per-episode airing rows, so it detects episode-count-released and episodes-moved; it SHALL NOT be routed through the MyAnimeList-metadata detection, which would diff fields AniList never supplied. A lean listing write detects the fields it writes — the episode count every lean write carries, and the premiere date season browsing writes so a listing can be classified to its premiere season — and SHALL NOT diff relations, which a lean write never touches.

A manually-triggered refresh SHALL therefore produce the same updates the scheduled one would, and no future refresh path can be added that silently skips detection. A path that writes a detected field without detecting is not merely incomplete: it absorbs the change, so the next path that does detect finds the new value already stored and reports nothing.

Every path SHALL nonetheless record only what "Nothing is recorded for an anime outside my list and its direct relations" allows. Season browsing and the Top-Anime rankings therefore record nothing for the anime they cache in bulk, since those are overwhelmingly neither my entries nor linked to one; they keep their detection so that an anime that *is* linked to my list is still covered wherever it happens to be written.

Writing an anime's metadata, or its per-episode airing rows, for the **first** time SHALL record nothing: a first observation is not a value becoming known or moving, and there is no earlier state to compare against.

For anime metadata, "the first time" SHALL mean the anime's first **full-detail** fetch, not the first time a row existed for it. A lean listing write caches an anime without the detail-only fields — premiere date, airing status, broadcast slot — so a row that has never been fully fetched holds no observation of them to compare against, and the fetch that first supplies them SHALL record nothing, exactly as an insert does. Where a lean listing write is itself the first write for an anime, it SHALL likewise record nothing. This SHALL cover relations as well as fields: a first full-detail fetch records neither an update nor a discovery.

Whether an anime has ever been fully fetched SHALL be read from the timestamp of its last full-detail fetch, captured before the fetch being detected stamps it, and SHALL NOT be inferred from whether any particular field is populated — the same rule `metadata-refresh` applies when deciding whether a detail page needs a fetch.

#### Scenario: A manual refresh produces news

- **WHEN** I refresh a single anime of my own by hand and that fetch writes a previously-unknown episode count
- **THEN** an episode-count update is recorded, exactly as the scheduled refresh would have recorded it

#### Scenario: The airing refresh detects its own kinds only

- **WHEN** an airing-data refresh writes an AniList total and replaces a qualifying anime's airing rows
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

#### Scenario: A lean row's first full fetch records no discovery either

- **WHEN** that same first full-detail fetch stores the anime's relation set for the first time
- **THEN** no discovery is recorded from it, and no announcement follows from that fetch

#### Scenario: A lean listing write detects the count it reveals

- **WHEN** a season-browse listing writes a total episode count for an already-fully-fetched anime of my own whose stored count was unknown
- **THEN** an episode-count-released update is recorded

#### Scenario: A lean listing write detects the premiere it moves

- **WHEN** a season-browse listing writes a different premiere date for an already-fully-fetched anime of my own that has not finished airing
- **THEN** a premiere-date-changed update is recorded, reporting the date it moved from

#### Scenario: A lean listing write to a never-fetched row records nothing

- **WHEN** a Top-Anime listing writes an episode count for an anime that has never had a full-detail fetch
- **THEN** no update is recorded

#### Scenario: A bulk listing write records nothing for strangers

- **WHEN** a season browse or a Top-Anime refresh writes episode counts and premiere dates across hundreds of already-fully-fetched anime that are neither my entries nor linked to one
- **THEN** no update is recorded for any of them

### Requirement: Updates are shown for my own entries and for their franchises

An update SHALL be shown where the anime it concerns is **either**:

- a list entry of my own whose status is not Dropped; **or**
- connected to at least one such entry by any relation MyAnimeList reports that external adjudication has not contradicted.

The first case exists because an anime I added to my list before it had any data is precisely an anime whose data I am waiting for: its episode count and premiere date are news to me whether or not it belongs to a franchise I already follow.

The second case is deliberately **not** narrowed to the same-story relations a series is built from (`SeriesRelations.TraversalSet`): a relation MAL classifies as `alternative_setting`, `character`, or `other` still counts, read in both directions the way an anime's own relation set is read. Series-building keeps its own narrower rule — traversing every relation type here would make the two disagree about what belongs to "the same series", which they don't need to; eligibility here only asks whether the anime is franchise-adjacent enough to be news, not whether it belongs on the series page. The one exclusion that still applies is a relation external adjudication (AniList) has actively contradicted — that's evidence the edge isn't real, not a matter of how loosely "related" is defined.

This SHALL be evaluated when the updates are read, not stored on the update. Dropping an entry SHALL therefore retire its updates from every view without any cleanup, and restoring it SHALL bring them back.

Adding an anime to my list SHALL NOT surface updates recorded before it qualified, because the recording gate means no such update exists.

Eligibility for display SHALL NOT be narrowed by which kind an update covers, nor by whether the anime is my own entry: a premiere-date change on an unlisted sequel of a show I am watching is shown like any other.

Every view of the updates — the navbar menu and the history — SHALL apply this same rule, so the two never disagree about which updates exist.

#### Scenario: A standalone entry of my own qualifies

- **WHEN** an anime I have on Plan to watch, related to nothing else in my list, has its premiere date released
- **THEN** that update is shown

#### Scenario: A sequel to a show I am watching is shown

- **WHEN** an announcement is recorded for an anime that is the sequel of an entry I am watching
- **THEN** it is shown

#### Scenario: Dropping my own entry retires its news

- **WHEN** I set an entry to Dropped and it is connected to nothing else in my list
- **THEN** its updates no longer appear in the menu or in the history

#### Scenario: Dropping the affiliate retires the franchise's news

- **WHEN** the only non-dropped entry connecting an anime to my list is set to Dropped, and the anime is not itself an entry
- **THEN** that anime's updates no longer appear in either view

#### Scenario: A shared-universe link qualifies

- **WHEN** an anime that is not my own entry has only a character or alternative-setting relation to my list
- **THEN** its updates are shown, naming that relation as the reason

#### Scenario: A contradicted edge does not qualify

- **WHEN** an anime that is not my own entry has only a relation edge external adjudication has contradicted
- **THEN** its updates are not shown

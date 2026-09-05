# anime-updates Specification

## Purpose
Detects and surfaces news about anime in and adjacent to the user's list — announcements, episode counts and premiere dates becoming known, and schedule changes to airing or unaired anime — so the user learns what changed without hunting for it. Shown from a control in the navbar, available on every page (last 30 days) and in a searchable, date-filterable history of everything ever recorded.

## Requirements

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

### Requirement: Facts noticed at the same moment form one update

Where a single detection notices more than one kind for the same anime, the system SHALL record **one** update covering every kind noticed, not one update per kind. Kinds noticed at different moments SHALL be separate updates.

#### Scenario: A count and a date released together

- **WHEN** one refresh writes both a previously-unknown episode count and a previously-unknown premiere date for the same anime
- **THEN** one update is recorded for that anime covering both kinds

#### Scenario: A delay that moves the slot too

- **WHEN** one refresh both moves an anime's premiere date and changes its broadcast slot
- **THEN** one update is recorded covering both kinds

#### Scenario: A count released later than the announcement

- **WHEN** an anime is announced with no episode count, and a refresh three weeks later writes its count
- **THEN** there are two updates for that anime — the announcement, and a later episode-count release

#### Scenario: A sequel announced with everything already decided

- **WHEN** a newly-announced anime is resolved and MAL already reports its episode count and premiere date
- **THEN** one announcement update is recorded, and no separate episode-count or premiere-date update is recorded for the same facts

### Requirement: A fact becomes known once; a schedule moves as often as it moves

The system SHALL record at most one **announcement**, one **episode-count release**, and one **premiere-date release** per anime, for all time. A detection noticing one of those kinds already recorded for that anime SHALL record nothing for it. This makes them idempotent: re-running detection, refreshing the same anime by hand, and the scheduled refresh reaching it SHALL between them produce no duplicate.

A value that goes unknown after its release was recorded and later reads as known again SHALL record nothing — there is no earlier known value to have moved from, and re-recording the release would turn upstream flapping into news.

The three **schedule-change** kinds SHALL carry no such limit: each is recorded every time the stored value actually moves, because a premiere delayed twice is two pieces of news. Their guard against duplication is the comparison itself — once the new value is stored, the next detection finds nothing changed.

#### Scenario: A repeated detection records nothing

- **WHEN** an anime whose episode-count release is already recorded is refreshed again
- **THEN** no second episode-count update is recorded

#### Scenario: A flapping value does not re-announce

- **WHEN** an anime's episode count is recorded as released, then reads as unknown on one refresh, then reads as known again
- **THEN** the log still holds exactly one episode-count update for that anime

#### Scenario: A second delay is news again

- **WHEN** an anime's premiere date is moved to a later date, and moved again a month afterwards
- **THEN** two premiere-date-changed updates are recorded

#### Scenario: An unchanged schedule records nothing

- **WHEN** a refresh rewrites an anime's premiere date, broadcast slot and episode dates to the values already stored
- **THEN** no update is recorded

### Requirement: A schedule change is recorded with what it moved from

Because a schedule change is news about a *movement*, the system SHALL store, on the update itself, the value each moved field held beforehand: the previous premiere date, the previous broadcast day and time, and — for moved episodes — the episode number together with the dates it moved from and to.

These stored values SHALL NOT be re-derived from the anime's current record, which by then holds only the new value.

Where several unaired episodes move in one detection, the update SHALL name the **earliest** of them, since that is the next one a viewer is waiting for.

Times SHALL be stored as the system stores broadcast times generally and presented in local time, so an update never reports a slot in a different timezone from the rest of the app.

#### Scenario: A delay reports both dates

- **WHEN** an anime's premiere date moves from 5 October to 12 October
- **THEN** the update reports that it moved from 5 October to 12 October

#### Scenario: A slot change reports the old slot

- **WHEN** an anime's broadcast slot moves
- **THEN** the update reports the previous slot and the new one, both in local time

#### Scenario: Several episodes moving names the earliest

- **WHEN** episodes 7, 8 and 9 all move a week later in one detection
- **THEN** one update is recorded naming episode 7 and the dates it moved between

#### Scenario: A later correction does not rewrite an earlier update

- **WHEN** an anime's premiere date moves again after a premiere-date-changed update was recorded
- **THEN** the earlier update still reports the pair of dates it was recorded for

### Requirement: Schedule changes are recorded only while an anime has not finished airing

The system SHALL record the three schedule-change kinds only for an anime MyAnimeList reports as *not yet aired* or *currently airing*. A schedule field changing on an anime that has finished airing SHALL record nothing.

A premiere date or broadcast slot changing on a show that finished years ago is MyAnimeList correcting its own records, not a schedule moving. Only a show still ahead of, or in the middle of, its broadcast can have its schedule move in a way anyone can act on.

#### Scenario: A correction to an old show is not news

- **WHEN** a refresh changes the stored premiere date of an anime that finished airing in 2011
- **THEN** no update is recorded

#### Scenario: An airing show's slot change is news

- **WHEN** a refresh changes the broadcast slot of a currently-airing anime
- **THEN** a broadcast-slot-changed update is recorded

#### Scenario: An unaired show's delay is news

- **WHEN** a refresh moves the premiere date of a not-yet-aired anime
- **THEN** a premiere-date-changed update is recorded

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

### Requirement: Episodes moved is detected only where per-episode airing data is kept

The system SHALL detect moved episodes by comparing an anime's stored per-episode air dates before and after they are replaced by a refresh, considering only episodes that had not yet aired at the moment of comparison.

Two dates SHALL count as a move when they fall on different **local calendar days**. A shift within the same local day SHALL NOT be a move, so upstream adjustments of minutes never become news.

Detection SHALL therefore reach exactly the anime the system keeps per-episode airing data for. An anime with no such data — one outside my list, including an announced anime not yet added — SHALL simply record no episodes-moved updates, and SHALL still record every other kind.

#### Scenario: A break week is news

- **WHEN** an anime's episode 7 moves from one local date to a later one
- **THEN** an episodes-moved update is recorded

#### Scenario: A minutes-level adjustment is not

- **WHEN** an episode's stored air instant shifts by twenty minutes within the same local day
- **THEN** no update is recorded

#### Scenario: Already-aired episodes are not watched for moves

- **WHEN** the stored air date of an episode that has already aired changes
- **THEN** no update is recorded

#### Scenario: An anime with no per-episode data still gets its other updates

- **WHEN** an announced anime with no stored per-episode airing data has its episode count released
- **THEN** the episode-count update is recorded as normal

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

### Requirement: An update names the anime, what happened, and why it concerns me

Each shown update SHALL report the anime's picture and title; what happened, naming which kinds it covers; the anime's **current** episode count and premiere date, each shown only where known and omitted entirely where not; and why it concerns me — the entry it is affiliated with together with the relation it holds to it, or, where the anime is my own entry and has no such affiliation, that entry's own status.

**Layout.** An update SHALL be presented as a card whose picture sits on the **left**, with the title beside it at the top, the news beneath the title, and the reason beneath the news. Cards SHALL share a common width wherever they are listed, and their height SHALL follow their content, so a card with a tall picture or more to say is taller than one without.

**The picture SHALL be drawn whole, at its own proportions.** No part of it SHALL be cropped away, and it SHALL NOT be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider and shorter, and a square picture SHALL be drawn square — the same rule the `artwork-selection` picker applies to its options. The drawn picture SHALL be bounded in **both** directions, so that a portrait picture cannot make a card towering and a wide picture cannot leave the text beside it unreadably narrow; a picture too wide to fit that bound SHALL be reduced whole rather than cropped. An anime with no picture SHALL show a placeholder of the app's usual poster proportions rather than a card with no picture area at all.

**One fact SHALL read as one line.** The news SHALL be one line per kind the update covers, and a kind whose news *is* a value SHALL state that value in its own line rather than announcing the kind and leaving the value to a separate line — `Total episodes: 12`, not `Episode count revealed` above `12 episodes`; a premiere date released, a premiere moved, a broadcast slot moved and an episode moved SHALL each read the same way, as one self-contained statement. A schedule change SHALL name both the value it moved to and the value it moved from, including for a moved episode.

The current-value lines SHALL then be shown for each fact **no news line already stated**: an episode-count release suppresses the episode-count line, and a premiere date released or changed suppresses the premiere-date line, while an announcement — which states neither — still reports both where known.

Episode count and premiere date SHALL be read from the anime's current cached record rather than from anything captured when the update was recorded, so a later correction is reflected on the update rather than leaving it stating a superseded value. The values a schedule change moved between are the exception, and SHALL be reported as recorded.

A date SHALL be shown with its year, so news about a premiere or an episode a year out is unambiguous.

**Nothing SHALL be clipped except a title in the dropdown.** In the navbar dropdown, a title too long for its card MAY be cut off, and SHALL then be recoverable in full the way the app's other truncated titles are. Every other line, on every surface, SHALL be shown in full, wrapping onto as many lines as it needs.

Where an update's anime is affiliated with more than one non-dropped entry, the system SHALL name one of them deterministically, preferring the most specific relation. Where the anime is both my own entry and affiliated with another, the affiliation SHALL be named, as it says more than the entry's own status does.

Selecting an update SHALL open that anime's detail page.

#### Scenario: An episode count reveal reads as one line

- **WHEN** an update covering an episode-count release is shown for an anime whose total is 12
- **THEN** its news reads `Total episodes: 12` on one line, and no separate episode-count line is shown beneath it

#### Scenario: A premiere reveal reads as one line

- **WHEN** an update covering a premiere-date release is shown
- **THEN** its news states the premiere date itself on one line, and no separate premiere-date line is shown beneath it

#### Scenario: An announcement still reports the facts it arrived with

- **WHEN** an announcement update is shown for an anime whose episode count and premiere date are both known
- **THEN** the card reads `Announced` and then reports the episode count and the premiere date

#### Scenario: A moved episode reports both dates

- **WHEN** an episodes-moved update is shown
- **THEN** its line names the episode, the date it moved to, and the date it moved from

#### Scenario: A portrait picture

- **WHEN** a shown update's anime has a portrait poster
- **THEN** the card draws it portrait, whole, and the card is correspondingly taller

#### Scenario: A landscape picture

- **WHEN** a shown update's anime has a picture wider than it is tall
- **THEN** the card draws it wide and short at its own proportions, whole, without cropping it to a portrait slice, and the text beside it still has room to read

#### Scenario: An extremely wide picture

- **WHEN** a shown update's anime has a picture too wide to fit the space a card gives it
- **THEN** it is reduced until it fits whole, rather than being cropped or pushing the card wider than its neighbours

#### Scenario: Cards differ in height but not in width

- **WHEN** a list holds one card with a tall picture and one with a short picture
- **THEN** the two cards are the same width and differ in height

#### Scenario: A long title in the dropdown

- **WHEN** a card in the navbar dropdown has a title too long for its width
- **THEN** the title is cut off, and the full title is available on hover as elsewhere in the app

#### Scenario: A card with a lot to say

- **WHEN** an update covers a broadcast-slot change, naming both the new slot and the previous one
- **THEN** the whole of that text is shown, wrapping as needed, rather than being cut off at one line

#### Scenario: An anime with no episode count yet

- **WHEN** a shown update's anime has no known episode count
- **THEN** no episode count is displayed for it, and its premiere date is displayed if known

#### Scenario: An anime with neither count nor date

- **WHEN** a shown update's anime has neither a known episode count nor a known premiere date
- **THEN** neither is displayed, and the update still shows its picture, title, news and reason

#### Scenario: A corrected count is reflected

- **WHEN** an anime's episode count is corrected from 12 to 13 after its episode-count update was recorded
- **THEN** that update displays 13

#### Scenario: A delay keeps the dates it was recorded with

- **WHEN** a premiere-date-changed update recorded a move from 5 October to 12 October, and the date later moves again
- **THEN** that update still reports the move from 5 October to 12 October, while a newer update reports the newer move

#### Scenario: The affiliation is named

- **WHEN** an update concerns the sequel of an entry I am watching
- **THEN** it names that entry and states that the update's anime is its sequel

#### Scenario: My own standalone entry names its status

- **WHEN** an update concerns an anime that is my own Plan to watch entry with no affiliation in my list
- **THEN** it reports that entry's own status rather than an affiliation

#### Scenario: Opening an update

- **WHEN** I select a shown update
- **THEN** the detail page for the anime it concerns opens

### Requirement: The updates menu shows the last 30 days, newest first

The system SHALL surface the last-30-days window from a control in the **navbar**, available on every page, rather than from a section on any one page. Selecting that control SHALL open a dropdown holding every shown update detected within the last 30 days, and no update older than that.

The dropdown SHALL carry a heading reading "Updates" at its top left, opposite the History control at its top right.

The dropdown SHALL lay them out as a single column of cards running **top to bottom**, most recently detected at the top. Where the column holds more cards than the dropdown's height, it SHALL scroll vertically rather than drop cards, and reaching either end of that scroll SHALL NOT pass the remaining movement on to the page behind. Scrolling SHALL remain fully usable — by wheel, trackpad, touch, or keyboard — while the scrollbar itself SHALL NOT be rendered, so a compact dropdown reads without one.

On opening, the dropdown SHALL be exactly as tall as its **three newest cards** together with the spacing between them: no fourth card partly visible, and no card cut short. Because card heights vary with their content, that height SHALL be taken from the three cards **as actually rendered** rather than from any assumed per-card height, and SHALL be re-taken whenever those heights change — including as pictures finish loading. Where fewer than three updates are shown, the dropdown SHALL be as tall as they need and no taller. Where three cards would not fit in the window, the dropdown SHALL be reduced to what fits and the rest reached by scrolling.

An update passing 30 days old SHALL leave the dropdown without being deleted, and SHALL remain in the history.

Where the window holds nothing to show, the dropdown SHALL say so in place of the list.

The dropdown SHALL close when Escape is pressed, when a click lands outside it, and when a card in it is followed to an anime's detail page.

#### Scenario: Reachable from every page

- **WHEN** I am on any page of the app
- **THEN** the navbar's updates control is present, and opening it shows the last 30 days of updates without navigating anywhere

#### Scenario: The dropdown is titled

- **WHEN** I open the updates dropdown
- **THEN** it reads "Updates" at its top left, with the History control at its top right

#### Scenario: Scrolling with no visible scrollbar

- **WHEN** the dropdown holds more cards than fit and I scroll it
- **THEN** the list scrolls to reveal the rest, and no scrollbar track or thumb is drawn over or beside it

#### Scenario: Newest at the top

- **WHEN** the dropdown shows three updates detected on different days
- **THEN** the most recently detected is at the top and the oldest at the bottom

#### Scenario: Opening height fits exactly three cards

- **WHEN** I open the dropdown on a window holding more than three updates, whose three newest cards are of differing heights
- **THEN** the dropdown is as tall as exactly those three cards and the spacing between them — the third card is whole and the fourth is not visible until I scroll

#### Scenario: The opening height follows the cards as they render

- **WHEN** a picture in one of the three newest cards finishes loading and changes that card's height
- **THEN** the dropdown's height is re-taken from the three cards as they now stand, still ending exactly at the third

#### Scenario: Fewer than three updates

- **WHEN** the window holds two updates
- **THEN** the dropdown is as tall as those two cards need and shows no empty space beneath them

#### Scenario: Three cards taller than the window

- **WHEN** the three newest cards together are taller than the space beneath the control
- **THEN** the dropdown is reduced to the space available and the remaining cards are reached by scrolling within it

#### Scenario: More cards than fit

- **WHEN** the window holds more cards than the dropdown's height
- **THEN** the dropdown scrolls vertically and every card remains reachable

#### Scenario: Scrolling the list does not scroll the page

- **WHEN** I scroll to the bottom of the dropdown's list and keep scrolling
- **THEN** the page behind the dropdown does not move

#### Scenario: An update ages out

- **WHEN** an update detected 31 days ago would otherwise be shown
- **THEN** it does not appear in the dropdown, and it still appears in the history

#### Scenario: Nothing to show

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the dropdown displays text saying there are no recent updates, in place of the list

#### Scenario: Dismissing the dropdown

- **WHEN** I press Escape, click outside the dropdown, or follow one of its cards to an anime
- **THEN** the dropdown closes

### Requirement: The updates history is searchable and date-filterable

The system SHALL offer, from the **top of the updates dropdown**, a control opening a history of **every** shown update ever recorded, listed chronologically with the most recent first — regardless of age. This control SHALL read as one of the app's ordinary secondary buttons — plain text, bordered — rather than in the site's accent colour, since it is not the primary action the dropdown exists for.

The history SHALL offer a search over the anime's titles and a from/to date range, both filtering the list as they are set, and a control clearing them that appears only while a filter is active. It SHALL distinguish "nothing has been recorded" from "nothing matches these filters".

The control opening the history SHALL remain available while the dropdown has nothing recent to show, so older updates stay reachable.

Every row of the history SHALL show **everything it carries**: its picture whole and at its own proportions, its title in full, and every line of its news and its reason in full, wrapping rather than being cut off. A row SHALL be as tall as its own content requires, so rows differ in height; the history SHALL NOT impose a common row height that clips what a row holds.

The history's own list SHALL be sized exactly the same way the dropdown sizes itself: as tall as its **three newest rows** together with the spacing between them, taken from the rows as actually rendered and re-taken as their heights change, so the third row is never cut off short whatever it contains. Where the list holds more rows than that, it SHALL scroll to reach them, remaining fully usable by wheel, trackpad, touch, or keyboard while the scrollbar itself SHALL NOT be rendered — matching the dropdown's own scrolling. The search field and date range SHALL stay in view above the list regardless.

#### Scenario: The history holds everything

- **WHEN** I open the history
- **THEN** it lists every shown update ever recorded, most recent first, including those older than 30 days

#### Scenario: Reached from the top of the dropdown

- **WHEN** I open the updates dropdown
- **THEN** the control opening the full history is at the top of it, above the cards

#### Scenario: The History control reads as a secondary button

- **WHEN** the updates dropdown is open
- **THEN** its History control shows plain, bordered styling rather than the site's accent colour

#### Scenario: Searching by title

- **WHEN** I type part of an anime's title into the history's search
- **THEN** only updates whose anime matches that text remain listed

#### Scenario: Filtering by date range

- **WHEN** I set a from date and a to date
- **THEN** only updates detected within that range, inclusive of both days, remain listed

#### Scenario: No match under a filter

- **WHEN** a filter matches none of the recorded updates
- **THEN** the history says nothing matches the filters, distinctly from saying nothing has been recorded

#### Scenario: History reachable from an empty dropdown

- **WHEN** the dropdown has no updates within the last 30 days
- **THEN** the control opening the history is still available and the history still lists the older updates

#### Scenario: A long row is shown in full

- **WHEN** the history lists an update whose news and reason are longer than one line each
- **THEN** the row grows to show all of that text rather than cutting it off

#### Scenario: A history row's picture is whole

- **WHEN** the history lists an update whose anime has a landscape or square picture
- **THEN** that picture is drawn whole at its own proportions, not cropped into a portrait thumbnail

#### Scenario: Rows differ in height

- **WHEN** the history lists one update with a short line of news and one with several
- **THEN** the two rows differ in height, each as tall as its own content

#### Scenario: Opening height fits exactly three rows

- **WHEN** the history holds more than three updates, whose three newest rows are of differing heights
- **THEN** the list is as tall as exactly those three rows and the spacing between them — the third row is whole and the fourth is not visible until I scroll

#### Scenario: Scrolling the history with no visible scrollbar

- **WHEN** the history holds more rows than fit and I scroll it
- **THEN** the list scrolls to reveal the rest, and no scrollbar track or thumb is drawn over or beside it

#### Scenario: Filters stay in view

- **WHEN** I scroll a long history
- **THEN** the search field and date range stay visible above the list

### Requirement: The updates control signals unseen news

The navbar's updates control SHALL carry an indicator whenever at least one update in the last-30-days window is **newer than the newest update already shown to me**, and SHALL carry no indicator otherwise. The indicator SHALL be reflected in the control's accessible name as well as visually, so it is not carried by appearance alone.

Opening the dropdown SHALL mark everything then in the window as shown, clearing the indicator. The indicator SHALL stay clear across reloads until an update newer than that is detected, at which point it SHALL appear again.

Where the system holds no record of anything having been shown, every update in the window SHALL count as unseen. The record SHALL be kept locally, alongside the app's other per-browser viewing preferences, and losing it SHALL do no more than raise the indicator once.

An update older than 30 days SHALL never raise the indicator, whether or not it was ever shown: the indicator covers the window the dropdown covers.

#### Scenario: A newly detected update raises the indicator

- **WHEN** an update is detected after the last time I opened the dropdown
- **THEN** the navbar's updates control shows its indicator

#### Scenario: Opening clears it

- **WHEN** I open the dropdown while the indicator is showing
- **THEN** the indicator clears, and stays clear when I close and reopen the dropdown

#### Scenario: It stays clear across a reload

- **WHEN** I open the dropdown, then reload the app with no new update detected in between
- **THEN** the control shows no indicator

#### Scenario: Only news newer than what I have seen counts

- **WHEN** I have opened the dropdown, and one further update is detected afterwards
- **THEN** the indicator appears for that one update and clears when I next open the dropdown

#### Scenario: First use

- **WHEN** the app has no record of my having been shown any update, and the window holds updates
- **THEN** the control shows its indicator until I open the dropdown

#### Scenario: An empty window shows no indicator

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the control shows no indicator, and the control itself is still present

#### Scenario: Ageing out does not raise it

- **WHEN** the newest update in the window is one I have already been shown, and older updates exist in the history
- **THEN** the control shows no indicator

### Requirement: The updates control is marked while its panel is open

The navbar's updates control SHALL carry the same treatment a navbar page control carries while its page is being viewed — a tinted background together with a border stronger than the one hovering produces — for as long as what it opened is on screen. This SHALL hold while its dropdown is open, and SHALL continue to hold while the updates history overlay opened from that dropdown is open, so the control that produced what is on screen stays visibly the source of it.

Hovering the control while it is marked SHALL NOT replace or weaken the mark, exactly as hovering the current page's navbar link does not.

Closing the dropdown, or closing the history overlay, SHALL clear the mark.

This mark SHALL be independent of the unseen-news indicator: the control SHALL be markable with or without the indicator showing, and the indicator's own rules SHALL be unaffected. Because opening the dropdown clears the indicator, the ordinary case is a marked control with no indicator.

#### Scenario: Opening the dropdown marks the control
- **WHEN** I open the updates dropdown
- **THEN** the updates control shows the same tinted background and border a navbar link shows for the page I am on

#### Scenario: The mark survives following History
- **WHEN** I open the dropdown and follow **History** to the updates history overlay
- **THEN** the updates control stays marked for as long as that overlay is open

#### Scenario: Closing clears the mark
- **WHEN** I close the dropdown, or close the history overlay
- **THEN** the updates control returns to its unmarked appearance

#### Scenario: Hovering the marked control keeps it marked
- **WHEN** I move the pointer over the updates control while its dropdown is open
- **THEN** it keeps its stronger mark rather than falling back to the hover border

#### Scenario: The mark and the indicator are independent
- **WHEN** I open the dropdown while the unseen indicator is showing
- **THEN** the control becomes marked and the indicator clears on its own existing rules, neither state being derived from the other

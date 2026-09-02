# anime-updates Specification

## Purpose
Detects and surfaces news about anime in and adjacent to the user's list — announcements, episode counts and premiere dates becoming known, and schedule changes to airing or unaired anime — so the user learns what changed without hunting for it. Shown on the Home page's Updates section (last 30 days) and in a searchable, date-filterable history of everything ever recorded.

## Requirements
### Requirement: What the system records as an update

The system SHALL record an update when, and only when, one of the following becomes true of an anime:

**Facts becoming known — recorded once each:**

- **Announced** — an anime the system has not recorded an announcement for appears, for the first time, in the relation set of an anime that already had a cached record.
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

- **WHEN** a refresh writes a total episode count for an anime whose stored count was unknown
- **THEN** an episode-count-released update is recorded for that anime

#### Scenario: An episode count arrives from AniList

- **WHEN** an airing-data refresh writes an AniList-supplied total episode count for an anime MyAnimeList publishes no total for, and whose stored count was unknown
- **THEN** an episode-count-released update is recorded for that anime, indistinguishable from one raised by a MyAnimeList refresh

#### Scenario: A count already known is not news again

- **WHEN** an airing-data refresh reads an AniList total for an anime whose stored count is already known
- **THEN** no update is recorded, whether AniList's figure matches the stored one or differs from it

#### Scenario: AniList's other values are not news

- **WHEN** an airing-data refresh reads AniList's airing status or start date alongside the episode count
- **THEN** no announcement, premiere-date or broadcast-slot update is recorded from them

#### Scenario: A premiere date arrives

- **WHEN** a refresh writes a premiere date for an anime whose stored premiere date was unknown
- **THEN** a premiere-date-released update is recorded for that anime

#### Scenario: A premiere is delayed

- **WHEN** a refresh changes an unaired anime's stored premiere date from 5 October to 12 October
- **THEN** a premiere-date-changed update is recorded for that anime

#### Scenario: A broadcast slot moves

- **WHEN** a refresh changes an airing anime's stored broadcast day from Mondays to Saturdays, or its broadcast time within the same day
- **THEN** a broadcast-slot-changed update is recorded for that anime

#### Scenario: An upcoming episode is pushed back

- **WHEN** an airing-data refresh moves an anime's next unaired episode from one date to a later one
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

### Requirement: An announcement is recorded only for an anime that has not finished airing

Before recording an announcement, the system SHALL establish the newly-related anime's airing status by fetching it, and SHALL record the announcement only when that status is *not yet aired* or *currently airing*. A newly-related anime that has finished airing SHALL record nothing.

This gate SHALL be evaluated once, when the announcement is recorded, and SHALL NOT be re-evaluated afterwards: an anime announced before it aired keeps its announcement once it starts airing.

MAL adds missing relation edges to long-finished anime routinely; such an edge is a correction to relation data reaching the system, not an announcement of a new show.

#### Scenario: A newly-announced sequel is recorded

- **WHEN** a refresh discovers a new relation edge to an anime that has not yet aired
- **THEN** an announcement update is recorded for it

#### Scenario: A newly-linked old anime is not announced

- **WHEN** a refresh discovers a new relation edge to an anime that finished airing in 2005
- **THEN** no announcement update is recorded

#### Scenario: An announcement survives its own premiere

- **WHEN** an anime announced three weeks ago starts airing
- **THEN** its announcement update remains recorded and continues to be shown

### Requirement: Newly-discovered relations are resolved before they become news

The system SHALL keep a record of every relation edge newly discovered on a cached anime, and SHALL mark each as processed once it has been considered for an announcement. Processing one SHALL:

- ensure the newly-related anime has a cached record, fetching it from MyAnimeList when it has none;
- apply the airing-status gate above; and
- mark the discovery processed whether or not an announcement resulted.

A processing attempt that fails to obtain the anime's record SHALL leave the discovery unprocessed, so the next pass retries it. Processing SHALL be idempotent: a discovery already processed SHALL never be considered again.

Where more than one discovery names the same newly-related anime, the system SHALL record one announcement for it, not one per discovery.

Discoveries recorded before this capability existed SHALL be marked processed and SHALL announce nothing.

#### Scenario: A discovery with no cached anime is fetched

- **WHEN** a discovery names a related anime with no cached record
- **THEN** that anime is fetched once, cached, and then considered for an announcement

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

Detection SHALL happen wherever the data it reads is written: episode count, premiere date and broadcast slot wherever cached anime metadata is written from a MyAnimeList fetch; episode count again wherever an AniList-supplied total is written, which is the airing-data refresh; and moved episodes wherever per-episode airing rows are replaced.

Each path SHALL detect the kinds its own data can produce and no others. The airing-data refresh writes an episode count and per-episode airing rows, so it detects episode-count-released and episodes-moved; it SHALL NOT be routed through the MyAnimeList-metadata detection, which would diff fields AniList never supplied.

A manually-triggered refresh SHALL therefore produce the same updates the scheduled one would, and no future refresh path can be added that silently skips detection.

Writing an anime's metadata, or its per-episode airing rows, for the **first** time SHALL record nothing: a first observation is not a value becoming known or moving, and there is no earlier state to compare against.

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

### Requirement: Updates are shown for my own entries and for their franchises

An update SHALL be shown where the anime it concerns is **either**:

- a list entry of my own whose status is not Dropped; **or**
- connected to at least one such entry by any relation MyAnimeList reports that external adjudication has not contradicted.

The first case exists because an anime I added to my list before it had any data is precisely an anime whose data I am waiting for: its episode count and premiere date are news to me whether or not it belongs to a franchise I already follow.

The second case is deliberately **not** narrowed to the same-story relations a series is built from (`SeriesRelations.TraversalSet`): a relation MAL classifies as `alternative_setting`, `character`, or `other` still counts, read in both directions the way an anime's own relation set is read. Series-building keeps its own narrower rule — traversing every relation type here would make the two disagree about what belongs to "the same series", which they don't need to; eligibility here only asks whether the anime is franchise-adjacent enough to be news, not whether it belongs on the series page. The one exclusion that still applies is a relation external adjudication (AniList) has actively contradicted — that's evidence the edge isn't real, not a matter of how loosely "related" is defined.

This SHALL be evaluated when the updates are read, not stored on the update. Dropping an entry SHALL therefore retire its updates from every view without any cleanup, and adding an anime to my list SHALL surface updates already recorded for it.

Every view of the updates — the Home section and the history — SHALL apply this same rule, so the two never disagree about which updates exist.

#### Scenario: A standalone entry of my own qualifies

- **WHEN** an anime I have on Plan to watch, related to nothing else in my list, has its premiere date released
- **THEN** that update is shown

#### Scenario: A sequel to a show I am watching is shown

- **WHEN** an announcement is recorded for an anime that is the sequel of an entry I am watching
- **THEN** it is shown

#### Scenario: Dropping my own entry retires its news

- **WHEN** I set an entry to Dropped and it is connected to nothing else in my list
- **THEN** its updates no longer appear in the Home section or in the history

#### Scenario: Dropping the affiliate retires the franchise's news

- **WHEN** the only non-dropped entry connecting an anime to my list is set to Dropped, and the anime is not itself an entry
- **THEN** that anime's updates no longer appear in either view

#### Scenario: Adding the anime surfaces its news

- **WHEN** an update was recorded for an anime I had no entry for and whose franchise I do not follow, and I then add it to my list
- **THEN** that update appears without being re-recorded

#### Scenario: A shared-universe link qualifies

- **WHEN** an anime that is not my own entry has only a character or alternative-setting relation to my list
- **THEN** its updates are shown, naming that relation as the reason

#### Scenario: A contradicted edge does not qualify

- **WHEN** an anime that is not my own entry has only a relation edge external adjudication has contradicted
- **THEN** its updates are not shown

### Requirement: An update names the anime, what happened, and why it concerns me

Each shown update SHALL report the anime's picture and title; a headline naming which kinds it covers; the anime's **current** episode count and premiere date, each shown only where known and omitted entirely where not; and why it concerns me — the entry it is affiliated with together with the relation it holds to it, or, where the anime is my own entry and has no such affiliation, that entry's own status.

Episode count and premiere date SHALL be read from the anime's current cached record rather than from anything captured when the update was recorded, so a later correction is reflected on the update rather than leaving it stating a superseded value. The values a schedule change moved between are the exception, and SHALL be reported as recorded.

Where an update's anime is affiliated with more than one non-dropped entry, the system SHALL name one of them deterministically, preferring the most specific relation. Where the anime is both my own entry and affiliated with another, the affiliation SHALL be named, as it says more than the entry's own status does.

Selecting an update SHALL open that anime's detail page.

#### Scenario: An anime with no episode count yet

- **WHEN** a shown update's anime has no known episode count
- **THEN** no episode count is displayed for it, and its premiere date is displayed if known

#### Scenario: An anime with neither count nor date

- **WHEN** a shown update's anime has neither a known episode count nor a known premiere date
- **THEN** neither is displayed, and the update still shows its picture, title, headline and reason

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

### Requirement: The Updates section shows the last 30 days, newest first

The Home page's Updates section SHALL show every shown update detected within the last 30 days, and no update older than that. It SHALL lay them out as a single row of horizontal cards running left to right, most recently detected leftmost. Where the row holds more cards than fit, it SHALL scroll horizontally rather than wrap to a second row or drop cards.

The section SHALL use the full width of the page.

An update passing 30 days old SHALL leave the section without being deleted, and SHALL remain in the history.

Where the section has nothing to show, it SHALL say so in place of the row.

#### Scenario: Newest first

- **WHEN** the section shows three updates detected on different days
- **THEN** the most recently detected is leftmost and the oldest is rightmost

#### Scenario: An update ages out

- **WHEN** an update detected 31 days ago would otherwise be shown
- **THEN** it does not appear in the section, and it still appears in the history

#### Scenario: More cards than fit

- **WHEN** the section holds more cards than the page width fits
- **THEN** the row scrolls horizontally and every card remains reachable on that one row

#### Scenario: Nothing to show

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the section displays text saying there are no recent updates, in place of the row

### Requirement: The updates history is searchable and date-filterable

The system SHALL offer, from the Updates section, a control opening a history of **every** shown update ever recorded, listed chronologically with the most recent first — regardless of age.

The history SHALL offer a search over the anime's titles and a from/to date range, both filtering the list as they are set, and a control clearing them that appears only while a filter is active. It SHALL distinguish "nothing has been recorded" from "nothing matches these filters".

The control opening the history SHALL remain available while the section has nothing recent to show, so older updates stay reachable.

#### Scenario: The history holds everything

- **WHEN** I open the history
- **THEN** it lists every shown update ever recorded, most recent first, including those older than 30 days

#### Scenario: Searching by title

- **WHEN** I type part of an anime's title into the history's search
- **THEN** only updates whose anime matches that text remain listed

#### Scenario: Filtering by date range

- **WHEN** I set a from date and a to date
- **THEN** only updates detected within that range, inclusive of both days, remain listed

#### Scenario: No match under a filter

- **WHEN** a filter matches none of the recorded updates
- **THEN** the history says nothing matches the filters, distinctly from saying nothing has been recorded

#### Scenario: History reachable from an empty section

- **WHEN** the section has no updates within the last 30 days
- **THEN** the control opening the history is still available and the history still lists the older updates

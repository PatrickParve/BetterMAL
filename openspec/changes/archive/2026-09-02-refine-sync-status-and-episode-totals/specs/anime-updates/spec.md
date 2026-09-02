## MODIFIED Requirements

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

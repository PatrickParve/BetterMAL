## MODIFIED Requirements

### Requirement: Newly-discovered relations are resolved before they become news

The system SHALL mark each recorded relation discovery as processed once it has been considered for an announcement. Processing one SHALL:

- establish, before any fetch, whether the newly-related anime had ever been fully fetched and whether I hold a list entry for it;
- where neither is true, ensure it has a cached record — fetching it from MyAnimeList where it has none, or where its only row is a lean listing one carrying no airing status — and apply the airing-status gate above;
- where either is true, record nothing and spend no MyAnimeList call; and
- mark the discovery processed whether or not an announcement resulted.

A processing attempt that fails to obtain the anime's record SHALL leave the discovery unprocessed, so the next pass retries it, except when MyAnimeList answers that it has no such anime. That discovery SHALL be marked processed and SHALL announce nothing, since an anime MyAnimeList has deleted can never produce news and retrying it would cost a call on every pass forever. Processing SHALL be idempotent: a discovery already processed SHALL never be considered again.

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

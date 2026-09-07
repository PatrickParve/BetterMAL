## MODIFIED Requirements

### Requirement: Series list read endpoint
The system SHALL expose a read endpoint returning every series eligible for the Series page, each carrying the figures a card shows and the figures the page sorts and filters by: series id — which is the series' root anime id, carried once rather than as two fields — title and English title, picture, status, progress badge, both main-line averages, whether the MAL average may be revealed, first and last year, main-line episode total with its lower-bound marker, entry count, main-line episodes watched and aired, the count of main-line entries that have started airing, and the average position the series' main-line entries hold in my rankings.

The **main-line aired entry count** SHALL count main-line members that have started airing — currently airing or finished airing — and SHALL exclude any that has not aired at all, matching the figure the profile's Top series section already uses for its own multi-entry filter.

The **average ranking position** SHALL be the mean, over the series' main-line members that my rankings cover, of each member's overall position in those rankings, computed over the whole main line. A member my rankings do not cover — an unscored member above all — SHALL be excluded from the mean altogether: it SHALL NOT contribute a rank and SHALL NOT count toward the divisor. A series with no covered main-line member SHALL carry no value at all rather than a placeholder figure. The ranking itself SHALL be the one the app derives from my entries and my stored hand-ordering — this endpoint SHALL read it, never store or duplicate it, so a score edit changes both the average and the position with no rebuild.

The endpoint SHALL compute those figures at read time from stored series members and my list, and SHALL NOT build, rebuild, or refresh any series, and SHALL make no MyAnimeList request — so its cost is bounded by what is stored no matter how incomplete the store is.

The endpoint SHALL return the whole eligible set in one response rather than a page of it, so the client can sort across everything without re-fetching. It SHALL return them in a deterministic default order — my main-line average descending, then display title — so a client that applies no ordering of its own still renders sensibly.

Score averages SHALL NOT be read from storage but computed from members' current scores, so editing a score changes what this endpoint returns with no rebuild.

#### Scenario: The endpoint returns eligible series only
- **WHEN** the endpoint is called on a store holding series both with and without members in my list
- **THEN** only the series with a member in my list are returned

#### Scenario: One id per series
- **WHEN** the endpoint returns a series whose root entry is the anime with MAL id 1735
- **THEN** it carries the single id 1735, which the card uses both to identify the series and to open its page

#### Scenario: The endpoint makes no MAL calls
- **WHEN** the endpoint is called while some stored series are partial
- **THEN** it returns immediately from stored data, builds nothing, and makes no MyAnimeList request

#### Scenario: An empty store returns an empty list
- **WHEN** the endpoint is called before any series has been built
- **THEN** it returns an empty list rather than an error

#### Scenario: A score edit is reflected immediately
- **WHEN** I change my score on one entry and the endpoint is called again
- **THEN** that series' my-average reflects the new score with no rebuild

#### Scenario: The aired entry count excludes an unaired sequel
- **WHEN** a series has two main-line entries of which one has been announced but has aired nothing
- **THEN** its main-line aired entry count is 1

#### Scenario: The average position covers only ranked members
- **WHEN** a series' main line holds one entry ranked 5th and two entries my rankings do not cover
- **THEN** its average ranking position is 5, not 5 divided across three members

#### Scenario: An unscored member is excluded from the mean
- **WHEN** a series' main line holds two entries I have scored, ranked 4th and 6th, and two I have not scored
- **THEN** its average ranking position is 5

#### Scenario: A series with nothing ranked carries no position
- **WHEN** no main-line member of a series appears in my rankings
- **THEN** the endpoint returns no average position for it, rather than a zero or a maximum

#### Scenario: A score edit moves the position too
- **WHEN** I raise my score on a main-line entry enough to move it up my rankings and the endpoint is called again
- **THEN** that series' average position reflects the new ranking, with no rebuild

#### Scenario: The response is deterministically ordered
- **WHEN** the endpoint is called twice with no data change
- **THEN** both responses list the series in the same order

## MODIFIED Requirements

### Requirement: Series sorting
The system SHALL offer a sort control over the whole listed set, with these orders:

| Sort | Key | Direction |
|---|---|---|
| Alphabetical | display title, case-insensitive | ascending |
| MAL average | main-line MAL average | descending |
| My average | main-line my-score average | descending |
| Status | `Airing`, then `Ongoing`, then `Finished` | that fixed order |
| Newest | the series' first-aired date | descending — the most recently started series first |
| Oldest | the series' first-aired date | ascending — the earliest started series first |
| My progress | main-line episodes watched divided by main-line episodes aired so far | descending — completed first, nothing watched last |
| Time spent | the series' total watch time | descending — the most time spent first, nothing watched last |

Sorting SHALL apply to every listed series at once, not only to those already scrolled into view, and switching sort SHALL NOT re-fetch.

A series the sort key cannot rank — no MAL average, no my-average, no known first-aired date — SHALL be placed after every series the key can rank, rather than being dropped from the list or sorted as a zero. Under My progress, a series with no main-line episodes aired at all SHALL be placed after every series with a real ratio, including those whose ratio is zero.

**Time spent SHALL sort by the same franchise total the profile's "Most time spent" section ranks by**, as the `profile-stats` capability defines it: first viewings plus rewatches, summed over **every** member of the series (main line and extras alike, version neighbours it holds included), each valued at that member's episode duration, with a member not in my list contributing nothing. The Series page SHALL NOT compute its own variant of this figure. The two surfaces SHALL read one total, so a series listed in both carries the same figure in each.

This total is a sort key and not a card figure. It SHALL cover every member rather than the default combination of alternatives the card's episode figures use, because time I spent watching a non-default alternative is still time spent.

The Time spent sort SHALL order by this total alone. It SHALL NOT apply the profile section's main-series listing rule. A series the Series page lists but "Most time spent" omits, such as one of which I have watched only an extra, SHALL still be ordered by its own total. A series with nothing watched has a total of zero, which is a real figure rather than an unrankable one. It SHALL sort after every series with a total above zero, among the other zero-total series by title.

Every sort SHALL break ties on display title ascending, so the order is total and stable.

**My average SHALL break its ties further before falling back to the title**, since a great many series share an average and title order says nothing about them. Where two series' my-averages compare equal, they SHALL be ordered by:

1. **the average position their main-line entries hold in my rankings**, ascending — the series whose entries sit nearer the top of my rankings first. The figure SHALL be the mean rank over the series' main-line entries **that hold a rank**, taken over the whole main line rather than over the default combination of alternatives the card's episode figures use.

   An entry that holds no rank SHALL be **left out of the mean entirely** — not counted as a worst rank, not counted as a zero, and not counted as a member the mean is divided by. An entry I have not scored holds no rank, and so does any other entry my rankings do not cover, so an unscored entry can neither raise nor lower its series' average position: a series of four entries where I have scored two is described by the mean of those two ranks alone, exactly as a series of two entries where I have scored both would be.

   A series **none** of whose main-line entries holds a rank has no such figure at all, and SHALL be placed after every tied series that has one.
2. **the count of main-line episodes aired so far**, descending — the larger series first;
3. **display title**, ascending, as every sort ends.

Two series that both have **no** my-average compare equal under the sort key and SHALL take this same chain, rather than being ordered by title alone.

This chain SHALL apply to the My average sort only. Every other sort SHALL keep breaking ties on display title ascending, exactly as it does today.

The MAL average sort SHALL order by the underlying average whether or not the hide-scores toggle is blurring it, matching how the Season page's MAL score sort already behaves — a sort control that silently stopped working when scores are hidden would be worse than one whose order implies what it orders by.

The page SHALL open on **My average**, and SHALL keep the chosen sort in the URL so a link carries it and back-navigation restores it.

#### Scenario: Sorting alphabetically uses the displayed title
- **WHEN** I sort alphabetically and a series' English title differs from its raw title
- **THEN** it is ordered by the title the card actually displays

#### Scenario: Sorting by my average
- **WHEN** I sort by my average
- **THEN** the series with the highest main-line my-average is first, and series I have scored nothing in come last

#### Scenario: Tied averages fall to my rankings
- **WHEN** two series both average 8.00 and the main-line entries of one sit at ranks 3 and 7 in my rankings while the other's sit at ranks 40 and 60
- **THEN** the first is placed above the second

#### Scenario: Only ranked entries count toward the average position
- **WHEN** a tied series has three main-line entries of which only one is ranked
- **THEN** its average position is that one entry's rank, not a figure penalised for the two without one

#### Scenario: An unscored entry does not drag its series down
- **WHEN** two series are tied on my average, one having four main-line entries of which I have scored the two sitting at ranks 4 and 6, and the other having exactly those two entries and nothing else
- **THEN** both have the same average position, because the two unscored entries hold no rank and are left out of the mean rather than counted against it

#### Scenario: Scoring a further entry is what moves the position
- **WHEN** I then score one of those unscored entries and it lands at rank 200
- **THEN** that series' average position drops to the mean of the three ranks, since the entry now holds one

#### Scenario: A tied series with nothing ranked sorts after those with ranks
- **WHEN** two series are tied on my average and none of one series' main-line entries appears in my rankings
- **THEN** that series is placed after the tied series that do have an average position

#### Scenario: Still tied falls to episodes aired
- **WHEN** two series are tied on my average and on their average position in my rankings, and one has 62 main-line episodes aired against the other's 24
- **THEN** the 62-episode series is placed first

#### Scenario: Still tied falls to the title
- **WHEN** two series are tied on my average, on average position, and on episodes aired
- **THEN** they are ordered by display title ascending

#### Scenario: Two unscored series still order sensibly
- **WHEN** I sort by my average and two series both have no my-average at all
- **THEN** they are ordered by average position in my rankings, then episodes aired, then title — not by title alone

#### Scenario: Other sorts are unchanged
- **WHEN** I sort by MAL average and several series share the same MAL average
- **THEN** they are ordered by display title ascending, exactly as before

#### Scenario: Sorting by status
- **WHEN** I sort by status
- **THEN** airing series come first, then ongoing, then finished

#### Scenario: Newest puts the latest start first
- **WHEN** I sort by Newest and one series began in 2024 while another began in 2005
- **THEN** the 2024 series is placed before the 2005 one

#### Scenario: Oldest puts the earliest start first
- **WHEN** I sort by Oldest with the same two series
- **THEN** the 2005 series is placed before the 2024 one

#### Scenario: Sorting by my progress
- **WHEN** I sort by my progress
- **THEN** series whose aired main line I have watched in full come first and series with nothing watched come last

#### Scenario: A series with nothing aired sorts last under progress
- **WHEN** I sort by my progress and one listed series has no main-line episodes aired at all
- **THEN** it is placed after every series with a real watched-against-aired ratio, including those at zero

#### Scenario: Sorting by time spent
- **WHEN** I sort by time spent and I have spent four days on one series and six hours on another
- **THEN** the four-day series is placed before the six-hour one

#### Scenario: Time spent matches the profile
- **WHEN** I sort by time spent and compare the order of the series that also appear in the profile's "Most time spent" strip
- **THEN** each of those series carries the same total on both surfaces, so they are ordered the same way relative to one another, apart from ties that each surface breaks on its own title rule

#### Scenario: Extras and rewatches count toward time spent
- **WHEN** a series' total is used for sorting
- **THEN** it includes its extras as well as its main line, and every recorded rewatch of every member as well as each member's first viewing

#### Scenario: A non-default alternative counts toward time spent
- **WHEN** a series' main line holds a version slot and I have watched both alternatives, twelve episodes of one and three of the other, so the card follows the first
- **THEN** the other alternative's three episodes still count toward the series' total, even though the card's episode figures follow only the default combination

#### Scenario: A series the profile omits is still ordered by its total
- **WHEN** I sort by time spent and a listed series has only an extra watched, so the profile's "Most time spent" strip omits it
- **THEN** it is placed according to that extra's watch time, not dropped and not sorted as zero

#### Scenario: Nothing watched sorts last under time spent
- **WHEN** I sort by time spent and two listed series have nothing watched at all
- **THEN** both are placed after every series with time spent, ordered between themselves by display title

#### Scenario: Unrankable series are kept, not dropped
- **WHEN** I sort by MAL average and some listed series have no MAL average at all
- **THEN** they are still listed, placed after every series that has one

#### Scenario: Sorting covers the whole list
- **WHEN** I change the sort without having scrolled to the end of the list
- **THEN** the reordering is over every listed series, so the new first card may be one that was not previously rendered

#### Scenario: Switching sort does not re-fetch
- **WHEN** I change the sort
- **THEN** the order changes from data already loaded, with no new request

#### Scenario: MAL sort works while scores are hidden
- **WHEN** the hide-scores toggle is on and I sort by MAL average
- **THEN** the cards are ordered by their MAL averages, with the values still blurred

#### Scenario: The sort survives back-navigation
- **WHEN** I sort by Newest, open a series, and navigate back
- **THEN** the page is still sorted by Newest

#### Scenario: Default sort
- **WHEN** I open the Series page with no sort specified
- **THEN** it is sorted by my average

### Requirement: Series list read endpoint
The system SHALL expose a read endpoint returning every series eligible for the Series page, each carrying the figures a card shows and the figures the page sorts and filters by: series id — which is the series' root anime id, carried once rather than as two fields — title and English title, picture, status, progress badge, both main-line averages, whether the MAL average may be revealed, first and last year, main-line episode total with its lower-bound marker, entry count, main-line episodes watched and aired, the count of main-line entries that have started airing, the average position the series' main-line entries hold in my rankings, and the series' total watch time.

The **main-line aired entry count** SHALL count main-line members that have started airing — currently airing or finished airing — and SHALL exclude any that has not aired at all, matching the figure the profile's Top series section already uses for its own multi-entry filter.

The **average ranking position** SHALL be the mean, over the series' main-line members that my rankings cover, of each member's overall position in those rankings, computed over the whole main line. A member my rankings do not cover — an unscored member above all — SHALL be excluded from the mean altogether: it SHALL NOT contribute a rank and SHALL NOT count toward the divisor. A series with no covered main-line member SHALL carry no value at all rather than a placeholder figure. The ranking itself SHALL be the one the app derives from my entries and my stored hand-ordering — this endpoint SHALL read it, never store or duplicate it, so a score edit changes both the average and the position with no rebuild.

The **total watch time** SHALL be the franchise total the profile's "Most time spent" section ranks by, in whole seconds, computed over every member by that section's per-member rules and not by a restatement of them. Every eligible series SHALL carry it, including one "Most time spent" omits. A series with nothing watched SHALL carry zero rather than no value. Like every other figure here it SHALL be computed at read time from members' current entries, so marking an episode watched or recording a rewatch changes it with no rebuild.

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

#### Scenario: The watch total equals the profile's
- **WHEN** a series appears both in this endpoint's response and in the profile's "Most time spent" section
- **THEN** both carry the same total watch time for it

#### Scenario: A series the profile omits still carries its total
- **WHEN** a listed series has only an extra watched, a two-hour movie
- **THEN** the endpoint returns it with a total watch time of two hours, though "Most time spent" does not list it

#### Scenario: Nothing watched carries zero
- **WHEN** every member of a listed series in my list is plan-to-watch with no episodes watched
- **THEN** the endpoint returns a total watch time of zero for it

#### Scenario: Watching an episode moves the total
- **WHEN** I mark one more episode of a series' member watched and the endpoint is called again
- **THEN** that series' total watch time has grown by that member's episode duration, with no rebuild

#### Scenario: The response is deterministically ordered
- **WHEN** the endpoint is called twice with no data change
- **THEN** both responses list the series in the same order

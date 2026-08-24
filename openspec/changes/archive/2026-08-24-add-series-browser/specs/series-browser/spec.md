## ADDED Requirements

### Requirement: The Series page lists every series with a member in my list
The system SHALL provide a Series page at `/series` listing every stored series that has at least one member — main line or extra, of any watch status — in my list.

A stored series with no member in my list SHALL NOT be listed. Series are derived from the relation graph by exploration as well as from my list (see the `series-page` capability), so a franchise the app built while I browsed a season or opened an unrelated detail page is not part of my library and SHALL NOT appear.

Membership in my list SHALL be the only eligibility rule. In particular the page SHALL NOT apply the watched-coverage rule the profile's Top series section applies (which requires two of a multi-entry main line to be in my list): that rule exists because Top series ranks franchises by an average, which one entry would misrepresent, whereas this page lists rather than ranks. The two surfaces SHALL therefore be permitted to list different sets of series.

The page SHALL NOT build or rebuild any series, and SHALL make no MyAnimeList request. It renders what is stored.

#### Scenario: A series with one entry in my list is listed
- **WHEN** exactly one member of a five-entry franchise is in my list, whatever its status
- **THEN** that series appears on the Series page

#### Scenario: An explored-only series is not listed
- **WHEN** a series was built while I browsed a season and none of its members is in my list
- **THEN** that series does not appear on the Series page

#### Scenario: An extra-only membership still lists the series
- **WHEN** the only member of a series in my list is one of its extras, not a main-line entry
- **THEN** the series appears on the Series page

#### Scenario: Opening the page costs no fetches
- **WHEN** I open the Series page
- **THEN** no series is built or rebuilt and no MyAnimeList request is made

#### Scenario: The page lists series Top series omits
- **WHEN** a franchise has three aired main-line entries of which only one is in my list
- **THEN** it appears on the Series page even though the profile's Top series section excludes it

### Requirement: Series card content
Each listed series SHALL be shown as a card in a grid, in the same card and grid form the Season and Search results pages use, carrying:

- the series' main picture — the root entry's picture, the same one the series page's header shows — or a placeholder when the root has none;
- the series' display title, with the English title preferred exactly as it is elsewhere in the app;
- the MAL average and my average across the **main line**, each rendered to two decimals, matching the series page's `MAL · main series` and `Mine · main series` chips in both computation and appearance;
- the series' status pill, one of `Airing`, `Ongoing`, `Upcoming`, or `Finished`, computed by the precedence the `series-page` capability defines and carrying the same four distinct colours the series page uses;
- my progress badge — `Completed`, `Caught up`, or `N behind` — or no badge, by the precedence the next requirement defines, carrying the same colours the series page uses for those states;
- the series' year span, rendered as `2013 – 2023`, or as the single year when every entry aired in one year;
- the main-line episode total; and
- the count of entries in the series.

The episode total SHALL be the **main-line** total, computed exactly as the series page's main-series episode total is: a member with a known episode count contributes it in full, a member without contributes its known aired-so-far count instead, and any unknown SHALL mark the figure as a lower bound (`62+ ep`) rather than presenting it as exact. A total of zero that is itself marked unknown SHALL read as unknown rather than as `0+ ep`.

The entry count SHALL cover **every** member, extras included — the same figure the series badge shows for a series in search results — so the two figures deliberately describe different member sets and each is labelled for the set it covers.

The whole card SHALL link to that series' page, targeting the series' root anime id.

#### Scenario: A card carries every figure
- **WHEN** the Series page renders a card for a franchise of four main-line seasons and three extras spanning 2013 to 2023
- **THEN** the card shows the root's picture, the series title, a MAL and a my average, a status pill, the year span "2013 – 2023", the main-line episode total, and an entry count of 7

#### Scenario: A single-year series shows one year
- **WHEN** every entry of a series aired in 2019
- **THEN** the card's year span reads "2019" rather than "2019 – 2019"

#### Scenario: An unknown episode count is marked as a lower bound
- **WHEN** a main-line entry of a listed series is currently airing with no published total episode count
- **THEN** the card's episode total is shown as a lower bound rather than as an exact figure

#### Scenario: Every main-line count unknown reads as unknown
- **WHEN** a series' only main-line entry has no published total and no known aired-so-far count
- **THEN** the card's episode total reads "Unknown" rather than "0+ ep"

#### Scenario: The episode total covers the main line, the entry count covers everything
- **WHEN** a series has 62 main-line episodes across 4 main-line entries plus 3 extras
- **THEN** the card shows 62 episodes and 7 entries

#### Scenario: Clicking a card opens the series
- **WHEN** I click a series card
- **THEN** the series page for that series opens, resolved from its root anime id

#### Scenario: A rootless picture falls back to a placeholder
- **WHEN** a listed series' root entry has no stored picture
- **THEN** the card renders the same placeholder an anime card renders for a missing picture, not a broken image

### Requirement: A card's progress badge follows the series page's precedence
The system SHALL choose each card's progress badge by the same precedence the `series-page` capability defines for its header's personal badge, evaluated over the series' main line:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Caught up` — every main-line entry that has finished airing (if any) is marked Completed in my list, every currently-airing main-line entry has me watching at least as many episodes as it has broadcast so far, and at least one main-line entry has finished airing or is currently airing.
3. `N behind` — the conditions of (2) hold except that currently-airing main-line entries have broadcast episodes I have not watched. N SHALL be the total of those unwatched broadcast episodes across the main line.
4. No badge — when a main-line entry that has finished airing is not marked Completed in my list, or when no main-line entry has finished airing or is currently airing.

A main-line entry that is not in my list SHALL count as zero episodes watched. An entry that has not aired at all SHALL NOT count against the badge.

When a currently-airing main-line entry's broadcast episode count is unknown, the card SHALL show no badge rather than claiming `Caught up` or inventing a behind count.

A card's badge and the series page's header badge for the same series SHALL agree.

#### Scenario: A finished series I have completed
- **WHEN** every member of a listed series has finished airing and every main-line entry is marked Completed in my list
- **THEN** the card shows a "Completed" badge

#### Scenario: Behind on an airing season
- **WHEN** I have completed every finished-airing main-line entry of a listed series and its currently-airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the card shows a "3 behind" badge

#### Scenario: An airing season not in my list at all
- **WHEN** I have completed every earlier main-line entry and the currently-airing season, with 8 episodes broadcast, is not in my list
- **THEN** the card shows an "8 behind" badge

#### Scenario: Caught up while the next entry is unaired
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the card shows a "Caught up" badge

#### Scenario: An unwatched finished entry means no badge
- **WHEN** a main-line entry of a listed series has finished airing and is not marked Completed in my list
- **THEN** the card shows no progress badge

#### Scenario: An unknown broadcast count means no badge
- **WHEN** a currently-airing main-line entry of a listed series has no known count of episodes broadcast so far
- **THEN** the card shows no progress badge, rather than "Caught up"

#### Scenario: Card and series page agree
- **WHEN** I read a card's badge and then open that series' page
- **THEN** the header shows the same badge

### Requirement: A card's MAL average obeys the series page's reveal rule
The system SHALL apply to each card's MAL average exactly the rule the `series-page` capability applies to its main-series MAL average: the average SHALL be shown in full rather than blurred when every main-line entry that has finished airing is marked **Completed or Dropped** in my list **and** no main-line entry is currently airing. Completed and Dropped SHALL count identically. A finished-airing main-line entry that is not in my list at all SHALL NOT satisfy the rule.

Otherwise, while the global hide-scores toggle is on, the average SHALL be blurred behind the same per-score reveal control every other MAL score uses, with the value absent from the rendered output until revealed. While the toggle is off, every card's MAL average SHALL be shown.

My own average SHALL never be hidden, matching every other surface.

The card SHALL NOT show the count of entries an average was computed over, matching the series page's chips.

#### Scenario: A settled series shows its MAL average
- **WHEN** the hide-scores toggle is on, and every finished-airing main-line entry of a listed series is Completed or Dropped with nothing on its main line currently airing
- **THEN** that card's MAL average is shown in full

#### Scenario: An unsettled series blurs its MAL average
- **WHEN** the hide-scores toggle is on and a listed series has a finished-airing main-line entry I have neither completed nor dropped
- **THEN** that card's MAL average is blurred, with the value absent from the rendered output, and a reveal control is offered in its place

#### Scenario: An airing main line suppresses the reveal
- **WHEN** the hide-scores toggle is on, every finished-airing main-line entry of a listed series is Completed or Dropped, and one main-line entry is currently airing
- **THEN** that card's MAL average stays blurred

#### Scenario: A finished-airing entry missing from my list suppresses the reveal
- **WHEN** the hide-scores toggle is on and a listed series' main line holds a finished-airing entry that is not in my list at all
- **THEN** that card's MAL average stays blurred

#### Scenario: Revealing one card does not reveal the others
- **WHEN** I use one card's reveal control
- **THEN** that card's MAL average is shown and every other blurred card stays blurred

#### Scenario: The card and the series page hide the same thing
- **WHEN** a card's MAL average is blurred and I open that series' page
- **THEN** the page's main-series MAL average is blurred too

#### Scenario: My average is always shown
- **WHEN** the hide-scores toggle is on
- **THEN** every card's my-average is shown in full

#### Scenario: No scored-count suffix
- **WHEN** I read a card's averages
- **THEN** each shows its figure alone, with no "N of M scored" count appended

### Requirement: An unscored average reads as no score
The system SHALL render a card's average as `No score` when no entry in the group it covers carries a score, rather than as `0.00`. My average SHALL exclude entries I have not scored, treating a score of 0 as unscored; the MAL average SHALL exclude entries with no MAL score. Both SHALL be unweighted means over main-line entries, so a movie counts the same as a season.

#### Scenario: A series I have not scored
- **WHEN** I have scored none of a listed series' main-line entries
- **THEN** its my-average reads "No score" rather than 0.00

#### Scenario: Partial scoring averages only what is scored
- **WHEN** I have scored three of a listed series' five main-line entries
- **THEN** its my-average is the mean of those three scores

### Requirement: Series sorting
The system SHALL offer a sort control over the whole listed set, with these orders:

| Sort | Key | Direction |
|---|---|---|
| Alphabetical | display title, case-insensitive | ascending |
| MAL average | main-line MAL average | descending |
| My average | main-line my-score average | descending |
| Status | `Airing`, then `Ongoing`, then `Upcoming`, then `Finished` | that fixed order |
| Newest | the series' first-aired date | descending — the most recently started series first |
| Oldest | the series' first-aired date | ascending — the earliest started series first |
| My progress | main-line episodes watched divided by main-line episodes aired so far | descending — completed first, nothing watched last |

Sorting SHALL apply to every listed series at once, not only to those already scrolled into view, and switching sort SHALL NOT re-fetch.

A series the sort key cannot rank — no MAL average, no my-average, no known first-aired date — SHALL be placed after every series the key can rank, rather than being dropped from the list or sorted as a zero. Under My progress, a series with no main-line episodes aired at all SHALL be placed after every series with a real ratio, including those whose ratio is zero.

Every sort SHALL break ties on display title ascending, so the order is total and stable.

The MAL average sort SHALL order by the underlying average whether or not the hide-scores toggle is blurring it, matching how the Season page's MAL score sort already behaves — a sort control that silently stopped working when scores are hidden would be worse than one whose order implies what it orders by.

The page SHALL open on **My average**, and SHALL keep the chosen sort in the URL so a link carries it and back-navigation restores it.

#### Scenario: Sorting alphabetically uses the displayed title
- **WHEN** I sort alphabetically and a series' English title differs from its raw title
- **THEN** it is ordered by the title the card actually displays

#### Scenario: Sorting by my average
- **WHEN** I sort by my average
- **THEN** the series with the highest main-line my-average is first, and series I have scored nothing in come last

#### Scenario: Sorting by status
- **WHEN** I sort by status
- **THEN** airing series come first, then ongoing, then upcoming, then finished

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
The system SHALL expose a read endpoint returning every series eligible for the Series page, each carrying the figures a card shows: root anime id, series id, title and English title, picture, status, progress badge, both main-line averages, whether the MAL average may be revealed, first and last year, main-line episode total with its lower-bound marker, and entry count.

The endpoint SHALL compute those figures at read time from stored series members and my list, and SHALL NOT build, rebuild, or refresh any series, and SHALL make no MyAnimeList request — so its cost is bounded by what is stored no matter how incomplete the store is.

The endpoint SHALL return the whole eligible set in one response rather than a page of it, so the client can sort across everything without re-fetching. It SHALL return them in a deterministic default order — my main-line average descending, then display title — so a client that applies no ordering of its own still renders sensibly.

Score averages SHALL NOT be read from storage but computed from members' current scores, so editing a score changes what this endpoint returns with no rebuild.

#### Scenario: The endpoint returns eligible series only
- **WHEN** the endpoint is called on a store holding series both with and without members in my list
- **THEN** only the series with a member in my list are returned

#### Scenario: The endpoint makes no MAL calls
- **WHEN** the endpoint is called while some stored series are partial
- **THEN** it returns immediately from stored data, builds nothing, and makes no MyAnimeList request

#### Scenario: An empty store returns an empty list
- **WHEN** the endpoint is called before any series has been built
- **THEN** it returns an empty list rather than an error

#### Scenario: A score edit is reflected immediately
- **WHEN** I change my score on one entry and the endpoint is called again
- **THEN** that series' my-average reflects the new score with no rebuild

#### Scenario: The response is deterministically ordered
- **WHEN** the endpoint is called twice with no data change
- **THEN** both responses list the series in the same order

### Requirement: The Series page states which empty situation it is in
The system SHALL distinguish, on the Series page, between having no series to list and still loading, and SHALL NOT present either as an error.

When no series is stored at all, or none has a member in my list, the page SHALL say that series are still being discovered from my list and SHALL link to the Settings page's "Build all series from my list" action — the same situation and the same remedy the profile page's Top series section already names.

While the list is loading the page SHALL show a loading indicator rather than the empty message.

When the list cannot be loaded, the page SHALL say so rather than showing the empty message, since "nothing here" and "this did not load" are different facts.

#### Scenario: Nothing built yet
- **WHEN** I open the Series page before any series has been built
- **THEN** it says series are still being discovered from my list and links to the Settings page's build action

#### Scenario: Loading is not emptiness
- **WHEN** the Series page's list is still loading
- **THEN** a loading indicator is shown rather than the empty message

#### Scenario: A failed load is not emptiness
- **WHEN** the Series page's list fails to load
- **THEN** the page says it could not be loaded rather than saying there are no series

### Requirement: The Series page follows the app's browse-page presentation
The system SHALL present the Series page with the same page furniture the other browse pages use: its `<h1>` inside a page-specific header wrapper whose heading margin is zeroed, its sort control in a filter/sort cluster at the cluster's shared control height, and its cards in the same fluid grid that fills the content width without introducing horizontal page scroll.

The page SHALL restore with back-navigation as every routed page does: its loaded list, its sort selection, and its scroll position SHALL all be restored rather than rebuilt, including the extent of the list that had been scrolled into view, so a restored deep scroll has the cards to scroll back to.

#### Scenario: The header matches the other browse pages
- **WHEN** the Series page renders
- **THEN** its title sits in a header wrapper with the heading margin zeroed and its sort control sits at the shared cluster control height

#### Scenario: The grid fills the width without horizontal scroll
- **WHEN** the Series page renders at any window width
- **THEN** the grid fills the content width and the page does not scroll horizontally

#### Scenario: Back-navigation restores the page
- **WHEN** I scroll deep into the Series page, open a series, and navigate back
- **THEN** the same list, the same sort, and the same scroll position are restored without the list being reloaded

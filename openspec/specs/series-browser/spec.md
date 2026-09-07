# series-browser Specification

## Purpose
TBD - created by archiving change add-series-browser. Update Purpose after archive.
## Requirements
### Requirement: The Series page lists every series with a member in my list
The system SHALL provide a Series page at `/series` listing every stored series that has at least one member — main line or extra, of any watch status — in my list.

A series is a story component (see the `series-versions` capability), so a franchise whose entries are joined by story relations SHALL be listed **once**, however many of its entries carry version relations. Two tellings joined by nothing but a version relation SHALL be listed as two cards. The page SHALL NOT list a series twice.

A stored series with no member in my list SHALL NOT be listed. Series are derived from the relation graph by exploration as well as from my list (see the `series-page` capability), so a franchise the app built while I browsed a season or opened an unrelated detail page is not part of my library and SHALL NOT appear.

A member held only as a **version neighbour** — an anime outside the series' story component, shown in its More section — SHALL NOT make its host series eligible. Otherwise adding one telling to my list would list every telling adjacent to it.

Membership in my list SHALL be the only eligibility rule. In particular the page SHALL NOT apply the watched-coverage rule the profile's Top series section applies (which requires two of a multi-entry main line to be in my list): that rule exists because Top series ranks franchises by an average, which one entry would misrepresent, whereas this page lists rather than ranks. The two surfaces SHALL therefore be permitted to list different sets of series.

The page SHALL NOT build or rebuild any series, and SHALL make no MyAnimeList request. It renders what is stored.

#### Scenario: A series with one entry in my list is listed
- **WHEN** exactly one member of a five-entry franchise is in my list, whatever its status
- **THEN** that series appears on the Series page

#### Scenario: Two separate tellings are two cards
- **WHEN** I have entries from both Fullmetal Alchemist (2003) and Brotherhood, which share no story relation
- **THEN** the Series page shows a card for each, with its own title, picture, averages and figures

#### Scenario: One franchise is one card
- **WHEN** I have entries from Demon Slayer, whose Mugen Ressha movie and TV arc are alternative versions on the same sequel chain
- **THEN** the Series page shows one card, not two near-identical ones

#### Scenario: Only the telling I follow is listed
- **WHEN** my list holds entries from one telling of a franchise and none from the other
- **THEN** only that telling is listed

#### Scenario: A version-neighbour membership does not list a series
- **WHEN** the only member of a series in my list is a version neighbour it holds as an extra
- **THEN** that series is not listed

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

### Requirement: A card's figures cover its own telling
Every figure on a series card — its averages, its status pill, its progress badge, its year span, its main-line episode total and its entry count — SHALL be computed over the members of **that** series alone.

A **version neighbour** that is a member SHALL count toward the figures of the series that holds it, exactly as any other extra does. Where that neighbour also has a series of its own, it counts on both cards: each card is read on its own to describe one series, and each is right for the series it describes. No surface SHALL sum figures across cards.

Where a series' main line holds a version slot, the card's figures SHALL use the **default** combination of alternatives, as the `series-page` capability's picked-route requirement defines. A card carries no picker, and the browser SHALL NOT re-derive figures per alternative.

Related entries — the untraversed relations the series page shows in More — SHALL NOT count toward any card figure, since they are not members.

#### Scenario: A version neighbour counts on the card that holds it
- **WHEN** the Fullmetal Alchemist (2003) series holds Brotherhood as a version-neighbour extra
- **THEN** the 2003 card's entry count and averages include Brotherhood, and Brotherhood's own card is computed over its own members

#### Scenario: A card uses the default route
- **WHEN** a franchise's main line holds four alternative routes
- **THEN** its card shows the episode total and progress of the default route rather than of all four

#### Scenario: Related entries are not counted
- **WHEN** a series' main line carries twelve `character` relations
- **THEN** its card's entry count is unchanged by them

### Requirement: Series card content
Each listed series SHALL be shown as a card in a grid, in the same card and grid form the Season and Search results pages use, carrying:

- the series' picture — the one the `series-identity` capability resolves, which is the series' chosen picture when it has one and its root entry's displayed picture otherwise, the same one the series page's header shows — or a placeholder when neither exists;
- the series' display title — likewise the one `series-identity` resolves, which is the series' chosen title when it has one and the root entry's title otherwise, with the English title preferred exactly as it is elsewhere in the app when no title has been chosen;
- the MAL average and my average across the **main line**, each rendered to two decimals, matching the series page's `MAL · main series` and `Mine · main series` chips in both computation and appearance;
- the series' status pill, one of `Airing`, `Ongoing`, `Upcoming`, or `Finished`, computed by the precedence the `series-page` capability defines and carrying the same four distinct colours the series page uses;
- my progress badge — one of `Completed`, `Caught up`, `N behind`, `Dropped`, or `Unwatched`, or no badge, by the precedence the next requirement defines, carrying the same colours the series page uses for those states;
- the series' year span, rendered as `2013 – 2023`, or as the single year when every entry aired in one year;
- the main-line episode total; and
- the count of entries in the series.

The episode total SHALL be the **main-line** total, computed exactly as the series page's main-series episode total is: a member with a known episode count contributes it in full, a member without contributes its known aired-so-far count instead, and any unknown SHALL mark the figure as a lower bound (`62+ ep`) rather than presenting it as exact. A total of zero that is itself marked unknown SHALL read as unknown rather than as `0+ ep`.

The entry count SHALL cover **every** member, extras included — the same figure the series badge shows for a series in search results — so the two figures deliberately describe different member sets and each is labelled for the set it covers.

The status pill and the progress badge SHALL each render at a consistent, uniform size regardless of their label's length, matching the `series-page` capability's own sizing rule for its header pill and badge.

The whole card SHALL link to that series' page, targeting the series' root anime id.

#### Scenario: A card carries every figure
- **WHEN** the Series page renders a card for a franchise of four main-line seasons and three extras spanning 2013 to 2023
- **THEN** the card shows the series' resolved picture, the series' resolved title, a MAL and a my average, a status pill, the year span "2013 – 2023", the main-line episode total, and an entry count of 7

#### Scenario: A card shows a chosen title and picture
- **WHEN** a series has been given a chosen title and a chosen picture
- **THEN** its card shows both, rather than the root entry's title and picture

#### Scenario: A card without choices is unchanged
- **WHEN** a series has neither a chosen title nor a chosen picture
- **THEN** its card shows the root entry's title and displayed picture exactly as before

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

#### Scenario: Badges are evenly sized across cards
- **WHEN** I compare a card showing "Finished"/"Completed" against one showing "Airing"/"3 behind"
- **THEN** both cards' pills and badges render at the same consistent size, regardless of label length

### Requirement: A Rewatching entry counts as fully watched in a card's figures
Wherever a series card counts **my watched main-line episodes**, a main-line entry marked **Rewatching** SHALL count as fully watched — as the greater of its own episodes-watched figure and its aired-episode figure — rather than as its current in-progress count. Entering Rewatching resets episodes-watched to zero, so without this rule a franchise the user has seen in full and is part-way through watching again reads as barely started.

This SHALL govern both the card's progress badge and the **My progress** sort, whose key is main-line episodes watched divided by main-line episodes aired so far, so a series being rewatched sorts with the series that are finished rather than with the ones never started.

It SHALL govern only the watched side of each pair: the card's main-line episode total, its aired figure, and its entry count describe the anime rather than the user, and SHALL be unchanged by a rewatch in progress.

The rule SHALL match the `series-page` capability's own rule exactly, so a card and the series page it opens can never disagree about the same series.

#### Scenario: A rewatching series does not sort as unwatched
- **WHEN** I sort by My progress and a series whose main line I have completed in full has its first season marked Rewatching with two episodes watched
- **THEN** it sorts among the fully-watched series, not among those with nothing watched

#### Scenario: A card's episode total is unmoved by a rewatch
- **WHEN** a main-line entry of a listed series is marked Rewatching
- **THEN** the card's main-line episode total and entry count are exactly what they were before the rewatch began

### Requirement: A card's progress badge follows the series page's precedence
The system SHALL choose each card's progress badge by the same precedence the `series-page` capability defines for its header's personal badge, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed or Rewatching in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past does not count.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

A main-line entry marked **Rewatching** SHALL count as fully watched throughout this precedence, per "A Rewatching entry counts as fully watched in a card's figures", and SHALL satisfy rule (1) alongside `Completed`.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count, so an unknown broadcast count SHALL only ever be able to produce no badge once evaluation reaches rules (3)/(4).

A main-line entry that is not in my list SHALL count as zero episodes watched. An entry that has not aired at all SHALL NOT count against the badge.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. `Caught up` and `N behind` SHALL keep their existing colours.

A card's badge and the series page's header badge for the same series SHALL agree.

#### Scenario: A finished series I have completed
- **WHEN** every member of a listed series has finished airing and every main-line entry is marked Completed in my list
- **THEN** the card shows a "Completed" badge in the app's Completed-status colour

#### Scenario: A finished series I am rewatching keeps its Completed badge
- **WHEN** every member of a listed series has finished airing, every main-line entry is marked Completed, and I then mark one of them Rewatching with two episodes watched
- **THEN** the card still shows "Completed", not a behind count

#### Scenario: Behind on an airing season
- **WHEN** I have completed every finished-airing main-line entry of a listed series and its currently-airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the card shows a "3 behind" badge

#### Scenario: An airing season not in my list at all
- **WHEN** I have completed every earlier main-line entry and the currently-airing season, with 8 episodes broadcast, is not in my list
- **THEN** the card shows an "8 behind" badge

#### Scenario: Caught up while the next entry is unaired
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the card shows a "Caught up" badge

#### Scenario: A partially watched finished entry shows a behind count
- **WHEN** a listed series' only main-line entry has finished airing with 12 episodes, I have watched 5, and I have neither completed nor dropped it
- **THEN** the card shows a "7 behind" badge, not no badge

#### Scenario: A dropped entry with nothing watched after it
- **WHEN** a listed series has one main-line entry marked Dropped and no main-line entry released after it has ever been watched
- **THEN** the card shows a "Dropped" badge, in the app's Dropped-status colour

#### Scenario: A dropped entry later resumed does not read as Dropped
- **WHEN** a listed series has an early main-line entry marked Dropped but a later main-line entry has watched episodes
- **THEN** the card does not show "Dropped"; it shows whatever "Caught up"/"N behind" the combined figures produce

#### Scenario: Nothing watched at all
- **WHEN** at least one main-line entry of a listed series has aired and I have watched none of the main line, with no entry marked Dropped
- **THEN** the card shows an "Unwatched" badge, in the app's Plan-to-watch colour

#### Scenario: A rewatching entry does not read as unwatched
- **WHEN** a listed series' only aired main-line entry is marked Rewatching with zero episodes watched
- **THEN** the card does not show "Unwatched"

#### Scenario: An unknown broadcast count means no badge, once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry of a listed series has no known count of episodes broadcast so far, and the series is not "Dropped" or "Unwatched"
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
The system SHALL present the Series page with the same page furniture the other browse pages use: its `<h1>` inside a page-specific header wrapper whose heading margin is zeroed, its sort control and filter buttons in a filter/sort cluster at the cluster's shared control height, and its cards in the same fluid grid that fills the content width without introducing horizontal page scroll.

The page SHALL restore with back-navigation as every routed page does: its loaded list, its sort selection, its filter selections, and its scroll position SHALL all be restored rather than rebuilt, including the extent of the list that had been scrolled into view, so a restored deep scroll has the cards to scroll back to.

#### Scenario: The header matches the other browse pages
- **WHEN** the Series page renders
- **THEN** its title sits in a header wrapper with the heading margin zeroed and its sort control and filter buttons sit at the shared cluster control height

#### Scenario: The grid fills the width without horizontal scroll
- **WHEN** the Series page renders at any window width
- **THEN** the grid fills the content width and the page does not scroll horizontally

#### Scenario: Back-navigation restores the page
- **WHEN** I scroll deep into the Series page, apply filters, open a series, and navigate back
- **THEN** the same list, the same sort, the same filter selections, and the same scroll position are restored without the list being reloaded

### Requirement: Series filtering
The system SHALL offer three independent filter groups over the whole listed set, applied client-side to the already-loaded list alongside sorting — no re-fetch on any filter change:

- **Progress** (multi-select): `Watched` (the card's progress badge is `Completed` or `Caught up` — everything from the main series that has aired has been watched), `Behind`, `Dropped`, `Unwatched`.
- **Status** (multi-select): `Airing`, `Ongoing`, `Upcoming`, `Finished` — the status pill's own four values.
- **Entries** (a single toggle): `Multi-entry only` — while on, only series whose main line holds two or more entries are listed.

Selecting more than one button within a multi-select group SHALL show a series matching **any** selected button in that group (an OR within the group). Selections across groups SHALL AND together: a series SHALL be listed only when it satisfies every group that has a selection. Selecting nothing in a group SHALL apply no filter for that group.

A series whose progress badge is "no badge" SHALL NOT match any of the four Progress buttons — selecting any Progress filter hides it, same as a series that simply doesn't match the selected value.

The **Multi-entry only** toggle SHALL apply the same rule the profile page's Top series control applies, so the two can never disagree about what multi-entry means:

- what counts is the number of **main-line** entries, never the series' total member count: a series with one main-line entry plus any number of extras — OVAs, specials, side stories — counts as single-entry and SHALL be hidden while the toggle is on;
- a main-line entry that has been announced but has not aired a single episode SHALL NOT count toward that total: a series is multi-entry only once a second main-line entry has actually started airing;
- the toggle SHALL default to **off**, so the page lists exactly what it lists today until the toggle is used;
- it SHALL narrow which series are listed and SHALL NOT change the order of those that remain, nor what any card shows;
- it SHALL make its current state apparent without the user having to compare the list before and after pressing it, taking the same active treatment the other filter buttons take.

Filtering SHALL apply to the whole listed set, not only to cards already scrolled into view, exactly as sorting already does, and the filtered set SHALL still be sorted by whatever sort is active.

The selected filters — all three groups — SHALL be kept in the URL, as the sort selection already is, so a link carries them and back-navigation restores them.

#### Scenario: Watched includes both Completed and Caught up
- **WHEN** I select the "Watched" progress filter
- **THEN** the list shows every series whose badge is "Completed" or "Caught up", and hides every other series

#### Scenario: Selecting several progress buttons ORs them together
- **WHEN** I select both "Behind" and "Dropped"
- **THEN** the list shows every series badged either "N behind" or "Dropped"

#### Scenario: Progress and status filters AND together
- **WHEN** I select the "Airing" status filter and the "Behind" progress filter
- **THEN** the list shows only series that are both currently airing and behind

#### Scenario: No selection in a group applies no filter for it
- **WHEN** I select a status filter but no progress filter
- **THEN** the list is narrowed by status alone, with every progress state included

#### Scenario: A series with no badge is excluded by any progress filter
- **WHEN** a listed series shows no progress badge and I select any Progress filter
- **THEN** that series is not shown

#### Scenario: Hiding single-entry series
- **WHEN** I switch "Multi-entry only" on
- **THEN** every series whose main line holds a single aired entry disappears, and the multi-entry series remain in the same relative order

#### Scenario: Extras do not make a series multi-entry
- **WHEN** the toggle is on and a series has one main-line entry plus several extras
- **THEN** that series is hidden, because only its main-line count is considered

#### Scenario: An announced-but-unaired sequel doesn't make a series multi-entry
- **WHEN** the toggle is on and a series has one aired main-line entry plus a second that is announced but has aired nothing
- **THEN** that series is hidden

#### Scenario: The toggle composes with the other groups
- **WHEN** the toggle is on and I also select the "Finished" status filter
- **THEN** only finished multi-entry series are listed

#### Scenario: The toggle defaults to off
- **WHEN** I open the Series page without using the toggle
- **THEN** every eligible series is listed, single-entry ones included

#### Scenario: Switching the toggle off restores the list
- **WHEN** I switch "Multi-entry only" back off
- **THEN** the single-entry series reappear in their sorted positions

#### Scenario: The toggle shows its state
- **WHEN** "Multi-entry only" is on
- **THEN** the control reads as active rather than looking identical to its off state

#### Scenario: The Series page and Top series agree
- **WHEN** a series is hidden by the profile page's Top series multi-entry control
- **THEN** the Series page's "Multi-entry only" toggle hides it too, on the same grounds

#### Scenario: Filtering covers the whole list
- **WHEN** I apply a filter without having scrolled to the end of the list
- **THEN** the filtering is applied over every listed series, not only those already rendered

#### Scenario: Filtering does not re-fetch
- **WHEN** I change a filter selection
- **THEN** the list re-renders from data already loaded, with no new request

#### Scenario: A filtered list stays sorted
- **WHEN** I sort by my average and then filter to "Behind"
- **THEN** the remaining series are still ordered by my average descending

#### Scenario: Filters survive back-navigation
- **WHEN** I select filters, including "Multi-entry only", open a series, and navigate back
- **THEN** the same filters are still applied


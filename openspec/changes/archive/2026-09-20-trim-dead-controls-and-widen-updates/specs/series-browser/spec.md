## MODIFIED Requirements

### Requirement: Series card content
Each listed series SHALL be shown as a card in a grid, in the same card and grid form the Season and Search results pages use, carrying:

- the series' picture — the one the `series-identity` capability resolves, which is the series' chosen picture when it has one and its root entry's displayed picture otherwise, the same one the series page's header shows — or a placeholder when neither exists;
- the series' display title — likewise the one `series-identity` resolves, which is the series' chosen title when it has one and the root entry's title otherwise, with the English title preferred exactly as it is elsewhere in the app when no title has been chosen;
- the MAL average and my average across the **main line**, each rendered to two decimals, matching the series page's `MAL · main series` and `Mine · main series` chips in both computation and appearance;
- the series' status pill, one of `Airing`, `Ongoing`, or `Finished`, computed by the precedence the `series-page` capability defines and carrying the same three distinct colours the series page uses;
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

#### Scenario: A card carries no fourth status
- **WHEN** a listed series has no member that has aired at all
- **THEN** its card's pill reads "Ongoing", and no card anywhere on the page carries a fourth status value

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

### Requirement: Series filtering
The system SHALL offer three independent filter groups over the whole listed set, applied client-side to the already-loaded list alongside sorting — no re-fetch on any filter change:

- **Progress** (multi-select): `Watched` (the card's progress badge is `Completed` or `Caught up` — everything from the main series that has aired has been watched), `Behind`, `Dropped`, `Unwatched`.
- **Status** (multi-select): `Airing`, `Ongoing`, `Finished` — the status pill's own three values. There SHALL be no button for a series none of whose members has aired; such a series reads `Ongoing` and is matched by the `Ongoing` button.
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

#### Scenario: A status a link no longer names is ignored
- **WHEN** I open a link whose URL names a status filter value the page no longer offers
- **THEN** the page lists every series as though no status filter were selected, rather than showing an error or an empty list

#### Scenario: An entirely unaired series is reachable by filter
- **WHEN** a listed series has no member that has aired and I select the "Ongoing" status filter
- **THEN** that series is listed

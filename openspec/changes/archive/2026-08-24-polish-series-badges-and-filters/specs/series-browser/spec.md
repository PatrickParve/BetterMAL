## MODIFIED Requirements

### Requirement: Series card content
Each listed series SHALL be shown as a card in a grid, in the same card and grid form the Season and Search results pages use, carrying:

- the series' main picture — the root entry's picture, the same one the series page's header shows — or a placeholder when the root has none;
- the series' display title, with the English title preferred exactly as it is elsewhere in the app;
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

#### Scenario: Badges are evenly sized across cards
- **WHEN** I compare a card showing "Finished"/"Completed" against one showing "Airing"/"3 behind"
- **THEN** both cards' pills and badges render at the same consistent size, regardless of label length

### Requirement: A card's progress badge follows the series page's precedence
The system SHALL choose each card's progress badge by the same precedence the `series-page` capability defines for its header's personal badge, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past does not count.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count, so an unknown broadcast count SHALL only ever be able to produce no badge once evaluation reaches rules (3)/(4).

A main-line entry that is not in my list SHALL count as zero episodes watched. An entry that has not aired at all SHALL NOT count against the badge.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. `Caught up` and `N behind` SHALL keep their existing colours.

A card's badge and the series page's header badge for the same series SHALL agree.

#### Scenario: A finished series I have completed
- **WHEN** every member of a listed series has finished airing and every main-line entry is marked Completed in my list
- **THEN** the card shows a "Completed" badge in the app's Completed-status colour

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

#### Scenario: An unknown broadcast count means no badge, once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry of a listed series has no known count of episodes broadcast so far, and the series is not "Dropped" or "Unwatched"
- **THEN** the card shows no progress badge, rather than "Caught up"

#### Scenario: Card and series page agree
- **WHEN** I read a card's badge and then open that series' page
- **THEN** the header shows the same badge

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

## ADDED Requirements

### Requirement: Series filtering
The system SHALL offer two independent, multi-select filter groups over the whole listed set, applied client-side to the already-loaded list alongside sorting — no re-fetch on any filter change:

- **Progress**: `Watched` (the card's progress badge is `Completed` or `Caught up` — everything from the main series that has aired has been watched), `Behind`, `Dropped`, `Unwatched`.
- **Status**: `Airing`, `Ongoing`, `Upcoming`, `Finished` — the status pill's own four values.

Selecting more than one button within a group SHALL show a series matching **any** selected button in that group (an OR within the group). Selecting a button in both groups SHALL show only a series matching both groups' selections (an AND across groups). Selecting no button in a group SHALL apply no filter for that group.

A series whose progress badge is "no badge" SHALL NOT match any of the four Progress buttons — selecting any Progress filter hides it, same as a series that simply doesn't match the selected value.

Filtering SHALL apply to the whole listed set, not only to cards already scrolled into view, exactly as sorting already does, and the filtered set SHALL still be sorted by whatever sort is active.

The selected filters SHALL be kept in the URL, as the sort selection already is, so a link carries them and back-navigation restores them.

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
- **WHEN** I select filters, open a series, and navigate back
- **THEN** the same filters are still applied

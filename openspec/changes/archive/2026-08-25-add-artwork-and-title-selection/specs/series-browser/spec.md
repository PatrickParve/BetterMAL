## MODIFIED Requirements

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
- **THEN** the card shows "2019" alone rather than "2019 – 2019"

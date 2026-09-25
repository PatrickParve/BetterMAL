## MODIFIED Requirements

### Requirement: Series card content
Each listed series SHALL be shown as a card in a grid, in the same card and grid form the Season and Search results pages use, carrying:

- the series' picture — the one the `series-identity` capability resolves, which is the series' chosen picture when it has one and its root entry's displayed picture otherwise, the same one the series page's header shows — or a placeholder when neither exists;
- the series' display title — likewise the one `series-identity` resolves, which is the series' chosen title when it has one and the root entry's title otherwise, with the English title preferred exactly as it is elsewhere in the app when no title has been chosen;
- the MAL average and my average across the **main line**, each rendered to two decimals, matching the series page's `MAL · main series` and `Mine · main series` chips in both computation and appearance;
- the series' status pill, one of `Airing`, `Ongoing`, or `Finished`, computed by the precedence the `series-page` capability defines and carrying the same three distinct colours the series page uses;
- my progress badge — one of `Completed`, `Caught up`, `N behind`, `Dropped`, or `Unwatched`, or no badge, by the precedence the next requirement defines, carrying the same colours the series page uses for those states;
- the series' year span, rendered as `2013 – 2023`, or as the single year when every main-line entry aired in one year;
- the main-line episode total; and
- the count of entries in the series.

The episode total SHALL be the **main-line** total, computed exactly as the series page's main-series episode total is: a member with a known episode count contributes it in full, a member without contributes its known aired-so-far count instead, and any unknown SHALL mark the figure as a lower bound (`62+ ep`) rather than presenting it as exact. A total of zero that is itself marked unknown SHALL read as unknown rather than as `0+ ep`.

The year span SHALL be the one the series page's header shows, computed by the same rule the `series-page` capability's "Series page header" requirement defines. It SHALL cover the **main line only**, every alternative version included, and no extra or related entry SHALL stretch it. A card and the page it opens SHALL therefore always show the same years. Where no main-line entry has a known start date, the card SHALL show the same no-year placeholder the page does. The span's first year is the first-aired date the Newest and Oldest sorts order by, so those sorts order a series by when its main series began.

The entry count SHALL cover **every** member, extras included — the same figure the series badge shows for a series in search results — so the two figures deliberately describe different member sets and each is labelled for the set it covers.

The status pill and the progress badge SHALL each render at a consistent, uniform size regardless of their label's length, matching the `series-page` capability's own sizing rule for its header pill and badge.

The whole card SHALL link to that series' page, targeting the series' root anime id.

#### Scenario: A card carries every figure
- **WHEN** the Series page renders a card for a franchise of four main-line seasons spanning 2013 to 2023 and three extras
- **THEN** the card shows the series' resolved picture, the series' resolved title, a MAL and a my average, a status pill, the year span "2013 – 2023", the main-line episode total, and an entry count of 7

#### Scenario: An extra outside the main series does not stretch the card's span
- **WHEN** a series' main line aired from 2013 to 2019 and one of its extras aired in 2023
- **THEN** its card's year span reads "2013 – 2019", the same span the series page's header shows

#### Scenario: Newest orders by the main series' start
- **WHEN** one series' main line began in 2010 with a pilot extra from 2006, and another series' main line began in 2008
- **THEN** sorting by Newest puts the 2010 series before the 2008 one, since the pilot does not count toward its first year

#### Scenario: A card shows a chosen title and picture
- **WHEN** a series has been given a chosen title and a chosen picture
- **THEN** its card shows both, rather than the root entry's title and picture

#### Scenario: A card without choices is unchanged
- **WHEN** a series has neither a chosen title nor a chosen picture
- **THEN** its card shows the root entry's title and displayed picture exactly as before

#### Scenario: A single-year series shows one year
- **WHEN** every main-line entry of a series aired in 2019
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

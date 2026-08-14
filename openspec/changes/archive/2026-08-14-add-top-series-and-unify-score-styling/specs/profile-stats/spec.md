## ADDED Requirements

### Requirement: Top series section
The profile page SHALL show a **Top series** section that ranks my franchises, presented like My top anime and Most rewatched: a horizontally-scrolling, drag-scrollable strip of fixed-size poster tiles, uncapped, using the same tile size and hover behaviour as the other two strips.

A series SHALL be eligible for the section when at least one of its members — main line or extra — is in my list. A series with no member in my list SHALL NOT appear.

Each tile SHALL show the series' poster and **both** of its main-series averages: MAL's average across the main line, and my average across the main line. Those figures SHALL be the same figures that series' own page shows for `MAL · main series` and `Mine · main series`, computed the same way, so the two surfaces can never disagree. The two figures SHALL be distinguishable by the score colour roles (see the `score-presentation` capability).

Opening a tile SHALL navigate to that series' page, not to an anime's detail page.

Each tile SHALL make available, without requiring navigation, how many main-line entries each average was computed over — the same "N of M scored" figure the series page shows beside its chips.

#### Scenario: Ranking my franchises
- **WHEN** I open the profile page
- **THEN** a Top series section lists my series as poster tiles, each showing both its MAL main-series average and my main-series average

#### Scenario: Only series I have watched something of
- **WHEN** a stored series has no member in my list
- **THEN** it does not appear in Top series

#### Scenario: A series I only know through an extra
- **WHEN** the only member of a series in my list is one of its extras
- **THEN** the series is eligible, and its two averages are still computed over its main line only

#### Scenario: Figures match the series page
- **WHEN** I compare a tile's two averages with that series' page
- **THEN** they equal the page's `MAL · main series` and `Mine · main series` averages

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile
- **THEN** I am taken to that series' page

#### Scenario: The strip scrolls rather than wrapping
- **WHEN** I have more series than fit across the section
- **THEN** the strip scrolls horizontally at a fixed tile size, and can be dragged to scroll, exactly like the other two strips

### Requirement: Top series ranking basis
Top series SHALL be ranked by a **main-series average**, and SHALL offer a control that switches which average ranks it: **my score** or **MAL's score**. The control SHALL default to my score.

Ranking SHALL be by the selected average in descending order. Ties SHALL be broken by the number of scored main-line entries the average was computed over, descending — so an average earned across more entries places higher — and then by title, case-insensitively.

Switching the basis SHALL reorder the strip without reloading the page's data and without the strip collapsing or changing height.

Both averages SHALL remain visible on every tile regardless of which basis is selected; the basis chooses the ordering, not what is shown.

#### Scenario: Default ranking
- **WHEN** I open the profile page without having changed the control
- **THEN** Top series is ranked by my main-series average, highest first

#### Scenario: Ranking by MAL's score
- **WHEN** I switch the control to MAL's score
- **THEN** the strip reorders by MAL's main-series average, highest first, and both averages are still shown on every tile

#### Scenario: An average earned over more entries wins a tie
- **WHEN** two series have the same average under the selected basis, one computed over five scored entries and one over two
- **THEN** the one computed over five places first

#### Scenario: Switching the basis is immediate
- **WHEN** I switch the ranking basis
- **THEN** the strip reorders immediately without a visible reload and without collapsing

### Requirement: Series unrankable under the selected basis are omitted
A series that has no value under the **selected** basis SHALL be omitted from the strip while that basis is selected: under my score, a series with no scored main-line entry; under MAL's score, a series whose main line carries no MAL score at all.

Switching the basis MAY therefore change which series are listed, not only their order.

#### Scenario: A series I have not scored
- **WHEN** the basis is my score and a series has no scored main-line entry
- **THEN** it is omitted from the strip

#### Scenario: The same series under MAL's score
- **WHEN** I then switch the basis to MAL's score and that series' main line does carry MAL scores
- **THEN** it appears in the strip

#### Scenario: A main line with no MAL score at all
- **WHEN** the basis is MAL's score and a series' main line is entirely unscored by MAL
- **THEN** it is omitted from the strip

### Requirement: Top series ranking basis is restored on back-navigation
The selected ranking basis SHALL behave like the existing profile filters: it SHALL reset to its default on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation. It SHALL be independent of the My top anime and Most rewatched media-type filters.

#### Scenario: Basis resets on a fresh visit
- **WHEN** I set the basis to MAL's score, navigate away, and later open the profile page fresh
- **THEN** the basis is back to my score

#### Scenario: Basis is restored going back
- **WHEN** I set the basis to MAL's score, open a series from the strip, and press back
- **THEN** the strip is still ranked by MAL's score

#### Scenario: Independent of the other filters
- **WHEN** I change the Top series basis
- **THEN** the My top anime and Most rewatched media-type filters are unchanged

### Requirement: Top series empty and incomplete states
When no series is eligible, the section SHALL say so rather than rendering an empty strip.

Because a series is only known once it has been built, the section SHALL disclose that its coverage grows over time, and its empty state SHALL point at the Settings action that builds every series from my list (see the `series-page` capability) rather than leaving the absence unexplained.

Reading the section SHALL never block on building a series and SHALL never fail because a series is missing — a series not yet built is simply not listed yet.

#### Scenario: Nothing to rank yet
- **WHEN** no series with a member in my list has been built
- **THEN** the section explains that series are still being discovered and names the Settings action that builds them all, instead of showing an empty strip

#### Scenario: Nothing rankable under the selected basis
- **WHEN** series are eligible but none has a value under the selected basis
- **THEN** the section says so for that basis rather than showing an empty strip

#### Scenario: Reading never waits on a build
- **WHEN** I open the profile page while series are still being built in the background
- **THEN** the section renders immediately with whatever series are already known

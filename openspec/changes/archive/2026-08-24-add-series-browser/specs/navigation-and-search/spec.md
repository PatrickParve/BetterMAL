## MODIFIED Requirements

### Requirement: Navbar layout
The system SHALL provide a navbar with two groups of controls: a left group of page links and a right group holding the search field and the account/preference controls. There SHALL be no third, centred group — the search field belongs to the right group.

The left group's links SHALL be, in order: **Home, My List, Series, Recap, Top, Season, Year, Airing**. Series SHALL sit directly to the right of My List and Recap directly to the right of Series, since both are views over the same list — Series grouping it into franchises and Recap slicing it by period. Year SHALL sit directly to the right of Season, since the two browse the same listings at different grains.

The right group's controls SHALL be, in order from left to right: **the search field, the hide/unhide MAL-score toggle, Profile, and the Settings (gear icon) button** — so Settings sits at the navbar's far right edge, Profile immediately to its left, then the score toggle, then the search field. Read right-to-left from the edge, the order is Settings, Profile, score toggle, search field.

Every control SHALL keep the behaviour, hover treatment, and accessible labelling it has today; only the ordering and the search field's group membership change. The search field SHALL keep its own width within the right group rather than being squeezed to the width of a button, and its type-ahead dropdown SHALL stay anchored beneath the field in its new position.

At window widths too narrow for one row, the navbar MAY wrap the search field onto its own row, and SHALL keep the two groups' internal orderings when it does.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Series, Recap, Top, Season, Year, Airing, Profile, or Settings)

#### Scenario: Left group order
- **WHEN** the navbar renders
- **THEN** its left links read Home, My List, Series, Recap, Top, Season, Year, Airing from left to right, with Series directly right of My List, Recap directly right of Series, and Year directly right of Season

#### Scenario: Opening the series browser from the navbar
- **WHEN** I click the navbar's Series link
- **THEN** the Series page opens, listing the series built from my list

#### Scenario: The Series link marks itself current
- **WHEN** I am on the Series page
- **THEN** the navbar's Series link carries the current-page marking, and opening an individual series' page from it does not leave that marking on

#### Scenario: Opening the year browser from the navbar
- **WHEN** I click the navbar's Year link
- **THEN** the year browser opens on the current year

#### Scenario: Right group order
- **WHEN** the navbar renders
- **THEN** its right controls read search field, score toggle, Profile, Settings from left to right, with Settings at the far right edge

#### Scenario: The search field is not centred
- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the search field sits in the right-hand group rather than centred between the two groups

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

#### Scenario: The dropdown follows the field
- **WHEN** I type into the relocated search field
- **THEN** the type-ahead dropdown appears anchored beneath the field, fully within the window rather than clipped at its right edge

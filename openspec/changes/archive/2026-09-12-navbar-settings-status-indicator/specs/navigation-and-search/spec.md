## MODIFIED Requirements

### Requirement: Navbar layout
The system SHALL provide a navbar with two groups of controls: a left group of page links and a right group holding the search field and the account/preference controls. There SHALL be no third, centred group — the search field belongs to the right group.

The left group's links SHALL be, in order: **Home, My List, Series, Recap, Top, Season, Year, Airing**. Series SHALL sit directly to the right of My List and Recap directly to the right of Series, since both are views over the same list — Series grouping it into franchises and Recap slicing it by period. Year SHALL sit directly to the right of Season, since the two browse the same listings at different grains.

The right group's controls SHALL be, in order from left to right: **the search field, the hide/unhide MAL-score toggle, the Updates control, Profile, and the Settings (gear icon) button** — so Settings sits at the navbar's far right edge, Profile immediately to its left, then Updates, then the score toggle, then the search field. Read right-to-left from the edge, the order is Settings, Profile, Updates, score toggle, search field.

The Updates control belongs to the right group rather than the left because it opens a menu over the current page instead of navigating to one, and because it carries per-user state as the other right-group controls do. It SHALL be an icon button of the same size **and resting appearance** as the Settings gear — plain, with no persistent background of its own — rather than reading as a separately-boxed control; it SHALL open and close the updates dropdown the `anime-updates` capability specifies, and its open state SHALL be exposed to assistive technology. Opening it SHALL NOT navigate anywhere or disturb the page behind it.

The Settings control SHALL report, without being opened, that something on the Settings page needs me and that long-running work is in flight: it carries the same indicator the Updates control carries, and a thin progress bar along its bottom edge, both on the terms `navbar-settings-status` sets out. Neither SHALL change its size, its position in the group, its hover treatment, or the fact that clicking it opens the Settings page; and both SHALL be reflected in its accessible name, which is otherwise "Settings" as before.

Every other control SHALL keep the behaviour, hover treatment, and accessible labelling it has today; only the ordering, the new Updates control, the Settings control's own status, and the search field's group membership change. The search field SHALL keep its own width within the right group rather than being squeezed to the width of a button, and its type-ahead dropdown SHALL stay anchored beneath the field in its new position.

At window widths too narrow for one row, the navbar MAY wrap the search field onto its own row, and SHALL keep the two groups' internal orderings when it does. The updates dropdown SHALL stay anchored beneath its own control and within the window at every width the app supports, rather than overflowing the window's right edge.

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
- **THEN** its right controls read search field, score toggle, Updates, Profile, Settings from left to right, with Settings at the far right edge

#### Scenario: Opening the updates menu
- **WHEN** I click the navbar's Updates control on any page
- **THEN** its dropdown opens beneath it over the current page, and I stay on that page

#### Scenario: The Updates control reads like its neighbours
- **WHEN** the Updates control is at rest, neither hovered nor open
- **THEN** it shows no background of its own, matching Profile and the Settings gear rather than standing out as a boxed control

#### Scenario: The updates dropdown stays within the window
- **WHEN** I open the updates dropdown at a narrow window width
- **THEN** it is anchored beneath its control and fully within the window rather than clipped at the window's right edge

#### Scenario: The search field is not centred
- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the search field sits in the right-hand group rather than centred between the two groups

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

#### Scenario: The gear reports without being opened
- **WHEN** changes are held for review and a job is running
- **THEN** the Settings gear carries the Updates control's indicator and a progress bar along its bottom edge, unchanged in size and position, and clicking it still opens the settings page

#### Scenario: The dropdown follows the field
- **WHEN** I type into the relocated search field
- **THEN** the type-ahead dropdown appears anchored beneath the field, fully within the window rather than clipped at its right edge


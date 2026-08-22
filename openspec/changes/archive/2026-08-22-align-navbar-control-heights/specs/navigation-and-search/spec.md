## ADDED Requirements

### Requirement: Navbar page controls and the search field share one height

The navbar's page controls — the six left links (Home, My List, Recap, Top, Season, Airing), the Profile link, and the Settings gear — together with the navbar's search field SHALL all render at exactly the same height, with their tops and bottoms aligned.

The shared height SHALL come from one shared definition rather than from per-control padding values that each happen to land near the same number, so a control added to the navbar later inherits the row's height instead of re-introducing a mismatch. The height SHALL hold across the full range of the app's fluid root font size, rather than agreeing only at one window width.

The hide/unhide score toggle is explicitly NOT part of this requirement. It is its own control family and SHALL keep its current shape, height, and behaviour unchanged.

The shared height SHALL NOT clip any control's contents at any supported window width, and SHALL NOT change any control's behaviour, its accessible name, its keyboard handling, its hover treatment, its position in the navbar's ordering, or where the search field's type-ahead dropdown is anchored.

The search field the Settings page renders shares its component with the navbar's field but is not in the navbar; its size SHALL be unaffected.

#### Scenario: The navbar row is flush

- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the six left links, Profile, the Settings gear, and the search field are all exactly the same height, with their tops and bottoms aligned

#### Scenario: The gear is no longer a small square

- **WHEN** I compare the Settings gear with the Profile link beside it
- **THEN** the two boxes are the same height

#### Scenario: Height holds as the font scales

- **WHEN** the window width changes enough to move the app's fluid root font size
- **THEN** those controls still match one another's height

#### Scenario: Nothing is clipped

- **WHEN** the navbar renders at the widest window width the app supports, where its root font size is largest
- **THEN** every link's text and the search field's text and placeholder are drawn whole, with nothing cut off along the top or bottom

#### Scenario: The score toggle is untouched

- **WHEN** the navbar renders
- **THEN** the hide/unhide score toggle keeps the pill shape, height, knob travel, and eye animation it had before, whether or not that height matches its neighbours

#### Scenario: The Settings page's search field is unaffected

- **WHEN** I open the Settings page and look at its anime-refresh search field
- **THEN** it is the size it was before, unchanged by the navbar's shared height

#### Scenario: Everything still works

- **WHEN** I click a navbar link, click the gear, type in the search field, or open its type-ahead dropdown
- **THEN** each behaves exactly as it did, with the dropdown anchored beneath the field as before

### Requirement: The navbar marks the current page with a border

The navbar control for the page currently being viewed SHALL carry a visible border in addition to its tinted background, so that being on a page reads at least as strongly as hovering a link does. This SHALL apply to every navbar control that leads to a page: the six left links, Profile, and the Settings gear.

The active border SHALL be visually stronger than the border a control shows on hover, so that an active control under the pointer still reads as the current page rather than as just another hovered link. Hovering the active control SHALL NOT replace or weaken its active border.

The Settings gear SHALL be marked this way when the settings page is being viewed, like every other navbar page control.

The score toggle is not a page control and SHALL NOT gain an active state.

#### Scenario: The current page's link is bordered

- **WHEN** I am on My List and look at the navbar
- **THEN** the My List link shows a border as well as its tinted background

#### Scenario: The gear is marked on the settings page

- **WHEN** I am on the settings page
- **THEN** the Settings gear shows the same active treatment the other navbar page controls show for their pages

#### Scenario: Active outranks hover

- **WHEN** I hover a navbar link for a page I am not on, and then hover the link for the page I am on
- **THEN** the active link's border is visibly stronger than the hovered one's, so which link is the current page is still clear

#### Scenario: Hovering the active control keeps it marked

- **WHEN** I move the pointer over the navbar control for the page I am already on
- **THEN** it shows the hover state while keeping its stronger active border

#### Scenario: The score toggle gains nothing

- **WHEN** the navbar renders on any page
- **THEN** the hide/unhide score toggle shows no active-page treatment

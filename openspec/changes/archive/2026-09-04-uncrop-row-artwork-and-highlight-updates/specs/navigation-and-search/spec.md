## MODIFIED Requirements

### Requirement: The navbar marks the current page with a border

The navbar control for the page currently being viewed SHALL carry a visible border in addition to its tinted background, so that being on a page reads at least as strongly as hovering a link does. This SHALL apply to every navbar control that leads to a page: the seven left links, Profile, and the Settings gear.

The active border SHALL be visually stronger than the border a control shows on hover, so that an active control under the pointer still reads as the current page rather than as just another hovered link. Hovering the active control SHALL NOT replace or weaken its active border.

The Settings gear SHALL be marked this way when the settings page is being viewed, like every other navbar page control.

The updates control opens a panel rather than a page, and SHALL be marked with this same treatment while what it opened is on screen, on the terms the `anime-updates` capability sets out. It is therefore not an exception to the rule that a navbar control shows where you are; it answers to its panel rather than to the route.

The score toggle is not a page control, opens nothing, and SHALL remain the one navbar control that is never marked.

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

#### Scenario: The updates control is marked by its panel

- **WHEN** the updates dropdown is open
- **THEN** the updates control carries the same marking a navbar page control carries for its page, without any page having changed

#### Scenario: The score toggle gains nothing

- **WHEN** the navbar renders on any page
- **THEN** the hide/unhide score toggle shows no active-page treatment, whatever else in the navbar is marked

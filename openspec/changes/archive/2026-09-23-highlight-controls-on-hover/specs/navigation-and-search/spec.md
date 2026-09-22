## ADDED Requirements

### Requirement: Every interactive control highlights on hover
The system SHALL give every enabled control it renders a visible hover state, on every page and inside every overlay. This covers buttons, links drawn as buttons, dropdowns, text and search fields, checkbox chips, and each option of a segmented switch. A control that does not visibly change when the pointer is over it SHALL be treated as a defect, whatever page it is on.

The same kind of control SHALL highlight the same way wherever it appears, so a control's hover tells me what kind of control it is and not which page I'm on:

- **An outlined control** is a control with a neutral fill and its own border: a button, a dropdown, a text field or a checkbox chip. On hover its border SHALL take the accent colour and its label SHALL take the heading colour. Its size and position SHALL stay the same.
- **A control already drawn as on** is one whose resting look already carries a tint and a coloured border: a filter that is narrowing the list, or a pressed toggle such as **Multi-entry only** or a status filter. Pressing it does something, so it SHALL still visibly respond under the pointer. Its outline SHALL thicken in its own colour (the accent colour, or the status colour it is drawn in) without changing its size. It SHALL NOT look identical hovered and at rest.
- **A segmented switch** SHALL take the accent border around the whole switch while the pointer is over it. The option under the pointer SHALL be picked out with a neutral tint, different from the accent tint that marks the chosen option, so hovering one option never makes it look chosen.
- **A filled action button**, one drawn in a solid colour as a call to action, SHALL darken its fill on hover. Its label SHALL stay legible in both themes.
- **The chosen option of a set where exactly one is chosen**, such as a tab row, a segmented switch or the current page number, MAY keep its selected look unchanged under the pointer, because choosing it again does nothing. It SHALL still read as chosen while hovered. This exemption does not cover a pressed toggle, since pressing a toggle again turns it off.

Navigation steppers and pagination keep the tinted highlight "Clearly visible hover state on navigation controls" already gives them, and the navbar search field keeps its own highlight from "The search field highlights on hover". Both are consistent within their own kind.

The dropdown rule SHALL be a rule of the app rather than of each component. It SHALL be written once, where it applies to every dropdown the app renders, so a dropdown added later carries it without anything being remembered about it. This is the same way the pointer cursor is expressed.

A hover state SHALL NOT change a control's size, position, or the layout around it.

#### Scenario: A dropdown on the season page
- **WHEN** I move the pointer over the Season page's season, year, or sort dropdown
- **THEN** its border takes the accent colour and its label the heading colour, the same as the Type filter beside it

#### Scenario: The same control on another page
- **WHEN** I hover the sort dropdown on the Series browser, the Home page's "Followed shows airing" sort, and My list's sort dropdown in turn
- **THEN** all three highlight in the same way

#### Scenario: The recap's period and type dropdowns
- **WHEN** I hover the Recap page's year or season dropdown, or its **All types** dropdown
- **THEN** each one highlights as an outlined control

#### Scenario: A checkbox chip
- **WHEN** I hover the **In my list** chip on the Season or Year page
- **THEN** its border takes the accent colour and its label the heading colour

#### Scenario: My list's find field
- **WHEN** I hover My list's find field
- **THEN** its border takes the accent colour, whether or not it holds text

#### Scenario: An active filter still responds
- **WHEN** My list's score filter is narrowing the list and I hover it
- **THEN** its outline thickens in the accent colour, so it visibly responds even though it was already accent-tinted

#### Scenario: A pressed toggle still responds
- **WHEN** **Multi-entry only** is pressed on the profile's "Top series" box or on the Series browser, and I hover it
- **THEN** its outline thickens, and it still reads as pressed

#### Scenario: A pressed status filter responds in its own colour
- **WHEN** a status filter on My list or the Series browser is pressed and I hover it
- **THEN** its outline thickens in that status's colour rather than switching to the accent colour

#### Scenario: The chosen tab
- **WHEN** I hover the media-type tab that is already chosen in "Most rewatched"
- **THEN** it still reads as the chosen tab

#### Scenario: The grouping switch
- **WHEN** **By status** is chosen and I hover **Single list**
- **THEN** the switch takes the accent border, **Single list** is picked out with a neutral tint, and **By status** still reads as the chosen option

#### Scenario: A filled action button
- **WHEN** I hover **Reset filters & sort**
- **THEN** its accent fill darkens and its label stays legible

#### Scenario: A Settings page action
- **WHEN** I hover **Resync now**, **Run full reconciliation**, or any other action button on the Settings page
- **THEN** it highlights in the same way as the accept and decline buttons on the same page

#### Scenario: A control inside an overlay
- **WHEN** I hover a Cancel or Close button in the rank editor, the completion-score overlay, or the related-anime overlay
- **THEN** it highlights as an outlined control

#### Scenario: Hover moves nothing
- **WHEN** any control is highlighted on hover
- **THEN** neither the control nor anything around it changes size or position

### Requirement: A disabled control shows no hover highlight
A control that is disabled SHALL NOT show any hover treatment. Moving the pointer over it SHALL leave its colour, background, border and size exactly as they are at rest, so it never offers itself as clickable when it isn't. This SHALL hold for every control the app can disable: the Airing page's week buttons, pagination arrows, period and season steppers, and the action buttons that disable themselves while a request is in flight.

A disabled control SHALL remain distinguishable from an enabled one without being hovered.

#### Scenario: The current week on the Airing page
- **WHEN** the Airing page is showing the current week and I hover its current-week button
- **THEN** the button stays dimmed and shows no highlight

#### Scenario: The first page of Top anime
- **WHEN** the Top anime page is on its first page and I hover the previous-page arrow
- **THEN** the arrow stays dimmed and shows no highlight

#### Scenario: The last page of Top anime
- **WHEN** the Top anime page is on its last page and I hover the next-page arrow
- **THEN** the arrow stays dimmed and shows no highlight

#### Scenario: A control that re-enables
- **WHEN** I step the Airing page back one week and hover the current-week button
- **THEN** it highlights like any other enabled control

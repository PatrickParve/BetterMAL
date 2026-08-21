## ADDED Requirements

### Requirement: Banded section titles
A section title drawn in a family SHALL be presented as a **band**: a filled block spanning the full width of the section's content — the list, table, or block the title heads — with the title's text centred on it.

The band SHALL be filled with the family's two colours as a gradient running across its width, so a family is identified by the fill rather than by the colour of the letters. The label SHALL be drawn in the family's own contrasting ink rather than in the page's heading colour, and SHALL be legible against the fill beneath it in both the light and the dark theme. A family whose fill is light in the current theme SHALL take a dark label there and a family whose fill is dark SHALL take a light one — the achromatic year family makes this unavoidable, filling toward black in the light theme and toward white in the dark one.

The band SHALL be the same width as the content it heads, starting where that content starts and ending where it ends, so it reads as a header for the block rather than as a chip floating above it. Every banded title on a page SHALL take the same height, padding, corner treatment, and text alignment as every other, so two bands differ only in colour and wording.

A band SHALL carry its label as ordinary selectable text at the size and weight a section title has, SHALL NOT clip or ellipsize its label, and SHALL wrap to a second line rather than overflow when the section is too narrow for the label on one line.

Banding SHALL be a property of what the section is about, not of what is happening to it: a band's fill SHALL NOT change on hover or focus of the title or of the section it heads.

Where the platform renders in a forced-colours mode, the band SHALL fall back to a plain bordered header whose label is drawn in the platform's own text colour, rather than to invisible text or an unreadable fill.

A band's colour SHALL never be the only thing identifying its section: every banded title SHALL keep the words it has today, so a reader who cannot distinguish the families loses nothing.

Section titles that carry no family SHALL NOT be banded — they SHALL keep the plain heading treatment they have today, so a band means "this section has a subject a family names".

#### Scenario: The band spans the section
- **WHEN** a banded title heads a list and its label is much shorter than that list is wide
- **THEN** the band still runs the full width of the list, starting at the list's left edge and ending at its right, with the label centred on it

#### Scenario: The fill carries the family
- **WHEN** I look at a banded title
- **THEN** the band is filled with its family's colours and the label sits on that fill in a contrasting ink, rather than the label itself being painted in the family's colours on the page background

#### Scenario: A filled label stays readable in both themes
- **WHEN** I view a year-family band in the light theme and again in the dark theme
- **THEN** its label is legible against the fill in both, taking a light label over the light theme's dark fill and a dark label over the dark theme's light fill

#### Scenario: Bands are uniform apart from colour
- **WHEN** two sections carrying different families sit side by side
- **THEN** their bands are the same height and shape with the label placed the same way in each, differing only in fill colour and wording

#### Scenario: A long title is not clipped
- **WHEN** a banded section is narrow enough that its title does not fit on one line
- **THEN** the label wraps onto a second line inside a taller band rather than being cut off or spilling outside it

#### Scenario: A band does not respond to the pointer
- **WHEN** I move the pointer over a banded title or over the section it heads
- **THEN** the band's fill and label colours do not change

#### Scenario: A band is never invisible
- **WHEN** the page is rendered in a forced-colours mode
- **THEN** the band renders as a plain bordered header with a legible label rather than disappearing

#### Scenario: The words still say it
- **WHEN** I read a banded title without being able to tell its family's colours apart
- **THEN** the title's text still names the section exactly as it did unbanded

#### Scenario: Untinted titles are not banded
- **WHEN** a section whose subject no family names renders its title
- **THEN** it is drawn as a plain heading with no band behind it

### Requirement: A family colours the rows of its section
Where a section carries a colour family, the rows inside that section SHALL take **that family's colour** in place of the app's default accent wherever a row would otherwise show the accent — specifically in the standard hover treatment (tinted background, coloured border, and elevation) applied on pointer hover and on keyboard focus alike.

Only the row's colours SHALL change. The treatment SHALL be the same one every followable row in the app uses — the same tint strength, the same border, the same elevation — so a hovered row in a family section and a hovered row anywhere else differ in hue and nothing else. A row's size, position, spacing, and resting appearance SHALL be exactly what they are without a family, and highlighting SHALL NOT reflow the list.

A section that carries no family SHALL keep the accent highlight it has today.

Where a row is itself about one of the two score roles — as a my-score-vs-MAL comparison row is — that row MAY take its own score role's colour instead of its section's family, so the row's highlight agrees with the role its own content names.

#### Scenario: A row highlights in its section's colour
- **WHEN** I move the pointer over a row of a section whose title band carries the season family
- **THEN** the row takes the standard hover highlight drawn in that family's colour rather than in the app's purple accent

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach one of those rows by keyboard rather than by pointer
- **THEN** it takes the same family-coloured highlight it takes on hover

#### Scenario: Only the colour differs
- **WHEN** I compare a hovered row in a family section against a hovered row in a section with no family
- **THEN** the two are the same size and shape with the same tint strength, border weight, and elevation, differing only in hue

#### Scenario: Highlighting does not reflow the list
- **WHEN** I move the pointer down a list of rows in a family section
- **THEN** no row changes size or position as it gains or loses the highlight

#### Scenario: A section with no family is unchanged
- **WHEN** I hover a row of a section whose title carries no family
- **THEN** it takes the app's accent highlight exactly as it does today

## REMOVED Requirements

### Requirement: Tinted section titles
**Reason**: Replaced by "Banded section titles". Painting the family's gradient through the title's own letters made a family read as a slightly off-colour heading rather than as a label for the section beneath it — the colour stopped where the words stopped, and the section it named carried no mark of its own.

**Migration**: Every title that was tinted becomes banded instead, with the same words, in the same family, in the same place. No section gains or loses a family, and no section's content changes.

## MODIFIED Requirements

### Requirement: A family's title and its control share its colours
Where a control selects the same subject a banded title describes, the control SHALL carry that subject's family. The recap's **Yearly** recap-type tab SHALL carry the year family, matching the year-level ranking titles a yearly period produces, and its **Season** tab SHALL carry the season family, matching the season-level ranking titles — so the tab pressed and the sections it produces are visibly the same colour.

Carrying a family SHALL change a control's colours only. Every other property of the control — its size, its shape, its states, and how those states differ from one another — SHALL be exactly what it is without a family.

Where a state of the control fills it with the family's colours and prints a label on top, that label SHALL stay legible against the fill beneath it. A family whose colours are light in the current theme SHALL take a dark label there rather than the light label a dark-filled family takes, so the label's colour follows the fill it sits on rather than being fixed for every family alike. The achromatic year family makes this unavoidable: it fills toward black in the light theme and toward white in the dark one, so a single fixed label colour would be unreadable in one theme or the other.

A selected control carrying a family and a banded title carrying the same family SHALL be filled from the same colours in the same direction, so the two read as the same colour rather than as two shades of one hue.

#### Scenario: The tab matches the sections it produces
- **WHEN** I select the recap's **Season** tab and look at the season-level ranking titles below it
- **THEN** the tab and those titles carry the same family's colours

#### Scenario: A family changes colour and nothing else
- **WHEN** I compare a control carrying a family against one that does not
- **THEN** the two are the same size and shape and differ only in colour

#### Scenario: A filled label stays readable in both themes
- **WHEN** the **Yearly** tab is selected in the light theme and again in the dark theme
- **THEN** its label is legible against the family's fill in both, taking a light label over the light theme's dark fill and a dark label over the dark theme's light fill

#### Scenario: A selected tab and its band match
- **WHEN** the **Season** tab is selected and a season-family band is visible on the same page
- **THEN** the two are filled from the same colours, so the tab reads as the same colour as the band rather than a lighter or darker variant of it

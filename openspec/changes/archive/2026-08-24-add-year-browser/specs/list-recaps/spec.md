## ADDED Requirements

### Requirement: Yearly recap links to its year page
A yearly recap SHALL offer a control that opens the year browser on that same year, so the anime that aired that year — not only the ones in my list — are one step away. The control SHALL NOT be offered on a season or multi-year recap, whose period is not a single year.

The control SHALL be the year-level counterpart of the season recap's season-page control and SHALL match it in every respect but subject: the same button presentation, the same height as the period controls it sits beside, the same hover and keyboard-focus treatment, and the same standing as a real link — openable in a new tab or window by the means the browser normally offers for links — despite being presented as a button.

The control SHALL take the **year** colour family on hover and on keyboard focus, matching the treatment the recap's own Yearly tab takes when hovered, rather than the app's generic accent. Its resting state SHALL stay neutral, matching the period controls beside it, so the colour appears in response to the pointer or focus rather than being worn all the time.

At most one period-browsing control SHALL be offered at a time: a season recap offers the season one, a yearly recap offers the year one, and a multi-year recap offers neither.

#### Scenario: Opening the year page from a recap
- **WHEN** a 2019 yearly recap is shown and I follow its year-page control
- **THEN** the year browser opens on 2019

#### Scenario: The control reads as a button
- **WHEN** a yearly recap is shown
- **THEN** its year-page control is presented as a button matching the height of the controls beside it, and highlights on hover and on keyboard focus as they do

#### Scenario: The control is still a link
- **WHEN** I open the year-page control in a new tab the way I would any link
- **THEN** the year browser opens in a new tab on that year

#### Scenario: Hovering wears the year colour
- **WHEN** I hover the year-page control
- **THEN** it highlights in the year family's colour, the same one the Yearly tab highlights in when hovered, rather than in the app's generic accent

#### Scenario: Focus wears the year colour
- **WHEN** I reach the year-page control by keyboard
- **THEN** its focus ring is drawn in the year family's colour, as the Yearly tab's is

#### Scenario: The resting control is neutral
- **WHEN** a yearly recap is shown and I am not pointing at or focused on the year-page control
- **THEN** it is drawn neutrally, matching the period controls beside it

#### Scenario: Not offered outside yearly mode
- **WHEN** a season or multi-year recap is shown
- **THEN** no year-page control is offered

#### Scenario: Switching between season and yearly modes swaps the control
- **WHEN** I switch a recap from season mode to yearly mode
- **THEN** the season-page control is replaced by the year-page control, rather than both appearing

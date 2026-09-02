## ADDED Requirements

### Requirement: The recap's control row groups its period-scoped controls
The row of controls beneath the recap's title SHALL be laid out in two groups: the period selection — the stepper arrows, the period label, and the period dropdowns — leads the row, and every control that acts on the *selected* period sits together at the row's trailing edge.

On a yearly recap the trailing group SHALL therefore hold the **What I watched** / **What aired** time filter immediately beside the **Browse the year** control, rather than the filter sitting alone in the middle of the row. On a season recap, whose only trailing control is **Browse the season**, and on a multi-year recap, whose only trailing control is the time filter, the single control SHALL sit at the trailing edge in the same place the group occupies, so the row reads the same way in all three modes.

Grouping SHALL change nothing about the controls themselves: each keeps its own presentation, its states, its height, and its behaviour, and the row SHALL still wrap onto further lines rather than overflow when the window is too narrow to hold both groups.

#### Scenario: The yearly recap's controls travel together
- **WHEN** a yearly recap is shown
- **THEN** the What I watched / What aired filter sits beside the Browse the year control at the trailing edge of the control row, with the period stepper leading the row

#### Scenario: A season recap keeps its shape
- **WHEN** a season recap is shown
- **THEN** the period stepper leads the row and Browse the season sits at its trailing edge

#### Scenario: A multi-year recap keeps its shape
- **WHEN** a multi-year recap is shown
- **THEN** the period stepper leads the row and the time filter sits at its trailing edge, with no period-browsing control offered

#### Scenario: Grouping changes nothing else
- **WHEN** I use the time filter on a yearly recap after this change
- **THEN** it behaves exactly as before, with the same states and the same height as the control beside it

#### Scenario: A narrow window wraps rather than overflows
- **WHEN** the window is too narrow to hold both groups on one line
- **THEN** the row wraps and no control is clipped or pushed outside the page

### Requirement: A top 6-10 row's score is inset from the row's edge
The score at the trailing edge of each top 6-10 row SHALL be inset from the row's border, so a visible gap separates the score from the border rather than the score sitting against the row's padding edge. The inset SHALL be the same on every row of the list, so the scores stay in one column.

Nothing else about the row SHALL move: its rank, poster, and title keep their positions and the row keeps its height, so the list does not reflow.

#### Scenario: The score has room at the row's end
- **WHEN** a recap shows ranks six through ten
- **THEN** each row's score sits clear of the row's right border rather than against it

#### Scenario: The scores stay in a column
- **WHEN** the rows show scores of differing widths
- **THEN** each score is inset by the same amount, so they line up as a column

#### Scenario: Nothing else moves
- **WHEN** I compare a top 6-10 row before and after this change
- **THEN** its rank, poster, title, and height are unchanged

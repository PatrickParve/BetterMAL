## MODIFIED Requirements

### Requirement: Rankings grouped into a season column and a year column
Where a multi-year recap shows more than one ranking, the rankings SHALL be laid out in two columns grouped by the level they rank: the year ranking and the years-by-time-watched ranking in one column, the season ranking and the seasons-by-time-watched ranking in the other, with the **year column leading** — placed first, on the left, and the season column beside it. A period's year story therefore reads down the leading column and its season story down the other, rather than the score rankings occupying one row and the time rankings another.

Within a column, the score ranking SHALL come before the time-watched ranking. A column whose rankings are both absent SHALL NOT be rendered as an empty column; where only one column has any ranking to show, that column SHALL occupy the layout on its own.

The two columns' rankings SHALL align row for row across the layout: the year-level and season-level score rankings SHALL begin on the same line, and so SHALL the two time-watched rankings, whatever differences in height the rankings above them have. In particular, a ranking that shows every one of its rows offers no "See all" control while the ranking beside it may offer one — and that difference SHALL NOT lift the ranking below it out of line with its counterpart. This alignment SHALL be achieved without introducing a "See all" control for a ranking that is already showing every row.

A yearly recap has no year-level rankings, so this two-column grouping does not apply to it. Where a yearly recap shows both the season ranking and the seasons-by-time-watched ranking, the two SHALL instead be laid out side by side in separate columns — the season ranking leading — rather than stacked one above the other, so a period's best season and most-watched season are directly comparable. Where only one of the two has rows, it SHALL occupy the layout on its own.

On a display too narrow for two columns, the rankings SHALL stack in a single column: for a multi-year recap, year rankings before season rankings; for a yearly recap, the season ranking before seasons-by-time-watched.

#### Scenario: A multi-year recap's four rankings
- **WHEN** a multi-year recap under **What aired** shows a season ranking, a year ranking, and both time-watched rankings
- **THEN** the year ranking and years-by-time-watched sit in the leading (left) column, and the season ranking and seasons-by-time-watched in the column beside it

#### Scenario: Order within a column
- **WHEN** a column shows both a score ranking and a time-watched ranking
- **THEN** the score ranking is shown above the time-watched one

#### Scenario: A five-year recap's time rankings stay in line
- **WHEN** a 2020–2024 recap shows a year ranking of exactly five years, which offers no "See all" control, beside a season ranking of twenty seasons, whose overflow offers one
- **THEN** years-by-time-watched and seasons-by-time-watched still begin on the same line

#### Scenario: No redundant overflow control
- **WHEN** a ranking is already showing every one of its rows
- **THEN** no "See all" control is offered for it, whatever the ranking beside it offers

#### Scenario: Only the season column has rankings
- **WHEN** a multi-year recap has season-level rankings to show but no year-level ranking has any rows
- **THEN** the season column occupies the layout on its own, with no empty year column beside it

#### Scenario: A yearly recap has no year column
- **WHEN** a yearly recap under **What aired** shows only the season-level rankings
- **THEN** no empty year column is rendered beside them

#### Scenario: A yearly recap's two rankings sit side by side
- **WHEN** a yearly recap under **What aired** shows both a season ranking and a seasons-by-time-watched ranking
- **THEN** the two are presented in separate columns next to each other, the season ranking leading, rather than one stacked above the other

#### Scenario: Narrow display stacks the columns
- **WHEN** a multi-year recap showing both columns is displayed too narrow for two columns
- **THEN** the rankings stack in one column with the year rankings before the season rankings

#### Scenario: Narrow display stacks a yearly recap's two rankings
- **WHEN** a yearly recap showing both the season ranking and seasons-by-time-watched is displayed too narrow for two columns
- **THEN** the two stack in one column with the season ranking first

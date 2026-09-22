## ADDED Requirements

### Requirement: Recap year choices start at MyAnimeList's first season year
Every year choice the recap page offers SHALL reach back to 1917, the first year in MyAnimeList's season archive and the same floor the Season and Year pages use. This covers the multi-year start and end years, the yearly recap's year, and the season recap's year. The choices SHALL run from 1917 up to the current year, listed most recent first. They SHALL widen to include any year the page is already showing that falls outside that range, as they do today.

The recap's floor and the Season and Year pages' floor SHALL be one value, so they can't drift apart.

The period steppers follow this floor through "Period stepper arrows": the previous control SHALL be unavailable only at 1917 in a yearly recap, and at winter 1917 in a season recap.

#### Scenario: Choosing an early year
- **WHEN** I open the yearly recap's year dropdown
- **THEN** it lists every year from the current year down to 1917

#### Scenario: A season recap in the archive's first year
- **WHEN** I choose winter 1917 in the season recap
- **THEN** the recap is shown for winter 1917, and the previous control is unavailable

#### Scenario: Stepping below 1960
- **WHEN** a 1960 yearly recap is shown and I use the previous control
- **THEN** the recap moves to 1959

#### Scenario: A multi-year range from the archive's start
- **WHEN** I choose a multi-year recap
- **THEN** both its start-year and end-year dropdowns reach back to 1917

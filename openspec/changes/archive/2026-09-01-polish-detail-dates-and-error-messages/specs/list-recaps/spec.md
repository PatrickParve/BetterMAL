## ADDED Requirements

### Requirement: The recap's "See all" overlay behaves as the profile's does
The overlay a recap ranking's "See all" control opens SHALL be the same overlay the profile page's favourites rankings open, and SHALL therefore be sized and dismissed by the rule the `profile-stats` capability defines for it: eight whole rows on open with no part of a ninth visible, its height derived from row height rather than from the viewport, the rest reached by scrolling, and no close control of its own — neither a Close button beneath the list nor a ✕ in its corner — leaving Esc and a click outside.

The overlay's own content SHALL be unchanged: which rows it lists, their order, their posters, the family it carries from the ranking that opened it, and where following a row leads SHALL be exactly as they are today. Rankings that show every one of their rows SHALL still offer no "See all" control at all.

#### Scenario: Eight whole rows from a recap ranking too
- **WHEN** I open the "See all" overlay from **Seasons by time watched** on a recap with more than eight qualifying seasons
- **THEN** eight rows are visible in full, no part of a ninth is shown, and the rest are reached by scrolling

#### Scenario: Dismissed the same way
- **WHEN** that overlay is open
- **THEN** it closes on Esc or on a click outside, and it offers no close control of its own — no Close button beneath the list and no ✕ in its corner

#### Scenario: The ranking's family is still carried
- **WHEN** I open the "See all" overlay from a season-level ranking
- **THEN** its title is banded in the season family and its rows highlight in that same family

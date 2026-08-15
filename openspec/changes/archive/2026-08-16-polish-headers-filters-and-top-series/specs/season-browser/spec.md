## ADDED Requirements

### Requirement: Season type filter
The system SHALL provide a multi-select Type filter beside the season sort control, using the same control and display labels as My List's type filter, offering only the media types actually present in the season's results. Selecting one or more types SHALL restrict the season results, and the result count used for paging, to matching types; with none selected, no type restriction applies. The filter SHALL be applied server-side, and the checkbox/selection state SHALL be part of the page's URL state, so paging and infinite scroll stay correct and the filter survives back-navigation — mirroring how the existing "In my list" filter is threaded through.

#### Scenario: Filtering a season to one type
- **WHEN** I select Movie in the season page's type filter
- **THEN** only movie entries are shown for that season, and the result count used for paging reflects only movies

#### Scenario: Filtering across paginated loads
- **WHEN** I select a type and scroll far enough to load additional pages
- **THEN** every loaded page continues to show only matching types, since the filter is applied server-side rather than only to already-loaded pages

#### Scenario: Only present types are offered
- **WHEN** a season contains no music videos
- **THEN** the type filter does not offer Music as an option for that season

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the season page's type filter
- **THEN** entries of every type are shown

#### Scenario: Filter persists through back-navigation
- **WHEN** I select a type, open an anime, then press the browser Back button
- **THEN** the same type selection is still applied and the filtered results are shown

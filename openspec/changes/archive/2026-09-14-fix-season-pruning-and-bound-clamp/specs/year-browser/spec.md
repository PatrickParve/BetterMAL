## MODIFIED Requirements

### Requirement: Year selection
The system SHALL allow changing the selected year, and SHALL keep the selected year, sort, and filter state in the page's URL so it persists through back-navigation from an anime detail page. Opening the Year page from the navbar with no year specified SHALL default to the current year.

The system SHALL provide both previous/next arrows that step one year at a time and a dropdown that jumps directly to a year, so a year is reachable in one action rather than by repeated stepping.

Forward navigation SHALL stop at the navigable year ceiling defined by "The year ceiling follows the season horizon": at the ceiling the next-year control SHALL be disabled, and the dropdown SHALL NOT offer any year later than it. Backward navigation SHALL be unaffected, and SHALL be floored at the same earliest year the season browser's quick-jump offers. The API itself refuses a year outside the range the season browser defines (see the `season-browser` requirement "Requests outside the seasons MAL could list are refused"), so what these controls prevent reaching is also enforced server-side.

A year addressed directly in the URL SHALL be rendered as asked rather than silently rewritten, so an old link or a bookmark is never redirected; the dropdown SHALL keep that year among its options so the control always has a valid value, and forward movement from there SHALL remain blocked. A URL-addressed year past the navigable ceiling but inside the API's range SHALL render like any other year. A URL-addressed year outside that range SHALL still not be rewritten. The API refuses it, so no request reaches MAL, and the page SHALL show the state it shows for a year that could not be loaded.

#### Scenario: Changing year with the arrows
- **WHEN** I press the next-year arrow
- **THEN** the page shows the following year's anime and the selection is reflected in the URL

#### Scenario: Jumping to a year
- **WHEN** I choose a year from the dropdown
- **THEN** the page jumps directly to that year without stepping through the intervening years

#### Scenario: Selection persists through back-navigation
- **WHEN** I pick a year, open an anime, then press the browser Back button
- **THEN** I return to the year I had selected, not the current year

#### Scenario: Navbar defaults to the current year
- **WHEN** I open the Year page from the navbar with no year in the URL
- **THEN** it defaults to the current year

#### Scenario: Stepping forward stops at the ceiling
- **WHEN** I am viewing the furthest navigable year
- **THEN** the next-year control is disabled and I cannot step past it, while the previous-year control still works

#### Scenario: The dropdown does not offer years past the ceiling
- **WHEN** I open the year dropdown
- **THEN** it offers nothing later than the navigable year ceiling

#### Scenario: A URL past the ceiling still renders
- **WHEN** I open a bookmarked link to a year beyond the navigable ceiling that is still inside the range the API accepts
- **THEN** that year is shown as asked, reporting that MyAnimeList has not listed it, its own value is selectable in the dropdown, and forward navigation from it remains blocked

#### Scenario: A URL outside the accepted range is not rewritten
- **WHEN** I open a link to a year the API refuses, such as 9999
- **THEN** the URL is left as it is, the year stays selectable in the dropdown, no request reaches MAL, and the page shows its could-not-be-loaded state

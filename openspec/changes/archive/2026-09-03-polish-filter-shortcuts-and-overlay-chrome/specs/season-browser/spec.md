## MODIFIED Requirements

### Requirement: Season type filter
The system SHALL provide a multi-select Type filter beside the season sort control, using the same control and display labels as My List's type filter, offering only the media types actually present in the season. Selecting one or more types SHALL restrict the season results, and the count the page reports, to matching types.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime passes the type filter and the page SHALL report that no anime match the current filters, using the same message it shows when other filters exclude everything rather than reporting that the season has no listing.

The selection state SHALL be part of the page's URL state so it survives back-navigation, and **All**, **None**, and a partial selection SHALL each be distinctly representable there: a URL that names no type filter at all SHALL mean **All**, so links made before this distinction existed continue to mean what they meant.

The filter SHALL be applied to the season's already-loaded listing rather than through a read, so it takes effect immediately with no loading state. The types it offers SHALL be derived from the whole listing, not from what the current selection leaves visible, so selecting one type SHALL NOT remove the others from the picker. Toggling the filter SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a season to one type
- **WHEN** I select Movie in the season page's type filter
- **THEN** only movie entries are shown for that season, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the season
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a season contains no music videos
- **THEN** the type filter does not offer Music as an option for that season

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the season's other types, so I can change or clear my selection

#### Scenario: The filter is instant
- **WHEN** I change the type selection on a season that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: All applies no type restriction
- **WHEN** the season page's type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the season page's type filter
- **THEN** no cards are shown and the page reports that no anime match the current filters, rather than that the season is not listed

#### Scenario: A link with no type filter means All
- **WHEN** I open a season page from a link whose address names no type filter
- **THEN** the type filter is on All and anime of every type are shown

#### Scenario: Filter persists through back-navigation
- **WHEN** I select a type, open an anime, then press the browser Back button
- **THEN** the same type selection is still applied and the filtered results are shown

#### Scenario: None persists through back-navigation
- **WHEN** I press **None**, navigate away, and press the browser Back button
- **THEN** the filter is still on **None** rather than back on All

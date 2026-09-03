## MODIFIED Requirements

### Requirement: Year type and in-my-list filters
The system SHALL provide, beside the Year page's sort control, the same two filters the season browser provides: a multi-select Type filter using the same control and display labels, offering only the media types actually present in the year; and an "In my list" checkbox, checked by default, which when unchecked excludes every anime that has an entry in my list.

The Type filter SHALL distinguish **All** from **None** exactly as the season browser's does: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime passes the type filter and the page SHALL report that no anime match the current filters rather than that the year has no listing. **All**, **None**, and a partial selection SHALL each be distinctly representable in the page's URL state, with a URL naming no type filter meaning **All**.

Both filters SHALL be applied to the year's already-loaded listing rather than through a read, so each takes effect immediately with no loading state, and each SHALL exclude exactly the anime it excludes today, with the count the page reports following the filtered set. The types offered SHALL be derived from the whole listing rather than from what the current selection leaves visible. Both SHALL be part of the page's URL state so they survive back-navigation, and toggling either SHALL count as a fresh view of the page, returning the grid to the top on its first screenful.

#### Scenario: Filtering a year to one type
- **WHEN** I select Movie in the Year page's type filter
- **THEN** only movie entries are shown for that year, and the count the page reports reflects only movies

#### Scenario: The filter holds all the way down
- **WHEN** I select a type and scroll to the end of the year
- **THEN** every card shown is of a matching type

#### Scenario: Only present types are offered
- **WHEN** a year's results contain no music videos
- **THEN** the type filter does not offer Music as an option for that year

#### Scenario: Selecting a type does not shrink the type options
- **WHEN** I select one type and the results narrow to it
- **THEN** the type filter still offers the year's other types, so I can change or clear my selection

#### Scenario: Both filters are instant
- **WHEN** I change the type selection or toggle "In my list" on a year that is already loaded
- **THEN** the grid updates immediately with no read and no loading state

#### Scenario: All applies no type restriction
- **WHEN** the Year page's type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the Year page's type filter
- **THEN** no cards are shown and the page reports that no anime match the current filters, rather than that the year is not listed

#### Scenario: Excluding anime in my list
- **WHEN** I uncheck "In my list"
- **THEN** every anime that has an entry in my list is removed from the results, the reported count reflects the smaller set, and scrolling continues to show only anime not yet in my list

#### Scenario: Filters persist through back-navigation
- **WHEN** I set a type filter, uncheck "In my list", open an anime, then press the browser Back button
- **THEN** both selections are still applied and the filtered results are shown

#### Scenario: None persists through back-navigation
- **WHEN** I press **None**, navigate away, and press the browser Back button
- **THEN** the filter is still on **None** rather than back on All

## MODIFIED Requirements

### Requirement: My list type filter
The system SHALL provide a multi-select type filter in the my-list filter bar. The filter SHALL offer only the media types actually present in my list — for example TV, Movie, OVA, ONA, Special, TV special, Music — plus an "Unknown" option when the list contains an entry whose type is not known, so the control never offers a choice that would return nothing.

Any combination of types SHALL be selectable. The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; with one or more types selected, only entries of those types are shown; on **None**, no entry passes the type filter and the list reports that nothing matches the current filters, offering to clear them. The control SHALL report its state in its label — All, None, the single selected type, or a count when several but not all are selected — so the active restriction is readable without opening it.

Media types SHALL be shown by display label (for example "TV special"), not by the raw value the API returns (`tv_special`), and the same labelling SHALL be used on list rows.

#### Scenario: Showing a single type
- **WHEN** I select only Movie in the type filter
- **THEN** only movie entries are shown

#### Scenario: Showing several types
- **WHEN** I select TV and ONA
- **THEN** entries of either type are shown and all others are hidden

#### Scenario: All applies no type restriction
- **WHEN** the type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None excludes every entry
- **WHEN** I press **None** in the type filter
- **THEN** no entries are shown and the page says nothing matches the current filters and offers to clear them

#### Scenario: Only present types are offered
- **WHEN** my list contains no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: Types read as labels
- **WHEN** the type filter lists its options and a row shows its type
- **THEN** each reads as a display label such as "TV special" rather than `tv_special`

### Requirement: My list airing-status filter
The system SHALL provide a multi-select airing-status filter in the my-list filter bar offering Finished airing, Currently airing, Not yet aired, and — when the list contains one — entries whose airing status is unknown. The filter SHALL be available under every status tab, including All, not only under Plan to watch.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no airing restriction applies; with one or more statuses selected, only entries of those statuses are shown; on **None**, no entry passes the airing filter and the list reports that nothing matches the current filters.

The rows' airing-status indicator SHALL be shown whenever this filter is narrowing the list — that is, whenever it is on anything other than **All** — as it is when Airing status is the primary sort key.

#### Scenario: Filtering to still-airing shows
- **WHEN** I select Currently airing while the Watching status tab is active
- **THEN** only entries I am watching whose anime is still airing are shown

#### Scenario: Available under every status tab
- **WHEN** any status tab is active, including All
- **THEN** the airing-status filter is offered

#### Scenario: All applies no airing restriction
- **WHEN** the airing-status filter is on All
- **THEN** entries of every airing status are shown

#### Scenario: None excludes every entry
- **WHEN** I press **None** in the airing-status filter
- **THEN** no entries are shown and the page says nothing matches the current filters

#### Scenario: The indicator follows the filter being used
- **WHEN** the airing-status filter is on anything other than All
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

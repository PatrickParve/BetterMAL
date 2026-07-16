## ADDED Requirements

### Requirement: Consistent list placement across my-list sort modes
The system SHALL place the my-list entry list at the same vertical offset below its status header in ranked view (sorted by MAL score, my score, or airing status) as in grouped view (alphabetical), for every status filter. Changing the sort SHALL NOT shift the list up or down relative to the header it sits under. The header-to-list spacing SHALL be defined by a single rule shared by both view modes, rather than by per-mode values that can diverge.

#### Scenario: Switching sort does not shift the list
- **WHEN** I change the my-list sort from Alphabetical to MAL score, my score, or airing status
- **THEN** the first entry row stays at the same vertical offset below its status header, and only the ordering of the rows changes

#### Scenario: Spacing is consistent under every status filter
- **WHEN** the my-list page is in ranked view under any status filter — All, Watching, Completed, Plan to watch, On hold, or Dropped
- **THEN** the gap between the header line and the first entry row matches the gap shown in grouped view

#### Scenario: Grouped view spacing is unchanged
- **WHEN** the my-list page is in grouped view
- **THEN** each status group's list sits directly below its group header at the established spacing, and consecutive status groups remain separated by the page's section spacing

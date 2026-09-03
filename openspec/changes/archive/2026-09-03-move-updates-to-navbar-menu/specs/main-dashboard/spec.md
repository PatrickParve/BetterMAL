## REMOVED Requirements

### Requirement: Updates section placement on the main page
**Reason**: The updates feed has moved off the main page into a navbar menu reachable from every page, so the main page no longer holds an Updates section to place. What the feed contains, how it is ordered and windowed, and the history it opens remain specified by the `anime-updates` capability, which now specifies the menu in place of the section.

**Migration**: None for stored data. The main page renders the currently-watching carousel directly above the "Airing today" / "Followed shows airing" row, and `GET /api/dashboard` no longer returns the `updates` field — the menu reads the same window from `GET /api/updates/recent` instead.

## MODIFIED Requirements

### Requirement: Dashboard section title dividers
The system SHALL render a thin horizontal divider rule directly beneath the title of each dashboard section on the main page — "Currently watching", "Airing today", and "Followed shows airing" — visually separating the section heading from its content. The divider SHALL span the width of the section's content area.

#### Scenario: Divider under each section title
- **WHEN** the main page renders the "Currently watching", "Airing today", and "Followed shows airing" sections
- **THEN** each section's title is underlined by a thin horizontal divider rule separating the heading from the section content

#### Scenario: Divider spans the section width
- **WHEN** a dashboard section title divider is rendered
- **THEN** the divider spans the width of that section's content area

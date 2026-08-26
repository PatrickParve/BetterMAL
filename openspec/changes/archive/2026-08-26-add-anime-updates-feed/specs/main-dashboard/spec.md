## ADDED Requirements

### Requirement: Updates section placement on the main page
The system SHALL render an "Updates" section on the main page directly **below** the "Currently watching" carousel and **above** the row holding "Airing today" and "Followed shows airing".

Unlike the "Currently watching" carousel, which is bounded to the width of five cards and centred, the Updates section SHALL span the full width of the page content, matching the combined width of the "Airing today" / "Followed shows airing" row beneath it.

The section SHALL be present whether or not it has anything to show; when it has nothing, it renders its own empty-state text rather than being omitted, so the page's section order never changes underneath the reader.

What the section contains, how it is ordered and windowed, and the history it opens are specified by the `anime-updates` capability.

#### Scenario: Position between the carousel and the airing row
- **WHEN** the main page renders
- **THEN** the Updates section appears below "Currently watching" and above the row containing "Airing today" and "Followed shows airing"

#### Scenario: Full content width
- **WHEN** the Updates section renders
- **THEN** it spans the full width of the page content rather than being bounded to a card count or centred

#### Scenario: The section holds its place when empty
- **WHEN** the Updates section has nothing to show
- **THEN** it still renders in its position, showing its empty-state text, and the sections around it do not move

## MODIFIED Requirements

### Requirement: Dashboard section title dividers
The system SHALL render a thin horizontal divider rule directly beneath the title of each dashboard section on the main page — "Currently watching", "Updates", "Airing today", and "Followed shows airing" — visually separating the section heading from its content. The divider SHALL span the width of the section's content area.

#### Scenario: Divider under each section title
- **WHEN** the main page renders the "Currently watching", "Updates", "Airing today", and "Followed shows airing" sections
- **THEN** each section's title is underlined by a thin horizontal divider rule separating the heading from the section content

#### Scenario: Divider spans the section width
- **WHEN** a dashboard section title divider is rendered
- **THEN** the divider spans the width of that section's content area

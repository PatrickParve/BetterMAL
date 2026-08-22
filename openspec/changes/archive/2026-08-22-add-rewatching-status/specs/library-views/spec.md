## ADDED Requirements

### Requirement: Rewatching carries its own status colour

The system SHALL give Rewatching its own colour in the status palette, used everywhere a watch status is colour-coded — the my-list row's status stripe, the status filter tab, and any other status-keyed treatment.

The colour SHALL be a **darker blue than Completed's**, so the two read as related — both are states of an anime the user has finished — while staying clearly distinguishable from one another and from every other status colour.

The pair SHALL remain distinguishable in both the light and the dark palette. Each is defined independently, so satisfying this in one does not satisfy it in the other.

#### Scenario: A rewatching row is identifiable

- **WHEN** my list shows a Rewatching entry
- **THEN** its status stripe carries the Rewatching colour, distinct from every other status colour on the page

#### Scenario: Related to Completed but not confusable

- **WHEN** a Rewatching row and a Completed row are shown together
- **THEN** both read as blues while remaining clearly distinguishable from one another

#### Scenario: Both palettes

- **WHEN** I view my list in the light theme and again in the dark theme
- **THEN** Rewatching and Completed are distinguishable from each other in both

## MODIFIED Requirements

### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → Rewatching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. Rewatching sits directly after Currently watching because both are runs in progress. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown.

Rows outside Plan to watch SHALL also show the airing-status indicator while the user is working with airing status — that is, while the airing-status filter has a selection or Airing status is the primary sort key — since the indicator is the value being filtered or ordered on. Outside those cases, rows in other status groups SHALL NOT show the indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, Rewatching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Rewatching sits with the in-progress groups
- **WHEN** my list holds both Rewatching and Completed entries
- **THEN** the Rewatching group appears directly after Currently watching, not beside Completed

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status shown while filtering or sorting by it
- **WHEN** the airing-status filter has a selection, or Airing status is the primary sort key
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

#### Scenario: Airing status otherwise only on Plan to watch
- **WHEN** no airing-status filter is selected and the sort is not by airing status
- **THEN** rows outside Plan to watch show the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** an entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

### Requirement: My list status filter tabs
The system SHALL provide status filter controls on the my-list page — All, Watching, Rewatching, Completed, Plan to watch, On hold, Dropped — so that selecting one shows only entries in that status. The Rewatching tab SHALL carry the Rewatching status colour, as every other tab carries its own status's colour.

#### Scenario: Filtering by status
- **WHEN** I select a status filter other than All
- **THEN** only entries in that status are shown

#### Scenario: Filtering to rewatches
- **WHEN** I select the Rewatching filter
- **THEN** only Rewatching entries are shown, and Completed entries are not among them

#### Scenario: Showing all statuses
- **WHEN** I select All
- **THEN** entries in every status are shown, grouped in the standard order

## MODIFIED Requirements

### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown. Rows in other status groups SHALL NOT show the airing-status indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status only on Plan to watch
- **WHEN** an entry is in any group other than Plan to watch
- **THEN** its row shows the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** a Plan to watch entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

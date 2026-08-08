## MODIFIED Requirements

### Requirement: Single anime detail layout
The system SHALL show a single anime page with the title and a large picture on the left, and near the top-right of the title two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box. Any info field for which no data is available SHALL display "No info" rather than being blank.

The info box's Status field SHALL, when the anime is currently airing and an aired-episode count is known, read `Currently airing: <aired>/<total> ep aired`, using `?` in place of an unknown total episode count. When the anime is currently airing but no aired-episode count is known, the field SHALL read `Currently airing` with no counts appended. Statuses other than currently airing SHALL be displayed unchanged.

The aired-episode count SHALL come from the anime's stored per-episode airing rows and SHALL NOT be estimated from its broadcast cadence or from elapsed time since its start date. An anime with no stored airing rows SHALL be treated as having no known aired count.

#### Scenario: Rendering the detail layout
- **WHEN** I open an anime's detail page
- **THEN** it shows the title with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

#### Scenario: MAL score respects the hide toggle
- **WHEN** the hide toggle is on
- **THEN** the MAL score in the rank/score box is blurred like everywhere else it appears

#### Scenario: Missing info field
- **WHEN** an info field (e.g. source or duration) has no data for that anime
- **THEN** that field shows "No info" instead of a blank value

#### Scenario: Currently airing with a known episode count
- **WHEN** I open the detail page of an anime that is currently airing, has 12 total episodes, and has stored airing rows placing 5 episodes in the past
- **THEN** the Status field reads "Currently airing: 5/12 ep aired"

#### Scenario: Currently airing with an unknown total
- **WHEN** I open the detail page of a currently airing anime whose total episode count is unpublished and that has stored airing rows placing 5 episodes in the past
- **THEN** the Status field reads "Currently airing: 5/? ep aired"

#### Scenario: Currently airing with no stored airing rows
- **WHEN** I open the detail page of a currently airing anime that has no stored per-episode airing rows
- **THEN** the Status field reads "Currently airing" with no episode counts appended, and no count is estimated from its broadcast cadence

#### Scenario: Other airing statuses unaffected
- **WHEN** I open the detail page of an anime that has finished airing or has not yet aired
- **THEN** the Status field reads "Finished airing" or "Not yet aired" respectively, with no episode counts appended

### Requirement: On-demand refresh action
The system SHALL provide an on-demand refresh action on the detail page for this anime's cached data. The action SHALL refresh both the anime's cached MyAnimeList metadata and its per-episode airing data, for that one anime only, at the moment it is requested.

A failure to refresh the airing data SHALL NOT fail the action when the metadata refresh succeeded; the action SHALL report success and the failure SHALL be logged.

#### Scenario: Refreshing this anime
- **WHEN** I trigger refresh on the detail page
- **THEN** the system performs a single-anime metadata refresh and a single-anime airing-data refresh, and updates the displayed cached data

#### Scenario: Corrected aired count appears immediately
- **WHEN** I trigger refresh on the detail page of an anime whose stored aired count was wrong and AniList now reports corrected airing data
- **THEN** the reloaded page shows the corrected aired count without waiting for a scheduled refresh

#### Scenario: Airing refresh fails
- **WHEN** I trigger refresh and the metadata refresh succeeds but the airing-data fetch fails
- **THEN** the action reports success, the metadata is updated, the previously stored airing rows are left intact, and the failure is logged

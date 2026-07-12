## MODIFIED Requirements

### Requirement: Single anime detail layout
The system SHALL show a single anime page with the title and a large picture on the left, and near the top-right of the title two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box. Any info field for which no data is available SHALL display "No info" rather than being blank.

#### Scenario: Rendering the detail layout
- **WHEN** I open an anime's detail page
- **THEN** it shows the title with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

#### Scenario: MAL score respects the hide toggle
- **WHEN** the hide toggle is on
- **THEN** the MAL score in the rank/score box is blurred like everywhere else it appears

#### Scenario: Missing info field
- **WHEN** an info field (e.g. source or duration) has no data for that anime
- **THEN** that field shows "No info" instead of a blank value

## REMOVED Requirements

### Requirement: External MAL and AniList links from id
**Reason**: The AniList link was built from the MAL id, but AniList uses its own id namespace unrelated to MAL, so the link was wrong for most anime.
**Migration**: Replaced by the MyAnimeList-only external link requirement below; no AniList link is shown.

## ADDED Requirements

### Requirement: External MyAnimeList link from id
The system SHALL provide a link to the anime's MyAnimeList page, built as a URL template from the MAL id, requiring no API call. No AniList link is shown.

#### Scenario: Building the external link
- **WHEN** the detail page renders
- **THEN** it shows a MyAnimeList link constructed from the MAL id without making an API call, and shows no AniList link

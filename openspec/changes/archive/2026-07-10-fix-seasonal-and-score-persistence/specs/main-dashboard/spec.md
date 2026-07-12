## MODIFIED Requirements

### Requirement: Current season section with filters and progress
The system SHALL show a "Current season" section containing only anime in my list that are airing this season, filterable by popularity, MAL score, alphabetical, and my score, with each card showing an episode-progress bar (watched/total, or watched/? when total is unknown). When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime rather than treated as most popular.

#### Scenario: Filtering current season
- **WHEN** I choose a sort/filter (popularity, MAL score, alphabetical, or my score)
- **THEN** the current-season cards reorder accordingly

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort the current-season section by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** those unranked anime appear last rather than at the top

#### Scenario: Progress bar with unknown total
- **WHEN** a current-season card's anime has an unknown total episode count
- **THEN** its progress bar shows `watched/?`

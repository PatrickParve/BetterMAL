## MODIFIED Requirements

### Requirement: Year sorting
The system SHALL allow sorting the year's anime by popularity, MAL score, alphabetically, and my score, using the same rules and the same labels the season browser uses, applied across the whole year at once rather than within each season.

Sorting by popularity SHALL place unranked anime — MAL popularity rank absent or zero — after all ranked anime, then ranked anime by ascending rank, then by title. Sorting by my score SHALL split the results into two ordered groups: first every anime I have scored, by my score descending — ties broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime by popularity under the same unranked-last rule. The grouping and the ordering within it SHALL be produced server-side so both hold across paginated loads, and the page SHALL render a visual break labelled `Unwatched` between the two groups, shown only when both groups have at least one anime.

#### Scenario: Sorting across the whole year
- **WHEN** I sort a year by MAL score
- **THEN** the highest-scored anime of the year leads, regardless of which of the year's seasons it aired in

#### Scenario: Sorting by my score
- **WHEN** I sort a year by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort a year by my score and several of its anime share a score
- **THEN** they appear in my ranking's order rather than in popularity order

#### Scenario: My-score grouping holds across pages
- **WHEN** I sort a year by my score and scroll far enough to load additional pages
- **THEN** no scored anime appears after the `Unwatched` divider

#### Scenario: No scored anime in the year
- **WHEN** I sort by my score in a year where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort a year by popularity and some anime have no popularity rank
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last

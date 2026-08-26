## MODIFIED Requirements

### Requirement: Season filtering
The system SHALL allow filtering/sorting season anime by popularity, score, alphabetical, and my score, where my score is meaningful only for anime also in my list. When sorting by popularity, anime that are unranked — MAL popularity rank absent or zero — SHALL be ordered after all ranked anime (a rank of `0` is not treated as "most popular"), then ranked anime by ascending rank (1 = most popular), then by title.

When sorting by my score, the results SHALL be split into two ordered groups: first every anime I have scored, ordered by my score descending — equal scores broken by my ranking, best-ranked first, per the `anime-ranking` capability, then by popularity, then by title for anime my ranking does not cover; then every remaining anime, ordered by popularity using the same unranked-last rule as the popularity sort. The grouping and the ordering within it SHALL be produced server-side so both hold across paginated loads, and the page SHALL render a visual break with the label `Unwatched` between the two groups, shown only when both groups have at least one anime.

#### Scenario: Sorting by my score
- **WHEN** I sort by my score
- **THEN** the anime I have scored appear first in descending score order, followed by an `Unwatched` divider, followed by the remaining anime in popularity order

#### Scenario: Equal scores follow my ranking
- **WHEN** I sort by my score in a season where I gave three anime the same score
- **THEN** those three appear in my ranking's order rather than in popularity order

#### Scenario: An unranked scored anime among ranked ones
- **WHEN** a scored anime the ranking does not cover shares a score with ranked anime
- **THEN** the ranked ones come first and it follows them, ordered by popularity among any other unranked anime of that score

#### Scenario: My-score grouping holds across pages
- **WHEN** I sort by my score and scroll far enough to load additional pages
- **THEN** no scored anime appears after the `Unwatched` divider — the grouping is consistent across every loaded page

#### Scenario: No scored anime in the season
- **WHEN** I sort by my score in a season where I have scored nothing
- **THEN** all anime are shown in popularity order with no `Unwatched` divider

#### Scenario: Unranked anime sort last by popularity
- **WHEN** I sort by popularity and some anime have no popularity rank (rank absent or zero)
- **THEN** the ranked anime appear first in ascending rank order and the unranked anime appear last, rather than an unranked anime sorting to the top

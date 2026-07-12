# profile-stats Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Anime stats computed from local data
The system SHALL show anime stats computed entirely from the local database: Days, Mean Score, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, and Episodes.

#### Scenario: Rendering stats
- **WHEN** the profile page loads
- **THEN** all listed stat values are computed from the local DB without a live API call

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), and episode-count increases. Consecutive episode increases for the same anime SHALL collapse into a single feed item rather than one item per increment. The feed SHALL NOT show episode-count decreases, status changes, drops, score changes, or rewatch-count changes.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions and episode increases in most-recent-first order, scrollable within its box

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Excluding decreases and drops
- **WHEN** an anime's episode count decreased, or its status changed or was dropped
- **THEN** no such item appears in the latest-updates feed

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

### Requirement: My top anime with minimum-of-ten fill and manual selection
The system SHALL show my top anime, always showing at least 10. It SHALL include all anime I scored 10; if there are 10 or more such anime it shows all of them with no cap. If there are fewer than 10, it SHALL fill the remainder up to 10 by next-highest score as the default, and SHALL provide an edit control in the box's top-right corner that opens a selection view listing the tied next-highest-scored anime so I can manually choose which occupy the remaining slots. A persisted manual selection SHALL take precedence over the default auto-fill.

#### Scenario: Ten or more perfect scores
- **WHEN** I have 10 or more anime scored 10
- **THEN** all of them are shown with no cap at 10

#### Scenario: Fewer than ten perfect scores (default fill)
- **WHEN** I have fewer than 10 anime scored 10 and have not made a manual selection
- **THEN** the list is filled up to 10 by next-highest score

#### Scenario: Manually choosing among ties
- **WHEN** I open the edit control and choose which tied next-highest-scored anime fill the remaining slots
- **THEN** those chosen anime occupy the remaining slots and the choice is persisted

#### Scenario: Manual selection takes precedence
- **WHEN** a manual top-anime selection has been saved
- **THEN** the profile page uses it instead of the default next-highest auto-fill

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value and the overall mean score

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" where my score is 3 or more points below the MAL score; and "I liked it, they didn't" where the MAL score is below 7 and my score is at least 2 points higher than the MAL score.

#### Scenario: They liked it, I didn't
- **WHEN** I have rated an anime and my score is at least 3 points below its MAL score
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I have rated an anime whose MAL score is below 7 and my score is at least 2 points higher than the MAL score
- **THEN** it appears in the "I liked it, they didn't" list


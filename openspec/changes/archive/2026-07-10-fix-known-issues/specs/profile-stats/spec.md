## MODIFIED Requirements

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

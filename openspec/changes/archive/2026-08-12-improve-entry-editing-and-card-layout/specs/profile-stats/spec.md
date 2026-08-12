## ADDED Requirements

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and rows SHALL keep the height they have without the change rather than growing to the poster's natural aspect ratio. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

#### Scenario: Poster fills a latest-updates row
- **WHEN** the Latest updates box renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a history row
- **WHEN** the full edit-history overlay renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a divergence row
- **WHEN** either opinion-divergence list renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Row heights are unchanged
- **WHEN** these rows adopt the full-height poster
- **THEN** each row occupies the same height as before, and no box grows taller

## MODIFIED Requirements

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), episode-count increases, completions, score changes, and anime removed from the list. The feed SHALL NOT show episode-count decreases, status changes other than completion, drops, or rewatch-count changes.

Consecutive episode-progress events for the same anime SHALL collapse into a single feed item rather than one item per event. A completion counts as an episode-progress event for this purpose, and SHALL supersede the progress events it collapses with: when watching an episode completed the anime, the feed SHALL report the completion and SHALL NOT also report the episode increase that produced it, nor the run of increases leading up to it. A removal is not an episode-progress event and SHALL NOT collapse with one.

A completion SHALL read as a completion whether it was reached by watching the last episode or by setting the status to completed directly.

A score change SHALL report the new score, or that the score was cleared when the score was removed.

A removal SHALL keep reading correctly after the entry it describes is gone — it names the anime from cached metadata, which outlives the entry.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions, episode increases, completions, score changes, and removals in most-recent-first order, scrollable within its box

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Completion supersedes its episode increment
- **WHEN** I watch the final episode of an anime, which marks it completed
- **THEN** the feed shows one item reading as a completion for that anime, and shows no episode-increase item for the same run of episodes

#### Scenario: Completion by status change
- **WHEN** I set an anime's status to completed without incrementing episodes
- **THEN** the feed shows a completion item for that anime

#### Scenario: Score change appears
- **WHEN** I change an anime's score
- **THEN** the feed shows an item for that anime reporting the new score

#### Scenario: Score cleared
- **WHEN** I remove an anime's score
- **THEN** the feed shows an item for that anime reporting that the score was cleared

#### Scenario: Removal appears
- **WHEN** I remove an anime from my list
- **THEN** the feed shows an item for that anime reporting that it was removed from the list, with its title and picture intact

#### Scenario: Excluding decreases and other status changes
- **WHEN** an anime's episode count decreased, or its status changed to something other than completed, or it was dropped
- **THEN** no such item appears in the latest-updates feed

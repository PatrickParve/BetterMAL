## ADDED Requirements

### Requirement: Pending entry removal record
The system SHALL persist, for every entry removed from my list whose MAL removal has not yet succeeded, a record identifying the anime and when the removal was requested. The record SHALL survive process restarts, SHALL be created in the same transaction that deletes the UserAnimeEntry, and SHALL be deleted once the removal has been pushed to MAL successfully.

Because it must outlive the entry it refers to, the record SHALL NOT depend on a UserAnimeEntry row existing. Re-adding an anime to my list while its removal is still pending SHALL discard the pending removal, so a queued delete can never erase an entry the user has since recreated.

#### Scenario: Removal is durable
- **WHEN** an entry is removed from my list and the application restarts before the MAL push succeeds
- **THEN** the pending removal record is still present and the removal is retried

#### Scenario: Record cleared on success
- **WHEN** an anime's removal is pushed to MAL successfully
- **THEN** its pending removal record is deleted

#### Scenario: Re-adding cancels a pending removal
- **WHEN** an anime is added back to my list while its removal is still pending
- **THEN** the pending removal record is discarded and no delete is pushed for it

## MODIFIED Requirements

### Requirement: ActivityLog entity
The system SHALL persist an ActivityLog of timestamped change records (status changes, episode increments, score changes, additions, completions, removals) sufficient to reconstruct a chronological feed. Episode-change records SHALL also store the previous episodes-watched value so the direction of the change (increase vs decrease) can be determined at read time.

A removal record SHALL survive the deletion of the UserAnimeEntry it describes, so the history of an anime that was removed from my list is not lost along with the entry.

#### Scenario: Recording a change
- **WHEN** a tracked field on a UserAnimeEntry changes
- **THEN** a timestamped ActivityLog row describing the change is written

#### Scenario: Recording episode-change direction
- **WHEN** an episodes-watched value changes
- **THEN** the ActivityLog row records the previous episodes-watched value alongside the new one

#### Scenario: Recording a removal
- **WHEN** an anime is removed from my list
- **THEN** a timestamped ActivityLog row recording the removal is written and is retained after the entry is deleted

## ADDED Requirements

### Requirement: Rewatching is pushed to MyAnimeList as watching

MyAnimeList's status values are `watching`, `completed`, `on_hold`, `dropped`, and `plan_to_watch`; it has no rewatching status. The system SHALL therefore push a Rewatching entry as **`watching`**, sending its episodes watched unchanged, so the rewatch reads on MyAnimeList as a run in progress and its episode figure matches the local one at every point.

Every locally defined status SHALL have a mapping to a MyAnimeList status value. A status with no mapping SHALL NOT be allowed to reach the push path, since an unmapped value would fail the sync of that entry rather than degrade.

#### Scenario: A rewatch is pushed as watching

- **WHEN** a Rewatching entry with 3 episodes watched is pushed to MyAnimeList
- **THEN** its status is sent as `watching` and its episode count as 3

#### Scenario: Rewatch count is still pushed

- **WHEN** a rewatch finishes and the entry returns to Completed with its rewatch count increased
- **THEN** the push sends `completed` together with the new rewatch count

#### Scenario: Every status maps

- **WHEN** an entry of any locally defined status is pushed
- **THEN** a MyAnimeList status value is sent for it, and no status causes the push to fail for lack of a mapping

### Requirement: Reconciliation does not demote a rewatch

Because a Rewatching entry is pushed as `watching`, MyAnimeList reports it back as `watching`. Reconciliation SHALL treat a local **Rewatching** entry as **matching** a remote `watching` status, and SHALL NOT record a difference or rewrite the entry to Watching on that basis.

This SHALL constrain the status comparison only. A remote status that is anything other than `watching` SHALL be diffed and applied normally, so a change genuinely made on MyAnimeList still reaches the local entry. Every other field — episodes watched, score, dates, rewatch count — SHALL be compared exactly as it is today.

Without this rule the first reconciliation after a rewatch is pushed would silently replace the user's Rewatching entry with Watching, destroying the distinction the status exists to hold.

#### Scenario: A rewatch survives reconciliation

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `watching`
- **THEN** no status difference is recorded and the entry stays Rewatching

#### Scenario: A real remote change still applies

- **WHEN** reconciliation compares a local Rewatching entry against a MyAnimeList entry reporting `dropped`
- **THEN** the difference is recorded and applied like any other remote status change

#### Scenario: Other fields still diff

- **WHEN** a local Rewatching entry and its MyAnimeList copy differ in episodes watched or score
- **THEN** those differences are recorded exactly as they would be for any other status

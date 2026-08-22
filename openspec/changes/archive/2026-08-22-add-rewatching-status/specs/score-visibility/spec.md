## ADDED Requirements

### Requirement: A rewatch keeps a completed entry's score visibility

The "Always show MAL scores for completed and dropped shows" setting SHALL treat **Rewatching** the same way it treats Completed, revealing the MAL score of a Rewatching entry in full whenever it would reveal a Completed one.

The reasoning that capability already gives for Completed applies at least as strongly here: a community average can no longer bias or spoil a viewing still ahead of the user, because the user has already seen the whole thing. It would also be incoherent for a score visible on a Completed entry to disappear the moment the user starts watching it again and reappear when they finish.

This SHALL hold wherever the setting applies — my-list rows, the anime detail page, series-page entry rows, and the profile page's divergence lists — and SHALL NOT change the setting's default, its independence from the global hide toggle, or its exclusion of the season and search browse cards.

#### Scenario: A rewatch keeps its revealed score

- **WHEN** the global hide toggle is on, the setting is enabled, and I set a Completed entry to Rewatching
- **THEN** its MAL score stays shown in full, with no placeholder and no reveal control

#### Scenario: Revealed everywhere the setting applies

- **WHEN** a Rewatching entry's MAL score is rendered on my list, the detail page, a series-page row, or a profile divergence list
- **THEN** it is revealed under the setting exactly as a Completed entry's would be

#### Scenario: The setting is otherwise unchanged

- **WHEN** the setting is disabled
- **THEN** a Rewatching entry's MAL score follows the global hide/unhide behaviour like any other

## ADDED Requirements

### Requirement: Raising progress resumes an entry

The system SHALL treat raising an entry's episodes watched as a statement that the anime is being watched now. When an edit raises the count **above** the stored one and the new count does **not** reach everything available, the system SHALL set the entry's status to **Watching**, unless its stored status is Watching, Rewatching, or Completed.

- Watching and Rewatching are excluded because there is nothing to resume — a rewatch in progress stays a rewatch in progress, per "A rewatch in progress behaves like watching".
- Completed is excluded because "An entry is Completed only while its progress covers everything available" already governs a Completed entry whose progress no longer covers everything, and sends it to Rewatching where that is permitted.
- Reaching everything available is unaffected: that case still completes the entry, per the existing completion rules.

The rule SHALL apply however the count is raised — the "+" control, the in-place editable count, or the entry editor — and SHALL apply when there is no known total or aired-so-far count to reach, since a raise can then reach nothing by definition.

The rule SHALL NOT fire on an edit that lowers the count or leaves it unchanged: lowering a Dropped or On hold entry's count corrects a record of something set aside, and says nothing about watching it now.

A status supplied explicitly in the same edit SHALL take precedence over this rule, **including the status the entry already holds** — so an edit that raises the count while stating "still Dropped" saves as Dropped. The entry editor SHALL make that possible and visible: while its episode-count field holds a value above the entry's stored count, and the conditions above are otherwise met, its status control SHALL show Watching, updating as the count is typed rather than only on save; whatever its status control shows when the edit is saved SHALL be what is saved.

A resumption SHALL be an ordinary status change in every other respect — written to the activity log and queued for MAL sync like any other, per "Edits log activity and trigger sync".

#### Scenario: Watching an episode of something planned

- **WHEN** I press "+" on a Plan to watch entry for a 12-episode anime at 0 episodes watched
- **THEN** its episodes watched becomes 1 and its status becomes Watching

#### Scenario: Picking a dropped show back up

- **WHEN** I set the in-place count on a Dropped entry from 5 to 7 of 24 episodes
- **THEN** its episodes watched becomes 7 and its status becomes Watching

#### Scenario: Resuming something on hold

- **WHEN** I press "+" on an On hold entry below the last available episode
- **THEN** its status becomes Watching

#### Scenario: Reaching the end still completes

- **WHEN** I press "+" on a Plan to watch entry at episode 11 of 12 available
- **THEN** its status becomes Completed rather than Watching

#### Scenario: An unknown total still resumes

- **WHEN** I raise the count on a Dropped entry whose anime has no known total or aired-so-far count
- **THEN** its status becomes Watching

#### Scenario: Lowering a count changes nothing

- **WHEN** I lower a Dropped entry's episodes watched from 5 to 3
- **THEN** its status stays Dropped

#### Scenario: A rewatch is not resumed

- **WHEN** I press "+" on a Rewatching entry below the last available episode
- **THEN** its status stays Rewatching

#### Scenario: A completed entry follows the Completed rule instead

- **WHEN** an edit leaves a Completed entry's progress short of everything available
- **THEN** it lands in Rewatching or Watching per "An entry is Completed only while its progress covers everything available", not by this rule

#### Scenario: The editor shows the resumption before saving

- **WHEN** I open the editor on a Dropped entry and type an episode count above the one it holds, short of everything available
- **THEN** the editor's status control changes to Watching while I am still editing

#### Scenario: Keeping the original status from the editor

- **WHEN** I raise the count in the editor, set its status control back to Dropped, and save
- **THEN** the entry saves with the raised count and its status stays Dropped

#### Scenario: Choosing a different status from the editor

- **WHEN** I raise the count in the editor and set its status control to On hold before saving
- **THEN** the entry saves as On hold with the raised count

#### Scenario: A resumption is logged and synced

- **WHEN** an entry is resumed to Watching by a raised count
- **THEN** an activity entry records the status change and the entry is queued for MAL sync, exactly as a status change made by hand would be

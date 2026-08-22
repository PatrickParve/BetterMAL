## ADDED Requirements

### Requirement: My list rows offer no progress or score control before the first episode

A my-list row whose anime has aired no episode (as resolved by the `list-editing` capability's "Whether an anime has aired an episode is resolved one way") SHALL show neither a progress cell nor a score control: no bar, no `watched/total` count, no "+" control, and no inline score dropdown. Those cells SHALL be left empty rather than showing a zeroed or disabled control.

The rest of the row SHALL be unchanged — its status-colour stripe, rank, poster, title, type and airing badge, MAL score, and Edit button all render exactly as they do for any other row, so the row keeps the list's column alignment and its full height.

The Edit button SHALL remain available, since Watching, Plan to watch, and the clearing of an existing score or rewatch count are still permitted from the editor.

Both controls SHALL reappear once the anime has aired an episode, with no action needed from the user beyond the list being read again.

#### Scenario: A row for an unaired anime

- **WHEN** my list shows an entry whose anime has aired no episode
- **THEN** its progress and score cells are empty, and its stripe, poster, title, MAL score, and Edit button render as usual

#### Scenario: The row's controls return with the first episode

- **WHEN** that anime airs its first episode and my list is read again
- **THEN** the row shows its progress bar, count, "+" control, and score dropdown again

#### Scenario: Columns stay aligned

- **WHEN** my list mixes rows for aired and unaired anime
- **THEN** every row keeps the same column positions and the same height

#### Scenario: Editing is still reachable

- **WHEN** I click the Edit button on a row for an anime that has aired no episode
- **THEN** the editor overlay opens as it does for any other row, with the limits the `list-editing` capability places on it

## MODIFIED Requirements

### Requirement: My list rows edit the watched count in place

The system SHALL make the `watched` count in each my-list row's progress cell directly editable in place, per the "Inline editable episode count" requirement, so an entry's episode number can be set without opening the edit overlay. The row's edit button SHALL remain available for status, score, and rewatch-count changes. Saving an in-place count edit SHALL update that row's count and bar without reloading the page or re-sorting the list.

This applies to every row that has a progress cell. A row whose anime has aired no episode has none — see "My list rows offer no progress or score control before the first episode" — and so offers no in-place count field either.

#### Scenario: Setting a row's count in place

- **WHEN** I click the count in a my-list row, type a number, and confirm
- **THEN** that row's episodes-watched is saved and its count and bar update in place, with no overlay opening

#### Scenario: Edit button still opens the overlay

- **WHEN** I click a row's edit button
- **THEN** the editor overlay opens as before, unaffected by the in-place count field

#### Scenario: Row stays in position after an in-place edit

- **WHEN** I save an in-place count edit on a row partway down the list
- **THEN** the list is not reloaded or reordered underneath me and the row keeps its position

#### Scenario: No count field where there is no progress cell

- **WHEN** a my-list row's anime has aired no episode
- **THEN** the row shows no editable count, because it shows no progress cell at all

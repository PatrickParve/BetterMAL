## MODIFIED Requirements

### Requirement: My list status filter tabs
The system SHALL provide status filter controls on the my-list page — All, Watching, Rewatching, Completed, Plan to watch, On hold, Dropped — so that the list shows only entries in the selected statuses. The Rewatching tab SHALL carry the Rewatching status colour, as every other tab carries its own status's colour.

The status controls SHALL be **multi-select**: any combination of Watching, Rewatching, Completed, Plan to watch, On hold, and Dropped MAY be selected at once, and the list SHALL show the union of the selected statuses. Selecting a status while others are selected SHALL add it to the selection rather than replacing it, and selecting an already-selected status SHALL remove it.

**All SHALL be exclusive.** Selecting All SHALL clear every other selection, and selecting any individual status SHALL clear All. All SHALL read as selected exactly when no individual status is selected, and in that state the list SHALL show entries in every status.

Deselecting the last remaining individual status SHALL return the controls to All rather than leaving nothing selected, so the page can never be filtered to an empty list by deselection alone.

Because several controls can be selected at once, the controls SHALL be presented and announced as independent toggles — each reporting its own pressed state — rather than as a single-selection tab list, so assistive technology is not told that exactly one is selected.

When the grouped view is in use, the visible groups SHALL follow the app's standard status group order rather than the order the statuses were selected in.

The selection SHALL be restored with the page on back/forward navigation, as the page's other view controls are, and SHALL open on All on a fresh visit — except where a deep link seeds the page with a specific status, which SHALL select that status alone.

#### Scenario: Filtering by one status
- **WHEN** I select a status filter other than All
- **THEN** only entries in that status are shown

#### Scenario: Filtering to rewatches
- **WHEN** I select the Rewatching filter
- **THEN** only Rewatching entries are shown, and Completed entries are not among them

#### Scenario: Selecting several statuses at once
- **WHEN** I select Watching, then Rewatching, then On hold
- **THEN** all three controls read as selected and the list shows entries in any of those three statuses

#### Scenario: Selected groups follow the standard order
- **WHEN** I select Dropped and then Watching, with the grouped view in use
- **THEN** the Watching group appears above the Dropped group, following the app's standard status order rather than my click order

#### Scenario: Deselecting one of several
- **WHEN** Watching, Rewatching, and On hold are selected and I select Rewatching again
- **THEN** Rewatching is deselected and the list shows Watching and On hold entries only

#### Scenario: Showing all statuses
- **WHEN** I select All
- **THEN** entries in every status are shown, grouped in the standard order

#### Scenario: All clears the other selections
- **WHEN** Watching and Completed are selected and I select All
- **THEN** Watching and Completed are both deselected, All reads as selected, and every entry is shown

#### Scenario: Selecting a status clears All
- **WHEN** All is selected and I select Completed
- **THEN** All is deselected, Completed alone is selected, and only Completed entries are shown

#### Scenario: Deselecting the last status returns to All
- **WHEN** Completed is the only selected status and I select it again
- **THEN** All reads as selected and every entry is shown, rather than the list going empty

#### Scenario: A deep link still selects one status
- **WHEN** I follow a link that opens my list focused on a single status
- **THEN** that status alone is selected, and I can add further statuses to it from there

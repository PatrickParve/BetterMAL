## MODIFIED Requirements

### Requirement: Progress bar and overlay status editor
The system SHALL show, below the picture, a progress bar (`watched/total`, or `watched/?` when the total is unknown) with the current status next to it. The `watched` count SHALL be directly editable in place, per the "Inline editable episode count" requirement, so a specific episode number can be set without opening the overlay. The edit button — the second of the three action buttons stacked beneath the progress bar, shown only once the anime is in my list — SHALL open an overlay on top of the page for updating episodes watched, rewatch count, and score, applying the list-editing business rules. The editor SHALL NOT include start/finish date fields, since those are set automatically by the app's date logic.

While the anime has aired no episode, the progress bar SHALL NOT be shown at all — no track, no `watched/total` count, and no increment control — per the `list-editing` capability's "The editable progress row appears only once an episode has aired". The status text that sits beside the bar SHALL still be shown in that case, since it is the page's only statement of the entry's status and is not an edit control, and the action buttons beneath SHALL still be shown. The bar SHALL appear as specified above once the anime has aired an episode.

#### Scenario: Opening the editor
- **WHEN** I click the edit button beneath the progress bar
- **THEN** an overlay opens with fields for episodes watched, rewatch count, and score, and no start/finish date fields

#### Scenario: Saving an edit
- **WHEN** I change episodes watched, rewatch count, or score in the overlay
- **THEN** the change is saved with the standard start/complete-date, activity-log, and debounced-sync behavior

#### Scenario: Editing the count in place
- **WHEN** I click the `watched` count next to the detail page's progress bar, type a number, and confirm
- **THEN** episodes-watched is saved to that number and the bar and count update in place, without the overlay opening

#### Scenario: In-place edit unavailable without an entry
- **WHEN** the anime is not in my list, so no entry exists yet
- **THEN** the count is not editable in place, and the add buttons rather than an edit button are what put it in my list

#### Scenario: No bar before the first episode
- **WHEN** I open the detail page for an anime that has aired no episode
- **THEN** no progress bar, count, or increment control is shown, while the status text beside it and the action buttons beneath it are shown as usual

#### Scenario: The bar appears with the first episode
- **WHEN** that anime airs its first episode and I open its detail page again
- **THEN** the progress bar, count, and increment control are shown as they are for any other anime

### Requirement: Broadcast progress on the detail page progress bar
While the anime is currently airing, the detail page's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar, so how much of the show exists yet is visible alongside how much of it I have seen. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render exactly as it does today, with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When neither count is known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total` (or `watched/?` when the total is unknown) and SHALL NOT restate the aired count, which the info box's Status field already names. The `watched` count SHALL remain editable in place and the increment button SHALL remain, exactly as specified by the "Progress bar and overlay status editor" requirement — the aired fill is an addition to that bar, not a replacement for it.

When my episodes watched changes on this page — by increment, by in-place edit, or from the entry editor — my fill SHALL update in place without a reload, and the aired fill SHALL be unaffected.

This requirement describes fills within a bar that is being drawn. Where the "Progress bar and overlay status editor" requirement withholds the bar entirely — an anime that has aired no episode — there is no track for either fill, and this requirement has nothing to add.

#### Scenario: Airing anime shows broadcast progress
- **WHEN** I open the detail page of a currently airing anime with 12 total episodes, 5 aired, and 3 watched
- **THEN** a blue fill spans 5/12 of the track, a purple fill spans 3/12 on top of it, and the label reads `3/12`

#### Scenario: Caught up with the broadcast
- **WHEN** I open the detail page of a currently airing anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither extends past the aired portion of the track

#### Scenario: Not started
- **WHEN** I open the detail page of a currently airing anime with 5 of 12 episodes aired that I have not started
- **THEN** only the blue aired fill is drawn and no purple fill appears

#### Scenario: Finished airing keeps the plain bar
- **WHEN** I open the detail page of an anime that has finished airing
- **THEN** no aired fill is drawn and the bar shows only my watched progress against the total, as it does today

#### Scenario: Not yet aired keeps the plain bar
- **WHEN** I open the detail page of an anime that has not yet aired
- **THEN** no aired fill is drawn — there is no bar at all, per "Progress bar and overlay status editor"

#### Scenario: Airing with an unknown total
- **WHEN** I open the detail page of a currently airing anime with an unpublished total episode count and 10 episodes aired, of which I have watched 5
- **THEN** the blue fill spans half the track, the purple fill covers half of that blue extent, and the label reads `5/?`

#### Scenario: Airing with no aired count
- **WHEN** I open the detail page of a currently airing anime with no determinable aired-episode count
- **THEN** no blue fill is drawn, and my watched fill and label render as they do today

#### Scenario: Incrementing updates my fill only
- **WHEN** I increment an episode from the detail page of a currently airing anime
- **THEN** the purple fill grows in place without a reload and the blue aired fill is unchanged

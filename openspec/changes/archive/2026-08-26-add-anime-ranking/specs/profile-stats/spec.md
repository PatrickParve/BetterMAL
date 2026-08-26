## MODIFIED Requirements

### Requirement: My top anime with minimum-of-ten fill and manual selection
The system SHALL show my top anime, always showing at least 10 when I have scored at least 10 anime. The list SHALL be ordered by score descending and grouped into score tiers: a lower-scored anime SHALL NEVER appear above a higher-scored one. It SHALL include all anime I scored 10; if there are 10 or more such anime it shows all of them with no cap. If there are fewer than 10, it SHALL fill the remainder up to 10 from the next-highest score tiers in descending score order; the tier that does not fully fit SHALL be truncated so the list stops at 10.

The order within a single score tier SHALL be the order the `anime-ranking` capability gives that score — my hand-ordered anime first in the order I set (never-placed members following them alphabetically), then short-form entries, then dropped anime. The section SHALL NOT compute an order of its own, so an anime I place here is placed everywhere the ranking is read, and an anime I drop falls to the bottom of its tier here as it does everywhere else. Membership of the truncated tier SHALL be expressed by that same tier order: the tier members that fit occupy the remaining slots, in tier order.

The system SHALL provide a **Rank** control in the box's top-right corner whenever any tier has more than one member. The control SHALL open an editor listing the hand-orderable members of every tier of the current list in full — including members of the truncated tier that do not fit — with a cut line marking how many of that tier's listed members are included. Short-form and dropped members SHALL NOT be listed, since the ranking places them by band and title and no editing here could move them; when a tier's cut falls among those unlisted members, every listed member of that tier is included and no cut line SHALL be drawn for it. Reordering members within a tier SHALL change their order in the list; moving a member across the cut line SHALL change which members of that tier are included. Every reorder SHALL persist into the one ranking on its own, with no separate save step, and the persisted order SHALL take precedence over the alphabetical default.

The editor SHALL additionally offer an action that leaves this top-list arrangement and opens the whole-library ranking editor described by the `anime-ranking` capability, so every scored anime can be arranged and not only those in contention for the top list.

Each tier in the editor SHALL have a promote boundary equal to the smaller of that tier's included-member count and 10. A row positioned before that boundary SHALL offer single-step move-up and move-down controls, disabled at the ends of its tier. A row positioned at or after that boundary SHALL offer a single promote control instead of the two single-step controls; activating it SHALL move that row to the last position before the boundary and push every row from that position onward down by one, leaving the rest of the tier order intact. The promote boundary SHALL NOT move when a row is promoted, so in a truncated tier promoting an excluded member takes the last included slot and drops the displaced member below the cut line. Each of these controls SHALL render its glyph centered within the control.

Reordering by dragging a row SHALL work across the whole tier in one drag: while a row is being dragged and held within an edge zone at the top or bottom of the editor's scrollable area, that area SHALL scroll in that direction continuously until the pointer leaves the edge zone or the drag ends, and the reorder SHALL apply to the row under the pointer when the drag is released. Dragging SHALL apply only within one tier; a row released over a different tier SHALL leave every tier's order unchanged.

While a drag is in progress the editor SHALL show a floating copy of the dragged row that follows the pointer, SHALL dim that row in its original place, and SHALL mark where the row would land. The editor SHALL NOT reorder the tier until the drag is released, so the rows under the pointer do not move mid-drag. A drag SHALL be cancellable, leaving every tier's order unchanged; while a drag is in progress the cancel key SHALL cancel the drag and SHALL NOT close the editor or discard any pending edits. Dragging SHALL NOT select the text of any row the pointer passes over.

#### Scenario: Ten or more perfect scores
- **WHEN** I have 10 or more anime scored 10
- **THEN** all of them are shown with no cap at 10

#### Scenario: Fewer than ten perfect scores (default fill)
- **WHEN** I have fewer than 10 anime scored 10 and have never ordered the lower tiers
- **THEN** the list is filled up to 10 from the next-highest score tiers, each tier in alphabetical order

#### Scenario: The edit control is named Rank
- **WHEN** I look at the "My top anime" box with more than one member in some tier
- **THEN** its top-right control reads **Rank**

#### Scenario: Reordering a tier that exactly fills the list
- **WHEN** I have exactly 10 anime scored 10 and I reorder them in the editor
- **THEN** the top list shows those same 10 anime in my chosen order, and the order is still there after a reload

#### Scenario: Score still dominates order
- **WHEN** I reorder anime across a list containing both 10s and 9s
- **THEN** every anime scored 10 still appears above every anime scored 9, and my ordering only applies within each of those tiers

#### Scenario: Dropped members sink within their tier
- **WHEN** a tier of the top list holds anime I have completed and one I dropped
- **THEN** the dropped one appears beneath every completed member of that tier, whatever its title

#### Scenario: Pinned members are not in the editor
- **WHEN** a tier shown in the editor holds a dropped anime and a Music entry
- **THEN** neither is listed among that tier's rows, while both still occupy their places in the top list itself

#### Scenario: The cut falls among unlisted members
- **WHEN** a truncated tier's included members cover every one of its hand-orderable members and stop part-way through its dropped ones
- **THEN** that tier's rows are all shown as included and no cut line is drawn for it

#### Scenario: Swapping a member of a truncated tier
- **WHEN** a tier has more members than the remaining slots and I move one of its excluded members above the cut line
- **THEN** that anime takes a slot in the top list, the member it displaced drops below the cut line and out of the list, and the change is persisted

#### Scenario: Newly scored anime joins an ordered tier
- **WHEN** I score a new anime into a tier whose order I have already set
- **THEN** it appears last among that tier's hand-ordered members

#### Scenario: Opening the whole-library ranking editor
- **WHEN** I use the whole-library action in the top-anime editor
- **THEN** the ranking editor opens over every anime I have scored, not only those in contention for the top list

#### Scenario: Promoting a row from deep in a long tier
- **WHEN** a tier has 40 members all included in the list and I activate the promote control on its 37th row
- **THEN** that row becomes the 10th row of the tier, the rows that were 10th through 36th each move down one place, and the rows after the 37th keep their positions

#### Scenario: Promote replaces the single-step arrows outside the top ten
- **WHEN** I open the editor on a tier whose promote boundary is 10
- **THEN** rows 1 through 10 show move-up and move-down controls and rows 11 onward show only the promote control

#### Scenario: Promoting an excluded member of a truncated tier
- **WHEN** a truncated tier includes 4 of its members and I activate the promote control on its 9th row
- **THEN** that row becomes the 4th row of the tier and is now above the cut line, the member that was 4th becomes 5th and falls below the cut line, and saving persists both

#### Scenario: Promote is repeatable up the tier
- **WHEN** I promote a row into the last top-ten slot and then use the move-up control on it
- **THEN** it moves one place at a time from there, so the promote control and the arrows compose without any intermediate save

#### Scenario: Dragging to the edge scrolls the editor
- **WHEN** I drag a row to the bottom edge of the editor's scroll area and hold it there
- **THEN** the list keeps scrolling down while I hold, and releasing over a row drops the dragged row at that row's position without any grab-and-drop cycling

#### Scenario: Auto-scroll stops with the drag
- **WHEN** I move the dragged row out of the edge zone, or release it, or cancel the drag
- **THEN** the automatic scrolling stops immediately

#### Scenario: Floating preview follows the pointer
- **WHEN** I press on a row and move the pointer
- **THEN** a floating copy of that row follows the pointer, the row stays dimmed in its original place, and the tier's rows do not move until I release

#### Scenario: Landing position is shown before release
- **WHEN** I hold a dragged row over another row of the same tier
- **THEN** the editor marks the position the dragged row would take, on the far side of the hovered row relative to where the drag started

#### Scenario: Cancelling a drag keeps the editor open
- **WHEN** I press the cancel key while dragging a row
- **THEN** the drag ends with no reorder, the editor stays open, and the reordering I did before the drag is still pending

#### Scenario: Drag across tiers is rejected
- **WHEN** I drag a row from one score tier and release it over a row in a different tier
- **THEN** no tier's order changes

#### Scenario: Pressing a row control does not start a drag
- **WHEN** I press the move-up, move-down, or promote control on a row
- **THEN** that control acts on click and no drag begins

#### Scenario: Dragging does not select text
- **WHEN** I press on a row and drag the pointer down across several other rows before releasing
- **THEN** none of the rows the pointer passed over end up with their title text or controls selected

#### Scenario: Row controls are centered
- **WHEN** I look at a move-up, move-down, or promote control on any row
- **THEN** the arrow or promote glyph sits centered within that control, not offset to one side

### Requirement: Top-anime ordering is shared across filters
The system SHALL draw every media-type filter view from the one persisted ranking, so ordering an anime once affects the unfiltered list, every filtered list containing it, and every other place in the app that orders by my score. Reordering within a filtered view SHALL preserve the relative positions of the same tier's hand-ordered members that the filter hides.

#### Scenario: Order set under a filter shows in the unfiltered list
- **WHEN** I reorder two anime of the same score while the Movie filter is active
- **THEN** switching back to All shows those two anime in that same relative order

#### Scenario: Hidden members keep their positions
- **WHEN** I reorder a tier while a media-type filter hides some of that tier's members
- **THEN** the hidden members keep their positions relative to the visible members that surrounded them

#### Scenario: Order set here shows outside the profile
- **WHEN** I reorder two anime of the same score in the "My top anime" editor
- **THEN** my list sorted by my score, the recap top 10, and the score board all show them in that same relative order

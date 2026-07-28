## MODIFIED Requirements

### Requirement: My top anime with minimum-of-ten fill and manual selection
The system SHALL show my top anime, always showing at least 10 when I have scored at least 10 anime. The list SHALL be ordered by score descending and grouped into score tiers: a lower-scored anime SHALL NEVER appear above a higher-scored one. It SHALL include all anime I scored 10; if there are 10 or more such anime it shows all of them with no cap. If there are fewer than 10, it SHALL fill the remainder up to 10 from the next-highest score tiers in descending score order; the tier that does not fully fit SHALL be truncated so the list stops at 10.

Within a single score tier the order SHALL be mine to control and SHALL be persisted. The default order for tier members I have never ordered SHALL be alphabetical by title, and those members SHALL appear after any members I have explicitly ordered. Membership of the truncated tier SHALL be expressed by that same tier order: the tier members that fit occupy the remaining slots, in tier order.

The system SHALL provide an edit control in the box's top-right corner whenever any tier has more than one member. The control SHALL open an editor listing every tier of the current list in full — including members of the truncated tier that do not fit — with a cut line marking how many of that tier's members are included. Reordering members within a tier SHALL change their order in the list; moving a member across the cut line SHALL change which members of that tier are included. Saving SHALL persist both, and the persisted order SHALL take precedence over the alphabetical default.

Each tier in the editor SHALL have a promote boundary equal to the smaller of that tier's included-member count and 10. A row positioned before that boundary SHALL offer single-step move-up and move-down controls, disabled at the ends of its tier. A row positioned at or after that boundary SHALL offer a single promote control instead of the two single-step controls; activating it SHALL move that row to the last position before the boundary and push every row from that position onward down by one, leaving the rest of the tier order intact. The promote boundary SHALL NOT move when a row is promoted, so in a truncated tier promoting an excluded member takes the last included slot and drops the displaced member below the cut line. Each of these controls SHALL render its glyph centered within the control.

Reordering by dragging a row SHALL work across the whole tier in one drag: while a row is being dragged and held within an edge zone at the top or bottom of the editor's scrollable area, that area SHALL scroll in that direction continuously until the pointer leaves the edge zone or the drag ends, and the reorder SHALL apply to the row under the pointer when the drag is released. Dragging SHALL apply only within one tier; a row released over a different tier SHALL leave every tier's order unchanged.

While a drag is in progress the editor SHALL show a floating copy of the dragged row that follows the pointer, SHALL dim that row in its original place, and SHALL mark where the row would land. The editor SHALL NOT reorder the tier until the drag is released, so the rows under the pointer do not move mid-drag. A drag SHALL be cancellable, leaving every tier's order unchanged; while a drag is in progress the cancel key SHALL cancel the drag and SHALL NOT close the editor or discard any pending edits. Dragging SHALL NOT select the text of any row the pointer passes over.

#### Scenario: Ten or more perfect scores
- **WHEN** I have 10 or more anime scored 10
- **THEN** all of them are shown with no cap at 10

#### Scenario: Fewer than ten perfect scores (default fill)
- **WHEN** I have fewer than 10 anime scored 10 and have never ordered the lower tiers
- **THEN** the list is filled up to 10 from the next-highest score tiers, each tier in alphabetical order

#### Scenario: Reordering a tier that exactly fills the list
- **WHEN** I have exactly 10 anime scored 10 and I reorder them in the editor
- **THEN** the top list shows those same 10 anime in my chosen order, and the order is still there after a reload

#### Scenario: Score still dominates order
- **WHEN** I reorder anime across a list containing both 10s and 9s
- **THEN** every anime scored 10 still appears above every anime scored 9, and my ordering only applies within each of those tiers

#### Scenario: Swapping a member of a truncated tier
- **WHEN** a tier has more members than the remaining slots and I move one of its excluded members above the cut line
- **THEN** that anime takes a slot in the top list, the member it displaced drops below the cut line and out of the list, and the change is persisted

#### Scenario: Newly scored anime joins an ordered tier
- **WHEN** I score a new anime into a tier whose order I have already set
- **THEN** it appears after the members I explicitly ordered, alphabetically among any other unordered members of that tier

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

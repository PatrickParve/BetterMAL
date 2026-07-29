# profile-stats Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Anime stats computed from local data
The system SHALL show anime stats computed entirely from the local database: Days, Mean Score, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, and Episodes.

#### Scenario: Rendering stats
- **WHEN** the profile page loads
- **THEN** all listed stat values are computed from the local DB without a live API call

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), and episode-count increases. Consecutive episode increases for the same anime SHALL collapse into a single feed item rather than one item per increment. The feed SHALL NOT show episode-count decreases, status changes, drops, score changes, or rewatch-count changes.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions and episode increases in most-recent-first order, scrollable within its box

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Excluding decreases and drops
- **WHEN** an anime's episode count decreased, or its status changed or was dropped
- **THEN** no such item appears in the latest-updates feed

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

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

### Requirement: My top anime media-type filter
The system SHALL provide a media-type filter on the "My top anime" box with the options All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL recompute the top list from only my scored entries of that media type, applying exactly the same rules as the unfiltered list (score tiers descending, all 10s uncapped, fill to a minimum of 10, my persisted tier order, truncated-tier membership by tier order). When a filtered subset has fewer than 10 scored entries the list SHALL show all of them without padding from other media types. The selected filter SHALL be a view control only and SHALL NOT persist across visits to the page; the page SHALL open on All.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "My top anime" filter
- **THEN** the list shows only my scored movies, ranked by the same score-tier and ordering rules as the unfiltered list

#### Scenario: Filtered subset smaller than ten
- **WHEN** I select a media type for which I have scored fewer than 10 anime
- **THEN** the list shows only those anime and does not pad with anime of other media types

#### Scenario: Filter resets on revisit
- **WHEN** I select a media type, navigate away, and return to the profile page
- **THEN** the filter is back on All

### Requirement: Top-anime ordering is shared across filters
The system SHALL keep one persisted top-anime ordering that every media-type filter view draws from, so ordering an anime once affects both the unfiltered list and any filtered list containing it. Reordering within a filtered view SHALL preserve the relative positions of the same tier's members that the filter hides.

#### Scenario: Order set under a filter shows in the unfiltered list
- **WHEN** I reorder two anime of the same score while the Movie filter is active
- **THEN** switching back to All shows those two anime in that same relative order

#### Scenario: Hidden members keep their positions
- **WHEN** I reorder a tier while a media-type filter hides some of that tier's members
- **THEN** the hidden members keep their positions relative to the visible members that surrounded them

### Requirement: Most rewatched section
The system SHALL show a "Most rewatched" section on the profile page, positioned directly below the "My top anime" box and rendered in the same style as it: a horizontal strip of poster tiles, each tile linking to that anime's detail page and carrying a badge in the same position as the top-anime strip's score badge.

The section SHALL contain every list entry whose rewatch count is greater than zero, regardless of watch status, and SHALL exclude every entry with a rewatch count of zero. The badge on each tile SHALL show that entry's rewatch count.

The strip SHALL be ordered by rewatch count descending. Ties SHALL be broken deterministically: by my score descending (entries with no score last), then alphabetically by title. The strip SHALL NOT be capped at any size — every qualifying entry SHALL be reachable by scrolling the strip to its end.

The section SHALL be read-only: it SHALL NOT offer any reordering control, and its order SHALL be derived entirely from rewatch counts rather than from the persisted top-anime ordering.

#### Scenario: Ordering by rewatch count
- **WHEN** the profile page loads and I have rewatched several anime different numbers of times
- **THEN** the "Most rewatched" strip lists them from most rewatched to least rewatched

#### Scenario: Entries never rewatched are excluded
- **WHEN** an anime on my list has a rewatch count of zero
- **THEN** it does not appear in the "Most rewatched" strip

#### Scenario: Rewatched but not completed
- **WHEN** an anime has a rewatch count above zero and a status other than completed
- **THEN** it still appears in the "Most rewatched" strip

#### Scenario: Breaking a tie
- **WHEN** two anime have the same rewatch count
- **THEN** the higher-scored one appears first, and if neither is scored higher they appear in alphabetical order by title

#### Scenario: Uncapped list scrolls to the end
- **WHEN** I have more rewatched anime than fit across the width of the box
- **THEN** the strip scrolls horizontally and reaches the last of them, with none dropped from the list

#### Scenario: No reorder control
- **WHEN** I look at the "Most rewatched" box
- **THEN** it offers no control for editing the order

#### Scenario: Opening an anime from the strip
- **WHEN** I click a tile in the "Most rewatched" strip
- **THEN** that anime's detail page opens

### Requirement: Most rewatched media-type filter
The system SHALL provide a media-type filter on the "Most rewatched" box with the same options as the "My top anime" filter: All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL rebuild the strip from only my rewatched entries of that media type, applying the same membership and ordering rules. The Specials option SHALL match both MAL media types the UI treats as specials.

The filter SHALL be a view control only: it SHALL NOT persist across visits to the page, and the section SHALL open on All. The two boxes' filters SHALL be independent, so changing the media type on one box SHALL NOT change the other box's selection or contents.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the strip shows only my rewatched movies, still ordered most rewatched first

#### Scenario: Filters are independent
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the "My top anime" box keeps its own selected filter and contents

#### Scenario: Filter resets on revisit
- **WHEN** I select a media type, navigate away, and return to the profile page
- **THEN** the "Most rewatched" filter is back on All

### Requirement: Most rewatched empty states
The system SHALL show a message in place of the strip whenever the current filter scope has no rewatched anime, worded for that scope: "No shows have been rewatched" under All, "No TV shows have been rewatched" under TV, "No movies have been rewatched" under Movie, "No OVAs have been rewatched" under OVA, "No ONAs have been rewatched" under ONA, and "No specials have been rewatched" under Specials.

#### Scenario: Nothing rewatched at all
- **WHEN** no anime on my list has a rewatch count above zero and the filter is on All
- **THEN** the box shows "No shows have been rewatched"

#### Scenario: Nothing rewatched in one media type
- **WHEN** I select Movie and none of my rewatched anime are movies
- **THEN** the box shows "No movies have been rewatched"

#### Scenario: Empty scope while other scopes have entries
- **WHEN** I have rewatched TV shows but no OVAs and I select OVA
- **THEN** the box shows "No OVAs have been rewatched" rather than falling back to the unfiltered list

### Requirement: Poster-tile hover in My top anime and Most rewatched
The system SHALL give each poster tile in the "My top anime" and "Most rewatched" strips a hover/focus state distinct from the border-and-background language used elsewhere in the app: the tile SHALL scale up slightly in place, with no border, ring, or background change drawn over or around the poster. The strip SHALL reserve enough space that a scaled-up edge tile is not clipped by the strip's scroll boundary, and the hovered tile SHALL render above its neighbours rather than being overlapped as it grows into the gap beside them.

#### Scenario: Hovering a poster tile
- **WHEN** I move the pointer over a tile in the "My top anime" or "Most rewatched" strip
- **THEN** that tile scales up slightly in place and no border, ring, or background appears

#### Scenario: Scaled edge tile is not clipped
- **WHEN** the strip is scrolled fully to one end and I hover the tile at that end
- **THEN** the scaled-up tile is fully visible, with no part of it cut off by the strip's edge

#### Scenario: Hovered tile is not overlapped by its neighbours
- **WHEN** a tile scales up and grows into the gap beside an adjacent tile
- **THEN** the hovered tile renders above its neighbours rather than being partly covered by them

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count. Each bar's length SHALL represent that score's share of all my rated anime — the count for that score divided by the total number of anime I have rated — not a size relative to whichever score has the highest count.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value and the overall mean score

#### Scenario: Bar length is a share of all rated anime
- **WHEN** one score accounts for 30% of all the anime I've rated
- **THEN** that score's bar fills 30% of the track width, regardless of how the counts for other scores compare to it

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" where my score is 3 or more points below the MAL score; and "I liked it, they didn't" where the MAL score is below 7 and my score is at least 2 points higher than the MAL score.

#### Scenario: They liked it, I didn't
- **WHEN** I have rated an anime and my score is at least 3 points below its MAL score
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I have rated an anime whose MAL score is below 7 and my score is at least 2 points higher than the MAL score
- **THEN** it appears in the "I liked it, they didn't" list


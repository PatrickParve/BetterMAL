# profile-stats Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Anime stats computed from local data
The system SHALL show anime stats computed entirely from the local database: Days, Mean Score, Watching, Completed, On-Hold, Dropped, Plan to Watch, Total Entries, Rewatched, and Episodes.

#### Scenario: Rendering stats
- **WHEN** the profile page loads
- **THEN** all listed stat values are computed from the local DB without a live API call

### Requirement: Profile list-row posters fill the row
The system SHALL render the poster in every profile list row — "Latest updates" rows, full edit-history rows, and both opinion-divergence lists' rows — flush with the row's top, bottom, and leading edges, filling the row's full height with no padding between the poster and those edges. The poster's leading corners SHALL follow the row's own corner radius, and a row SHALL take its height from its list rather than from the poster's natural aspect ratio — the poster SHALL never inflate a row to its own intrinsic size. A row whose anime has no picture SHALL render its placeholder at the same full-height size.

A poster SHALL keep the proportions of the poster art at whatever height its row has: where a list sets its own row height, the poster's width SHALL follow that height rather than staying at a width fixed for some other row height.

#### Scenario: Poster fills a latest-updates row
- **WHEN** the Latest updates box renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a history row
- **WHEN** the full edit-history overlay renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster fills a divergence row
- **WHEN** either opinion-divergence list renders a row
- **THEN** that row's poster touches the row's top, bottom, and leading edges with no gap

#### Scenario: Poster follows its row's height
- **WHEN** the Latest updates rows are taller than the divergence and history rows
- **THEN** their posters are correspondingly wider, keeping the poster proportions rather than rendering a narrow crop

#### Scenario: A poster never sets the row height
- **WHEN** any of these rows renders its poster
- **THEN** the row occupies the height its list gives it, and no box grows to accommodate the image's intrinsic size

### Requirement: Latest updates activity feed
The system SHALL show a "Latest updates" feed built from the ActivityLog, most recent first, scrollable within its box, containing only: anime added to the list (any status), episode-count increases, completions, score changes, rewatch-count changes, and anime removed from the list. The feed SHALL NOT show episode-count decreases, status changes other than completion, drops, or start/finish date changes.

Each row SHALL describe its change as a single phrase and SHALL NOT pair a change-type label with a detail that restates it. The phrasing SHALL be:

- an addition: the anime was added to the list, naming the status it was added with
- an episode increase: the episode number reached, or the range when several collapse
- a completion: that the anime was completed, followed by the score when one was set as part of finishing it
- a score change: the new score, or that the score was cleared
- a rewatch-count change: the new rewatch count
- a removal: that the anime was removed from the list

Consecutive episode-progress events for the same anime SHALL collapse into a single feed item rather than one item per event. A completion counts as an episode-progress event for this purpose, and SHALL supersede the progress events it collapses with: when watching an episode completed the anime, the feed SHALL report the completion and SHALL NOT also report the episode increase that produced it, nor the run of increases leading up to it. A removal is not an episode-progress event and SHALL NOT collapse with one.

A completion SHALL read as a completion whether it was reached by watching the last episode or by setting the status to completed directly.

A score set as part of finishing an anime SHALL be reported on the completion's own row rather than as a second row. This SHALL hold both when the score and the completion were saved together and when the score was saved separately moments later, as it is when the completion score prompt is answered.

The feed SHALL show at most one row per anime per field group, reporting that anime's newest value for that group; older rows for the same anime and group SHALL be dropped from the feed. The field groups are: progress (episode increases and completions), score, rewatch count, and list membership (additions and removals). Rows for different anime, or for different field groups of the same anime, SHALL NOT collapse into each other. The full edit history SHALL remain unaffected by this collapsing and SHALL still record every step.

A removal SHALL keep reading correctly after the entry it describes is gone — it names the anime from cached metadata, which outlives the entry.

#### Scenario: Showing recent activity
- **WHEN** the profile page loads
- **THEN** the latest-updates feed lists only additions, episode increases, completions, score changes, rewatch-count changes, and removals in most-recent-first order, scrollable within its box

#### Scenario: A row reads as one phrase
- **WHEN** any row of the latest-updates feed renders
- **THEN** its change is described once, with no label-and-detail pair that repeats the same wording

#### Scenario: Addition names the status it was added with
- **WHEN** I add an anime to my list as Watching
- **THEN** the feed shows one row for it reading that it was added to the list as Watching

#### Scenario: Collapsing consecutive increments
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** those increases appear as a single feed item, not one per increment

#### Scenario: Completion supersedes its episode increment
- **WHEN** I watch the final episode of an anime, which marks it completed
- **THEN** the feed shows one item reading as a completion for that anime, and shows no episode-increase item for the same run of episodes

#### Scenario: Completion by status change
- **WHEN** I set an anime's status to completed without incrementing episodes
- **THEN** the feed shows a completion item for that anime

#### Scenario: Completion and score in one row
- **WHEN** I finish an anime and give it a score in the same save
- **THEN** the feed shows a single row for that anime reporting the completion together with the score, and no separate score row for it

#### Scenario: Score from the completion prompt joins the completion row
- **WHEN** I finish an anime by watching its last episode and then save a score in the completion score prompt that follows
- **THEN** the feed still shows a single row reporting the completion together with that score

#### Scenario: Score change appears
- **WHEN** I change an anime's score outside of finishing it
- **THEN** the feed shows an item for that anime reporting the new score

#### Scenario: Score cleared
- **WHEN** I remove an anime's score
- **THEN** the feed shows an item for that anime reporting that the score was cleared

#### Scenario: Rewatch count change appears
- **WHEN** I change an anime's rewatch count
- **THEN** the feed shows an item for that anime reporting the new rewatch count

#### Scenario: Removal appears
- **WHEN** I remove an anime from my list
- **THEN** the feed shows an item for that anime reporting that it was removed from the list, with its title and picture intact

#### Scenario: Re-editing a field right after editing it
- **WHEN** I change an anime's score and then change it again to a different value
- **THEN** the feed shows one row for that anime's score, reporting the value it now has

#### Scenario: Editing a field back to its previous value
- **WHEN** I change an anime's episode count and then change it back
- **THEN** the feed shows one progress row for that anime, reporting the episode count it now has, rather than one row per edit

#### Scenario: Different fields of the same anime stay separate
- **WHEN** I change an anime's score and its rewatch count
- **THEN** the feed shows a row for each of those changes

#### Scenario: Excluding decreases and other status changes
- **WHEN** an anime's episode count decreased, or its status changed to something other than completed, or it was dropped, or its start or finish date changed
- **THEN** no such item appears in the latest-updates feed

### Requirement: Latest-updates titles are truncated to one line
Each row of the "Latest updates" feed SHALL show its anime's title on a single line, cut off with an ellipsis when the title is longer than the row allows, so that a long title never pushes the row's other content out of place. The full title SHALL be available on hover.

#### Scenario: A long title in the feed
- **WHEN** a feed row's title is too long for one line
- **THEN** it is cut off with an ellipsis and the row's change description and timestamp keep their place

#### Scenario: Recovering the full title
- **WHEN** I hover a feed row's truncated title
- **THEN** the full title is shown

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

The overlay SHALL also be dismissable from a close control in its own top-right corner, aligned with its title: an icon control (a ✕) rather than a labelled button beneath the list. It SHALL carry an accessible label naming what it does, SHALL be reachable and operable by keyboard, and SHALL show a hover state in the same style as the app's other icon controls. It SHALL remain visible and in place regardless of how far the history list is scrolled. No other close control SHALL be offered.

Each history row SHALL show the anime's poster picture alongside its title, in the same style as the rows of the "Latest updates" box, and SHALL show the anime's English title when one is available, falling back to the default title otherwise. A row's title SHALL be truncated to at most two lines, with the full title available on hover.

The history list SHALL show five whole rows when the overlay opens, with no part of a sixth row visible beneath them and no row clipped part-way. Its height SHALL be derived from the height of a row rather than from the viewport, so the same five whole rows are shown at any window height; the rest of the history SHALL be reached by scrolling the list. A history holding fewer than five rows SHALL show what it has without reserving the height of five.

Each history row SHALL describe its change as a single phrase, using the same phrasing as the "Latest updates" feed, and SHALL NOT pair a change-type label with a detail that restates it. Change types the feed omits — status changes, start-date changes, and finish-date changes — SHALL appear in the history, each named once by the same rule: a status change reporting the status it moved from and to, a date change reporting the new date or that the date was cleared.

Unlike the "Latest updates" feed, the full history SHALL NOT drop any episode-watched entry, and SHALL NOT collapse repeated edits of the same field into the newest one. A consecutive run of episode-watched entries for the same anime SHALL instead collapse into a single row reporting the range of episodes watched (for example, "Episodes 4-8") rather than one row per episode; a run of a single entry SHALL keep its own "Episode N" wording.

A score set as part of finishing an anime SHALL be reported on the completion's own row rather than as a second row, on the same terms as in the "Latest updates" feed — whether saved together with the completion or separately moments later. The episode run that led to the completion SHALL keep its own range row rather than being folded into the completion row.

The overlay SHALL let me narrow the history by anime title and by date:

- A search field SHALL filter rows to those whose anime title matches what I typed, matching case-insensitively against both the default and the English title, on any part of the title rather than only its start.
- A start date and an end date SHALL each filter rows to those on or after, and on or before, that date, interpreted as whole days in my local time. Either may be left empty, leaving that end of the range open.
- The search and the date bounds SHALL apply together, and SHALL be clearable back to the unfiltered history without closing the overlay.
- When filters match nothing, the overlay SHALL say so rather than render an empty list.
- Filtering SHALL apply to the collapsed rows the history already shows, and SHALL NOT require a reload of the page or a refetch per keystroke.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: Closing from the corner control
- **WHEN** I click the ✕ in the overlay's top-right corner
- **THEN** the overlay closes, and no separate Close button is shown beneath the list

#### Scenario: The close control is keyboard-operable
- **WHEN** I tab to the ✕ and press Enter or Space
- **THEN** the overlay closes, and while focused the control is clearly marked as focused

#### Scenario: Five whole rows on open
- **WHEN** the overlay opens on a history with more than five entries
- **THEN** five rows are visible in full, no part of a sixth is shown, and no row is cut through the middle

#### Scenario: Whole rows at any window height
- **WHEN** the overlay is opened in a taller or shorter window
- **THEN** it still shows five whole rows rather than a fraction of a row

#### Scenario: The rest of the history is still reachable
- **WHEN** the history holds more than five rows
- **THEN** the remainder is reached by scrolling the list, and the ✕ stays in place while it scrolls

#### Scenario: A short history
- **WHEN** the history holds two rows
- **THEN** the overlay shows those two rows without reserving the height of five

#### Scenario: English titles in the history
- **WHEN** a history row's anime has a stored English title
- **THEN** that row shows the English title rather than the default title

#### Scenario: History rows show pictures
- **WHEN** the history overlay lists an anime
- **THEN** that row shows the anime's poster picture next to its title

#### Scenario: Long history title
- **WHEN** a history row's title is longer than two lines
- **THEN** it is cut off at two lines and hovering it reveals the full title

#### Scenario: A history row reads as one phrase
- **WHEN** any row of the full history renders
- **THEN** its change is described once, with no label-and-detail pair that repeats the same wording

#### Scenario: Consecutive episode-watched entries collapse into a range
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** the full history shows a single row reporting the range of episodes watched, not one row per increase

#### Scenario: A finished anime in the history
- **WHEN** I watch an anime's last episodes, which completes it, and score it
- **THEN** the history shows one row reporting the completion together with the score, and above the episode-range row for the run that led to it

#### Scenario: Repeated edits are all kept
- **WHEN** I change an anime's score several times in a row
- **THEN** the full history shows a row for each of those changes

#### Scenario: Searching by title
- **WHEN** I type part of an anime's title into the history's search field
- **THEN** only rows whose anime matches that text, by either its default or English title, remain listed

#### Scenario: Filtering by a date range
- **WHEN** I set a start date and an end date in the history overlay
- **THEN** only rows timestamped within those days, inclusive of both, remain listed

#### Scenario: An open-ended date bound
- **WHEN** I set only a start date
- **THEN** every row from that day onwards remains listed and earlier rows are hidden

#### Scenario: Search and dates combined
- **WHEN** both a title search and a date bound are set
- **THEN** only rows matching the title and falling inside the date range remain listed

#### Scenario: Clearing the filters
- **WHEN** I clear the search field and the date inputs
- **THEN** the full unfiltered history is listed again without closing the overlay

#### Scenario: Filters match nothing
- **WHEN** the active filters exclude every row
- **THEN** the overlay states that no history matches rather than showing an empty list

### Requirement: Profile top-row box proportions
The "Anime stats" box SHALL be laid out narrow enough that each stat's value reads as belonging to its label without a connecting rule, and SHALL stay within that narrow width at any window width or display resolution rather than growing with the available space. The remaining width of the profile page's top row SHALL be taken by the other boxes in that row.

The "Latest updates" box's list SHALL fill the box down to its bottom edge rather than stopping at a fixed height, so the box has no dead space below its last visible row when the row of boxes is taller than the list's fixed height would be.

#### Scenario: Stats box on a wide display
- **WHEN** the profile page is viewed on a very wide display
- **THEN** the "Anime stats" box stays narrow and its labels and values stay close together

#### Scenario: Latest updates fills its box
- **WHEN** the profile page's top row is taller than the latest-updates list's contents would otherwise occupy
- **THEN** the list's scroll area extends to the bottom of its box, leaving no empty gap beneath it

### Requirement: Scrollbars sit beside scrollable content
Every scrollable list on the profile page and in its overlays — the "Latest updates" feed, both opinion-divergence lists, and the full edit-history list — SHALL lay out its scrollbar beside the rows rather than over them, so no row's right-hand edge, border, or content is covered by the scrollbar.

#### Scenario: Feed scrollbar does not cover rows
- **WHEN** the "Latest updates" feed has more rows than fit and shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

#### Scenario: Divergence scrollbar does not cover rows
- **WHEN** an opinion-divergence list has more rows than fit and shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

#### Scenario: History scrollbar does not cover rows
- **WHEN** the full edit-history overlay shows a scrollbar
- **THEN** the scrollbar sits beside the rows and no row is drawn underneath it

### Requirement: Profile lists show whole rows only
The "Latest updates" feed SHALL show exactly five rows at rest, with no part of a sixth row visible beneath them. Each opinion-divergence list SHALL show at most ten rows at rest, with no part of an eleventh visible beneath them; a list with fewer than ten rows SHALL show what it has without reserving space for the rest.

The feed SHALL reconcile this with filling its box: rather than stopping at a fixed height and leaving dead space, its rows SHALL take their height from the height the box gives the feed, so that five of them fill it exactly. Row height SHALL therefore follow the box — a taller box makes taller rows, not four and a fraction — and the feed SHALL keep a floor below which it does not shrink, so it cannot collapse when nothing else in the row of boxes is tall.

Rows partly scrolled out of view while scrolling are not covered by this: the requirement is about what the lists show before and after a scroll gesture, not during one.

#### Scenario: No sliver of a sixth update
- **WHEN** the profile page loads with more than five entries in the Latest updates feed
- **THEN** five rows are visible in full and no part of the sixth is shown

#### Scenario: Whole rows at any window width
- **WHEN** the window width changes enough to change the height of the profile page's top row of boxes
- **THEN** the feed still shows exactly five whole rows, resized to fill the new height

#### Scenario: Feed still fills its box
- **WHEN** the top row of boxes is taller than the feed's floor height
- **THEN** the feed's five rows extend to the bottom of the box, leaving no empty gap beneath them

#### Scenario: The rest is still reachable
- **WHEN** the feed holds more than five rows
- **THEN** the remainder is reached by scrolling the feed

#### Scenario: No sliver of an eleventh divergence row
- **WHEN** an opinion-divergence list holds more than ten anime
- **THEN** ten rows are visible in full, no part of the eleventh is shown, and the rest is reached by scrolling the list

#### Scenario: A short divergence list
- **WHEN** an opinion-divergence list holds three anime
- **THEN** its box shows those three rows and does not reserve the height of ten

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
The system SHALL provide a media-type filter on the "My top anime" box with the options All (default), TV, Movie, OVA, ONA, and Specials. Selecting a type SHALL recompute the top list from only my scored entries of that media type, applying exactly the same rules as the unfiltered list (score tiers descending, all 10s uncapped, fill to a minimum of 10, my persisted tier order, truncated-tier membership by tier order). When a filtered subset has fewer than 10 scored entries the list SHALL show all of them without padding from other media types.

The selected filter SHALL be a view control only and SHALL NOT be persisted. On a fresh visit to the profile page — a navbar link, a typed URL, or a reload — the box SHALL open on All. When the profile page is restored by back/forward navigation, the box SHALL show the filter that was selected when the page was left.

Switching the filter SHALL NOT visibly clear the box's contents while the new selection loads: the previously shown list, and any control whose visibility depends on it, SHALL remain in place until the new selection's list is ready.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "My top anime" filter
- **THEN** the list shows only my scored movies, ranked by the same score-tier and ordering rules as the unfiltered list

#### Scenario: Filtered subset smaller than ten
- **WHEN** I select a media type for which I have scored fewer than 10 anime
- **THEN** the list shows only those anime and does not pad with anime of other media types

#### Scenario: Filter resets on a fresh visit
- **WHEN** I select a media type, navigate away, and reach the profile page again by clicking its navbar link
- **THEN** the filter is back on All

#### Scenario: Filter is restored on a back navigation
- **WHEN** I select a media type, open an anime from the strip, and then navigate back
- **THEN** the "My top anime" box still shows that media type and its filtered contents

#### Scenario: Switching filters does not clear the box
- **WHEN** I select a different media type in the "My top anime" filter
- **THEN** the box keeps showing its previous list, without an empty flash, until the new type's list is ready

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

The rewatch-count badge SHALL carry the same badge form as the top-anime strip's score badge — the same position, size, pill shape, and dark tint that keeps it legible over any poster, plus a matching border — but SHALL be rendered in white/silver rather than in either score colour role. It SHALL NOT use the "mine" purple or the "MAL" blue, so a rewatch count is never read as a score, while still reading as a deliberate badge rather than as plain text laid over the poster.

The strip SHALL be ordered by rewatch count descending. Ties SHALL be broken alphabetically by title, case-insensitively, and by nothing else — my score SHALL NOT participate in the ordering, so equally-rewatched entries read in one predictable sequence rather than one that shifts whenever a score changes. The strip SHALL NOT be capped at any size — every qualifying entry SHALL be reachable by scrolling the strip to its end.

The section SHALL be read-only: it SHALL NOT offer any reordering control, and its order SHALL be derived entirely from rewatch counts rather than from the persisted top-anime ordering.

#### Scenario: Ordering by rewatch count
- **WHEN** the profile page loads and I have rewatched several anime different numbers of times
- **THEN** the "Most rewatched" strip lists them from most rewatched to least rewatched

#### Scenario: The count badge carries its own colour
- **WHEN** I look at a tile in the "Most rewatched" strip
- **THEN** its rewatch count sits in a white/silver bordered badge of the same shape, size, and position as the top-anime strip's score badge, distinct from both score colours

#### Scenario: A rewatch count is not mistaken for a score
- **WHEN** I look at the "My top anime" and "Most rewatched" strips together
- **THEN** the top-anime badges read as scores in the purple "mine" role and the rewatch badges read as counts in white/silver, so the two are never confused

#### Scenario: The badge stays legible over any poster
- **WHEN** a rewatched tile's poster is bright, pale, or busy behind the badge
- **THEN** the badge's count remains legible against it

#### Scenario: Entries never rewatched are excluded
- **WHEN** an anime on my list has a rewatch count of zero
- **THEN** it does not appear in the "Most rewatched" strip

#### Scenario: Rewatched but not completed
- **WHEN** an anime has a rewatch count above zero and a status other than completed
- **THEN** it still appears in the "Most rewatched" strip

#### Scenario: Breaking a tie
- **WHEN** two anime have the same rewatch count
- **THEN** they appear in alphabetical order by title, regardless of how I have scored them

#### Scenario: A score change does not reorder the strip
- **WHEN** I change my score on an anime in the "Most rewatched" strip without changing its rewatch count
- **THEN** its position in the strip is unchanged

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

The filter SHALL be a view control only and SHALL NOT be persisted: on a fresh visit the section SHALL open on All, and when the profile page is restored by back/forward navigation the section SHALL show the filter that was selected when the page was left. The two boxes' filters SHALL be independent, so changing the media type on one box SHALL NOT change the other box's selection or contents.

Switching the filter SHALL NOT visibly clear the strip's contents while the new selection loads: the previously shown strip SHALL remain in place until the new selection's list is ready.

#### Scenario: Filtering to a single media type
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the strip shows only my rewatched movies, still ordered most rewatched first

#### Scenario: Filters are independent
- **WHEN** I select Movie in the "Most rewatched" filter
- **THEN** the "My top anime" box keeps its own selected filter and contents

#### Scenario: Filter resets on a fresh visit
- **WHEN** I select a media type, navigate away, and reach the profile page again by clicking its navbar link
- **THEN** the "Most rewatched" filter is back on All

#### Scenario: Both filters are restored on a back navigation
- **WHEN** I select different media types in the two boxes, open an anime, and then navigate back
- **THEN** each box still shows the media type it had

#### Scenario: Switching filters does not clear the strip
- **WHEN** I select a different media type in the "Most rewatched" filter
- **THEN** the strip keeps showing its previous contents, without an empty flash, until the new type's list is ready

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

### Requirement: Poster strips keep a fixed tile size and scroll horizontally only
The "My top anime", "Top series", and "Most rewatched" strips SHALL size their poster tiles as a fixed fraction of the strip's width — ten tiles across the visible width — regardless of how many entries the strip contains. Tiles SHALL NOT shrink to fit additional entries; entries beyond the tenth SHALL be reached by scrolling the strip horizontally.

Ten tiles SHALL actually fit: the tile's full rendered width, including any border it carries, SHALL be what the ten-across computation divides the strip's visible width into, so that a strip holding exactly ten entries shows all ten whole — the tenth SHALL NOT be clipped at the strip's trailing edge, and this SHALL hold for a strip that cannot be scrolled as much as for one that can.

Each strip SHALL scroll along the horizontal axis only. Vertical scrolling within a strip SHALL be impossible: a trackpad or wheel gesture SHALL NOT be able to displace the strip's contents up or down, including where a hovered tile's scaled-up size exceeds the strip's height.

A strip holding no more entries than fit across its visible width SHALL NOT be scrollable at all: no wheel gesture and no drag SHALL displace its contents by any amount, and the strip SHALL NOT present itself as scrollable — including the grab cursor, which SHALL appear only on a strip that can actually be scrolled. Sub-pixel differences between the tiles' total width and the strip's width SHALL NOT make a strip that fits behave as a scrollable one.

#### Scenario: Exactly ten entries are all whole
- **WHEN** a strip holds exactly ten entries
- **THEN** all ten tiles are fully visible, with the tenth's trailing edge inside the strip rather than cut off by it

#### Scenario: The tenth tile of a longer strip
- **WHEN** a strip holds more than ten entries and sits at its starting scroll position
- **THEN** the tenth tile is whole and the eleventh is the first one reached by scrolling

#### Scenario: More than ten top anime
- **WHEN** I have more than ten anime scored 10
- **THEN** the tiles stay the same size as when there were exactly ten, and the rest are reached by scrolling the strip

#### Scenario: Exactly ten entries do not scroll
- **WHEN** a media-type filter leaves a strip with exactly ten entries
- **THEN** the strip cannot be scrolled or dragged in either direction, and its cursor does not offer to drag it

#### Scenario: Switching to a filter that fits stops the scrolling
- **WHEN** I switch from a filter whose strip scrolls to one whose entries all fit
- **THEN** the strip stops being scrollable, rather than keeping a few pixels of travel from the wider list

#### Scenario: Tile size matches between the strips
- **WHEN** two of the strips are shown at the same window width
- **THEN** their tiles are the same size

#### Scenario: No vertical drift
- **WHEN** I make a vertical scroll gesture with the pointer over any strip
- **THEN** the strip's contents do not move up or down

### Requirement: Poster strips show no scrollbar
The "My top anime", "Top series", and "Most rewatched" strips SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the posters are the content and a bar under them is noise.

Hiding the scrollbar SHALL NOT cost the strips any scrolling: a strip that holds more entries than fit SHALL still scroll by wheel gesture and by dragging it, exactly as it does today.

This SHALL apply to the poster strips only. The profile page's vertical lists — the "Latest updates" feed and the full edit-history overlay — SHALL keep their scrollbars beside their rows.

#### Scenario: No bar under the posters
- **WHEN** a strip holds more entries than fit across it
- **THEN** no scrollbar is drawn under or over the tiles, at rest or while scrolling

#### Scenario: Scrolling still works
- **WHEN** I drag an overflowing strip, or make a horizontal wheel gesture over it
- **THEN** it scrolls exactly as it did when it had a scrollbar

#### Scenario: Vertical lists keep theirs
- **WHEN** the "Latest updates" feed has more rows than fit
- **THEN** it still shows its scrollbar beside its rows

### Requirement: All-anime score distribution
The system SHALL show a count of anime per score/rating value plus the overall mean score, rendered as a bar per score value alongside its count and that score's share of all my rated anime.

Each bar's length SHALL be that score's count relative to the largest count across all score values, so the most-common score's bar fills the full width of its track and every other bar is drawn in proportion to it. A score with a count of zero SHALL render an empty track.

Each row SHALL show, after its count, that score's share of all my rated anime as a percentage — the count for that score divided by the total number of anime I have rated. A share that rounds to zero but comes from a non-zero count SHALL be shown as less than one percent rather than as zero.

When I have rated no anime at all, every track SHALL render empty and no share SHALL be shown as a nonsensical value.

#### Scenario: Rendering the distribution
- **WHEN** the profile page loads
- **THEN** it shows how many anime have each score value, that score's share as a percentage, and the overall mean score

#### Scenario: The most-common score fills its track
- **WHEN** one score has more anime than any other score
- **THEN** that score's bar fills the full width of its track

#### Scenario: Bars are proportional to the largest
- **WHEN** one score has half as many anime as the most-common score
- **THEN** its bar fills half the track width

#### Scenario: Share shown alongside the count
- **WHEN** a score accounts for 30% of all the anime I've rated
- **THEN** its row shows that score's count followed by 30%

#### Scenario: A very small share
- **WHEN** a score has at least one anime but its share rounds down to zero percent
- **THEN** its row shows less than one percent rather than zero percent

#### Scenario: Nothing rated yet
- **WHEN** I have rated no anime
- **THEN** every bar renders as an empty track and no share is shown as an invalid number

### Requirement: Opinion divergence lists
The system SHALL show two opinion-divergence lists comparing my score against MAL's score for anime I have rated: "They liked it, I didn't" and "I liked it, they didn't".

Membership SHALL be decided on *standardized* scores rather than the raw difference between them. MAL's community averages occupy a far narrower band than personal 1–10 scores, so an identical raw gap on both sides can never populate both lists; each score SHALL therefore be measured in standard deviations from the mean of its own scale. The population for both scales SHALL be the anime I have rated that also carry a MAL average — the same population both lists draw from — and the mean and standard deviation SHALL be recomputed from that population whenever the profile is built, so the rules track my list as it grows rather than sitting on fixed constants.

For each anime in that population, its divergence SHALL be how many standard deviations MAL's score sits above its own mean minus how many my score sits above mine. An anime SHALL qualify for a list when its divergence reaches 1.0 in that list's direction **and** it passes that list's label gate, which keeps the heading's claim honest about both parties:

- "They liked it, I didn't" — MAL's score is at least 7.5 and my score is at most 5.
- "I liked it, they didn't" — my score is at least 8 and MAL's score is at most 7.5.

The 5 and 8 boundaries are MAL's own score labels: 5 is "Average" and below it lies everything worse, 8 is "Very Good" and above it everything better. The scores between them — 6 ("Fine") and 7 ("Good") — are a neutral band, and an anime I scored in that band SHALL fall in neither list however far MAL's average sits from it. The 7.5 boundary splits MAL's community scale between its "Good" and "Very Good" labels.

Each list SHALL be ordered by divergence, strongest first, with ties broken by title case-insensitively. Neither list SHALL be capped: every anime that qualifies SHALL be present, reachable by scrolling its box.

Divergence needs a population to normalize against. When fewer than 10 anime carry both my score and a MAL average, or when either scale's standard deviation across that population is zero, both lists SHALL be empty rather than ranking on a spread that does not exist.

#### Scenario: They liked it, I didn't
- **WHEN** I rated an anime 3 and its MAL average is 8.70, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "They liked it, I didn't" list

#### Scenario: I liked it, they didn't
- **WHEN** I rated an anime 9 and its MAL average is 6.31, more than 1.0 standard deviation apart once each score is standardized against its own scale
- **THEN** it appears in the "I liked it, they didn't" list

#### Scenario: Both lists populate
- **WHEN** my scores run lower on average and spread wider than the MAL averages of the same anime
- **THEN** neither list is starved by that difference in scale — each holds every anime that diverges by the same standardized amount in its own direction

#### Scenario: A score I did not dislike stays out
- **WHEN** an anime's MAL average diverges upward from my score by more than 1.0 standard deviation but I scored it 6
- **THEN** it does not appear in "They liked it, I didn't", because the list claims I didn't like it and a 6 does not say that

#### Scenario: A community score that is not a dislike stays out
- **WHEN** I scored an anime 10 and it diverges downward by more than 1.0 standard deviation, but its MAL average is 7.8
- **THEN** it does not appear in "I liked it, they didn't", because the list claims they didn't like it and a 7.8 average does not say that

#### Scenario: Strongest disagreement first
- **WHEN** either list renders
- **THEN** its rows run from the largest standardized divergence to the smallest

#### Scenario: Too little to normalize against
- **WHEN** fewer than 10 of my rated anime carry a MAL average
- **THEN** both lists are empty and each box shows its empty state

### Requirement: Top series section
The profile page SHALL show a **Top series** section that ranks my franchises, presented like My top anime and Most rewatched: a horizontally-scrolling, drag-scrollable strip of fixed-size poster tiles, uncapped, using the same tile size and hover behaviour as the other two strips.

A series SHALL be eligible for the section when at least one of its members — main line or extra — is in my list. A series with no member in my list SHALL NOT appear.

A series whose main line holds two or more entries that have started airing SHALL additionally require that **at least two of those aired main-line entries are in my list**. A series that clears the first condition but not this one SHALL NOT be listed, because its averages would rank a whole franchise on a single entry's worth of my viewing. An entry counts as in my list at **any** status — plan-to-watch included — and a main-line entry that has been announced but has not aired a single episode SHALL NOT count toward either total, so a series whose main line holds one aired entry is unaffected by this rule however many sequels are confirmed.

That exclusion SHALL be unconditional: no control SHALL switch it off, and it SHALL apply under either ranking basis. It SHALL be independent of the single-entry filter, which narrows the strip further when the user switches it on.

Each tile SHALL show the series' poster and **both** of its main-series averages: MAL's average across the main line, and my average across the main line. Those figures SHALL be the same figures that series' own page shows for `MAL · main series` and `Mine · main series`, computed the same way, so the two surfaces can never disagree. The two figures SHALL be distinguishable by the score colour roles (see the `score-presentation` capability).

Opening a tile SHALL navigate to that series' page, not to an anime's detail page.

Each tile SHALL make available, without requiring navigation, how many main-line entries each average was computed over — the same "N of M scored" figure the series page shows beside its chips.

#### Scenario: Ranking my franchises
- **WHEN** I open the profile page
- **THEN** a Top series section lists my series as poster tiles, each showing both its MAL main-series average and my main-series average

#### Scenario: Only series I have watched something of
- **WHEN** a stored series has no member in my list
- **THEN** it does not appear in Top series

#### Scenario: A franchise I have barely started
- **WHEN** a series' main line has three seasons that have aired and only one of them is in my list
- **THEN** the series is not listed, under either ranking basis

#### Scenario: Two of a franchise's seasons is enough
- **WHEN** a series' main line has three seasons that have aired and two of them are in my list
- **THEN** the series is listed and ranked normally

#### Scenario: An unaired sequel does not make a franchise barely-watched
- **WHEN** a series' main line has one aired season, which is in my list, plus a second season that is announced but has not aired an episode
- **THEN** the series is listed, because only one main-line entry has actually aired

#### Scenario: Plan-to-watch counts as coverage
- **WHEN** a series' main line has two aired seasons, one completed and one sitting in my list as plan-to-watch
- **THEN** the series is listed, because both aired main-line entries are in my list

#### Scenario: A series I only know through an extra
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has a single aired entry
- **THEN** the series is eligible, and its two averages are still computed over its main line only

#### Scenario: An extra is not coverage of a multi-season franchise
- **WHEN** the only member of a series in my list is one of its extras, and the series' main line has three aired entries
- **THEN** the series is not listed, because at most one of its aired main-line entries is in my list

#### Scenario: The exclusion cannot be switched off
- **WHEN** I look for a way to see the franchises this rule hides
- **THEN** the section offers none, and switching the single-entry filter or the ranking basis does not bring them back

#### Scenario: Figures match the series page
- **WHEN** I compare a tile's two averages with that series' page
- **THEN** they equal the page's `MAL · main series` and `Mine · main series` averages

#### Scenario: Opening a series from the strip
- **WHEN** I click a tile
- **THEN** I am taken to that series' page

#### Scenario: The strip scrolls rather than wrapping
- **WHEN** I have more series than fit across the section
- **THEN** the strip scrolls horizontally at a fixed tile size, and can be dragged to scroll, exactly like the other two strips

### Requirement: Top series ranking basis
Top series SHALL be ranked by a **main-series average**, and SHALL offer a control that switches which average ranks it: **my score** or **MAL's score**. The control SHALL default to my score.

Ranking SHALL be by the selected average in descending order. Ties SHALL be broken by the number of scored main-line entries the average was computed over, descending — so an average earned across more entries places higher — and then by title, case-insensitively.

Switching the basis SHALL reorder the strip without reloading the page's data and without the strip collapsing or changing height.

Both averages SHALL remain visible on every tile regardless of which basis is selected; the basis chooses the ordering, not what is shown.

#### Scenario: Default ranking
- **WHEN** I open the profile page without having changed the control
- **THEN** Top series is ranked by my main-series average, highest first

#### Scenario: Ranking by MAL's score
- **WHEN** I switch the control to MAL's score
- **THEN** the strip reorders by MAL's main-series average, highest first, and both averages are still shown on every tile

#### Scenario: An average earned over more entries wins a tie
- **WHEN** two series have the same average under the selected basis, one computed over five scored entries and one over two
- **THEN** the one computed over five places first

#### Scenario: Switching the basis is immediate
- **WHEN** I switch the ranking basis
- **THEN** the strip reorders immediately without a visible reload and without collapsing

### Requirement: Series unrankable under the selected basis are omitted
A series that has no value under the **selected** basis SHALL be omitted from the strip while that basis is selected: under my score, a series with no scored main-line entry; under MAL's score, a series whose main line carries no MAL score at all.

Switching the basis MAY therefore change which series are listed, not only their order.

#### Scenario: A series I have not scored
- **WHEN** the basis is my score and a series has no scored main-line entry
- **THEN** it is omitted from the strip

#### Scenario: The same series under MAL's score
- **WHEN** I then switch the basis to MAL's score and that series' main line does carry MAL scores
- **THEN** it appears in the strip

#### Scenario: A main line with no MAL score at all
- **WHEN** the basis is MAL's score and a series' main line is entirely unscored by MAL
- **THEN** it is omitted from the strip

### Requirement: Top series ranking basis is restored on back-navigation
The selected ranking basis SHALL behave like the existing profile filters: it SHALL reset to its default on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation. It SHALL be independent of the My top anime and Most rewatched media-type filters.

#### Scenario: Basis resets on a fresh visit
- **WHEN** I set the basis to MAL's score, navigate away, and later open the profile page fresh
- **THEN** the basis is back to my score

#### Scenario: Basis is restored going back
- **WHEN** I set the basis to MAL's score, open a series from the strip, and press back
- **THEN** the strip is still ranked by MAL's score

#### Scenario: Independent of the other filters
- **WHEN** I change the Top series basis
- **THEN** the My top anime and Most rewatched media-type filters are unchanged

### Requirement: Top series single-entry filter
Top series SHALL offer a control that hides every series whose **main line** holds a single entry, and shows them again when it is switched off. The control SHALL default to showing them, so the section lists the same series it lists today until the control is used.

What counts SHALL be the number of main-line entries, not the series' total member count: a series with one main-line entry plus any number of extras — OVAs, specials, side stories — SHALL count as single-entry and SHALL be hidden while the filter is on. A series with two or more main-line entries SHALL remain listed regardless of how many extras it has.

A main-line entry that has been announced but has not yet aired a single episode SHALL NOT count toward that total: a series is multi-entry only once a second main-line entry has actually started airing, not merely once it has been confirmed.

The control SHALL narrow which series are listed and SHALL NOT change the order of those that remain, nor what any tile shows.

The control SHALL be independent of the ranking basis: switching one SHALL NOT reset the other, and a series SHALL be listed only when it survives both — it has a value under the selected basis and, while the filter is on, more than one main-line entry.

The control SHALL make its current state apparent without requiring the user to compare the strip before and after pressing it.

#### Scenario: Default lists single-entry series
- **WHEN** I open the profile page without having used the control
- **THEN** Top series lists every eligible series, single-entry ones included, exactly as it does today

#### Scenario: Hiding single-entry series
- **WHEN** I switch the filter on
- **THEN** every series whose main line holds a single entry disappears from the strip, and the multi-entry series remain in the same relative order

#### Scenario: Extras do not make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry plus several extras in my list
- **THEN** that series is hidden, because only its main-line count is considered

#### Scenario: A franchise with extras stays listed
- **WHEN** the filter is on and a series has three main-line seasons plus extras
- **THEN** that series remains in the strip

#### Scenario: An announced-but-unaired sequel doesn't make a series multi-entry
- **WHEN** the filter is on and a series has one main-line entry that has aired plus a second main-line entry that's announced but hasn't aired a single episode
- **THEN** that series is hidden, because the second entry doesn't count until it starts airing

#### Scenario: Showing them again
- **WHEN** I switch the filter back off
- **THEN** the single-entry series reappear in the strip in their ranked positions

#### Scenario: Filtering is immediate
- **WHEN** I switch the filter
- **THEN** the strip updates immediately without reloading the page's data and without collapsing

#### Scenario: Composing with the ranking basis
- **WHEN** the filter is on and I switch the ranking basis to MAL's score
- **THEN** the strip reorders by MAL's main-series average and single-entry series stay hidden

#### Scenario: The control shows its state
- **WHEN** the filter is on
- **THEN** the control reads as active rather than looking identical to its off state

### Requirement: Top series single-entry filter is restored on back-navigation
The single-entry filter SHALL behave like the Top series ranking basis: it SHALL reset to its default — single-entry series shown — on a fresh visit to the profile page, and SHALL be restored when returning to the page by back-navigation.

#### Scenario: Filter resets on a fresh visit
- **WHEN** I switch the filter on, navigate away, and later open the profile page fresh
- **THEN** single-entry series are listed again

#### Scenario: Filter is restored going back
- **WHEN** I switch the filter on, open a series from the strip, and press back
- **THEN** single-entry series are still hidden

#### Scenario: Independent of the ranking basis
- **WHEN** I switch the filter on and then change the ranking basis
- **THEN** the filter is still on, and switching the filter afterwards leaves the basis where I set it

### Requirement: Top series empty and incomplete states
When no series is eligible, the section SHALL say so rather than rendering an empty strip.

Because a series is only known once it has been built, the section SHALL disclose that its coverage grows over time, and its empty state SHALL point at the Settings action that builds every series from my list (see the `series-page` capability) rather than leaving the absence unexplained.

Reading the section SHALL never block on building a series and SHALL never fail because a series is missing — a series not yet built is simply not listed yet.

When series are eligible but the single-entry filter leaves none to show, the section SHALL say that the filter is what emptied it, so the state reads as a filter effect rather than as missing data, and SHALL NOT point at the Settings build action.

#### Scenario: Nothing to rank yet
- **WHEN** no series with a member in my list has been built
- **THEN** the section explains that series are still being discovered and names the Settings action that builds them all, instead of showing an empty strip

#### Scenario: Nothing rankable under the selected basis
- **WHEN** series are eligible but none has a value under the selected basis
- **THEN** the section says so for that basis rather than showing an empty strip

#### Scenario: Reading never waits on a build
- **WHEN** I open the profile page while series are still being built in the background
- **THEN** the section renders immediately with whatever series are already known

#### Scenario: The filter hid everything
- **WHEN** the single-entry filter is on and every series rankable under the selected basis has a single main-line entry
- **THEN** the section says the filter left nothing to show, rather than showing an empty strip or blaming missing series data


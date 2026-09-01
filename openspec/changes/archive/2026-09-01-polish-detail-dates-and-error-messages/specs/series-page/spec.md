## MODIFIED Requirements

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by their relation to the main line, so the extras read as a different kind of thing from the chronological main line and each group states how its entries stand to the franchise.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned. Because groups no longer share a media type, each tile's media type SHALL be legible on the tile itself.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading, counting the entries the media-type filter currently admits.

The More section SHALL offer three section-wide controls: an "in my list" control, an expand/collapse-all control, and the media-type filter buttons its own requirement defines. Which extras are visible SHALL be governed by those controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

**Every group SHALL render collapsed when a series page is opened.** The section SHALL therefore open as a column of relation-group headings, each carrying its count, with no tiles rendered at all — a franchise with a dozen relation groups is not made to fill the page before the reader has asked for any of it. A group opens from its own heading, from the expand/collapse-all control, from the "in my list" control, or by selecting a media type it holds, per the media-type filter requirement.

The "in my list" filter SHALL be on when a series page is opened. While it is on, an expanded group SHALL show only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — hiding every extra that is not.

**Activating the "in my list" control SHALL show my entries.** It SHALL turn the filter on, drop every per-group exemption from it, and open every group holding at least one extra of mine that the media-type filter admits, so that what the section shows afterwards is exactly my own extras — narrowed to the selected media types when any are selected, and across every group when none are. A group holding none of mine SHALL be left collapsed, so the section is not padded with headings that would show nothing. When no group holds an extra of mine at all, every group SHALL stay collapsed.

Activating the control again SHALL collapse every group, returning the section to its headings alone with the filter still on — the same "show it / put it away" pair the expand/collapse-all control offers for everything.

The control SHALL report which of those two states the section is in: it SHALL read as on only while the filter is in force, no group is exempt from it, and at least one group is open. A freshly opened series page — filter on, every group collapsed — SHALL therefore read as off, so the first press does something visible rather than nothing; and opening one group in full from its heading SHALL make it read off, per "A More group's heading opens that group in full".

Turning the filter **off** is done by the expand/collapse-all control, which shows everything, or by a group's heading, which exempts that one group; the "in my list" control itself never turns the filter off.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, expanding them all and turning the filter off. It SHALL therefore read "Expand" on a freshly opened series page, whose groups are all collapsed. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own from its heading, per "A More group's heading opens that group in full", and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group that is showing at least one tile while the filter hides the rest SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows. A group showing no tiles at all — because it is collapsed, or because nothing in it is in my list — SHALL NOT offer that control: its heading opens it, and its entry count is already in the heading.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More by relation
- **WHEN** a series has two recap specials, one side-story OVA, and one alternative version
- **THEN** the More section shows a collapsible "Summary", "Side story" and "Alternative version" group, each heading carrying its count, and their poster tiles once opened

#### Scenario: Media type is legible on the tile
- **WHEN** one "Side story" group holds an OVA, a movie and a special
- **THEN** each tile states its own media type

#### Scenario: A tile's episode count is not truncated away
- **WHEN** a portrait-pictured extra is a `TV special` that aired in 2003 and has 99 episodes
- **THEN** its tile shows `TV special · 2003 · 99 ep` in full at the grid's narrowest column width

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are longer still than that worst case
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: Opening a series shows the headings alone
- **WHEN** I open a series with twenty extras across five relation groups, four of the extras being in my list
- **THEN** all five groups are collapsed with their counts in their headings, no tile is rendered, the all-groups control reads "Expand all", and the "in my list" control reads as off

#### Scenario: Pressing "in my list" shows my extras
- **WHEN** I then activate the "in my list" control
- **THEN** every group holding at least one of those four extras opens showing exactly those, the groups holding none of mine stay collapsed, and the control reads as on

#### Scenario: Pressing it again puts them away
- **WHEN** the section is showing my extras and I activate the "in my list" control again
- **THEN** every group collapses, no tile is rendered, and the control reads as off

#### Scenario: With a media type selected, only that type of mine is shown
- **WHEN** I select "Movie" and then activate the "in my list" control
- **THEN** the groups holding a movie of mine open showing only those movies, and no entry of another type and no movie that is not in my list is shown

#### Scenario: Nothing of mine anywhere
- **WHEN** none of a series' extras is in my list and I activate the "in my list" control
- **THEN** every group stays collapsed and no tile is rendered

#### Scenario: Opening one group shows every extra in it
- **WHEN** I activate the heading of one collapsed group
- **THEN** that group shows every one of its extras and reads as exempt from the filter, while the other groups keep the state they were in

#### Scenario: Expanding shows everything
- **WHEN** I activate the all-groups control on a freshly opened series
- **THEN** all twenty extras are shown, the "in my list" control reads as off, and the control now reads "Collapse all"

#### Scenario: Collapsing hides my own extras too
- **WHEN** every extra is shown and I activate the "Collapse all" control
- **THEN** no tiles are rendered in any group, including the extras in my list, and the control reads "Expand all" again

#### Scenario: Filtering back to my list
- **WHEN** every extra is shown and I activate the "in my list" control
- **THEN** each group holding an extra of mine shows only those extras, a group holding none of mine is collapsed, and the all-groups control reads "Expand all"

#### Scenario: A group with nothing of mine in it
- **WHEN** the filter is on, a group is expanded, and it holds no extras that are in my list
- **THEN** that group shows its heading and entry count with no tiles and no hidden-count control, and its heading opens it in full

#### Scenario: Revealing one group's hidden extras
- **WHEN** the filter is on, an expanded group is showing the extras of mine it holds while hiding others, and I activate that group's hidden-count control
- **THEN** that group shows all of its tiles while every other group keeps showing only my own extras

#### Scenario: Controls are offered whatever my statuses
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** the "in my list" control and the all-groups control are both still offered, and expanding then collapsing hides those completed extras

#### Scenario: A large More section is not treated differently
- **WHEN** I open a series with twenty extras and one with four extras
- **THEN** both open with every group collapsed and the filter on, rather than one of them starting expanded

#### Scenario: Editing from a tile
- **WHEN** I use a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the tile and the series stats reflect the new score without a page reload

#### Scenario: An extra added to my list from its tile stays visible
- **WHEN** the filter is on, I add an extra to my list from a revealed tile, and the section re-renders
- **THEN** that extra is now one of the tiles the filter keeps

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one relation group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more relation groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

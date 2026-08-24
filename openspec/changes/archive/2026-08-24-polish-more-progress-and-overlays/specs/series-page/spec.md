## ADDED Requirements

### Requirement: A More group's heading opens that group in full

Each More group's heading SHALL be the control that opens that group, with collapse as its off state. Activating a heading SHALL show **every** extra in that group — including the extras not in my list — unless the group is already showing every one of them, in which case it SHALL collapse the group so that none of its tiles is rendered.

Opening a group this way SHALL exempt that group, and only that group, from the "in my list" filter: every other group SHALL keep showing exactly what it was showing. An exempted group SHALL stay exempt until the filter is turned back on.

While the "in my list" filter is off, every group is already showing everything, so a heading SHALL simply expand and collapse its group.

The "in my list" control SHALL report itself as **on** only while the filter is in force across every group — that is, while it is on and no group has been opened in full. Opening any group in full SHALL therefore make that control read as off, so the section never reports itself as filtered while showing a group whole.

Activating the "in my list" control while it reads as off SHALL turn the filter on, drop every group's exemption, and expand every group, returning the section to the state a freshly opened series page is in. Activating it while it reads as on SHALL show every extra, as it does today.

#### Scenario: Opening a group that holds nothing of mine

- **WHEN** the filter is on, a group holds seven extras of which none is in my list, and I activate that group's heading
- **THEN** all seven of its tiles are shown, every other group keeps showing only my own extras, and the "in my list" control now reads as off

#### Scenario: The heading collapses a group it has opened

- **WHEN** I activate the heading of a group that is showing all of its extras
- **THEN** that group renders no tiles at all

#### Scenario: Opening a group that holds some of mine

- **WHEN** the filter is on, a group holds six extras of which three are in my list, and I activate that group's heading
- **THEN** all six of its tiles are shown, rather than the three the filter was showing

#### Scenario: Opening one group leaves the others alone

- **WHEN** I open one group in full while the filter is on
- **THEN** every other group still shows only the extras in my list, each with its own hidden-count control if it has one

#### Scenario: Turning the filter back on re-filters everything

- **WHEN** a group has been opened in full and I activate the "in my list" control, which reads as off
- **THEN** every group is expanded and showing only the extras in my list, and the control reads as on again

#### Scenario: The heading is a plain toggle while the filter is off

- **WHEN** the "in my list" filter is off and I activate a group's heading twice
- **THEN** that group collapses and then shows all of its extras again

#### Scenario: A collapsed group offers no hidden-count control

- **WHEN** a group is collapsed
- **THEN** it shows its heading and entry count alone, with no control naming how many tiles are hidden

## MODIFIED Requirements

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by media type, so the extras read as a different kind of thing from the chronological main line.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading.

The More section SHALL offer two section-wide controls: an "in my list" filter and an expand/collapse-all control. Which extras are visible SHALL be governed by those two controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

The "in my list" filter SHALL be on when a series page is opened: every group renders expanded, showing only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — and hiding every extra that is not in my list. It SHALL be a two-state control that reports which state it is in, per "A More group's heading opens that group in full". Turning it on SHALL restore that filtered view across every group; turning it off SHALL show every extra.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, turning the filter off. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own from its heading, per "A More group's heading opens that group in full", and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group that is showing at least one tile while the filter hides the rest SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows. A group showing no tiles at all — because it is collapsed, or because nothing in it is in my list — SHALL NOT offer that control: its heading opens it, and its entry count is already in the heading.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them as poster tiles under a collapsible group per media type, each heading carrying its count

#### Scenario: A tile's episode count is not truncated away
- **WHEN** a portrait-pictured extra is a `TV special` that aired in 2003 and has 99 episodes
- **THEN** its tile shows `TV special · 2003 · 99 ep` in full at the grid's narrowest column width

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are longer still than that worst case
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: Opening a series shows only my own extras
- **WHEN** I open a series with twenty extras, four of which are in my list — one Watching, one Completed, one Dropped, one Plan to watch
- **THEN** every group is expanded showing only those four tiles, the sixteen extras not in my list are hidden, and the all-groups control reads "Expand all"

#### Scenario: Expanding shows everything
- **WHEN** I activate the all-groups control from that state
- **THEN** all twenty extras are shown, the "in my list" filter reads as off, and the control now reads "Collapse all"

#### Scenario: Collapsing hides my own extras too
- **WHEN** every extra is shown and I activate the "Collapse all" control
- **THEN** no tiles are rendered in any group, including the extras in my list, and the control reads "Expand all" again

#### Scenario: Filtering back to my list
- **WHEN** every extra is shown and I turn the "in my list" filter on
- **THEN** each group shows only its extras that are in my list, and the all-groups control reads "Expand all"

#### Scenario: A group with nothing of mine in it
- **WHEN** the filter is on and a group holds no extras that are in my list
- **THEN** that group shows its heading and entry count with no tiles and no hidden-count control, and its heading opens it in full

#### Scenario: Revealing one group's hidden extras
- **WHEN** the filter is on, a group is showing the extras of mine it holds while hiding others, and I activate that group's hidden-count control
- **THEN** that group shows all of its tiles while every other group keeps showing only my own extras

#### Scenario: Controls are offered whatever my statuses
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** the "in my list" filter and the all-groups control are both still offered, and collapsing hides those completed extras

#### Scenario: A large More section is not treated differently
- **WHEN** I open a series with twenty extras and one with four extras
- **THEN** both open with their groups expanded and filtered to the extras in my list, rather than one of them starting collapsed

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
- **WHEN** a series has extras in only one media-type group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more media-type groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

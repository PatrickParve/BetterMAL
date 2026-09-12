## MODIFIED Requirements

### Requirement: A More group's heading opens that group in full

Each More group's heading SHALL be the control that opens that group, with collapse as its off state. Activating a heading SHALL show **every** extra in that group — including the extras not in my list — unless the group is already showing every one of them, in which case it SHALL collapse the group so that none of its tiles is rendered.

Opening a group this way while the "in my list" filter is on SHALL exempt that group, and only that group, from the filter: every other group SHALL keep showing exactly what it was showing. An exempted group SHALL stay exempt until the filter is turned back on.

While the "in my list" filter is off — its state on a freshly opened page — a group has nothing to be exempted from, so a heading SHALL simply expand and collapse its group.

The "in my list" control SHALL report itself as **on** exactly while the filter is in force across every group — that is, while the filter is on and no group has been opened in full. Opening any group in full SHALL therefore make that control read as off, so the section never reports itself as filtered while showing a group whole. The control's reported state SHALL depend on nothing else: in particular it SHALL NOT depend on whether any group is expanded, so opening or collapsing groups — from a heading, from the expand/collapse-all control, or by selecting a media type — SHALL never change what the control reads while the filter itself is unchanged.

The "in my list" control SHALL govern **what an expanded group shows**. Activating it while it reads as off SHALL turn the filter on and drop every group's exemption, so every expanded group returns to showing only the extras in my list, and SHALL additionally open the groups holding my extras as the More section requirement describes. Activating it while it reads as on SHALL turn the filter off, showing every extra of every expanded group and changing no group's collapsed state.

#### Scenario: Opening a group that holds nothing of mine

- **WHEN** the filter is on, a group holds seven extras of which none is in my list, and I activate that group's heading
- **THEN** all seven of its tiles are shown, every other group keeps showing what it was showing, and the "in my list" control now reads as off

#### Scenario: The heading collapses a group it has opened

- **WHEN** I activate the heading of a group that is showing all of its extras
- **THEN** that group renders no tiles at all

#### Scenario: Opening a group that holds some of mine

- **WHEN** the filter is on, an expanded group holds six extras of which three are in my list, and I activate that group's heading
- **THEN** all six of its tiles are shown, rather than the three the filter was showing

#### Scenario: Opening one group leaves the others alone

- **WHEN** I open one group in full while the filter is on
- **THEN** every other group is unchanged — an expanded one still shows only the extras in my list with its own hidden-count control if it has one, and a collapsed one is still collapsed

#### Scenario: Opening a group does not turn the filter on

- **WHEN** the filter is off, as on a freshly opened page, and I open one group from its heading
- **THEN** that group shows every one of its extras and the "in my list" control still reads as off

#### Scenario: Turning the filter back on re-filters without collapsing

- **WHEN** a group has been opened in full and I activate the "in my list" control, which reads as off
- **THEN** every group's exemption is dropped, every expanded group shows only the extras in my list, the groups holding an extra of mine are opened, and the control reads as on

#### Scenario: Turning the filter off does not collapse anything

- **WHEN** the filter reads as on, some groups are expanded and some are collapsed, and I activate the "in my list" control
- **THEN** each expanded group now shows every one of its extras, each collapsed group is still collapsed, and the control reads as off

#### Scenario: The heading is a plain toggle while the filter is off

- **WHEN** the "in my list" filter is off and I activate a group's heading twice
- **THEN** that group collapses and then shows all of its extras again

#### Scenario: A collapsed group offers no hidden-count control

- **WHEN** a group is collapsed
- **THEN** it shows its heading and entry count alone, with no control naming how many tiles are hidden

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by their relation to the main line, so the extras read as a different kind of thing from the chronological main line and each group states how its entries stand to the franchise.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned. Because groups no longer share a media type, each tile's media type SHALL be legible on the tile itself.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading, counting the entries the media-type filter currently admits.

The More section SHALL offer three section-wide controls: an "in my list" control, an expand/collapse-all control, and the media-type filter buttons its own requirement defines. Which extras are visible SHALL be governed by those controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

**Every group SHALL render collapsed when a series page is opened.** The section SHALL therefore open as a column of relation-group headings, each carrying its count, with no tiles rendered at all — a franchise with a dozen relation groups is not made to fill the page before the reader has asked for any of it. A group opens from its own heading, from the expand/collapse-all control, from the "in my list" control, or by selecting a media type it holds, per the media-type filter requirement.

**The "in my list" filter SHALL be off when a series page is opened**, with no media type selected, so that no section control reads as on and nothing has been narrowed on the user's behalf. An expanded group SHALL therefore show every extra it holds until the filter is turned on. While the filter is on, an expanded group SHALL show only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — hiding every extra that is not.

**Activating the "in my list" control while it reads as off SHALL show my entries.** It SHALL turn the filter on, drop every per-group exemption from it, and open every group holding at least one extra of mine that the media-type filter admits, so that what the section shows afterwards is exactly my own extras — narrowed to the selected media types when any are selected, and across every group when none are. A group holding none of mine SHALL be left collapsed, so the section is not padded with headings that would show nothing. When no group holds an extra of mine at all, every group SHALL stay collapsed.

**Activating the control while it reads as on SHALL turn the filter off**, so every expanded group widens to show every extra it holds. It SHALL change no group's collapsed state in that direction: a collapsed group stays collapsed, and nothing the user had opened is put away. The control is therefore a two-way toggle over one fact — whether the filter is in force — and never reads as on while the section is showing extras that are not mine.

The control SHALL report which of those two states the filter is in: it SHALL read as on exactly while the filter is in force and no group is exempt from it, per "A More group's heading opens that group in full". A freshly opened series page SHALL therefore read as off because the filter is off, not because nothing is expanded.

Putting the section away is the expand/collapse-all control's job, not the "in my list" control's.

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

#### Scenario: Opening a series shows the headings alone with nothing narrowed
- **WHEN** I open a series with twenty extras across five relation groups, four of the extras being in my list
- **THEN** all five groups are collapsed with their counts in their headings, no tile is rendered, the all-groups control reads "Expand all", no media type is selected, and the "in my list" control reads as off because the filter is off

#### Scenario: Pressing "in my list" shows my extras
- **WHEN** I then activate the "in my list" control
- **THEN** every group holding at least one of those four extras opens showing exactly those, the groups holding none of mine stay collapsed, and the control reads as on

#### Scenario: Pressing it again turns the filter off
- **WHEN** the section is showing my extras and I activate the "in my list" control again
- **THEN** the filter is off, every group that was open now shows every extra it holds, every group that was collapsed is still collapsed, and the control reads as off

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
- **THEN** both open with every group collapsed and the filter off, rather than one of them starting expanded or filtered

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

### Requirement: The More section offers media-type filter buttons
Above the More section the page SHALL offer one button per media type present among that series' extras and related entries — TV, Movie, OVA, ONA, Special, Music, PV, and any other type those entries carry — as a multi-select set.

Selecting a type SHALL narrow every group to the entries of that type. Selecting several SHALL show the entries of any selected type. Selecting none SHALL narrow nothing, which is the state a freshly opened page is in.

**Selecting a type SHALL open every group that holds at least one entry of that type**, so the entries it admits are actually rendered rather than merely counted in a heading. Because the section opens with every group collapsed, a type filter that only narrowed the groups would tell the reader which relation group holds a music entry without ever showing the entry itself. A group opened this way SHALL be opened exactly as the expand/collapse-all control opens one: its collapsed state becomes expanded and stays that way until something collapses it. It SHALL NOT be exempted from the "in my list" filter — only a group heading grants that exemption. Deselecting a type SHALL NOT collapse anything.

**Selecting or deselecting a type SHALL NOT change the "in my list" filter, nor what its control reads.** Opening groups is the only effect a type button has beyond narrowing. A user who selects "Music" on a freshly opened page — where the filter is off — SHALL therefore see every music entry the series holds, mine and not mine alike, with the "in my list" control still reading as off.

**While the "in my list" filter is on, a media type that no extra of mine carries SHALL NOT be selectable**: its button SHALL be offered in a disabled state, so the section cannot be narrowed to a combination that holds nothing. Such a button SHALL make the reason available to assistive technology rather than only greying out. Turning the filter on SHALL drop any already-selected type that no extra of mine carries; when that leaves no type selected, no type restriction applies and the section shows my extras across every group — which is what the user asked for by pressing "in my list" last. A dropped selection SHALL NOT be restored when the filter is later turned off; every type present among the series' extras SHALL simply become selectable again.

The type filter SHALL compose with the "in my list" filter and with each group's collapsed or opened state rather than replacing them: an entry is shown when its type is admitted **and** the other controls admit it. Each group's heading count SHALL report the entries the type filter admits.

While at least one type is selected, a group left with no admitted entries SHALL NOT be rendered at all, since a column of empty headings across a dozen relation groups tells the reader nothing.

The type buttons SHALL NOT narrow the main-line timeline. The main line is a watch order whose left-to-right sequence is its meaning, and hiding one of its entries would misstate the series.

#### Scenario: One type narrows every group
- **WHEN** I select "Movie"
- **THEN** every group holding a movie is opened and shows its movies, and groups holding no movie are not rendered

#### Scenario: Selecting a type opens the collapsed groups holding it
- **WHEN** every group is collapsed, as on a freshly opened series page, and I select "Music"
- **THEN** every group holding a music entry is expanded and renders the music entries the other controls admit, rather than showing its heading alone

#### Scenario: Selecting a type does not turn the "in my list" filter on
- **WHEN** I open a series page and select "Music"
- **THEN** every music entry in the series is shown, whether or not it is in my list, and the "in my list" control still reads as off

#### Scenario: Selecting my list after a type narrows to that type of mine
- **WHEN** I have "Music" selected and I then activate the "in my list" control
- **THEN** the groups holding a music entry of mine show only those, and the control reads as on

#### Scenario: A type I hold nothing of is dropped rather than showing nothing
- **WHEN** I have "Music" selected on a series in which none of my own extras is a music entry, and I activate the "in my list" control
- **THEN** the "Music" selection is dropped, the section shows my extras across every group with no type restriction, and the "Music" button is offered disabled while the filter is on

#### Scenario: An unavailable type cannot be selected while the filter is on
- **WHEN** the "in my list" filter is on and the series holds music entries but none of mine is one
- **THEN** the "Music" button is disabled and selecting it does nothing

#### Scenario: A partly unavailable selection keeps the types I hold
- **WHEN** I have "Movie" and "Music" selected, I own a movie among the extras but no music entry, and I activate the "in my list" control
- **THEN** "Movie" stays selected and the section shows my movies, while "Music" is dropped and offered disabled

#### Scenario: Turning the filter off makes every present type selectable again
- **WHEN** a type has been dropped because I hold nothing of it and I then turn the "in my list" filter off
- **THEN** that type's button is selectable again, unselected, and selecting it shows every entry of that type

#### Scenario: Several types are additive
- **WHEN** I select "Movie" and then "OVA"
- **THEN** every group shows its movies and its OVAs, and the groups holding an OVA are opened as well

#### Scenario: Deselecting the last type restores everything
- **WHEN** I deselect the only selected type
- **THEN** every group shows what the other controls admit, and the groups the type filter opened stay open

#### Scenario: The timeline is untouched
- **WHEN** I select "Movie" on a series whose main line is four TV seasons
- **THEN** the timeline still shows all four, in watch order

#### Scenario: Only present types are offered
- **WHEN** a series' extras and related entries hold no music entry
- **THEN** no "Music" button is offered

#### Scenario: The type filter composes with the list filter
- **WHEN** the "in my list" filter is on and I select "OVA", of which I hold at least one
- **THEN** the groups holding OVAs are opened and show only the OVAs that are in my list, each offering its hidden-count control if it hides others

#### Scenario: Counts follow the type filter
- **WHEN** a group holds six entries of which two are movies and I select "Movie"
- **THEN** that group's heading reports two

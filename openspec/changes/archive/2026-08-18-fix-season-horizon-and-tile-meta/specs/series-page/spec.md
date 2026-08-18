## MODIFIED Requirements

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by media type, so the extras read as a different kind of thing from the chronological main line.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text.

Each More group SHALL show its entry count in its heading. When a series has more than twelve extras every group SHALL start collapsed, and otherwise every group SHALL start expanded.

Each More group SHALL be collapsible, and the section SHALL offer one control that expands or collapses every group at once — unless every extra in the series is already marked Completed in my list, in which case no group offers a collapse/expand control, every group SHALL always render fully expanded, and the one-control affordance SHALL NOT be offered at all, since there is nothing left worth hiding.

That one control, when offered, SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and SHALL read "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

A collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them as poster tiles under a collapsible group per media type, each heading carrying its count

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are too wide for the tile at the narrowest column width the grid produces
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: A large More section starts collapsed
- **WHEN** I open a series with twenty extras
- **THEN** every More group starts collapsed, no tiles are rendered, and one control expands them all

#### Scenario: A small More section starts open
- **WHEN** I open a series with four extras
- **THEN** their groups start expanded

#### Scenario: Editing from a tile
- **WHEN** I use a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the tile and the series stats reflect the new score without a page reload

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

#### Scenario: Collapse control suppressed once everything is watched
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** every More group renders fully expanded with no per-group toggle and no all-groups control

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one media-type group and at least one is not Completed
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more media-type groups and at least one extra is not Completed
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

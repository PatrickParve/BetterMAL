## ADDED Requirements

### Requirement: The top three are presented as a podium
The recap SHALL present the three highest-ranked anime of its top 10 as a podium of cards rather than as rows, so the best of a period is identifiable at a glance rather than only by reading its numeral.

The podium SHALL be laid out as a podium: the first-ranked card SHALL sit between the other two and stand taller than them, and the second- and third-ranked cards SHALL sit either side of it with their lower edges aligned to its own. Each card SHALL carry, at minimum:

- its rank, in a badge coloured for that rank — gold for first, silver for second, bronze for third — the three colours being visually distinct from one another and from the app's accent colour in both the light and the dark theme;
- the anime's poster art at a size that reads as artwork rather than as a thumbnail, with a placeholder of the same size when the anime has no picture;
- the anime's title, up to two lines, with the full title available on hover when it is truncated;
- its score on the currently selected ranking basis, in the app's existing score language for that basis.

The whole card SHALL be one link to the anime's page, and following it SHALL open the same page the equivalent row would have.

The three cards SHALL appear in rank order in the page's reading and keyboard order, first-ranked first, even though the first-ranked card is placed centrally. Where the display is too narrow for three cards side by side, the podium SHALL stack into a single column in rank order, each card at the full column width.

The podium SHALL show only as many cards as the period has entries: two entries SHALL produce two cards and one entry a single card, with no placeholder or empty card standing in for the missing rank.

#### Scenario: The best of a period stands out
- **WHEN** a recap with ten or more entries is shown
- **THEN** its top three appear as cards, with the first-ranked card centred and raised above the second- and third-ranked cards on either side of it

#### Scenario: Rank is colour-coded
- **WHEN** I look at the podium
- **THEN** the first, second, and third cards carry gold, silver, and bronze rank badges respectively, each legible in both the light and the dark theme

#### Scenario: A card opens its anime
- **WHEN** I follow any podium card
- **THEN** that anime's page opens, exactly as following its row would have

#### Scenario: Reading order follows rank
- **WHEN** I move through the podium by keyboard or with a screen reader
- **THEN** I reach the first-ranked card first, then the second, then the third, regardless of where each sits on screen

#### Scenario: Narrow displays stack the podium
- **WHEN** the display is too narrow for three cards side by side
- **THEN** the three cards stack in one column in rank order, first-ranked at the top

#### Scenario: A period with two entries
- **WHEN** a recap's included set holds only two anime
- **THEN** two cards are shown and no empty third card appears

#### Scenario: A long title does not change the card
- **WHEN** one card's title needs two lines and another's needs one
- **THEN** both cards are the same height, and the truncated title is available in full on hover

### Requirement: The podium is animated
The podium SHALL be animated: its cards SHALL arrive with motion when a period's results are shown rather than appearing statically, and SHALL respond to the pointer with motion as well as with colour. The first-ranked card SHALL carry a continuous decorative motion that distinguishes it from the other two.

No animation SHALL move, resize, or reflow any other element on the page, and none SHALL be required in order to read, follow, or operate the podium — a card is fully usable at every point of every animation.

The podium SHALL re-animate its arrival whenever the set it is showing genuinely changes — a new period, time filter, ranking basis, or media-type narrowing — and SHALL NOT re-animate on a re-render that leaves the same three anime in the same order.

Where the user has asked their system for reduced motion, every one of these animations SHALL be switched off — the arrival, the pointer response, and the decorative motion alike — leaving the podium fully legible and fully operable, with the colour part of the pointer response still shown.

#### Scenario: The podium arrives with motion
- **WHEN** a recap's results are shown
- **THEN** the three cards animate into place rather than appearing instantly, and the page around them does not shift as they do

#### Scenario: A card responds to the pointer
- **WHEN** I move the pointer over a podium card
- **THEN** it responds with motion as well as with a colour change, and nothing else on the page moves

#### Scenario: First place is distinguished
- **WHEN** the podium is shown
- **THEN** the first-ranked card carries a continuous decorative motion the other two do not

#### Scenario: Re-animating on a new set
- **WHEN** I change the period, the time filter, the ranking basis, or the media-type narrowing
- **THEN** the podium animates into place again for the new set

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** the podium shows no arrival animation, no decorative motion, and no motion on hover, and remains fully legible and followable with its colour hover feedback intact

### Requirement: The recap's segmented controls show every state
Every segmented control on the recap page — the recap-type tabs, the time filter, and the ranking-basis toggle — SHALL make each of its states visually distinct from every other:

- **unselected at rest**, and **unselected under the pointer**, which SHALL differ from it;
- **selected**, which SHALL be unmistakable as the current choice next to its unselected siblings;
- **selected under the pointer**, which SHALL differ visibly from selected at rest, so moving the pointer onto the option that is already chosen still gives feedback rather than appearing inert;
- **focused by keyboard**, which SHALL show a visible focus indicator distinct from the hover treatment;
- **disabled**, which SHALL read as unavailable and SHALL show neither the hover nor the focus treatment.

All three groups SHALL use the same state treatments as one another, so a selected time filter and a selected recap-type tab look alike.

None of these states SHALL change the control's height or width, so the cluster a control sits in SHALL NOT reflow as the pointer moves across it, and the controls SHALL remain the same height as the selects and other controls beside them.

#### Scenario: Hovering the already-selected option
- **WHEN** I move the pointer over the recap-type tab that is already selected
- **THEN** it changes visibly from its resting selected appearance

#### Scenario: Hovering an unselected option
- **WHEN** I move the pointer over an unselected option
- **THEN** it changes visibly from its resting appearance, and still reads as unselected next to the selected one

#### Scenario: Keyboard focus is visible
- **WHEN** I reach one of these controls with the keyboard
- **THEN** a visible focus indicator is shown, distinct from the hover treatment

#### Scenario: A disabled filter reads as unavailable
- **WHEN** a time filter option is unavailable because the period holds nothing for it and I move the pointer over it
- **THEN** it reads as unavailable and takes neither the hover nor the focus treatment

#### Scenario: The control cluster does not reflow
- **WHEN** I move the pointer along a row of these controls
- **THEN** none of them changes size and nothing beside them moves

#### Scenario: The groups agree with one another
- **WHEN** a recap shows both its type tabs and its time filter
- **THEN** the selected option in each looks the same as the selected option in the other

## MODIFIED Requirements

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis, with ties broken by title case-insensitively so the order is stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero. When the included set holds fewer than ten entries, the recap SHALL show all of them.

The ten SHALL be presented in two forms: the first three as the podium described in "The top three are presented as a podium", and the remaining ranks as rows below it. The split SHALL be presentational only — the same ten anime in the same order are shown either way — and the rows SHALL continue the podium's numbering rather than restarting, both in the rank each row shows and in the list semantics exposed to assistive technology.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: The ten split into a podium and rows
- **WHEN** a recap shows ten anime
- **THEN** the first three are shown as podium cards and ranks four through ten as rows beneath them, numbered four through ten

#### Scenario: Three or fewer entries
- **WHEN** the included set holds three or fewer anime
- **THEN** they are all shown on the podium and no row list is shown

#### Scenario: Stable tie order
- **WHEN** several anime share the same score at the cut line
- **THEN** they are ordered by title, and the same order is shown on every reload

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

### Requirement: Recap rows highlight on hover
Every row of the recap page that links to an anime, a season, or a year — the top-10 rows below the podium, the hot-take rows, and the rows of all four rankings, inline and in the "See all" overlay alike — SHALL adopt the app's standard accent hover treatment: the same tinted background, accent border, and elevation that the anime cards on my list, the season page, and the top-anime page already use, applied on both pointer hover and keyboard focus.

The treatment SHALL be the same one those pages use rather than a recap-specific variant, so a hovered row reads identically wherever it appears. It SHALL NOT change a row's size or position.

The podium cards are not rows and are covered by "The podium is animated" instead; their richer treatment SHALL NOT be applied to any row.

#### Scenario: A hovered top-10 row
- **WHEN** I move the pointer over a row of the recap's top 10
- **THEN** that row takes the same accent highlight an anime card takes on my list

#### Scenario: A hovered hot take
- **WHEN** I move the pointer over a hot-take row
- **THEN** that row takes the same accent highlight

#### Scenario: A hovered ranking row
- **WHEN** I move the pointer over a row of any of the recap's rankings, or a row of a "See all" overlay
- **THEN** that row takes the same accent highlight

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach one of these rows by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Highlighting does not reflow the list
- **WHEN** I move the pointer down a list of rows
- **THEN** no row changes size or position as it gains or loses the highlight

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT cause any layout reflow: no tile's box, and no tile's place in the grid, SHALL change as the pointer crosses the block, so tiles never shift one another around. A tile MAY lift or otherwise move within its own box as part of the feedback, provided nothing else on the page moves with it.

A tile that describes a set of anime, per the "Drilling into a recap stat" requirement, SHALL be distinguishable from an aggregate tile that never does, **at rest as well as on hover**: each tile SHALL carry a persistent visual marker of which kind it is, so the distinction can be seen without moving the pointer. On hover, the former SHALL take the app's standard accent hover treatment — the same tinted background, accent border, and elevation its cards and rows use — while the aggregate tiles SHALL take a quieter feedback. Within the tiles that describe a set, only the ones with a target — a non-zero count — SHALL show the pointer cursor and be reachable and followable by keyboard, showing the same treatment on keyboard focus as on hover; a zero-count tile takes the same accent highlight but SHALL NOT show the pointer cursor and is not a keyboard stop, since it has nowhere to lead. Hovering SHALL therefore not promise a destination that does not exist.

Every tile SHALL keep the same box — the same padding, border, and size — so the block reads as one grid. Each tile's figure SHALL be set in tabular figures so the numbers of two tiles side by side share one rhythm, and its label SHALL be visually subordinate to its figure.

#### Scenario: The stat block is labelled
- **WHEN** a recap renders its stats
- **THEN** the block carries a heading naming it, styled as the page's other section headings are

#### Scenario: A hovered tile is identifiable
- **WHEN** I move the pointer over one stat tile
- **THEN** that tile is visibly distinguished from the tiles around it

#### Scenario: Hover does not reflow the block
- **WHEN** I move the pointer across the stat tiles
- **THEN** no tile other than the hovered one changes in any way, and none of them changes the space it occupies in the grid

#### Scenario: Followable and aggregate tiles differ at rest
- **WHEN** I look at the stat block without moving the pointer
- **THEN** I can tell which tiles lead somewhere and which are aggregates from a persistent marker on each

#### Scenario: A followable tile reads as clickable
- **WHEN** I move the pointer over **Completed**
- **THEN** it takes the accent highlight the app's cards take and the cursor shows it can be followed

#### Scenario: An aggregate tile does not read as clickable
- **WHEN** I move the pointer over **Time spent**
- **THEN** it gives quieter feedback than the followable tiles and the cursor does not mark it as clickable

#### Scenario: Figures line up
- **WHEN** two stat tiles sit side by side
- **THEN** their figures are set in tabular figures so the digits share one rhythm

#### Scenario: Keyboard reaches the followable tiles
- **WHEN** I move through the stat block by keyboard
- **THEN** each followable tile can be focused and followed, showing the same highlight it shows on hover

### Requirement: Season recap links to its season page
A season recap SHALL offer a control that opens the season browser on that same season and year, so the anime that aired that season — not only the ones in my list — are one step away. The control SHALL NOT be offered on a yearly or multi-year recap, whose period is not a single season.

The control SHALL be presented as a button rather than as inline text: it SHALL be the same height as the period controls it sits beside, and SHALL take the same hover and keyboard-focus treatment the app's other controls take. It SHALL remain a real link — openable in a new tab or window by the means the browser normally offers for links — despite being presented as a button.

#### Scenario: Opening the season page from a recap
- **WHEN** a fall 2019 season recap is shown and I follow its season-page control
- **THEN** the season browser opens on fall 2019

#### Scenario: The control reads as a button
- **WHEN** a season recap is shown
- **THEN** its season-page control is presented as a button matching the height of the controls beside it, and highlights on hover and on keyboard focus as they do

#### Scenario: The control is still a link
- **WHEN** I open the season-page control in a new tab the way I would any link
- **THEN** the season browser opens in a new tab on that season and year

#### Scenario: Not offered outside season mode
- **WHEN** a yearly or multi-year recap is shown
- **THEN** no season-page control is offered

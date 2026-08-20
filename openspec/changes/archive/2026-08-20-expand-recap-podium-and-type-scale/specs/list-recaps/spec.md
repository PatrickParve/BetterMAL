## RENAMED Requirements

- FROM: `### Requirement: The top three are presented as a podium`
- TO: `### Requirement: The top five are presented as a podium`

## MODIFIED Requirements

### Requirement: The top five are presented as a podium
The recap SHALL present the five highest-ranked anime of its top 10 as a podium of cards rather than as rows, so the best of a period is identifiable at a glance rather than only by reading its numeral.

The podium SHALL be laid out as a descending row: the cards SHALL sit side by side in rank order, the first-ranked card the largest and leading the row, each following card no larger than the one before it, and the fourth- and fifth-ranked cards SHALL be visibly smaller than the third. Every card's lower edge SHALL sit on one line, so the row's silhouette steps down from first to fifth. The podium SHALL occupy the full width available to the top-10 section rather than being capped short of it, so the section holds no empty band beside the cards.

Each card SHALL carry, at minimum:

- its rank, in a badge coloured for that rank — gold for first, silver for second, bronze for third, and for fourth and fifth a neutral badge visibly subordinate to the three medals and distinct from all of them, in both the light and the dark theme;
- the anime's poster art at a size that reads as artwork rather than as a thumbnail, with a placeholder of the same size when the anime has no picture;
- the anime's title, up to two lines, with the full title available on hover when it is truncated;
- its score on the currently selected ranking basis, in the app's existing score language for that basis.

Every card's score SHALL sit in a box of one shared size — the same box on the fifth card's smaller card as on the first card's larger one — wide enough for the widest value either ranking basis can print, which is a two-digit score under my scores and a two-decimal figure under MAL's, and set in tabular figures so the five scores read as a column.

The whole card SHALL be one link to the anime's page, and following it SHALL open the same page the equivalent row would have.

The five cards SHALL appear in rank order in the page's reading and keyboard order, first-ranked first. Where the display is too narrow for five cards side by side, the podium SHALL reflow onto fewer cards per line — and at the narrowest into a single column, each card at the full width of the column — always in rank order, filling each line before starting the next. Once the cards no longer share one line the descending sizes MAY be dropped, since cards on different lines cannot be compared by size.

The podium SHALL show only as many cards as the period has entries: two entries SHALL produce two cards and one entry a single card, with no placeholder or empty card standing in for a missing rank.

#### Scenario: The best of a period stands out
- **WHEN** a recap with ten or more entries is shown
- **THEN** its top five appear as cards in one descending row, the first-ranked card largest and leading, the fourth and fifth smaller than the third, and every card's lower edge on one line

#### Scenario: The podium fills its section
- **WHEN** the top-10 section is wider than the podium's cards need
- **THEN** the five cards spread to fill that width rather than leaving an empty band beside them

#### Scenario: Rank is colour-coded
- **WHEN** I look at the podium
- **THEN** the first, second, and third cards carry gold, silver, and bronze rank badges, the fourth and fifth carry a neutral badge subordinate to those three, and each is legible in both the light and the dark theme

#### Scenario: Every card's score box is the same size
- **WHEN** the podium shows five cards of different widths
- **THEN** the five score boxes are identical in size and aligned as a column, and a score of `10` fits its box without wrapping or clipping

#### Scenario: A MAL score fits the same box
- **WHEN** I switch the ranking basis to MAL's scores
- **THEN** each card's two-decimal MAL figure fits the same box the my-score figures used, with no card's box resizing

#### Scenario: A card opens its anime
- **WHEN** I follow any podium card
- **THEN** that anime's page opens, exactly as following its row would have

#### Scenario: Reading order follows rank
- **WHEN** I move through the podium by keyboard or with a screen reader
- **THEN** I reach the first-ranked card first, then the second, and so on to the fifth

#### Scenario: Narrow displays reflow the podium
- **WHEN** the display is too narrow for five cards side by side
- **THEN** the cards reflow onto fewer per line in rank order, filling each line before the next

#### Scenario: The narrowest display stacks the podium
- **WHEN** the display is too narrow for even two cards side by side
- **THEN** the cards stack in one column in rank order, first-ranked at the top, each at the full width of the column

#### Scenario: A period with two entries
- **WHEN** a recap's included set holds only two anime
- **THEN** two cards are shown and no empty third, fourth, or fifth card appears

#### Scenario: A long title does not change the card
- **WHEN** one card's title needs two lines and another's needs one
- **THEN** both cards are the same height, and the truncated title is available in full on hover

### Requirement: The podium is animated
The podium SHALL be animated: its cards SHALL arrive with motion when a period's results are shown rather than appearing statically, and SHALL respond to the pointer with motion as well as with colour. The first-ranked card SHALL carry a continuous decorative motion that distinguishes it from the others.

That decorative motion SHALL be a slow drift of light across the card's surface, and SHALL satisfy all of:

- **Whole-card, not a band.** It SHALL travel across the card's whole surface rather than being confined to one edge or strip of it.
- **Continuous.** There SHALL be no part of its cycle in which the card carries no visible motion — the effect SHALL NOT sweep off the card and leave it inert until the next pass.
- **Slow.** Its pace SHALL be markedly slower than the card's pointer response, reading as an ambient drift rather than as a repeated sweep.
- **The poster is excluded.** The anime's poster art SHALL NOT be tinted, lit, or animated by it; the motion passes around the artwork, not over it.
- **The score moves with the card.** The card's score box SHALL be lit by the same pass, at the same moment and in the same way as the card surface behind it, rather than sitting inert on top of the motion.

No animation SHALL move, resize, or reflow any other element on the page, and none SHALL be required in order to read, follow, or operate the podium — a card is fully usable at every point of every animation.

The podium SHALL re-animate its arrival whenever the set it is showing genuinely changes — a new period, time filter, ranking basis, or media-type narrowing — and SHALL NOT re-animate on a re-render that leaves the same anime in the same order.

Where the user has asked their system for reduced motion, every one of these animations SHALL be switched off — the arrival, the pointer response, and the decorative motion alike — leaving the podium fully legible and fully operable, with the colour part of the pointer response still shown.

#### Scenario: The podium arrives with motion
- **WHEN** a recap's results are shown
- **THEN** the cards animate into place rather than appearing instantly, and the page around them does not shift as they do

#### Scenario: A card responds to the pointer
- **WHEN** I move the pointer over a podium card
- **THEN** it responds with motion as well as with a colour change, and nothing else on the page moves

#### Scenario: First place is distinguished
- **WHEN** the podium is shown
- **THEN** the first-ranked card carries a continuous decorative motion the other cards do not

#### Scenario: The motion covers the card
- **WHEN** I watch the first-ranked card
- **THEN** the motion travels across the card's whole surface rather than only across a band at one edge

#### Scenario: The motion never stops
- **WHEN** I watch the first-ranked card through several full cycles
- **THEN** at no point does the card sit still waiting for the next pass

#### Scenario: The score lights with the card
- **WHEN** the motion passes the first-ranked card's score
- **THEN** the score box lights in the same pass and the same way as the card surface behind it

#### Scenario: The poster is left alone
- **WHEN** the motion passes over the first-ranked card
- **THEN** the anime's poster art is not tinted, lit, or moved by it

#### Scenario: Re-animating on a new set
- **WHEN** I change the period, the time filter, the ranking basis, or the media-type narrowing
- **THEN** the podium animates into place again for the new set

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** the podium shows no arrival animation, no decorative motion, and no motion on hover, and remains fully legible and followable with its colour hover feedback intact

### Requirement: Top 10 of the period
Every recap SHALL show the ten highest-ranked anime of the included set, ranked by the selected ranking basis, with ties broken by title case-insensitively so the order is stable across reloads. Entries with no score on the selected basis SHALL be ranked below every scored entry rather than treated as zero. When the included set holds fewer than ten entries, the recap SHALL show all of them.

The ten SHALL be presented in two forms: the first five as the podium described in "The top five are presented as a podium", and the remaining ranks as rows below it. The split SHALL be presentational only — the same ten anime in the same order are shown either way — and the rows SHALL continue the podium's numbering rather than restarting, both in the rank each row shows and in the list semantics exposed to assistive technology.

#### Scenario: Ten of many
- **WHEN** the included set holds 40 anime
- **THEN** the ten highest-ranked are shown in descending order

#### Scenario: Fewer than ten
- **WHEN** the included set holds six anime
- **THEN** all six are shown

#### Scenario: The ten split into a podium and rows
- **WHEN** a recap shows ten anime
- **THEN** the first five are shown as podium cards and ranks six through ten as rows beneath them, numbered six through ten

#### Scenario: Five or fewer entries
- **WHEN** the included set holds five or fewer anime
- **THEN** they are all shown on the podium and no row list is shown

#### Scenario: Stable tie order
- **WHEN** several anime share the same score at the cut line
- **THEN** they are ordered by title, and the same order is shown on every reload

#### Scenario: Unscored entries rank last
- **WHEN** the included set holds both scored and unscored anime on the selected basis
- **THEN** every scored anime is ranked above every unscored one

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT cause any layout reflow: no tile's box, and no tile's place in the grid, SHALL change as the pointer crosses the block, so tiles never shift one another around. A tile MAY lift or otherwise move within its own box as part of the feedback, provided nothing else on the page moves with it.

A tile that leads somewhere, per the "Drilling into a recap stat" requirement, SHALL be distinguishable from one that does not, **at rest as well as on hover**: each tile SHALL carry a persistent visual marker of which kind it is, so the distinction can be seen without moving the pointer. On hover, a tile that leads somewhere SHALL take the app's standard accent hover treatment — the same tinted background, accent border, and elevation its cards and rows use — show the pointer cursor, and be reachable and followable by keyboard, showing the same treatment on keyboard focus as on hover. Every other tile — an aggregate tile that never leads anywhere, and a followable stat whose own count is zero and so has nothing to lead to — SHALL take the quieter resting marker and the quieter hover feedback, SHALL NOT show the pointer cursor, and SHALL NOT be a keyboard stop. Hovering SHALL therefore never promise a destination that does not exist.

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

#### Scenario: Tiles that lead somewhere differ at rest
- **WHEN** I look at the stat block without moving the pointer
- **THEN** I can tell which tiles lead somewhere and which do not from a persistent marker on each

#### Scenario: A followable tile reads as clickable
- **WHEN** I move the pointer over **Completed** on a period with completed anime in it
- **THEN** it takes the accent highlight the app's cards take and the cursor shows it can be followed

#### Scenario: An aggregate tile does not read as clickable
- **WHEN** I move the pointer over **Time spent**
- **THEN** it gives quieter feedback than the followable tiles and the cursor does not mark it as clickable

#### Scenario: Figures line up
- **WHEN** two stat tiles sit side by side
- **THEN** their figures are set in tabular figures so the digits share one rhythm

#### Scenario: Keyboard reaches the followable tiles
- **WHEN** I move through the stat block by keyboard
- **THEN** each tile that leads somewhere can be focused and followed, showing the same highlight it shows on hover, and no other tile is a stop

### Requirement: Drilling into a recap stat
Each stat that describes a set of anime SHALL be followable, opening my list showing exactly the anime that stat counts. The followable stats are **In this period**, **Completed**, **Dropped**, **Movies watched**, and **Currently watching**. **Mean score**, **Episodes watched**, and **Time spent** describe an aggregate rather than a set and SHALL NOT be followable.

Following a stat SHALL open my list scoped to the same period, time filter, and media type the recap is showing, narrowed further to the stat's own rule:

- **In this period** — the whole included set, exactly as the recap's "See all in my list" control already opens it.
- **Completed** — the included entries whose status is Completed.
- **Dropped** — the included entries whose status is Dropped.
- **Movies watched** — the included entries whose media type is `movie` and which have at least one episode watched, whatever their status, matching what the tile counts.
- **Currently watching** — the entries with status Watching whose anime's air-start date falls inside the recap's period. Because that stat is counted on air date under **both** time filters, its target SHALL carry the period's aired-attributed scope even when the recap is showing **What I watched**, so the list and the tile describe the same set.

The number the tile shows and the number of anime my list then holds SHALL agree.

The narrowing SHALL arrive applied to my list's own filter controls rather than as a second hidden scope, so it is visible on the page and can be changed or cleared there. A stat whose narrowing leaves a control at its default SHALL leave that control alone.

A followable stat whose own count is zero SHALL NOT offer a target — there is nothing for it to lead to — and SHALL be presented exactly as an aggregate tile is: the same resting marker and the same quieter hover feedback, with no pointer cursor and no keyboard stop. A tile with the accent marker therefore always leads somewhere.

#### Scenario: Movies of a year
- **WHEN** I follow **Movies watched** on a 2020 recap under **What aired**
- **THEN** my list opens scoped to 2020 showing exactly the films of that year I have watched at least one episode of, and its row count matches the number the tile showed

#### Scenario: What I dropped
- **WHEN** I follow **Dropped** on a recap
- **THEN** my list opens scoped to that same period, filter, and media type, showing only its dropped entries

#### Scenario: The period total
- **WHEN** I follow **In this period**
- **THEN** my list opens showing the recap's whole included set, with no further narrowing applied

#### Scenario: Currently watching keeps its air-date basis
- **WHEN** a 2024 recap is set to **What I watched** and I follow **Currently watching**
- **THEN** my list opens on the anime that started airing in 2024 which I am currently watching — the same set the tile counted — rather than on my 2024 watch history

#### Scenario: The narrowing is visible on my list
- **WHEN** I arrive on my list by following **Completed**
- **THEN** my list's own status control shows Completed, and I can change or clear it there

#### Scenario: Aggregates are not followable
- **WHEN** I move the pointer over **Mean score**, **Episodes watched**, or **Time spent**
- **THEN** no target is offered for them

#### Scenario: A zero-count stat reads as an aggregate tile
- **WHEN** a recap has nothing dropped this period and I look at and then hover **Dropped**
- **THEN** it carries the same resting marker and the same quieter hover feedback **Time spent** carries, no target is offered, the cursor does not mark it as clickable, and it is not a keyboard stop

#### Scenario: The recap's media-type narrowing carries through
- **WHEN** a recap narrowed to films is showing and I follow one of its followable stats
- **THEN** my list opens with that same media-type narrowing in force alongside the stat's own

### Requirement: The score board is dismissible like the recap's other overlays
The score board SHALL be dismissible by the Escape key, by clicking outside it, and by an explicit close control, in the same way the recap's "see all" ranking overlay is. It SHALL be scrollable within itself when it holds more than fits, leaving the recap behind it in place.

The board SHALL read as a plain, ordinary list: no slot's header bar SHALL pin, stick, or otherwise hold its position as the board scrolls past it — every element of the board, slot headers and poster tiles alike, SHALL move with the scroll exactly as any other block of content would. The only motion in the board that is not the ordinary scroll is a tile's own hover/focus lift, per "Board posters answer to the pointer and the keyboard", and the 10 slot's decorative motion, per "The 10 slot shares the podium's first-place motion".

Closing the board SHALL return the recap exactly as it was left, at the same scroll position.

#### Scenario: Closing with the keyboard
- **WHEN** I press Escape with the board open
- **THEN** the board closes and the recap is where I left it

#### Scenario: Closing by clicking away
- **WHEN** I click outside the board
- **THEN** it closes, as the ranking overlay does

#### Scenario: A period with hundreds of scored anime
- **WHEN** the board holds more than fits on screen
- **THEN** it scrolls within itself and the recap behind it does not move

#### Scenario: No slot header pins while scrolling
- **WHEN** I scroll through a slot that holds more anime than fits on screen
- **THEN** that slot's own header bar scrolls away with its posters like any other content, rather than staying fixed in place

## ADDED Requirements

### Requirement: The recap's supporting text sits on one legible scale
The recap's supporting text SHALL be set large enough to read as part of the page beside its artwork rather than as fine print beneath it. This SHALL apply to the top-10 section — both the podium cards' titles and the rank, title, and score of every row beneath them — the stat tiles' figures and labels, the rating distribution's rows, the rows of all four season and year rankings, and the hot-take rows.

Raising the scale SHALL preserve the existing hierarchy rather than flatten it: a stat tile's label SHALL remain visually subordinate to its figure, and a ranking row's meta text SHALL remain subordinate to its label.

Raising the scale SHALL NOT break any layout the smaller text fitted: no text SHALL be clipped or overflow its box, the stat block SHALL keep its grid in the lead column beside the top 10, every rating-distribution row's bar track SHALL still start and end on one line with the others, and ranking rows SHALL still share one height per the "Ranking rows share a single height" requirement.

#### Scenario: The top 10 reads at page weight
- **WHEN** I look at the top-10 section
- **THEN** the podium cards' titles and the rows' rank, title, and score are set larger than the page's smallest supporting text and read comfortably beside the poster art

#### Scenario: Stats, distribution, rankings, and hot takes follow
- **WHEN** I look at the stat block, the rating distribution, the season and year rankings, and the hot takes
- **THEN** each is set on the same raised scale as the top 10 rather than at its previous smaller size

#### Scenario: Hierarchy survives the increase
- **WHEN** I look at a stat tile and a ranking row
- **THEN** the tile's label is still visibly subordinate to its figure, and the row's meta text still subordinate to its label

#### Scenario: Nothing overflows at the larger size
- **WHEN** the recap is shown at the width where the stat block sits beside the top 10, and again where it goes full width
- **THEN** no figure, label, title, or meta text is clipped or spills out of its box, and the distribution's bars all start and end on one line

### Requirement: Board posters are drawn near their source resolution
Each poster tile on the score board SHALL be large enough that its poster is drawn near the source art's own pixel dimensions rather than at a steep reduction of them, which is what makes a poster read soft. On a display with a device pixel ratio of 2, a tile SHALL be drawn at no less than two thirds of the source art's pixel dimensions, and never larger than them.

Each tile's box SHALL follow the proportion of the poster art itself rather than a taller one, so a poster fills its tile without part of the artwork being cropped away to fit.

Nothing else about the board SHALL change: the same anime appear in the same slots in the same order, and every poster keeps the lift, the card, the link, and the keyboard behaviour required by "Board posters answer to the pointer and the keyboard".

#### Scenario: A poster reads sharply on a high-density display
- **WHEN** I open the score board on a display with a device pixel ratio of 2
- **THEN** each poster is drawn at no less than two thirds of its source art's pixel dimensions — a gentle reduction rather than the steep one that softened it — and never at more than them

#### Scenario: Posters are not cropped to a taller box
- **WHEN** I look at a poster on the board
- **THEN** it fills its tile at the proportion of the artwork, with no visible slice cut from its sides

#### Scenario: The board still fits its slots
- **WHEN** a slot holds many anime at the larger tile size
- **THEN** the grid still fills the overlay's width, wrapping onto further lines, and the overlay never scrolls sideways

#### Scenario: The board's behaviour is unchanged
- **WHEN** I hover, focus, and follow a poster at the new size
- **THEN** it lifts, shows its card, and opens the anime exactly as before, and its slot's count is unchanged

### Requirement: The 10 slot shares the podium's first-place motion
The score board's 10 slot SHALL carry the same continuous decorative motion as the podium's first-ranked card, per "The podium is animated": a slow, continuous drift of light across the slot's own header bar, travelling across its whole surface rather than one edge or band of it, and present at every point of its cycle rather than sweeping off and leaving the bar inert until the next pass.

The motion's highlight SHALL be coloured so that it can never read as a different tier's colour — including silver, the colour shared by the 6 and 5 slots — no matter how the motion's layers combine at any point in its cycle. In both the light and the dark theme, the 10 slot's score numeral and count SHALL remain legible at every point of the motion's cycle, including where the motion's highlight passes directly behind them.

Where the user has asked their system for reduced motion, this motion SHALL be switched off, exactly as the podium's first-place motion is, per "The podium is animated".

#### Scenario: The 10 slot drifts like the podium's first card
- **WHEN** I watch the score board's 10 slot through several full cycles
- **THEN** the motion travels across the header's whole surface, is present at every instant, and reads as the same slow drift the podium's first-ranked card carries

#### Scenario: The motion never reads as a different tier's colour
- **WHEN** I watch the 10 slot's motion through a full cycle in either theme
- **THEN** its highlight never reads as silver or as any other tier's colour, even at the point in its cycle where the motion is brightest

#### Scenario: The numeral stays legible through the motion
- **WHEN** I watch the 10 slot's numeral and count through a full cycle in either theme
- **THEN** both remain readable at every point, including when the motion's highlight passes directly behind them

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion
- **THEN** the 10 slot's header shows no motion, and its numeral and count remain fully legible

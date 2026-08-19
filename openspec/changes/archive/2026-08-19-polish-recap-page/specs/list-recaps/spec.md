## ADDED Requirements

### Requirement: Recap page title names its period
The recap page SHALL carry exactly one heading naming what is being recapped, composing the selected period and the word "recap" into a single title — a season recap titled by its season and year, a yearly recap by its year, and a multi-year recap by its inclusive year range. The page SHALL NOT additionally repeat the period as a second heading below the controls.

The title SHALL update whenever the mode or period changes, without the page navigating away, and SHALL be present whether or not the period holds any entries, so an empty period is still identified by name.

#### Scenario: A season recap's title
- **WHEN** a season recap for fall 2019 is shown
- **THEN** the page's single heading names fall 2019 as the period being recapped

#### Scenario: A yearly recap's title
- **WHEN** a yearly recap for 2022 is shown
- **THEN** the page's single heading names 2022 as the period being recapped

#### Scenario: A multi-year recap's title
- **WHEN** a multi-year recap covers 2011 through 2020
- **THEN** the page's single heading names that inclusive range as the period being recapped

#### Scenario: The period is named once, not twice
- **WHEN** any recap is shown
- **THEN** the period appears in the page's title and is not repeated as a separate heading above the recap's content

#### Scenario: The title follows the period
- **WHEN** I change the period or switch mode
- **THEN** the title re-renders for the newly selected period in place

#### Scenario: An empty period is still named
- **WHEN** a selected period includes no entries under either time filter
- **THEN** the title still names that period alongside the message that there is nothing to recap for it

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT change the tile's size or position, so hovering across the block never reflows it.

#### Scenario: The stat block is labelled
- **WHEN** a recap renders its stats
- **THEN** the block carries a heading naming it, styled as the page's other section headings are

#### Scenario: A hovered tile is identifiable
- **WHEN** I move the pointer over one stat tile
- **THEN** that tile is visibly distinguished from the tiles around it

#### Scenario: Hover does not reflow the block
- **WHEN** I move the pointer across the stat tiles
- **THEN** no tile changes size or position as it gains or loses the highlight

### Requirement: Recap rows highlight on hover
Every row of the recap page that links to an anime, a season, or a year — the top-10 rows, the hot-take rows, and the rows of all four rankings, inline and in the "See all" overlay alike — SHALL adopt the app's standard accent hover treatment: the same tinted background, accent border, and elevation that the anime cards on my list, the season page, and the top-anime page already use, applied on both pointer hover and keyboard focus.

The treatment SHALL be the same one those pages use rather than a recap-specific variant, so a hovered row reads identically wherever it appears. It SHALL NOT change a row's size or position.

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

### Requirement: Top three posters for every ranked season and year
Every row of the season ranking and of the year ranking SHALL display the posters of my three highest-scored anime from that season or year, so each ranked row is illustrated rather than only the leader. Where a group holds fewer than three scored anime, those available SHALL be shown. This SHALL hold for the rows shown inline and for every row of the "See all" overlay alike.

The same rule SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, since both surfaces rank the same groups on the same basis.

The two time-watched rankings are not covered by this requirement. The seasons-by-time-watched ranking shows no posters on any row, including the leading one. The years-by-time-watched ranking continues to illustrate its leading row alone, with posters picked by episodes watched.

#### Scenario: Every ranked season is illustrated
- **WHEN** a season ranking shows five seasons, each holding at least three scored anime
- **THEN** all five rows show three posters, not only the first

#### Scenario: Every ranked year is illustrated
- **WHEN** a year ranking shows five years
- **THEN** all five rows show the posters of my three highest-scored anime from that year

#### Scenario: Fewer than three available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters

#### Scenario: The overlay matches the inline list
- **WHEN** I open the "See all" overlay of a season or year ranking
- **THEN** every row in it shows posters under the same rule as the inline rows

#### Scenario: Seasons by time watched shows no posters
- **WHEN** the seasons-by-time-watched ranking is shown
- **THEN** no row, including the leading one, shows posters

#### Scenario: Years by time watched keeps leader-only posters
- **WHEN** the years-by-time-watched ranking is shown
- **THEN** only its leading row shows posters

### Requirement: Rankings grouped into a season column and a year column
Where a multi-year recap shows more than one ranking, the rankings SHALL be laid out in two columns grouped by the level they rank: the season ranking and the seasons-by-time-watched ranking in one column, the year ranking and the years-by-time-watched ranking in the other, with the season column leading. A period's season story therefore reads down one column and its year story down the other, rather than the score rankings occupying one row and the time rankings another.

Within a column, the score ranking SHALL come before the time-watched ranking. A column whose rankings are both absent SHALL NOT be rendered as an empty column; where only one column has any ranking to show, that column SHALL occupy the layout on its own.

A yearly recap has no year-level rankings, so this two-column grouping does not apply to it. Where a yearly recap shows both the season ranking and the seasons-by-time-watched ranking, the two SHALL instead be laid out side by side in separate columns — the season ranking leading — rather than stacked one above the other, so a period's best season and most-watched season are directly comparable. Where only one of the two has rows, it SHALL occupy the layout on its own.

On a display too narrow for two columns, the rankings SHALL stack in a single column: for a multi-year recap, season rankings before year rankings; for a yearly recap, the season ranking before seasons-by-time-watched.

#### Scenario: A multi-year recap's four rankings
- **WHEN** a multi-year recap under **What aired** shows a season ranking, a year ranking, and both time-watched rankings
- **THEN** the season ranking and seasons-by-time-watched sit in the leading column, and the year ranking and years-by-time-watched in the other

#### Scenario: Order within a column
- **WHEN** a column shows both a score ranking and a time-watched ranking
- **THEN** the score ranking is shown above the time-watched one

#### Scenario: A yearly recap has no year column
- **WHEN** a yearly recap under **What aired** shows only the season-level rankings
- **THEN** no empty year column is rendered beside them

#### Scenario: A yearly recap's two rankings sit side by side
- **WHEN** a yearly recap under **What aired** shows both a season ranking and a seasons-by-time-watched ranking
- **THEN** the two are presented in separate columns next to each other, the season ranking leading, rather than one stacked above the other

#### Scenario: Narrow display stacks the columns
- **WHEN** a recap showing both columns is displayed too narrow for two columns
- **THEN** the rankings stack in one column with the season rankings before the year rankings

#### Scenario: Narrow display stacks a yearly recap's two rankings
- **WHEN** a yearly recap showing both the season ranking and seasons-by-time-watched is displayed too narrow for two columns
- **THEN** the two stack in one column with the season ranking first

### Requirement: Period stepper arrows
A season recap and a yearly recap SHALL each offer a previous and a next control beside their period selection, stepping the period by one unit of the current mode: one season for a season recap, crossing into the adjacent year at the boundaries of the calendar, and one year for a yearly recap. Stepping SHALL change the period exactly as selecting it from the period controls does, leaving the mode, the time filter, the ranking basis, and the media-type narrowing untouched, and SHALL be carried in the page URL like any other period change.

A multi-year recap SHALL NOT offer these controls, since a two-ended range has no single step.

A control that would step outside the range of periods the recap's own period controls offer SHALL be presented as unavailable and SHALL NOT act.

#### Scenario: Stepping a season
- **WHEN** a fall 2019 season recap is shown and I use the next control
- **THEN** the recap moves to winter 2020, keeping its ranking basis and media-type narrowing

#### Scenario: Stepping across a year boundary
- **WHEN** a winter 2020 season recap is shown and I use the previous control
- **THEN** the recap moves to fall 2019

#### Scenario: Stepping a year
- **WHEN** a 2022 yearly recap is shown and I use the previous control
- **THEN** the recap moves to 2021, keeping its time filter

#### Scenario: Multi-year has no stepper
- **WHEN** a multi-year recap is shown
- **THEN** no previous or next control is offered

#### Scenario: Stepping is addressable
- **WHEN** I step to another period and then reload the page
- **THEN** the recap is shown for the period I stepped to

#### Scenario: The end of the range
- **WHEN** the selected period is the earliest or latest the period controls offer
- **THEN** the control that would step past it is shown as unavailable and does nothing

### Requirement: Season recap links to its season page
A season recap SHALL offer a control that opens the season browser on that same season and year, so the anime that aired that season — not only the ones in my list — are one step away. The control SHALL NOT be offered on a yearly or multi-year recap, whose period is not a single season.

#### Scenario: Opening the season page from a recap
- **WHEN** a fall 2019 season recap is shown and I follow its season-page control
- **THEN** the season browser opens on fall 2019

#### Scenario: Not offered outside season mode
- **WHEN** a yearly or multi-year recap is shown
- **THEN** no season-page control is offered

## MODIFIED Requirements

### Requirement: Hot takes
Every recap SHALL report up to five **hot takes** — the included entries where my score and MAL's community score disagree sharply enough, and in a definite enough direction, to count as a disagreement rather than as ordinary variation. An included entry SHALL be eligible only when it has both my score and a MAL score and it satisfies one of the two rules the profile page's opinion-divergence lists apply:

- **They liked it, I didn't** — the divergence runs at least one standard deviation in MAL's direction, my score is at or below the "average" boundary of the personal scale, and MAL's community score is at or above the community-like boundary.
- **I liked it, they didn't** — the divergence runs at least one standard deviation in my direction, my score is at or above the "very good" boundary of the personal scale, and MAL's community score is at or below that same community boundary.

The thresholds and boundaries SHALL be the ones the profile page already uses, drawn from a single shared definition rather than restated, so an anime that is a hot take in a recap is one the profile page also names, and one the profile page does not name never appears as a hot take.

Qualifying hot takes SHALL be ordered by the size of the divergence, largest first, with ties broken by title case-insensitively so the order is stable across reloads. Each hot take SHALL show the anime, both scores, and which direction the divergence runs, so a pick I rated far above the community reads differently from one I rated far below it.

Divergence SHALL be measured on a common scale rather than by subtracting a 1–10 personal score from a MAL community average directly, using the same normalisation the profile page's opinion-divergence lists already apply.

When fewer than five included entries qualify, the recap SHALL show those that do; when none qualify — including when my list holds too few scored pairs for the normalisation to be computed at all — it SHALL say so rather than present near-agreements as hot takes.

#### Scenario: Five hot takes
- **WHEN** a recap's included set holds more than five entries that satisfy either rule
- **THEN** the five with the largest divergence are shown, largest first, each naming both scores

#### Scenario: Ordered by the size of the disagreement
- **WHEN** several entries qualify by different margins
- **THEN** they are ordered by how far apart the two scores are, largest first

#### Scenario: A near-agreement is not a hot take
- **WHEN** an included entry has both scores but its divergence falls short of one standard deviation
- **THEN** it is not shown as a hot take, however few other entries qualify

#### Scenario: A high score MAL also liked is not a hot take
- **WHEN** I scored an anime 9 and MAL's community score for it is 8.4
- **THEN** it is not shown as a hot take, since neither rule's like/dislike boundaries are crossed

#### Scenario: Agreement with the profile page
- **WHEN** an anime appears in one of the profile page's opinion-divergence lists and falls inside a recap's included set
- **THEN** that anime is eligible to appear as a hot take in that recap

#### Scenario: Divergence direction is visible
- **WHEN** a hot take is one I scored far above MAL's average, and another is one I scored far below it
- **THEN** the two are distinguishable on screen rather than presented identically

#### Scenario: Fewer than five qualify
- **WHEN** only two included entries satisfy either rule
- **THEN** two hot takes are shown

#### Scenario: Nothing qualifies
- **WHEN** no included entry satisfies either rule
- **THEN** the recap states that there are no hot takes for this period

#### Scenario: Too few scored pairs to normalise
- **WHEN** my whole list holds too few entries scored by both me and MAL for the shared normalisation to be computed
- **THEN** the recap states that there are no hot takes for this period rather than ranking on an uncomputable divergence

### Requirement: Year ranking
A multi-year recap under the **What aired** filter SHALL rank the years its period covers, best first, on the same basis as the season ranking. Only years holding at least one included anime that I scored SHALL be ranked.

At most five years SHALL be shown at once. When more than five qualify, the recap SHALL offer a control that opens an overlay listing every qualifying year in rank order. The ranking SHALL NOT be shown on a yearly or season recap, whose period is a single year or less.

The year ranking's placement relative to the season ranking is governed by the "Rankings grouped into a season column and a year column" requirement.

#### Scenario: Ranking a decade
- **WHEN** a multi-year recap covers 2011 through 2020 under **What aired** and I scored anime from eight of those years
- **THEN** the five best-ranked years are shown, and a control opens an overlay listing all eight

#### Scenario: Five or fewer years
- **WHEN** only four years qualify
- **THEN** all four are shown and no overlay control is offered

#### Scenario: Years with nothing watched are omitted
- **WHEN** I scored nothing that debuted in 2013 within the selected range
- **THEN** 2013 does not appear in the ranking or in the overlay

#### Scenario: Not shown on a yearly recap
- **WHEN** a yearly recap is shown
- **THEN** no year ranking is offered

#### Scenario: Both rankings sit side by side
- **WHEN** a multi-year recap shows both a season ranking and a year ranking on a wide display
- **THEN** the two are presented next to each other rather than one above the other

### Requirement: Recap page section order
The recap page SHALL present its sections in a fixed order, leading with the period's top anime rather than with its statistics:

1. The top anime of the period, with the stat block beside it, positioned to the **right** of the top anime on a display wide enough for the two to sit side by side. On a narrow display the two SHALL stack, with the top anime first and the stat block below it rather than hidden or truncated.
2. The rankings — season, year, and most time watched — laid out per the "Rankings grouped into a season column and a year column" requirement.
3. The hot takes.

The period controls, mode tabs, time filter, and period stepper SHALL remain above all of these, so the period can be changed without scrolling past the recap.

#### Scenario: Top anime leads the page
- **WHEN** a recap renders for a period holding entries
- **THEN** the top anime is the first content below the period controls, with the stat block to its right

#### Scenario: Narrow display stacks the two
- **WHEN** the recap is shown on a display too narrow for a side-by-side layout
- **THEN** the top anime is shown first with the stat block below it, both fully readable

#### Scenario: Hot takes come last
- **WHEN** a recap shows stats, a top anime list, rankings, and hot takes
- **THEN** the hot takes appear after every ranking

## REMOVED Requirements

### Requirement: Top three posters for the leading season and year
**Reason**: Superseded by "Top three posters for every ranked season and year" — illustrating only the leading row left the other four rows of each ranking bare, and the leader-only rule was the sole reason the profile page's Favourite seasons and Favourite years were unillustrated below rank 1.

**Migration**: None for consumers — the ranking DTOs are unchanged and every row's poster list is populated where it was previously empty. The time-watched rankings' leader-only posters are unaffected and remain covered by the "Most time watched ranking" requirement.

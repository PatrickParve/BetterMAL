## ADDED Requirements

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

A followable stat whose own count is zero SHALL NOT offer a target — there is nothing for it to lead to — but SHALL still take the same accent hover/focus highlight the tiles beside it take, so it reads as the same kind of tile rather than degrading to an aggregate tile's quieter feedback.

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

#### Scenario: A zero-count stat is highlighted but not followable
- **WHEN** a recap has nothing dropped this period and I move the pointer over **Dropped**
- **THEN** it takes the same accent highlight a non-zero followable tile takes, but no target is offered and it cannot be followed

#### Scenario: The recap's media-type narrowing carries through
- **WHEN** a recap narrowed to films is showing and I follow one of its followable stats
- **THEN** my list opens with that same media-type narrowing in force alongside the stat's own

### Requirement: Drilling into a rating distribution row
Each row of the recap's rating distribution SHALL be followable as a whole — its score, its bar, its count, and its share forming one target rather than only the number being clickable — opening my list scoped to the recap's period, time filter, and media type and narrowed to the anime I gave that score.

The count the row shows and the number of anime my list then holds SHALL agree. As with the stat tiles, the narrowing SHALL arrive applied to my list's own score control rather than as a hidden scope. A row whose count is zero SHALL NOT offer a target — there is nothing for it to lead to — but SHALL still take the same accent hover/focus highlight the rows beside it take, so it reads as the same kind of row rather than as plain text.

Following a row SHALL NOT narrow by status: the distribution counts every included entry carrying that score, whatever its status.

#### Scenario: Opening a score's anime
- **WHEN** a 2020 recap's distribution shows 12 anime scored 8 and I follow that row
- **THEN** my list opens scoped to 2020 showing exactly those 12 anime

#### Scenario: The whole row is the target
- **WHEN** I move the pointer anywhere along a distribution row — over its score, its bar, its count, or its share
- **THEN** the same target is offered for all of them

#### Scenario: An empty score row
- **WHEN** I move the pointer over a score row whose count is zero
- **THEN** it takes the same accent highlight the rows around it take, but no target is offered and it cannot be followed

#### Scenario: Every status is included
- **WHEN** I follow a score row and the period holds both completed and dropped anime carrying that score
- **THEN** both are shown

### Requirement: Distribution rows highlight on hover
Each row of the recap's rating distribution SHALL adopt the app's standard accent hover treatment — the same tinted background, accent border, and elevation the recap's ranking rows, top-10 rows, and hot-take rows already use — on both pointer hover and keyboard focus, so a followable row reads as one of the project's cards rather than as plain text.

The treatment SHALL NOT change the row's size or position, and SHALL NOT change the width or alignment of any of the block's columns, so the bars stay comparable while the pointer moves down the block.

#### Scenario: A hovered distribution row
- **WHEN** I move the pointer over a row of the recap's rating distribution
- **THEN** that row takes the same accent highlight a ranking row takes

#### Scenario: Keyboard focus matches hover
- **WHEN** I reach a distribution row by keyboard rather than by pointer
- **THEN** it takes the same highlight it takes on hover

#### Scenario: Hover does not disturb the bars
- **WHEN** I move the pointer down the distribution
- **THEN** no row changes size or position, and every bar keeps the same track width and alignment it had

#### Scenario: The profile page's block is unchanged
- **WHEN** the profile page renders its own score distribution
- **THEN** its rows are presented exactly as before, without this treatment

### Requirement: Hot take rows read as columns
The hot-take list SHALL align its trailing cells into columns: my score, MAL's score, and the divergence-direction label each occupying a fixed column across every row, so scores read down against scores and the direction labels down against each other. The anime title SHALL absorb the remaining width.

The columns SHALL hold their alignment regardless of a row's title length, of how many digits either score has, and of whether the MAL score is hidden by the score-visibility setting. The two direction labels — "MAL liked it more" and "I liked it more" — differ in length, so the label column SHALL be sized for the longer of the two rather than to each row's own text.

#### Scenario: Scores line up down the list
- **WHEN** a recap shows five hot takes with titles of differing lengths
- **THEN** each row's my-score sits directly above and below the others', and likewise each row's MAL score

#### Scenario: Direction labels line up
- **WHEN** one hot take is labelled "MAL liked it more" and the next "I liked it more"
- **THEN** the two labels occupy the same column, neither shifting the scores beside it

#### Scenario: A hidden MAL score does not shift the row
- **WHEN** scores are hidden and one hot take's MAL score renders as concealed
- **THEN** that row's columns stay aligned with the rows around it

## MODIFIED Requirements

### Requirement: The stat block is headed and responds to hover
The recap's stat block SHALL carry its own heading naming it as the period's statistics, in the same style as the recap's other section headings, so it reads as a section of the page rather than as an unlabelled strip of numbers.

Each stat tile SHALL give visual feedback while the pointer is over it — distinct from its resting state — so the tile under the pointer is identifiable. The feedback SHALL NOT change the tile's size or position, so hovering across the block never reflows it.

A tile that describes a set of anime, per the "Drilling into a recap stat" requirement, SHALL be distinguishable from an aggregate tile that never does: the former SHALL take the app's standard accent hover treatment — the same tinted background, accent border, and elevation its cards and rows use — while the aggregate tiles SHALL take a quieter feedback. Within the tiles that describe a set, only the ones with a target — a non-zero count — SHALL show the pointer cursor and be reachable and followable by keyboard, showing the same treatment on keyboard focus as on hover; a zero-count tile takes the same accent highlight but SHALL NOT show the pointer cursor and is not a keyboard stop, since it has nowhere to lead. Hovering SHALL therefore not promise a destination that does not exist.

Every tile SHALL keep the same box — the same padding, border, and size — so the block reads as one grid and never reflows as the pointer crosses between tiles.

#### Scenario: The stat block is labelled
- **WHEN** a recap renders its stats
- **THEN** the block carries a heading naming it, styled as the page's other section headings are

#### Scenario: A hovered tile is identifiable
- **WHEN** I move the pointer over one stat tile
- **THEN** that tile is visibly distinguished from the tiles around it

#### Scenario: Hover does not reflow the block
- **WHEN** I move the pointer across the stat tiles
- **THEN** no tile changes size or position as it gains or loses the highlight

#### Scenario: A followable tile reads as clickable
- **WHEN** I move the pointer over **Completed**
- **THEN** it takes the accent highlight the app's cards take and the cursor shows it can be followed

#### Scenario: An aggregate tile does not read as clickable
- **WHEN** I move the pointer over **Time spent**
- **THEN** it gives quieter feedback than the followable tiles and the cursor does not mark it as clickable

#### Scenario: Keyboard reaches the followable tiles
- **WHEN** I move through the stat block by keyboard
- **THEN** each followable tile can be focused and followed, showing the same highlight it shows on hover

### Requirement: Rankings grouped into a season column and a year column
Where a multi-year recap shows more than one ranking, the rankings SHALL be laid out in two columns grouped by the level they rank: the season ranking and the seasons-by-time-watched ranking in one column, the year ranking and the years-by-time-watched ranking in the other, with the season column leading. A period's season story therefore reads down one column and its year story down the other, rather than the score rankings occupying one row and the time rankings another.

Within a column, the score ranking SHALL come before the time-watched ranking. A column whose rankings are both absent SHALL NOT be rendered as an empty column; where only one column has any ranking to show, that column SHALL occupy the layout on its own.

The two columns' rankings SHALL align row for row across the layout: the season-level and year-level score rankings SHALL begin on the same line, and so SHALL the two time-watched rankings, whatever differences in height the rankings above them have. In particular, a ranking that shows every one of its rows offers no "See all" control while the ranking beside it may offer one — and that difference SHALL NOT lift the ranking below it out of line with its counterpart. This alignment SHALL be achieved without introducing a "See all" control for a ranking that is already showing every row.

A yearly recap has no year-level rankings, so this two-column grouping does not apply to it. Where a yearly recap shows both the season ranking and the seasons-by-time-watched ranking, the two SHALL instead be laid out side by side in separate columns — the season ranking leading — rather than stacked one above the other, so a period's best season and most-watched season are directly comparable. Where only one of the two has rows, it SHALL occupy the layout on its own.

On a display too narrow for two columns, the rankings SHALL stack in a single column: for a multi-year recap, season rankings before year rankings; for a yearly recap, the season ranking before seasons-by-time-watched.

#### Scenario: A multi-year recap's four rankings
- **WHEN** a multi-year recap under **What aired** shows a season ranking, a year ranking, and both time-watched rankings
- **THEN** the season ranking and seasons-by-time-watched sit in the leading column, and the year ranking and years-by-time-watched in the other

#### Scenario: Order within a column
- **WHEN** a column shows both a score ranking and a time-watched ranking
- **THEN** the score ranking is shown above the time-watched one

#### Scenario: A five-year recap's time rankings stay in line
- **WHEN** a 2020–2024 recap shows a season ranking of twenty seasons, whose overflow offers a "See all" control, beside a year ranking of exactly five years, which offers none
- **THEN** seasons-by-time-watched and years-by-time-watched still begin on the same line

#### Scenario: No redundant overflow control
- **WHEN** a ranking is already showing every one of its rows
- **THEN** no "See all" control is offered for it, whatever the ranking beside it offers

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

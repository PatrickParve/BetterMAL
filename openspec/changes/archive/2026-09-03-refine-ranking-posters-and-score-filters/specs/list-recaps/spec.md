## MODIFIED Requirements

### Requirement: Top three posters for every ranked season and year
Every row of the season ranking and of the year ranking SHALL display the posters of three of my anime from that season or year, so each ranked row is illustrated rather than only the leader. Where fewer than three qualify, those available SHALL be shown. This SHALL hold for the rows shown inline and for every row of the "See all" overlay alike.

Which three, and in what order, SHALL follow **my ranking** — the single total order the `anime-ranking` capability defines — rather than a title comparison. The group's best-ranked anime SHALL be shown first and the others in ranking order after it, laid out left to right so the best-ranked poster is leftmost. Because that ranking orders by my score before anything else, the three SHALL still be my highest-scored anime of the group; what changes is that two anime I scored the same are separated by where I placed them, not by their titles. An anime the ranking does not cover — one I scored but have at Plan to watch, or one that has not aired — SHALL fall after every ranked anime carrying its score, ordered among such anime by title case-insensitively, matching the nulls-last treatment the recap's own top 10 and score board already apply.

Where a ranking is showing a score selection, per "The season and year rankings can be ranked by how many of a score they hold", each row's posters SHALL instead be drawn from **the selected score alone**: the three best-ranked anime of that group carrying that score, in the same ranking order. A row's illustration therefore always agrees with the figure the row was ranked on. With no score selected the posters are the group's three best overall, as above.

The same rules SHALL apply wherever these rankings are rendered, the profile page's Favourite seasons and Favourite years included, since both surfaces rank the same groups on the same basis.

The two time-watched rankings are not covered by this requirement. The seasons-by-time-watched ranking shows no posters on any row, including the leading one. The years-by-time-watched ranking continues to illustrate its leading row alone, with posters picked by episodes watched.

#### Scenario: Every ranked season is illustrated
- **WHEN** a season ranking shows five seasons, each holding at least three scored anime
- **THEN** all five rows show three posters, not only the first

#### Scenario: Every ranked year is illustrated
- **WHEN** a year ranking shows five years
- **THEN** all five rows show the posters of three of my anime from that year

#### Scenario: My ranking breaks a tie at one score
- **WHEN** a ranked season holds four anime I scored 9 and none higher
- **THEN** the three I rank highest are shown, in my ranking's order, rather than the three whose titles come first alphabetically

#### Scenario: The best-ranked poster is leftmost
- **WHEN** a row shows three posters
- **THEN** the best-ranked of the three is the leftmost and the others follow it in ranking order

#### Scenario: Score still leads
- **WHEN** a ranked year holds one anime I scored 10 and several I scored 8
- **THEN** the 10 is shown first, whatever my ranking says about the 8s

#### Scenario: An anime outside the ranking falls last
- **WHEN** a ranked season holds a scored Plan-to-watch anime and three ranked anime carrying the same score
- **THEN** the three ranked ones are shown and the Plan-to-watch one is not

#### Scenario: Posters follow a score selection
- **WHEN** a ranking is showing the selection **8** and a row holds anime I scored 10, 9, and 8
- **THEN** that row's posters are its best-ranked anime scored 8, not its 10s and 9s

#### Scenario: Posters return to the group's best under All
- **WHEN** I return a ranking to **All**
- **THEN** every row's posters are its three best-ranked anime again, whatever their scores

#### Scenario: Fewer than three available
- **WHEN** a ranked season holds only two scored anime
- **THEN** that row shows two posters

#### Scenario: Fewer than three at the selected score
- **WHEN** a ranking is showing the selection 7 and a row holds only one anime I scored 7
- **THEN** that row shows one poster

#### Scenario: The overlay matches the inline list
- **WHEN** I open the "See all" overlay of a season or year ranking
- **THEN** every row in it shows posters under the same rule as the inline rows, including under a score selection

#### Scenario: Seasons by time watched shows no posters
- **WHEN** the seasons-by-time-watched ranking is shown
- **THEN** no row, including the leading one, shows posters

#### Scenario: Years by time watched keeps leader-only posters
- **WHEN** the years-by-time-watched ranking is shown
- **THEN** only its leading row shows posters, picked by episodes watched rather than by my ranking

## ADDED Requirements

### Requirement: The season and year rankings can be ranked by how many of a score they hold
The recap page's **Season ranking** and **Year ranking** SHALL each offer the same score filter the profile page's favourites rankings offer, as defined by the `profile-stats` capability's "Favourites rankings can be ranked by how many of a score they hold": an **All** button, the words **With most:**, and one button per score from **10 down to 1**, sitting between the ranking's title and its rows. Its presentation SHALL be identical on both pages — the score-tier colours, the family colour on **All**, the single button width, and no size change between states.

A score SHALL be offered only when at least one group in that ranking holds at least one anime I scored it, computed over the ranking as the current period produced it, so no offered button can produce an empty ranking. **All** SHALL always be offered. Selecting a score SHALL re-rank that ranking by how many anime of that score each group holds, most first, omitting every group holding none, with ties falling back to the ranking's own order — the identical rule the profile page applies. The five-row cap, the "See all" overlay and its naming of the selection, and the row's figures all follow that same requirement.

The two rankings SHALL hold **independent** selections, and neither SHALL be affected by the other. The two time-watched rankings SHALL NOT offer the control: they rank on time watched and hold no per-score figure to rank by.

Each selection SHALL be carried in the page URL alongside the recap's period, time filter, ranking basis, and media type, so a filtered ranking can be linked to and reloads to what it was showing. Selecting a score SHALL hold the page's scroll position, as the ranking basis and media-type controls do, rather than returning to the top.

Changing the period SHALL keep a selection that the new period's ranking still offers. A selection naming a score the currently shown ranking does not offer SHALL read as **All** rather than emptying the ranking, and SHALL NOT be discarded — stepping back to a period that does offer that score SHALL show the selection again.

The control SHALL appear only where its ranking appears: neither ranking is shown under the **What I watched** filter, the year ranking is shown only on a multi-year recap, and neither is shown on a season recap.

#### Scenario: The control is offered under each ranking's title
- **WHEN** a multi-year recap under **What aired** shows its Season ranking and Year ranking
- **THEN** each shows an All button and a "With most:" row of score buttons between its title and its rows

#### Scenario: All is the default
- **WHEN** I open a recap without touching the control
- **THEN** both rankings are on All and ranked exactly as they are by weighted average

#### Scenario: Ranking the seasons by how many 9s
- **WHEN** I select 9 in the Season ranking
- **THEN** the seasons are re-ranked with the season holding the most anime I scored 9 first, numbered from one, and seasons holding no 9 are omitted

#### Scenario: The two rankings are independent
- **WHEN** I select 10 in the Year ranking
- **THEN** the Season ranking is still on All

#### Scenario: The time rankings offer no filter
- **WHEN** a recap shows seasons-by-time-watched or years-by-time-watched
- **THEN** neither offers a score filter

#### Scenario: A selection is addressable
- **WHEN** I select 8 in the Season ranking and reload the page
- **THEN** 8 is still selected and the ranking is still ranked by it

#### Scenario: Selecting a score holds the scroll position
- **WHEN** I scroll down to a ranking and press one of its score buttons
- **THEN** the ranking re-ranks in place and the page does not jump to the top

#### Scenario: Stepping to a period without that score
- **WHEN** 3 is selected and I step to a period whose ranking holds no anime I scored 3
- **THEN** that ranking is shown on All rather than empty

#### Scenario: Stepping back restores the selection
- **WHEN** I step back to a period whose ranking does hold anime I scored 3
- **THEN** 3 is the selection again

#### Scenario: No control where there is no ranking
- **WHEN** a recap's time filter is **What I watched**, or the recap is a season recap
- **THEN** no score filter is shown, since neither score ranking is shown

### Requirement: The recap's ranking rows state their own figures
Each row of the season ranking and of the year ranking SHALL state the figures it was actually ranked on. Under **All** a row SHALL state how many of my anime the group was computed over and the weighted score it was ranked on. Under a score selection it SHALL state how many anime of the selected score the group holds alongside how many of my anime it was computed over, exactly as the profile page's favourites rankings do.

No row SHALL show a placeholder, an empty value, or a figure belonging to another row's position in the list.

#### Scenario: An unfiltered row states its score and coverage
- **WHEN** a season ranking is shown on All and its first row covers six of my anime at a weighted score of 7.64
- **THEN** that row reads as six scored at 7.64

#### Scenario: Every unfiltered row is stated the same way
- **WHEN** a season ranking shows five rows on All
- **THEN** each states its own scored count and weighted score, and none states a count of a score it was not ranked by

#### Scenario: A filtered row states the count it was ranked on
- **WHEN** the Year ranking is showing the selection 9
- **THEN** each row states how many anime I scored 9 it holds, alongside how many of my anime it was computed over

### Requirement: A top 6-10 row's title is capped
The anime title on each of the recap's top 6-10 rows SHALL be capped at a fixed maximum width, narrower than the row itself, so a long title is truncated at that cap rather than running the full width of the row. The cap SHALL be the same on every row of the list, and SHALL scale with the row's own text size rather than being fixed against a viewport.

A title shorter than the cap SHALL be shown in full and SHALL NOT be padded out to it. A title cut off by the cap SHALL be truncated with an ellipsis and SHALL remain available in full on hover, as it is today. Where the row is narrower than the cap, the title SHALL still shrink to what the row leaves it, so the cap never widens a row or forces it to scroll.

Nothing else about the row SHALL move: its rank, its poster, the column its score sits in, and the row's height are all unchanged.

#### Scenario: A long title stops at the cap
- **WHEN** a top 6-10 row holds an anime whose title is longer than the cap
- **THEN** the title is truncated with an ellipsis at the cap rather than spanning the row

#### Scenario: A short title is untouched
- **WHEN** a row holds a short title
- **THEN** it is shown in full, with no padding out to the cap

#### Scenario: The full title is still reachable
- **WHEN** I hover a truncated title
- **THEN** the full title is shown

#### Scenario: The cap is the same on every row
- **WHEN** ranks six through ten hold titles of differing lengths
- **THEN** every truncated one is cut at the same width

#### Scenario: A narrow row still fits
- **WHEN** the recap is shown on a display too narrow for the cap
- **THEN** the title truncates at what the row leaves it, and the row neither widens nor scrolls sideways

#### Scenario: Nothing else moves
- **WHEN** I compare a top 6-10 row before and after this change
- **THEN** its rank, poster, score column, and height are unchanged

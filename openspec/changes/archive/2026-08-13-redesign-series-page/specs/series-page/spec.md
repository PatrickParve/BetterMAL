## MODIFIED Requirements

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL read `Ongoing` when any member is currently airing, `Upcoming` when no member has finished airing and at least one has not yet aired, and `Finished` otherwise — with `Finished · sequel upcoming` when a finished series has a member that has not yet aired.

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed in my list.
2. `Caught up` — every main-line entry that has finished airing (if any) is marked Completed in my list, every currently-airing main-line entry has me watching at least as many episodes as it has broadcast so far, and at least one main-line entry has finished airing or is currently airing.
3. `N behind` — the conditions of (2) hold except that currently-airing main-line entries have broadcast episodes I have not watched. N SHALL be the total of those unwatched broadcast episodes across the main line.
4. No badge — when a main-line entry that has finished airing is not marked Completed in my list, or when no main-line entry has finished airing or is currently airing (nothing has aired yet to be caught up on or behind on).

`Caught up` SHALL therefore never be claimed while episodes of a running main-line season sit unwatched. An entry that has not finished airing SHALL count against the badge only to the extent it has actually broadcast — it cannot be completed yet, but the episodes it has already aired are episodes I could have watched. An entry that has not aired at all SHALL NOT count against the badge, and an entry that is not in my list SHALL count as zero episodes watched.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all; the "every main-line entry that has finished airing is marked Completed" condition is vacuously satisfied in that case; rather than showing no badge for the entire run, it holds the currently-airing entry itself to the same behind-count check as any other currently-airing main-line entry, so a franchise with only ever one continuous entry can still show `Caught up` or `N behind`.

When a currently-airing main-line entry's broadcast episode count is unknown, the page SHALL show no badge rather than claiming `Caught up` or inventing a behind count, since it cannot tell whether I am current.

#### Scenario: Ongoing series
- **WHEN** I open a series whose latest season is currently airing
- **THEN** the pill reads "Ongoing"

#### Scenario: Finished series with an announced sequel
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Finished · sequel upcoming"

#### Scenario: Year span
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

#### Scenario: Completed series is badged
- **WHEN** I have marked every main-line entry of a fully finished series as Completed
- **THEN** the header shows a "Completed" badge next to the status pill

#### Scenario: Caught up on an ongoing series
- **WHEN** I have completed every main-line entry that has finished airing, and I have watched all 8 episodes the currently airing season has broadcast so far
- **THEN** the header shows a "Caught up" badge rather than "Completed"

#### Scenario: Behind on an airing season is not "caught up"
- **WHEN** I have completed every main-line entry that has finished airing, and the currently airing season has broadcast 8 episodes of which I have watched 5
- **THEN** the header shows a "3 behind" badge and does not show "Caught up"

#### Scenario: An airing season I have not started at all
- **WHEN** I have completed every earlier main-line entry and the currently airing season, with 8 episodes broadcast, is not in my list
- **THEN** the header shows an "8 behind" badge

#### Scenario: Caught up when the next entry has not aired yet
- **WHEN** every main-line entry that has aired is completed and one main-line entry has not yet aired
- **THEN** the header shows a "Caught up" badge

#### Scenario: Unfinished series is not badged
- **WHEN** one main-line entry that finished airing is not marked Completed in my list
- **THEN** no personal badge is shown

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far
- **THEN** no personal badge is shown, rather than "Caught up"

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. Watched time SHALL count watched episodes only and SHALL NOT multiply by rewatch count.

While any member of the series is currently airing, my progress SHALL be shown with the same broadcast-progress bar the home page uses — episodes aired so far as the primary fill, my watched episodes layered on top of it — so it is visible how much of what has aired I have seen. When no member is airing, my progress SHALL use the plain watched-against-total bar, since aired and total are then the same figure.

The progress figures SHALL be named rather than left to be inferred from a bare `x/y` label: the page SHALL state my watched episode count, the episodes aired so far, and the main-line total as three separately named figures, each keyed to the colour of the fill it describes. The aired figure SHALL be shown only while a member of the series is currently airing, since it is otherwise the same number as the total, and the total SHALL carry its lower-bound marker here exactly as it does in the episode total.

Episodes aired SHALL be summed over exactly those main-line entries whose total episode count is known, counting an entry that has finished airing as its full total, a currently airing entry as the episodes it has aired so far, and an entry that has not yet aired as none — so the aired figure can never exceed the total the page shows.

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans. The longest gap between consecutive main-line entries SHALL be presented on the timeline ribbon, where the interval is drawn to scale, rather than as a stat line.

Where several entries tie for the highest MAL score, or for my highest score, the page SHALL list every tied entry rather than picking one. Tied highest-MAL entries SHALL be listed in watch order. Tied favourites SHALL be listed in my saved favourite order, with entries I have not ordered following in watch order.

The highest MAL score SHALL be shown in full rather than blurred when the entry holding it is one I have both completed and scored, since I already know that score.

#### Scenario: Runtime of the main series
- **WHEN** I open a series whose main line totals 62 episodes averaging 24 minutes
- **THEN** the page shows the main-line episode total and a runtime of "1d 0h 48min"

#### Scenario: Extras counted separately
- **WHEN** a series has 5 specials beyond its main line
- **THEN** their episode count and runtime appear as their own figures, not inside the main-line totals

#### Scenario: Unknown episode counts are marked
- **WHEN** a main-line entry is currently airing with an unpublished total episode count
- **THEN** the runtime total is presented as a lower bound rather than an exact figure

#### Scenario: My progress through the series
- **WHEN** I have watched 38 of a series' 62 main-line episodes and nothing is airing
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, my time watched, and the time left to finish

#### Scenario: Broadcast progress while a season is airing
- **WHEN** a series' latest season is currently airing, 12 of its episodes have aired, and earlier seasons total 50 episodes
- **THEN** my progress shows a bar whose aired fill covers 62 episodes with my watched episodes layered on top of it, and the watched, aired, and total figures are each named beside it

#### Scenario: The aired figure is only shown while something is airing
- **WHEN** I open a series where no member is currently airing
- **THEN** the progress readout names my watched count and the total, and does not repeat the total as a separate aired figure

#### Scenario: Tied highest MAL scores list every entry
- **WHEN** two seasons of a series share the same highest MAL score
- **THEN** both are listed under the highest MAL score, in watch order

#### Scenario: Highest MAL score of a completed entry is not blurred
- **WHEN** the hide-scores toggle is on and the highest-MAL-scored entry is one I have completed and scored
- **THEN** its MAL score is shown in full

#### Scenario: Tied favourites list every entry
- **WHEN** I have given the same highest score to three entries of a series
- **THEN** all three are listed as my favourite

### Requirement: Main series and More sections
The series page SHALL list the main line as numbered rows in watch order. Each row SHALL show the entry's picture, title, media type, year, episode count, MAL score, my score, and my list status, SHALL link to that anime's detail page, and SHALL offer an edit control that opens the app's shared entry editor.

A row for a currently-airing entry SHALL additionally state how many of its episodes have broadcast so far, worded so it cannot be read as the entry's total (e.g. `12 of 24 aired`). A row for an entry I have started but not completed SHALL additionally state my watched episode count against that entry's total, so my position in an unfinished entry is readable without opening it.

Extras SHALL be presented under a More section as poster tiles rather than as rows, grouped by media type, so the extras read as a different kind of thing from the numbered main line. Each tile SHALL carry the information a row carries — picture, title, year, episode count, MAL score, my score, and my list status, with the media type carried by its group heading — SHALL link to that anime's detail page, and SHALL offer the same edit control.

Each More group SHALL be collapsible and SHALL show its entry count in its heading. When a series has more than twelve extras every group SHALL start collapsed, and otherwise every group SHALL start expanded; the section SHALL offer one control that expands or collapses every group at once. A collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long.

An edit saved from a series row or tile SHALL update it in place without reloading the page.

#### Scenario: Main line in watch order
- **WHEN** I open a series with four main-line entries
- **THEN** they are listed 1–4 in watch order with picture, title, type, year, episodes, MAL score, my score, and my status

#### Scenario: An airing row states what is out
- **WHEN** a main-line entry is currently airing with 12 of its 24 episodes broadcast
- **THEN** its row states that 12 of 24 have aired, distinct from the entry's episode total

#### Scenario: A partly-watched row states my position
- **WHEN** I have watched 5 episodes of a 24-episode entry and have not completed it
- **THEN** its row states my 5 against that entry's 24

#### Scenario: Extras grouped in More
- **WHEN** a series has two specials, one OVA, and a music video
- **THEN** the More section shows them as poster tiles under a collapsible group per media type, each heading carrying its count

#### Scenario: A large More section starts collapsed
- **WHEN** I open a series with twenty extras
- **THEN** every More group starts collapsed, no tiles are rendered, and one control expands them all

#### Scenario: A small More section starts open
- **WHEN** I open a series with four extras
- **THEN** their groups start expanded

#### Scenario: Editing from a row
- **WHEN** I use a row's or a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the row or tile and the series averages reflect the new score without a page reload

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

## ADDED Requirements

### Requirement: Series timeline ribbon
The series page SHALL show the main line as a timeline ribbon laid out on a time axis spanning the main line's first and last air dates, so the interval between two entries is drawn to scale and a long wait between seasons is visible as space rather than only as a number.

Each main-line entry SHALL be drawn as a block covering its air dates — from its aired-from date to its aired-to date, to the present for an entry still airing, and as a minimum-width marker for an entry that aired on a single date or has no end date. Each block SHALL be filled to show how much of that entry I have watched, SHALL show broadcast progress behind my own fill while that entry is airing, SHALL name its entry, and SHALL link to that anime's detail page.

Each block SHALL carry a paired score bar — MAL's score for that entry and mine — drawn on the same 0–10 basis, so a divergence between MAL and me is visible across the series in the same place as the chronology.

The longest gap between consecutive main-line entries SHALL be marked on the ribbon, naming its length and the two entries it falls between, each linking to its detail page.

Because the ribbon encodes scores as graphical magnitudes, which a blur would not conceal, the MAL side SHALL NOT be rendered at all while the hide-scores toggle is on; a short note SHALL take its place, and my own scores SHALL continue to be shown.

Main-line entries with no air date SHALL be shown in a trailing group rather than placed on the axis, and when no main-line entry has an air date the ribbon SHALL fall back to even spacing in watch order.

The ribbon SHALL scroll within its own container when it does not fit, rather than making the page scroll sideways.

#### Scenario: A gap is drawn to scale
- **WHEN** a series has seasons in 2013, 2014, and 2019
- **THEN** the space between the 2014 and 2019 blocks is far wider than the space between the 2013 and 2014 blocks

#### Scenario: Longest gap named on the ribbon
- **WHEN** a series' longest wait between consecutive main-line entries was from 2015 to 2019
- **THEN** that gap is marked on the ribbon with its length and the two entries it falls between, each linking to its detail page

#### Scenario: My progress on each block
- **WHEN** I have completed the first season, watched half the second, and not started the third
- **THEN** the first block is fully filled, the second half filled, and the third empty

#### Scenario: Scores paired per entry
- **WHEN** I open a series where MAL rates the third season lowest and I rate it highest
- **THEN** that block's paired bars show both scores and the divergence is visible

#### Scenario: Hidden scores are not encoded graphically
- **WHEN** the hide-scores toggle is on
- **THEN** the MAL side of the ribbon is absent from the rendered output entirely, replaced by a note, while my scores still render

#### Scenario: Entries with no air date
- **WHEN** a main-line entry has no air date
- **THEN** it appears in a trailing group beside the axis rather than being placed on it

### Requirement: Score and progress colour language
The series page SHALL use one colour convention throughout: MAL's figures and broadcast progress SHALL use the app's blue — the colour the airing-progress bar already fills with for episodes aired — and my own figures and my watched progress SHALL use the app's purple accent. This SHALL apply to the score averages, the per-entry score bars on the timeline, and the progress fills alike, so which side of a figure is "the world" and which is "me" is readable without labels.

The score averages SHALL be rendered as compact chips within the header rather than as full-width panels, each naming what it averages, its value, and the count it was computed over, and each honouring its existing reveal rules under the hide-scores toggle.

#### Scenario: MAL and my averages are colour-keyed
- **WHEN** I open a series
- **THEN** the MAL average chips carry the same colour as the aired fill of the progress bar, and my average chips carry the same colour as my watched fill

#### Scenario: Averages sit in the header
- **WHEN** I open a series
- **THEN** the score averages appear as compact chips inside the header block rather than as a row of full-width panels below it

#### Scenario: Chips still honour hidden scores
- **WHEN** the hide-scores toggle is on and the series' reveal rules do not apply
- **THEN** the MAL average chips are blurred exactly as the panels were, with the value absent from the rendered output

## REMOVED Requirements

### Requirement: Score comparison strip
**Reason**: Folded into the timeline ribbon — the per-entry MAL-vs-mine comparison is now drawn on the same chronology as the entries themselves, rather than as a second, separately-ordered strip of bars further down the page.

**Migration**: None for stored data. The comparison and its hide-scores rule are preserved by the "Series timeline ribbon" requirement, which carries the same per-entry pairing and the same "MAL side is not rendered at all while scores are hidden" rule.

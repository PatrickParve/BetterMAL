## MODIFIED Requirements

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series' **main series** (e.g. `2013 – 2023`, or the single year when every main-series entry aired in one year).

The year span SHALL be computed over the **main line only**. It SHALL run from the earliest main-line entry's start year to the latest main-line entry's finish year. An entry's start date is the date it began airing, or the announced date it begins for an entry not yet aired. An entry with no known finish date, because it is still airing or has not aired yet, SHALL use its start year as its finish year, as the span does today. Extras, related entries and every other entry shown in More SHALL NOT count toward it, whether they aired before the main series began or after it ended. Every main-line entry SHALL count, every alternative version in a version slot included, so the span describes the franchise's main series and does not change when the picked route does. The span describes the main series as a whole, like the score averages, and does not describe the picked route. An entry with no known start date SHALL NOT count. When no main-line entry has a known start date, the header SHALL show the page's existing no-year placeholder rather than falling back to the extras' years.

The header SHALL be the page's hero rather than a thumbnail strip: the picture SHALL be rendered large enough to read as the page's subject, and the title, status pill, personal badge, year span, external links, score averages, and main-line progress SHALL all sit inside that one block, so the series' summary is read in one place instead of down a column of separate panels.

The status pill SHALL take one of three values, chosen by this precedence:

1. `Airing` — a **main-line** entry of the series is currently airing.
2. `Ongoing` — no main-line entry is currently airing, and some member of the series is currently airing.
3. `Ongoing` — some member has not yet aired.
4. `Finished` — otherwise.

There SHALL be no separate value for a series none of whose members has aired at all. Such a series has something still to come, which is exactly what `Ongoing` means, and SHALL read `Ongoing` under rule 3 alongside every other series with an announced, unaired member.

`Airing` SHALL therefore be reserved for a series with something of its main line on the air right now, and `Ongoing` for a series with nothing of its main line on the air but something still to come — either an announced, not-yet-aired member, or a member outside the main line that is currently airing. A series whose main line has finished but whose OVA or special is currently broadcasting SHALL read `Ongoing`, not `Airing` and not `Finished`.

`Finished` SHALL continue to be reserved for a series with nothing left to come: a series whose aired members have all finished but which has an announced, not-yet-aired member SHALL read `Ongoing`, not `Finished`. The pill SHALL have no `Finished · sequel upcoming` state.

The three values SHALL be visually distinguishable from one another, each carrying its own colour rather than two of them sharing one. `Airing` SHALL carry the same colour this page already uses to mark an entry as on the air now, so the header pill and the timeline's on-air marking agree.

The progress bar and progress readout beneath the header SHALL treat `Airing` exactly as they treat `Ongoing`: both values SHALL select the broadcast progress bar and show the aired-episode figure, since both describe a series that is still running.

Beside the status pill the page SHALL show a personal badge describing where I stand in the main line, chosen by this precedence, evaluated over the series' aired main-line entries in release order:

1. `Completed` — every member of the series has finished airing, at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked **Completed or Rewatching** in my list.
2. `Dropped` — among main-line entries that have aired (finished airing or currently airing), at least one is marked Dropped in my list, and no aired main-line entry released after the most recently aired such drop has ever been watched at all. A drop I later watched past — some later aired main-line entry has any watched episodes — does not count; the badge describes where I stand today, not history.
3. `Caught up` — I have watched at least one main-line episode, and the total I have watched across the aired main line meets the total that has actually broadcast across it.
4. `N behind` — I have watched at least one main-line episode, but fewer than have broadcast across the aired main line. N SHALL be the total broadcast main-line episodes minus the total I have watched, summed across every aired main-line entry — not only a currently-airing one.
5. `Unwatched` — at least one main-line entry has aired, I have watched none of the main line at all, and rule (2) does not already apply.
6. No badge — when nothing in the main line has aired yet, or when a currently-airing main-line entry's broadcast episode count is unknown and rules (3)/(4) cannot otherwise be resolved.

A main-line entry marked **Rewatching** SHALL count as **fully watched** throughout this precedence — as the greater of its own episodes-watched figure and its aired-episode figure, rather than as its current in-progress count. Entering Rewatching resets episodes-watched to zero, so without this rule a franchise I have seen in full and am part-way through watching again would read `N behind`, `Unwatched`, or `Dropped` on the strength of a reset counter. `Rewatching` also satisfies rule (1) alongside `Completed`, so starting a rewatch of a finished franchise SHALL NOT downgrade its badge from `Completed`.

Rules (2) and (5) SHALL be decided without needing any entry's broadcast episode count — only watch status and watched-episode counts — so a currently-airing entry's unknown broadcast count SHALL NOT block them; it SHALL only be able to produce no badge once evaluation reaches rules (3)/(4). An entry that has not aired at all SHALL NOT count toward any of these figures, and an entry that is not in my list SHALL count as zero episodes watched.

`Completed` SHALL carry the colour the app already uses for a Completed watch status. `Caught up` SHALL keep the colour that already marks an entry as on the air now. `N behind` SHALL keep its existing warning colour. `Dropped` SHALL carry the colour the app already uses for a Dropped watch status. `Unwatched` SHALL carry the colour the app already uses for a Plan-to-watch status. All five SHALL remain visually distinct from one another and from the status pill's own three colours.

The status pill and the personal badge SHALL each render at a consistent, uniform size regardless of their label's length, so the two sit beside each other as evenly sized controls rather than ragged text of varying width.

A main line consisting of a single still-running entry — a long-running show that has never split into a "finished" season, such as a long-running weekly series — has no finished-airing entry at all, so rule (1) (which requires at least one finished-airing main-line entry) never applies to it; it falls through to rules (2)–(5) exactly as a multi-entry franchise would, and can still show `Caught up` or `N behind` against its own currently-airing broadcast count.

#### Scenario: A series with a season on the air
- **WHEN** I open a series whose latest main-line season is currently airing
- **THEN** the pill reads "Airing"

#### Scenario: Only an extra is on the air
- **WHEN** every main-line entry of a series has finished airing and one of its OVAs is currently airing
- **THEN** the pill reads "Ongoing", not "Airing" and not "Finished"

#### Scenario: A series with an announced sequel is ongoing
- **WHEN** every aired member of a series has finished but one member has not yet aired
- **THEN** the pill reads "Ongoing"

#### Scenario: A main-line season on the air outranks an announced sequel
- **WHEN** one main-line season of a series is currently airing and a further season has been announced but has not aired
- **THEN** the pill reads "Airing"

#### Scenario: A first season airing before anything has finished
- **WHEN** a series' only aired member is a main-line entry that is currently airing, and no member has finished airing
- **THEN** the pill reads "Airing", not "Ongoing"

#### Scenario: Finished means nothing is left to come
- **WHEN** every member of a series has finished airing and no member is unaired
- **THEN** the pill reads "Finished"

#### Scenario: Nothing has aired yet
- **WHEN** no member of a series has finished airing, none is currently airing, and at least one has not yet aired
- **THEN** the pill reads "Ongoing" — the same value every other series with something still to come carries, with no separate value of its own

#### Scenario: Airing and Ongoing are told apart at a glance
- **WHEN** I compare a series reading "Airing" with one reading "Ongoing"
- **THEN** the two pills carry different colours, and the "Airing" pill carries the same colour the page's timeline uses to mark an entry as on the air now

#### Scenario: An airing series keeps the broadcast progress bar
- **WHEN** I open a series whose pill reads "Airing"
- **THEN** the header's progress bar shows broadcast progress behind my watched progress and the readout states the aired-episode figure, exactly as it does for an "Ongoing" series

#### Scenario: Year span
- **WHEN** a series' earliest main-line entry began airing in 2013 and its latest main-line entry finished airing in 2023
- **THEN** the header shows "2013 – 2023"

#### Scenario: A later extra does not stretch the span
- **WHEN** a series' main line aired from 2013 to 2019 and a film shown in its More section aired in 2023
- **THEN** the header shows "2013 – 2019", not "2013 – 2023"

#### Scenario: An earlier extra does not stretch the span
- **WHEN** a series' main line began airing in 2015 and a pilot OVA shown in its More section aired in 2012
- **THEN** the header's span begins at 2015, not 2012

#### Scenario: A single-year main series shows one year
- **WHEN** every main-line entry of a series aired in 2019 and one of its OVAs aired in 2021
- **THEN** the header shows "2019" rather than "2019 – 2021"

#### Scenario: The span holds still across a route switch
- **WHEN** a series' main line holds two alternative versions of its first season, from 2003 and 2009, and I switch the picked version
- **THEN** the header's year span is the same before and after the switch, covering both versions

#### Scenario: A still-airing main-line entry ends the span at its start year
- **WHEN** a series' latest main-line entry began airing in 2025 and has not finished
- **THEN** the header's span ends at 2025

#### Scenario: No dated main-line entry
- **WHEN** no main-line entry of a series has a known start date, although one of its extras does
- **THEN** the header shows the page's no-year placeholder rather than the extra's year

#### Scenario: Completed series is badged
- **WHEN** I have marked every main-line entry of a fully finished series as Completed
- **THEN** the header shows a "Completed" badge next to the status pill, in the app's Completed-status colour

#### Scenario: Rewatching a finished franchise does not lose the Completed badge
- **WHEN** every member of a series has finished airing, I have completed every main-line entry, and I then mark its first season Rewatching with two episodes watched
- **THEN** the header still shows "Completed", not "Caught up" and not a behind count

#### Scenario: A rewatch in progress does not read as behind
- **WHEN** a series' three finished main-line seasons total 36 broadcast episodes, I have completed the second and third, and the first is marked Rewatching with two of its twelve episodes watched
- **THEN** the header does not show "10 behind"; the rewatching season counts as fully watched

#### Scenario: A rewatch in progress does not read as unwatched
- **WHEN** a series' only main-line entry has finished airing and is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Unwatched"

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

#### Scenario: A partially watched finished entry shows a behind count, not silence
- **WHEN** a series' only main-line entry has finished airing with 12 episodes, I have watched 5, and I have neither completed nor dropped it
- **THEN** the header shows a "7 behind" badge — the same behind-count treatment a currently-airing entry gets, not "no badge"

#### Scenario: A dropped entry with nothing watched after it
- **WHEN** I marked one main-line entry Dropped and have never watched any main-line entry released after it
- **THEN** the header shows a "Dropped" badge, in the app's Dropped-status colour

#### Scenario: A dropped entry I later resumed
- **WHEN** I marked an early main-line entry Dropped but have since watched episodes of a later main-line entry
- **THEN** the header does not show "Dropped"; it shows whatever "Caught up"/"N behind" the combined watched-versus-aired figures produce

#### Scenario: A rewatch after a drop counts as watching past it
- **WHEN** I marked an early main-line entry Dropped and a later main-line entry is marked Rewatching with zero episodes watched
- **THEN** the header does not show "Dropped", because the rewatching entry counts as watched

#### Scenario: Nothing watched at all shows Unwatched
- **WHEN** at least one main-line entry has finished or is currently airing and I have watched none of the main line, with no entry marked Dropped
- **THEN** the header shows an "Unwatched" badge, in the app's Plan-to-watch colour, rather than no badge

#### Scenario: Behind on a single continuously-airing entry
- **WHEN** a series' main line is a single entry that has never finished airing (no prior season to be "finished"), currently airing, with 1173 episodes broadcast so far of which I have watched 1100
- **THEN** the header shows a "73 behind" badge

#### Scenario: An entirely unaired series is not badged
- **WHEN** no main-line entry has finished airing or is currently airing (every main-line entry is not yet aired)
- **THEN** no personal badge is shown

#### Scenario: Unknown broadcast count shows no badge, but only once Dropped/Unwatched are ruled out
- **WHEN** a currently-airing main-line entry has no known count of episodes broadcast so far, and the series is not already "Dropped" or "Unwatched" by those rules
- **THEN** no personal badge is shown, rather than "Caught up"

#### Scenario: The status pill and personal badge are evenly sized
- **WHEN** I compare a series showing "Airing" and "3 behind" against one showing "Finished" and "Completed"
- **THEN** both pills and both badges render at the same consistent size, regardless of how much shorter or longer their labels are

### Requirement: Every picture is shown whole on the series page
An entry's picture SHALL be shown whole wherever the series page renders it, whatever its shape: the page header's picture, a main-line timeline card's picture, and a More tile's picture. The shapes are those the `artwork-presentation` capability defines: a **poster**, an **upright** picture, and a **wide** picture (square or landscape). Only a poster on a timeline card or a More tile SHALL be cropped to fill its box, and only as it is today. The page header SHALL crop no picture of any shape.

In the page header, every picture that is not landscape SHALL keep the width the header's portrait picture has and take whatever height its own proportions give it at that width. This covers a poster, an upright picture and a square picture alike. Nothing SHALL be cut off: a 2:3 poster SHALL be drawn at 2:3 rather than trimmed to the header's portrait box, a narrower or wider poster likewise at its own proportions, and a square picture square. No maximum height SHALL be imposed on it.

A landscape picture, strictly wider than it is tall, SHALL keep the header's existing landscape treatment. It SHALL be drawn whole and wider than the portrait width, with the score averages and progress placed beneath the picture rather than beside it. A square picture is not landscape under this test, and SHALL keep the portrait layout with the score averages and progress beside it.

In every case the title, status pill, personal badge, year span, links, score averages and progress SHALL keep their existing positions relative to the picture. Only the picture's own height, and so the height of the header block, SHALL follow from its proportions.

Until the header picture has loaded, the header SHALL reserve the existing portrait box's size, so the header does not collapse and then expand as the picture arrives.

On a timeline card and on a More tile, the card's picture area SHALL keep the height it has for a poster, so every card in a row still lines its picture, title and footer up with its neighbours'. A wide picture SHALL be fitted whole inside that area rather than cropped to fill it, and the card carrying it SHALL be wider than its poster neighbours so the fitted picture is shown at a useful size rather than reduced to a sliver of the card's height:

- on the timeline, the card SHALL take the one widened card size;
- in More, the tile SHALL span two of its grid's columns, under `artwork-presentation`'s rule for column grids. It stays in the group's order, leaves the row before it one column short when it does not fit at that row's end, and stays one column wide where the grid has room for only one.

An upright picture SHALL keep the width of a poster card or tile and be fitted whole inside its picture area. Wherever a fitted picture leaves part of a picture area uncovered, that space SHALL be filled from the picture itself, as `artwork-presentation` requires.

A card's extra width SHALL be a consequence of its artwork's shape alone. It SHALL NOT vary with how long the entry ran, how long the wait before it was, its episode count, its scores, or my progress on it. It SHALL take only one widened size rather than a size computed per image, so a wider card can never be read as a duration or magnitude signal.

Wherever this capability refers to a landscape timeline card, a landscape More tile, or landscape artwork on either, it SHALL mean one carrying a wide picture in this sense, square pictures included.

On a timeline card and on a More tile, a poster SHALL keep its existing box and its existing card width unchanged. The placeholder shown when an entry has no picture SHALL keep its existing box everywhere, the page header included, since it has no proportions to adopt.

Because a picture's shape is not known until the image itself has loaded, the page SHALL render the existing poster boxes until then, and adopt a picture's treatment once its shape is known. The page SHALL NOT request, store, or wait on any additional data to make this decision.

#### Scenario: A landscape header picture is not cropped
- **WHEN** I open a series whose root entry's picture is wider than it is tall
- **THEN** the header shows that whole picture at its own proportions, wider than the portrait width, with the title, pill, badge, year span and links beside it and the score averages and progress beneath it, exactly as a landscape header is laid out today

#### Scenario: A 2:3 header poster is not trimmed
- **WHEN** I open a series whose picture is a 2:3 poster, such as a TMDB poster
- **THEN** the header shows the whole poster at 2:3, at the header's portrait width and slightly taller than the portrait box, with nothing cut off at its top or bottom, and the score averages and progress stay beside it

#### Scenario: A wider-than-usual header poster is not trimmed
- **WHEN** I open a series whose picture is a poster slightly wider in proportion than the header's portrait box, such as a 0.74 picture
- **THEN** the header shows the whole poster at the portrait width and at its own, slightly shorter height, with nothing cut off at its sides

#### Scenario: The header holds its size while the picture loads
- **WHEN** I open a series page and its header picture has not finished loading
- **THEN** the header reserves the portrait box's usual size, and adopts the picture's own height once it has loaded

#### Scenario: A square header picture is drawn square
- **WHEN** I open a series whose picture is exactly as wide as it is tall
- **THEN** the header shows the whole picture square, at the header's portrait picture width, with nothing cut off

#### Scenario: A landscape timeline card shows its whole picture
- **WHEN** a main-line entry's picture is wider than it is tall
- **THEN** its timeline card shows the whole picture, the card is wider than its poster neighbours, and its picture area, title, chips, and footer still line up with theirs

#### Scenario: A square timeline card shows its whole picture
- **WHEN** a main-line entry's picture is exactly as wide as it is tall
- **THEN** its timeline card takes the same widened size a landscape card takes, and shows the whole square picture inside its picture area

#### Scenario: A landscape More tile shows its whole picture
- **WHEN** an extra's picture is wider than it is tall
- **THEN** its More tile shows the whole picture and is wider than the poster tiles in its group, while its rows stay aligned with them

#### Scenario: A square More tile shows its whole picture
- **WHEN** an extra's picture is exactly as wide as it is tall
- **THEN** its More tile spans two columns and shows the whole square picture, with nothing cut off at its top, bottom or sides

#### Scenario: An upright picture is whole in a poster-width card
- **WHEN** a main-line entry's or an extra's picture is upright, such as a 4:5 picture
- **THEN** its card or tile keeps the poster width and shows the whole picture inside its picture area, with the space around it filled from the picture itself

#### Scenario: More keeps its order
- **WHEN** a wide More tile's turn falls in the last column of its group's row
- **THEN** it begins the next row, and every tile in the group still appears in the group's order

#### Scenario: Card width still says nothing about duration
- **WHEN** a series has one entry that ran a single cour and another that ran for several years, both with poster pictures
- **THEN** both cards render at the same width, and the only cards that differ in width anywhere on the page are those whose own artwork is wide

#### Scenario: Poster cards and tiles are untouched
- **WHEN** I open a series in which every picture is a poster
- **THEN** every timeline card and every More tile renders exactly as it does today, and only the header picture is drawn at its own proportions

### Requirement: Series figures follow the picked route
Where a series' main line holds one or more version slots, the page's figures SHALL take two different member scopes.

The **score averages** — MAL's and mine, across the main line and across all entries, as "Series score averages" defines — SHALL be computed over **every** main-line entry, every alternative included, whatever is picked. They SHALL NOT change when the picker does, so the figure that describes the franchise stays stable.

The header's **year span** SHALL likewise cover every main-line entry, every alternative included, per "Series page header", and SHALL NOT change when the picker does. So SHALL the browser card's year span, which is the same figure.

Every other main-line figure "Series stats" defines — the main-line episode total, the main-line runtime total, episodes aired, my watched episodes and watched time, my rewatched time, entries completed, the lower-bound marker on an unknown episode count, whether the main line is settled by me, and the longest gap — SHALL be computed over the **trunk plus the picked alternatives and their branches**, so that time left describes the route I chose rather than counting every retelling of the same story.

Those figures SHALL be delivered with the series for each admissible combination of picks, so switching a picker changes them without a further request. The number of combinations SHALL be capped; beyond the cap, slots after the first SHALL keep their default alternative's figures while the picker still changes which entries are shown.

The delivered figures describe the series as the server last read it. Where an edit made on the page has since changed one of them, the page SHALL show the edited value on **every** route rather than the delivered one, per "The stats box stays live after an in-place edit".

The figures a series carries outside its own page — the browser card, the profile's Top series, and search — SHALL use the default combination.

A series whose main line has no version slot SHALL be unaffected: every figure covers its whole main line, exactly as before.

#### Scenario: Averages hold still across a switch
- **WHEN** I switch between two routes of a series
- **THEN** the MAL and my score averages are unchanged

#### Scenario: The year span holds still across a switch
- **WHEN** I switch between two routes of a series whose alternatives aired in different years
- **THEN** the header's year span is unchanged, and still covers every alternative

#### Scenario: Totals follow the route
- **WHEN** I switch from a four-entry route to a three-entry route
- **THEN** the main-line episode total, the runtime total and the time left all change to describe the route now picked

#### Scenario: Time left describes one route
- **WHEN** a series holds four alternative retellings of the same story on its main line
- **THEN** its time left counts the picked route once, not all four

#### Scenario: Switching costs no request
- **WHEN** I switch the picked alternative
- **THEN** the figures update without another series request

#### Scenario: An edited figure survives a switch
- **WHEN** I edit a member's status and then switch the picked alternative
- **THEN** the route's figures account for that edit rather than reverting to the values delivered with the series

#### Scenario: The card uses the default
- **WHEN** I look at that series' card in the series browser
- **THEN** its figures are those of the default combination

#### Scenario: A series without a slot is unchanged
- **WHEN** a series' main line holds no alternative versions
- **THEN** every figure covers its whole main line as before

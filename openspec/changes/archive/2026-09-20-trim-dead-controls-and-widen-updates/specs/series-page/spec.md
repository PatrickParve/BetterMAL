## MODIFIED Requirements

### Requirement: Series page header
The series page SHALL show the root entry's picture, the series title, a status pill, and the year span of the series (e.g. `2013 – 2023`, or the single year when every entry aired in one year).

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
- **WHEN** a series' earliest entry aired in 2013 and its latest in 2023
- **THEN** the header shows "2013 – 2023"

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

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Wherever this page counts **my watched main-line episodes** — the progress bar's watched fill, the named watched figure beside it, and therefore time left — a main-line entry marked **Rewatching** SHALL count as fully watched, as the greater of its own episodes-watched figure and its aired-episode figure. This is the same rule the header's personal badge applies, so the badge and the progress figures directly beside it can never disagree about the same entry. It governs only the watched side of each pair: the episode total, the aired figure, and the runtime describe the anime rather than me, and SHALL be unchanged by a rewatch in progress. It does not multiply anything by a rewatch count: a rewatched entry still counts **once** in these figures. Time watched is defined separately below and does count rewatch runs; time left continues to subtract only these once-counted figures, so a rewatch in progress can never drive it negative or make a half-watched franchise read as finished.

The entries-completed stat SHALL cover the extras as well as the main line, as two separately labelled figures within one stat: how many main-line entries I have completed out of the main-line total, and how many extras I have completed out of the extras total. The two SHALL NOT be summed into a single figure, so which half of the series is unfinished stays visible. When the series has no extras, the extras figure SHALL be omitted and the stat SHALL show the main-line figure alone rather than an "0 of 0". An entry marked Rewatching SHALL count as completed in this stat, since a rewatch can only follow a completed run.

**Time watched SHALL count rewatches.** It SHALL be the main line's once-counted watched time — the figure the paragraph above governs — plus the time spent rewatching that main line: for each main-line entry, one further complete run of it for every recorded rewatch, plus the episodes watched so far in a run still in progress. This is the definition the profile page's watch time already uses, so the two pages SHALL NOT describe the same hours differently. A twelve-episode season I have completed, rewatched twice, and am two episodes into rewatching a third time SHALL therefore contribute 38 episodes of time watched, not 12.

Rewatch time SHALL be counted over the **main line only**, matching every other figure in this stat box and matching what time left subtracts. Time watched MAY exceed the main line's runtime as a result, which is correct rather than an error; time left SHALL NOT be derived from it.

The two time figures SHALL be presented as **one stat**, in the two-labelled-rows shape the entries-completed stat already uses: a single stat titled **Time**, holding a row labelled `Watched:` and a row labelled `Left:`. They SHALL NOT be rendered as two separate stat cells, since they are two halves of one question about the same series.

Each row SHALL keep its own rule for whether it appears, unchanged. The **Watched** row SHALL appear whenever time watched is non-zero, including on a series with nothing left to watch — a finished franchise that has been rewatched is precisely the case where the figure says something the entries-completed and progress figures do not. The **Left** row SHALL appear only while there is time left: when it computes to zero — I have watched at least as much of the main line as its runtime accounts for — it SHALL NOT be rendered, since "0min left" restates what the badge and the progress bar already say. A Time stat holding one qualifying row SHALL show that row alone rather than an empty or zeroed counterpart; when neither row qualifies, the Time stat SHALL NOT be rendered at all.

That withholding of the Left row SHALL NOT apply when the main-line runtime is itself unknown — a zero runtime total that the page already marks as unknown rather than as an exact figure. A zero time left derived from a runtime nobody knows reports missing data, not a series I have finished, so the Left row SHALL still be shown in that case.

Runtime SHALL be computed as episodes times the entry's average episode duration, falling back to the app's existing 24-minutes-per-episode assumption when a duration is unknown, and SHALL be formatted in days, hours and minutes (e.g. `4d 6h 30min`). When any counted entry's episode count is unknown, the page SHALL mark the total as a lower bound rather than presenting it as exact. An entry whose total episode count is unknown SHALL still contribute its known aired-so-far episode count toward that lower bound, rather than contributing nothing, whenever an aired count is known for it — so a still-airing entry with no announced total makes the lower bound tighter instead of forcing the whole stat to read as wholly unknown. Time left SHALL be derived from once-counted watched episodes only and SHALL NOT multiply by rewatch count; time watched SHALL count rewatch runs exactly as defined above.

While any member of the series is currently airing, my progress SHALL be shown with the same broadcast-progress bar the home page uses — episodes aired so far as the primary fill, my watched episodes layered on top of it — so it is visible how much of what has aired I have seen. When no member is airing, my progress SHALL use the plain watched-against-total bar, since aired and total are then the same figure.

The progress figures SHALL be named rather than left to be inferred from a bare `x/y` label: the page SHALL state my watched episode count, the episodes aired so far, and the main-line total as three separately named figures, each keyed to the colour of the fill it describes. The aired figure SHALL be shown only while a member of the series is currently airing, since it is otherwise the same number as the total, and the total SHALL carry its lower-bound marker here exactly as it does in the episode total.

Episodes aired SHALL be summed over exactly those main-line entries whose total episode count is known, counting an entry that has finished airing as its full total, a currently airing entry as the episodes it has aired so far, and an entry that has not yet aired as none — so the aired figure can never exceed the total the page shows.

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres of the series.

Studios and Genres SHALL be taken from the **main line as the picked route shows it** — the same member basis the episode total, the runtime, the aired figure, the watched figures and the entries-completed main-line figure already use — and SHALL NOT include a studio or genre carried only by an extra. A franchise's OVAs, specials, side stories and other extras SHALL therefore contribute nothing to either list, however many of them the series holds. Each list SHALL remain de-duplicated and ordered case-insensitively as it is today, and SHALL be omitted when the picked main line yields no value for it.

The page SHALL additionally show which entry or entries are tied for the most rewatches across the series (main line and extras alike), naming each tied entry and its rewatch count. This stat SHALL NOT be shown at all when no member of the series has been rewatched, rather than showing a stat naming zero-rewatch entries.

Where several entries tie for the highest MAL score, for my highest score, or for the most rewatches, the page SHALL list every tied entry rather than picking one. Tied highest-MAL entries and tied most-rewatched entries SHALL be listed in watch order. Tied favourites SHALL be listed in my saved favourite order, with entries I have not ordered following in watch order.

The Highest MAL score stat SHALL be gated **as a whole** rather than entry by entry, and SHALL be gated only while the global hide-scores toggle is on. While that toggle is off the stat SHALL always render, whatever the state of the series — the toggle is the one switch governing it, and a user who wants the stat withheld turns scores off.

While the toggle is on, the stat SHALL render only once the series is settled. The series counts as settled on exactly the condition the main-series MAL average chip already uses — the same predicate over the same population, since both are computed across the whole unfiltered main line — so the two can never disagree about whether this series' MAL figures may be shown.

Where the stat renders, it SHALL do so as it does today: the title, link and MAL score of every tied entry, each entry's score still following the ordinary hide-scores rules for its own status.

While the toggle is on and the series is **not** settled, the stat SHALL show a reveal control in place of that list, naming no entry and showing no score until the control is used. That control SHALL be the same control a hidden MAL score renders, carrying no text label of its own — the stat's own heading already names what is withheld — so a withheld stat reads as every other withheld value in the app does. Naming the tied-highest entry of an unfinished franchise is a comparative claim about entries the user has not reached — it can bias anticipation for a season not yet out or not yet watched, and it can change on its own once an airing season finishes and gathers votes. That is true even of an entry the user has already completed, so while the toggle is on the gate SHALL NOT be relaxed per entry: a completed entry SHALL be withheld along with the rest until the series is settled. What this withholds is a spoiler rather than a score value, but it is nonetheless governed by the hide-scores toggle rather than applying unconditionally, so that one switch answers for every way this capability withholds a MAL figure.

Using that control SHALL reveal the list for the current page view only, rendered exactly as in the settled case. The reveal SHALL NOT persist: it SHALL be dropped on navigating away from the series page and back, on navigating to a different series, and on a reload — the same non-persistence every other score reveal in the app follows.

Within one page view the reveal SHALL hold. It SHALL survive every control the series page offers without leaving it — picking a different route through the series, filtering the More section, and editing an entry in place — and SHALL survive the series data being refreshed beneath it, including a refresh that leaves the series unsettled. Leaving the page, moving to another series, and reloading SHALL be the only things that end it. The reveal answers a request the user made on this page view, and withdrawing it while they are still reading would be indistinguishable from a fault.

Whether the series is settled SHALL be evaluated afresh from the current data each time the stat renders, and SHALL NOT be recorded when the stat is first shown or when its control is used. A series that stops being settled — a metadata refresh surfacing a newly airing entry, say — SHALL therefore withhold the stat again on the next render, without a navigation and without any separate step to invalidate it.

#### Scenario: Runtime of the main series
- **WHEN** I open a series whose main line totals 62 episodes averaging 24 minutes
- **THEN** the page shows the main-line episode total and a runtime of "1d 0h 48min"

#### Scenario: Extras counted separately
- **WHEN** a series has 5 specials beyond its main line
- **THEN** their episode count and runtime appear as their own figures, not inside the main-line totals

#### Scenario: Unknown episode counts are marked
- **WHEN** a main-line entry is currently airing with an unpublished total episode count
- **THEN** the runtime total is presented as a lower bound rather than an exact figure

#### Scenario: An unpublished total still counts what has aired
- **WHEN** a main-line entry is currently airing with no published total episode count but 1,100 episodes are known to have aired for it
- **THEN** the main-line episode total includes those 1,100 aired episodes rather than contributing zero for that entry, and the total is still marked as a lower bound

#### Scenario: My progress through the series
- **WHEN** I have watched 38 of a series' 62 main-line episodes and nothing is airing
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, and one Time stat carrying my watched and left figures

#### Scenario: A rewatching entry keeps the progress bar full
- **WHEN** a series' 62 main-line episodes are all watched and one 12-episode season is marked Rewatching with two episodes watched
- **THEN** the progress bar still reads 62 of 62 watched, matching the "Completed" badge beside it, rather than dropping to 52

#### Scenario: A rewatching entry counts as a completed entry
- **WHEN** I have completed five of a series' six main-line entries and marked the sixth Rewatching
- **THEN** the entries-completed stat reads "6 of 6"

#### Scenario: The episode total is unmoved by a rewatch
- **WHEN** a main-line entry is marked Rewatching
- **THEN** the main-line episode total, the aired figure, and the runtime are exactly what they were before the rewatch began

#### Scenario: Entries completed covers extras too
- **WHEN** I open a series where I have completed 5 of 6 main-line entries and 2 of its 3 extras
- **THEN** the entries-completed stat shows "5 of 6" for the main line and "2 of 3" for the extras as two labelled figures, rather than one combined "7 of 9"

#### Scenario: A series with no extras shows one figure
- **WHEN** I open a series whose every member is main line
- **THEN** the entries-completed stat shows only the main-line figure, with no extras figure beside it

#### Scenario: The two time figures share one stat
- **WHEN** I open a part-watched series
- **THEN** the stats box holds one "Time" stat with a `Watched:` row and a `Left:` row inside it, rather than two separate "Time watched" and "Time left" stats

#### Scenario: A finished series keeps the Time stat with only its watched row
- **WHEN** I open a series whose main line I have watched in full, so no time is left
- **THEN** the Time stat is shown with its `Watched:` row alone, and no `Left:` row

#### Scenario: A completed rewatch adds to time watched
- **WHEN** a series' main line totals 62 episodes averaging 24 minutes, I have completed all of it, and one 12-episode season carries a rewatch count of 2
- **THEN** the Time stat's watched row covers 86 episodes rather than 62, and no left row is shown

#### Scenario: A rewatch in progress counts the episodes watched so far
- **WHEN** a 12-episode season I completed is marked Rewatching with two episodes watched and a rewatch count of 2
- **THEN** that entry contributes 38 episodes to time watched — its original run, its two completed rewatches, and the two episodes of the run in progress

#### Scenario: A rewatch does not move time left
- **WHEN** I have watched 38 of a series' 62 main-line episodes and then rewatch a completed 12-episode season twice
- **THEN** the Time stat's left row is exactly what it was before the rewatches, while its watched row has grown by those 24 episodes

#### Scenario: Extras' rewatches stay out of the figure
- **WHEN** an extra (not a main-line member) carries a rewatch count of 3
- **THEN** the series' time watched is unchanged by it

#### Scenario: A part-watched series keeps both time rows
- **WHEN** I open a series with main-line episodes I have not yet watched
- **THEN** the Time stat shows both its `Watched:` and its `Left:` row

#### Scenario: An unknown runtime is not mistaken for a finished series
- **WHEN** I open a series whose main-line runtime total is unknown, so it reports zero time left without my having watched it through
- **THEN** the Time stat still shows both rows, because the zero reflects a runtime nobody knows rather than a series I have finished

#### Scenario: A series I have not started shows no watched row
- **WHEN** I open a series with no main-line entry in my list
- **THEN** the Time stat shows its `Left:` row alone, carrying the whole main-line runtime, with no `Watched:` row

#### Scenario: Neither row qualifies
- **WHEN** a series has nothing watched and an exactly-known main-line runtime of zero
- **THEN** no Time stat is rendered at all

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
- **WHEN** the series is settled, the hide-scores toggle is on, and the highest-MAL-scored entry is one I have completed
- **THEN** its title, link, and MAL score are shown in full

#### Scenario: Highest MAL score of a dropped entry is shown
- **WHEN** the series is settled, the highest-MAL-scored entry of that series is one I have marked Dropped, and I never gave it a score of my own
- **THEN** the stat names that entry, links to it, and shows its MAL score, exactly as it would for a completed entry

#### Scenario: Showing scores shows the stat whatever the series
- **WHEN** the hide-scores toggle is off and I open a series with a member currently airing, or one whose main line I have not finished
- **THEN** the Highest MAL score stat renders its tied entries directly, with no reveal control to use first

#### Scenario: A settled series shows the stat with no control
- **WHEN** the hide-scores toggle is on, every finished-airing entry of a series' main line is one I have Completed, Dropped, or am Rewatching, and nothing in the series is currently airing
- **THEN** the Highest MAL score stat renders its tied entries directly, with no reveal control to use first

#### Scenario: An airing series withholds the stat behind a control
- **WHEN** the hide-scores toggle is on and a member of the series is currently airing
- **THEN** the Highest MAL score stat shows a reveal control naming no entry, even for entries I have already completed

#### Scenario: The control is the one a hidden score uses
- **WHEN** the Highest MAL score stat is withheld
- **THEN** its reveal control is the same control a hidden MAL score renders, with no text label of its own

#### Scenario: A main line I have not finished withholds the stat
- **WHEN** the hide-scores toggle is on and a finished-airing entry of the series' main line is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
- **THEN** the Highest MAL score stat shows its reveal control rather than naming any entry

#### Scenario: Using the control reveals the stat for this page view
- **WHEN** the stat is withheld and I use its reveal control
- **THEN** it renders its tied entries exactly as it would for a settled series, each entry's own score following the ordinary hide-scores rules

#### Scenario: Turning hiding back on withholds a revealed stat again
- **WHEN** I reveal the stat on an unsettled series, turn the hide-scores toggle off, and then turn it on again without leaving the page
- **THEN** the stat shows its reveal control again, the same way turning the toggle on drops an individually revealed score

#### Scenario: A revealed stat is withheld again on returning
- **WHEN** the hide-scores toggle is on, I reveal the stat on an unsettled series, then navigate to another series or away and back, or reload the page
- **THEN** the stat shows its reveal control again rather than the entries I had revealed

#### Scenario: Losing settledness withholds the stat again without a navigation
- **WHEN** the hide-scores toggle is on, the stat is showing because its series was settled, and the series data then changes so that a member is currently airing
- **THEN** the stat is withheld behind its reveal control again on the next render, with no navigation and no reload

#### Scenario: A reveal I asked for is not withdrawn by a refresh
- **WHEN** the hide-scores toggle is on, I use the reveal control on an unsettled series, and the series data is then refreshed beneath me while I stay on the page and the series stays unsettled
- **THEN** the stat stays revealed, rather than closing again under me

#### Scenario: A reveal survives the page's own controls
- **WHEN** I use the reveal control and then pick a different route through the series, filter the More section, or edit an entry in place without leaving the page
- **THEN** the stat stays revealed through all of them

#### Scenario: Tied favourites list every entry
- **WHEN** I have given the same highest score to three entries of a series
- **THEN** all three are listed as my favourite

#### Scenario: Most rewatched entry is shown
- **WHEN** one entry in a series has a rewatch count of 3 and no other member has a higher rewatch count
- **THEN** the page shows a "Most rewatched" stat naming that entry and its count of 3

#### Scenario: Tied most-rewatched entries list every entry
- **WHEN** two entries in a series share the same highest rewatch count
- **THEN** both are listed under "Most rewatched", in watch order

#### Scenario: No rewatch stat when nothing has been rewatched
- **WHEN** no member of a series has a rewatch count above zero
- **THEN** the page shows no "Most rewatched" stat at all

#### Scenario: An extra's studio stays out of Studios
- **WHEN** a series' main line was animated by one studio and one of its OVAs by a different studio
- **THEN** the Studios stat names the main line's studio alone, and does not name the OVA's

#### Scenario: An extra's genre stays out of Genres
- **WHEN** a series' main line carries no comedy entry but one of its specials is tagged Comedy
- **THEN** the Genres stat does not list Comedy

#### Scenario: Studios and Genres follow the picked route
- **WHEN** a version slot's two alternatives were animated by different studios and I pick the other alternative
- **THEN** the Studios stat reports the picked alternative's studio, moving with the episode and runtime figures beside it

#### Scenario: A main line with no studio on record shows no Studios stat
- **WHEN** no entry of the picked main line has a studio recorded, while one of the series' extras does
- **THEN** the Studios stat is not shown at all

## ADDED Requirements

### Requirement: The "in my list" control is offered only when it can narrow something

The More section's "in my list" control SHALL be rendered only when at least one of
the entries the More section holds is in my list. When none of them is, the control
SHALL NOT be rendered at all — not as a disabled control, and not as a control that
filters everything away — since its only possible effect there is to empty every
group in the section.

The population deciding this SHALL be exactly the population the filter narrows: the
entries the More section shows, real extras and related entries alike. The control
SHALL therefore be present exactly when activating it could leave at least one tile
standing.

While the control is withheld, the filter SHALL be treated as **off** regardless of
any filter state the page restored, so a group can never be left narrowed with no
control able to widen it again. Restoring a page whose stored filter state says "on"
into a series with nothing of mine among its extras SHALL show every group
unfiltered.

Nothing else in the More section SHALL depend on this. The media-type filter row, the
expand/collapse-all control, each group's heading, the hidden-count control and the
groups themselves SHALL render and behave exactly as they do when the control is
present.

#### Scenario: A series with no extras of mine offers no filter

- **WHEN** I open a series whose More section holds extras and related entries, none of which is in my list
- **THEN** no "in my list" control is shown, and the expand/collapse-all control and media-type filter row are shown as usual

#### Scenario: One extra of mine is enough

- **WHEN** exactly one of the entries in a series' More section is in my list
- **THEN** the "in my list" control is shown and behaves exactly as it does on a series with many

#### Scenario: A restored filter cannot strand a section

- **WHEN** I return to a series whose stored More-section state has the filter on, and no entry in its More section is in my list
- **THEN** every expanded group shows all of its entries, as though the filter were off

#### Scenario: The groups are otherwise untouched

- **WHEN** the "in my list" control is withheld and I activate a group's heading
- **THEN** that group opens showing every one of its entries, and collapses again on a second activation, exactly as it would with the control present

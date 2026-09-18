## MODIFIED Requirements

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

The page SHALL additionally show the highest MAL-scored entry, my highest-scored entry, and the studios and genres the series spans.

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

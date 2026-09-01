## ADDED Requirements

### Requirement: The stats box stays live after an in-place edit
Every figure the series page derives from its members SHALL describe the page's **current** member data, without a reload and on whichever route is picked.

When an entry editor save from a timeline card or a More tile changes a member's score, status, episodes watched, or rewatch count, the page SHALL recompute, for the picked route: the four score averages, my highest-scored entries, the most-rewatched entries, both entries-completed figures, time watched, time left, the progress bar and its named watched figure, and the header's personal badge. It SHALL NOT keep showing the figures the server last delivered for any of them.

Switching the picked route SHALL NOT discard an edit's effect. The figures for the newly picked route SHALL be derived from the same current member data, so no figure reverts to its pre-edit value by switching away from a route and back to it.

Reordering my tied favourites SHALL take effect on whichever route is picked, not only on the default one.

The recomputed figures SHALL cover exactly the members the server's own figures cover — the main-line entries the picked route shows, and the series' real extras. **Related entries** — anime shown in More because a main-line member relates to them, without being members of the series — SHALL NOT enter any average or any stat, exactly as they do not on the server. An edit SHALL therefore never move a figure onto a different member basis than the one it was first delivered on.

Figures no edit can change — the studios and genres the series spans, the longest gap, the main-line and extras episode and runtime totals, the aired-episode figures, and the member counts — MAY continue to be taken from the figures delivered for the picked route.

#### Scenario: Scoring a second entry to the same top score lists both as my favourite
- **WHEN** one entry holds my highest score of 9, and I score a second entry 9 from its card
- **THEN** both are listed as my favourite immediately, with reorder controls, without reloading the page

#### Scenario: Raising one entry above a tie narrows the list
- **WHEN** three entries tie for my highest score and I raise one of them to 10
- **THEN** only that entry is listed as my favourite

#### Scenario: An edit on a non-default route updates the stats box
- **WHEN** I switch to a non-default route and then mark one of its entries Completed with a score
- **THEN** my favourite, entries completed, time watched, time left and the progress bar all update, exactly as they would on the default route

#### Scenario: Reordering favourites on a non-default route
- **WHEN** I switch to a non-default route and move a tied favourite to the top
- **THEN** it is listed first, and it is still first after the save

#### Scenario: Switching routes does not resurrect a pre-edit figure
- **WHEN** I edit an entry's score, switch to another route, and switch back
- **THEN** every figure still reflects the edit rather than the value the server last delivered

#### Scenario: Recording a rewatch updates the rewatch and time figures
- **WHEN** I set a main-line entry's rewatch count to 1 from its card
- **THEN** the "Most rewatched" stat names that entry with a count of 1 and time watched grows by one run of it, without a reload

#### Scenario: Completing an entry updates entries completed
- **WHEN** I mark the last unfinished main-line entry Completed
- **THEN** the entries-completed stat reads the full main-line count and the header badge reads "Completed"

#### Scenario: Related entries stay out of the recomputed averages
- **WHEN** a series' More section shows related entries alongside its real extras and I edit one of the real extras' scores
- **THEN** the "Everything" averages are recomputed over the main line and the real extras only, on the same member basis the server delivered them

## MODIFIED Requirements

### Requirement: Watch order and series root
The system SHALL order main-line entries in **story order**: a topological ordering over the `sequel`/`prequel` edges among the main-line members, so an entry always precedes the entries it is a prequel to. The system SHALL present that ordering as the series' watch order, rendering the main-line cards in it. The page SHALL NOT print a position number on a card: the sequence of the cards already carries the order, and a number badge on every card only restates it.

Where the main line contains a **version slot** — main-line entries that are alternative versions of one another, per the `series-versions` capability — the ordering SHALL be computed with each slot contracted to a single position, so every alternative of a slot takes the same position and a branch entry is ordered around the slot rather than always after it. The entries actually shown for the picked alternative SHALL be rendered as one continuous sequence, with no gap left where an unpicked branch's entries would have been.

Aired-from date SHALL be a tie-break, not the ordering: where two entries are unconstrained relative to each other — neither reachable from the other over the chain — the earlier aired-from date SHALL come first, entries lacking a date SHALL be placed last, and MAL id SHALL break the remaining tie. An entry with no chain edge at all SHALL therefore be placed purely by its aired date, interleaved with the chain.

The main line is deliberately **story order, not release order**. A prequel film released after the season it precedes belongs before that season in a watch order, and ordering by air date puts it after — the chain edges the main line is already classified from state the correct order and were previously discarded at the ordering step.

Where the chain edges contain a cycle, so that no topological order exists — including a cycle introduced by contracting a version slot — the system SHALL break the cycle in favour of aired-from order and produce a stable ordering rather than failing the build. Extras remain ordered by aired-from date within their **relation group**, as specified under "Main line and extras".

The root SHALL be the first entry of the series' watch order, and the series SHALL take its title and its main picture from that root. The root SHALL NOT change with the picked alternative, so a series' header is stable however the watch order is filtered.

#### Scenario: A prequel film released later still sorts first
- **WHEN** a series contains Jujutsu Kaisen (aired 2020-10-03) and Jujutsu Kaisen 0 (aired 2021-12-24), and MAL states `40748 --prequel--> 48561` on both ends
- **THEN** Jujutsu Kaisen 0 is first in the watch order and Jujutsu Kaisen is second

#### Scenario: Chain order beats air date
- **WHEN** two main-line entries are linked by a `sequel`/`prequel` edge whose direction disagrees with their aired-from dates
- **THEN** the chain edge decides their order

#### Scenario: No position numbers on the cards
- **WHEN** I open any series
- **THEN** no main-line card carries a watch-order number, and the cards' left-to-right sequence is the watch order

#### Scenario: Alternatives share a position
- **WHEN** two main-line entries form a version slot
- **THEN** they occupy one position in the watch order rather than two consecutive ones

#### Scenario: The header does not move with the picker
- **WHEN** I switch the picked alternative of a version slot
- **THEN** the series' title and main picture are unchanged

#### Scenario: Unconstrained entries fall back to air date
- **WHEN** two main-line entries have no chain path between them
- **THEN** the one that aired first is ordered first, with MAL id breaking a remaining tie

#### Scenario: An entry with no chain edge is placed by date
- **WHEN** a main-line entry carries no `sequel`/`prequel` edge to any other member
- **THEN** it is placed among the others by its aired-from date

#### Scenario: Ordinary series are unaffected
- **WHEN** I open a series whose entries aired in 2013, 2015, a movie in 2016, and 2019, each chaining to the next
- **THEN** the main-line cards are shown in that same order, left to right, and none of them carries a number

#### Scenario: A cyclic chain still renders
- **WHEN** MAL's relations put two main-line entries in a `sequel`/`prequel` cycle
- **THEN** the series still renders its main line, ordered by aired-from date where the cycle is broken

#### Scenario: Series picture and title come from the first entry
- **WHEN** I open a series whose earliest story-order main-line entry is its first season
- **THEN** the page's main picture and the series title are that first season's

#### Scenario: Two separate tellings have their own roots
- **WHEN** I open each of two series joined only by a version relation
- **THEN** each page shows its own first entry as its picture and title, not the other's

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

The highest MAL score SHALL be shown in full rather than blurred when the entry holding it is one I have marked **Completed** or **Dropped** in my list. Both statuses settle my relationship with that entry — dropping a show is as much a decision about it as finishing one — so neither leaves a viewing ahead of me that naming the series' best entry could spoil. My having scored that entry SHALL NOT be required: a dropped entry frequently carries no score of mine, and withholding the stat until one exists would hide it indefinitely. Until the entry reaches one of those two statuses, the page SHALL withhold that entry's title and link entirely — not only its score — so an entry I have not settled is never named by this stat; this withholding applies regardless of the hide-scores toggle's own state, since it protects against spoiling which entry is best rather than against exposing a score value.

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
- **WHEN** the hide-scores toggle is on and the highest-MAL-scored entry is one I have completed
- **THEN** its title, link, and MAL score are shown in full

#### Scenario: Highest MAL score of a dropped entry is shown
- **WHEN** the highest-MAL-scored entry of a series is one I have marked Dropped, and I never gave it a score of my own
- **THEN** the stat names that entry, links to it, and shows its MAL score, exactly as it would for a completed entry

#### Scenario: An unsettled entry's title is withheld from Highest MAL score
- **WHEN** the entry holding the series' highest MAL score is one I am Watching, have On-hold, Plan to watch, or do not have in my list at all
- **THEN** the page shows no title or link for that entry in the Highest MAL score stat, regardless of whether the hide-scores toggle is on or off

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

### Requirement: Series figures follow the picked route
Where a series' main line holds one or more version slots, the page's figures SHALL take two different member scopes.

The **score averages** — MAL's and mine, across the main line and across all entries, as "Series score averages" defines — SHALL be computed over **every** main-line entry, every alternative included, whatever is picked. They SHALL NOT change when the picker does, so the figure that describes the franchise stays stable.

Every other main-line figure "Series stats" defines — the main-line episode total, the main-line runtime total, episodes aired, my watched episodes and watched time, my rewatched time, entries completed, the lower-bound marker on an unknown episode count, whether the main line is settled by me, and the longest gap — SHALL be computed over the **trunk plus the picked alternatives and their branches**, so that time left describes the route I chose rather than counting every retelling of the same story.

Those figures SHALL be delivered with the series for each admissible combination of picks, so switching a picker changes them without a further request. The number of combinations SHALL be capped; beyond the cap, slots after the first SHALL keep their default alternative's figures while the picker still changes which entries are shown.

The delivered figures describe the series as the server last read it. Where an edit made on the page has since changed one of them, the page SHALL show the edited value on **every** route rather than the delivered one, per "The stats box stays live after an in-place edit".

The figures a series carries outside its own page — the browser card, the profile's Top series, and search — SHALL use the default combination.

A series whose main line has no version slot SHALL be unaffected: every figure covers its whole main line, exactly as before.

#### Scenario: Averages hold still across a switch
- **WHEN** I switch between two routes of a series
- **THEN** the MAL and my score averages are unchanged

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

### Requirement: Favourite ordering within a series
When several entries tie for my highest score in a series, the page SHALL let me order them by hand, so that which of them is really my favourite is recorded rather than decided by watch order.

The order SHALL be the same order the `anime-ranking` capability's ranking holds for that score: entries tied for my highest score in this series SHALL be listed in ascending rank order, exactly as the ranking editor would arrange them. This order is therefore not stored per series and is not scoped to series membership — it SHALL survive a series rebuild and a route switch untouched, and it SHALL NOT be discarded by anything this capability does. Reordering it here SHALL be reflected in the ranking editor and in every other ordering the `anime-ranking` capability's "Every by-my-score ordering reads the ranking" requirement lists, and a reorder made there SHALL likewise be reflected here.

Reordering SHALL be offered only when two or more entries are tied. An entry with no rank in the ranking (never hand-ordered) SHALL rank after every ranked entry of the tie.

Moving one tied entry past its neighbour SHALL reposition that entry to sit immediately beside the neighbour it was moved past, within the ranking's shared score tier — not merely trade rank numbers with it while leaving every anime between them, of this series or any other, undisturbed. The tie itself SHALL be re-derived as my scores change: scoring a further entry to the current top score SHALL add it to the list, placed by its own rank among the others, without a reload; raising one entry above the tie SHALL leave that entry listed alone.

Reordering SHALL be offered, SHALL take effect, and SHALL persist on whichever route of the series is picked, not only on the default one.

An ordering that fails to save SHALL leave the page showing the order that is actually stored, rather than a local order the server does not have.

#### Scenario: Reordering tied favourites
- **WHEN** three entries tie for my highest score and I move the third to the top
- **THEN** it is listed first as my favourite, and it is still listed first when I reload the page

#### Scenario: A distant favourite is pulled adjacent, not swapped in place
- **WHEN** two tied favourites sit far apart in the ranking's shared score tier, with other anime between them, and I move the lower one above the higher one
- **THEN** it is repositioned to sit immediately beside the other in the ranking, rather than trading rank numbers with it and leaving the anime between them where they were

#### Scenario: Favourite order survives a rebuild
- **WHEN** I have ordered my tied favourites and then use the Rebuild control
- **THEN** the entries that are still members keep the order I gave them

#### Scenario: No reordering without a tie
- **WHEN** one entry alone holds my highest score in a series
- **THEN** no reorder controls are shown

#### Scenario: A new tie appears without a reload
- **WHEN** one entry holds my highest score and I give a second entry the same score
- **THEN** both are listed as my favourite, ordered by their rank, with reorder controls, without reloading the page

#### Scenario: A newly tied entry is placed by its own rank
- **WHEN** two tied favourites are already ordered and a third entry joins the tie
- **THEN** the third is inserted among the other two according to its own rank in the ranking, rather than simply appended after them

#### Scenario: Reordering while a non-default route is picked
- **WHEN** I pick a route other than the default and reorder my tied favourites
- **THEN** the new order is shown immediately and survives a reload

#### Scenario: A reorder here moves the same ranking everywhere else
- **WHEN** I reorder tied favourites from a series page
- **THEN** the same new order is reflected in the ranking editor and in every other place ordered by my score

#### Scenario: A failed save does not stick
- **WHEN** I reorder my favourites and the save fails
- **THEN** the page returns to the previously stored order

### Requirement: A More group's heading opens that group in full

Each More group's heading SHALL be the control that opens that group, with collapse as its off state. Activating a heading SHALL show **every** extra in that group — including the extras not in my list — unless the group is already showing every one of them, in which case it SHALL collapse the group so that none of its tiles is rendered.

Opening a group this way SHALL exempt that group, and only that group, from the "in my list" filter: every other group SHALL keep showing exactly what it was showing. An exempted group SHALL stay exempt until the filter is turned back on.

While the "in my list" filter is off, a group has nothing to be exempted from, so a heading SHALL simply expand and collapse its group.

The "in my list" control SHALL report itself as **on** only while the filter is in force across every group — that is, while it is on and no group has been opened in full. Opening any group in full SHALL therefore make that control read as off, so the section never reports itself as filtered while showing a group whole.

The "in my list" control SHALL govern **what an expanded group shows**, and SHALL NOT change any group's collapsed state in either direction. Activating it while it reads as off SHALL turn the filter on and drop every group's exemption, so every expanded group returns to showing only the extras in my list. Activating it while it reads as on SHALL show every extra of every expanded group. Because the section opens with its groups collapsed, coupling this control to collapse would make turning the filter on hide everything; the expand/collapse-all control is the one that changes collapse.

#### Scenario: Opening a group that holds nothing of mine

- **WHEN** the filter is on, a group holds seven extras of which none is in my list, and I activate that group's heading
- **THEN** all seven of its tiles are shown, every other group keeps showing what it was showing, and the "in my list" control now reads as off

#### Scenario: The heading collapses a group it has opened

- **WHEN** I activate the heading of a group that is showing all of its extras
- **THEN** that group renders no tiles at all

#### Scenario: Opening a group that holds some of mine

- **WHEN** the filter is on, an expanded group holds six extras of which three are in my list, and I activate that group's heading
- **THEN** all six of its tiles are shown, rather than the three the filter was showing

#### Scenario: Opening one group leaves the others alone

- **WHEN** I open one group in full while the filter is on
- **THEN** every other group is unchanged — an expanded one still shows only the extras in my list with its own hidden-count control if it has one, and a collapsed one is still collapsed

#### Scenario: Turning the filter back on re-filters without collapsing

- **WHEN** a group has been opened in full and I activate the "in my list" control, which reads as off
- **THEN** every group's exemption is dropped and every expanded group shows only the extras in my list, no group's collapsed state has changed, and the control reads as on again

#### Scenario: Turning the filter off does not collapse anything

- **WHEN** the filter reads as on, some groups are expanded and some are collapsed, and I activate the "in my list" control
- **THEN** each expanded group now shows every one of its extras, each collapsed group is still collapsed, and the control reads as off

#### Scenario: The heading is a plain toggle while the filter is off

- **WHEN** the "in my list" filter is off and I activate a group's heading twice
- **THEN** that group collapses and then shows all of its extras again

#### Scenario: A collapsed group offers no hidden-count control

- **WHEN** a group is collapsed
- **THEN** it shows its heading and entry count alone, with no control naming how many tiles are hidden

### Requirement: Main series and More sections
The main line's presentation SHALL be governed by the Series timeline ribbon requirement, not by this one; this requirement governs only the More section, where extras SHALL be presented as poster tiles grouped by their relation to the main line, so the extras read as a different kind of thing from the chronological main line and each group states how its entries stand to the franchise.

Each tile SHALL carry the information a main-line card carries — picture, title, media type, year, episode count, MAL score, my score, and my list status — SHALL link to that anime's detail page, SHALL offer the same edit control, and SHALL show its entry's rewatch count when it is greater than zero. As on a main-line card, a tile's title SHALL reserve the same vertical space regardless of line count, so tiles in the same row stay aligned. Because groups no longer share a media type, each tile's media type SHALL be legible on the tile itself.

Each of a tile's secondary text lines — the media type/year/episode count line, and the aired-progress line when it is shown — SHALL occupy exactly one line whatever its content, truncating with an ellipsis rather than wrapping, so no tile is made taller than its row neighbours by the length of its own text. The grid SHALL size its columns so that a tile whose picture is portrait is wide enough to show that meta line in full for the ordinary worst case — a two-word media type such as `TV special`, a four-digit year, and a two-digit episode count — so an extra's episode count is not the part that gets truncated away. Ellipsis truncation remains the backstop for longer content, not the normal outcome.

Each More group SHALL show its entry count in its heading, counting the entries the media-type filter currently admits.

The More section SHALL offer three section-wide controls: an "in my list" filter, an expand/collapse-all control, and the media-type filter buttons its own requirement defines. Which extras are visible SHALL be governed by those controls and the group headings alone — the section SHALL NOT force any extra to stay visible on the user's behalf, whatever its status or progress, and SHALL NOT vary its initial state with how many extras the series has.

**Every group SHALL render collapsed when a series page is opened.** The section SHALL therefore open as a column of relation-group headings, each carrying its count, with no tiles rendered at all — a franchise with a dozen relation groups is not made to fill the page before the reader has asked for any of it. A group opens from its own heading, from the expand/collapse-all control, or by selecting a media type it holds, per the media-type filter requirement.

The "in my list" filter SHALL be on when a series page is opened. It governs what an **expanded** group shows: only the extras that are in my list — whatever their status: Watching, Completed, On hold, Plan to watch, or Dropped alike — hiding every extra that is not in my list. It SHALL be a two-state control that reports which state it is in, per "A More group's heading opens that group in full", and SHALL NOT change any group's collapsed state.

The expand/collapse-all control SHALL read "Expand" while anything is hidden — whether by the filter, by a collapsed group, or by both — and activating it SHALL show every extra of every group, expanding them all and turning the filter off. It SHALL therefore read "Expand" on a freshly opened series page, whose groups are all collapsed. Once every extra is shown it SHALL read "Collapse", and activating it SHALL collapse every group so that no tile is rendered at all, including the extras in my list. It SHALL read "Expand all"/"Collapse all" when the series has more than one More group, and "Expand"/"Collapse" without the word "all" when it has exactly one group, since "all" is meaningless applied to a single category.

Both controls SHALL always be offered while the series has extras, whatever my statuses across them.

Each More group SHALL additionally be collapsible on its own from its heading, per "A More group's heading opens that group in full", and a collapsed group SHALL NOT render its tiles, so a franchise with many extras cannot make the page arbitrarily long. A group that is showing at least one tile while the filter hides the rest SHALL offer a control naming how many are hidden, which reveals that group's remaining tiles without changing what any other group shows. A group showing no tiles at all — because it is collapsed, or because nothing in it is in my list — SHALL NOT offer that control: its heading opens it, and its entry count is already in the heading.

An edit saved from a tile SHALL update it in place without reloading the page.

#### Scenario: Extras grouped in More by relation
- **WHEN** a series has two recap specials, one side-story OVA, and one alternative version
- **THEN** the More section shows a collapsible "Summary", "Side story" and "Alternative version" group, each heading carrying its count, and their poster tiles once opened

#### Scenario: Media type is legible on the tile
- **WHEN** one "Side story" group holds an OVA, a movie and a special
- **THEN** each tile states its own media type

#### Scenario: A tile's episode count is not truncated away
- **WHEN** a portrait-pictured extra is a `TV special` that aired in 2003 and has 99 episodes
- **THEN** its tile shows `TV special · 2003 · 99 ep` in full at the grid's narrowest column width

#### Scenario: A long meta line stays on one line
- **WHEN** a tile's media type, year, and episode count are longer still than that worst case
- **THEN** the line truncates with an ellipsis on one line, and the tile's score chips and footer stay aligned with the other tiles in its row

#### Scenario: Opening a series shows the headings alone
- **WHEN** I open a series with twenty extras across five relation groups, four of the extras being in my list
- **THEN** all five groups are collapsed with their counts in their headings, no tile is rendered, and the all-groups control reads "Expand all"

#### Scenario: Opening one group shows only my own extras in it
- **WHEN** I then activate the heading of one of those groups
- **THEN** that group shows every one of its extras and reads as exempt from the filter, while the other four stay collapsed

#### Scenario: Expanding shows everything
- **WHEN** I activate the all-groups control on a freshly opened series
- **THEN** all twenty extras are shown, the "in my list" filter reads as off, and the control now reads "Collapse all"

#### Scenario: Collapsing hides my own extras too
- **WHEN** every extra is shown and I activate the "Collapse all" control
- **THEN** no tiles are rendered in any group, including the extras in my list, and the control reads "Expand all" again

#### Scenario: Filtering back to my list
- **WHEN** every extra is shown and I turn the "in my list" filter on
- **THEN** each group is still expanded and shows only its extras that are in my list, and the all-groups control reads "Expand all"

#### Scenario: A group with nothing of mine in it
- **WHEN** the filter is on, a group is expanded, and it holds no extras that are in my list
- **THEN** that group shows its heading and entry count with no tiles and no hidden-count control, and its heading opens it in full

#### Scenario: Revealing one group's hidden extras
- **WHEN** the filter is on, an expanded group is showing the extras of mine it holds while hiding others, and I activate that group's hidden-count control
- **THEN** that group shows all of its tiles while every other group keeps showing only my own extras

#### Scenario: Controls are offered whatever my statuses
- **WHEN** every extra in a series is marked Completed in my list
- **THEN** the "in my list" filter and the all-groups control are both still offered, and expanding then collapsing hides those completed extras

#### Scenario: A large More section is not treated differently
- **WHEN** I open a series with twenty extras and one with four extras
- **THEN** both open with every group collapsed and the filter on, rather than one of them starting expanded

#### Scenario: Editing from a tile
- **WHEN** I use a tile's edit control and save a new score
- **THEN** the same entry editor used elsewhere in the app opens, and the tile and the series stats reflect the new score without a page reload

#### Scenario: An extra added to my list from its tile stays visible
- **WHEN** the filter is on, I add an extra to my list from a revealed tile, and the section re-renders
- **THEN** that extra is now one of the tiles the filter keeps

#### Scenario: A series with no extras
- **WHEN** every member of a series is main line
- **THEN** the More section is not shown

#### Scenario: Singular wording for one extras category
- **WHEN** a series has extras in only one relation group
- **THEN** the all-groups control reads "Expand" or "Collapse" without the word "all"

#### Scenario: Plural wording for more than one extras category
- **WHEN** a series has extras across two or more relation groups
- **THEN** the all-groups control reads "Expand all" or "Collapse all"

#### Scenario: A rewatched extra shows its count
- **WHEN** an extra has a rewatch count of 1
- **THEN** its tile shows a rewatch indicator reading 1

### Requirement: The More section offers media-type filter buttons
Above the More section the page SHALL offer one button per media type present among that series' extras and related entries — TV, Movie, OVA, ONA, Special, Music, PV, and any other type those entries carry — as a multi-select set.

Selecting a type SHALL narrow every group to the entries of that type. Selecting several SHALL show the entries of any selected type. Selecting none SHALL narrow nothing, which is the state a freshly opened page is in.

**Selecting a type SHALL open every group that holds at least one entry of that type**, so the entries it admits are actually rendered rather than merely counted in a heading. Because the section opens with every group collapsed, a type filter that only narrowed the groups would tell the reader which relation group holds a music entry without ever showing the entry itself. A group opened this way SHALL be opened exactly as the expand/collapse-all control opens one: its collapsed state becomes expanded and stays that way until something collapses it. It SHALL NOT be exempted from the "in my list" filter — only a group heading grants that exemption. Deselecting a type SHALL NOT collapse anything.

The type filter SHALL compose with the "in my list" filter and with each group's collapsed or opened state rather than replacing them: an entry is shown when its type is admitted **and** the other controls admit it. Each group's heading count SHALL report the entries the type filter admits.

While at least one type is selected, a group left with no admitted entries SHALL NOT be rendered at all, since a column of empty headings across a dozen relation groups tells the reader nothing.

The type buttons SHALL NOT narrow the main-line timeline. The main line is a watch order whose left-to-right sequence is its meaning, and hiding one of its entries would misstate the series.

#### Scenario: One type narrows every group
- **WHEN** I select "Movie"
- **THEN** every group holding a movie is opened and shows its movies, and groups holding no movie are not rendered

#### Scenario: Selecting a type opens the collapsed groups holding it
- **WHEN** every group is collapsed, as on a freshly opened series page, and I select "Music"
- **THEN** every group holding a music entry is expanded and renders the music entries the other controls admit, rather than showing its heading alone

#### Scenario: Several types are additive
- **WHEN** I select "Movie" and then "OVA"
- **THEN** every group shows its movies and its OVAs, and the groups holding an OVA are opened as well

#### Scenario: Deselecting the last type restores everything
- **WHEN** I deselect the only selected type
- **THEN** every group shows what the other controls admit, and the groups the type filter opened stay open

#### Scenario: The timeline is untouched
- **WHEN** I select "Movie" on a series whose main line is four TV seasons
- **THEN** the timeline still shows all four, in watch order

#### Scenario: Only present types are offered
- **WHEN** a series' extras and related entries hold no music entry
- **THEN** no "Music" button is offered

#### Scenario: The type filter composes with the list filter
- **WHEN** the "in my list" filter is on and I select "OVA"
- **THEN** the groups holding OVAs are opened and show only the OVAs that are in my list, each offering its hidden-count control if it hides others

#### Scenario: Counts follow the type filter
- **WHEN** a group holds six entries of which two are movies and I select "Movie"
- **THEN** that group's heading reports two

### Requirement: The More section's view state is restored with the page
The More section's four view controls — the "in my list" filter, the media-type filter, each group's collapsed state, and each group's exemption from the "in my list" filter — together with the **alternative picked for each version slot** SHALL be part of the series page's restorable state, restored on back/forward navigation exactly as every other page's view controls are, per the `page-state-restoration` capability.

Returning to a series page by back/forward navigation SHALL therefore show the page as it was left: a group opened in full is still open, a collapsed group is still collapsed, the media types I selected are still selected, the "in my list" control still reports the state it reported when the page was left, and the route I picked is still picked.

A fresh visit — a link, a typed URL, a reload — SHALL still open the section on its documented default: the "in my list" filter on, no media type selected, **every group collapsed**, no group exempted, and every version slot on the default alternative the `series-versions` capability defines.

A group key held in restored state that matches no group the restored page renders — because the series' extras changed between the two renders — SHALL be ignored rather than treated as an error. A restored media type that no extra of the series carries SHALL likewise be ignored, as SHALL a restored pick naming an anime that is no longer an alternative of any slot.

The state SHALL NOT be persisted beyond the browser tab's application session, and SHALL NOT be shared between two different series' pages.

#### Scenario: An opened group is still open on return
- **WHEN** I open a More group in full, open one of its extras, and navigate back
- **THEN** that group is still showing all of its tiles, and the "in my list" control still reads as off

#### Scenario: The filter is not reset by a round trip
- **WHEN** I turn the "in my list" filter off, expand a group, open an entry, and navigate back
- **THEN** the filter is still off and that group still shows every one of its extras

#### Scenario: Selected media types survive a round trip
- **WHEN** I select "Movie" and "OVA", open an entry, and navigate back
- **THEN** both are still selected and the section shows the same tiles it showed before

#### Scenario: A picked route survives a round trip
- **WHEN** I pick a route other than the default, open one of its entries, and navigate back
- **THEN** that route is still picked and the same main-line entries are shown

#### Scenario: An expanded group is still expanded on return
- **WHEN** I expand a group, navigate away, and navigate back
- **THEN** that group still renders its tiles

#### Scenario: A fresh visit still opens on the default
- **WHEN** I reach a series page by following a link rather than by navigating back
- **THEN** the "in my list" filter is on, no media type is selected, every group is collapsed, no group is exempted, and every version slot is on its default alternative

#### Scenario: A stale pick is ignored
- **WHEN** restored state names a picked anime that the rebuilt series no longer holds as an alternative
- **THEN** the slot opens on its default rather than failing

#### Scenario: Two series do not share More-section state
- **WHEN** I open one series and expand its More section, then open a different series
- **THEN** the second series' More section opens with every group collapsed, unaffected by the first

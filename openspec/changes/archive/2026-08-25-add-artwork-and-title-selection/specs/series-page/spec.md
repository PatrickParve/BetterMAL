## MODIFIED Requirements

### Requirement: Series stats
The series page SHALL show, for the main line: total episode count, total runtime, and my progress through it — episodes watched against total, entries completed against total, time watched, and time left to finish. Extras' episode count and runtime SHALL be reported separately rather than folded into the main-line totals.

Wherever this page counts **my watched main-line episodes** — the progress bar's watched fill, the named watched figure beside it, and therefore time left — a main-line entry marked **Rewatching** SHALL count as fully watched, as the greater of its own episodes-watched figure and its aired-episode figure. This is the same rule the header's personal badge applies, so the badge and the progress figures directly beside it can never disagree about the same entry. It governs only the watched side of each pair: the episode total, the aired figure, and the runtime describe the anime rather than me, and SHALL be unchanged by a rewatch in progress. It does not multiply anything by a rewatch count: a rewatched entry still counts **once** in these figures. Time watched is defined separately below and does count rewatch runs; time left continues to subtract only these once-counted figures, so a rewatch in progress can never drive it negative or make a half-watched franchise read as finished.

The entries-completed stat SHALL cover the extras as well as the main line, as two separately labelled figures within one stat: how many main-line entries I have completed out of the main-line total, and how many extras I have completed out of the extras total. The two SHALL NOT be summed into a single figure, so which half of the series is unfinished stays visible. When the series has no extras, the extras figure SHALL be omitted and the stat SHALL show the main-line figure alone rather than an "0 of 0". An entry marked Rewatching SHALL count as completed in this stat, since a rewatch can only follow a completed run.

**Time watched SHALL count rewatches.** It SHALL be the main line's once-counted watched time — the figure the paragraph above governs — plus the time spent rewatching that main line: for each main-line entry, one further complete run of it for every recorded rewatch, plus the episodes watched so far in a run still in progress. This is the definition the profile page's watch time already uses, so the two pages SHALL NOT describe the same hours differently. A twelve-episode season I have completed, rewatched twice, and am two episodes into rewatching a third time SHALL therefore contribute 38 episodes of time watched, not 12.

Rewatch time SHALL be counted over the **main line only**, matching every other figure in this stat box and matching what time left subtracts. Time watched MAY exceed the main line's runtime as a result, which is correct rather than an error; time left SHALL NOT be derived from it.

The two time stats SHALL be shown independently of one another. **Time watched** SHALL be shown whenever it is non-zero, including on a series with nothing left to watch — a finished franchise that has been rewatched is precisely the case where the figure says something the entries-completed and progress figures do not. **Time left** SHALL be shown only while there is time left: when it computes to zero — I have watched at least as much of the main line as its runtime accounts for — it SHALL NOT be rendered, since "0min left" restates what the badge and the progress bar already say. The page SHALL therefore show time watched with time left hidden.

That withholding of time left SHALL NOT apply when the main-line runtime is itself unknown — a zero runtime total that the page already marks as unknown rather than as an exact figure. A zero time left derived from a runtime nobody knows reports missing data, not a series I have finished, so time left SHALL still be shown in that case.

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
- **THEN** the page shows a progress bar with my 38 watched episodes and the 62 total each named, how many entries I have completed, my time watched, and the time left to finish

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

#### Scenario: A finished series hides time left but keeps time watched
- **WHEN** I open a series whose main line I have watched in full, so no time is left
- **THEN** "Time left" is not shown, and "Time watched" is still shown

#### Scenario: A completed rewatch adds to time watched
- **WHEN** a series' main line totals 62 episodes averaging 24 minutes, I have completed all of it, and one 12-episode season carries a rewatch count of 2
- **THEN** time watched covers 86 episodes rather than 62, and time left is still not shown

#### Scenario: A rewatch in progress counts the episodes watched so far
- **WHEN** a 12-episode season I completed is marked Rewatching with two episodes watched and a rewatch count of 2
- **THEN** that entry contributes 38 episodes to time watched — its original run, its two completed rewatches, and the two episodes of the run in progress

#### Scenario: A rewatch does not move time left
- **WHEN** I have watched 38 of a series' 62 main-line episodes and then rewatch a completed 12-episode season twice
- **THEN** time left is exactly what it was before the rewatches, while time watched has grown by those 24 episodes

#### Scenario: Extras' rewatches stay out of the figure
- **WHEN** an extra (not a main-line member) carries a rewatch count of 3
- **THEN** the series' time watched is unchanged by it

#### Scenario: A part-watched series keeps both time stats
- **WHEN** I open a series with main-line episodes I have not yet watched
- **THEN** both "Time watched" and "Time left" are shown

#### Scenario: An unknown runtime is not mistaken for a finished series
- **WHEN** I open a series whose main-line runtime total is unknown, so it reports zero time left without my having watched it through
- **THEN** both "Time watched" and "Time left" are still shown, because the zero reflects a runtime nobody knows rather than a series I have finished

#### Scenario: A series I have not started shows no time watched
- **WHEN** I open a series with no main-line entry in my list
- **THEN** "Time watched" is not shown, and "Time left" is shown as the whole main-line runtime

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


## ADDED Requirements

### Requirement: The series page header carries picture and title controls
The series page header SHALL carry a **Choose picture** control and a **Choose title** control for the series it is showing.

**Choose picture** SHALL be rendered only when the series has more than one picture to choose between, and SHALL open the series picture picker the `artwork-selection` capability defines. A series with a single picture available SHALL show no control at all, rather than a control that opens onto one image.

**Choose title** SHALL always be rendered, since a title can always be trimmed even when only one is offered. It SHALL open a picker listing every title offered by the `series-identity` capability, together with a text field for a trimmed title, and SHALL refuse to submit a title that capability's rule rejects.

Both controls SHALL affect the series only. Neither SHALL change any member anime's own picture or title, and neither SHALL cause anything to be written to MyAnimeList.

Choosing a picture or a title SHALL take effect on the page without a reload, and SHALL be reflected on every other surface that shows this series the next time it is read.

#### Scenario: A multi-picture series offers the control
- **WHEN** I open a series whose main-line members between them offer six distinct pictures
- **THEN** the header shows a "Choose picture" control, and clicking it opens a picker of those six pictures

#### Scenario: A single-picture series shows no picture control
- **WHEN** I open a series whose main-line members offer exactly one distinct picture between them
- **THEN** the header shows no "Choose picture" control

#### Scenario: The title control is always available
- **WHEN** I open any series
- **THEN** the header shows a "Choose title" control

#### Scenario: A chosen picture applies immediately
- **WHEN** I pick a picture from the series picker
- **THEN** the header's picture changes to it without a page reload

#### Scenario: Choosing does not touch the members
- **WHEN** I choose a picture for a series
- **THEN** no main-line member's own displayed picture changes, and nothing is pushed to MyAnimeList

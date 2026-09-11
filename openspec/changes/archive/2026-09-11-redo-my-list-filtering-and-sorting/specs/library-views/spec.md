## MODIFIED Requirements

### Requirement: My list filter bar
The system SHALL present every my-list filter and sort control in one controls block directly below the status filter tabs, above the entries it applies to. The block SHALL appear exactly once on the page — never repeated per status group — and SHALL apply to every group on screen alike.

The block SHALL be divided into two groups, each on its own row and each carrying a visible label:

- a **Filter** group holding the controls that narrow which entries are shown — find in list, the type filter, the airing-status filter, the score filter, and the Started filter — in that order;
- a **Sort** group holding the controls that order and arrange them — the sort key, its direction, the tiebreaker, and the grouping choice — in that order.

No control SHALL sit in the other group's row, and the two groups SHALL stay on separate rows at every viewport width, so narrowing and ordering never read as one strip. Each group SHALL be exposed to assistive technology as a group named by its label.

When a group's controls do not fit on one line they SHALL wrap onto further lines within that group's own row, rather than overflowing, scrolling horizontally, or flowing into the other group. Where the viewport is too narrow to hold a group's label beside its controls, the label SHALL sit above them instead.

Using any control in the block SHALL NOT make another control in the block appear, disappear, or change size. Every control in both groups SHALL be present whatever the others are set to, and a control whose label varies with its value SHALL hold one width across those values, so the controls beside it never shift sideways and no row gains or loses a line because of a selection.

Every control in the block SHALL be rectangular and share the one control height the `page-header-design` capability defines for a filter cluster. The rounded pill shape SHALL be reserved for the status filter tabs, so a control that narrows or orders the list is never mistaken for a status tab.

The page SHALL offer a **Reset filters & sort** action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, the Started filter off, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default. It SHALL sit in the results line described by "My list reports what is being shown" rather than among the controls, and SHALL NOT change the selected status tabs or dismiss a recap scope, which are separate controls.

#### Scenario: One block for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the controls block appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Narrowing and ordering are two labelled groups
- **WHEN** the my-list page renders
- **THEN** a row labelled Filter holds find in list, Type, Airing, Score and Started, and a separate row labelled Sort below it holds the sort key, the direction control, the tiebreaker and the grouping choice

#### Scenario: A group wraps within its own row
- **WHEN** the viewport is too narrow to fit the Filter group's controls on one line
- **THEN** they wrap onto further lines under the Filter label, the Sort group stays on its own row below, and the page does not scroll horizontally

#### Scenario: Labels move above on a narrow viewport
- **WHEN** the viewport is too narrow to hold a group's label beside its controls
- **THEN** each group's label sits above that group's controls, and the two groups remain separate

#### Scenario: Choosing a sort key moves nothing
- **WHEN** I change the sort key from Alphabetical to Currently airing first, and back
- **THEN** no control appears or disappears, and the direction control and the tiebreaker stay exactly where they were

#### Scenario: Only the status tabs are pills
- **WHEN** I look at the page above the list
- **THEN** the status tabs are the only rounded pills, and the Started filter, the direction control and the grouping choice are rectangular controls matching the selects beside them

#### Scenario: Resetting
- **WHEN** I have narrowed or reordered the list and use **Reset filters & sort**
- **THEN** the text query, type, airing-status, score and Started filters are cleared, the sort returns to alphabetical in its natural direction with no tiebreaker, grouping by status is on, and the selected status tabs are left as they were

#### Scenario: Reset hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no **Reset filters & sort** action is shown

#### Scenario: The Started filter counts as off-default
- **WHEN** the Started filter is the only control I have changed
- **THEN** the **Reset filters & sort** action is shown, and using it turns the filter off

#### Scenario: Reset leaves a recap scope in place
- **WHEN** a recap scope is active and I use **Reset filters & sort**
- **THEN** the filters and sort return to their defaults and the list stays scoped to the same recap period

### Requirement: Find in list
The system SHALL provide a text field, first in the my-list Filter group, that narrows the list to entries whose title contains the typed text, matched case-insensitively against both the English title and the original title, so an entry is found under either name. The match SHALL be a substring match, not a prefix-only match. An empty field SHALL impose no restriction. The typed text SHALL combine with every other filter rather than replacing them.

While the field holds text it SHALL offer a clear control inside the field that empties it in one action and leaves focus in the field. The clear control SHALL sit within the field's own bounds, so its appearance neither changes the field's size nor moves the controls beside it.

#### Scenario: Narrowing by title text
- **WHEN** I type text into the find-in-list field
- **THEN** only entries whose English or original title contains that text, ignoring case, remain shown

#### Scenario: Matching the other title
- **WHEN** an entry is displayed under its English title and I type part of its original title
- **THEN** that entry is still shown

#### Scenario: Clearing the text
- **WHEN** I clear the find-in-list field
- **THEN** the text restriction is lifted and the remaining filters continue to apply

#### Scenario: Clearing in one action
- **WHEN** the field holds text and I use its clear control
- **THEN** the field is emptied, focus stays in the field, and the Type filter beside it has not moved

#### Scenario: No clear control when empty
- **WHEN** the find-in-list field is empty
- **THEN** it shows no clear control

### Requirement: My list started filter
The system SHALL provide a **Started** filter in the my-list Filter group that narrows the list to entries I have watched at least one episode of, whatever their status. Off — its default — it SHALL impose no restriction. It SHALL combine with every other filter rather than replacing them.

It SHALL be presented as a checkbox control labelled Started, showing its state as a tick and sharing the Filter group's control height, rather than as a rounded pill shaped like the status filter tabs.

#### Scenario: Narrowing to what I have started
- **WHEN** I turn the Started filter on
- **THEN** only entries with at least one episode watched are shown, whichever status they hold

#### Scenario: Off by default
- **WHEN** the my-list page renders without the filter being set
- **THEN** entries with no episodes watched are shown alongside the rest

#### Scenario: Combining with the type filter
- **WHEN** I turn the Started filter on and select Movie in the type filter
- **THEN** only films I have watched are shown

#### Scenario: Reads as a filter, not a status
- **WHEN** I look at the Started filter
- **THEN** it is a checkbox control in the Filter group rather than a rounded pill like the status tabs, and its tick shows whether it is on

### Requirement: My list two-level sorting
The system SHALL order my list by a primary sort key with an optional tiebreaker key, both chosen in the Sort group, so orderings such as "my score, then MAL score" or "episodes watched, then MAL score" are expressible directly.

Both selectors SHALL offer: Alphabetical, My score, MAL score, Popularity, Episodes watched, Progress, Total episodes, Airing status, Type, Start date, and Finish date. The tiebreaker selector SHALL additionally offer "none", which is its default, and SHALL NOT offer the key already chosen as primary.

**Airing status** SHALL be offered in each selector as three choices, one per status that can come first — **Finished airing first**, **Currently airing first**, and **Not yet aired first** — presented together under an Airing status heading, each worded so it reads unambiguously as the selector's shown value once chosen. Choosing one SHALL select Airing status as that selector's key and set which status comes first; the remaining two statuses SHALL follow it in their established cycle (Finished airing → Currently airing → Not yet aired, wrapping around). This choice SHALL be the only way the first status is chosen: choosing Airing status SHALL NOT make any further control appear. When Airing status is the tiebreaker, the status chosen to come first in the tiebreaker SHALL be the one applied. While Airing status is the primary key, the tiebreaker SHALL offer none of the three Airing status choices.

**Popularity** SHALL order entries by their anime's MAL popularity rank — the popularity figure the app shows for an anime, where rank 1 is the anime with the most MAL members.

Each key SHALL have a natural direction — descending for My score, MAL score, Episodes watched, Progress, Total episodes, Start date and Finish date; most popular first (popularity rank 1 first, ascending by rank) for Popularity; ascending for Alphabetical and Type; and, for Airing status, the order its chosen first status gives.

A direction control beside the primary key SHALL flip the primary key between its natural direction and the reverse. It SHALL name, in words, the order it is currently producing for the current key — **Highest first** / **Lowest first** for My score, MAL score and Progress; **Most first** / **Fewest first** for Episodes watched and Total episodes; **Most popular first** / **Least popular first** for Popularity; **A–Z** / **Z–A** for Alphabetical and Type; **Newest first** / **Oldest first** for Start date and Finish date — rather than acting as a bare "Reverse" toggle, and SHALL hold one width whichever of these it shows. While Airing status is the primary key, the direction control SHALL be shown as unavailable and SHALL convey that the order is set by which status comes first; the list SHALL then follow the chosen status order whatever direction was last set, and choosing another key SHALL resume that direction. The tiebreaker SHALL always apply in its own natural direction.

Entries missing the value being sorted on — no score, no start or finish date, an unknown total, an unknown popularity rank or unknown airing status — SHALL sort last regardless of the direction chosen, rather than leading the list when the direction is flipped. When the primary and tiebreaker keys both tie, entries SHALL fall back to alphabetical order, so the same list always renders in the same order.

Where **My score** is the primary or the tiebreaker key, entries left tied on it SHALL be separated by my ranking — best-ranked first — before that alphabetical fallback, per the `anime-ranking` capability. Rank SHALL apply as a tiebreaker does: always in its own natural direction, so flipping the primary direction to lowest-score-first still orders each score's entries best-ranked first. A tied entry with no rank SHALL sort after every ranked entry of the same score.

#### Scenario: Sorting by my score then MAL score
- **WHEN** I choose My score as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by my score highest first, and entries sharing the same score of mine are ordered among themselves by MAL score highest first

#### Scenario: Sorting by my score alone follows my ranking
- **WHEN** I choose My score as the primary sort with no tiebreaker
- **THEN** entries sharing a score appear in my ranking's order rather than alphabetically

#### Scenario: Ranking breaks a tie the tiebreaker could not
- **WHEN** I sort by My score with MAL score as the tiebreaker and two entries share both scores
- **THEN** they are ordered by my ranking rather than alphabetically

#### Scenario: Sorting by progress then MAL score
- **WHEN** I choose Episodes watched as the primary sort and MAL score as the tiebreaker
- **THEN** entries are ordered by episodes watched highest first, with ties broken by MAL score

#### Scenario: Sorting by popularity
- **WHEN** I choose Popularity as the primary sort
- **THEN** entries are ordered from most popular to least popular — an anime with popularity rank 12 before one with rank 340, and that before one with rank 5,000

#### Scenario: Reversing the popularity sort
- **WHEN** I flip the direction control while sorted by Popularity
- **THEN** entries are ordered from least popular to most popular, and the control reads Least popular first

#### Scenario: Popularity as the tiebreaker
- **WHEN** I choose My score as the primary sort and Popularity as the tiebreaker
- **THEN** entries sharing a score of mine are ordered among themselves most popular first

#### Scenario: Unknown popularity sorts last
- **WHEN** the list is sorted by Popularity, in either direction, and some entries' anime have no popularity rank recorded
- **THEN** those entries appear at the end of the list

#### Scenario: The direction control names the order
- **WHEN** I sort by My score
- **THEN** the direction control reads Highest first, and flipping it makes it read Lowest first and orders the list lowest score first

#### Scenario: The direction control keeps its width
- **WHEN** I switch the sort key between Alphabetical, Popularity and Start date
- **THEN** the direction control reads A–Z, Most popular first and Newest first in turn, and the tiebreaker beside it does not move

#### Scenario: Reversing the primary direction
- **WHEN** I flip the direction control while sorted by My score
- **THEN** entries are ordered by my score lowest first, and the tiebreaker still applies in its own natural direction

#### Scenario: Ranking is not reversed with the primary key
- **WHEN** I flip the direction control while sorted by My score
- **THEN** within each score the entries are still ordered best-ranked first

#### Scenario: An unranked entry among ranked ones
- **WHEN** a scored Plan-to-watch entry shares a score with ranked entries in a my-score sort
- **THEN** it appears after all of them

#### Scenario: Missing values sort last in either direction
- **WHEN** the list is sorted by a key some entries have no value for, in either direction
- **THEN** the entries with no value appear at the end of the list

#### Scenario: Tiebreaker cannot repeat the primary key
- **WHEN** My score is the primary sort key
- **THEN** the tiebreaker selector does not offer My score

#### Scenario: Fully tied entries keep a stable order
- **WHEN** two entries tie on both the primary and tiebreaker keys, and neither carries a rank
- **THEN** they appear in alphabetical order, and that order is the same every time the list renders

#### Scenario: Choosing which airing status comes first
- **WHEN** I choose Currently airing first as the sort key
- **THEN** currently airing entries come first, then not-yet-aired entries, then finished-airing entries, entries with an unknown airing status come last, and no additional control appears

#### Scenario: The airing choice reads as its own value
- **WHEN** I have chosen Not yet aired first and the sort selector is closed
- **THEN** the selector reads "Not yet aired first"

#### Scenario: Direction does not apply to airing status
- **WHEN** any Airing status choice is the primary sort key
- **THEN** the direction control is shown as unavailable and conveys that the order is set by which status comes first

#### Scenario: A reversed direction survives a detour through airing status
- **WHEN** I sort by My score, flip it to Lowest first, switch to Finished airing first, and then switch back to My score
- **THEN** the list is ordered lowest score first again and the direction control reads Lowest first

#### Scenario: Unknown airing status stays last
- **WHEN** I sort by any Airing status choice after having flipped the direction on another key
- **THEN** entries with an unknown airing status appear at the end of the list

#### Scenario: Airing status as the tiebreaker names its own order
- **WHEN** I choose My score as the primary sort and Not yet aired first as the tiebreaker
- **THEN** entries sharing a score of mine are ordered not-yet-aired first, then finished-airing, then currently airing

#### Scenario: Airing status cannot tiebreak itself
- **WHEN** any Airing status choice is the primary sort key
- **THEN** the tiebreaker offers none of the three Airing status choices

### Requirement: My list grouping is an explicit choice
The system SHALL provide a grouping choice in the my-list Sort group that decides whether entries are grouped, rather than inferring it from the sort key. It SHALL be presented as two options side by side — **By status** and **Single list** — with exactly one of them selected, each reporting its own pressed state to assistive technology. **By status** SHALL be selected by default.

With **By status**, entries SHALL appear under the standard status groups (Watching → Rewatching → On hold → Plan to watch → Completed → Dropped), with the active sort applied inside each group and no rank numbers. With **Single list**, entries SHALL appear as one flat list ordered by the active sort. Changing the sort key SHALL NOT change whether the list is grouped, and changing the status tab SHALL NOT reset the sort.

#### Scenario: Both options are shown
- **WHEN** the my-list page renders on a fresh visit
- **THEN** the Sort group shows both By status and Single list, with By status selected

#### Scenario: Grouping on with a score sort
- **WHEN** By status is selected and I sort by my score
- **THEN** entries stay grouped by status, and each group's entries are ordered by my score

#### Scenario: Turning grouping off
- **WHEN** I choose Single list
- **THEN** entries appear as one flat list ordered by the active sort, with no status group headers

#### Scenario: Changing sort leaves grouping alone
- **WHEN** I change the sort key or direction
- **THEN** the list stays grouped or flat exactly as it was

#### Scenario: Changing status tab leaves the sort alone
- **WHEN** I switch from one status tab to another
- **THEN** the primary sort key, tiebreaker and direction are left as they were

### Requirement: My list reports what is being shown
The system SHALL show a results line directly above the list whenever the list has loaded. The line SHALL report a count: while any filter is narrowing the list, how many entries are shown out of the total in the current status selection (for example "Showing 12 of 340"); otherwise that total alone (for example "340 anime"). The numbers SHALL be visually emphasised over the words around them, so the count reads at a glance rather than as faint fine print. A change in the count SHALL be announced to assistive technology without moving focus.

The line SHALL be present whether or not a filter is narrowing the list, so starting to filter never inserts a line above the list and pushes the entries down. It SHALL also carry the recap-scope indicator while a scope is active (per "My list opens scoped to a recap period") and, at its far end, the **Reset filters & sort** action while that is offered (per "My list filter bar"). On a viewport wide enough to hold its contents on one line, the line SHALL keep one height whether or not the indicator or the reset action is shown.

While the list is loading, no count SHALL be shown.

When filters exclude every entry, the system SHALL say that nothing matches the current filters and offer the **Reset filters & sort** action, rather than showing the "nothing here yet" message used for a genuinely empty status. Both states SHALL be presented as a framed block of their own — a primary line set in heading weight and a secondary line of explanation — rather than as a single line of faint body text, and the two SHALL read differently from one another.

#### Scenario: Count while filtered
- **WHEN** a filter narrows the list
- **THEN** the results line reports how many entries are shown out of the total for the active status selection, for example "Showing 12 of 340"

#### Scenario: Count when unfiltered
- **WHEN** no filter is narrowing the list
- **THEN** the results line reports the total alone, for example "340 anime"

#### Scenario: Starting to filter does not push the list down
- **WHEN** I type the first character into the find-in-list field
- **THEN** no line is inserted above the list; the results line's text changes in place

#### Scenario: The reset action does not change the line's height
- **WHEN** I change the sort key on a wide viewport, so the reset action appears
- **THEN** the results line keeps its height and the first entry row stays at the same vertical position

#### Scenario: Filters match nothing
- **WHEN** the active filters exclude every entry
- **THEN** a framed block says nothing matches the current filters, explains that filters can be removed or reset, and offers **Reset filters & sort**

#### Scenario: Genuinely empty status
- **WHEN** the active status tab holds no entries at all and no filter is active
- **THEN** the page shows its "nothing here yet" block, which reads differently from the nothing-matches block and offers no reset

#### Scenario: Nothing counted while loading
- **WHEN** the list is still loading
- **THEN** no count is shown

### Requirement: Recap a period control on my list
The system SHALL offer a **Recap a period** control in the my-list page's header, beside the page title, so that recapping a period is reachable from the list it recaps. The control SHALL be presented as a page action — a rectangular button in the app's small-button style — and SHALL sit outside both the status-tab row and the filter/sort controls block, so it is never mistaken for a status tab or a filter. Opening or dismissing it SHALL NOT change which entries the list is showing.

Activating the control SHALL open an overlay in which the user picks the recap type (multi-year, yearly, or season) and the settings that type needs — the years for a multi-year recap, the year for a yearly one, the year and season for a season one, and the time filter for the multi-year and yearly types. Confirming the selection SHALL apply that period to my list as a recap scope, in place, per "My list opens scoped to a recap period" and the `list-recaps` capability's "Scoping my list to a period from my list"; dismissing the overlay SHALL leave the my-list page exactly as it was.

The overlay SHALL NOT offer a period or time filter that would produce an empty recap, applying the same availability rule the recap itself uses: an option covering no entries is shown as unavailable and cannot be confirmed.

#### Scenario: Opening the recap picker
- **WHEN** I activate **Recap a period** on the my-list page
- **THEN** an overlay opens offering the multi-year, yearly, and season recap types with the settings each needs

#### Scenario: Confirming a period scopes the list
- **WHEN** I pick a yearly recap for 2022 and confirm
- **THEN** I stay on my list, which narrows to the anime the 2022 recap includes, and the scope indicator names 2022

#### Scenario: Dismissing the picker
- **WHEN** I open the overlay and dismiss it without confirming
- **THEN** the my-list page is unchanged — same status filter, same filter and sort selections, same scroll position

#### Scenario: The control sits in the page header
- **WHEN** the my-list page renders
- **THEN** **Recap a period** appears in the header row beside the "My list" title, not in the status-tab row and not among the filter or sort controls, and it is not shaped like a status tab

#### Scenario: Unavailable options cannot be confirmed
- **WHEN** the picker offers a time filter that would include no entries for the selected period
- **THEN** that option is shown as unavailable and cannot be confirmed

### Requirement: My list opens scoped to a recap period
The system SHALL let the my-list page open scoped to a recap's period, time filter, and media type, arriving from the recap's "see all" control, and SHALL then show exactly the anime that recap included — no more and no fewer — whatever their watch statuses.

The scope SHALL be carried in the page URL so it survives a reload and back-navigation, and SHALL be shown on the page as a labelled, dismissible indicator naming the period, time filter, and media type it represents, so a narrowed list is never mistaken for the whole list. The indicator SHALL be a compact chip, one line tall, at the start of the results line described by "My list reports what is being shown", carrying that label, a link to the recap of the same period, filter and media type, and a dismiss control. It SHALL NOT be a full-width banner and SHALL NOT add a row of its own between the status tabs and the controls block. Dismissing it SHALL return the page to the unscoped list without disturbing the status filter, the filter and sort selections, or the grouping choice.

While a recap scope is active the page's own status tabs, filters, and sorting SHALL continue to work, narrowing and ordering within the scoped set rather than escaping it. The results line's count SHALL report against the scoped set.

The page SHALL additionally accept, alongside a scope, a narrowing that names one of the recap's stats or one of its score-distribution rows. Such a narrowing SHALL be applied by setting the page's **own** controls — the status tab, the type filter, the score filter, and the Started filter — to the values that express it, rather than as a second, hidden scope, so it is visible on arrival and can be adjusted or cleared with the page's ordinary controls. Controls the narrowing does not concern SHALL be left at their defaults, and the resulting set SHALL match the number that was followed.

A narrowing SHALL be applied once, on arrival. Changing any of those controls afterwards SHALL take effect and SHALL NOT be reverted, and dismissing the scope SHALL clear the narrowing along with it. Returning to the page with the browser's back or forward buttons SHALL restore the controls as they were left, not as they arrived.

#### Scenario: Arriving from a recap
- **WHEN** I open my list from a fall 2019 recap narrowed to TV
- **THEN** the list shows exactly the TV anime that recap included, across every watch status they hold

#### Scenario: The scope is labelled
- **WHEN** my list is showing a recap scope
- **THEN** a compact chip at the start of the results line names the period, time filter, and media type the scope represents, with a link to that recap and a dismiss control

#### Scenario: The scope does not push the page down
- **WHEN** a recap scope becomes active
- **THEN** the status tabs and the controls block stay where they were, and the scope appears as a chip in the results line rather than as a banner above the controls

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** the full list returns, with my status filter, filter and sort selections, and grouping untouched

#### Scenario: Filtering within a scope
- **WHEN** a recap scope is active and I select the Completed status tab
- **THEN** only completed entries from within the scoped set are shown, not completed entries from the whole list

#### Scenario: Counting within a scope
- **WHEN** a recap scope is active and further filters narrow it
- **THEN** the results line reports the shown and total counts against the scoped set

#### Scenario: The scope survives a reload
- **WHEN** I reload the page, or return to it with the browser's back button
- **THEN** the same recap scope is still applied

#### Scenario: Arriving from a recap stat
- **WHEN** I open my list by following a 2020 recap's **Movies watched** stat
- **THEN** the page arrives scoped to 2020 with its type filter set to Movie and its Started filter on, and shows exactly the films of that period I have watched

#### Scenario: Arriving from a distribution row
- **WHEN** I open my list by following a recap distribution's score-8 row
- **THEN** the page arrives scoped to that period with its score filter set to 8, and its status tab left on All

#### Scenario: The arrival narrowing is adjustable
- **WHEN** I arrive from a recap stat and then change the control it set
- **THEN** the change takes effect and is not reverted, and **Reset filters & sort** restores the page's defaults within the scope

#### Scenario: Dismissing clears the narrowing too
- **WHEN** I arrive from a recap stat and dismiss the scope indicator
- **THEN** the full list returns with the arrival narrowing gone, not reapplied

## ADDED Requirements

### Requirement: My list marks the filters in force
Each control in the my-list Filter group SHALL show, at rest and without being opened, whether it is currently narrowing the list: the find-in-list field while it holds text, the type and airing-status filters while on anything other than **All** (including **None**), the score filter while on anything other than Any, and the Started filter while on. A narrowing control SHALL be drawn with the app's active accent treatment; a control at its default SHALL be drawn neutral. The marking SHALL NOT change any control's size, and SHALL NOT be the only signal of the control's state — each control's own label, value, or tick continues to state it.

Controls in the Sort group SHALL NOT carry this marking, since ordering the list hides nothing.

#### Scenario: A narrowing filter is marked
- **WHEN** I set the score filter to 8 and leave the type filter on All
- **THEN** the score filter is drawn with the active accent and the type filter is drawn neutral

#### Scenario: None counts as narrowing
- **WHEN** I press **None** in the airing-status filter
- **THEN** the airing-status filter is drawn with the active accent

#### Scenario: Returning to the default clears the mark
- **WHEN** I empty the find-in-list field
- **THEN** the field is drawn neutral again

#### Scenario: Marking moves nothing
- **WHEN** I turn the Started filter on and off
- **THEN** no control in the Filter group changes size or position

#### Scenario: Sorting is not marked
- **WHEN** I change the sort key and direction
- **THEN** no Sort group control is drawn with the narrowing accent

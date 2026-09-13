# library-views Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: My list grouped and ordered by status
The system SHALL present my list as one list grouped and ordered as Currently watching → Rewatching → On hold → Plan to watch → Completed → Dropped, where each entry shows picture, title, type (TV/movie), progress, my score, MAL score (respecting the hide/unhide toggle), and an edit button. Rewatching sits directly after Currently watching because both are runs in progress. For entries in the **Plan to watch** group, each row SHALL additionally show an airing-status indicator alongside the type — **Not aired**, **Airing**, or **Aired** (mapped from the anime's `not_yet_aired`, `currently_airing`, and `finished_airing` values) — so the user can tell at a glance whether a queued show is already out, still airing, or has not yet started; when the airing status is unknown, no indicator is shown.

Rows outside Plan to watch SHALL also show the airing-status indicator while the user is working with airing status — that is, while the airing-status filter has a selection or Airing status is the primary sort key — since the indicator is the value being filtered or ordered on. Outside those cases, rows in other status groups SHALL NOT show the indicator.

#### Scenario: Rendering the grouped list
- **WHEN** the my-list page loads
- **THEN** entries appear grouped in the order Currently watching, Rewatching, On hold, Plan to watch, Completed, Dropped, each showing picture, title, type, progress, my score, MAL score, and an edit button

#### Scenario: Rewatching sits with the in-progress groups
- **WHEN** my list holds both Rewatching and Completed entries
- **THEN** the Rewatching group appears directly after Currently watching, not beside Completed

#### Scenario: Plan-to-watch row shows airing status
- **WHEN** the Plan to watch group renders an entry whose anime has a known airing status
- **THEN** that row shows an airing-status indicator (Not aired, Airing, or Aired) next to the type

#### Scenario: Airing status shown while filtering or sorting by it
- **WHEN** the airing-status filter has a selection, or Airing status is the primary sort key
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

#### Scenario: Airing status otherwise only on Plan to watch
- **WHEN** no airing-status filter is selected and the sort is not by airing status
- **THEN** rows outside Plan to watch show the type without an airing-status indicator

#### Scenario: Unknown airing status shows no indicator
- **WHEN** an entry's anime has no known airing status
- **THEN** its row shows the type with no airing-status indicator

### Requirement: Rewatching carries its own status colour

The system SHALL give Rewatching its own colour in the status palette, used everywhere a watch status is colour-coded — the my-list row's status stripe, the status filter tab, and any other status-keyed treatment.

The colour SHALL be a **darker blue than Completed's**, so the two read as related — both are states of an anime the user has finished — while staying clearly distinguishable from one another and from every other status colour.

The pair SHALL remain distinguishable in both the light and the dark palette. Each is defined independently, so satisfying this in one does not satisfy it in the other.

#### Scenario: A rewatching row is identifiable

- **WHEN** my list shows a Rewatching entry
- **THEN** its status stripe carries the Rewatching colour, distinct from every other status colour on the page

#### Scenario: Related to Completed but not confusable

- **WHEN** a Rewatching row and a Completed row are shown together
- **THEN** both read as blues while remaining clearly distinguishable from one another

#### Scenario: Both palettes

- **WHEN** I view my list in the light theme and again in the dark theme
- **THEN** Rewatching and Completed are distinguishable from each other in both

### Requirement: List-row posters fill the row
The system SHALL render the poster in a my-list row and a top-anime row flush with the row's top and bottom edges, filling the row's full height with no padding above or below it, so the poster reads as part of the card rather than an image floating inside it. On a top-anime row, or a my-list row preceded by a rank column, the poster SHALL also sit flush with its leading neighbour (the rank column, or the row's leading edge when there is no rank), still filling the row's height. On a my-list row with no rank shown, the poster SHALL instead sit a small fixed gap after the row's status-colour stripe, rather than flush against it, so the stripe and poster read as two distinct elements.

Rows SHALL keep the height they have without the change: the row does not grow to the poster's natural aspect ratio, so adopting this layout does not lengthen the page. The poster's width MAY grow along with its height to avoid cropping the image more tightly than before. A row whose anime has no picture SHALL render its placeholder at the same full-height size, so rows with and without a picture stay aligned.

Where the anime's displayed picture is **at least as wide as it is tall**, the row SHALL draw it whole at its own proportions on the terms the `artwork-presentation` capability sets out: the row's height is unchanged, the picture takes the width its proportions give it at that height up to that capability's bound, and the row's title and everything after it begin further along by that extra width. This is the one case in which two rows' posters differ in width; a portrait poster keeps exactly the box described above, and the row's height, its progress, score, and edit columns, and its status stripe are unchanged in every case.

#### Scenario: Poster fills a my-list row
- **WHEN** the my-list page renders a row in grouped (unranked) view
- **THEN** that row's poster touches the row's top and bottom edges, with a small fixed gap between the status-colour stripe and the poster's leading edge

#### Scenario: Poster fills a ranked row
- **WHEN** a my-list or top-anime row shows a rank number before its poster
- **THEN** the poster still touches the row's top and bottom edges, sitting after the rank column rather than at the leading edge

#### Scenario: Row heights are unchanged
- **WHEN** rows adopt the full-height poster
- **THEN** each row occupies the same height as before, and the list is no longer than it was

#### Scenario: Missing picture keeps the row aligned
- **WHEN** a row's anime has no poster picture
- **THEN** its placeholder occupies the same full-height area, keeping the row's content aligned with its neighbours

#### Scenario: A landscape picture is drawn landscape
- **WHEN** a my-list or top-anime row's anime has a displayed picture wider than it is tall
- **THEN** the whole picture is shown at the row's height and at its own width there, with no part cropped away, and the row's title begins after it

#### Scenario: A landscape row is no taller than its neighbours
- **WHEN** a list mixes portrait and landscape artwork
- **THEN** every row stands the same height, and only the width of the landscape rows' pictures differs

### Requirement: Rank numbers when sorted by score
The system SHALL show rank numbers (e.g. `#1`) on the left of my-list entries when the list is flat — that is, when grouping by status is off — and the primary sort key is anything other than Alphabetical, since a rank against an alphabetical ordering carries no meaning. A grouped list SHALL NOT show rank numbers. In flat mode the system SHALL show a single header line naming the active status filter (or "All") above the list.

The number SHALL be the row's position in the list as currently filtered and sorted, counting from 1, and SHALL NOT be the anime's overall rank from the `anime-ranking` capability — a filtered list numbers what it shows.

The rank SHALL occupy a fixed-width column sized for the longest rank the list can produce (at least three digits), independent of the rank actually shown on a given row. The `#` SHALL start at the same horizontal position on every row, and the poster that follows SHALL start at the same horizontal position on every row, so a rank of any length neither shifts the posters out of alignment nor runs underneath one.

#### Scenario: Ranks on a flat sorted list
- **WHEN** grouping is off and I sort by MAL score, my score, episodes watched, or any key other than Alphabetical
- **THEN** each entry shows a rank number on the left

#### Scenario: Numbers count the filtered list
- **WHEN** grouping is off, I sort by my score, and a status filter hides everything above my 8s
- **THEN** the first row shown is numbered `#1`, whatever overall rank that anime holds

#### Scenario: Sorting by airing status
- **WHEN** grouping is off and I sort by airing status
- **THEN** each entry shows a rank number on the left, ordered by the chosen show-first status ahead of the other two

#### Scenario: No ranks when grouped
- **WHEN** grouping by status is on
- **THEN** no rank numbers are shown, whatever the sort key

#### Scenario: No ranks on an alphabetical flat list
- **WHEN** grouping is off and the primary sort key is Alphabetical
- **THEN** no rank numbers are shown

#### Scenario: Flat view shows an active-filter header
- **WHEN** the my-list page is in flat (ungrouped) mode
- **THEN** a header line above the list names the active status filter, or "All" when unfiltered

#### Scenario: Three-digit rank stays clear of the poster
- **WHEN** a ranked row's number reaches three digits (for example `#100`)
- **THEN** the whole number is visible beside the poster rather than overlapping or sliding under it

#### Scenario: Ranks and posters align down the list
- **WHEN** a ranked list contains rows with one-, two-, and three-digit ranks
- **THEN** every row's `#` starts at the same horizontal position and every row's poster starts at the same horizontal position

### Requirement: My list status filter tabs
The system SHALL provide status filter controls on the my-list page — All, Watching, Rewatching, Completed, Plan to watch, On hold, Dropped — so that the list shows only entries in the selected statuses. The Rewatching tab SHALL carry the Rewatching status colour, as every other tab carries its own status's colour.

The status controls SHALL be **multi-select**: any combination of Watching, Rewatching, Completed, Plan to watch, On hold, and Dropped MAY be selected at once, and the list SHALL show the union of the selected statuses. Selecting a status while others are selected SHALL add it to the selection rather than replacing it, and selecting an already-selected status SHALL remove it.

**All SHALL be exclusive.** Selecting All SHALL clear every other selection, and selecting any individual status SHALL clear All. All SHALL read as selected exactly when no individual status is selected, and in that state the list SHALL show entries in every status.

Deselecting the last remaining individual status SHALL return the controls to All rather than leaving nothing selected, so the page can never be filtered to an empty list by deselection alone.

Because several controls can be selected at once, the controls SHALL be presented and announced as independent toggles — each reporting its own pressed state — rather than as a single-selection tab list, so assistive technology is not told that exactly one is selected.

When the grouped view is in use, the visible groups SHALL follow the app's standard status group order rather than the order the statuses were selected in.

The selection SHALL be restored with the page on back/forward navigation, as the page's other view controls are, and SHALL open on All on a fresh visit — except where a deep link seeds the page with a specific status, which SHALL select that status alone.

#### Scenario: Filtering by one status
- **WHEN** I select a status filter other than All
- **THEN** only entries in that status are shown

#### Scenario: Filtering to rewatches
- **WHEN** I select the Rewatching filter
- **THEN** only Rewatching entries are shown, and Completed entries are not among them

#### Scenario: Selecting several statuses at once
- **WHEN** I select Watching, then Rewatching, then On hold
- **THEN** all three controls read as selected and the list shows entries in any of those three statuses

#### Scenario: Selected groups follow the standard order
- **WHEN** I select Dropped and then Watching, with the grouped view in use
- **THEN** the Watching group appears above the Dropped group, following the app's standard status order rather than my click order

#### Scenario: Deselecting one of several
- **WHEN** Watching, Rewatching, and On hold are selected and I select Rewatching again
- **THEN** Rewatching is deselected and the list shows Watching and On hold entries only

#### Scenario: Showing all statuses
- **WHEN** I select All
- **THEN** entries in every status are shown, grouped in the standard order

#### Scenario: All clears the other selections
- **WHEN** Watching and Completed are selected and I select All
- **THEN** Watching and Completed are both deselected, All reads as selected, and every entry is shown

#### Scenario: Selecting a status clears All
- **WHEN** All is selected and I select Completed
- **THEN** All is deselected, Completed alone is selected, and only Completed entries are shown

#### Scenario: Deselecting the last status returns to All
- **WHEN** Completed is the only selected status and I select it again
- **THEN** All reads as selected and every entry is shown, rather than the list going empty

#### Scenario: A deep link still selects one status
- **WHEN** I follow a link that opens my list focused on a single status
- **THEN** that status alone is selected, and I can add further statuses to it from there

### Requirement: My list filter bar
The system SHALL present every my-list filter and sort control in one controls block directly below the status filter tabs, above the entries it applies to. The block SHALL appear exactly once on the page — never repeated per status group — and SHALL apply to every group on screen alike.

The block SHALL be divided into two groups, each on its own row and each carrying a visible label:

- a **Filter** group holding the controls that narrow which entries are shown — find in list, the type filter, the airing-status filter, and the score filter — in that order;
- a **Sort** group holding the controls that order and arrange them — the sort key, its direction, the tiebreaker, and the grouping choice — in that order.

No control SHALL sit in the other group's row, and the two groups SHALL stay on separate rows at every viewport width, so narrowing and ordering never read as one strip. Each group SHALL be exposed to assistive technology as a group named by its label.

When a group's controls do not fit on one line they SHALL wrap onto further lines within that group's own row, rather than overflowing, scrolling horizontally, or flowing into the other group. Where the viewport is too narrow to hold a group's label beside its controls, the label SHALL sit above them instead.

Using any control in the block SHALL NOT make another control in the block appear, disappear, or change size. Every control in both groups SHALL be present whatever the others are set to, and a control whose label varies with its value SHALL hold one width across those values, so the controls beside it never shift sideways and no row gains or loses a line because of a selection.

Every control in the block SHALL be rectangular and share the one control height the `page-header-design` capability defines for a filter cluster. The rounded pill shape SHALL be reserved for the status filter tabs, so a control that narrows or orders the list is never mistaken for a status tab.

The page SHALL offer a **Reset filters & sort** action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default. It SHALL sit in the results line described by "My list reports what is being shown" rather than among the controls, and SHALL NOT change the selected status tabs or dismiss a recap scope, which are separate controls.

#### Scenario: One block for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the controls block appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Narrowing and ordering are two labelled groups
- **WHEN** the my-list page renders
- **THEN** a row labelled Filter holds find in list, Type, Airing and Score, and a separate row labelled Sort below it holds the sort key, the direction control, the tiebreaker and the grouping choice

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
- **THEN** the status tabs are the only rounded pills, and the direction control and the grouping choice are rectangular controls matching the selects beside them

#### Scenario: Resetting
- **WHEN** I have narrowed or reordered the list and use **Reset filters & sort**
- **THEN** the text query, type, airing-status and score filters are cleared, the sort returns to alphabetical in its natural direction with no tiebreaker, grouping by status is on, and the selected status tabs are left as they were

#### Scenario: Reset hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no **Reset filters & sort** action is shown

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

### Requirement: My list marks the filters in force
Each control in the my-list Filter group SHALL show, at rest and without being opened, whether it is currently narrowing the list: the find-in-list field while it holds text, the type and airing-status filters while on anything other than **All** (including **None**), and the score filter while on anything other than Any. A narrowing control SHALL be drawn with the app's active accent treatment; a control at its default SHALL be drawn neutral. The marking SHALL NOT change any control's size, and SHALL NOT be the only signal of the control's state — each control's own label, value, or tick continues to state it.

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

#### Scenario: Sorting is not marked
- **WHEN** I change the sort key and direction
- **THEN** no Sort group control is drawn with the narrowing accent

### Requirement: My list type filter
The system SHALL provide a multi-select type filter in the my-list filter bar. The filter SHALL offer only the media types actually present in my list — for example TV, Movie, OVA, ONA, Special, TV special, Music — plus an "Unknown" option when the list contains an entry whose type is not known, so the control never offers a choice that would return nothing.

Any combination of types SHALL be selectable. The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; with one or more types selected, only entries of those types are shown; on **None**, no entry passes the type filter and the list reports that nothing matches the current filters, offering to clear them. The control SHALL report its state in its label — All, None, the single selected type, or a count when several but not all are selected — so the active restriction is readable without opening it.

Media types SHALL be shown by display label (for example "TV special"), not by the raw value the API returns (`tv_special`), and the same labelling SHALL be used on list rows.

#### Scenario: Showing a single type
- **WHEN** I select only Movie in the type filter
- **THEN** only movie entries are shown

#### Scenario: Showing several types
- **WHEN** I select TV and ONA
- **THEN** entries of either type are shown and all others are hidden

#### Scenario: All applies no type restriction
- **WHEN** the type filter is on All
- **THEN** entries of every type are shown

#### Scenario: None excludes every entry
- **WHEN** I press **None** in the type filter
- **THEN** no entries are shown and the page says nothing matches the current filters and offers to clear them

#### Scenario: Only present types are offered
- **WHEN** my list contains no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: Types read as labels
- **WHEN** the type filter lists its options and a row shows its type
- **THEN** each reads as a display label such as "TV special" rather than `tv_special`

### Requirement: My list airing-status filter
The system SHALL provide a multi-select airing-status filter in the my-list filter bar offering Finished airing, Currently airing, Not yet aired, and — when the list contains one — entries whose airing status is unknown. The filter SHALL be available under every status tab, including All, not only under Plan to watch.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no airing restriction applies; with one or more statuses selected, only entries of those statuses are shown; on **None**, no entry passes the airing filter and the list reports that nothing matches the current filters.

The rows' airing-status indicator SHALL be shown whenever this filter is narrowing the list — that is, whenever it is on anything other than **All** — as it is when Airing status is the primary sort key.

#### Scenario: Filtering to still-airing shows
- **WHEN** I select Currently airing while the Watching status tab is active
- **THEN** only entries I am watching whose anime is still airing are shown

#### Scenario: Available under every status tab
- **WHEN** any status tab is active, including All
- **THEN** the airing-status filter is offered

#### Scenario: All applies no airing restriction
- **WHEN** the airing-status filter is on All
- **THEN** entries of every airing status are shown

#### Scenario: None excludes every entry
- **WHEN** I press **None** in the airing-status filter
- **THEN** no entries are shown and the page says nothing matches the current filters

#### Scenario: The indicator follows the filter being used
- **WHEN** the airing-status filter is on anything other than All
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

### Requirement: My list score filter
The system SHALL provide a score filter in the my-list filter bar with the options Any, Rated, Unrated, and one option per score value from 10 down to 1. Rated SHALL show only entries carrying a score of 1–10; Unrated SHALL show only entries with no score; a score-value option SHALL show only entries carrying exactly that score; Any SHALL impose no restriction.

The score-value options SHALL be presented alongside Rated and Unrated in the same control, each naming the score it selects, so choosing "the anime I scored 8" is one selection rather than a filter plus a sort.

#### Scenario: Finding unrated entries
- **WHEN** I select Unrated
- **THEN** only entries with no score of my own are shown

#### Scenario: Finding rated entries
- **WHEN** I select Rated
- **THEN** only entries carrying a score of mine are shown

#### Scenario: Finding one score
- **WHEN** I select the score 8
- **THEN** only entries I scored exactly 8 are shown, and entries scored 7 or 9 are not

#### Scenario: A score with no entries
- **WHEN** I select a score I have given to nothing in the current view
- **THEN** the list reports that nothing matches, rather than falling back to every rated entry

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

### Requirement: My list edit opens an overlay
The system SHALL open the entry editor as an overlay on top of the my-list page when an entry's edit button is used, consistent with the editor overlay used everywhere edit/add-to-list actions appear.

#### Scenario: Opening the editor overlay
- **WHEN** I click an entry's edit button
- **THEN** an editor overlay opens on top of the page for that entry

### Requirement: My list rows offer no progress or score control before the first episode

A my-list row whose anime has aired no episode (as resolved by the "Whether an anime has aired an episode is resolved one way" requirement in the `list-editing` capability) SHALL show neither a progress cell nor a score control: no bar, no `watched/total` count, no "+" control, and no inline score dropdown. Those cells SHALL be left empty rather than showing a zeroed or disabled control.

The rest of the row SHALL be unchanged — its status-colour stripe, rank, poster, title, type and airing badge, MAL score, and Edit button all render exactly as they do for any other row, so the row keeps the list's column alignment and its full height.

The Edit button SHALL remain available, since Watching, Plan to watch, and the clearing of an existing score or rewatch count are still permitted from the editor.

Both controls SHALL reappear once the anime has aired an episode, with no action needed from the user beyond the list being read again.

#### Scenario: A row for an unaired anime

- **WHEN** my list shows an entry whose anime has aired no episode
- **THEN** its progress and score cells are empty, and its stripe, poster, title, MAL score, and Edit button render as usual

#### Scenario: The row's controls return with the first episode

- **WHEN** that anime airs its first episode and my list is read again
- **THEN** the row shows its progress bar, count, "+" control, and score dropdown again

#### Scenario: Columns stay aligned

- **WHEN** my list mixes rows for aired and unaired anime
- **THEN** every row keeps the same column positions and the same height

#### Scenario: Editing is still reachable

- **WHEN** I click the Edit button on a row for an anime that has aired no episode
- **THEN** the editor overlay opens as it does for any other row, with the limits the `list-editing` capability places on it

### Requirement: My list rows edit the watched count in place
The system SHALL make the `watched` count in each my-list row's progress cell directly editable in place, per the "Inline editable episode count" requirement, so an entry's episode number can be set without opening the edit overlay. The row's edit button SHALL remain available for status, score, and rewatch-count changes. Saving an in-place count edit SHALL update that row's count and bar without reloading the page or re-sorting the list.

This applies to every row that has a progress cell. A row whose anime has aired no episode has none — see "My list rows offer no progress or score control before the first episode" — and so offers no in-place count field either.

#### Scenario: Setting a row's count in place
- **WHEN** I click the count in a my-list row, type a number, and confirm
- **THEN** that row's episodes-watched is saved and its count and bar update in place, with no overlay opening

#### Scenario: Edit button still opens the overlay
- **WHEN** I click a row's edit button
- **THEN** the editor overlay opens as before, unaffected by the in-place count field

#### Scenario: Row stays in position after an in-place edit
- **WHEN** I save an in-place count edit on a row partway down the list
- **THEN** the list is not reloaded or reordered underneath me and the row keeps its position

#### Scenario: No count field where there is no progress cell
- **WHEN** a my-list row's anime has aired no episode
- **THEN** the row shows no editable count, because it shows no progress cell at all

### Requirement: Top anime ranking list selector
The Top anime page SHALL offer a selector that chooses which of MyAnimeList's rankings the page shows, presented as a row of buttons — one per list — placed on its own row below the page's title, sharing that row with the ranking's pagination controls so the two are readable together without crowding the title. The buttons SHALL wrap onto further lines when the window is too narrow to hold them on one, and the pagination controls SHALL drop to their own line beneath the selector when there is no longer room for both on one line, rather than either overflowing or scrolling horizontally.

The selectable lists SHALL be exactly: **All**, **TV**, **Movie**, **OVA**, **Special**, **Popularity**, and **Favourite**. The system SHALL NOT offer an ONA or a Music list, because MyAnimeList's API does not expose either as a ranking, and SHALL NOT introduce a second data provider to supply them.

The button for the list currently shown SHALL be visibly highlighted as selected, distinctly from the unselected buttons, and SHALL expose that selected state to assistive technology rather than signalling it by colour alone. The remaining buttons SHALL stay interactive at all times, including while a newly selected list is still loading, so a mis-click can be corrected without waiting.

A fresh visit to the Top anime page SHALL show the All list. Selecting a list SHALL add a step to the browser's navigation history, so going back returns to the list previously being viewed, and SHALL reset the ranking to its first page — a page number from one list does not carry over to another. Selecting the list already shown SHALL do nothing: no history step and no reload.

Every list SHALL be presented with the same three-tier layout, pagination, and per-entry content as the All list, since each list is ranked 1–500 in its own right.

#### Scenario: Opening Top anime shows the All list
- **WHEN** I reach the Top anime page by its navbar link
- **THEN** the All list is shown and the All button is highlighted as selected

#### Scenario: Selecting a list switches the ranking
- **WHEN** I select the Movie button
- **THEN** the page shows MyAnimeList's top movies, ranked from 1, and the Movie button becomes the highlighted one while All is no longer highlighted

#### Scenario: The selected list survives leaving and coming back
- **WHEN** I select the Favourite list, open an anime from it, and then go back
- **THEN** I return to the Favourite list with its button still highlighted, not to the All list

#### Scenario: Going back steps to the previously selected list
- **WHEN** I select Movie and then select OVA, and then go back
- **THEN** I return to the Movie list

#### Scenario: Switching lists returns to the first page
- **WHEN** I am on page 4 of the All list and select the TV button
- **THEN** the TV list is shown from its first page, with its showcase and top-ten tiers visible

#### Scenario: Selecting the list already shown does nothing
- **WHEN** the Movie list is shown and I select the Movie button again
- **THEN** the page does not reload the list and going back still leads to whatever preceded the Movie list

#### Scenario: No ONA or Music button is offered
- **WHEN** I look at the ranking list selector
- **THEN** there are exactly seven buttons — All, TV, Movie, OVA, Special, Popularity, Favourite — with no ONA or Music option

#### Scenario: Selection is not signalled by colour alone
- **WHEN** the page is read by assistive technology
- **THEN** the selected list's button reports itself as pressed/selected, so the active list is discoverable without seeing the highlight

### Requirement: Fast switching between ranking lists
Switching between ranking lists SHALL NOT blank the page. A list already loaded during the current application session SHALL be shown immediately when re-selected, without a network request and without any loading state.

When a list not yet loaded in this session is selected, the page SHALL keep the previously shown list rendered — visually muted and non-interactive, so it is unmistakably not the list the highlighted button names — until the new list arrives, rather than replacing it with a loading message or an empty page. Only the page's very first load, when there is no list on screen at all, SHALL show a plain loading state.

#### Scenario: Returning to an already-loaded list is instant
- **WHEN** I select Movie, then All, then Movie again
- **THEN** the second Movie selection renders immediately with no loading state and no further request

#### Scenario: Loading an unseen list keeps the current one visible
- **WHEN** I select a list for the first time this session and its data has not arrived yet
- **THEN** the list I was reading stays on screen, muted and not interactive, until the new list replaces it

#### Scenario: The selector stays usable while a list loads
- **WHEN** a newly selected list is still loading
- **THEN** I can select a different list without waiting for the first one to finish

#### Scenario: First arrival shows a loading state
- **WHEN** I open the Top anime page for the first time in a session and no list has loaded yet
- **THEN** a plain loading indicator is shown, because there is no previous list to keep on screen

### Requirement: List membership edits apply across every loaded ranking list
Adding or editing an anime from one ranking list SHALL update that anime wherever it appears in every other ranking list loaded in the current session, since the lists overlap. Returning to another list SHALL therefore never offer to add an anime that is already in my list.

#### Scenario: Adding from one list updates another
- **WHEN** I add an anime to my list from the Movie ranking, and that anime also appears in the All ranking I loaded earlier
- **THEN** going back to the All ranking shows that anime with an "Edit" action, not "Add"

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where every entry — whatever form it takes — shows its rank, picture, title, my score, and MAL score. The page shows one selected ranking list at a time (see "Top anime ranking list selector"); everything below applies to whichever list is selected. Because this page ranks anime overall, an entry's anime may not be in my list; the flat-row tier (rank 11 and beyond) SHALL additionally carry a list-action button, conditional — "Add" when the anime is not yet in my list, and "Edit" when it is — the showcase and top-ten tiers (ranks 1–10) SHALL NOT carry one, keeping those tiers focused on the ranking and scores alone. The list SHALL be paginated at 50 entries per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

Changing the page from the bottom controls (page-number buttons or arrows) SHALL scroll the page back to the top, so the newly-shown entries are visible without the reader having to scroll up manually. The top-right arrows need no such scroll, since they already sit at the top of the page.

Changing the page, by any control, SHALL add a step to the browser's own navigation history rather than only updating local view state. Going back (the browser's back button, a keyboard shortcut, or a trackpad swipe gesture) SHALL therefore step to the previously-viewed page of the ranking, and going forward again SHALL return to the page just left — the same way back/forward already move between any other two pages in the app. Only once that page-by-page history is exhausted SHALL going back leave the Top anime page entirely, for whichever page preceded it in the browsing session.

The ranking SHALL be presented in three tiers, each visually distinct from the next, so the shape of the page itself communicates where an anime sits in the ranking:

- **Ranks 1–3 — showcase cards.** Three cards, each a self-contained bordered surface carrying its rank, poster, title, and both scores. They SHALL be laid out in ascending rank order following the reading direction, so the first card is rank 1; the presentation SHALL NOT reorder them visually away from their document order. Each card SHALL carry a prominent rank badge in a gold, silver, and bronze family for ranks 1, 2, and 3 respectively, and the card SHALL pick up its own medal colour beyond the badge (for example in its border and surface tint) so the three are distinguishable from one another at a glance and from every other tier. Rank 1 SHALL read as the most prominent of the three. The medal colours SHALL render the same in light and dark mode, since a medal's colour is its meaning. The card's two score chips SHALL be the same size as each other regardless of their label or value text differing in length, so the pair reads as one deliberate row rather than two mismatched boxes.
- **Ranks 4–10 — top-ten card row.** A single row of poster cards, smaller than the showcase cards, each carrying its own rank badge, title, and both scores, inside a bordered, coloured box of its own so the tier reads as a defined group rather than loose posters. The badge SHALL remain fully legible over any poster artwork, bright or dark, rather than relying on a translucent overlay whose contrast depends on the art beneath it.
- **Ranks 11 and beyond — flat rows.** The existing full-width rows, each with plain `#N` rank text, poster, title, my score, right-aligned MAL score, and its action button.

The showcase tier's medal identity SHALL be drawn from the application-wide medal colours — the same gold, silver, and bronze the recap podium uses — rather than from a palette local to this page, so the two surfaces that rank a top three in the app cannot drift apart in colour. Its rank badge SHALL take the same form as the recap podium's: a medal-coloured outline around a neutral fill, carrying the rank as a bare number. The medal treatment carried over SHALL be colour and badge form only; the podium's own sizing, entrance animation, hover response, and rank-1 sheen SHALL NOT follow it onto this page.

A showcase card's score chips SHALL be compact — sized for the narrow column beside the poster rather than to the app's default chip width — and within them the role label ("My score", "MAL") SHALL be large enough to read as a label rather than as fine print, at a size closer to its own value's than today's, while staying subordinate to that value.

Both card tiers (ranks 1–3 and 4–10) SHALL render an entry's poster in the same proportions the anime pages give that poster, so the artwork shown here is the artwork the anime's own detail page shows, not a differently-cropped portion of it. Neither tier SHALL crop a poster to proportions narrower than that box in order to fit its layout. The flat-row tier's small thumbnail is unaffected by this rule.

The rank of any entry SHALL be readable from the entry itself, in every tier, without relying on its position in the layout.

Because a page holds 50 entries, the showcase and top-ten tiers SHALL appear only on page 1; every entry on page 2 and beyond SHALL use the flat row form.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 entries of the selected list, each with its rank, picture, title, my score, and MAL score, plus a list-action button for entries in the flat-row tier

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-entry page, up to rank 500

#### Scenario: Changing pages from the bottom scrolls back to the top
- **WHEN** I use the bottom page-number controls or arrows to change the page, from anywhere on the page
- **THEN** the page scrolls back to the top so the newly-shown entries — the showcase tier on page 1, or the first flat rows on later pages — are visible immediately

#### Scenario: Going back steps to the previous page of the ranking
- **WHEN** I change from page 1 to page 2, then to page 3, and then go back — via the browser's back button, a keyboard shortcut, or a trackpad swipe gesture
- **THEN** I return to page 2 of the ranking, not to whatever page I was on before I opened Top anime

#### Scenario: Going forward returns to the page just left
- **WHEN** I go back from page 3 to page 2 as above, then go forward
- **THEN** I return to page 3

#### Scenario: Going back past the first page leaves Top anime
- **WHEN** I am on page 1 of the ranking, having reached it without changing pages or lists since arriving, and go back
- **THEN** I leave the Top anime page for whatever page I was on immediately before it

#### Scenario: Page numbers in the middle of the range
- **WHEN** I am on page 6 of 10
- **THEN** the page-number controls show 1, an ellipsis, 5, 6, 7, an ellipsis, and 10 — and no other page numbers

#### Scenario: Page numbers near an edge of the range
- **WHEN** I am on page 2 of 10
- **THEN** the page-number controls show 1, 2, 3, an ellipsis, and 10, with no ellipsis between adjacent pages

#### Scenario: Flat-row entry not in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Flat-row entry already in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

#### Scenario: Top 3 stand out from the rest of the ranking
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 1, 2, and 3 render as three showcase cards, each carrying a gold, silver, or bronze rank badge and matching card accent, visibly different from both the top-ten card row and the flat rows below

#### Scenario: The top 3 read in rank order
- **WHEN** I look at the three showcase cards
- **THEN** they run 1, 2, 3 in reading order, and each card states its own rank

#### Scenario: The showcase and the recap podium agree on gold
- **WHEN** I compare a Top anime showcase card with the recap page's podium card of the same rank
- **THEN** both carry the same medal colour and the same badge form, in light mode and in dark mode alike

#### Scenario: The podium's motion does not follow its colours
- **WHEN** the top-anime page's first page renders
- **THEN** the showcase cards appear without an entrance animation, rank 1 carries no sweeping sheen, and hovering a card does not lift it

#### Scenario: A showcase card's two score boxes match
- **WHEN** I look at a showcase card's "My score" and "MAL" chips
- **THEN** the two boxes are the same width and height as each other, even though "My score" is longer text than "MAL"

#### Scenario: A showcase chip's label is readable
- **WHEN** I look at a showcase card's score chips
- **THEN** each chip's label is legible beside its value rather than reading as fine print, and the chip itself takes no more room beside the poster than its two short lines need

#### Scenario: A top-ten poster matches the anime's own page
- **WHEN** I open the detail page of an anime shown in the showcase or top-ten tier
- **THEN** its poster there is the same picture, showing the same extent of the artwork, as the Top anime card showed — neither is a narrower crop of the other

#### Scenario: Ranks 4 to 10 form their own tier
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 4 through 10 appear as one row of cards, each stating its rank, sized and styled distinctly from both the showcase cards above and the flat rows below

#### Scenario: A rank badge stays legible over bright poster art
- **WHEN** a top-ten card's poster art is bright, pale, or visually busy
- **THEN** its rank badge is still fully legible, because the badge does not depend on contrast against the artwork behind it

#### Scenario: Medal colours survive a theme switch
- **WHEN** I switch between light and dark mode
- **THEN** the gold, silver, and bronze accents on ranks 1, 2, and 3 still read as gold, silver, and bronze

#### Scenario: Card tiers do not appear on later pages
- **WHEN** I view page 2 or later of the top-anime ranking
- **THEN** every entry on that page uses the flat row form, since ranks 1–10 only appear on page 1

#### Scenario: A filtered list is ranked in its own right
- **WHEN** I view the Movie list
- **THEN** its entries are numbered from rank 1, with the top three in the showcase tier, rather than carrying their positions in the overall ranking

### Requirement: Daily refresh of the Top Anime ranking
The system SHALL re-fetch a Top Anime ranking list's lean listing fields the first time that list is viewed on a local calendar day after its own last fetch, serving it from cache on same-day revisits. Each selectable list SHALL keep its own last-fetched time, so viewing one list never marks another as fetched for the day. If a list has never been viewed, it SHALL never be proactively fetched — opening the page SHALL NOT warm the lists the user has not selected.

At most one refresh per list SHALL be in flight at a time: a second request for the same list arriving while its refresh is running SHALL wait for it and then serve the refreshed cache, rather than starting a second fetch of that list. Requests for two different lists SHALL NOT wait on each other. A fetch that fails SHALL NOT count as that list's fetch for the day — the next visit to that list retries — and SHALL NOT prevent the page from serving whatever is already cached for it.

#### Scenario: New-day visit
- **WHEN** I open a Top Anime ranking list that has not been fetched yet on the current local calendar day
- **THEN** the system re-fetches that list live and updates its cache

#### Scenario: Same-day revisit
- **WHEN** I reopen the same ranking list again on the same local day
- **THEN** it is served from the cache without a live re-fetch

#### Scenario: Each list has its own daily clock
- **WHEN** I have already viewed the All list today and then select the Movie list for the first time today
- **THEN** the Movie list is fetched live, because the All list's fetch does not count as Movie's

#### Scenario: Concurrent visits share one refresh
- **WHEN** a second request for the same ranking list arrives while its refresh is already running
- **THEN** no additional MAL fetch is started, and the second request is served from the refreshed cache once the first completes

#### Scenario: Different lists refresh in parallel
- **WHEN** requests for two different ranking lists arrive at the same time, neither fetched yet today
- **THEN** neither waits on the other, because they are different subjects

#### Scenario: A failed fetch does not consume the day
- **WHEN** a ranking list's refresh fails and I open that list again the same day
- **THEN** the refresh is retried, because only a successful fetch marks that list as fetched for that day

#### Scenario: A failed fetch still serves the cache
- **WHEN** a ranking list's refresh fails and that list has cached rows from an earlier fetch
- **THEN** the page renders those cached rows rather than an error or an empty list

#### Scenario: Never visited stays unfetched
- **WHEN** a Top Anime ranking list has never been viewed
- **THEN** no background job fetches it, and viewing any other list does not fetch it either

### Requirement: Consistent list placement across my-list view modes
The system SHALL place the my-list entry list at the same vertical offset below its status header in flat (ungrouped) view as in grouped view, for every status filter. Switching between grouped and flat view, or changing the sort, SHALL NOT shift the list up or down relative to the header it sits under. The header-to-list spacing SHALL be defined by a single rule shared by both view modes, rather than by per-mode values that can diverge.

#### Scenario: Switching view mode does not shift the list
- **WHEN** I turn grouping off, or change the sort key or direction
- **THEN** the first entry row stays at the same vertical offset below its header, and only the ordering or grouping of the rows changes

#### Scenario: Spacing is consistent under every status filter
- **WHEN** the my-list page is in flat view under any status filter — All, Watching, Completed, Plan to watch, On hold, or Dropped
- **THEN** the gap between the header line and the first entry row matches the gap shown in grouped view

#### Scenario: Grouped view spacing is unchanged
- **WHEN** the my-list page is in grouped view
- **THEN** each status group's list sits directly below its group header at the established spacing, and consecutive status groups remain separated by the page's section spacing

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

The page SHALL additionally accept, alongside a scope, a narrowing that names one of the recap's stats or one of its score-distribution rows. Such a narrowing SHALL be applied by setting the page's **own** controls — the status tab, the type filter, and the score filter — to the values that express it, rather than as a second, hidden scope, so it is visible on arrival and can be adjusted or cleared with the page's ordinary controls. Controls the narrowing does not concern SHALL be left at their defaults, and the resulting set SHALL match the number that was followed. A stat with no equivalent among the page's own controls — such as "Movies watched", which counts a movie regardless of watch status — SHALL narrow only by the controls that do have an equivalent (here, type), rather than misrepresenting the stat with a narrower filter than it means.

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
- **THEN** the page arrives scoped to 2020 with its type filter set to Movie, showing that period's films whatever their watch status, since no page control narrows by watched-at-all

#### Scenario: Arriving from a distribution row
- **WHEN** I open my list by following a recap distribution's score-8 row
- **THEN** the page arrives scoped to that period with its score filter set to 8, and its status tab left on All

#### Scenario: The arrival narrowing is adjustable
- **WHEN** I arrive from a recap stat and then change the control it set
- **THEN** the change takes effect and is not reverted, and **Reset filters & sort** restores the page's defaults within the scope

#### Scenario: Dismissing clears the narrowing too
- **WHEN** I arrive from a recap stat and dismiss the scope indicator
- **THEN** the full list returns with the arrival narrowing gone, not reapplied

### Requirement: Broadcast progress on my-list rows

While an anime is currently airing, its my-list row's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar, the dashboard's currently-watching cards, and the anime detail page's bar. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

This SHALL be an addition to the row's existing bar, not a second bar and not a replacement: the row SHALL keep exactly one progress cell, its `watched/total` label, its in-place editable count, and its increment control, and the row's height SHALL be unchanged.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When the aired count is not known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total`, or `watched/?` when the total is unknown, and SHALL NOT restate the aired count.

A row that has no progress cell at all — an anime that has aired no episode, per "My list rows offer no progress or score control before the first episode" — SHALL be unaffected: it gains no bar from this requirement.

When my episodes watched changes — by the increment control or by editing the count in place — my fill SHALL update in place without a reload or a re-sort, and the aired fill SHALL be unaffected.

#### Scenario: An airing row shows broadcast progress

- **WHEN** a my-list row renders for a currently airing anime with 12 total episodes, 5 aired, and 3 watched
- **THEN** a blue fill spans 5/12 of the track, a purple fill spans 3/12 on top of it, and the label reads `3/12`

#### Scenario: Caught up with the broadcast

- **WHEN** a my-list row renders for a currently airing anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither extends past the aired portion of the track

#### Scenario: Finished airing keeps the plain bar

- **WHEN** a my-list row renders for an anime that has finished airing
- **THEN** no aired fill is drawn and the bar shows only my watched progress against the total, as it did before

#### Scenario: Unknown total on an airing anime

- **WHEN** a my-list row renders for a currently airing anime whose total episode count is unknown and whose aired count is known
- **THEN** the aired fill spans half the track, my fill is measured within that extent, and the label reads `watched/?`

#### Scenario: Incrementing an airing row

- **WHEN** I press "+" on a my-list row for a currently airing anime
- **THEN** my purple fill and the count advance in place, the blue aired fill is unchanged, and the row keeps its position in the list

#### Scenario: A row with no progress cell gains nothing

- **WHEN** a my-list row's anime has aired no episode
- **THEN** the row still shows no progress cell at all

### Requirement: The my-list read's database round trips do not grow with my list
Serving my list SHALL issue a number of database reads that does not grow with the number of entries in it. The aired-so-far count each row carries SHALL be resolved for the whole list through the `episode-airing-data` capability's bulk aired-count read, rather than by a read per entry inside a loop, and the resulting figures SHALL be the same ones handed to the airing watch-status settling that runs on this read.

This SHALL change no value the list reports: the bulk read answers exactly what the per-anime read answers for the same anime and instant, an entry whose anime has no stored airing rows stays unknown rather than becoming zero, and the rows, their order and their grouping are unaffected.

#### Scenario: A large list costs a bounded number of reads
- **WHEN** my list of six hundred entries is served
- **THEN** the aired-so-far counts for every entry are resolved in one read rather than one read per entry

#### Scenario: The rows are unchanged
- **WHEN** my list is served before and after this change for the same stored data at the same instant
- **THEN** both responses carry the same entries in the same order with the same aired counts, and an entry with no stored airing rows reports an unknown count in both

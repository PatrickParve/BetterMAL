## ADDED Requirements

### Requirement: My list filters offer only what the listed entries make possible

The my-list Filter group's three narrowing controls — the type filter, the airing-status filter and the score filter — SHALL offer only choices that would change what the list shows. A choice SHALL be offered when making it would both **remove at least one of the entries currently listed** and **leave at least one entry**. A choice that would return nothing, and a choice that would return exactly what is already on screen, SHALL NOT be offered.

"Currently listed" SHALL mean the entries left by every *other* control on the page — the recap scope, the status filter tabs, the find-in-list text, and the two filters other than the one being offered. A control's own restriction SHALL NOT narrow its own options, so a filter can always be widened again after it has been narrowed. The offered choices SHALL follow those other controls as they change, rather than being fixed from the whole list when the page loads.

A control SHALL always offer the value it is currently set to, even when the rule above would otherwise drop it, so any restriction in force can be lifted from the control that applies it.

Narrowing the options SHALL NOT change any filter's own value: a filter set to a value the rest of the page has since narrowed away from SHALL keep that value, and the list SHALL report that nothing matches rather than the filter silently widening. Returning the other controls to where they were SHALL therefore return the list to what it was showing.

When a control is left with no choice to make — every listed entry has the same type, the same airing status, or the same score — it SHALL be unavailable rather than removed, as "My list score filter" and the `page-header-design` capability's "The shared multi-select filter is unavailable when it has nothing to choose between" define for each control.

#### Scenario: Options follow the status tab
- **WHEN** I select the **On hold** tab and nothing I have on hold is a music video
- **THEN** the type filter does not offer Music, and offers it again when I return to **All**

#### Scenario: Filters narrow each other
- **WHEN** I select Movie in the type filter and none of my movies is currently airing
- **THEN** the airing-status filter does not offer Currently airing

#### Scenario: A filter does not narrow its own options
- **WHEN** I select Movie in the type filter
- **THEN** the type filter still offers every other type present alongside movies under the other controls, so I can add TV to the selection or return to **All**

#### Scenario: The find text narrows the options too
- **WHEN** I type text in find-in-list that matches only TV series
- **THEN** the type filter offers only TV, and the other types return when I clear the text

#### Scenario: A choice that would change nothing is not offered
- **WHEN** every entry currently listed carries a score of 8
- **THEN** the score filter does not offer 8, since choosing it would leave the list exactly as it is

#### Scenario: A selection in force is always offered
- **WHEN** the type filter is on Movie and I switch to a status tab under which I have no movies
- **THEN** the type filter still offers Movie, still shows it as selected, and can still be returned to **All**

#### Scenario: Narrowing the options does not change a filter's value
- **WHEN** the type filter is on Movie, I switch to a status tab with no movies, and I switch back
- **THEN** the type filter is still on Movie and the list shows my movies again, without my having reselected anything

### Requirement: My list filter controls hold one size

Every control in the my-list Filter group SHALL keep one width for the life of the page, whatever it is set to and whatever it currently offers. A control SHALL be drawn at that width on the first frame — before any entry has loaded — and SHALL NOT resize when entries arrive, when the status tabs change, when the find-in-list text changes, or when another filter changes what it offers. No control in the group SHALL move sideways as another is used.

The width SHALL be that of the widest label the control could ever show — reserved from the full set of values of its kind, not from the values the list happens to contain — so no label is clipped or wrapped and no width is a fixed guess. It SHALL follow the app's fluid root font size, as the cluster's shared height already does.

Each of the type, airing-status and score controls SHALL align its label to the leading edge of that reserved width, so the label begins in the same place whatever it says.

#### Scenario: The controls are their final size before the list loads
- **WHEN** I reload my list and watch the Filter row while the entries are still loading
- **THEN** the Type, Airing and Score controls are already at their final width and do not grow when the entries arrive

#### Scenario: Narrowing the options does not resize a control
- **WHEN** I change the status tab so the Airing filter offers fewer statuses
- **THEN** the Airing control's width is unchanged and the Score control beside it does not move

#### Scenario: The longest label still fits
- **WHEN** the Airing filter reads "Airing: Currently airing" and the Score filter reads "Score: Unrated"
- **THEN** each label is shown in full, neither clipped nor wrapped

#### Scenario: Labels start in the same place
- **WHEN** the Type filter moves between "Type: All" and "Type: TV special"
- **THEN** both labels begin at the same position inside the control rather than being centred in it

## MODIFIED Requirements

### Requirement: My list type filter
The system SHALL provide a multi-select type filter in the my-list filter bar. The filter SHALL offer the media types present among the entries the page's other controls leave listed — plus an "Unknown" option when those entries include one whose type is not known — and SHALL NOT offer a type absent from them, so the control never offers a choice that would return nothing. Which entries those are, and the filter's own selection always being offered, are defined by "My list filters offer only what the listed entries make possible".

Any combination of types SHALL be selectable. The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; with one or more types selected, only entries of those types are shown; on **None**, no entry passes the type filter and the list reports that nothing matches the current filters, offering to clear them. The control SHALL report its state in its label — All, None, the single selected type, or a count when several but not all are selected — so the active restriction is readable without opening it.

When the listed entries are all of one type, the filter SHALL be unavailable and SHALL read as that type, per the `page-header-design` capability's "The shared multi-select filter is unavailable when it has nothing to choose between".

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

#### Scenario: Only types the other controls leave listed are offered
- **WHEN** my list contains music videos but none of them is on hold, and I select the **On hold** tab
- **THEN** the type filter does not offer Music as an option

#### Scenario: One type left is not a choice
- **WHEN** every entry currently listed is a TV series and I have set no type restriction
- **THEN** the type filter reads "Type: TV", is drawn as unavailable, and does not open

#### Scenario: Types read as labels
- **WHEN** the type filter lists its options and a row shows its type
- **THEN** each reads as a display label such as "TV special" rather than `tv_special`

### Requirement: My list airing-status filter
The system SHALL provide a multi-select airing-status filter in the my-list filter bar offering the airing statuses — Finished airing, Currently airing, Not yet aired, and an unknown option — present among the entries the page's other controls leave listed, and SHALL NOT offer a status absent from them. Which entries those are, and the filter's own selection always being offered, are defined by "My list filters offer only what the listed entries make possible". The filter SHALL be present under every status tab, including All, not only under Plan to watch — present, though not necessarily available, as below. It SHALL be the only airing-status control on the page: airing status narrows the list but does not order it.

The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no airing restriction applies; with one or more statuses selected, only entries of those statuses are shown; on **None**, no entry passes the airing filter and the list reports that nothing matches the current filters.

When the listed entries all share one airing status, the filter SHALL be unavailable and SHALL read as that status, per the `page-header-design` capability's "The shared multi-select filter is unavailable when it has nothing to choose between". An unavailable filter SHALL still be shown, in its place and at its size, so the control is never missing from the row.

The rows' airing-status indicator SHALL be shown whenever this filter is narrowing the list — that is, whenever it is on anything other than **All**.

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

#### Scenario: Only statuses the other controls leave listed are offered
- **WHEN** I select the **Completed** tab and every completed entry has finished airing
- **THEN** the airing-status filter offers neither Currently airing nor Not yet aired

#### Scenario: One airing status left is not a choice
- **WHEN** every entry currently listed has finished airing and I have set no airing restriction
- **THEN** the airing-status filter reads "Airing: Finished airing", is drawn as unavailable, and does not open

#### Scenario: The indicator follows the filter being used
- **WHEN** the airing-status filter is on anything other than All
- **THEN** every row with a known airing status shows the indicator, whatever its watch status

### Requirement: My list score filter
The system SHALL provide a score filter in the my-list filter bar. Any SHALL always be offered and SHALL impose no restriction. Rated SHALL show only entries carrying a score of 1–10; Unrated SHALL show only entries with no score; a score-value option SHALL show only entries carrying exactly that score.

Rated, Unrated and the score values from 10 down to 1 SHALL each be offered only when choosing it would change what the list shows, per "My list filters offer only what the listed entries make possible": a score value SHALL be offered when some but not all of the listed entries carry exactly that score, and Rated and Unrated SHALL be offered when the listed entries include both rated and unrated ones. The filter SHALL always offer the value it is currently set to.

The score-value options SHALL be presented alongside Rated and Unrated in the same control, each naming the score it selects, so choosing "the anime I scored 8" is one selection rather than a filter plus a sort.

When Any is the only remaining option and the filter is not itself narrowing the list, the control SHALL be unavailable and SHALL read as the one thing true of every listed entry — the score they all carry (for example "Score: 8"), or "Score: Unrated" when none of them is rated. A filter that is itself narrowing the list SHALL remain available whatever it offers, so its restriction can always be lifted.

#### Scenario: Finding unrated entries
- **WHEN** I select Unrated
- **THEN** only entries with no score of my own are shown

#### Scenario: Finding rated entries
- **WHEN** I select Rated
- **THEN** only entries carrying a score of mine are shown

#### Scenario: Finding one score
- **WHEN** I select the score 8
- **THEN** only entries I scored exactly 8 are shown, and entries scored 7 or 9 are not

#### Scenario: A score nothing carries is not offered
- **WHEN** I have given nothing in the current view a 3
- **THEN** the score filter does not offer 3

#### Scenario: A stale score selection still reports honestly
- **WHEN** the score filter is on 8 and I narrow the other controls to entries none of which I scored 8
- **THEN** the filter stays on 8, still offers 8 so I can leave it, and the list reports that nothing matches rather than falling back to every rated entry

#### Scenario: Rated and Unrated go together
- **WHEN** every entry currently listed carries a score of mine
- **THEN** the score filter offers neither Rated — which would change nothing — nor Unrated, which would return nothing

#### Scenario: One score left is not a choice
- **WHEN** every entry currently listed carries a score of 8 and I have set no score restriction
- **THEN** the score filter reads "Score: 8", is drawn as unavailable, and does not open

#### Scenario: Nothing rated is not a choice either
- **WHEN** nothing currently listed carries a score of mine and I have set no score restriction
- **THEN** the score filter reads "Score: Unrated" and is drawn as unavailable

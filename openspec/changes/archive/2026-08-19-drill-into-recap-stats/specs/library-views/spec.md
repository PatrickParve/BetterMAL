## ADDED Requirements

### Requirement: My list started filter
The system SHALL provide a **Started** filter in the my-list filter bar that narrows the list to entries I have watched at least one episode of, whatever their status. Off — its default — it SHALL impose no restriction. It SHALL combine with every other filter rather than replacing them, and SHALL be presented as a two-state control in the filter bar's narrowing cluster, in the same style as the bar's other toggles.

#### Scenario: Narrowing to what I have started
- **WHEN** I turn the Started filter on
- **THEN** only entries with at least one episode watched are shown, whichever status they hold

#### Scenario: Off by default
- **WHEN** the my-list page renders without the filter being set
- **THEN** entries with no episodes watched are shown alongside the rest

#### Scenario: Combining with the type filter
- **WHEN** I turn the Started filter on and select Movie in the type filter
- **THEN** only films I have watched are shown

## MODIFIED Requirements

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

### Requirement: My list filter bar
The system SHALL present every my-list filter and sort control in a single bar directly below the status filter tabs, above the entries it applies to. The bar SHALL appear exactly once on the page — never repeated per status group — and SHALL apply to every group on screen alike. When the bar is wider than the viewport its controls SHALL wrap onto further lines rather than overflow or scroll horizontally, matching how the status tabs already wrap.

The bar SHALL be laid out as two clusters: the filters that narrow which entries are shown, and the controls that order them.

The bar SHALL offer a "Clear filters" action that restores the page's default view — no text query, no type restriction, no airing restriction, no score restriction, the Started filter off, alphabetical primary sort in its natural direction, no tiebreaker, grouped by status. The action SHALL be shown only while at least one of those is not at its default, and SHALL NOT change the selected status tab, which is a separate control.

#### Scenario: One bar for the whole page
- **WHEN** the my-list page renders with several status groups on screen
- **THEN** the filter bar appears once below the status tabs, and no status group header carries its own copy of any filter or sort control

#### Scenario: Controls wrap on a narrow viewport
- **WHEN** the viewport is too narrow to fit the bar's controls on one line
- **THEN** the controls wrap onto additional lines and the page does not scroll horizontally

#### Scenario: Clearing filters
- **WHEN** I have narrowed or reordered the list and use "Clear filters"
- **THEN** the text query, type, airing-status, score and Started filters are cleared, the sort returns to alphabetical with no tiebreaker, grouping by status is on, and the selected status tab is left as it was

#### Scenario: Clear action hidden at defaults
- **WHEN** every filter and sort control is at its default value
- **THEN** no "Clear filters" action is shown

#### Scenario: The Started filter counts as off-default
- **WHEN** the Started filter is the only control I have changed
- **THEN** the "Clear filters" action is shown, and using it turns the filter off

### Requirement: My list opens scoped to a recap period
The system SHALL let the my-list page open scoped to a recap's period, time filter, and media type, arriving from the recap's "see all" control, and SHALL then show exactly the anime that recap included — no more and no fewer — whatever their watch statuses.

The scope SHALL be carried in the page URL so it survives a reload and back-navigation, and SHALL be shown on the page as a labelled, dismissible indicator naming the period it represents, so a narrowed list is never mistaken for the whole list. Dismissing it SHALL return the page to the unscoped list without disturbing the status filter, filter bar, or sort selections.

While a recap scope is active the page's own status tabs, filter bar, and sorting SHALL continue to work, narrowing and ordering within the scoped set rather than escaping it. The page's "showing N of M" count SHALL report against the scoped set.

The page SHALL additionally accept, alongside a scope, a narrowing that names one of the recap's stats or one of its score-distribution rows. Such a narrowing SHALL be applied by setting the page's **own** controls — the status tab, the type filter, the score filter, and the Started filter — to the values that express it, rather than as a second, hidden scope, so it is visible on arrival and can be adjusted or cleared with the page's ordinary controls. Controls the narrowing does not concern SHALL be left at their defaults, and the resulting set SHALL match the number that was followed.

A narrowing SHALL be applied once, on arrival. Changing any of those controls afterwards SHALL take effect and SHALL NOT be reverted, and dismissing the scope SHALL clear the narrowing along with it. Returning to the page with the browser's back or forward buttons SHALL restore the controls as they were left, not as they arrived.

#### Scenario: Arriving from a recap
- **WHEN** I open my list from a fall 2019 recap narrowed to TV
- **THEN** the list shows exactly the TV anime that recap included, across every watch status they hold

#### Scenario: The scope is labelled
- **WHEN** my list is showing a recap scope
- **THEN** an indicator names the period, time filter, and media type the scope represents

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** the full list returns, with my status filter, filter-bar selections, and sort untouched

#### Scenario: Filtering within a scope
- **WHEN** a recap scope is active and I select the Completed status tab
- **THEN** only completed entries from within the scoped set are shown, not completed entries from the whole list

#### Scenario: Counting within a scope
- **WHEN** a recap scope is active and further filters narrow it
- **THEN** the "showing N of M" line reports N and M against the scoped set

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
- **THEN** the change takes effect and is not reverted, and "Clear filters" restores the page's defaults within the scope

#### Scenario: Dismissing clears the narrowing too
- **WHEN** I arrive from a recap stat and dismiss the scope indicator
- **THEN** the full list returns with the arrival narrowing gone, not reapplied

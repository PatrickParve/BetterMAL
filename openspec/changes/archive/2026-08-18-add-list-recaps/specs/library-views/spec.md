## ADDED Requirements

### Requirement: Recap a period control on my list
The system SHALL offer a **Recap a period** control on the my-list page, in the same row as the status filter tabs and visually separated from them, so that recapping a period is reachable from the list it recaps. The control is a navigation entry point rather than a filter or sort control, so it SHALL sit outside the filter bar and SHALL NOT change which entries the list is showing when it is opened or dismissed.

Activating the control SHALL open an overlay in which the user picks the recap type (multi-year, yearly, or season) and the settings that type needs — the years for a multi-year recap, the year for a yearly one, the year and season for a season one, and the time filter for the multi-year and yearly types. Confirming the selection SHALL open the recap for that period; dismissing the overlay SHALL leave the my-list page exactly as it was.

The overlay SHALL NOT offer a period or time filter that would produce an empty recap, applying the same availability rule the recap itself uses: an option covering no entries is shown as unavailable and cannot be confirmed.

#### Scenario: Opening the recap picker
- **WHEN** I activate **Recap a period** on the my-list page
- **THEN** an overlay opens offering the multi-year, yearly, and season recap types with the settings each needs

#### Scenario: Confirming a period
- **WHEN** I pick a yearly recap for 2022 and confirm
- **THEN** the recap for 2022 opens

#### Scenario: Dismissing the picker
- **WHEN** I open the overlay and dismiss it without confirming
- **THEN** the my-list page is unchanged — same status filter, same filter bar selections, same scroll position

#### Scenario: The control sits with the status tabs
- **WHEN** the my-list page renders
- **THEN** **Recap a period** appears in the status-tab row, set apart from the All/Watching/Completed/Plan to watch/On hold/Dropped tabs, and not among the filter bar's controls

#### Scenario: Unavailable options cannot be confirmed
- **WHEN** the picker offers a time filter that would include no entries for the selected period
- **THEN** that option is shown as unavailable and cannot be confirmed

### Requirement: My list opens scoped to a recap period
The system SHALL let the my-list page open scoped to a recap's period, time filter, and media type, arriving from the recap's "see all" control, and SHALL then show exactly the anime that recap included — no more and no fewer — whatever their watch statuses.

The scope SHALL be carried in the page URL so it survives a reload and back-navigation, and SHALL be shown on the page as a labelled, dismissible indicator naming the period it represents, so a narrowed list is never mistaken for the whole list. Dismissing it SHALL return the page to the unscoped list without disturbing the status filter, filter bar, or sort selections.

While a recap scope is active the page's own status tabs, filter bar, and sorting SHALL continue to work, narrowing and ordering within the scoped set rather than escaping it. The page's "showing N of M" count SHALL report against the scoped set.

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

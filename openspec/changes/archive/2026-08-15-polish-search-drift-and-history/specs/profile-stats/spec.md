## MODIFIED Requirements

### Requirement: Full edit-history overlay
The system SHALL provide, in the top-right corner of the "Latest updates" box, a control that opens a full history of every edit as an overlay on top of the page, which closes on Esc or a click outside it.

The overlay SHALL also be dismissable from a close control in its own top-right corner, aligned with its title: an icon control (a ✕) rather than a labelled button beneath the list. It SHALL carry an accessible label naming what it does, SHALL be reachable and operable by keyboard, and SHALL show a hover state in the same style as the app's other icon controls. It SHALL remain visible and in place regardless of how far the history list is scrolled. No other close control SHALL be offered.

Each history row SHALL show the anime's poster picture alongside its title, in the same style as the rows of the "Latest updates" box, and SHALL show the anime's English title when one is available, falling back to the default title otherwise. A row's title SHALL be truncated to at most two lines, with the full title available on hover.

The history list SHALL show five whole rows when the overlay opens, with no part of a sixth row visible beneath them and no row clipped part-way. Its height SHALL be derived from the height of a row rather than from the viewport, so the same five whole rows are shown at any window height; the rest of the history SHALL be reached by scrolling the list. A history holding fewer than five rows SHALL show what it has without reserving the height of five.

Each history row SHALL describe its change as a single phrase, using the same phrasing as the "Latest updates" feed, and SHALL NOT pair a change-type label with a detail that restates it. Change types the feed omits — status changes, start-date changes, and finish-date changes — SHALL appear in the history, each named once by the same rule: a status change reporting the status it moved from and to, a date change reporting the new date or that the date was cleared.

Unlike the "Latest updates" feed, the full history SHALL NOT drop any episode-watched entry, and SHALL NOT collapse repeated edits of the same field into the newest one. A consecutive run of episode-watched entries for the same anime SHALL instead collapse into a single row reporting the range of episodes watched (for example, "Episodes 4-8") rather than one row per episode; a run of a single entry SHALL keep its own "Episode N" wording.

A score set as part of finishing an anime SHALL be reported on the completion's own row rather than as a second row, on the same terms as in the "Latest updates" feed — whether saved together with the completion or separately moments later. The episode run that led to the completion SHALL keep its own range row rather than being folded into the completion row.

The overlay SHALL let me narrow the history by anime title and by date:

- A search field SHALL filter rows to those whose anime title matches what I typed, matching case-insensitively against both the default and the English title, on any part of the title rather than only its start.
- A start date and an end date SHALL each filter rows to those on or after, and on or before, that date, interpreted as whole days in my local time. Either may be left empty, leaving that end of the range open.
- The search and the date bounds SHALL apply together, and SHALL be clearable back to the unfiltered history without closing the overlay.
- When filters match nothing, the overlay SHALL say so rather than render an empty list.
- Filtering SHALL apply to the collapsed rows the history already shows, and SHALL NOT require a reload of the page or a refetch per keystroke.

#### Scenario: Opening the full history
- **WHEN** I click the history control in the Latest updates box
- **THEN** an overlay opens listing the full edit history

#### Scenario: Closing the history overlay
- **WHEN** the history overlay is open and I press Esc or click outside it
- **THEN** the overlay closes

#### Scenario: Closing from the corner control
- **WHEN** I click the ✕ in the overlay's top-right corner
- **THEN** the overlay closes, and no separate Close button is shown beneath the list

#### Scenario: The close control is keyboard-operable
- **WHEN** I tab to the ✕ and press Enter or Space
- **THEN** the overlay closes, and while focused the control is clearly marked as focused

#### Scenario: Five whole rows on open
- **WHEN** the overlay opens on a history with more than five entries
- **THEN** five rows are visible in full, no part of a sixth is shown, and no row is cut through the middle

#### Scenario: Whole rows at any window height
- **WHEN** the overlay is opened in a taller or shorter window
- **THEN** it still shows five whole rows rather than a fraction of a row

#### Scenario: The rest of the history is still reachable
- **WHEN** the history holds more than five rows
- **THEN** the remainder is reached by scrolling the list, and the ✕ stays in place while it scrolls

#### Scenario: A short history
- **WHEN** the history holds two rows
- **THEN** the overlay shows those two rows without reserving the height of five

#### Scenario: English titles in the history
- **WHEN** a history row's anime has a stored English title
- **THEN** that row shows the English title rather than the default title

#### Scenario: History rows show pictures
- **WHEN** the history overlay lists an anime
- **THEN** that row shows the anime's poster picture next to its title

#### Scenario: Long history title
- **WHEN** a history row's title is longer than two lines
- **THEN** it is cut off at two lines and hovering it reveals the full title

#### Scenario: A history row reads as one phrase
- **WHEN** any row of the full history renders
- **THEN** its change is described once, with no label-and-detail pair that repeats the same wording

#### Scenario: Consecutive episode-watched entries collapse into a range
- **WHEN** an anime's episode count was increased several times in a row
- **THEN** the full history shows a single row reporting the range of episodes watched, not one row per increase

#### Scenario: A finished anime in the history
- **WHEN** I watch an anime's last episodes, which completes it, and score it
- **THEN** the history shows one row reporting the completion together with the score, and above the episode-range row for the run that led to it

#### Scenario: Repeated edits are all kept
- **WHEN** I change an anime's score several times in a row
- **THEN** the full history shows a row for each of those changes

#### Scenario: Searching by title
- **WHEN** I type part of an anime's title into the history's search field
- **THEN** only rows whose anime matches that text, by either its default or English title, remain listed

#### Scenario: Filtering by a date range
- **WHEN** I set a start date and an end date in the history overlay
- **THEN** only rows timestamped within those days, inclusive of both, remain listed

#### Scenario: An open-ended date bound
- **WHEN** I set only a start date
- **THEN** every row from that day onwards remains listed and earlier rows are hidden

#### Scenario: Search and dates combined
- **WHEN** both a title search and a date bound are set
- **THEN** only rows matching the title and falling inside the date range remain listed

#### Scenario: Clearing the filters
- **WHEN** I clear the search field and the date inputs
- **THEN** the full unfiltered history is listed again without closing the overlay

#### Scenario: Filters match nothing
- **WHEN** the active filters exclude every row
- **THEN** the overlay states that no history matches rather than showing an empty list

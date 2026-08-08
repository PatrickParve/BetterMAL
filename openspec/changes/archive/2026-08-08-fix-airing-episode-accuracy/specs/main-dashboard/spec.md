## MODIFIED Requirements

### Requirement: Aired-episode count for followed airing shows
The dashboard data for the followed-shows-airing section SHALL include, per anime, the number of episodes that have aired as of the current instant. The count SHALL be the highest episode number among that anime's stored per-episode airing rows whose air instant has passed. An episode SHALL be counted as aired only once its stored air instant has passed.

The count SHALL NOT be estimated from a weekly broadcast cadence, from elapsed time since the anime's start date, or from any other projection. When the anime has no stored airing rows, the count SHALL be reported as unknown rather than guessed or reported as zero.

The count SHALL NOT be clamped to the anime's total episode count from MyAnimeList, and SHALL NOT be substituted with that total for an anime that has finished airing. A finished anime's stored rows already cover its whole run.

#### Scenario: Aired count from stored per-episode rows
- **WHEN** the aired count is computed for an anime whose stored rows place episodes 1 through 4 in the past and episode 5 in the future
- **THEN** the reported aired count is 4

#### Scenario: Today's episode has not aired yet
- **WHEN** the aired count is computed on a day this anime broadcasts, before the stored air instant of that day's episode has passed
- **THEN** today's episode is not counted as aired

#### Scenario: Finished show counts every stored episode
- **WHEN** the aired count is computed for an anime that has finished airing and has stored rows for all of its episodes
- **THEN** the reported aired count equals the highest stored episode number

#### Scenario: Show on hiatus does not accrue episodes
- **WHEN** the aired count is computed for an anime that stopped broadcasting ten weeks ago and has no stored episode rows since
- **THEN** the count is the highest episode number that actually aired, and does not grow by a further ten

#### Scenario: Count exceeds the MyAnimeList total
- **WHEN** the aired count is computed for an anime with a stored, already-aired row for episode 13 whose cached MyAnimeList total is 12
- **THEN** the reported aired count is 13 rather than 12

#### Scenario: Aired count is unknown
- **WHEN** the aired count is computed for an anime with no stored airing rows
- **THEN** the aired count is reported as unknown rather than as zero or an estimate

#### Scenario: Unknown count is rendered as unknown
- **WHEN** a followed-shows-airing card renders for an anime whose aired count is unknown
- **THEN** the card shows an explicit unknown marker in place of the count rather than the number zero

### Requirement: Next-episode countdown on currently-watching cards
The system SHALL show, on each currently-watching card, a countdown to the next episode in the form "Next ep: in X days, Y h", computed from the earliest stored per-episode air instant still in the future, converted to local time.

The countdown SHALL NOT be projected from the anime's broadcast day and time when no future stored episode row exists.

#### Scenario: Showing the countdown
- **WHEN** a currently-watching card is rendered for an anime with a stored episode row whose air instant is in the future
- **THEN** it shows the time remaining until that instant as "Next ep: in X days, Y h"

#### Scenario: No known next episode
- **WHEN** a currently-watching anime has no stored episode row with a future air instant
- **THEN** the card omits the next-episode countdown rather than showing a stale or projected value

#### Scenario: Countdown skips a break
- **WHEN** a currently-watching anime is on a one-week break and its next stored episode is two weeks out
- **THEN** the countdown counts to that instant rather than to the next weekly broadcast slot

### Requirement: Airing today filtered to my list in local time
The system SHALL show an "Airing today" section as a list column (not a grid) containing only anime in my list that have a stored per-episode airing row whose air instant converts to today's local date, where each row links to the anime's detail page.

Each row SHALL be laid out as a thumbnail image beside two stacked lines of text. The first line SHALL read `time : Ep N`, where the time and the episode number are those of the stored airing row for that local date. The second line SHALL be the anime's display title.

The row thumbnail SHALL be a poster at the same 2:3 aspect ratio used by anime cards elsewhere in the app, and SHALL be large enough to read as the row's own picture rather than an inline icon — clearly taller than the two lines of text beside it, so the poster, not the text, sets the row's height.

The two text lines SHALL be aligned to the top of the row rather than centred against the thumbnail: the `time : Ep N` line SHALL begin at the top of the thumbnail, with the title starting directly beneath it and the remaining space falling below the text.

The title SHALL be clamped to at most two lines, with an ellipsis (`…`) marking a title cut short, so no single row can grow unbounded in height. The ellipsis SHALL appear only when the title genuinely overflows at the section's rendered width — a title that fits SHALL be shown in full, with no ellipsis and no truncation at a fixed character count.

The section SHALL NOT list an anime for which no stored airing row falls on today's local date, even when its cached broadcast day matches today. No row's episode number SHALL be projected from a broadcast cadence.

The dashboard payload backing this section SHALL carry the episode number of the stored row per anime.

#### Scenario: Local-day airing filter
- **WHEN** the main page loads
- **THEN** "Airing today" lists only my-list anime with a stored episode air instant converting to today's local date

#### Scenario: Row layout
- **WHEN** an "Airing today" row renders for an anime whose stored episode 4 airs at 19:30 local time
- **THEN** the row shows its poster thumbnail beside `19:30 : Ep 4` with the anime's title on the line below

#### Scenario: Poster anchors the row
- **WHEN** an "Airing today" row renders
- **THEN** its thumbnail is a 2:3 poster taller than the two text lines beside it, and the poster's height sets the row's height

#### Scenario: Text starts at the top of the row
- **WHEN** an "Airing today" row renders with a one-line title
- **THEN** the `time : Ep N` line sits at the top of the row level with the top of the poster, the title sits directly below it, and the leftover space is below the title rather than split above and below the text

#### Scenario: Long title clamped
- **WHEN** an "Airing today" row's title is too long to fit on two lines at the section's width
- **THEN** the title is cut off at the end of the second line with an ellipsis rather than wrapping further

#### Scenario: Short title shown in full
- **WHEN** an "Airing today" row's title fits within two lines at the section's width
- **THEN** it is shown in full with no ellipsis

#### Scenario: Anime on break today
- **WHEN** a my-list anime's cached broadcast day is today but it has no stored episode airing today because it is on a break
- **THEN** it does not appear in "Airing today"

#### Scenario: Nothing airing today
- **WHEN** no my-list anime have a stored episode airing today in local time
- **THEN** the section shows a small message indicating nothing is airing today

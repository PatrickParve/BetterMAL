## MODIFIED Requirements

### Requirement: Single anime detail layout
The system SHALL show a single anime page whose title sits in the page's own header block, per the `page-header-design` capability, above a body carrying a large picture on the left and, near the top-right, two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box whenever the anime has a synopsis or a background. Any info field for which no data is available SHALL display "No info" rather than being blank.

The synopsis/background box SHALL carry a Synopsis section only when the anime has a synopsis, and a Background section only when it has a background. When the anime has neither, the box SHALL be omitted entirely: no empty box, no heading, and no placeholder text such as "No synopsis available." A synopsis or background that is empty or consists only of whitespace SHALL count as absent. The "No info" rule applies to the info box's fields only, not to the synopsis/background box.

The title SHALL NOT sit flush against the top of the page's content area: the header block SHALL own the spacing above and below the title so it reads as this page's header rather than as a line of text the body was pushed down by. The related-entry links (Series, Main series, More, Prequel, Sequel) SHALL keep sharing the title's row, positioned as they are today, rather than moving into the body.

The two score boxes SHALL be sized to the content they hold rather than stretched to fill the width of the main column: they SHALL NOT each take half the column's width, and their internal spacing SHALL be proportionate to the few short lines inside them rather than reusing the padding of a full-width panel such as the info or synopsis box. The pair SHALL read as a compact figure block beside the title, not as two large empty panels. They SHALL remain side by side at ordinary widths and SHALL keep stacking on narrow viewports, and the info and synopsis boxes below SHALL keep their existing full-width sizing.

Within those two boxes, the MAL score and my score SHALL each be presented as a plain coloured number on a labelled line, in the same `Label: value` form as the rank and popularity lines beside them — NOT as a tinted, bordered chip. No tint, border, or block SHALL be drawn around either score. The colour SHALL be carried by the number itself: blue for the MAL score, purple for my score, per the `score-presentation` capability's colour roles. The label SHALL stay in ordinary text rather than taking the score's colour, and the two score lines SHALL sit in the same line rhythm as the other lines in their box rather than as a raised figure among them. The boxes themselves SHALL remain, keeping their border and their existing content.

Removing the chip SHALL NOT change what the MAL score does: it SHALL still respect the hide/unhide toggle, still offer its per-score reveal control in the MAL colour, and still keep a hidden value out of the rendered output, exactly as it does elsewhere.

The info box's Status field SHALL, when the anime is currently airing and an aired-episode count is known, read `Currently airing: <aired>/<total> ep aired`, using `?` in place of an unknown total episode count. When the anime is currently airing but no aired-episode count is known, the field SHALL read `Currently airing` with no counts appended. Statuses other than currently airing SHALL be displayed unchanged.

The aired-episode count SHALL come from the anime's stored per-episode airing rows and SHALL NOT be estimated from its broadcast cadence or from elapsed time since its start date. An anime with no stored airing rows SHALL be treated as having no known aired count.

#### Scenario: Rendering the detail layout
- **WHEN** I open the detail page of an anime that has a synopsis
- **THEN** it shows the title in the page's header block above a body with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

#### Scenario: No synopsis and no background
- **WHEN** I open the detail page of an anime that has neither a synopsis nor a background
- **THEN** no synopsis/background box is rendered at all — no empty box, no "Synopsis" heading, and no "No synopsis available." text — and the info box is the last box in the main column

#### Scenario: Synopsis without a background
- **WHEN** I open the detail page of an anime that has a synopsis but no background
- **THEN** the box shows the Synopsis section only, with no Background heading

#### Scenario: Background without a synopsis
- **WHEN** I open the detail page of an anime that has a background but no synopsis
- **THEN** the box shows the Background section only, with no Synopsis heading and no placeholder in its place

#### Scenario: Whitespace-only text counts as absent
- **WHEN** an anime's synopsis is stored as an empty or whitespace-only string and it has no background
- **THEN** the synopsis/background box is omitted, exactly as when no synopsis is stored

#### Scenario: The title has room above it
- **WHEN** I open an anime's detail page
- **THEN** its title sits in a header block with spacing above and below it, rather than flush against the top of the page's content area

#### Scenario: Related links stay beside the title
- **WHEN** I open the detail page of an anime that has a prequel and a sequel
- **THEN** the related-entry links are still on the title's row, to its right, rather than moved into the body

#### Scenario: Scores are coloured numbers, not chips
- **WHEN** I open an anime's detail page for an entry I have scored
- **THEN** the MAL score appears as a blue number and my score as a purple number, each on its own labelled line, with no tinted or bordered block around either

#### Scenario: A score line reads like the lines beside it
- **WHEN** I look at the rank/MAL-score box
- **THEN** the MAL score's line has the same form and spacing as the rank and popularity lines above it, differing only in the colour of its number

#### Scenario: A hidden MAL score without a chip
- **WHEN** the hide toggle is on and I open an anime's detail page
- **THEN** the MAL score's line shows its reveal control in the MAL colour rather than a value, and revealing it shows the score in place

#### Scenario: Score boxes are sized to their content
- **WHEN** I open an anime's detail page for an entry I have scored
- **THEN** the two score boxes take only the width and height their few lines of content need, rather than each stretching across half the main column with large empty space inside

#### Scenario: The boxes below keep their width
- **WHEN** I open an anime's detail page
- **THEN** the info box and, when present, the synopsis box below the score boxes still span the full width of the main column

#### Scenario: MAL score respects the hide toggle
- **WHEN** the hide toggle is on
- **THEN** the MAL score in the rank/score box is hidden behind its reveal control like everywhere else it appears

#### Scenario: Missing info field
- **WHEN** an info field (e.g. source or duration) has no data for that anime
- **THEN** that field shows "No info" instead of a blank value

#### Scenario: Currently airing with a known episode count
- **WHEN** I open the detail page of an anime that is currently airing, has 12 total episodes, and has stored airing rows placing 5 episodes in the past
- **THEN** the Status field reads "Currently airing: 5/12 ep aired"

#### Scenario: Currently airing with an unknown total
- **WHEN** I open the detail page of a currently airing anime whose total episode count is unpublished and that has stored airing rows placing 5 episodes in the past
- **THEN** the Status field reads "Currently airing: 5/? ep aired"

#### Scenario: Currently airing with no stored airing rows
- **WHEN** I open the detail page of a currently airing anime that has no stored per-episode airing rows
- **THEN** the Status field reads "Currently airing" with no episode counts appended, and no count is estimated from its broadcast cadence

#### Scenario: Other airing statuses unaffected
- **WHEN** I open the detail page of an anime that has finished airing or has not yet aired
- **THEN** the Status field reads "Finished airing" or "Not yet aired" respectively, with no episode counts appended

### Requirement: Next-episode countdown in the Status field
When an anime has a stored per-episode airing row whose air instant is in the future, the detail page's Status field SHALL append a countdown to that next episode, expressed in whole days and whole hours (e.g. `Currently airing: 5/12 ep aired · next in 2d 7h`). The countdown SHALL be derived from the earliest stored future air instant, and SHALL NOT be estimated from the broadcast cadence, the start date, or elapsed time.

The time remaining SHALL be rounded **up** to the next whole hour before being split into days and hours, so the countdown never reads fewer hours than actually remain: any remainder under an hour reads `0d 1h`, a remainder of 2 days, 6 hours and 20 minutes reads `2d 7h`, and a remainder of 23 hours and 30 minutes reads `1d 0h`. A remainder that is already a whole number of hours SHALL be shown unchanged. The countdown SHALL never read `0d 0h` while the next episode is still in the future.

An anime with no stored future airing row SHALL show no countdown, whatever its airing status — including a currently-airing anime whose upcoming episodes have not been fetched.

The countdown SHALL be computed server-side against the request instant and delivered as a days/hours pair, through the same computation the currently-watching carousel's countdown uses, so the two surfaces always agree for the same episode.

#### Scenario: Currently airing with a stored future episode
- **WHEN** I open the detail page of a currently airing anime with 12 total episodes, 5 stored episodes in the past, and the next stored episode exactly 2 days and 7 hours away
- **THEN** the Status field reads "Currently airing: 5/12 ep aired · next in 2d 7h"

#### Scenario: A partial hour rounds up
- **WHEN** the next stored air instant is 2 days, 6 hours and 20 minutes from now
- **THEN** the countdown reads "next in 2d 7h"

#### Scenario: Rounding up carries into a day
- **WHEN** the next stored air instant is 23 hours and 30 minutes from now
- **THEN** the countdown reads "next in 1d 0h"

#### Scenario: Not yet aired with a stored premiere
- **WHEN** I open the detail page of an anime that has not yet aired and whose stored first episode is exactly 10 days away
- **THEN** the Status field reads "Not yet aired · next in 10d 0h"

#### Scenario: Currently airing with no stored future episode
- **WHEN** I open the detail page of a currently airing anime whose stored airing rows are all in the past
- **THEN** the Status field shows the aired counts with no countdown appended, and no countdown is estimated from its broadcast cadence

#### Scenario: Finished airing shows no countdown
- **WHEN** I open the detail page of an anime that has finished airing
- **THEN** the Status field reads "Finished airing" with no countdown appended

#### Scenario: Next episode less than an hour away
- **WHEN** the next stored air instant is 40 minutes from now
- **THEN** the countdown reads "next in 0d 1h" rather than "0d 0h" or being omitted

#### Scenario: One minute left
- **WHEN** the next stored air instant is 1 minute from now
- **THEN** the countdown reads "next in 0d 1h"

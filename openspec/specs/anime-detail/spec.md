# anime-detail Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Single anime detail layout
The system SHALL show a single anime page whose title sits in the page's own header block, per the `page-header-design` capability, above a body carrying a large picture on the left and, near the top-right, a box showing rank and MAL score (MAL score respecting the hide/unhide toggle) and, only once the anime has a score of mine, a separate box beside it showing my score, the rewatch count when it is not zero, and my finish date while my status is Completed — per the "The two score boxes share one size" requirement. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box whenever the anime has a synopsis or a background. Any info field for which no data is available SHALL display "No info" rather than being blank.

The synopsis/background box SHALL carry a Synopsis section only when the anime has a synopsis, and a Background section only when it has a background. When the anime has neither, the box SHALL be omitted entirely: no empty box, no heading, and no placeholder text such as "No synopsis available." A synopsis or background that is empty or consists only of whitespace SHALL count as absent. The "No info" rule applies to the info box's fields only, not to the synopsis/background box.

The title SHALL NOT sit flush against the top of the page's content area: the header block SHALL own the spacing above and below the title so it reads as this page's header rather than as a line of text the body was pushed down by. The related-entry links (Series, Main series, More, Prequel, Sequel) SHALL keep sharing the title's row, positioned as they are today, rather than moving into the body.

The two score boxes SHALL be sized to the content they hold rather than stretched to fill the width of the main column: they SHALL NOT each take half the column's width, and their internal spacing SHALL be proportionate to the few short lines inside them rather than reusing the padding of a full-width panel such as the info or synopsis box. The pair SHALL read as a compact figure block beside the title, not as two large empty panels. They SHALL remain side by side at ordinary widths and SHALL keep stacking on narrow viewports, and the info and synopsis boxes below SHALL keep their existing full-width sizing.

Within those two boxes, the MAL score and my score SHALL each be presented as a plain coloured number on a labelled line, in the same `Label: value` form as the rank and popularity lines beside them — NOT as a tinted, bordered chip. No tint, border, or block SHALL be drawn around either score. The colour SHALL be carried by the number itself: blue for the MAL score, purple for my score, per the `score-presentation` capability's colour roles. The label SHALL stay in ordinary text rather than taking the score's colour, and the two score lines SHALL sit in the same line rhythm as the other lines in their box rather than as a raised figure among them. The boxes themselves SHALL remain, keeping their border and their existing content.

Removing the chip SHALL NOT change what the MAL score does: it SHALL still respect the hide/unhide toggle, still offer its per-score reveal control in the MAL colour, and still keep a hidden value out of the rendered output, exactly as it does elsewhere.

The info box's Status field SHALL, when the anime is currently airing and an aired-episode count is known, read `Currently airing: <aired>/<total> ep aired`, using `?` in place of an unknown total episode count. When the anime is currently airing but no aired-episode count is known, the field SHALL read `Currently airing` with no counts appended. Statuses other than currently airing SHALL be displayed unchanged.

The aired-episode count SHALL come from the anime's stored per-episode airing rows and SHALL NOT be estimated from its broadcast cadence or from elapsed time since its start date. An anime with no stored airing rows SHALL be treated as having no known aired count.

#### Scenario: Rendering the detail layout
- **WHEN** I open the detail page of an anime that has a synopsis and a score of mine
- **THEN** it shows the title in the page's header block above a body with a large picture on the left, a "rank and MAL score" box and a separate "my score" box — showing my score, the rewatch count when it is not zero, and my finish date while my status is Completed — side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

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

### Requirement: The detail page title wraps at spaces only

When an anime's title on the detail page is too long for one line, it SHALL wrap at a **space**. A word or token SHALL NOT be split across two lines: a token containing a hyphen, en or em dash, colon, slash, full stop, or any other punctuation SHALL move to the next line whole, together with whatever follows it, rather than being broken at that punctuation with a fragment left hanging at the end of the first line.

This SHALL hold at every window width and at every size of the app's fluid root font, and for a title of any number of lines — every break in it falls at a space.

Nothing else about the title changes: it SHALL keep the same font, size, weight, colour, and position in the page's header block, SHALL keep sharing its row with the related-entry controls, and SHALL still be the English-preferred display title. The text SHALL be selectable and copyable as the plain title it is, with no characters inserted into it that would appear in what is copied.

A single token too long to fit on a line by itself MAY still be broken, since the alternative is text running outside the page. This is a last resort for a token no ordinary title contains, not a licence to break tokens that would fit.

#### Scenario: A title with a colon wraps at a space
- **WHEN** the detail page shows a title long enough to need two lines, containing a colon mid-title
- **THEN** the break falls at a space, and no part of a word sits alone at the end of the first line

#### Scenario: A hyphenated token is not split
- **WHEN** a two-line title contains a hyphenated token such as "Re:ZERO -Starting Life in Another World-"
- **THEN** the token stays whole on one line rather than breaking at its hyphen or dash

#### Scenario: A one-line title is unaffected
- **WHEN** a title fits on one line
- **THEN** it renders exactly as it does today

#### Scenario: The title is still copyable
- **WHEN** I select the title and copy it
- **THEN** what I get is the title itself, with no extra or invisible characters

#### Scenario: The related controls stay put
- **WHEN** a title wraps to two lines
- **THEN** the related-entry controls remain on the title's row as they are today

### Requirement: The two score boxes share one size

The detail page's two score boxes — the MAL box (MAL score, rank, popularity) and my box (my score, and the rewatch count and finish date when it has them) — SHALL be drawn at one shared width: that of whichever of the two needs more room for its own content. Neither box SHALL be narrower than the other, whatever it happens to hold.

That shared width SHALL be the wider box's natural content width, so the pair stays sized to its content and is not stretched to fill the main column, per "Single anime detail layout". The two boxes SHALL also keep the shared height they have today, so the pair reads as one matched figure block beside the title.

Whichever box is wider SHALL be allowed to vary with the anime: my box grows as it gains a rewatch-count line and a finish-date line, and it SHALL take the pair with it when it becomes the wider of the two.

Where my box is not shown at all — no score of my own — the MAL box SHALL render at its own content width, exactly as it does today.

Where the viewport is narrow enough that the two boxes stack, they SHALL keep one shared width in that stacked arrangement too.

#### Scenario: My box is the wider one

- **WHEN** the detail page shows my score, a rewatch count, and a finish date beside the MAL box
- **THEN** both boxes are drawn at the width my box needs, and neither is narrower than the other

#### Scenario: The MAL box is the wider one

- **WHEN** my box holds my score alone and the MAL box's rank and popularity lines are longer
- **THEN** both boxes are drawn at the width the MAL box needs

#### Scenario: The pair stays content-sized

- **WHEN** either box is the wider one
- **THEN** the pair still occupies only the width its content needs, rather than each box taking half the main column

#### Scenario: The boxes stay level

- **WHEN** the two boxes hold a different number of lines
- **THEN** they are drawn at the same height as well as the same width

#### Scenario: Only the MAL box is shown

- **WHEN** the anime has no score of mine and only the MAL box is drawn
- **THEN** that box is sized to its own content, unchanged from today

#### Scenario: Stacked on a narrow viewport

- **WHEN** the viewport is narrow enough that the two boxes stack
- **THEN** both boxes are still drawn at one shared width

### Requirement: Portrait artwork is shown at its own proportions on the detail page
An anime whose picture is portrait — its intrinsic height greater than or equal to its intrinsic width — SHALL have that picture rendered at its own proportions on the detail page. The whole image SHALL be visible: no part of it SHALL be cut off to make it fill a box of a fixed height.

The picture SHALL keep the width the poster box already has and take whatever height its own proportions give it at that width. A picture whose proportions are taller than the poster box's SHALL therefore be rendered taller than the box rather than centre-cropped to it, and a picture whose proportions are shorter SHALL be rendered shorter rather than cropped to fill it. No maximum height SHALL be imposed: an unusually tall poster SHALL be shown whole.

Everything beside the picture SHALL be unaffected: the page's two-column body, the score, info, and synopsis boxes to its right, and the width of the column the picture sits in SHALL all be exactly as they are today. Only the picture's own height, and the position of the progress controls stacked beneath it in the same column, SHALL follow from the artwork.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL reserve the existing portrait poster box's height until then, so the column does not collapse and then expand as the artwork arrives. The page SHALL NOT request, store, or wait on any additional data to make this decision.

The placeholder shown when an anime has no picture at all SHALL keep the existing fixed poster box, since it has no artwork whose proportions could be adopted.

#### Scenario: A taller-than-usual poster is not cropped
- **WHEN** I open the detail page of an anime whose poster is taller in proportion than the page's poster box — for example a season whose key art is unusually tall
- **THEN** the whole poster is shown, as wide as the poster box has always been and taller than it, rather than a centre-cropped slice of it

#### Scenario: An ordinary poster is unchanged in width
- **WHEN** I open the detail page of an anime whose poster is close to the poster box's proportions
- **THEN** it is drawn at the same width it always has been, with its own height, and looks as it did before

#### Scenario: A shorter poster is not stretched or cropped
- **WHEN** I open the detail page of an anime whose poster is shorter in proportion than the poster box
- **THEN** it is shown whole at its own height rather than being cropped to fill a taller box

#### Scenario: The page beside the picture does not move
- **WHEN** I open the detail page of an anime with an unusually tall poster
- **THEN** the score, info, and synopsis boxes beside it sit exactly where they do for any other anime, and the column holding the picture is the same width

#### Scenario: The column does not collapse while the image loads
- **WHEN** I open a detail page and the picture has not finished loading
- **THEN** the column reserves the poster box's usual height, and the content beneath the picture does not jump when the image arrives

#### Scenario: A missing picture keeps the portrait placeholder
- **WHEN** I open the detail page of an anime that has no picture
- **THEN** the placeholder occupies the fixed portrait poster box as it does today

### Requirement: Landscape artwork is shown whole on the detail page
An anime whose picture is landscape — its intrinsic width greater than its intrinsic height — SHALL have that picture rendered at its own proportions on the detail page rather than cropped to the page's portrait poster box. The whole image SHALL be visible: no part of it SHALL be cut off to make it fill a taller box.

Landscape artwork SHALL additionally be shown **wider** than the portrait poster box: the column holding it SHALL widen to a size that lets a wide, short image read as the page's subject, and the picture SHALL take that width, with whatever height its own proportions give it there. This widening is what distinguishes landscape artwork from portrait artwork, which keeps the poster box's width (see "Portrait artwork is shown at its own proportions on the detail page").

The controls beneath the picture SHALL follow the widened column, and every box beside it SHALL keep its existing position and width.

Because a picture's proportions are not known until the image itself has loaded, the page SHALL render the existing portrait box until then and adopt the landscape treatment once the artwork is known to be landscape. The page SHALL NOT request, store, or wait on any additional data to make this decision.

The placeholder shown when an anime has no picture at all SHALL keep the existing portrait poster box, since there is no artwork to measure.

#### Scenario: A landscape picture is not cropped
- **WHEN** I open the detail page of an anime whose picture is wider than it is tall
- **THEN** the whole picture is shown at its own proportions — a wide, shorter image — rather than a centre-cropped portrait slice of it

#### Scenario: Landscape artwork is shown wider than a poster
- **WHEN** I open that same detail page
- **THEN** the picture and the column holding it are wider than the portrait poster box, and the progress controls beneath the picture span that widened column

#### Scenario: The boxes beside a landscape picture are unchanged
- **WHEN** I open that same detail page
- **THEN** the score, info, and synopsis boxes beside the picture sit exactly where they do for any other anime

#### Scenario: Portrait artwork is not widened
- **WHEN** I open the detail page of an anime whose picture is taller than it is wide
- **THEN** its picture is drawn at the poster box's width, not the wider landscape width, and takes its own height there

#### Scenario: A missing picture keeps the portrait placeholder
- **WHEN** I open the detail page of an anime that has no picture
- **THEN** the placeholder occupies the portrait poster box as it does today

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

### Requirement: Rating and season in the info box
The info box SHALL show the anime's MAL content rating (e.g. G, PG, PG-13, R, R+, Rx) and the season it aired in (e.g. "Spring 2026"), the latter derived from its aired-from date the same way the app already derives season membership elsewhere. The season value SHALL be a link to that season's browse page. When aired-from is unknown, no season link SHALL be shown.

#### Scenario: Rating is shown
- **WHEN** I open the detail page of an anime rated PG-13
- **THEN** the info box shows "PG-13" as the Rating

#### Scenario: Season links to the season page
- **WHEN** I open the detail page of an anime that first aired in Spring 2026
- **THEN** the info box shows "Spring 2026" as a link, and clicking it navigates to that season's browse page

#### Scenario: No season shown without an aired-from date
- **WHEN** an anime's aired-from date is unknown
- **THEN** the info box shows no season value

### Requirement: Single-day aired range
When an anime's aired-from and aired-to dates fall on the same day — a movie or special released in a single day — the info box's Aired field SHALL show that one date rather than a from–to range. When the dates differ, or either is unknown, the existing from–to (or from–"No info") rendering applies unchanged.

#### Scenario: Movie aired in a single day
- **WHEN** I open the detail page of a movie whose aired-from and aired-to dates are both August 6, 2022
- **THEN** the Aired field reads "Aug 6, 2022" rather than "Aug 6, 2022 – Aug 6, 2022"

#### Scenario: Multi-day range is unaffected
- **WHEN** an anime's aired-from and aired-to dates differ
- **THEN** the Aired field shows the existing from–to range

### Requirement: Per-episode duration label
Since the stored average-episode-duration is always a per-episode figure, the info box's Duration field SHALL append `/ep` to the duration value whenever the anime has more than one episode or its episode count is not yet known. When the anime is a single-episode release (`TotalEpisodes == 1`), the field SHALL show the plain duration with no suffix.

The duration value itself SHALL be rendered from the stored seconds rounded to whole minutes, in one of two forms:

- Under 60 minutes: the minute count with a `min` unit (e.g. `24 min`).
- 60 minutes or more: whole hours and remaining minutes (e.g. `1h 55min`), with the minutes part omitted when the duration is a whole number of hours (e.g. `2h`).

The threshold SHALL be applied to the rounded per-episode duration itself, not to the anime's media type, so that a feature-length OVA or special is formatted the same way a movie is. The `/ep` suffix SHALL compose onto the hours-and-minutes form unchanged.

#### Scenario: Series duration is labelled per episode
- **WHEN** I open the detail page of a 24-episode series whose average episode duration is 24 minutes
- **THEN** the Duration field reads "24 min/ep"

#### Scenario: Movie duration in hours and minutes
- **WHEN** I open the detail page of a one-episode movie whose duration is 115 minutes
- **THEN** the Duration field reads "1h 55min"

#### Scenario: Whole-hour duration omits the minutes
- **WHEN** I open the detail page of a one-episode movie whose duration is exactly 120 minutes
- **THEN** the Duration field reads "2h"

#### Scenario: Long-episode series keeps the per-episode suffix
- **WHEN** I open the detail page of a multi-episode anime whose average episode duration is 65 minutes
- **THEN** the Duration field reads "1h 5min/ep"

#### Scenario: Exactly at the threshold
- **WHEN** an anime's rounded per-episode duration is exactly 60 minutes
- **THEN** the Duration field reads "1h" rather than "60 min"

#### Scenario: Ongoing anime with an unknown episode count
- **WHEN** I open the detail page of a currently airing anime whose total episode count is not yet known
- **THEN** the Duration field appends "/ep", since an unknown count is treated as more than one episode

### Requirement: Prequel and sequel controls are always present
The system SHALL show a prequel control and a sequel control in the top-right corner of the detail page for **every** anime, whether or not such a relation exists, so the related-links row keeps one shape from anime to anime and a control never moves out from under the pointer between two pages.

A control whose relation exists SHALL be a link to that related anime's detail page, exactly as it is today. A control whose relation does not exist SHALL be rendered in a visibly dimmed, inactive treatment: it SHALL NOT be clickable, SHALL NOT navigate anywhere, SHALL NOT be reachable by keyboard as an interactive control, and SHALL be reported as disabled to assistive technology. It SHALL occupy the same position and the same size as its enabled form, so no other control in the row shifts when a relation is absent.

The dimmed treatment SHALL be distinguishable at a glance from the enabled one, and a disabled control SHALL NOT take the hover treatment its enabled form takes.

The relations these controls are drawn from SHALL be the anime's full two-directional relation set — the edges it stores itself plus the inverted edges other anime store pointing at it — so a prequel or sequel that MyAnimeList recorded only on the other side still gets an enabled control here. An anime that is the target of another anime's `sequel` relation SHALL therefore show an enabled prequel control for it, whether or not it stores a `prequel` relation of its own.

When more than one candidate exists for a control, the target SHALL be the one the ranked resolution rules select, **not** the first that MyAnimeList reports; the remainder SHALL be reachable through the More overlay. An edge an external source contradicts SHALL NOT be given an enabled control, and its control SHALL be shown dimmed as though the relation were absent.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding control(s) are enabled and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel, in either direction
- **THEN** both controls are still shown, each dimmed and not clickable

#### Scenario: One of the two is absent
- **WHEN** an anime has a sequel but no prequel
- **THEN** the sequel control is enabled and the prequel control is shown dimmed in its usual place, with the sequel control in the same position it occupies on an anime that has both

#### Scenario: A dimmed control does nothing
- **WHEN** I click, tab to, or hover a dimmed prequel control
- **THEN** nothing is navigated to, no hover treatment is applied, and assistive technology reports the control as disabled

#### Scenario: A prequel MAL only recorded on the other side
- **WHEN** I open the detail page of an anime that stores no prequel relation, but which another anime names as its sequel
- **THEN** an enabled prequel control is shown linking to that other anime

#### Scenario: Multiple prequels
- **WHEN** an anime has two prequels
- **THEN** the prequel control links to the one the ranked resolution selects and the other is listed in the More overlay

#### Scenario: A contradicted edge gets no enabled control
- **WHEN** an anime's only sequel candidate is an edge an external source contradicts
- **THEN** the sequel control is shown dimmed, and the entry remains listed in the More overlay

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel, and SHALL present each anime's relations as the union of its own stored edges and the inverted edges other anime store pointing at it.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

An entry derived from an incoming edge SHALL be grouped under the relation that edge means from this anime's side, except where the relation has no confident inverse (`spin_off`, `adaptation`, or an unrecognized string), in which case the raw relation SHALL be used. Where an outgoing and an incoming edge name the same anime, one entry SHALL be shown, not two.

Every stored edge SHALL remain listed in this overlay, including one an external source contradicts. The overlay is where a relation the app declines to act on is still visible; withholding a button is a statement about confidence, not a reason to hide data.

MyAnimeList's `related_anime` data itself carries a media type per related node when requested via nested field selection (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request and store it directly — no separate fetch of the related anime is needed to learn its media type, and no backfill mechanism SHALL exist for this purpose. A relation row for which MAL reported no media type MAY fall back to a lookup in our own cached metadata for that related anime; if neither source has it, the entry SHALL show "Unknown", matching how an unknown media type is labelled elsewhere in the app.

When the anime being viewed is itself a side entry — that is, it has a `parent_story` relation in either direction, whether stored on itself or derived from another anime's `side_story` edge — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported from either side, no such button SHALL be shown.

Related-anime data (including each relation's media type) SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

#### Scenario: A relation MAL stored only on the other side appears in the overlay
- **WHEN** I open the detail page of an anime that stores no relations of its own, but which another anime names with a `side_story` relation
- **THEN** the More overlay lists that other anime under "Parent story"

#### Scenario: A contradicted relation is still listed
- **WHEN** an anime holds a relation an external source contradicts
- **THEN** the entry appears in the More overlay under its relation heading, even though it earns no dedicated button and is not traversed into a series

#### Scenario: Overlay rows show media type with no extra request
- **WHEN** I open the detail page of an anime with related entries never separately cached before, and then press More
- **THEN** every row already shows its media type (from the same full-detail fetch that loaded the page), with no additional request made for any of them

#### Scenario: Loading the detail page fetches nothing extra for relations
- **WHEN** I open the detail page of an anime with many related entries we have never cached
- **THEN** the single full-detail fetch for that page also resolves every relation's media type, and no separate request is made for any related anime

#### Scenario: A relation MAL cannot resolve shows Unknown
- **WHEN** MAL's related_anime data reports no media type for a relation, and our own cache has no metadata for that related anime either
- **THEN** that row shows "Unknown" beneath its title

#### Scenario: Navigating from the overlay
- **WHEN** I click a related anime in the More overlay
- **THEN** the overlay closes and the app navigates to that anime's detail page

#### Scenario: Anime with only a prequel and sequel
- **WHEN** I open the detail page of an anime whose only related entries, in either direction, are a prequel and a sequel
- **THEN** no "More" button is shown

#### Scenario: Side entry links back to its main series
- **WHEN** I open the detail page of a movie that MAL reports as having a parent story
- **THEN** a "Main series" button is shown and navigates to that parent anime's detail page

#### Scenario: Main-series button from the parent's own side_story edge
- **WHEN** I open the detail page of a movie that stores no `parent_story` relation, but whose parent story stores a `side_story` relation naming it
- **THEN** a "Main series" button is shown linking to that parent

#### Scenario: Main-series button absent without a parent story
- **WHEN** I open the detail page of an anime for which no parent story is reported from either side
- **THEN** no "Main series" button is shown

#### Scenario: Lean refresh preserves relations
- **WHEN** an anime with stored related-anime entries is refreshed as part of a season or top-anime listing fetch
- **THEN** its stored related-anime entries (including their media types) are left intact

#### Scenario: Closing the overlay
- **WHEN** the More overlay is open and I press Escape or click outside it
- **THEN** the overlay closes and the detail page is unchanged

### Requirement: Related-anime rows show English titles
Every related-anime row in the More overlay SHALL show the related anime's English title when one is known, falling back to its stored MyAnimeList title when none is — the same title choice every other surface in the app makes, so the same anime is not named one way on a card and another way in this list.

The English title SHALL be served with the related-anime data the detail page already loads, resolved from the app's own cached metadata for that anime. No additional request SHALL be made to learn it, and a related anime the cache holds no metadata for SHALL simply show its stored title.

The rows' grouping, ordering, media-type line, navigation, and hide-scores behaviour SHALL be unchanged.

#### Scenario: A related anime with an English title
- **WHEN** I open the More overlay of an anime whose side story is cached with the English title "Sword Art Online: Extra Edition"
- **THEN** that row is titled in English rather than in romaji

#### Scenario: A related anime with no English title
- **WHEN** a related anime has no English title in our cache
- **THEN** its row shows the title MyAnimeList stored for the relation

#### Scenario: No extra request for titles
- **WHEN** I open the detail page of an anime with many related entries and press More
- **THEN** every row is already titled, with no additional request made for any related anime

### Requirement: A failed data refresh is shown as a failure, not as an empty page
When the detail page's read reports that fetching fresh data was attempted and failed, the page SHALL say so and SHALL offer a retry, rather than rendering the relations row as though the anime simply has no relations.

The rest of the page SHALL still render from whatever cached data was returned. The retry SHALL re-read the anime, taking the same path a first visit does.

A page served after a failed fetch of a never-cached anime is otherwise indistinguishable from a correct render of an anime with no relations — same absent prequel, sequel, More, and Series controls — which is the one case where showing nothing is actively misleading.

#### Scenario: Failed fetch offers a retry
- **WHEN** I open an anime's detail page and the server reports that its refresh failed
- **THEN** the page tells me the data could not be refreshed and offers a retry control

#### Scenario: Retry re-reads the anime
- **WHEN** I press that retry control
- **THEN** the anime is read again and the page re-renders from the result

#### Scenario: A genuinely relation-less anime is not mislabelled
- **WHEN** I open the detail page of an anime whose data refreshed successfully and which has no related anime
- **THEN** no failure message is shown and the relations row is simply absent, as today

#### Scenario: Cached content still renders behind the notice
- **WHEN** a refresh fails for an anime that already had cached data
- **THEN** the page renders that cached data with the failure notice alongside it, rather than replacing the page with an error

### Requirement: Detail page reads itself once per visit
Opening an anime's detail page SHALL issue exactly one read of that anime, however many times the page's load effect runs, and SHALL NOT re-read it to observe a change that a mutation response already reported.

The page SHALL re-read the anime only for the manual **Refresh data** action, which is an explicit user request for fresh data.

#### Scenario: One read per visit
- **WHEN** I navigate to an anime's detail page
- **THEN** exactly one `GET /api/anime/{id}` is issued for that visit

#### Scenario: Completing an anime from the detail page
- **WHEN** I increment the last episode from the detail page and the completion-score prompt saves a score and closes
- **THEN** the page's displayed entry, progress bar, and score box update from the saved entry, and the anime is not re-read

#### Scenario: Refresh data still re-reads
- **WHEN** I press "Refresh data"
- **THEN** the anime is refreshed against MAL and AniList and the page re-reads it, as it does today

### Requirement: Progress bar and overlay status editor
The system SHALL show, below the picture, a progress bar (`watched/total`, or `watched/?` when the total is unknown) with the current status next to it. The `watched` count SHALL be directly editable in place, per the "Inline editable episode count" requirement, so a specific episode number can be set without opening the overlay. The edit button — the second of the three action buttons stacked beneath the progress bar, shown only once the anime is in my list — SHALL open an overlay on top of the page for updating episodes watched, rewatch count, and score, applying the list-editing business rules. The overlay SHALL also expose the start and finish date fields per the `list-editing` capability's "Start and finish dates are editable in the editor" requirement, behind the same collapsed "Dates" disclosure.

While the anime has aired no episode, the progress bar SHALL NOT be shown at all — no track, no `watched/total` count, and no increment control — per the "The editable progress row appears only once an episode has aired" requirement. The status text that sits beside the bar SHALL still be shown in that case, since it is the page's only statement of the entry's status and is not an edit control, and the action buttons beneath SHALL still be shown. The bar SHALL appear as specified above once the anime has aired an episode.

#### Scenario: Opening the editor
- **WHEN** I click the edit button beneath the progress bar
- **THEN** an overlay opens with fields for episodes watched, rewatch count, and score, plus a collapsed "Dates" disclosure for the start and finish date fields

#### Scenario: Saving an edit
- **WHEN** I change episodes watched, rewatch count, or score in the overlay
- **THEN** the change is saved with the standard start/complete-date, activity-log, and debounced-sync behavior

#### Scenario: Editing the count in place
- **WHEN** I click the `watched` count next to the detail page's progress bar, type a number, and confirm
- **THEN** episodes-watched is saved to that number and the bar and count update in place, without the overlay opening

#### Scenario: In-place edit unavailable without an entry
- **WHEN** the anime is not in my list, so no entry exists yet
- **THEN** the count is not editable in place, and the add buttons rather than an edit button are what put it in my list

#### Scenario: No bar before the first episode
- **WHEN** I open the detail page for an anime that has aired no episode
- **THEN** no progress bar, count, or increment control is shown, while the status text beside it and the action buttons beneath it are shown as usual

#### Scenario: The bar appears with the first episode
- **WHEN** that anime airs its first episode and I open its detail page again
- **THEN** the progress bar, count, and increment control are shown as they are for any other anime

### Requirement: Broadcast progress on the detail page progress bar
While the anime is currently airing, the detail page's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar, so how much of the show exists yet is visible alongside how much of it I have seen. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render exactly as it does today, with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When neither count is known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total` (or `watched/?` when the total is unknown) and SHALL NOT restate the aired count, which the info box's Status field already names. The `watched` count SHALL remain editable in place and the increment button SHALL remain, exactly as specified by the "Progress bar and overlay status editor" requirement — the aired fill is an addition to that bar, not a replacement for it.

When my episodes watched changes on this page — by increment, by in-place edit, or from the entry editor — my fill SHALL update in place without a reload, and the aired fill SHALL be unaffected.

This requirement describes fills within a bar that is being drawn. Where the "Progress bar and overlay status editor" requirement withholds the bar entirely — an anime that has aired no episode — there is no track for either fill, and this requirement has nothing to add.

#### Scenario: Airing anime shows broadcast progress
- **WHEN** I open the detail page of a currently airing anime with 12 total episodes, 5 aired, and 3 watched
- **THEN** a blue fill spans 5/12 of the track, a purple fill spans 3/12 on top of it, and the label reads `3/12`

#### Scenario: Caught up with the broadcast
- **WHEN** I open the detail page of a currently airing anime where I have watched every episode that has aired
- **THEN** the purple fill covers the full extent of the blue fill and neither extends past the aired portion of the track

#### Scenario: Not started
- **WHEN** I open the detail page of a currently airing anime with 5 of 12 episodes aired that I have not started
- **THEN** only the blue aired fill is drawn and no purple fill appears

#### Scenario: Finished airing keeps the plain bar
- **WHEN** I open the detail page of an anime that has finished airing
- **THEN** no aired fill is drawn and the bar shows only my watched progress against the total, as it does today

#### Scenario: Not yet aired keeps the plain bar
- **WHEN** I open the detail page of an anime that has not yet aired
- **THEN** no aired fill is drawn — there is no bar at all, per "Progress bar and overlay status editor"

#### Scenario: Airing with an unknown total
- **WHEN** I open the detail page of a currently airing anime with an unpublished total episode count and 10 episodes aired, of which I have watched 5
- **THEN** the blue fill spans half the track, the purple fill covers half of that blue extent, and the label reads `5/?`

#### Scenario: Airing with no aired count
- **WHEN** I open the detail page of a currently airing anime with no determinable aired-episode count
- **THEN** no blue fill is drawn, and my watched fill and label render as they do today

#### Scenario: Incrementing updates my fill only
- **WHEN** I increment an episode from the detail page of a currently airing anime
- **THEN** the purple fill grows in place without a reload and the blue aired fill is unchanged

### Requirement: Add-to-list action buttons
The detail page SHALL show up to three actions beneath the progress bar: an **Add to watching** button and an **Add to list** button (or **Edit**, once the anime is in my list) laid out side by side, with the **Refresh data** button below them spanning the full width. Exactly three actions SHALL be present at all times, except that **Add to watching** SHALL be omitted once the anime's status is Watching, Completed, or Dropped, leaving two.

**Add to watching** SHALL set the anime's list status to Watching, creating the entry if the anime is not in my list yet, and SHALL be shown whenever the anime is not in my list or its status is On hold or Plan to watch — that is, in every case where pressing it would actually change something.

**Add to list** SHALL add the anime as Plan to watch, and SHALL be shown only while the anime is not in my list. Once an entry exists, that button SHALL be replaced in place by the **Edit** button, which opens the entry editor overlay as it does today.

Both add actions SHALL go through the standard list-editing business rules (start/complete-date lifecycle, activity logging, debounced MAL sync) and SHALL update the page's displayed status and progress bar in place, without a full reload. While an add action is in flight, its button SHALL be disabled so a double-click cannot submit twice.

#### Scenario: Adding an anime not in my list to watching
- **WHEN** I open the detail page of an anime not in my list and click "Add to watching"
- **THEN** an entry is created with status Watching, the displayed status changes to Watching, the "Add to list" button is replaced by "Edit", and the change is logged and queued for sync

#### Scenario: Add-to-watching disappears once the anime is watching
- **WHEN** I click "Add to watching" on an anime not in my list
- **THEN** the button disappears once the status becomes Watching, leaving "Edit" and "Refresh data"

#### Scenario: Adding an anime not in my list as plan to watch
- **WHEN** I open the detail page of an anime not in my list and click "Add to list"
- **THEN** an entry is created with status Plan to watch, the displayed status changes to Plan to watch, and that button is replaced by "Edit"

#### Scenario: Add-to-list is replaced by edit once in my list
- **WHEN** I open the detail page of an anime already in my list with status Plan to watch
- **THEN** the buttons read "Add to watching", "Edit", and "Refresh data", and no "Add to list" button is shown

#### Scenario: Moving an existing entry to watching
- **WHEN** I click "Add to watching" on an anime already in my list with status On hold
- **THEN** the entry's status becomes Watching and the status change is recorded in the activity log

#### Scenario: Double-click while the add is in flight
- **WHEN** I click "Add to watching" twice in quick succession
- **THEN** the button is disabled after the first click and only one entry-update request is sent

#### Scenario: No "Add to watching" once watching, completed, or dropped
- **WHEN** I open the detail page of an anime whose status is Watching, Completed, or Dropped
- **THEN** no "Add to watching" button is shown, and only "Edit" and "Refresh data" appear

#### Scenario: Add actions sit side by side
- **WHEN** the detail page renders its action buttons
- **THEN** "Add to watching" and "Add to list" (or "Edit") are laid out side by side, with "Refresh data" beneath them

### Requirement: On-demand refresh action
The system SHALL provide an on-demand refresh action on the detail page for this anime's cached data. The action SHALL refresh both the anime's cached MyAnimeList metadata and its per-episode airing data, for that one anime only, at the moment it is requested.

A failure to refresh the airing data SHALL NOT fail the action when the metadata refresh succeeded; the action SHALL report success and the failure SHALL be logged.

#### Scenario: Refreshing this anime
- **WHEN** I trigger refresh on the detail page
- **THEN** the system performs a single-anime metadata refresh and a single-anime airing-data refresh, and updates the displayed cached data

#### Scenario: Corrected aired count appears immediately
- **WHEN** I trigger refresh on the detail page of an anime whose stored aired count was wrong and AniList now reports corrected airing data
- **THEN** the reloaded page shows the corrected aired count without waiting for a scheduled refresh

#### Scenario: Airing refresh fails
- **WHEN** I trigger refresh and the metadata refresh succeeds but the airing-data fetch fails
- **THEN** the action reports success, the metadata is updated, the previously stored airing rows are left intact, and the failure is logged

### Requirement: External MyAnimeList link from id
The detail page's info box SHALL show three external links — MyAnimeList, AniList, and SeriesGraph — in the grid cell previously occupied by the plain MyAnimeList link, rendered as a consistent set of labelled link buttons rather than bare inline text, each opening in a new tab.

The MyAnimeList link SHALL be built as a URL template from the MAL id, requiring no API call.

The AniList link SHALL point at `https://anilist.co/anime/<aniListId>` using the AniList media id already cached for that anime by the airing-data sync. When no AniList id is cached for the anime, the link SHALL instead point at an AniList title search for that anime, so the link is never absent and never broken.

The SeriesGraph link SHALL point at a SeriesGraph title search for that anime, since SeriesGraph indexes shows by an id the app does not hold.

Titles used to build search links SHALL be URL-encoded.

The three link buttons SHALL keep a fixed, content-sized shape regardless of how many lines the adjacent genres line wraps to: they SHALL NOT stretch to fill the height of the info-grid row they share with the genres field. When the genres line wraps onto two lines and grows taller than the buttons, the button row SHALL be vertically centered against that taller genres block rather than stretched to fill it or anchored to its top.

#### Scenario: Building the MyAnimeList link
- **WHEN** the detail page renders
- **THEN** it shows a MyAnimeList link constructed from the MAL id without making an API call

#### Scenario: AniList deep link from the cached id
- **WHEN** I open the detail page of an anime whose AniList media id has been cached by the airing sync
- **THEN** the AniList link points at that anime's AniList page using the cached id

#### Scenario: AniList link without a cached id
- **WHEN** I open the detail page of an anime for which no AniList id has been cached
- **THEN** the AniList link points at an AniList search for the anime's title rather than being hidden or pointing at a wrong anime

#### Scenario: SeriesGraph link
- **WHEN** the detail page renders
- **THEN** it shows a SeriesGraph link pointing at a SeriesGraph search for the anime's title

#### Scenario: Links open externally
- **WHEN** I click any of the three external links
- **THEN** it opens in a new tab and the detail page stays loaded

#### Scenario: Title with characters needing encoding
- **WHEN** the anime's title contains spaces, `&`, or `#`
- **THEN** the search links encode them so the destination resolves to a search for the full title

#### Scenario: Buttons stay a fixed size when genres wrap to two lines
- **WHEN** the genres line wraps onto two lines
- **THEN** the three link buttons remain the same size they are when genres fit on one line, and are vertically centered alongside the genres block rather than stretched to match its height

#### Scenario: Buttons are the same size whether genres wrap or not
- **WHEN** I compare the link buttons on an anime whose genres fit one line to one whose genres wrap to two lines
- **THEN** the buttons are exactly the same size in both cases

### Requirement: Series link in the relations row
The detail page's relations row SHALL include a **Series** link to the series page for the anime being viewed, whenever that anime has at least one stored relation of a story type (`sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version`), or the anime is already recorded as a member of a built series. The relation check is decided from data the page already loads, without an extra request; the membership check is a single indexed lookup on the anime's id, not a series build.

The membership check exists because a member reached only by a *reverse* edge from another anime — its own relations were never fetched, or are otherwise thin — has no story relation of its own for the relation check to find, even though it already belongs to a built series.

An anime whose only relations are non-story ones (e.g. `alternative_setting`, `character`, `other`) and that is not a recorded series member SHALL show no Series link. The link SHALL be present on main-line entries and side entries alike, so every anime that belongs to a series can reach it.

#### Scenario: Series link on a season
- **WHEN** I open the detail page of an anime that has a sequel relation
- **THEN** the relations row shows a "Series" link, and following it opens that anime's series page

#### Scenario: Series link on a special
- **WHEN** I open the detail page of a special linked to its parent story
- **THEN** the relations row shows a "Series" link to the same series its parent story belongs to

#### Scenario: Series link on a thin member reached only by a reverse edge
- **WHEN** I open the detail page of an anime whose own relations are empty but which is already recorded as a member of a built series
- **THEN** the relations row shows a "Series" link to that series

#### Scenario: No series link without story relations or recorded membership
- **WHEN** I open the detail page of a standalone anime whose only relations are `character` or `other`, and which is not a member of any built series
- **THEN** no "Series" link is shown

#### Scenario: Existing relation buttons are unaffected
- **WHEN** an anime has prequel, sequel, parent-story, and other relations
- **THEN** the Prequel, Sequel, Main series, and More controls behave exactly as before, with the Series link added alongside them

### Requirement: The detail page offers a picture choice for a my-list anime
The anime detail page SHALL carry a **Choose picture** control for an anime that is in my list and has more than one picture available, and SHALL NOT carry it otherwise.

The control SHALL sit in the page's action row, alongside the existing refresh action, rather than over the artwork itself — the artwork is rendered at its own proportions and an overlaid control would cover it.

The control SHALL open the picture picker the `artwork-selection` capability defines, over the anime's option set. Choosing SHALL update the page's own artwork without a reload.

The page SHALL render its artwork from the anime's displayed picture, so a chosen picture is what the detail page shows, and the existing portrait and landscape artwork rules SHALL apply to it unchanged — a chosen picture that is wider than it is tall SHALL be shown whole at landscape proportions exactly as a MAL main picture of the same shape would be.

#### Scenario: A my-list anime with several pictures
- **WHEN** I open the detail page of an anime in my list whose picture set holds four pictures
- **THEN** the action row shows a "Choose picture" control beside the refresh action

#### Scenario: An anime not in my list
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** no "Choose picture" control is shown

#### Scenario: A my-list anime with one picture
- **WHEN** I open the detail page of an anime in my list for which MAL publishes exactly one picture
- **THEN** no "Choose picture" control is shown

#### Scenario: Choosing updates the page
- **WHEN** I choose a picture from the picker
- **THEN** the page's artwork changes to it without a reload

#### Scenario: A landscape choice is shown whole
- **WHEN** I choose a picture that is wider than it is tall
- **THEN** it is shown at landscape proportions and is not cropped, exactly as a landscape main picture would be

### Requirement: The detail response carries the anime's pictures and asks for a backfill when needed
The detail read SHALL return, for the anime it describes: its displayed picture, MAL's own main picture, and its stored picture set.

When the anime is in my list and its picture set has never been fetched, the response SHALL carry a flag saying so, and the client SHALL make one follow-up request that fetches the set and returns it. The detail read itself SHALL NOT block on that fetch.

When a staleness-triggered full-detail fetch runs as part of the same read and the anime is in my list, the picture set SHALL arrive with it and the flag SHALL NOT be set.

The flag SHALL NOT be set for an anime that is not in my list, since no fetch is due for one.

#### Scenario: A pending backfill is flagged
- **WHEN** I open the detail page of a my-list anime whose full detail is fresh and whose picture set has never been fetched
- **THEN** the response is flagged as needing a picture fetch, and the client makes one follow-up request for it

#### Scenario: A fresh fetch carries the pictures
- **WHEN** opening the detail page triggers a full-detail fetch for a my-list anime
- **THEN** the picture set arrives with it and no follow-up request is made

#### Scenario: A non-list anime is never flagged
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** the response carries no picture-fetch flag


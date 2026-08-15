# anime-detail Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Single anime detail layout
The system SHALL show a single anime page with the title and a large picture on the left, and near the top-right of the title two separate side-by-side boxes: one showing rank and MAL score (MAL score respecting the hide/unhide toggle), and one showing my score and rewatch count. Below those it SHALL show an info box (type, status, source, duration, studio, aired-from/to, and genres) and, beneath it, a synopsis/background box. Any info field for which no data is available SHALL display "No info" rather than being blank.

The two score boxes SHALL be sized to the content they hold rather than stretched to fill the width of the main column: they SHALL NOT each take half the column's width, and their internal spacing SHALL be proportionate to the few short lines inside them rather than reusing the padding of a full-width panel such as the info or synopsis box. The pair SHALL read as a compact figure block beside the title, not as two large empty panels. They SHALL remain side by side at ordinary widths and SHALL keep stacking on narrow viewports, and the info and synopsis boxes below SHALL keep their existing full-width sizing.

Within those two boxes, the MAL score and my score SHALL each be presented as a plain coloured number on a labelled line, in the same `Label: value` form as the rank and popularity lines beside them — NOT as a tinted, bordered chip. No tint, border, or block SHALL be drawn around either score. The colour SHALL be carried by the number itself: blue for the MAL score, purple for my score, per the `score-presentation` capability's colour roles. The label SHALL stay in ordinary text rather than taking the score's colour, and the two score lines SHALL sit in the same line rhythm as the other lines in their box rather than as a raised figure among them. The boxes themselves SHALL remain, keeping their border and their existing content.

Removing the chip SHALL NOT change what the MAL score does: it SHALL still respect the hide/unhide toggle, still offer its per-score reveal control in the MAL colour, and still keep a hidden value out of the rendered output, exactly as it does elsewhere.

The info box's Status field SHALL, when the anime is currently airing and an aired-episode count is known, read `Currently airing: <aired>/<total> ep aired`, using `?` in place of an unknown total episode count. When the anime is currently airing but no aired-episode count is known, the field SHALL read `Currently airing` with no counts appended. Statuses other than currently airing SHALL be displayed unchanged.

The aired-episode count SHALL come from the anime's stored per-episode airing rows and SHALL NOT be estimated from its broadcast cadence or from elapsed time since its start date. An anime with no stored airing rows SHALL be treated as having no known aired count.

#### Scenario: Rendering the detail layout
- **WHEN** I open an anime's detail page
- **THEN** it shows the title with a large picture on the left, a "rank and MAL score" box and a separate "my score and rewatch count" box side by side, an info box (type, status, source, duration, studio, aired-from/to, genres), and a synopsis/background box

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
- **THEN** the info box and the synopsis box below the score boxes still span the full width of the main column

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

An anime with no stored future airing row SHALL show no countdown, whatever its airing status — including a currently-airing anime whose upcoming episodes have not been fetched. When the next air instant is less than an hour away, the countdown SHALL read `0d 0h` rather than being hidden.

The countdown SHALL be computed server-side against the request instant and delivered as a days/hours pair, matching the shape the currently-watching carousel already consumes.

#### Scenario: Currently airing with a stored future episode
- **WHEN** I open the detail page of a currently airing anime with 12 total episodes, 5 stored episodes in the past, and the next stored episode 2 days and 7 hours away
- **THEN** the Status field reads "Currently airing: 5/12 ep aired · next in 2d 7h"

#### Scenario: Not yet aired with a stored premiere
- **WHEN** I open the detail page of an anime that has not yet aired and whose stored first episode is 10 days away
- **THEN** the Status field reads "Not yet aired · next in 10d 0h"

#### Scenario: Currently airing with no stored future episode
- **WHEN** I open the detail page of a currently airing anime whose stored airing rows are all in the past
- **THEN** the Status field shows the aired counts with no countdown appended, and no countdown is estimated from its broadcast cadence

#### Scenario: Finished airing shows no countdown
- **WHEN** I open the detail page of an anime that has finished airing
- **THEN** the Status field reads "Finished airing" with no countdown appended

#### Scenario: Next episode less than an hour away
- **WHEN** the next stored air instant is 40 minutes from now
- **THEN** the countdown reads "next in 0d 0h" rather than being omitted

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

### Requirement: Prequel/sequel links when they exist
The system SHALL show prequel and sequel link buttons in the top-right corner of the detail page, only when such related anime exist for that anime, each linking to the related anime's detail page. When an anime has more than one prequel or more than one sequel, the first that MyAnimeList reports SHALL be used for the button; the remainder SHALL be reachable through the More overlay.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding link button(s) are shown and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel
- **THEN** no prequel/sequel buttons are shown

#### Scenario: Multiple prequels
- **WHEN** an anime has two prequels
- **THEN** the prequel button links to the first one MAL reports and the second is listed in the More overlay

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

MyAnimeList's `related_anime` data itself carries a media type per related node when requested via nested field selection (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request and store it directly — no separate fetch of the related anime is needed to learn its media type, and no backfill mechanism SHALL exist for this purpose. A relation row written before this was stored, or for which MAL reported no media type, MAY fall back to a lookup in our own cached metadata for that related anime; if neither source has it, the entry SHALL show "Unknown", matching how an unknown media type is labelled elsewhere in the app.

When the anime being viewed is itself a side entry — that is, MyAnimeList reports a `parent_story` relation for it — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported, no such button SHALL be shown.

Related-anime data (including each relation's media type) SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

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
- **WHEN** I open the detail page of an anime whose only related entries are a prequel and a sequel
- **THEN** no "More" button is shown

#### Scenario: Side entry links back to its main series
- **WHEN** I open the detail page of a movie that MAL reports as having a parent story
- **THEN** a "Main series" button is shown and navigates to that parent anime's detail page

#### Scenario: Main-series button absent without a parent story
- **WHEN** I open the detail page of an anime for which MAL reports no parent story
- **THEN** no "Main series" button is shown

#### Scenario: Lean refresh preserves relations
- **WHEN** an anime with stored related-anime entries is refreshed as part of a season or top-anime listing fetch
- **THEN** its stored related-anime entries (including their media types) are left intact

#### Scenario: Closing the overlay
- **WHEN** the More overlay is open and I press Escape or click outside it
- **THEN** the overlay closes and the detail page is unchanged

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
The system SHALL show, below the picture, a progress bar (`watched/total`, or `watched/?` when the total is unknown) with the current status next to it. The `watched` count SHALL be directly editable in place, per the "Inline editable episode count" requirement, so a specific episode number can be set without opening the overlay. The edit button — the second of the three action buttons stacked beneath the progress bar, shown only once the anime is in my list — SHALL open an overlay on top of the page for updating episodes watched, rewatch count, and score, applying the list-editing business rules. The editor SHALL NOT include start/finish date fields, since those are set automatically by the app's date logic.

#### Scenario: Opening the editor
- **WHEN** I click the edit button beneath the progress bar
- **THEN** an overlay opens with fields for episodes watched, rewatch count, and score, and no start/finish date fields

#### Scenario: Saving an edit
- **WHEN** I change episodes watched, rewatch count, or score in the overlay
- **THEN** the change is saved with the standard start/complete-date, activity-log, and debounced-sync behavior

#### Scenario: Editing the count in place
- **WHEN** I click the `watched` count next to the detail page's progress bar, type a number, and confirm
- **THEN** episodes-watched is saved to that number and the bar and count update in place, without the overlay opening

#### Scenario: In-place edit unavailable without an entry
- **WHEN** the anime is not in my list, so no entry exists yet
- **THEN** the count is not editable in place, and the add buttons rather than an edit button are what put it in my list

### Requirement: Broadcast progress on the detail page progress bar
While the anime is currently airing, the detail page's progress bar SHALL render broadcast progress — episodes aired out of the anime's total episode count — as a fill behind my watched fill, in the same blue used by the home page's airing-progress bar, so how much of the show exists yet is visible alongside how much of it I have seen. My watched progress SHALL keep the site's purple accent, layered on top within the same track.

The aired fill SHALL be drawn only while MAL reports the anime as currently airing. For any other airing status the bar SHALL render exactly as it does today, with no aired fill — a finished run's aired count and total are the same figure, and an unaired show has broadcast nothing.

When the total episode count is known, the aired fill SHALL span episodes aired out of that total. When the total is unknown and the aired count is known, the aired fill SHALL span exactly half the track as a fixed "progress so far, end unknown" marker, and my watched fill SHALL be measured within that extent against the aired count, so being caught up on everything aired covers the aired extent exactly and my fill can never exceed it. When neither count is known, no aired fill SHALL be drawn. Both fills SHALL be clamped so neither can exceed the track.

The bar's label SHALL remain `watched/total` (or `watched/?` when the total is unknown) and SHALL NOT restate the aired count, which the info box's Status field already names. The `watched` count SHALL remain editable in place and the increment button SHALL remain, exactly as specified by the "Progress bar and overlay status editor" requirement — the aired fill is an addition to that bar, not a replacement for it.

When my episodes watched changes on this page — by increment, by in-place edit, or from the entry editor — my fill SHALL update in place without a reload, and the aired fill SHALL be unaffected.

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
- **THEN** no aired fill is drawn

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


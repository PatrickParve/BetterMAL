## ADDED Requirements

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

### Requirement: Add-to-list action buttons
The detail page SHALL show up to three actions beneath the progress bar: an **Add to watching** button and an **Add to list** button (or **Edit**, once the anime is in my list) laid out side by side, with the **Refresh data** button below them spanning the full width. Exactly three actions SHALL be present at all times, except that **Add to watching** SHALL be omitted once the anime's status is Completed or Dropped, leaving two.

**Add to watching** SHALL set the anime's list status to Watching, creating the entry if the anime is not in my list yet, and SHALL remain available whatever the current status is, other than Completed or Dropped.

**Add to list** SHALL add the anime as Plan to watch, and SHALL be shown only while the anime is not in my list. Once an entry exists, that button SHALL be replaced in place by the **Edit** button, which opens the entry editor overlay as it does today.

Both add actions SHALL go through the standard list-editing business rules (start/complete-date lifecycle, activity logging, debounced MAL sync) and SHALL update the page's displayed status and progress bar in place, without a full reload. While an add action is in flight, its button SHALL be disabled so a double-click cannot submit twice.

#### Scenario: Adding an anime not in my list to watching
- **WHEN** I open the detail page of an anime not in my list and click "Add to watching"
- **THEN** an entry is created with status Watching, the displayed status changes to Watching, the "Add to list" button is replaced by "Edit", and the change is logged and queued for sync

#### Scenario: Adding an anime not in my list as plan to watch
- **WHEN** I open the detail page of an anime not in my list and click "Add to list"
- **THEN** an entry is created with status Plan to watch, the displayed status changes to Plan to watch, and that button is replaced by "Edit"

#### Scenario: Add-to-list is replaced by edit once in my list
- **WHEN** I open the detail page of an anime already in my list
- **THEN** the buttons read "Add to watching", "Edit", and "Refresh data", and no "Add to list" button is shown

#### Scenario: Moving an existing entry to watching
- **WHEN** I click "Add to watching" on an anime already in my list with status On hold
- **THEN** the entry's status becomes Watching and the status change is recorded in the activity log

#### Scenario: Double-click while the add is in flight
- **WHEN** I click "Add to watching" twice in quick succession
- **THEN** the button is disabled after the first click and only one entry-update request is sent

#### Scenario: No "Add to watching" once completed or dropped
- **WHEN** I open the detail page of an anime whose status is Completed or Dropped
- **THEN** no "Add to watching" button is shown, and only "Edit" and "Refresh data" appear

#### Scenario: Add actions sit side by side
- **WHEN** the detail page renders its action buttons
- **THEN** "Add to watching" and "Add to list" (or "Edit") are laid out side by side, with "Refresh data" beneath them

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title, sourced from our own cached metadata since MAL's related-anime data does not itself carry a media type. The overlay SHALL open immediately with whatever media types are already cached, and opening it SHALL also trigger a capped, best-effort backfill (one paced MAL call per not-yet-cached related anime, up to a fixed limit) that updates the overlay's entries in place as it completes; a related anime that stays uncached (beyond the cap, or on fetch failure) shows "Unknown", matching how an unknown media type is labelled elsewhere in the app. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

When the anime being viewed is itself a side entry — that is, MyAnimeList reports a `parent_story` relation for it — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported, no such button SHALL be shown.

Related-anime data SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

#### Scenario: Overlay rows show media type when cached
- **WHEN** the More overlay lists a related anime that we have already cached as a movie
- **THEN** that row shows "Movie" beneath its title

#### Scenario: Overlay row for an uncached related anime resolves after backfill
- **WHEN** the More overlay lists a related anime we have not separately cached
- **THEN** that row shows "Unknown" beneath its title until the on-open backfill fetches and caches it, at which point the row updates in place to show its real media type

#### Scenario: Uncached related anime beyond the backfill cap
- **WHEN** an anime has more uncached related entries than the backfill's per-open limit
- **THEN** the entries beyond that limit keep showing "Unknown" for this open

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
- **THEN** its stored related-anime entries are left intact

#### Scenario: Closing the overlay
- **WHEN** the More overlay is open and I press Escape or click outside it
- **THEN** the overlay closes and the detail page is unchanged

### Requirement: Single-day aired range
When an anime's aired-from and aired-to dates fall on the same day — a movie or special released in a single day — the info box's Aired field SHALL show that one date rather than a from–to range. When the dates differ, or either is unknown, the existing from–to (or from–"No info") rendering applies unchanged.

#### Scenario: Movie aired in a single day
- **WHEN** I open the detail page of a movie whose aired-from and aired-to dates are both August 6, 2022
- **THEN** the Aired field reads "Aug 6, 2022" rather than "Aug 6, 2022 – Aug 6, 2022"

#### Scenario: Multi-day range is unaffected
- **WHEN** an anime's aired-from and aired-to dates differ
- **THEN** the Aired field shows the existing from–to range

### Requirement: Per-episode duration label
Since the stored average-episode-duration is always a per-episode figure, the info box's Duration field SHALL append `/ep` to the minutes value whenever the anime has more than one episode or its episode count is not yet known. When the anime is a single-episode release (`TotalEpisodes == 1`), the field SHALL show the plain minutes with no suffix.

#### Scenario: Series duration is labelled per episode
- **WHEN** I open the detail page of a 24-episode series whose average episode duration is 24 minutes
- **THEN** the Duration field reads "24 min/ep"

#### Scenario: Movie duration has no suffix
- **WHEN** I open the detail page of a one-episode movie whose duration is 115 minutes
- **THEN** the Duration field reads "115 min"

#### Scenario: Ongoing anime with an unknown episode count
- **WHEN** I open the detail page of a currently airing anime whose total episode count is not yet known
- **THEN** the Duration field appends "/ep", since an unknown count is treated as more than one episode

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

## MODIFIED Requirements

### Requirement: External MyAnimeList link from id
The detail page's info box SHALL show three external links — MyAnimeList, AniList, and SeriesGraph — in the grid cell previously occupied by the plain MyAnimeList link, rendered as a consistent set of labelled link buttons rather than bare inline text, each opening in a new tab.

The MyAnimeList link SHALL be built as a URL template from the MAL id, requiring no API call.

The AniList link SHALL point at `https://anilist.co/anime/<aniListId>` using the AniList media id already cached for that anime by the airing-data sync. When no AniList id is cached for the anime, the link SHALL instead point at an AniList title search for that anime, so the link is never absent and never broken.

The SeriesGraph link SHALL point at a SeriesGraph title search for that anime, since SeriesGraph indexes shows by an id the app does not hold.

Titles used to build search links SHALL be URL-encoded.

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

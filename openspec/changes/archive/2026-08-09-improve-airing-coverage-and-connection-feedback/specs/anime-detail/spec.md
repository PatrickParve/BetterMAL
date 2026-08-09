## MODIFIED Requirements

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

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title, sourced from our own cached metadata since MAL's related-anime data does not itself carry a media type.

The capped, best-effort media-type backfill (one paced MAL call per not-yet-cached related anime, up to a fixed limit) SHALL be triggered when the detail page loads, not when the overlay opens, so that media types are already resolved by the time the overlay is opened. The backfill SHALL be triggered only when at least one of the anime's related entries has no cached media type, so an anime whose relations are all cached costs no extra request on later visits. The backfill SHALL NOT block or delay the detail page's render, and pressing **More** SHALL open the overlay immediately whether or not a backfill is still in flight.

The overlay SHALL show a loading indication while a backfill is in flight, and SHALL update its entries in place as the backfill completes. A related anime that stays uncached (beyond the cap, or on fetch failure) shows "Unknown", matching how an unknown media type is labelled elsewhere in the app. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

When the anime being viewed is itself a side entry — that is, MyAnimeList reports a `parent_story` relation for it — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported, no such button SHALL be shown.

Related-anime data SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

#### Scenario: Backfill starts on page load
- **WHEN** I open the detail page of an anime with related entries whose media types are not cached
- **THEN** the media-type backfill starts immediately without my opening the overlay, and the page renders and stays interactive while it runs

#### Scenario: Media types are already resolved when the overlay opens
- **WHEN** I open the detail page of such an anime, wait for the backfill to finish, and then press "More"
- **THEN** the overlay opens with the backfilled media types already shown, with no loading indication

#### Scenario: Opening the overlay mid-backfill
- **WHEN** I press "More" while the load-time backfill is still in flight
- **THEN** the overlay opens immediately with whatever media types are already known, shows a loading indication, and updates its entries in place as the backfill completes

#### Scenario: No backfill when every media type is cached
- **WHEN** I open the detail page of an anime whose related entries all have cached media types
- **THEN** no backfill request is made, and the overlay shows every media type when opened

#### Scenario: Uncached related anime beyond the backfill cap
- **WHEN** an anime has more uncached related entries than the backfill's per-visit limit
- **THEN** the entries beyond that limit keep showing "Unknown" for this visit, and a later visit backfills the next batch

#### Scenario: Navigating away mid-backfill
- **WHEN** I leave the detail page while its backfill is still in flight
- **THEN** the page unmounts cleanly with no error, and whatever the backfill has already cached stays cached

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

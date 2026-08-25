## ADDED Requirements

### Requirement: An anime's picture set is stored, not just its main picture
The system SHALL store, per anime, the full set of picture URLs MyAnimeList publishes for it, alongside MAL's own main picture and the picture the app displays.

Three distinct values SHALL be kept per anime:

- **MAL's main picture** — whatever MAL reports as `main_picture`, always overwritten by any MAL sync.
- **The displayed picture** — the picture every surface in the app renders for that anime. It equals MAL's main picture until a picture is chosen, and equals the chosen picture thereafter.
- **The picture set** — every URL MAL returns under `pictures` for that anime, in MAL's order.

The system SHALL also record **when the picture set was last fetched**, as a value distinct from "when full detail was last fetched". An anime whose picture set has never been fetched SHALL be distinguishable from one that has been fetched and has exactly one picture, so the latter is never re-fetched on every visit.

An anime SHALL be treated as having a chosen picture exactly when its displayed picture differs from MAL's main picture. There SHALL NOT be a separate stored flag that can disagree with those two values.

#### Scenario: Storing a picture set
- **WHEN** the system fetches an anime whose MAL record lists six pictures
- **THEN** all six URLs are stored in MAL's order, and the fetch timestamp is recorded

#### Scenario: One picture is not the same as none
- **WHEN** an anime's picture set has been fetched and MAL publishes exactly one picture for it
- **THEN** the anime is recorded as fetched, and it is not fetched again until its next full-detail refresh

#### Scenario: Nothing is chosen by default
- **WHEN** an anime's picture set has been fetched and no picture has been chosen
- **THEN** its displayed picture equals MAL's main picture

### Requirement: Picture sets are fetched only for anime on my list
The system SHALL fetch an anime's picture set only when that anime is in my list. An anime reached by browsing — a season listing, the Year or Top pages, a search result, a related-anime link, or a series member not in my list — SHALL NOT have its picture set fetched, and SHALL keep MAL's main picture as its displayed picture.

Eligibility SHALL be evaluated at fetch time from whether a list entry exists, so adding an anime to my list makes it eligible immediately and removing it makes it ineligible, with no separate bookkeeping.

The system SHALL NOT sweep my whole list fetching picture sets. Fetching SHALL happen only through the paths the next requirement defines.

#### Scenario: A browsed anime is not fetched for pictures
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** no picture-set fetch is made for it, and it renders with MAL's main picture

#### Scenario: Adding to my list makes an anime eligible
- **WHEN** I add an anime to my list from its detail page
- **THEN** the anime becomes eligible, and its picture set is fetched on the next read of that page

#### Scenario: No bulk fetch
- **WHEN** this capability is first deployed against a list of several hundred anime
- **THEN** no background job fetches picture sets for the whole list

### Requirement: Picture sets ride existing fetches, and are backfilled on visit
The system SHALL request MAL's `pictures` field as part of a full-detail fetch **whenever the anime being fetched is in my list**, so that the initial import, the scheduled tiered my-list refresh, a manual refresh, and a detail page's own staleness-triggered fetch all populate the picture set at no additional MAL request. For an anime not in my list the field SHALL be omitted from the request.

Because a my-list anime already inside its staleness tier will not be fetched again for some time, the system SHALL additionally support a **visit-triggered picture backfill**: when a page needs an anime's picture set and that anime is in my list and has never had one fetched, the system SHALL fetch it in a single request and return the result. Concurrent backfills of the same anime SHALL collapse into one fetch, as every other visit-triggered fetch in the app does.

A backfill that fails SHALL leave the anime marked as never fetched, so the next visit retries it.

A picture set SHALL NOT carry a staleness tier of its own. It is refreshed whenever its anime's full detail is refreshed, and no more often.

#### Scenario: A tiered refresh carries the pictures
- **WHEN** the nightly my-list refresh fetches full detail for an anime
- **THEN** the request includes `pictures`, the returned set is stored, and no second request is made for it

#### Scenario: A non-list anime's fetch omits the field
- **WHEN** the detail page of an anime that is not in my list triggers a full-detail fetch
- **THEN** the request does not include `pictures` and no picture set is stored

#### Scenario: A fresh row is backfilled on visit
- **WHEN** I open the detail page of a my-list anime whose full detail is inside its staleness tier and whose picture set has never been fetched
- **THEN** the page renders from cache and one picture-set fetch is made for it

#### Scenario: Concurrent visits fetch once
- **WHEN** two requests for the same anime's picture backfill arrive at once
- **THEN** exactly one MAL request is made and both are served from it

#### Scenario: A failed backfill retries later
- **WHEN** a picture backfill fails
- **THEN** the anime is still recorded as never fetched, and the next visit attempts the fetch again

### Requirement: A chosen picture is shown everywhere that anime is shown
When a picture has been chosen for an anime, every surface in the app that renders that anime SHALL render the chosen picture: the home dashboard, my list, the Season, Year, Top, Airing, Search and Recap pages, the profile, the series page and its timeline, the series browser, and related-anime tiles.

A related-anime tile SHALL prefer the far end's stored displayed picture when the app has a record for that anime, falling back to the picture MAL supplied with the relation only when it has none.

Choosing a picture SHALL NOT be pushed to MyAnimeList, and SHALL NOT alter the anime's list entry in any way.

#### Scenario: One choice, every page
- **WHEN** I choose a picture for an anime and then visit the home page, my list, its season, and the series it belongs to
- **THEN** every one of those pages shows the chosen picture

#### Scenario: Related tiles follow the choice
- **WHEN** an anime with a chosen picture appears as another anime's related entry
- **THEN** the related tile shows the chosen picture

#### Scenario: Nothing reaches MyAnimeList
- **WHEN** I choose a picture
- **THEN** no request is made to MyAnimeList as a result

### Requirement: A chosen picture survives every MAL sync
No MyAnimeList sync of any kind SHALL overwrite or clear a chosen picture. This SHALL hold for a full-detail fetch, a lean listing refresh from the Season, Year, Top or Search paths, the weekly reconciliation, and the corrective re-sync alike.

Each of those paths SHALL still record MAL's own main picture, so the choice can be cleared back to it at any time.

An anime with **no** chosen picture SHALL continue to follow MAL: when MAL changes its main picture, that anime's displayed picture SHALL change with it.

#### Scenario: A full refresh does not revert a choice
- **WHEN** an anime with a chosen picture is refreshed from MAL
- **THEN** its displayed picture is still the chosen one, and MAL's own main picture is updated behind it

#### Scenario: A season listing does not revert a choice
- **WHEN** an anime with a chosen picture appears in a season listing refresh
- **THEN** its displayed picture is unchanged

#### Scenario: An unchosen anime follows MAL
- **WHEN** MAL changes the main picture of an anime for which nothing has been chosen
- **THEN** that anime's displayed picture becomes the new MAL picture

### Requirement: Choosing MAL's own picture clears the choice
The picker SHALL present MAL's main picture among the options. Choosing it SHALL clear the override rather than storing a redundant one, so "reset to default" and "pick the default" are the same act and produce the same state.

The system SHALL additionally accept an explicit reset that clears the choice without naming a picture.

#### Scenario: Picking the default clears the override
- **WHEN** an anime has a chosen picture and I pick the picture MAL calls its main picture
- **THEN** the anime is no longer recorded as having a chosen picture, and it follows MAL again from then on

#### Scenario: Explicit reset
- **WHEN** I reset an anime's picture
- **THEN** its displayed picture becomes MAL's main picture

### Requirement: Only pictures MAL publishes may be chosen
The system SHALL accept as a choice only a URL that is in the anime's own option set — its stored picture set together with MAL's main picture. Any other value SHALL be rejected.

A choice SHALL be rejected for an anime that is not in my list.

The system SHALL NOT accept an uploaded image, a pasted URL from elsewhere, or a picture belonging to a different anime.

#### Scenario: An arbitrary URL is refused
- **WHEN** a request tries to set an anime's picture to a URL not in its option set
- **THEN** the request is rejected and the stored picture is unchanged

#### Scenario: A non-list anime cannot be chosen for
- **WHEN** a request tries to set a picture for an anime that is not in my list
- **THEN** the request is rejected

### Requirement: A stored choice is never re-validated away
A chosen picture SHALL be kept even when a later fetch no longer lists it, and SHALL NOT be silently reverted to MAL's main picture.

The picker SHALL always include the current choice among its options, marked as the current one, so a choice that has left MAL's set is visible and replaceable rather than merely broken.

#### Scenario: MAL drops the chosen picture
- **WHEN** an anime's picture set is refreshed and no longer contains the chosen picture
- **THEN** the chosen picture is still displayed, and the picker shows it as the current selection alongside the new options

### Requirement: The picture picker
The system SHALL offer a picture picker as an overlay showing every option as an image, with the current selection marked. Clicking an option SHALL set it and close the overlay.

The overlay SHALL behave as the app's other overlays do — dismissable with Escape and by clicking outside it, and locking the page behind it from scrolling.

The control that opens the picker SHALL be rendered **only when there is more than one option**, so a picker never opens onto a single image and an anime with several pictures never lacks the control.

#### Scenario: Opening and choosing
- **WHEN** I open the picker and click a picture
- **THEN** that picture becomes the anime's picture and the overlay closes

#### Scenario: The current selection is marked
- **WHEN** I open the picker for an anime that has a chosen picture
- **THEN** that picture is marked as the current selection

#### Scenario: One option, no control
- **WHEN** an anime's option set holds exactly one picture
- **THEN** no control to open the picker is rendered

#### Scenario: Dismissing without choosing
- **WHEN** I press Escape or click outside the overlay
- **THEN** it closes and the anime's picture is unchanged

### Requirement: A series' picture is chosen from its main line's artwork
A series SHALL have its own chosen picture, independent of any member's. Its option set SHALL be the union, deduplicated by URL, of:

- every main-line member's displayed picture and MAL main picture, which exist for every member whether or not it is in my list; and
- every main-line member's stored picture set, which exists only for members in my list.

Options SHALL be ordered by main-line watch order, and within a member by MAL's own order. Extras SHALL NOT contribute options.

When a series has no chosen picture, it SHALL show its root member's **displayed** picture — so choosing a picture for the root anime also changes the series' picture, until the series is given one of its own, which then wins.

Setting, clearing, validating, and surviving MAL syncs SHALL work for a series picture exactly as the requirements above define for an anime picture.

#### Scenario: The pool spans the main line
- **WHEN** a series has four main-line members, three of them in my list with five, four, and two pictures respectively, and one not in my list
- **THEN** the picker offers those eleven pictures plus the fourth member's main picture, deduplicated, in main-line order

#### Scenario: Extras contribute nothing
- **WHEN** a series' extras carry pictures of their own
- **THEN** none of them appear in the series picker

#### Scenario: A series with no choice follows its root
- **WHEN** a series has no chosen picture and I choose a new picture for its root anime
- **THEN** the series shows that picture too

#### Scenario: A series choice outranks the root's
- **WHEN** a series has a chosen picture and its root anime also has one
- **THEN** the series shows its own chosen picture and the root anime shows its own

### Requirement: The series picture pool is filled in with a bounded budget
When a series page is opened, the system SHALL fetch picture sets for main-line members that are in my list and have never had one fetched, up to a fixed maximum of **8 members per visit**, so that opening a large franchise can never trigger an unbounded run of MAL requests.

The response SHALL report how many eligible members remain unfetched, so the page can say that the picker is not yet complete. Each subsequent visit SHALL continue the work until none remain.

The page SHALL render from cached data first; the backfill SHALL NOT block the page's own read.

#### Scenario: A large franchise is bounded
- **WHEN** I open a series with thirty main-line members in my list, none of whose picture sets have been fetched
- **THEN** at most eight are fetched on that visit and the response reports the remainder

#### Scenario: Revisits finish the job
- **WHEN** I return to that series page repeatedly
- **THEN** each visit fetches up to eight more until no eligible member is left unfetched

#### Scenario: Nothing to do
- **WHEN** every eligible main-line member already has a picture set
- **THEN** no MAL request is made on the visit

#### Scenario: The page does not wait
- **WHEN** the backfill is running
- **THEN** the series page has already rendered from cache

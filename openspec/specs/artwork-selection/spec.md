# artwork-selection Specification

## Purpose
The artwork-selection capability stores MAL's whole picture set for a my-list anime, not just its main picture, and lets me choose which picture represents an anime or a series' main line — a choice shown everywhere that anime or series appears, surviving every MAL sync, and timestamped when I make it. It covers only which picture is picked and stored; how a picked picture is drawn is artwork-presentation's, resolving a series' title alongside its picture is series-identity's, and carrying a choice to another device is device-transfer's.

## Requirements

### Requirement: Every presentation choice carries the time it was made
The system SHALL store, beside each of the three presentation choices — an anime's chosen picture, a series' chosen title, and a series' chosen picture — the time that choice last changed.

The time SHALL be recorded when a choice is set and when it is cleared alike. A clearing is a change, and it SHALL be possible to recognise it as the later of two conflicting versions of the same choice.

The time SHALL be the moment the change was made, taken from the act that makes it. Nothing that later reads, copies, exports, or rebuilds a choice SHALL restamp it: a choice made three weeks ago SHALL still report a three-week-old time. No MyAnimeList sync of any kind SHALL write one of these times.

A recorded time SHALL NOT be the marker of whether a choice exists. A time stored beside no chosen value SHALL mean a choice that was cleared at that time; no time at all SHALL mean that no choice has ever been made or cleared for that anime or series.

Setting a choice to the value it already holds SHALL record the time of that write, since it is a write.

These times exist so that the same choice made on two devices can be ordered against each other. No surface of the app SHALL be required to display them.

#### Scenario: Setting records the time
- **WHEN** I choose a picture for an anime
- **THEN** the anime's chosen picture and the time I chose it are both stored

#### Scenario: Clearing records the time
- **WHEN** I clear a series' chosen picture
- **THEN** no chosen picture is stored for it and the time I cleared it is recorded

#### Scenario: A choice keeps its own age
- **WHEN** a picture chosen three weeks ago is read, rendered, or carried through a series rebuild today
- **THEN** its recorded time is still the three-week-old one

#### Scenario: A MAL sync does not stamp anything
- **WHEN** an anime with a chosen picture is refreshed from MAL
- **THEN** its chosen picture's recorded time is unchanged

#### Scenario: Never chosen has no time
- **WHEN** an anime has never had a picture chosen or cleared
- **THEN** no time is recorded for its picture choice

#### Scenario: Choosing the same picture again
- **WHEN** I choose the picture an anime already has chosen
- **THEN** the recorded time becomes the time of that write

### Requirement: An anime's picture set is stored, not just its main picture
The system SHALL store, per anime, the full set of picture URLs MyAnimeList publishes for it, alongside MAL's own main picture, the picture chosen for it, and the picture the app displays.

Four distinct values SHALL be kept per anime:

- **MAL's main picture** — whatever MAL reports as `main_picture`, always overwritten by any MAL sync.
- **The chosen picture** — the picture I have chosen for that anime. Its default SHALL be nothing at all, which SHALL mean "no choice; follow MAL".
- **The displayed picture** — the picture every surface in the app renders for that anime. It SHALL be derived from the two values above: the chosen picture where there is one, MAL's main picture otherwise. Every write of either value it derives from SHALL re-derive it in the same write, so the three can never disagree.
- **The picture set** — every URL MAL returns under `pictures` for that anime, in MAL's order.

The system SHALL also record **when the picture set was last fetched**, as a value distinct from "when full detail was last fetched". An anime whose picture set has never been fetched SHALL be distinguishable from one that has been fetched and has exactly one picture, so the latter is never re-fetched on every visit.

An anime SHALL be treated as having a chosen picture exactly when a chosen picture is stored for it. Chosen-ness SHALL NOT be inferred by comparing the displayed picture with MAL's main picture: those two hold the same URL both when MAL's own picture was chosen deliberately and when nothing was chosen at all, and those are different states. Nor is such a comparison meaningful beyond the one database that made both writes, since another device may have cached MAL's picture at another time.

#### Scenario: Storing a picture set
- **WHEN** the system fetches an anime whose MAL record lists six pictures
- **THEN** all six URLs are stored in MAL's order, and the fetch timestamp is recorded

#### Scenario: One picture is not the same as none
- **WHEN** an anime's picture set has been fetched and MAL publishes exactly one picture for it
- **THEN** the anime is recorded as fetched, and it is not fetched again until its next full-detail refresh

#### Scenario: Nothing is chosen by default
- **WHEN** an anime's picture set has been fetched and no picture has been chosen
- **THEN** no chosen picture is stored for it and its displayed picture equals MAL's main picture

#### Scenario: A chosen picture that is MAL's own
- **WHEN** I choose the picture MAL calls an anime's main picture
- **THEN** that URL is stored as the chosen picture and the anime is recorded as having one, even though its displayed picture equals MAL's

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
- **WHEN** the scheduled my-list refresh fetches full detail for an anime
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
No MyAnimeList sync of any kind SHALL overwrite or clear a chosen picture. This SHALL hold for a full-detail fetch, a lean listing refresh from the Season, Year, Top or Search paths, and the weekly reconciliation alike.

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

### Requirement: Picking MAL's own picture pins it, and clearing is an act of its own
The picker SHALL present MAL's main picture among the options. Choosing it SHALL store it as the chosen picture exactly as choosing any other option does, so that anime stops following MAL's main picture from then on: a later MAL change of main picture SHALL NOT move that anime's displayed picture.

Clearing SHALL be a separate act from choosing, and the only way to remove a choice. It SHALL remove the chosen picture so that the anime follows MAL's main picture again, and SHALL record the time.

A choice SHALL NOT be re-validated away by this rule any more than by any other: a chosen picture that happens to be MAL's own current main picture SHALL remain stored as a choice until it is cleared.

The same SHALL hold for a series' chosen picture, whose default is its root member's MAL picture: choosing the picture the series already shows by default SHALL store it as a choice, and only clearing SHALL return the series to following its root's MAL picture — including when the root anime itself carries its own chosen picture, which the series default SHALL NOT follow.

#### Scenario: Picking the default stores it
- **WHEN** I pick the picture MAL calls an anime's main picture
- **THEN** that URL is stored as the anime's chosen picture, and the anime is recorded as having a chosen picture

#### Scenario: A pinned picture survives a MAL picture change
- **WHEN** MAL changes the main picture of an anime whose chosen picture is the main picture MAL published before
- **THEN** the anime still displays the picture I chose, and MAL's new main picture is recorded behind it

#### Scenario: Clearing returns the anime to MAL
- **WHEN** I clear an anime's chosen picture
- **THEN** no chosen picture is stored for it, its displayed picture becomes MAL's main picture, and it follows MAL from then on

#### Scenario: A series choice that matches its root
- **WHEN** a series shows its root member's MAL picture by default and I pick that same picture in the series picker
- **THEN** it is stored as the series' own chosen picture, and choosing a different picture for the root anime no longer changes the series' picture

#### Scenario: A series default ignores the root anime's own choice
- **WHEN** a series has no chosen picture of its own, and its root anime has a chosen picture that differs from MAL's main picture for that anime
- **THEN** the series displays the root anime's MAL picture, not the root anime's chosen picture

### Requirement: Only pictures MAL or TMDB offers for that anime may be chosen
The system SHALL accept as a choice only a URL in the anime's own option set. That set is:
- its stored MAL picture set, together with MAL's main picture
- the TMDB images of the sets the anime draws from (`tmdb-artwork`), as cached on this device, each in its built URL form
- its current choice

Any other value SHALL be rejected.

A choice SHALL be rejected for an anime that is not in my list.

The system SHALL NOT accept any of the following:
- an uploaded image
- a URL pasted from elsewhere
- a picture belonging to a different anime, including a TMDB image from a set the anime does not draw from

A chosen TMDB image SHALL be stored as its full URL, as a chosen MAL picture is, because the chosen picture is what every surface renders.

#### Scenario: An arbitrary URL is refused
- **WHEN** a request tries to set an anime's picture to a URL not in its option set
- **THEN** the request is rejected and the stored picture is unchanged

#### Scenario: A non-list anime cannot be chosen for
- **WHEN** a request tries to set a picture for an anime that is not in my list
- **THEN** the request is rejected

#### Scenario: A TMDB image of the anime's own set is accepted
- **WHEN** I choose a TMDB poster from the Season scope of an anime in my list
- **THEN** its full URL is stored as the anime's chosen picture, and the anime displays it everywhere

#### Scenario: Another show's TMDB image is refused
- **WHEN** a request tries to set an anime's picture to a TMDB image from a set that anime does not draw from
- **THEN** the request is rejected and the stored picture is unchanged

### Requirement: A stored choice is never re-validated away
A chosen picture SHALL be kept even when a later fetch no longer lists it, and SHALL NOT be silently reverted to MAL's main picture.

The picker SHALL always include the current choice among its options, marked as the current one, so a choice that has left MAL's set is visible and replaceable rather than merely broken.

#### Scenario: MAL drops the chosen picture
- **WHEN** an anime's picture set is refreshed and no longer contains the chosen picture
- **THEN** the chosen picture is still displayed, and the picker shows it as the current selection alongside the new options

### Requirement: The picture picker
The system SHALL offer a picture picker as an overlay that shows every option as an image, with the current selection marked. Clicking an option SHALL set it and close the overlay.

The picker SHALL show its options in labelled sections by source, **MyAnimeList** first and then **TMDB**:
- On an anime's picker, the TMDB options SHALL be divided by scope, as `tmdb-artwork` defines scopes: **Series**, then **Season** (naming TMDB's season number), then **Movie**. Each scope SHALL be divided by language: **No language**, then **Japanese**, then **English**.
- On a series' picker, the TMDB options SHALL be divided by language only, in the same order. The series, season and movie images of the whole franchise SHALL be mixed within each language group.

Within a TMDB group, posters SHALL come before backdrops, each in the order TMDB lists them.

A section, scope or language group with no options SHALL NOT be shown. An anime or series with no TMDB images therefore shows the MyAnimeList section alone.

Every division SHALL carry a visible heading, so that no option's source, scope or language is ambiguous. No option SHALL appear in the picker more than once.

Each group of options SHALL open and close under its own heading, and the heading SHALL name the group and count its options. The groups are the MyAnimeList group and each TMDB language group. On an anime's picker, the Series, Season and Movie headings SHALL NOT open or close themselves; the language groups under them do.
- A closed group SHALL NOT download any of its images.
- Opening a group SHALL download that group's images only.
- Opening or closing a group SHALL make no request to the backend or to the TMDB API. The only requests SHALL be the downloads of that group's images.

When the picker opens, these groups SHALL be open and every other group closed:
- the MyAnimeList group
- the group holding the current selection

Which groups are open SHALL be kept for as long as I stay on the page, so reopening the picker shows what I last had open. A fresh visit to the page SHALL start from the defaults above again.

The current selection SHALL be marked wherever it appears. When the current selection appears in no section, because its source no longer lists it, it SHALL be shown in a group of its own ahead of the sections, and that group SHALL be open.

While a TMDB fetch for the picker's options is still pending, the picker SHALL note that more pictures may appear. The picker SHALL note that TMDB has no match when TMDB access is configured and no MAL id involved has a TMDB mapping. For an anime, that is its own MAL id. For a series, it is the MAL ids of its members.

Every option SHALL be shown **whole, at its own proportions**. No part of any option SHALL be cropped away, and no option SHALL be letterboxed inside a box of a different shape. A portrait picture SHALL be drawn portrait. A landscape picture SHALL be drawn landscape, and correspondingly wider than a portrait option beside it. A square picture SHALL be drawn square. Two options that differ only near their edges SHALL therefore be distinguishable in the picker, which is the whole point of showing them side by side.

Options SHALL be laid out to a **common height** across every section, each option's width following from its own proportions at that height. A set mixing orientations SHALL therefore still form tidy rows rather than a ragged field. That height SHALL be smaller than the picture that the page behind the overlay displays for the same anime or series. A large option set then shows more of itself at once, and a picker of a franchise's whole pool is not mostly below the fold.

An option whose proportions are so wide that it cannot fit the width available to a row SHALL be reduced to fit whole within that width, rather than being cropped or forced to overflow the overlay.

The whole of an option, its full drawn area whatever its shape, SHALL be the control that chooses it. The current selection's marking SHALL be legible on options of every shape.

The overlay SHALL behave as the app's other overlays do:
- it can be dismissed with Escape and by clicking outside it
- it locks the page behind it from scrolling
- it closes when the page it was opened over is navigated away from

The control that opens the picker SHALL be rendered **only when there is more than one option**, counting MyAnimeList and TMDB options together. A picker then never opens onto a single image, and an anime with several pictures never lacks the control.

The picker SHALL offer a control that **clears the choice**, rendered only when a choice is stored, so that the control's presence is itself the sign that one is. Its wording SHALL name the default that clearing returns to, which differs by surface: MAL's own main picture for an anime, and the root member's MAL picture for a series. Using it SHALL clear the choice and close the overlay, just as choosing an option sets it and closes.

These presentation rules SHALL hold for the anime picker and the series picker alike, except where the division of TMDB options above differs between them.

#### Scenario: Opening and choosing
- **WHEN** I open the picker and click a picture
- **THEN** that picture becomes the anime's picture and the overlay closes

#### Scenario: The current selection is marked
- **WHEN** I open the picker for an anime that has a chosen picture
- **THEN** that picture is marked as the current selection

#### Scenario: One option, no control
- **WHEN** an anime's option set holds exactly one picture, counting MyAnimeList and TMDB together
- **THEN** no control to open the picker is rendered

#### Scenario: Dismissing without choosing
- **WHEN** I press Escape or click outside the overlay
- **THEN** it closes and the anime's picture is unchanged

#### Scenario: Clearing from the picker
- **WHEN** I open the picker for an anime with a chosen picture and use the control that clears it
- **THEN** the choice is removed, the overlay closes, and the anime displays MAL's main picture again

#### Scenario: No choice, no clear control
- **WHEN** I open the picker for an anime that has no chosen picture
- **THEN** no clear control is rendered

#### Scenario: The series picker names its own default
- **WHEN** I open the picker for a series with a chosen picture
- **THEN** its clear control names the root anime's MAL picture as what clearing returns to, and using it makes the series follow its root member's MAL picture again

#### Scenario: MyAnimeList and TMDB are shown apart
- **WHEN** I open the picker for an anime with both MAL pictures and cached TMDB images
- **THEN** the MAL pictures sit under a MyAnimeList heading, and every TMDB image sits under a TMDB heading below it

#### Scenario: An anime's TMDB options by scope, then language
- **WHEN** I open the picker for a season whose Series set holds Japanese and no-language images and whose Season set holds English posters
- **THEN** the TMDB part shows a Series scope with a No language group then a Japanese group, followed by a Season scope naming its season number with an English group

#### Scenario: A series' TMDB options by language only
- **WHEN** I open the series picker for a franchise with cached series, season and movie images in Japanese
- **THEN** its TMDB part shows one Japanese group holding all of them, with no Series, Season or Movie sub-division

#### Scenario: Empty groups are not shown
- **WHEN** an anime's TMDB sets hold no English images
- **THEN** no English heading is shown for it

#### Scenario: Closed groups download nothing
- **WHEN** I open the picker for an anime with no chosen picture, whose TMDB sets hold 40 images across three language groups
- **THEN** the MyAnimeList group is open, each TMDB group is closed under a heading showing how many images it holds, and none of the TMDB images is downloaded

#### Scenario: Opening one group downloads only that group
- **WHEN** I open the Japanese group under TMDB · Series
- **THEN** its images are downloaded and shown, no other closed group's images are downloaded, and no request is made to the backend or the TMDB API

#### Scenario: The MyAnimeList group can be closed
- **WHEN** I close the MyAnimeList group
- **THEN** its images are hidden, and the TMDB groups stay as they were

#### Scenario: The current picture's group starts open
- **WHEN** I open the picker for an anime whose current picture is a TMDB English poster from its Season scope
- **THEN** that Season scope's English group is open, with the picture marked, while its other TMDB groups are closed

#### Scenario: Open groups are kept while I stay on the page
- **WHEN** I open the Japanese group, close the picker without choosing, and reopen the picker
- **THEN** the Japanese group is still open

#### Scenario: A fresh visit starts from the defaults
- **WHEN** I leave the page and later open it again
- **THEN** the picker opens with only the MyAnimeList group and the current picture's group open

#### Scenario: A choice no longer listed stays visible
- **WHEN** an anime's chosen TMDB image no longer appears in its refreshed TMDB set
- **THEN** the picker shows it, marked as the current selection, in a group of its own ahead of the sections

#### Scenario: A pending TMDB fetch is noted
- **WHEN** I open the picker while the anime's TMDB fetch is still running
- **THEN** the picker notes that more pictures may appear

#### Scenario: No TMDB match is noted
- **WHEN** TMDB access is configured and I open the picker for an anime in my list with no TMDB mapping
- **THEN** the picker shows its MyAnimeList section and notes that TMDB has no match for it

#### Scenario: A landscape option is shown landscape
- **WHEN** I open the picker on an option set containing a picture wider than it is tall
- **THEN** that option is drawn as a wide, short image at its own proportions — wider than the portrait options beside it — rather than as a centre-cropped portrait slice of it

#### Scenario: Nothing is cut off
- **WHEN** I open the picker on an option set containing two pictures that differ only near their left and right edges
- **THEN** both are shown whole and the difference between them is visible in the picker

#### Scenario: Mixed orientations line up
- **WHEN** an option set holds both portrait and landscape pictures
- **THEN** every option is drawn to the same height, differing only in width, and the options sit in even rows

#### Scenario: Options are smaller than the picture on the page
- **WHEN** I open the picker from a page showing that anime's or series' picture
- **THEN** each option is drawn shorter than that picture, so more options fit in the overlay at once

#### Scenario: An extremely wide option still fits
- **WHEN** an option is so wide that at the common height it would be wider than the overlay's row
- **THEN** it is reduced to fit whole within that width, and the overlay does not scroll sideways

#### Scenario: The series picker looks the same
- **WHEN** I open the series picture picker on a pool drawn from several main-line members
- **THEN** its options follow the same rules — shown whole, at their own proportions, to a common height

### Requirement: A series' picture is chosen from its main line's artwork and its franchise's TMDB images
A series SHALL have its own chosen picture, independent of any member's. Its option set SHALL be the union, deduplicated by URL, of:
- every main-line member's displayed picture and MAL main picture, which exist for every member whether or not it is in my list
- every main-line member's stored picture set, which exists only for members in my list
- the TMDB images of the sets the series draws from (`tmdb-artwork`), as cached on this device

The MyAnimeList options SHALL be ordered by main-line watch order, and within a member by MAL's own order. Extras SHALL NOT contribute MyAnimeList options. The TMDB options follow the franchise rule `tmdb-artwork` defines, under which a movie anywhere in the series contributes, an extra included.

When a series has no chosen picture, it SHALL show its root member's **MAL main picture**, not the root's displayed picture, so a picture chosen for the root anime SHALL NOT change the series' default. Explicitly choosing that same MAL picture as the series' own picture SHALL be stored as a choice of the series', which is distinct from merely following the default.

Setting, clearing, validating, and surviving MAL syncs SHALL work for a series picture exactly as the requirements above define for an anime picture.

#### Scenario: The pool spans the main line
- **WHEN** a series has four main-line members, three of them in my list with five, four, and two pictures respectively, and one not in my list
- **THEN** the picker's MyAnimeList section offers those eleven pictures plus the fourth member's main picture, deduplicated, in main-line order

#### Scenario: Extras contribute no MyAnimeList pictures
- **WHEN** a series' extras carry MAL pictures of their own
- **THEN** none of those MAL pictures appear in the series picker

#### Scenario: The pool includes the franchise's TMDB images
- **WHEN** a series' main line maps to a TMDB show whose series and season sets are cached, and a movie among its extras maps to a cached TMDB movie set
- **THEN** the series picker offers those TMDB images alongside its MyAnimeList options, and any of them can be chosen as the series' picture

#### Scenario: A series with no choice follows its root's MAL picture
- **WHEN** a series has no chosen picture
- **THEN** it shows its root member's MAL main picture, unaffected by any chosen picture stored on the root anime

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

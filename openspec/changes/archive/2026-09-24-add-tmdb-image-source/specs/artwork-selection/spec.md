## RENAMED Requirements

- FROM: `### Requirement: Only pictures MAL publishes may be chosen`
- TO: `### Requirement: Only pictures MAL or TMDB offers for that anime may be chosen`

- FROM: `### Requirement: A series' picture is chosen from its main line's artwork`
- TO: `### Requirement: A series' picture is chosen from its main line's artwork and its franchise's TMDB images`

## MODIFIED Requirements

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

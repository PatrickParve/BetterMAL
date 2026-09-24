## MODIFIED Requirements

### Requirement: On-demand refresh action
The system SHALL provide an on-demand refresh action on the detail page for this anime's cached data. The action SHALL refresh the anime's cached MyAnimeList metadata and its per-episode airing data, for that one anime only, at the moment it is requested.

The action SHALL also refetch the TMDB sets the anime draws from (`tmdb-artwork`), whatever their age, when all three of these hold:
- the anime is in my list
- the anime has a TMDB mapping
- TMDB access is configured

When the metadata refresh succeeds, a failure to refresh the airing data or the TMDB sets SHALL NOT fail the action. The action SHALL report success, SHALL leave the previously stored airing rows and TMDB images intact, and SHALL log the failure.

#### Scenario: Refreshing this anime
- **WHEN** I trigger refresh on the detail page
- **THEN** the system performs a single-anime metadata refresh and a single-anime airing-data refresh, and updates the displayed cached data

#### Scenario: Corrected aired count appears immediately
- **WHEN** I trigger refresh on the detail page of an anime whose stored aired count was wrong and AniList now reports corrected airing data
- **THEN** the reloaded page shows the corrected aired count without waiting for a scheduled refresh

#### Scenario: Airing refresh fails
- **WHEN** I trigger refresh and the metadata refresh succeeds but the airing-data fetch fails
- **THEN** the action reports success, the metadata is updated, the previously stored airing rows are left intact, and the failure is logged

#### Scenario: TMDB images are refetched
- **WHEN** I trigger refresh on the detail page of a mapped anime in my list whose TMDB sets were fetched last week
- **THEN** those sets are fetched again, and the reloaded picker offers whatever TMDB now lists

#### Scenario: TMDB fails during a refresh
- **WHEN** I trigger refresh, the metadata refresh succeeds, and TMDB does not answer
- **THEN** the action reports success, the anime's cached TMDB images are left intact, and the failure is logged

### Requirement: External MyAnimeList link from id
The detail page's info box SHALL show its external links in the grid cell previously occupied by the plain MyAnimeList link: MyAnimeList, AniList, SeriesGraph, and IMDb when known. They SHALL be rendered as a consistent set of labelled link buttons rather than as bare inline text, and each SHALL open in a new tab.

The MyAnimeList link SHALL be built as a URL template from the MAL id, requiring no API call.

The AniList link SHALL point at `https://anilist.co/anime/<aniListId>`, using the AniList media id already cached for that anime by the airing-data sync. When no AniList id is cached for the anime, the link SHALL instead point at an AniList title search for that anime, so the link is never absent and never broken.

The SeriesGraph link SHALL point at a SeriesGraph title search for that anime, since SeriesGraph indexes shows by an id the app does not hold.

The IMDb link SHALL point at `https://www.imdb.com/title/<imdbId>/`, one for each IMDb id the anime's mapping holds (`external-id-mapping`). It SHALL be built from the id with no API call.
- A single id SHALL be labelled **IMDb**.
- Several ids SHALL each get a link, labelled **IMDb 1**, **IMDb 2** and so on, in the mapping's order.
- When the mapping holds no IMDb id, no IMDb link SHALL be shown. Unlike the AniList link, it has no search fallback.

Titles used to build search links SHALL be URL-encoded.

The link buttons SHALL keep a fixed, content-sized shape however many lines the adjacent genres line wraps to. They SHALL NOT stretch to fill the height of the info-grid row they share with the genres field. When the genres line wraps onto two lines and grows taller than the buttons, the button row SHALL be vertically centered against that taller genres block, rather than stretched to fill it or anchored to its top.

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

#### Scenario: IMDb link from the mapping
- **WHEN** I open the detail page of an anime whose mapping holds the IMDb id `tt2560140`
- **THEN** it shows an IMDb link pointing at `https://www.imdb.com/title/tt2560140/`, built without an API call

#### Scenario: No IMDb id, no IMDb link
- **WHEN** I open the detail page of an anime whose mapping holds no IMDb id, or that has no mapping at all
- **THEN** no IMDb link is shown, and the other external links are unaffected

#### Scenario: Several IMDb ids
- **WHEN** an anime's mapping holds two IMDb ids
- **THEN** two IMDb links are shown, labelled "IMDb 1" and "IMDb 2" in the mapping's order

#### Scenario: Links open externally
- **WHEN** I click any of the external links
- **THEN** it opens in a new tab and the detail page stays loaded

#### Scenario: Title with characters needing encoding
- **WHEN** the anime's title contains spaces, `&`, or `#`
- **THEN** the search links encode them so the destination resolves to a search for the full title

#### Scenario: Buttons stay a fixed size when genres wrap to two lines
- **WHEN** the genres line wraps onto two lines
- **THEN** the link buttons remain the same size they are when genres fit on one line, and are vertically centered alongside the genres block rather than stretched to match its height

#### Scenario: Buttons are the same size whether genres wrap or not
- **WHEN** I compare the link buttons on an anime whose genres fit one line to one whose genres wrap to two lines
- **THEN** the buttons are exactly the same size in both cases

### Requirement: The detail page offers a picture choice for a my-list anime
The anime detail page SHALL carry a **Choose picture** control when the anime is in my list and has more than one picture available, counting its MyAnimeList and TMDB options together. It SHALL NOT carry the control otherwise.

When a TMDB follow-up fetch brings the count above one, the control SHALL appear without a reload.

The control SHALL sit in the page's action row, alongside the existing refresh action, rather than over the artwork itself. The artwork is rendered at its own proportions, and an overlaid control would cover it.

The control SHALL open the picture picker the `artwork-selection` capability defines, over the anime's option set. Choosing SHALL update the page's own artwork without a reload.

The page SHALL render its artwork from the anime's displayed picture, so a chosen picture is what the detail page shows. The existing portrait and landscape artwork rules SHALL apply to it unchanged, whether it came from MyAnimeList or from TMDB. A chosen picture that is wider than it is tall, such as a TMDB backdrop, SHALL be shown whole at landscape proportions, exactly as a MAL main picture of the same shape would be.

#### Scenario: A my-list anime with several pictures
- **WHEN** I open the detail page of an anime in my list whose picture set holds four pictures
- **THEN** the action row shows a "Choose picture" control beside the refresh action

#### Scenario: An anime not in my list
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** no "Choose picture" control is shown

#### Scenario: A my-list anime with one picture
- **WHEN** I open the detail page of an anime in my list for which MAL publishes exactly one picture and no TMDB image is cached
- **THEN** no "Choose picture" control is shown

#### Scenario: One MAL picture plus TMDB images
- **WHEN** I open the detail page of an anime in my list for which MAL publishes exactly one picture and its TMDB sets hold images
- **THEN** the action row shows a "Choose picture" control

#### Scenario: TMDB images arriving after the page renders
- **WHEN** an anime in my list with one MAL picture renders without the control, and its TMDB follow-up fetch then returns images
- **THEN** the "Choose picture" control appears without a reload

#### Scenario: Choosing updates the page
- **WHEN** I choose a picture from the picker
- **THEN** the page's artwork changes to it without a reload

#### Scenario: A landscape choice is shown whole
- **WHEN** I choose a picture that is wider than it is tall
- **THEN** it is shown at landscape proportions and is not cropped, exactly as a landscape main picture would be

## ADDED Requirements

### Requirement: The detail response carries the anime's TMDB pictures and asks for a TMDB fetch when one is due
For an anime in my list, the detail read SHALL return:
- the cached TMDB images of the sets the anime draws from (`tmdb-artwork`), grouped by scope and language as the picker shows them, each with its width and height
- whether the anime has a TMDB mapping at all

When all three of the following hold, the response SHALL carry a flag saying a TMDB fetch is due:
- TMDB access is configured
- the anime is in my list
- at least one of its sets is due, meaning never fetched or more than 30 days old

On that flag, the client SHALL make one follow-up request. It fetches the due sets and returns the anime's TMDB images. The page SHALL merge the result into what it shows without a reload. The detail read itself SHALL NOT call TMDB.

The response SHALL carry the anime's mapped IMDb ids whether or not the anime is in my list.

For an anime not in my list, the response SHALL carry no TMDB images and no TMDB flag.

#### Scenario: A due set is flagged and fetched after render
- **WHEN** I open the detail page of a mapped anime in my list whose Season set has never been fetched
- **THEN** the page renders from cache with the flag set, one follow-up request fetches the set, and its images join the picker without a reload

#### Scenario: Fresh sets are not flagged
- **WHEN** I open the detail page of a mapped anime in my list whose sets were all fetched within 30 days
- **THEN** the response carries its cached TMDB images and no flag, and no follow-up request is made

#### Scenario: Not in my list
- **WHEN** I open the detail page of a mapped anime that is not in my list
- **THEN** the response carries its IMDb ids but no TMDB images and no flag

#### Scenario: No key configured
- **WHEN** no TMDB API key is configured and I open the detail page of a mapped anime in my list
- **THEN** the response carries no flag, and no follow-up request is made

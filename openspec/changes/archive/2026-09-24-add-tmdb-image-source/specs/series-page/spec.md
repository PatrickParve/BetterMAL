## MODIFIED Requirements

### Requirement: Series external links
The series page SHALL offer links out to MyAnimeList, AniList, SeriesGraph, and IMDb when known, matching the links the anime detail page offers for a single anime.

Every link SHALL target the series root, the first entry in watch order, since that is the entry under which each site indexes the franchise.

The AniList link SHALL use the root's AniList id when one is stored. Otherwise it SHALL fall back to an AniList title search for the root's display title. The SeriesGraph link SHALL use a title search for the root's display title.

The IMDb link SHALL be built from the IMDb ids in the root's mapping (`external-id-mapping`), exactly as the detail page builds its own: one link per id, labelled as the detail page labels them. When the root's mapping holds no IMDb id, no IMDb link SHALL be shown, even if another member has one.

#### Scenario: Links target the first entry
- **WHEN** I open a series whose first entry in watch order is its first season
- **THEN** the MyAnimeList link opens that first season's MAL page, not the page of whichever member I arrived from

#### Scenario: AniList link without a stored id
- **WHEN** the series root has no stored AniList id
- **THEN** the AniList link opens an AniList search for the root's title rather than a broken link

#### Scenario: The IMDb link targets the root
- **WHEN** I arrive at a series from its third season, and the root's mapping holds the IMDb id `tt2560140`
- **THEN** the IMDb link opens `https://www.imdb.com/title/tt2560140/`

#### Scenario: A root without an IMDb id
- **WHEN** the series root's mapping holds no IMDb id
- **THEN** no IMDb link is shown, and the MyAnimeList, AniList and SeriesGraph links are unaffected

### Requirement: The series page carries picture and title controls beside Rebuild
The series page SHALL carry a **Choose picture** control and a **Choose title** control for the series it is showing.

Both controls SHALL sit in the **Series stats** heading row, alongside the **Rebuild** control, and SHALL be presented as that row's controls are rather than as links. They SHALL NOT sit in the page's header block among the external links. A control that changes what the series is SHALL be grouped with the other such control, not with links that navigate away. The header block SHALL therefore hold only the picture, title, status, year span and external links. Nothing SHALL be left in its place where the two controls were.

**Rebuild** SHALL keep its position at the end of that group, next to the partial- and truncated-series notices that qualify it, so those notices still read as belonging to it.

**Choose picture** SHALL be rendered only when the series has more than one picture to choose between, counting its MyAnimeList and TMDB options together. It SHALL open the series picture picker the `artwork-selection` capability defines. A series with a single picture available SHALL show no control at all, rather than a control that opens onto one image. The control's absence SHALL NOT disturb the position of the remaining controls in the row. When a TMDB follow-up fetch brings the count above one, the control SHALL appear without a reload.

**Choose title** SHALL always be rendered, since a title can always be trimmed even when only one is offered. It SHALL open a picker that lists every title the `series-identity` capability offers, together with a text field for a trimmed title. It SHALL refuse to submit a title that capability's rule rejects.

Both controls SHALL affect the series only. Neither SHALL change any member anime's own picture or title, and neither SHALL cause anything to be written to MyAnimeList.

Choosing a picture or a title SHALL take effect on the page without a reload, and SHALL be reflected on every other surface that shows this series the next time that surface is read.

#### Scenario: A multi-picture series offers the control
- **WHEN** I open a series whose main-line members and TMDB sets between them offer six distinct pictures
- **THEN** the Series stats row shows a "Choose picture" control, and clicking it opens a picker of those six pictures

#### Scenario: A single-picture series shows no picture control
- **WHEN** I open a series whose main-line members offer exactly one distinct picture between them and no TMDB image is cached for it
- **THEN** no "Choose picture" control is shown, and "Choose title" and "Rebuild" still sit together in the Series stats row

#### Scenario: The title control is always available
- **WHEN** I open any series
- **THEN** the Series stats row shows a "Choose title" control

#### Scenario: The controls sit with Rebuild, not with the links
- **WHEN** I open any series
- **THEN** "Choose picture" and "Choose title" appear in the Series stats heading row next to "Rebuild", and the page header block below the title shows only the external links: MyAnimeList, AniList, SeriesGraph, and IMDb when known

#### Scenario: Rebuild keeps its notices
- **WHEN** I open a series that could not be built in full
- **THEN** the "Some entries couldn't be loaded yet" notice still reads alongside the Rebuild control in that same row

#### Scenario: A chosen picture applies immediately
- **WHEN** I pick a picture from the series picker
- **THEN** the header's picture changes to it without a page reload

#### Scenario: A chosen TMDB backdrop in the header
- **WHEN** I pick a TMDB backdrop from the series picker
- **THEN** the header shows it whole at landscape proportions, as it would a landscape MAL picture

#### Scenario: Choosing does not touch the members
- **WHEN** I choose a picture for a series
- **THEN** no main-line member's own displayed picture changes, and nothing is pushed to MyAnimeList

## ADDED Requirements

### Requirement: The series read carries the franchise's TMDB pictures and fetches due sets afterwards
The series read SHALL return the cached TMDB images of the sets the series draws from (`tmdb-artwork`). They SHALL be grouped by language as the series picker shows them, each image with its width and height. The read SHALL also return how many of those sets are due, and whether any member has a TMDB mapping at all.

When TMDB access is configured and at least one set is due, the client SHALL make one follow-up request per visit. That request fetches due sets within the series page's budget, and returns the series' TMDB images and how many sets remain due. The page SHALL merge the result without a reload.

The follow-up SHALL NOT delay the page, which has already rendered from cache. It SHALL NOT disturb the page's MyAnimeList picture backfill: each follow-up updates only what it fetched.

#### Scenario: A first visit fetches after rendering
- **WHEN** I open a series none of whose TMDB sets have been fetched
- **THEN** the page renders from cache, one follow-up request fetches up to 20 sets, and their images join the series picker without a reload

#### Scenario: A remainder carries over to the next visit
- **WHEN** the follow-up reports sets still due
- **THEN** no further request is made on this visit, and the next visit's follow-up continues with them

#### Scenario: Both backfills run on one visit
- **WHEN** a series has both MyAnimeList picture sets and TMDB sets still to fetch
- **THEN** both follow-ups run, and neither one's result overwrites the other's

#### Scenario: Nothing due
- **WHEN** every TMDB set of the series was fetched within the last 30 days
- **THEN** the read carries the cached images, reports nothing due, and no follow-up request is made

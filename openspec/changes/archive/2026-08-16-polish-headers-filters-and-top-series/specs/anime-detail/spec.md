## MODIFIED Requirements

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

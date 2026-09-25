# tmdb-artwork Specification

## Purpose
The tmdb-artwork capability fetches, caches and refreshes TMDB posters and backdrops so that they can be offered as pictures beside MyAnimeList's. It covers the optional API key (without one the app makes no TMDB request and works as before), the three endpoints and three languages that are fetched and the images that are discarded, the three caches keyed by TMDB id — series, season and movie — that every anime mapping to the same id shares, which sets an anime and which sets a series draw from, fetching a set in a follow-up request when a page needs it and refreshing it every 30 days, fetching an anime's sets only while it is in my list, the 20-request budget of a series-page visit, the guarantee that a TMDB failure or rate limit never breaks a page, the deletion of any set last fetched more than 150 days ago to stay within TMDB's caching terms, and TMDB's required logo and notice. Which of these images may be chosen is artwork-selection's, how a chosen picture is drawn is artwork-presentation's, and the mapping that names each anime's TMDB ids is external-id-mapping's.

## Requirements

### Requirement: TMDB access is configured by an API key and is optional
The system SHALL call TMDB's v3 API with an API key read from configuration. The key SHALL never be written into source code.

When no key is configured, the system SHALL:
- make no TMDB request of any kind
- report no TMDB fetch as pending
- keep every other part of the app working exactly as it does with a key, including the id mapping (`external-id-mapping`) and IMDb links

Images cached while a key was configured SHALL still be offered after the key is removed, for as long as the cache keeps them ("No cached TMDB set is kept for more than five months").

#### Scenario: No key, no requests
- **WHEN** the application runs with no TMDB API key configured and I open the detail page of a mapped anime in my list
- **THEN** no TMDB request is made, no TMDB fetch is reported as pending, and the rest of the page is unaffected

#### Scenario: Cached images outlive the key
- **WHEN** images were cached for an anime while a key was configured, and the key has since been removed
- **THEN** the anime's picker still offers those images

### Requirement: Only posters and backdrops in Japanese, English or no language are fetched
The system SHALL fetch TMDB images only from these endpoints:
- `GET /tv/{id}/images` for a TMDB TV id: posters and backdrops
- `GET /tv/{id}/season/{n}/images` for a TMDB TV id and season: posters, the only kind this endpoint returns
- `GET /movie/{id}/images` for a TMDB movie id: posters and backdrops

Every such request SHALL carry `include_image_language=ja,en,null`, with exactly those three values.

The system SHALL discard the following without storing or showing them:
- the `logos` array that every response carries
- any image whose language is not Japanese, English or none

Episode stills SHALL NOT be fetched. `GET /configuration` SHALL NOT be called.

#### Scenario: A series-level request
- **WHEN** the images of TMDB TV id 1429 are fetched
- **THEN** one request is made to `/tv/1429/images` carrying `include_image_language=ja,en,null`, and its posters and backdrops are stored

#### Scenario: Logos are discarded
- **WHEN** a response carries posters, backdrops and three logos
- **THEN** no logo is stored or offered anywhere

#### Scenario: A season is posters only
- **WHEN** the images of season 2 of TV id 85937 are fetched
- **THEN** one request is made to `/tv/85937/season/2/images`, and only posters are stored

#### Scenario: A stray language is dropped
- **WHEN** a response includes an image tagged with a language other than Japanese, English or none
- **THEN** that image is not stored

### Requirement: Image sets are cached by TMDB id and shared by every anime that maps to them
The system SHALL cache fetched images in three separate stores, each keyed by TMDB identity and never by MyAnimeList id:
- **series images**, keyed by TMDB TV id
- **season images**, keyed by TMDB TV id and season number
- **movie images**, keyed by TMDB movie id

Every MyAnimeList entry that maps to the same TMDB key SHALL read the same cached set. A set SHALL be fetched once for all of them, never once per entry.

For each image the store SHALL hold:
- its TMDB file path
- its language: Japanese, English, or none
- whether it is a poster or a backdrop
- its width and height

The store SHALL also keep the order in which TMDB listed the images.

The full image URL SHALL NOT be stored in the cache. It SHALL be built when read, as `https://image.tmdb.org/t/p/original` followed by the file path (which begins with `/`).

Each set SHALL record when it was fetched. A fetch that returns no qualifying image SHALL store the set as fetched and empty, which is distinct from never fetched.

#### Scenario: Seasons share one series set
- **WHEN** I open the detail pages of two seasons that both map to TV id 1429
- **THEN** TV id 1429's series images are fetched once, and both pages offer the same set

#### Scenario: The URL is built on read
- **WHEN** a cached image has the file path `/abc123.jpg`
- **THEN** it is offered as `https://image.tmdb.org/t/p/original/abc123.jpg`, while the cache holds only `/abc123.jpg`

#### Scenario: Fetched-and-empty is not unfetched
- **WHEN** TMDB returns no posters or backdrops in Japanese, English or no language for a movie
- **THEN** the movie's set is recorded as fetched and empty, and it is not requested again until it is due

### Requirement: Which TMDB sets an anime draws from
An anime's TMDB images SHALL come from the sets its mapping names, in three scopes:
- **Series:** the set of its TMDB TV id, when it has one.
- **Season:** the set for its TV id and TMDB season number, when that number is 1 or more. Season 0 is TMDB's "Specials" bucket, and its posters describe the bucket rather than the anime, so it SHALL NOT be used.
- **Movie:** the set of each of its TMDB movie ids, taken together as one Movie scope.

An anime has no TMDB images when either of these holds:
- it has no mapping
- its mapping holds only IMDb ids

#### Scenario: A season of a show
- **WHEN** an anime maps to TV id 1429 and TMDB season 3
- **THEN** it draws from TV id 1429's series set and from the set for season 3 of TV id 1429

#### Scenario: A whole show with no season number
- **WHEN** an anime maps to a TV id with no TMDB season number
- **THEN** it draws from that TV id's series set only

#### Scenario: A special filed under season 0
- **WHEN** an anime maps to a TV id and TMDB season 0
- **THEN** it draws from that TV id's series set only, and season 0's set is never fetched for it

#### Scenario: A movie
- **WHEN** an anime maps to movie id 635302
- **THEN** it draws from that movie's set as its Movie scope

#### Scenario: A movie split in two
- **WHEN** an anime maps to two movie ids
- **THEN** its Movie scope offers both sets' images together

#### Scenario: No mapping
- **WHEN** an anime has no mapping, or its mapping holds only IMDb ids
- **THEN** it has no TMDB images and no TMDB request is ever made for it

### Requirement: Which TMDB sets a series draws from
A series' TMDB images SHALL come from its stored members, both its main line and its extras. The related entries shown beside them are not members and SHALL NOT count. The series draws from:
- **Series:** the set of every TMDB TV id held by a main-line member.
- **Season:** the set of every season numbered 1 or more of those same TV ids, wherever any member maps to one.
- **Movie:** the set of every TMDB movie id held by any member.

When the main line spans several TMDB TV ids, the series SHALL draw from every one of them. A TV id held only by an extra, such as a spin-off that TMDB lists as a separate show, SHALL NOT contribute its series or season images.

#### Scenario: One show, several seasons and a film
- **WHEN** a series' main line maps to TV id 85937 seasons 1 through 5, and one of its members maps to movie id 635302
- **THEN** the series draws from TV id 85937's series set, the season sets for seasons 1 through 5, and movie 635302's set

#### Scenario: A main line spanning two shows
- **WHEN** a series' main line holds one member mapped to TV id 46260 and another mapped to TV id 31910
- **THEN** the series draws from both TV ids' series sets

#### Scenario: A spin-off extra
- **WHEN** an extra of a series maps to a TV id no main-line member holds
- **THEN** that TV id contributes nothing to the series

#### Scenario: A movie among the extras
- **WHEN** a movie that is an extra of a series maps to a TMDB movie id
- **THEN** that movie's set contributes to the series

#### Scenario: A related entry is not a member
- **WHEN** an anime is shown in a series' More section only as a related entry, and maps to a TMDB movie id
- **THEN** that movie contributes nothing to the series

### Requirement: Sets are fetched when a page needs them, then refreshed every 30 days
The system SHALL fetch a TMDB set only because a page needs it, at two points:
- the first time a page that offers the set's images is opened while the set has never been fetched
- whenever the set's last fetch is more than 30 days old

The system SHALL NOT sweep the mapping or my list in the background, fetching sets ahead of need.

The fetch SHALL run in a follow-up request, after the page has rendered from what is cached. The page's own read SHALL NOT wait on TMDB.

Concurrent fetches of the same set SHALL collapse into a single TMDB request.

A refetch SHALL replace the set's images wholesale.

A fetch that fails SHALL leave the set as it was, whether never fetched or still holding its previous images, so a later visit retries it. A TMDB id that TMDB reports as not found SHALL be recorded as fetched and empty.

#### Scenario: A set's first need
- **WHEN** I open a page that offers a set's images and the set has never been fetched
- **THEN** the page renders from cache, and a follow-up request fetches the set

#### Scenario: A fresh set is not fetched again
- **WHEN** I open a page whose sets were all fetched within the last 30 days
- **THEN** no TMDB request is made

#### Scenario: A stale set is refreshed
- **WHEN** I open a page offering a set last fetched 31 days ago
- **THEN** the follow-up request fetches the set again and replaces its images

#### Scenario: A failure keeps what was there
- **WHEN** refetching a set fails
- **THEN** its previously cached images are still offered, and a later visit tries again

#### Scenario: An id TMDB no longer has
- **WHEN** TMDB answers "not found" for a mapped TV id
- **THEN** that set is recorded as fetched and empty, and it is not requested again for 30 days

#### Scenario: Two pages, one request
- **WHEN** the detail pages of two seasons sharing a TV id request that TV id's set at the same moment
- **THEN** exactly one TMDB request is made for it

### Requirement: No cached TMDB set is kept for more than five months
TMDB's terms of use forbid caching what it returns for more than six months. The system SHALL therefore delete a cached set, together with its images, once the set's last fetch is more than 150 days old. This SHALL apply to all three stores, to a set stored as fetched and empty as much as to one holding images, and whether or not a TMDB API key is configured.

The deletion SHALL run on every hourly airing tick, including the one at start-up, in a scope of its own, and SHALL make no TMDB request. A failure of it SHALL be logged and SHALL NOT stop the id-mapping sync or the airing work, and the next tick SHALL try again.

A deleted set SHALL count as never fetched afterwards: a page that needs it SHALL fetch it as it would any new set, when a key is configured.

The deletion SHALL NOT change a picture I have chosen, which is stored on the anime or series as the address I chose (`artwork-selection`), and SHALL NOT change the id mapping. Every page SHALL keep showing the chosen picture, and the picker SHALL keep offering it as the current picture.

#### Scenario: An old set is deleted with its images
- **WHEN** a season's set was last fetched 151 days ago and the hourly tick runs
- **THEN** the set and its images are deleted

#### Scenario: A younger set is kept
- **WHEN** a set was last fetched 149 days ago, or 31 days ago and is due for a refresh
- **THEN** the tick keeps it and its images

#### Scenario: A set fetched and empty expires too
- **WHEN** a set recorded as fetched and empty was last fetched 151 days ago
- **THEN** it is deleted like any other

#### Scenario: A chosen picture outlives its set
- **WHEN** I chose a TMDB picture for Oshi no Ko and the set it came from is deleted for its age
- **THEN** My List, the profile, the home page and every other page still show that picture, and its picker offers it as the current picture, which I can keep or choose again

#### Scenario: A deleted set is fetched again when needed
- **WHEN** a set was deleted, a key is configured, and I open a page that offers its images
- **THEN** the page renders without it and a follow-up request fetches it again

#### Scenario: No key, still deleted
- **WHEN** no TMDB API key is configured and a cached set is older than 150 days
- **THEN** it is deleted, and no TMDB request is made

#### Scenario: A failing deletion does not stop the tick
- **WHEN** the deletion fails during a tick
- **THEN** the failure is logged, and the id-mapping sync and the airing work still run

### Requirement: An anime's TMDB sets are fetched only while it is in my list
The system SHALL fetch an anime's TMDB sets only while that anime is in my list, because only an anime in my list has a picture to choose (`artwork-selection`). Opening the detail page of an anime not in my list SHALL trigger no TMDB request.

#### Scenario: A browsed anime is not fetched
- **WHEN** I open the detail page of a mapped anime that is not in my list
- **THEN** no TMDB request is made for it

#### Scenario: Adding to my list makes it eligible
- **WHEN** I add a mapped anime to my list from its detail page
- **THEN** its TMDB sets are fetched after the next read of that page

### Requirement: A series page fetches its TMDB sets within a bounded budget
When a series page is opened, the system SHALL fetch the series' due TMDB sets, meaning sets never fetched or more than 30 days old. It SHALL make at most **20 TMDB requests per visit**. It SHALL report how many due sets remain, and each later visit SHALL continue until none remain.

Whether members are in my list SHALL NOT limit which sets are fetched, because a series' picture can be chosen for any series.

#### Scenario: A large franchise is bounded
- **WHEN** I open a series with 45 due TMDB sets
- **THEN** at most 20 are fetched on that visit, and 25 are reported as remaining

#### Scenario: Revisits finish the work
- **WHEN** I return to that series page
- **THEN** up to 20 more are fetched on each visit until none remain

#### Scenario: Nothing due
- **WHEN** every TMDB set of a series was fetched within the last 30 days
- **THEN** opening its page makes no TMDB request

### Requirement: TMDB failures and rate limiting never break a page
A TMDB problem of any kind (an error response, a timeout, a rejected key or a rate-limit response) SHALL NOT fail any of the following:
- the page's read
- the follow-up request
- any action the fetch is part of

The affected set SHALL be left as it was, and the failure SHALL be logged.

On a rate-limit response, the system SHALL wait as long as TMDB asks, up to 10 seconds, and retry that request once. If the retry is also refused, it SHALL count as a failure for that set.

The API key SHALL NOT appear in any log line.

#### Scenario: TMDB is down
- **WHEN** TMDB does not answer during a follow-up fetch
- **THEN** the follow-up returns whatever is cached, the page keeps working, and the failure is logged

#### Scenario: Rate limited once
- **WHEN** TMDB answers a request with a rate-limit response that asks for a two-second wait
- **THEN** the request is retried once after two seconds and its result stored

#### Scenario: A rejected key
- **WHEN** TMDB rejects the configured key
- **THEN** no set is changed, the page keeps working, and the failure is logged without the key

### Requirement: TMDB is credited with its logo and notice
The system SHALL credit TMDB as TMDB's API terms of use require. The credit SHALL consist of:
- TMDB's logo, unaltered and linking to `https://www.themoviedb.org/`. It SHALL be less prominent than the application's own name and marks.
- this notice, word for word: "This application uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB."

The credit SHALL be shown in both of these places:
- wherever a picture picker offers TMDB images
- in the Settings page's Credits group (`settings-page`), whether or not a TMDB API key is configured

#### Scenario: A picker with TMDB images
- **WHEN** I open a picture picker that holds TMDB images
- **THEN** it shows TMDB's logo and the notice

#### Scenario: A picker without TMDB images
- **WHEN** I open a picture picker that holds no TMDB images
- **THEN** no TMDB logo or notice is shown

#### Scenario: The Settings page credits TMDB
- **WHEN** I open the Settings page, with or without a TMDB API key configured
- **THEN** its Credits group shows TMDB's logo and the notice

#### Scenario: The logo links to TMDB
- **WHEN** I click TMDB's logo
- **THEN** `https://www.themoviedb.org/` opens in a new tab and the page I was on is unchanged

### Requirement: A TMDB picture is downloaded at a size fitted to where it is drawn
Wherever the app draws a TMDB picture, the browser SHALL download it at one of TMDB's fixed widths rather than as the `original` file. The width SHALL be the smallest of `w342`, `w500`, `w780` and `w1280` that is at least twice the widest the surface can draw that picture, so the picture stays sharp on a high-density screen. The `original` file SHALL be downloaded only where no fixed width is wide enough.

Surfaces that draw pictures at about the same size SHALL share one width, so the choice is made per kind of surface (row slots, grid cards and poster boxes, wider tiles and picker options, page headers) rather than per picture.

The width SHALL change only the address the browser downloads from. The picture's identity SHALL remain its `original` URL everywhere else. That covers what is stored as a choice, what the API sends and accepts, what the picker compares to mark the current picture, and what device transfer and list backup carry. Choosing a picture in the picker SHALL store the `original` URL, as today.

A MyAnimeList picture SHALL be downloaded exactly as today.

If the fitted download fails, the surface SHALL fall back to the `original` file, so a TMDB change to its fixed sizes can never leave a picture missing.

The blurred fill behind an upright or wide picture SHALL use the same download as the picture itself, so it still costs no additional request.

#### Scenario: A grid card downloads a fitted poster
- **WHEN** the Series page shows a series whose chosen picture is `https://image.tmdb.org/t/p/original/abc123.jpg`
- **THEN** the browser downloads `https://image.tmdb.org/t/p/w780/abc123.jpg` for its card, and never the `original` file

#### Scenario: A row downloads a small rendition
- **WHEN** My list shows a row whose chosen picture is a TMDB image
- **THEN** the browser downloads that image's `w342` rendition for the row

#### Scenario: The header gets a larger rendition
- **WHEN** the anime detail page shows a TMDB picture
- **THEN** the browser downloads its `w1280` rendition

#### Scenario: Choosing still stores the original
- **WHEN** I pick a TMDB image in the picture picker, whose option was downloaded at `w780`
- **THEN** the stored choice is the image's `original` URL, and the picker marks that option as the current picture

#### Scenario: MAL pictures are unchanged
- **WHEN** any surface shows a MyAnimeList picture
- **THEN** it is downloaded from the same URL as before this change

#### Scenario: A failed fitted download falls back
- **WHEN** the fitted rendition of a TMDB picture fails to load
- **THEN** the surface loads the `original` file instead and draws it

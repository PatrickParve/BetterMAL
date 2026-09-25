## ADDED Requirements

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

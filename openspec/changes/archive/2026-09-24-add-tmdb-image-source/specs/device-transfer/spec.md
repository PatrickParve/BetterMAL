## MODIFIED Requirements

### Requirement: An imported choice passes the picker's own checks
A choice from the file that sets a value SHALL be stored only if the picker would accept the same value here:
- **An anime picture:** the anime is in my list, and the picture is one of that anime's pictures as the picker offers them, from MyAnimeList or from TMDB.
- **A series picture:** the picture is one the series' picker offers.
- **A series title:** the title passes the rule for a custom series title, checked against the titles offered for that series here. It SHALL be stored in the form that rule stores.

A choice that clears a value SHALL need no check, as clearing in the picker needs none.

A picture choice refused because the picture is not among those offered SHALL be checked **once more** before it is reported:
1. The picture sets the offer is built from SHALL first be fetched again, including sets fetched before. What is fetched depends on the refused picture:
   - **Not a TMDB image:** fetch from MyAnimeList, as follows.
     - For an anime picture, fetch that anime's own set.
     - For a series picture, fetch the set of each main-line member in my list.
   - **A TMDB image:** fetch from TMDB every set the anime or series draws from (`tmdb-artwork`), whatever their age. The series page's per-visit budget does not apply.
2. The choice SHALL then be checked again.

MyAnimeList picture sets SHALL still be fetched only for anime in my list. When TMDB access is not configured on this device, no TMDB set SHALL be fetched, and a refused TMDB choice SHALL go straight to being reported.

A choice that is still refused after that, or that is refused for any other reason, SHALL NOT be stored. It SHALL be reported, and every other choice SHALL still be applied.

#### Scenario: A picture accepted once its set is fetched
- **WHEN** the file chooses a picture for an anime in my list whose picture set this device has never fetched
- **THEN** that anime's set is fetched, the choice is checked again and stored, and nothing is reported for it

#### Scenario: A set fetched long ago is fetched again
- **WHEN** the file chooses a picture that MyAnimeList added after this device last fetched that anime's set
- **THEN** the set is fetched again, and the choice is stored

#### Scenario: A TMDB choice accepted once its sets are fetched
- **WHEN** the file chooses a TMDB image for a mapped anime in my list whose TMDB sets this device has never fetched, and TMDB access is configured
- **THEN** that anime's TMDB sets are fetched, the choice is checked again and stored, and no MyAnimeList request is made for it

#### Scenario: A TMDB choice without TMDB access
- **WHEN** the file chooses a TMDB image this device has not cached, and no TMDB API key is configured here
- **THEN** the choice is not stored, and it is reported

#### Scenario: Still refused after the fetch
- **WHEN** the file chooses a picture that is not among the anime's pictures, even after its set is fetched
- **THEN** the choice is not stored, and it is reported

#### Scenario: An anime not in my list
- **WHEN** the file chooses a picture for an anime that is not in my list here
- **THEN** the choice is not stored, and it is reported

#### Scenario: A title that is not a trim of an offered title
- **WHEN** the file chooses a series title that is not a contiguous trim of any title offered for that series here
- **THEN** the title is not stored, and it is reported

#### Scenario: A clear needs no check
- **WHEN** the file carries a newer cleared picture choice for an anime that is not in my list here
- **THEN** the clear and its time are stored

## MODIFIED Requirements

### Requirement: No import while the list is being imported
The system SHALL refuse to start an import, saying why, until a MyAnimeList list import has **gone through my whole list** since the application started.

A list import run has gone through the list when it has read the list and dealt with every anime it found missing. That includes a run that found nothing missing, and a run that ended with some anime it could not fetch. A run that failed before it could read the list has not gone through it.

A choice cannot be applied to an anime that is not yet in my list. Importing into a half-populated device would therefore report failures that are not failures.

The refusal SHALL say why, in terms of where the list import stands:
- **while a run is fetching anime:** how far it has got
- **while a run is still reading the list:** that the list is being checked against MyAnimeList
- **when the last run could not read the list:** that it could not, and when it will try again, or that it will try when the app next starts
- **while the connection to MyAnimeList is lost:** that the list cannot be checked until I re-authorize
- **when no run has started since the application started:** that the list has not finished importing since the app started

The system SHALL refuse to start an import while another import is running. Two imports SHALL never run at once.

#### Scenario: The list is still being imported
- **WHEN** I import a file while the list import stands at 142 of 380
- **THEN** the import is refused, saying the list is still being imported and how far it has got, and nothing changes

#### Scenario: The list is being checked
- **WHEN** I import a file while the list import is still reading my MyAnimeList list
- **THEN** the import is refused, saying the list is being checked against MyAnimeList

#### Scenario: The list could not be read
- **WHEN** I import a file after the list import failed to read my MyAnimeList list and a retry is planned
- **THEN** the import is refused, saying the list couldn't be read and when it will try again

#### Scenario: The connection is lost
- **WHEN** I import a file while the connection to MyAnimeList is lost
- **THEN** the import is refused, saying the list can't be checked until I re-authorize

#### Scenario: A list import with nothing missing opens the way
- **WHEN** the list import has read my list and found nothing missing
- **THEN** a file I import is accepted

#### Scenario: Anime the list import could not fetch do not block
- **WHEN** the list import went through my list but could not fetch two anime
- **THEN** a file I import is accepted, and choices for those two anime are reported as failures in its report

#### Scenario: An import is already running
- **WHEN** I import a file while another import is running
- **THEN** the second is refused, and the first carries on

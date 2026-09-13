## MODIFIED Requirements

### Requirement: Visible import progress indicator
The system SHALL show the import's progress, on the Settings page, only while a run has work to do or has something to report about it.

**Work** SHALL be each anime on my MyAnimeList list that this device's list lacks:
- an anime with no stored metadata, which needs a full-detail fetch
- an anime whose metadata is stored but which has no list entry

An anime whose removal from my list is still pending SHALL NOT be work (see "The import leaves a pending removal alone").

The run's **total** SHALL count only the work, never the whole MyAnimeList list, and its progress SHALL count the work done (for example "3 / 12").

An anime in the work that MyAnimeList lists with a status the app does not recognize SHALL be **left out** (see `mal-write-sync`, "A MyAnimeList list status the app does not recognize is never guessed"):
- no list entry is added for it
- no metadata is fetched for it
- a warning names it and the status MyAnimeList gave

It stays in the run's total and does not count as done. The rest of the run SHALL carry on unaffected.

- A run SHALL be **quiet** while it reads the list, because until then it cannot know whether it has work.
- A run that finds **no work** SHALL show nothing at all. It SHALL also clear anything an earlier run in the same session left shown, since nothing is missing any more.
- Where this device's list holds **no entries**, a first import is certain to have work. The run SHALL therefore be shown from the moment it starts, before its total is known.
- A run with work SHALL end as **complete** when every anime was added. It SHALL end as **failed** when any could not be fetched or any was left out, saying how many of each. The anime that could not be fetched or were left out stay missing, so a later run tries them again.
- A run that fails **before it has read the list** SHALL stay quiet, since it does not know whether it had work, unless it was shown from the start; then it SHALL be shown as failed with its reason. Either way, whatever an earlier run left shown SHALL stay shown.

#### Scenario: Showing progress
- **WHEN** a run that found work is fetching it
- **THEN** the UI displays the count of anime brought in versus the number it found missing

#### Scenario: Nothing new shows nothing
- **WHEN** the application starts and every anime on my MyAnimeList list is already on this device
- **THEN** no import progress, outcome, or heading is shown

#### Scenario: Anime added on another device
- **WHEN** I added four anime to MyAnimeList from my other device and this device starts
- **THEN** the import is shown with a total of four, and then reports how it ended

#### Scenario: A first import is shown from the start
- **WHEN** this device's list is empty and the import starts
- **THEN** the import is shown straight away as starting, before it has read the list, and then with its total

#### Scenario: Anime that could not be fetched
- **WHEN** a run with twelve anime to bring in cannot fetch three of them
- **THEN** it ends as failed, saying that three of twelve could not be fetched

#### Scenario: An anime with an unrecognized status is left out
- **WHEN** a run with twelve anime to bring in finds that MyAnimeList lists one of them with a status the app does not recognize
- **THEN** no entry is added and no metadata is fetched for that anime, the other eleven are brought in, and the run ends as failed, saying that one of twelve was left out

#### Scenario: A failed list read on a device with entries stays quiet
- **WHEN** this device already has list entries and the import cannot read the MyAnimeList list at all
- **THEN** nothing new is shown for the import

#### Scenario: A failed first import is shown
- **WHEN** this device's list is empty and the import cannot read the MyAnimeList list
- **THEN** the import is shown as failed, with its reason

### Requirement: An import that could not finish tries again on its own

A run that could not finish SHALL be followed by another run, started by the application on its own, after 1, 5, 15, 60 and 60 minutes in turn. A run could not finish when it failed before reading the list, or ended with anime it could not fetch or left out.

- After the fifth such run, no further run SHALL be started until the application starts again or I re-authorize.
- A run that finishes with nothing left missing SHALL end the sequence.
- Re-authorizing SHALL start a run at once and begin the sequence afresh.

No run SHALL be started, by a retry or otherwise, while the connection is lost. A run that failed because the connection was lost SHALL NOT plan another.

While another run is planned, the import's reported state SHALL carry when it will start, so the Settings page can say so. When none is planned after a failure, the page SHALL say that the import tries again when the app next starts.

#### Scenario: A failed start is retried
- **WHEN** the application starts before the network is up, so the import cannot read the list
- **THEN** the import runs again a minute later, and again after 5 and 15 minutes if it keeps failing

#### Scenario: A run that left anime out is retried
- **WHEN** a run ends having left out an anime whose MyAnimeList status the app does not recognize
- **THEN** another run is planned in the same sequence as for anime that could not be fetched

#### Scenario: Retries stop after the fifth
- **WHEN** five retries in a row could not finish
- **THEN** no further run is started until the application starts again or I re-authorize, and the page says it will try again when the app next starts

#### Scenario: A successful retry ends the sequence
- **WHEN** a retry brings in every missing anime
- **THEN** no further retry is planned

#### Scenario: No retries while the connection is lost
- **WHEN** the import fails because the connection to MyAnimeList was lost
- **THEN** no retry is planned, and the next run starts when I re-authorize

#### Scenario: The next try is announced
- **WHEN** a shown import has failed and a retry is planned
- **THEN** its reported state carries the time of the retry

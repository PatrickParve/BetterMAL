# initial-import Specification

## Purpose
The initial-import capability governs the one-time background import of my full MyAnimeList list after I authorize the app: inserting entries progressively as they arrive, pacing requests conservatively, resuming an import that was interrupted, and showing visible progress while it runs. An import that could not finish retries on its own, and it never touches an entry I've already asked to remove while the import is still catching up to it.
## Requirements
### Requirement: Background full-list import after authorization
The system SHALL run a background job that fetches the full list via `GET /v2/users/@me/animelist`, paginated at roughly 100 entries per page. It SHALL run:

- after the one-time authorization, and after every later re-authorization
- each time the application starts while a MyAnimeList connection is stored

It SHALL NOT run while the connection is recorded as lost (see `mal-api-integration`, "A refused sign-in is recorded as a lost connection"). Re-authorizing starts it.

#### Scenario: Paginated list fetch
- **WHEN** the initial import runs
- **THEN** it pages through the entire `@me` animelist rather than assuming a single response

#### Scenario: The import runs at every start
- **WHEN** the application starts with a MyAnimeList connection stored
- **THEN** the import runs

#### Scenario: No run while the connection is lost
- **WHEN** the application starts while the connection is recorded as lost
- **THEN** the import does not run

#### Scenario: Re-authorizing starts it
- **WHEN** I re-authorize
- **THEN** the import runs straight away

### Requirement: Progressive per-entry insertion
The system SHALL fetch full anime metadata for each list entry and insert it into Postgres as each fetch completes, not batched at the end, so the UI can render partial data while the sync runs.

#### Scenario: Rendering during import
- **WHEN** the import has completed some but not all entries
- **THEN** the already-imported anime are present in Postgres and viewable while remaining ones continue loading

### Requirement: Conservative request pacing
The system SHALL pace import requests conservatively (roughly one request per second) so a library of a few hundred anime completes in low minutes.

#### Scenario: Paced fetching
- **WHEN** the import fetches metadata for many entries
- **THEN** requests are spaced out rather than issued in an unthrottled burst

### Requirement: Resumable import
The system SHALL make the import resumable by skipping anime already present in AnimeMetadata and continuing, rather than restarting from zero, if the app or PC restarts mid-sync.

#### Scenario: Restart mid-sync
- **WHEN** the import is interrupted and restarts
- **THEN** anime already stored are skipped and the import continues with the remaining entries

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

### Requirement: The import leaves a pending removal alone

The import SHALL NOT add a list entry, or fetch metadata, for an anime whose removal from my list is still pending. This SHALL hold whether the removal is waiting to be pushed or is held for review.

MyAnimeList still listing that anime is the expected in-flight state, exactly as reconciliation treats it. Re-adding the entry would undo a removal I made.

Once the removal has been pushed, MyAnimeList no longer lists the anime and there is nothing for the import to find. Where a held removal is declined, the entry is restored by the decline, not by the import.

#### Scenario: A pending removal is not restored
- **WHEN** I removed an anime, its removal has not reached MyAnimeList, and the application restarts
- **THEN** the import does not add the anime back to my list, and does not count it as work

#### Scenario: A held removal stays for my decision
- **WHEN** a removal is held for review and the import runs
- **THEN** the anime stays absent locally and the removal stays held

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


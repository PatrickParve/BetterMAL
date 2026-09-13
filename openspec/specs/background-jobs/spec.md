# background-jobs Specification

## Purpose
The background-jobs capability defines the one shared lifecycle every long-running background job in the app reports through — the MyAnimeList list import, sync now, run full reconciliation, deciding every held change, the full airing-date refresh, the build-all-series run, and the import from a file. It governs how a job's state is started, held, and read, so that every surface that shows job progress (the Settings page, and later the navbar) can read one consistent shape rather than each job inventing its own.

## Requirements

### Requirement: Every background job shares one lifecycle

The system's background jobs SHALL each report their state in one shared shape:

- a **phase**: not started, running, complete, or failed
- the work **done** and, once it is known, the **total** it is out of. While a job cannot yet know its total, the total SHALL be reported as unknown, never as zero.
- when it failed, a **reason** in plain words where the job knows one — MyAnimeList or AniList could not be reached, or the connection to MyAnimeList was lost — and otherwise a statement that the reason is in the backend logs
- when the run **started** and when it **ended**

The jobs SHALL be:
- the MyAnimeList list import
- sync now
- run full reconciliation
- deciding every held change (accept all or decline all)
- the full airing-date refresh
- the build-all-series run
- the import from a file

A job that has not run since the application started SHALL report "not started" and nothing else.

#### Scenario: A job with an unknown total
- **WHEN** run full reconciliation has read 300 anime from my MyAnimeList list and is still reading
- **THEN** it reports running, 300 done, and an unknown total, not a total of zero

#### Scenario: A failure carries its reason
- **WHEN** run full reconciliation fails because MyAnimeList cannot be reached
- **THEN** it reports failed, the progress it reached, and the reason that MyAnimeList couldn't be reached

#### Scenario: An unexplained failure points at the logs
- **WHEN** a job fails for a reason it cannot name
- **THEN** its reason says that the details are in the backend logs

#### Scenario: A job that never ran
- **WHEN** the application has just started and build all series has not been run
- **THEN** it reports not started, with no progress, reason or times

### Requirement: A job is started once

Starting a job SHALL check that it is neither starting nor running and mark it running **in one step**. Any request to start it that arrives while it is starting or running SHALL start nothing, and SHALL queue nothing to run afterwards. This SHALL hold whether the requests come from one page, from several browsers, or both. Such a request SHALL be answered with the state of the run already in progress.

The request that does start a job SHALL be answered with the job already reported as running, before any of its work has begun, so one request is enough for a page to show it.

A job that has ended, whether complete or failed, SHALL start again on the next request.

#### Scenario: The first press reports running
- **WHEN** I start the full airing-date refresh
- **THEN** the answer to that request already reports it as running

#### Scenario: A second press while running starts nothing
- **WHEN** run full reconciliation is running and a second request to start it arrives, from this browser or another
- **THEN** no second run starts or is queued, and the answer reports the run in progress

#### Scenario: A second press while starting starts nothing
- **WHEN** a job has been started but its work has not yet begun, and a second request to start it arrives
- **THEN** exactly one run takes place

#### Scenario: A finished job starts again
- **WHEN** build all series has completed and I start it again
- **THEN** a new run starts

### Requirement: A run always ends as complete or failed

Every run SHALL end as **complete** or **failed**. A run whose work throws SHALL be reported as failed, with the progress it had reached, whether it throws before its total is known or partway through. No run SHALL stay reported as running once its work has stopped.

A run that gets through its work but cannot do all of it SHALL end as failed rather than complete, saying how many items were left. This covers:
- edits it could not send
- held changes that stayed held
- anime it could not fetch or refresh
- series it could not build
- anime it left out because MyAnimeList gave a list status the app does not recognize (see `mal-write-sync`, "A MyAnimeList list status the app does not recognize is never guessed")

Such a run SHALL still process every item it can. Ending as failed reports the items left undone and does not stop the run early.

An item that was handled and simply had nothing to store is **not** left undone. This covers an anime AniList has no airing data for, and an anime whose story relations lead to no other anime. It SHALL NOT make a run fail.

A run stopped because the application itself is stopping is exempt: its state is lost with the process.

#### Scenario: A crash is reported
- **WHEN** the full airing-date refresh throws partway through
- **THEN** it is reported as failed with the progress it had reached, and not as running

#### Scenario: A crash before the total is known
- **WHEN** a job throws before it has found how much work it has
- **THEN** it is reported as failed rather than left running

#### Scenario: A partial run is not complete
- **WHEN** sync now sends 3 of 5 edits and the other 2 fail
- **THEN** it ends as failed, saying that 2 of 5 could not be sent

#### Scenario: An airing-date refresh with failed anime is not complete
- **WHEN** the full airing-date refresh processes 595 anime and the refresh of 3 of them fails
- **THEN** it still processes all 595, and ends as failed, saying that 3 of 595 could not be refreshed

#### Scenario: Anime with no airing data do not fail the refresh
- **WHEN** the full airing-date refresh processes 595 anime, AniList has no airing data for 12 of them, and none fail
- **THEN** it ends as complete

#### Scenario: A bulk series build with failed targets is not complete
- **WHEN** build all series processes 40 targets and building 2 of them fails
- **THEN** it still processes all 40, and ends as failed, saying that 2 of 40 could not be built

### Requirement: Job state is kept on the server, in memory

A job's state SHALL be held by the server, not by the page that started it. Leaving the page and coming back, or opening it in another browser, SHALL show the same run and the same progress.

Job state SHALL NOT be written to storage. A restart SHALL return every job to "not started", which is acceptable even for a finished job's report.

The weekly check's outcome is not a job's state, and is kept across restarts (see `mal-write-sync`).

#### Scenario: Leaving the page keeps the run
- **WHEN** I start the full airing-date refresh, leave Settings, and come back while it runs
- **THEN** the page shows the same run and its current progress

#### Scenario: Another browser sees the run
- **WHEN** a job is running and I open Settings in a second browser
- **THEN** the second browser shows the same run and progress

#### Scenario: A restart clears finished reports
- **WHEN** a job has completed and the application restarts
- **THEN** that job reports not started

### Requirement: Automatic runs of shared work are told apart from runs I started

Where an automatic process runs the same work as a job I can start, its runs SHALL NOT be reported as that job:

- the weekly reconciliation SHALL NOT be reported as a run of "Run full reconciliation"
- the two-minute push retry SHALL NOT be reported as a run of "Sync now"

An automatic run SHALL NOT change that job's phase, progress, reason or times, and SHALL NOT make the job read as running.

#### Scenario: The weekly check shows no progress
- **WHEN** the weekly reconciliation runs while nobody has started a reconciliation
- **THEN** "Run full reconciliation" still reports what it reported before, and never shows the weekly run's progress

#### Scenario: The push retry is not a sync now
- **WHEN** the two-minute retry pushes pending edits
- **THEN** "Sync now" still reports what it reported before

### Requirement: Job and connection state are read together

The system SHALL provide **one read** that returns, together:

- the state of every job listed in "Every background job shares one lifecycle", and for each one whether its **outcome has been reported as seen** (see "A job's outcome can be reported as seen")
- the MyAnimeList connection state (see `mal-api-integration`, "The connection state is reported")
- when the weekly check last ran, whether it failed, with its reason, and whether that outcome has been reported as seen
- what is **waiting for me to decide**: how many changes are held for review, and whether a reconciliation diff is waiting
- the **pending-push figures**: how many of my edits are waiting to be sent, and when the last push succeeded

The read SHALL NOT call MyAnimeList or any other outside service. It SHALL be answerable from state the application already holds — its in-memory job state plus counts and single-row reads of its own database — and SHALL be cheap enough to repeat every second.

The held count SHALL be counted from stored rows. It is therefore the count before any item that MyAnimeList already agrees with has been cleared, and MAY briefly exceed what the Settings page's review lists (see `settings-page`, "The sync readout separates what is retrying from what is waiting on me").

Whether a diff is waiting SHALL be reported as a plain yes or no. The diff itself SHALL NOT be part of this read.

It SHALL report the MyAnimeList list import as that import is shown, so a quiet run appears as not started (see `initial-import`, "Visible import progress indicator").

This read exists so that the Settings page and the navbar see one consistent picture, from one request, rather than making one request per job or per fact.

#### Scenario: One request answers everything
- **WHEN** the application reads the job and connection state
- **THEN** a single request returns every job's state, whether each job's outcome has been seen, the connection state, the weekly check's outcome, the held count, whether a diff is waiting, and the pending-push figures

#### Scenario: No outside calls
- **WHEN** that read is made while MyAnimeList is unreachable
- **THEN** it answers from what the application already holds, without contacting MyAnimeList

#### Scenario: A quiet list import reads as nothing
- **WHEN** the list import is reading my MyAnimeList list and has not yet found any work
- **THEN** the read reports the list import as not started

#### Scenario: A waiting diff is a yes, not a payload
- **WHEN** a reconciliation diff of 40 differences is waiting for review
- **THEN** the read says a diff is waiting, and does not carry its differences

#### Scenario: The held count comes from stored rows
- **WHEN** three changes are held for review, one of which MyAnimeList already agrees with
- **THEN** the read reports three, and contacts MyAnimeList about none of them

### Requirement: A job's outcome can be reported as seen

Every job's ended outcome SHALL carry whether it **has been reported as seen**. A run that has not ended SHALL carry it as not seen.

The system SHALL accept a report that names one or more jobs, each with the **time its run ended** as the reporter saw it. For each named job:

- the outcome SHALL be recorded as seen only when the run it holds still ended at that time;
- a time that no longer matches SHALL record nothing, so a run that ended between the reporter's read and its report is not marked seen on the strength of the previous run's report;
- a job that is not running and never ran, or is still running, SHALL record nothing.

A report naming an unknown job SHALL be refused.

**Starting a run clears it.** A job moved to running SHALL carry its outcome as not seen again, so the new run's ending is reported afresh.

**Where it is kept.** Because a job's state is held in memory (see "Job state is kept on the server, in memory"), so is this. A restart returns every job to not started, which takes the question with it.

The weekly check's outcome is not a job's state and is kept across restarts (see `mal-write-sync`). Whether **it** has been seen SHALL be kept across restarts with it, against the run it belongs to, and SHALL be cleared when a later weekly run records a new outcome.

A report SHALL be accepted from any browser on this device and SHALL apply to all of them, since the state is the server's.

#### Scenario: An outcome is reported as seen
- **WHEN** the build-all-series run has failed, and its failure is reported as seen with the time it ended
- **THEN** the read reports that job's outcome as seen, with its phase, progress and reason unchanged

#### Scenario: A stale report records nothing
- **WHEN** a report arrives naming a job and an end time that is not the end time of the run the job now holds
- **THEN** nothing is recorded as seen, and the run's outcome still reads as unseen

#### Scenario: A new run is unseen again
- **WHEN** a job's outcome has been reported as seen and that job is started again
- **THEN** it reads as not seen while it runs, and its new outcome reads as not seen when it ends

#### Scenario: A running job cannot be reported
- **WHEN** a report names a job that is still running
- **THEN** nothing is recorded

#### Scenario: An unknown job is refused
- **WHEN** a report names a job the system does not have
- **THEN** the report is refused

#### Scenario: A restart forgets the question
- **WHEN** a job's outcome has been reported as seen and the application restarts
- **THEN** that job reports not started

#### Scenario: A seen weekly failure stays seen across a restart
- **WHEN** the weekly check's failure has been reported as seen and the application restarts
- **THEN** the weekly check still reports that failure, still as seen

#### Scenario: A new weekly outcome is unseen
- **WHEN** the weekly check's failure has been reported as seen and a later weekly run fails
- **THEN** the weekly check's outcome reads as not seen again

# background-jobs Specification

## Purpose
The background-jobs capability defines the one shared lifecycle every long-running background job in the app reports through — the MyAnimeList list import, sync now, run full reconciliation, deciding every held change, the corrective re-sync, the full airing-date refresh, the build-all-series run, and the import from a file. It governs how a job's state is started, held, and read, so that every surface that shows job progress (the Settings page, and later the navbar) can read one consistent shape rather than each job inventing its own.

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
- the corrective re-sync
- the full airing-date refresh
- the build-all-series run
- the import from a file

A job that has not run since the application started SHALL report "not started" and nothing else.

#### Scenario: A job with an unknown total
- **WHEN** run full reconciliation has read 300 anime from my MyAnimeList list and is still reading
- **THEN** it reports running, 300 done, and an unknown total, not a total of zero

#### Scenario: A failure carries its reason
- **WHEN** the corrective re-sync fails because MyAnimeList cannot be reached
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
- **WHEN** the corrective re-sync is running and a second request to start it arrives, from this browser or another
- **THEN** no second run starts or is queued, and the answer reports the run in progress

#### Scenario: A second press while starting starts nothing
- **WHEN** a job has been started but its work has not yet begun, and a second request to start it arrives
- **THEN** exactly one run takes place

#### Scenario: A finished job starts again
- **WHEN** build all series has completed and I start it again
- **THEN** a new run starts

### Requirement: A run always ends as complete or failed

Every run SHALL end as **complete** or **failed**. A run whose work throws SHALL be reported as failed, with the progress it had reached, whether it throws before its total is known or partway through. No run SHALL stay reported as running once its work has stopped.

A run that gets through its work but cannot do all of it SHALL end as failed rather than complete, saying how many items were left. This covers edits it could not send, held changes that stayed held, and anime it could not fetch.

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

### Requirement: Job state is kept on the server, in memory

A job's state SHALL be held by the server, not by the page that started it. Leaving the page and coming back, or opening it in another browser, SHALL show the same run and the same progress.

Job state SHALL NOT be written to storage. A restart SHALL return every job to "not started", which is acceptable even for a finished job's report.

The weekly check's outcome is not a job's state, and is kept across restarts (see `mal-write-sync`).

#### Scenario: Leaving the page keeps the run
- **WHEN** I start the corrective re-sync, leave Settings, and come back while it runs
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

- the state of every job listed in "Every background job shares one lifecycle"
- the MyAnimeList connection state (see `mal-api-integration`, "The connection state is reported")
- when the weekly check last ran, and whether it failed, with its reason

The read SHALL NOT call MyAnimeList or any other outside service. It SHALL be cheap enough to repeat every second.

It SHALL report the MyAnimeList list import as that import is shown, so a quiet run appears as not started (see `initial-import`, "Visible import progress indicator").

This read exists so that the Settings page, and later the navbar, see one consistent picture rather than making one request per job.

#### Scenario: One request answers everything
- **WHEN** the Settings page reads the job and connection state
- **THEN** a single request returns every job's state, the connection state, and the weekly check's outcome

#### Scenario: No outside calls
- **WHEN** that read is made while MyAnimeList is unreachable
- **THEN** it answers from what the application already holds, without contacting MyAnimeList

#### Scenario: A quiet list import reads as nothing
- **WHEN** the list import is reading my MyAnimeList list and has not yet found any work
- **THEN** the read reports the list import as not started

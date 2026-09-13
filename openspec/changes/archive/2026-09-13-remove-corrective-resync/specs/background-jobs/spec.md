## MODIFIED Requirements

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

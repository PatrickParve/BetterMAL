## MODIFIED Requirements

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

## ADDED Requirements

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
- **WHEN** the corrective re-sync has failed, and its failure is reported as seen with the time it ended
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

# navbar-settings-status Specification

## Purpose
The navbar-settings-status capability governs the Settings control's two indicators — the "needs me" badge and the background-job progress bar — and exactly what raises and clears each: an outcome I've already been shown or routine background work raises nothing, and only one bar shows at a time, oldest first. Both are derived from server state alone, at no cost outside this device, so every browser open on the same device agrees. It covers only the navbar's own signal; a job's lifecycle belongs to background-jobs, and showing jobs in full is settings-page's.

## Requirements

### Requirement: The Settings control signals that something needs me

The navbar's Settings control SHALL carry an indicator whenever at least one of these is true, and SHALL carry none otherwise:

- **changes are held for review** — edits made in a previous session, waiting for me to accept or decline;
- **a reconciliation diff is waiting** — MyAnimeList's list differs from this device's, whether a manually started reconciliation or the weekly check found it;
- **the connection to MyAnimeList is lost**;
- **a job ended** — completed or failed — and **its outcome has not been reported as seen** (see "An outcome I have already been shown raises nothing").

The indicator SHALL be the same indicator the Updates control carries, in the same position and appearance, from one shared definition rather than a copy of it, so the two cannot drift apart.

It SHALL carry no number. One indicator stands for however many reasons apply at once.

The indicator SHALL be reflected in the control's accessible name as well as visually, so it is not carried by appearance alone.

The Settings control SHALL otherwise keep what it does today: it navigates to the Settings page when clicked, and it marks itself as the current page while that page is showing.

#### Scenario: Held changes raise it
- **WHEN** two changes are held for review
- **THEN** the Settings control shows its indicator, and its accessible name says something needs attention

#### Scenario: A waiting diff raises it
- **WHEN** a reconciliation has stored a diff that I have not yet accepted or cancelled
- **THEN** the Settings control shows its indicator

#### Scenario: A lost connection raises it
- **WHEN** MyAnimeList has stopped accepting this device's login
- **THEN** the Settings control shows its indicator

#### Scenario: A finished job raises it
- **WHEN** the build-all-series run completes while I am on another page
- **THEN** the Settings control shows its indicator

#### Scenario: A failed job raises it
- **WHEN** the full airing-date refresh fails because AniList couldn't be reached
- **THEN** the Settings control shows its indicator

#### Scenario: Nothing waiting shows nothing
- **WHEN** nothing is held, no diff is waiting, the connection is fine, and no job has ended unseen
- **THEN** the Settings control shows no indicator, and the control itself is still present

#### Scenario: Several reasons are still one indicator
- **WHEN** changes are held, a diff is waiting, and a job has just failed
- **THEN** the Settings control shows one indicator, with no count

#### Scenario: It still opens Settings
- **WHEN** I click the Settings control while its indicator is showing
- **THEN** the Settings page opens, exactly as it does without the indicator

### Requirement: What clears the indicator depends on what raised it

An indicator raised by **held changes, a waiting diff, or a lost connection** SHALL clear only when that fact is no longer true — every held change decided, the diff accepted or cancelled, the connection re-authorized. Opening the Settings page SHALL NOT by itself clear it.

An indicator raised by an **ended job** SHALL clear once that outcome has been reported as seen, which opening the Settings page does (see `settings-page`, "The page reports the outcomes it shows as seen"). The outcome itself SHALL stay on the Settings page; only the indicator clears.

A **failed weekly check** SHALL clear the same way an ended job's does, since the Settings page is where its reason is shown.

Because an application restart returns every job to not-started, it SHALL also clear any indicator those jobs' outcomes were raising. An indicator raised by held changes, a waiting diff, a lost connection, or a failed weekly check SHALL survive a restart, because the facts behind those do.

#### Scenario: Opening the page does not clear held changes' indicator
- **WHEN** changes are held for review and I open the Settings page and leave it again without deciding them
- **THEN** the Settings control still shows its indicator

#### Scenario: Deciding the held changes clears it
- **WHEN** I accept or decline every held change
- **THEN** the Settings control's indicator clears

#### Scenario: Cancelling the diff clears it
- **WHEN** a diff was waiting and I cancel it
- **THEN** the Settings control's indicator clears

#### Scenario: Re-authorizing clears it
- **WHEN** the connection was lost and I re-authorize
- **THEN** the Settings control's indicator clears

#### Scenario: Opening the page clears a finished job's indicator
- **WHEN** a job has completed unseen and I open the Settings page
- **THEN** the Settings control's indicator clears, and the page still shows that job's outcome

#### Scenario: A restart clears a finished job's indicator
- **WHEN** a job has failed unseen and the application restarts
- **THEN** the Settings control shows no indicator for it, and the Settings page reports that job as not started

#### Scenario: A restart does not clear a lost connection's indicator
- **WHEN** the connection is lost and the application restarts
- **THEN** the Settings control still shows its indicator

### Requirement: An outcome I have already been shown raises nothing

A job outcome that has been reported as seen SHALL NOT raise the indicator again. A **new run** of that job SHALL clear that report, so the new run's own outcome raises the indicator as any other would.

A job that ends while I am watching it on the Settings page therefore SHALL NOT raise the indicator at all, since the page reports the outcome as seen as it shows it.

#### Scenario: A job watched to its end raises nothing
- **WHEN** I start the airing-date refresh from the Settings page and stay there until it completes
- **THEN** the Settings control shows no indicator for it

#### Scenario: The next run raises it again
- **WHEN** a job's outcome has been seen, and I start that job again from the Settings page, navigate away, and it fails
- **THEN** the Settings control shows its indicator

#### Scenario: A job started elsewhere and finished elsewhere raises it
- **WHEN** a job is started from the Settings page in one browser and finishes while every browser is showing another page
- **THEN** the Settings control shows its indicator in each of them

### Requirement: Routine background work raises nothing

Work that is not one of the reported jobs SHALL NOT raise the indicator and SHALL NOT draw the progress bar. That covers:

- the **weekly MyAnimeList check**, except when it fails — the differences it finds are already covered by the waiting diff;
- the **MyAnimeList list import that finds nothing to fetch** — a run with no work reports nothing at all;
- the **two-minute retry** of pushes that failed, the **push after each edit**, the **metadata refresh**, the **relation checks**, the **episode-schedule refresh**, and the **token refresh**.

#### Scenario: A weekly check that finds differences raises only the diff
- **WHEN** the weekly check runs and stores a diff
- **THEN** the Settings control's indicator stands for the waiting diff, and clears when I accept or cancel it, not when I open the page

#### Scenario: A weekly check that fails raises the indicator
- **WHEN** the weekly check fails because MyAnimeList couldn't be reached
- **THEN** the Settings control shows its indicator, and opening the Settings page clears it

#### Scenario: A quiet list import raises nothing
- **WHEN** the list import runs at startup and finds nothing on MyAnimeList that this device lacks
- **THEN** the Settings control shows no indicator and no progress bar

#### Scenario: Routine work stays silent
- **WHEN** the metadata refresh, the episode-schedule refresh, a push retry, or a token refresh runs
- **THEN** the Settings control shows no indicator and no progress bar

### Requirement: The Settings control shows a progress bar while a job runs

While any of the reported jobs is running, the navbar's Settings control SHALL carry a **thin progress bar along its bottom edge**, within the control's own bounds. The bar SHALL carry **no text and no counts**.

The bar SHALL be the same bar the Settings page shows for that job, in look and in behaviour, from one shared definition rather than a copy:

- **filled in proportion** to the work done out of the total, while the job knows its total;
- **moving continuously** while the job does not yet know its total, rather than sitting empty or showing a zero total;
- **still, and distinct from a proportional bar**, where the reader's system asks for reduced motion.

The jobs that draw it are the ones the system reports (see `background-jobs`, "Every background job shares one lifecycle"): the MyAnimeList list import while it has anime to fetch, sync now, run full reconciliation, accepting or declining every held change, the full airing-date refresh, the build-all-series run, and the import from a file.

The bar SHALL disappear as soon as no reported job is running.

The bar SHALL be reflected in the control's accessible name, so that a job in flight is not conveyed by appearance alone. The bar's proportion SHALL NOT be announced: it is decoration for what the Settings page reports in words.

#### Scenario: A job with a total fills the bar
- **WHEN** the build-all-series run is 40 of 120 through its work
- **THEN** the Settings control shows a bar filled to that proportion, with no text, and its accessible name says a job is running

#### Scenario: A job without a total moves the bar
- **WHEN** run full reconciliation is still reading my MyAnimeList list
- **THEN** the Settings control shows a moving bar rather than an empty or zero-filled one

#### Scenario: The bar goes when the work does
- **WHEN** the last running job ends
- **THEN** the Settings control shows no bar, and its accessible name no longer says a job is running

#### Scenario: Reduced motion keeps the bar still
- **WHEN** a job without a known total is running and the reader's system asks for reduced motion
- **THEN** the bar is still, and still distinguishable from a proportionally filled one

#### Scenario: The list import draws the bar when it has work
- **WHEN** the list import has 12 anime to fetch from MyAnimeList
- **THEN** the Settings control shows its progress bar

### Requirement: One bar at a time, oldest first

While more than one reported job is running, the Settings control SHALL show the bar of the job whose run **started first**. A job that has been started but whose work has not begun yet counts as started, at the time it was started.

When that job ends, the bar SHALL move to the job that started next among those still running, and the job that ended SHALL raise the indicator on the terms above. When no job is left running, the bar SHALL disappear.

Where two runs started at the same instant, which one the bar shows SHALL be decided the same way in every browser, rather than differing between them.

#### Scenario: The older job's bar is shown
- **WHEN** the build-all-series run has been going for a minute and I then start the airing-date refresh
- **THEN** the Settings control keeps showing the build's progress

#### Scenario: The bar hands over
- **WHEN** the older of two running jobs completes
- **THEN** the bar switches to the one still running, and the completed one raises the indicator

#### Scenario: A job that has not begun still counts
- **WHEN** I start a job whose work has not begun yet, and another job was already running
- **THEN** the bar still shows the job that was already running, and the new one is queued behind it in start order

### Requirement: The indicator and the bar can show at once

The indicator and the progress bar SHALL be able to show at the same time, and neither SHALL hide or displace the other. The control's accessible name SHALL then state both facts.

#### Scenario: Both at once
- **WHEN** changes are held for review while a job is running
- **THEN** the Settings control shows both its indicator and its progress bar, and its accessible name says both that something needs attention and that a job is running

### Requirement: Every browser on this device shows the same thing

The indicator and the progress bar SHALL be derived from state the server holds, not from state a browser keeps for itself, so that every browser on this device shows the same indicator and the same bar for the same facts. Neither SHALL travel between devices.

Each browser SHALL re-read that state on the same terms the Settings page does (see `background-jobs`, "Job and connection state are read together"): often while a job runs, seldom otherwise, and immediately when its window gains focus or its tab becomes visible. A browser SHALL NOT need a reload to pick up either.

Reporting an outcome as seen in one browser SHALL therefore clear its indicator in another as soon as that browser next reads the state.

#### Scenario: A second browser shows the same bar
- **WHEN** I start a job in one browser
- **THEN** another browser on this device shows the same progress bar on its Settings control, without a reload

#### Scenario: Opening Settings in one browser clears the indicator in another
- **WHEN** a job has ended unseen, both browsers show the indicator, and I open the Settings page in one of them
- **THEN** the other browser's indicator clears as soon as it next reads the state, and at once when I switch to it

#### Scenario: Catching up on focus
- **WHEN** something needing me appears while a browser's window is not focused
- **THEN** that browser shows the indicator as soon as its window gains focus, without waiting for its regular re-read

### Requirement: Reading the state for the navbar costs nothing outside this device

Whatever the navbar reads to decide the indicator and the bar SHALL be answerable from state this device already holds — in-memory job state and counts of its own rows — and SHALL NOT call MyAnimeList, AniList, or any other outside service, on any page.

In particular, the number of changes held for review SHALL be counted from stored rows rather than by asking MyAnimeList about each held item. A held change that would clear itself once MyAnimeList is consulted MAY therefore keep the indicator showing until the Settings page consults it. That is accepted: opening the page is what clears such an item, and the indicator follows on the next read.

#### Scenario: No outside calls from the navbar
- **WHEN** I move between pages with the indicator showing and a job running
- **THEN** nothing the navbar reads calls MyAnimeList or AniList

#### Scenario: A self-clearing held item may hold the indicator
- **WHEN** the one held change turns out to match MyAnimeList's current values, and I have not opened the Settings page since
- **THEN** the Settings control may still show its indicator, and it clears once the Settings page has cleared that item

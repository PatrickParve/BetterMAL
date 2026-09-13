## MODIFIED Requirements

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

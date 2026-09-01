# action-failure-notices Specification

## Purpose
TBD - created by archiving change polish-detail-dates-and-error-messages. Update Purpose after archive.

## Requirements

### Requirement: A failed action is never silent
The system SHALL report every action it took on the user's behalf that did not take effect, so no control can be pressed, appear to do nothing, and leave the user to work out whether it worked.

This SHALL cover, at minimum, every edit made from a control outside the entry editor — the "+" episode-increment button wherever it appears (the progress bar, the currently-watching carousel, my list, and the detail page), the inline episode-count edit, and the detail page's add-to-list and add-to-watching actions. An action whose failure the user is already told about in place — a save in the entry editor, which reports in the editor itself — SHALL NOT also raise this notice, so one failure is reported once.

The report SHALL say what did not happen and, where the server stated a reason, that reason. It SHALL name the anime the action was taken on, so a failure is attributable when the control that raised it is one of many on a page.

The system SHALL NOT silently roll an action back and say nothing: where the displayed value was not changed because the action failed, the notice is what accounts for it.

#### Scenario: An increment the server refuses
- **WHEN** I press "+" on an anime and the server rejects the edit
- **THEN** a notice reports that the episode count was not raised, names the anime, and states the server's reason

#### Scenario: An increment that fails with no reason given
- **WHEN** a "+" press fails without the server stating a reason
- **THEN** a notice still reports that the episode count was not raised for that anime

#### Scenario: The count does not move
- **WHEN** an increment fails
- **THEN** the episode count shown is the count before the press, and the notice explains why it did not move

#### Scenario: One failure is reported once
- **WHEN** a save made in the entry editor is rejected
- **THEN** the editor reports it and no separate app-wide notice is raised for the same failure

#### Scenario: An add-to-list that fails
- **WHEN** I use "Add to list" on a detail page and the request fails
- **THEN** a notice reports that the anime was not added, and the page does not show it as in my list

### Requirement: Failure notices are an app-wide mechanism
The failure notice SHALL be a single app-wide mechanism, mounted once outside the routed pages, rather than something each page or control implements for itself — on the same terms as the connection-status notice, so a control added later is covered without asking to be.

A notice SHALL be dismissable by the user and SHALL also dismiss itself after enough time to be read. It SHALL NOT block the page behind it, hold the page still, take focus away from what the user is doing, or require an acknowledgement before the app can be used again.

Where several actions fail at once, the notices SHALL NOT stack without limit: the system SHALL show the most recent few and SHALL NOT let a burst of failures cover the page.

A notice SHALL be announced to assistive technology as a status message rather than as an alert that interrupts, and SHALL be readable in both the light and the dark theme.

Navigating to another page SHALL dismiss the notices raised on the page left behind, since the control they refer to is no longer on screen.

#### Scenario: Mounted once for the whole app
- **WHEN** an action fails on any page
- **THEN** the same notice mechanism reports it, with no page-specific implementation involved

#### Scenario: The page stays usable
- **WHEN** a notice is showing
- **THEN** the page behind it still scrolls, still takes clicks, and keeps the focus it had

#### Scenario: A notice goes away on its own
- **WHEN** a notice has been shown long enough to read and I do nothing
- **THEN** it dismisses itself

#### Scenario: A notice can be dismissed early
- **WHEN** I use a notice's dismiss control
- **THEN** it disappears immediately

#### Scenario: A burst of failures does not cover the page
- **WHEN** several actions fail in quick succession
- **THEN** only the most recent few notices are shown at once

#### Scenario: Leaving the page clears its notices
- **WHEN** a notice is showing and I navigate to another page
- **THEN** the notice is dismissed

### Requirement: The client keeps the reason the server gave
The system SHALL preserve the message the backend returns with a rejected request so a caller can show it, rather than discarding the response body and reporting only a status code. Every rejection the API answers with a stated reason SHALL reach the caller carrying that reason.

A failure with no such body — a network error, an unreachable backend, a response that is not a stated rejection — SHALL reach the caller as it does today, and every existing caller that handles a failure without reading a reason SHALL keep working unchanged.

Reporting a failure SHALL NOT change what any request returns on success, and SHALL NOT interfere with the reachability reporting the `connection-status` capability defines.

#### Scenario: A stated reason survives to the caller
- **WHEN** the backend rejects a request with a stated reason
- **THEN** the caller receives that reason alongside the failure

#### Scenario: A failure with no stated reason
- **WHEN** a request fails at the network level
- **THEN** the caller receives a failure carrying no reason, and handles it as it does today

#### Scenario: Existing handlers are unaffected
- **WHEN** an existing caller catches a failure without reading a reason
- **THEN** it behaves exactly as it did before

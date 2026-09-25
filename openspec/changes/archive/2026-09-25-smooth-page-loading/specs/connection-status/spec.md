## MODIFIED Requirements

### Requirement: First-load failure keeps its full-page message
When the app's very first request fails — before the main shell has mounted and there is no page to show a notice over — the system SHALL keep showing its full-page "can't reach the backend" message rather than the notice bar.

The two SHALL NOT be shown at the same time: the full-page message covers the pre-shell case, and the notice covers every failure after the shell has mounted.

While the full-page message is showing, the system SHALL retry that first request by itself at the same fixed interval the notice's health poll uses, and SHALL offer a **Try again** control that retries it at once. As soon as a retry succeeds, the app SHALL continue exactly as a first load that had succeeded, to the connect screen or into the app, with no browser reload. The message SHALL say that it is retrying rather than asking me to reload the page.

Before that first request has settled, the pre-shell loading message SHALL follow the `page-load-states` capability's loading presentation: nothing for that capability's delay, then a loading indicator.

#### Scenario: Backend down at first load
- **WHEN** I load the app while the backend is down
- **THEN** the full-page "can't reach the backend" message is shown, saying it is retrying, and no notice bar appears over it

#### Scenario: The app starts by itself once the backend is up
- **WHEN** the full-page message is showing and the backend comes up
- **THEN** within one retry interval the app continues into the page I opened, without my reloading the browser

#### Scenario: Try again from the full-page message
- **WHEN** the full-page message is showing, the backend is up again, and I press Try again
- **THEN** the app continues at once rather than waiting for the next retry

#### Scenario: A fast first load shows no loading text
- **WHEN** I load the app and the backend answers the first request within the loading indicator's delay
- **THEN** no "Loading…" message is drawn before the app appears

#### Scenario: Backend down after the app has loaded
- **WHEN** the app has loaded successfully and the backend later becomes unreachable
- **THEN** the notice bar is shown and the app stays on the page I was using

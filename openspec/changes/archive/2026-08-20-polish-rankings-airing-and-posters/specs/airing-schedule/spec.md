## ADDED Requirements

### Requirement: Today is marked in the schedule's day headers
When the displayed week contains the viewer's current local date, the schedule SHALL mark that one day-column's header as today, so the current day can be found at a glance without reading the seven dates.

The mark SHALL live in the day-column header at the top of the grid — the row already carrying the weekday name and `<day>.<month>` — and SHALL distinguish that header from the other six by more than a single hue, so it survives a theme switch and is not the only cue available to a viewer who cannot distinguish the accent colour from the header's ordinary colour.

The marked header SHALL keep showing exactly what every other header shows: the same weekday name and the same unpadded `<day>.<month>`, in the same position. Marking today SHALL NOT change the header's height, the column's width, or the position of any slot beneath it, so the seven columns stay aligned and no other column moves when the week changes.

The marked column SHALL carry `aria-current="date"` so assistive technology announces it as the current date rather than relying on the visual treatment alone.

Exactly one column SHALL ever be marked. A displayed week that does not contain the current local date SHALL mark no column at all — including a past or future week that shares a weekday with today.

Which date counts as today SHALL be the viewer's local date, determined the same way the page's `current` control determines the week to jump to, so the marked column and that control can never disagree about which day today is.

#### Scenario: The current week marks today
- **WHEN** the week containing the current local date is displayed
- **THEN** exactly one day-column's header — the one whose local date is today — is visually distinguished from the other six and carries `aria-current="date"`

#### Scenario: Another week marks nothing
- **WHEN** I navigate to the previous or next week
- **THEN** no day-column header is marked as today, even though one of them falls on the same weekday as today

#### Scenario: The mark does not disturb the grid
- **WHEN** I compare the marked column with its neighbours
- **THEN** its header shows the same weekday name and `<day>.<month>` as any other column, the columns are the same width, and the slots beneath each header start at the same height

#### Scenario: Returning to the current week re-marks today
- **WHEN** I navigate away to another week and then use the `current` control
- **THEN** today's column is marked again, in the column matching the same local date the `current` control navigated to

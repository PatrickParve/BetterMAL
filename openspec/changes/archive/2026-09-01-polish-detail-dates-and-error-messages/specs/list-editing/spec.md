## MODIFIED Requirements

### Requirement: Start and finish dates are editable in the editor
The system SHALL expose the entry's start date and finish date in the entry editor behind a collapsed "Dates" disclosure, so the fields are available without adding permanent height to the form. Expanding the disclosure SHALL reveal both date fields pre-filled with the entry's stored dates, each of which SHALL be settable to a date or cleared to empty.

Each date field SHALL be composed of three dropdowns — year, month, and day — rather than the browser's own date control, so the field looks and behaves the same in every browser rather than following each browser's native date widget. The day dropdown SHALL offer exactly the days the selected month and year hold, including 29 February in a leap year, and SHALL NOT offer a day that month does not have. Each dropdown SHALL offer a blank option alongside its values.

Clearing SHALL be possible from the field itself and SHALL be reachable in one action: selecting the blank option in any of a field's three dropdowns SHALL clear that whole date, and each field SHALL additionally offer a clear control that empties it outright. A field whose date is already empty SHALL show every dropdown blank, and SHALL NOT pre-select today or any other value on the user's behalf. Each field SHALL additionally offer a control that sets it to today's date in one action.

A date SHALL be considered set only when all three of its parts are chosen; a partly filled field SHALL be treated as empty rather than as an error, and SHALL be sent as no date at all.

Neither date SHALL be settable to a day later than today. The dropdowns SHALL only ever offer choices that keep the field at or before today — the year list SHALL NOT reach past the current year, the month list SHALL NOT reach past the current month once the current year is chosen, and the day list SHALL NOT reach past today's day once the current year and month are both chosen — rather than offering every choice and rejecting an out-of-range result afterward. The server SHALL independently reject a start or finish date later than today on any request that sets one, so the rule holds even if a future date reaches the API by some other path than this field.

The two dates SHALL be independent: a start date with no finish date, and a finish date with no start date, SHALL each be valid and SHALL each save. Only when both are set SHALL their order be checked, and a finish date on the **same day** as the start date SHALL be accepted. The system SHALL reject a finish date earlier than the entry's start date, reporting the problem rather than saving. The check SHALL be made against the dates the entry would hold after the edit — so a finish date set earlier than a start date left untouched from a previous edit is caught too, not only two dates changed together. A date that is unchanged SHALL NOT be sent as an edit.

A changed date SHALL go through the same entry-edit path as every other field, so it is logged as activity and queued for MAL sync with the entry's other values.

#### Scenario: Revealing the date fields
- **WHEN** I open the entry editor and expand the Dates disclosure
- **THEN** start-date and finish-date fields appear, each as a year, month, and day dropdown, pre-filled with the entry's stored dates

#### Scenario: Dates collapsed by default
- **WHEN** the entry editor opens
- **THEN** the date fields are hidden behind the collapsed Dates disclosure

#### Scenario: The fields look the same in every browser
- **WHEN** I open the Dates disclosure in Safari on macOS and in another browser
- **THEN** both show the same three dropdowns, with no browser's own date widget involved

#### Scenario: Setting a date
- **WHEN** I set a start or finish date and save
- **THEN** the date is stored on the entry, logged as activity, and queued for MAL sync

#### Scenario: Clearing a date from its clear control
- **WHEN** I use a date field's clear control on a field that had a value, and save
- **THEN** the entry's date is emptied and the change is logged and queued for sync

#### Scenario: Clearing a date from a dropdown
- **WHEN** I select the blank option in the month dropdown of a filled date field
- **THEN** the whole field reads as empty, and saving empties the entry's date

#### Scenario: Days follow the month
- **WHEN** I select February 2024 and then February 2023
- **THEN** the day dropdown offers 29 days for 2024 and 28 for 2023

#### Scenario: A finish date without a start date
- **WHEN** the entry has no start date, and I set only a finish date and save
- **THEN** the finish date is stored and the save is not rejected

#### Scenario: A start date without a finish date
- **WHEN** the entry has no finish date, and I set only a start date and save
- **THEN** the start date is stored and the save is not rejected

#### Scenario: Same-day start and finish
- **WHEN** I set the start date and the finish date to the same day and save
- **THEN** the save succeeds

#### Scenario: Finish before start rejected
- **WHEN** I enter a finish date earlier than the start date and save
- **THEN** the save is rejected with an explanation naming the problem, and neither date is changed

#### Scenario: Finish before a start date I did not touch
- **WHEN** the entry already has a start date, and I set a finish date earlier than it and save
- **THEN** the save is rejected with the same explanation, and neither date is changed

#### Scenario: Setting a date to today in one action
- **WHEN** I use a date field's "Today" control
- **THEN** all three of its dropdowns are set to today's year, month, and day

#### Scenario: The dropdowns never offer a future date
- **WHEN** I open a date field and select the current year
- **THEN** the month dropdown offers no month later than the current one, and once the current month is also selected, the day dropdown offers no day later than today's

#### Scenario: A future date is rejected even if it reaches the server
- **WHEN** a request sets a start or finish date later than today
- **THEN** the save is rejected with an explanation naming the problem, and neither date is changed

### Requirement: Entry editor controls share one size
The system SHALL render every control in the entry editor — dropdowns and text/number inputs alike — at the same width and the same height, so the form reads as a single aligned column rather than a mix of control sizes. Controls added to the editor later SHALL inherit the same sizing rather than defining their own.

A date field, being three dropdowns and a clear control rather than one input, SHALL as a whole occupy the same width and the same height as a single control of the form, its parts sharing that width between them on one line. Its dropdowns SHALL be the same height as every other control, so the Dates disclosure adds no new control size to the form.

#### Scenario: Dropdowns match inputs
- **WHEN** the entry editor is open
- **THEN** its status and score dropdowns are the same width and height as its episodes-watched and rewatch-count inputs

#### Scenario: Date fields match the rest
- **WHEN** the Dates disclosure is expanded
- **THEN** each date field spans the same width and stands at the same height as every other control in the editor, with its three dropdowns sharing that width on one line

## ADDED Requirements

### Requirement: A rejected edit reports the reason it was rejected
When the server rejects an entry edit and states why, the system SHALL show that reason to the user in place of a generic failure message, so a rejection the user can act on — a finish date before a start date, a status that anime cannot take, a score on an anime that has aired nothing — reads as what it is rather than as "something went wrong".

The reason SHALL be reported wherever the edit was made: in the entry editor for a save made there, and through the app-wide failure notice the `action-failure-notices` capability defines for an edit made from a control outside it. A rejection that carries no stated reason, and a failure that is not a rejection at all (the server unreachable, an unexpected error), SHALL fall back to the existing generic message.

The reasons the server states SHALL be written for the reader: they SHALL NOT carry an anime's numeric id, a parameter name, an exception type, or any other internal detail, and SHALL read as a sentence naming what was refused and why.

A rejected edit SHALL leave the form as it was, with the user's values intact, so the problem can be corrected and the save retried without re-entering anything.

#### Scenario: A date-order rejection reads as one
- **WHEN** I save a finish date earlier than the entry's start date
- **THEN** the editor reports that the finish date cannot be earlier than the start date, rather than "Could not save changes. Please try again."

#### Scenario: The reason carries no internal detail
- **WHEN** any rejection message is shown
- **THEN** it contains no anime id, parameter name, or exception type

#### Scenario: An unexplained failure keeps the generic message
- **WHEN** a save fails without the server stating a reason
- **THEN** the editor shows its existing generic failure message

#### Scenario: The form survives a rejection
- **WHEN** a save is rejected
- **THEN** every value I had entered is still in the form, and I can correct the problem and save again

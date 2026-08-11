## ADDED Requirements

### Requirement: A truncated title reveals itself on hover
Wherever the system truncates an anime title for display — cut off at one line, at a fixed number of lines, or with an ellipsis — the full title SHALL be recoverable by pointing at it. Hovering the truncated title SHALL show the full title in a small box positioned below the pointer, which SHALL follow the pointer while it remains over the title and SHALL disappear when the pointer leaves.

The box SHALL NOT overlap the truncated title's own box, even when the title spans multiple lines and the pointer is near its top edge — the tooltip is meant to reveal text the title is hiding, not sit on top of text the title is already showing.

The tooltip SHALL appear only for titles that are actually truncated: a title that fits in full SHALL show no tooltip. Where this tooltip is used, the browser's own native title tooltip SHALL NOT also be shown, so the same text never appears twice.

The tooltip SHALL be presentation only: it SHALL NOT intercept pointer events, so it never blocks a click on the row or tile beneath it.

#### Scenario: Hovering a cut-off title
- **WHEN** I point at a title that has been cut off
- **THEN** a small box appears below the pointer showing the full title

#### Scenario: The tooltip follows the pointer
- **WHEN** I move the pointer across a truncated title
- **THEN** the box tracks the pointer, staying below it

#### Scenario: Tooltip clears a multi-line title
- **WHEN** I hover near the top of a title that is truncated across two lines
- **THEN** the tooltip appears below the whole title, not overlapping either of its lines

#### Scenario: Leaving the title
- **WHEN** I move the pointer off the title
- **THEN** the box disappears

#### Scenario: A title that fits
- **WHEN** I point at a title that is displayed in full
- **THEN** no tooltip appears

#### Scenario: No duplicate native tooltip
- **WHEN** I rest the pointer on a truncated title long enough for the browser's own tooltip to appear
- **THEN** only the app's tooltip is shown, not a second native one

#### Scenario: The tooltip does not block clicking
- **WHEN** the tooltip is visible beneath the pointer and I click
- **THEN** the click reaches the row or tile under the pointer

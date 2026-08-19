## ADDED Requirements

### Requirement: The search field highlights on hover
The system SHALL highlight the navbar search field while the pointer is over it, using the same highlight the field shows when it has focus, so the field responds to the pointer as every other navigation control in the app does.

The highlight SHALL stay within the field's own bounds, exactly as the focus indication does — it SHALL NOT be drawn outside the field, SHALL NOT overlap or sit on top of the magnifier, and SHALL NOT change the field's size or position, so hovering the navbar never reflows it. Hovering a field that already has focus SHALL look the same as focusing it, rather than compounding the two into a heavier treatment. The magnifier SHALL keep its own hover state within the highlighted field.

#### Scenario: Hovering the field
- **WHEN** I move the pointer over the navbar search field
- **THEN** the field shows the same highlight it shows when focused

#### Scenario: The highlight stays inside the field
- **WHEN** the field is highlighted by hover
- **THEN** no part of the highlight touches or covers the magnifier, and the field neither grows nor moves

#### Scenario: Hovering a focused field
- **WHEN** I move the pointer over the field while typing in it
- **THEN** it looks as it does when focused, without a second, heavier ring

#### Scenario: The magnifier still responds
- **WHEN** I move the pointer from the field onto the magnifier inside it
- **THEN** the magnifier shows its own hover state as before, within the highlighted field

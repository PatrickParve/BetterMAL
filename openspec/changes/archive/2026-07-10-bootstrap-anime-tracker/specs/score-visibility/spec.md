## ADDED Requirements

### Requirement: Global MAL-score hide toggle
The system SHALL provide a global toggle that, when hidden, replaces every MAL score everywhere with a blur that does not leak the underlying value.

#### Scenario: Hiding all scores
- **WHEN** I turn the hide toggle on
- **THEN** every MAL score across the app is blurred and the actual value is not exposed in the rendered output

### Requirement: Per-score in-place reveal
The system SHALL give each blurred score its own small unhide control that reveals just that one score in place.

#### Scenario: Revealing a single score
- **WHEN** I use a blurred score's unhide control
- **THEN** only that one score is revealed in place while the others remain blurred

### Requirement: Reveal is not persisted
The system SHALL re-hide any individually revealed score when navigating away, keeping the unhidden state non-persistent.

#### Scenario: Navigating away re-hides
- **WHEN** I reveal a score and then navigate away and back
- **THEN** that score is blurred again

## MODIFIED Requirements

### Requirement: Global MAL-score hide toggle
The system SHALL provide a global toggle that, when hidden, replaces every MAL score everywhere with a blur that does not leak the underlying value. The toggle's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`), so a user who hides scores does not see them reappear on refresh.

#### Scenario: Hiding all scores
- **WHEN** I turn the hide toggle on
- **THEN** every MAL score across the app is blurred and the actual value is not exposed in the rendered output

#### Scenario: Hidden state survives a reload
- **WHEN** I turn the hide toggle on and then reload the page or open the app in a new tab
- **THEN** scores are still hidden without my having to toggle again

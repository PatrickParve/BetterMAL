## MODIFIED Requirements

### Requirement: Score controls match the score they set
The system SHALL apply the "mine" colour role to the controls that set my score — the inline score control on a My List row, the score control in the entry editor, and the score dropdown in the completion score prompt — so that setting a score looks like the score it sets. Those controls SHALL remain standard form controls with unchanged keyboard, pointer, and mobile behaviour.

A control showing no score SHALL render neutrally rather than in the "mine" colour, since there is no score to colour.

#### Scenario: Setting a score looks like the score
- **WHEN** I look at the score control on a My List row for an anime I have scored
- **THEN** the control carries the same purple role as the score itself

#### Scenario: The completion prompt's dropdown follows the chosen score
- **WHEN** the completion prompt opens with "No score" pre-selected and I then pick 8
- **THEN** the dropdown renders neutrally while it reads "No score" and carries the purple role once it reads 8

#### Scenario: An unscored entry's control is neutral
- **WHEN** I look at the score control for an anime I have not scored
- **THEN** the control renders neutrally rather than in the score colour

#### Scenario: The control still behaves like a form control
- **WHEN** I open and use the score control by keyboard or by pointer
- **THEN** it behaves exactly as it did before, including on mobile

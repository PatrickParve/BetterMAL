## MODIFIED Requirements

### Requirement: Reveal is not persisted
The system SHALL re-hide any individually revealed score when navigating away, keeping the unhidden state non-persistent.

Re-hiding SHALL NOT depend on how the client happens to reuse page components across a navigation. A revealed score SHALL be hidden again whenever the page being viewed changes, including a navigation **between two pages of the same kind** — one anime's detail page to another's, or one series' page to another's — where the client keeps the same page component mounted and only its parameter changes. A reveal SHALL therefore never be carried onto a page the user did not reveal it on, in either direction of travel: forward to a new anime, or back to one visited earlier.

Turning the global hide toggle back on SHALL also drop every individual reveal, so a score revealed before the toggle was switched off is hidden again when it is switched on, rather than staying visible. This keeps the hide toggle's own guarantee — that while it is on, every MAL score is replaced by its reveal control — true at the moment it is switched on and not only for scores nobody had revealed.

A reveal SHALL NOT be restored by a back or forward navigation, and SHALL NOT survive a reload.

#### Scenario: Navigating away re-hides
- **WHEN** I reveal a score and then navigate away and back
- **THEN** that score is hidden again

#### Scenario: A reveal does not follow me to another anime
- **WHEN** the hide toggle is on, I open one anime's detail page, navigate to a second anime's detail page and reveal its MAL score, and then go back to the first anime
- **THEN** the first anime's MAL score shows its reveal control, as it did before I left it

#### Scenario: A reveal does not travel forward either
- **WHEN** the hide toggle is on, I reveal a score on one anime's detail page and then open a second anime from that page
- **THEN** the second anime's MAL score shows its reveal control rather than its value

#### Scenario: A reveal does not survive a reload
- **WHEN** I reveal a score and then reload the page
- **THEN** that score shows its reveal control again

#### Scenario: Turning hiding back on drops a reveal
- **WHEN** the hide toggle is on, I reveal one score, switch the toggle off so every score is shown, and then switch it on again without leaving the page
- **THEN** every MAL score on the page shows its reveal control, including the one I had revealed

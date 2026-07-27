## MODIFIED Requirements

### Requirement: Always-show-completed-scores setting
The system SHALL provide a setting on the Settings page, "Always show MAL scores for completed shows", that when enabled reveals the MAL score of any anime the user has marked **Completed** in full — no blur and no per-score reveal control — even while the global hide-scores toggle is on. The setting SHALL default to off, SHALL be independent of the global hide toggle (it never changes the global toggle's state), and SHALL apply only to scores belonging to completed entries; every other MAL score continues to follow the global hide/unhide behavior. The setting SHALL apply to every MAL score the app renders, wherever it appears — including the profile page's "They liked it, I didn't" and "I liked it, they didn't" lists — so any view rendering a MAL score SHALL know whether that score's entry is completed. The setting's on/off state SHALL persist across page reloads and new tabs (client-side, e.g. `localStorage`).

#### Scenario: Completed scores shown while hiding is on
- **WHEN** the global hide toggle is on and I enable "Always show MAL scores for completed shows"
- **THEN** the MAL score of every anime I have completed is shown in full with no blur and no reveal control, while all non-completed scores remain blurred

#### Scenario: Completed scores shown in the opinion-divergence lists
- **WHEN** the global hide toggle is on, "Always show MAL scores for completed shows" is enabled, and a completed anime appears in "They liked it, I didn't" or "I liked it, they didn't"
- **THEN** that row's MAL score is shown in full with no blur and no reveal control, while rows for entries I have not completed stay blurred

#### Scenario: Setting off keeps completed scores hidden
- **WHEN** the global hide toggle is on and "Always show MAL scores for completed shows" is off
- **THEN** completed anime's MAL scores are blurred with a reveal control, exactly like every other score

#### Scenario: Setting has no effect while scores are globally visible
- **WHEN** the global hide toggle is off
- **THEN** all MAL scores are shown regardless of the "Always show MAL scores for completed shows" setting

#### Scenario: Setting survives a reload
- **WHEN** I enable "Always show MAL scores for completed shows" and then reload the page or open the app in a new tab
- **THEN** the setting is still enabled without my having to toggle it again

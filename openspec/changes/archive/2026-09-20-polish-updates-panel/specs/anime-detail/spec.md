## MODIFIED Requirements

### Requirement: Prequel and sequel controls are always present
The system SHALL show a prequel control and a sequel control in the top-right corner of the detail page for **every** anime, whether or not such a relation exists, so the related-links row keeps one shape from anime to anime and a control never moves out from under the pointer between two pages.

A control whose relation exists SHALL be a link to that related anime's detail page, exactly as it is today. A control whose relation does not exist SHALL be rendered in a visibly dimmed, inactive treatment: it SHALL NOT be clickable, SHALL NOT navigate anywhere, SHALL NOT be reachable by keyboard as an interactive control, and SHALL be reported as disabled to assistive technology. It SHALL occupy the same position and the same size as its enabled form, so no other control in the row shifts when a relation is absent.

The dimmed treatment SHALL be distinguishable at a glance from the enabled one, and a disabled control SHALL NOT take the hover treatment its enabled form takes.

An enabled control SHALL name its target on hover, and SHALL name it by the title the app displays for that anime — its English title where one is known, its MyAnimeList title otherwise — the same title choice every other surface makes, so hovering a control does not name the anime it leads to differently from the page it leads to.

The relations these controls are drawn from SHALL be the anime's full two-directional relation set — the edges it stores itself plus the inverted edges other anime store pointing at it — so a prequel or sequel that MyAnimeList recorded only on the other side still gets an enabled control here. An anime that is the target of another anime's `sequel` relation SHALL therefore show an enabled prequel control for it, whether or not it stores a `prequel` relation of its own.

When more than one candidate exists for a control, the target SHALL be the one the ranked resolution rules select, **not** the first that MyAnimeList reports; the remainder SHALL be reachable through the More overlay. An edge an external source contradicts SHALL NOT be given an enabled control, and its control SHALL be shown dimmed as though the relation were absent.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding control(s) are enabled and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel, in either direction
- **THEN** both controls are still shown, each dimmed and not clickable

#### Scenario: One of the two is absent
- **WHEN** an anime has a sequel but no prequel
- **THEN** the sequel control is enabled and the prequel control is shown dimmed in its usual place, with the sequel control in the same position it occupies on an anime that has both

#### Scenario: A dimmed control does nothing
- **WHEN** I click, tab to, or hover a dimmed prequel control
- **THEN** nothing is navigated to, no hover treatment is applied, and assistive technology reports the control as disabled

#### Scenario: An enabled control names its target in English
- **WHEN** I hover the sequel control of an anime whose sequel is cached with the English title "Reincarnated as a Sword Season 2"
- **THEN** it names that anime in English rather than in romaji

#### Scenario: A target with no English title
- **WHEN** the anime a control leads to has no English title in our cache
- **THEN** the control names it by its stored MyAnimeList title

#### Scenario: A prequel MAL only recorded on the other side
- **WHEN** I open the detail page of an anime that stores no prequel relation, but which another anime names as its sequel
- **THEN** an enabled prequel control is shown linking to that other anime

#### Scenario: Multiple prequels
- **WHEN** an anime has two prequels
- **THEN** the prequel control links to the one the ranked resolution selects and the other is listed in the More overlay

#### Scenario: A contradicted edge gets no enabled control
- **WHEN** an anime's only sequel candidate is an edge an external source contradicts
- **THEN** the sequel control is shown dimmed, and the entry remains listed in the More overlay

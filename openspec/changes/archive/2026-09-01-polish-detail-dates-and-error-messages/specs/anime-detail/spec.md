## RENAMED Requirements

- FROM: `### Requirement: Prequel/sequel links when they exist`
- TO: `### Requirement: Prequel and sequel controls are always present`

## MODIFIED Requirements

### Requirement: Prequel and sequel controls are always present
The system SHALL show a prequel control and a sequel control in the top-right corner of the detail page for **every** anime, whether or not such a relation exists, so the related-links row keeps one shape from anime to anime and a control never moves out from under the pointer between two pages.

A control whose relation exists SHALL be a link to that related anime's detail page, exactly as it is today. A control whose relation does not exist SHALL be rendered in a visibly dimmed, inactive treatment: it SHALL NOT be clickable, SHALL NOT navigate anywhere, SHALL NOT be reachable by keyboard as an interactive control, and SHALL be reported as disabled to assistive technology. It SHALL occupy the same position and the same size as its enabled form, so no other control in the row shifts when a relation is absent.

The dimmed treatment SHALL be distinguishable at a glance from the enabled one, and a disabled control SHALL NOT take the hover treatment its enabled form takes.

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

#### Scenario: A prequel MAL only recorded on the other side
- **WHEN** I open the detail page of an anime that stores no prequel relation, but which another anime names as its sequel
- **THEN** an enabled prequel control is shown linking to that other anime

#### Scenario: Multiple prequels
- **WHEN** an anime has two prequels
- **THEN** the prequel control links to the one the ranked resolution selects and the other is listed in the More overlay

#### Scenario: A contradicted edge gets no enabled control
- **WHEN** an anime's only sequel candidate is an edge an external source contradicts
- **THEN** the sequel control is shown dimmed, and the entry remains listed in the More overlay

## ADDED Requirements

### Requirement: Related-anime rows show English titles
Every related-anime row in the More overlay SHALL show the related anime's English title when one is known, falling back to its stored MyAnimeList title when none is — the same title choice every other surface in the app makes, so the same anime is not named one way on a card and another way in this list.

The English title SHALL be served with the related-anime data the detail page already loads, resolved from the app's own cached metadata for that anime. No additional request SHALL be made to learn it, and a related anime the cache holds no metadata for SHALL simply show its stored title.

The rows' grouping, ordering, media-type line, navigation, and hide-scores behaviour SHALL be unchanged.

#### Scenario: A related anime with an English title
- **WHEN** I open the More overlay of an anime whose side story is cached with the English title "Sword Art Online: Extra Edition"
- **THEN** that row is titled in English rather than in romaji

#### Scenario: A related anime with no English title
- **WHEN** a related anime has no English title in our cache
- **THEN** its row shows the title MyAnimeList stored for the relation

#### Scenario: No extra request for titles
- **WHEN** I open the detail page of an anime with many related entries and press More
- **THEN** every row is already titled, with no additional request made for any related anime

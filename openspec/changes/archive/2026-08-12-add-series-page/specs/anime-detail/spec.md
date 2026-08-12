## ADDED Requirements

### Requirement: Series link in the relations row
The detail page's relations row SHALL include a **Series** link to the series page for the anime being viewed, whenever that anime has at least one stored relation of a story type (`sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version`). The link SHALL be decided from the relation data the page already loads, without an extra request to check whether a series exists.

An anime whose only relations are non-story ones (e.g. `alternative_setting`, `character`, `other`) SHALL show no Series link. The link SHALL be present on main-line entries and side entries alike, so every anime that belongs to a series can reach it.

#### Scenario: Series link on a season
- **WHEN** I open the detail page of an anime that has a sequel relation
- **THEN** the relations row shows a "Series" link, and following it opens that anime's series page

#### Scenario: Series link on a special
- **WHEN** I open the detail page of a special linked to its parent story
- **THEN** the relations row shows a "Series" link to the same series its parent story belongs to

#### Scenario: No series link without story relations
- **WHEN** I open the detail page of a standalone anime whose only relations are `character` or `other`
- **THEN** no "Series" link is shown

#### Scenario: Existing relation buttons are unaffected
- **WHEN** an anime has prequel, sequel, parent-story, and other relations
- **THEN** the Prequel, Sequel, Main series, and More controls behave exactly as before, with the Series link added alongside them

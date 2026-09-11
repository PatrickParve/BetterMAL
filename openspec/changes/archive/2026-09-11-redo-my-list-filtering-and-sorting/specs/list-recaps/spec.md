## MODIFIED Requirements

### Requirement: Scoping my list to a period from my list
My list SHALL offer a period control that narrows the list in place to the anime of a chosen period, rather than navigating away to the recap page. Choosing a period SHALL apply that period's included set — the same set the recap of that period would cover, under the same period mode, time filter, and media type — as a scope on the list, leaving every other list filter, sort, and grouping control untouched and still usable on top of it.

The control SHALL sit in my list's page header, beside the page title, set apart from both the status filter tabs and the filter/sort controls, per the `library-views` capability's "Recap a period control on my list". The applied scope SHALL be shown as a dismissible indicator naming the period, the time filter, and the media type — a compact chip in my list's results line rather than a banner — and dismissing it SHALL return the list to its unscoped contents.

The scope indicator SHALL offer a link to the full recap of that same period, filter, and media type, so the recap page remains one step away from a scoped list.

#### Scenario: Choosing a period scopes the list
- **WHEN** I choose fall 2019 from my list's period control
- **THEN** my list narrows to the anime that recap includes, and I stay on my list

#### Scenario: The period control sits in the page header
- **WHEN** my list is shown
- **THEN** the period control is presented in the page header beside the title, not in the status-tab row and not among the filter or sort controls

#### Scenario: List controls still apply over a scope
- **WHEN** a period scope is applied and I then filter to Completed and sort by score
- **THEN** the list shows the completed anime of that period ordered by score

#### Scenario: Dismissing the scope
- **WHEN** I dismiss the scope indicator
- **THEN** my list returns to its full contents with my other filters unchanged

#### Scenario: Opening the recap from a scoped list
- **WHEN** my list is scoped to fall 2019 narrowed to TV and I follow the scope indicator's recap link
- **THEN** the recap page opens on fall 2019 with the same media-type narrowing

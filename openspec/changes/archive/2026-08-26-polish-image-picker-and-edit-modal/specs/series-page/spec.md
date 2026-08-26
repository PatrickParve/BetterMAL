## RENAMED Requirements

- FROM: `### Requirement: The series page header carries picture and title controls`
- TO: `### Requirement: The series page carries picture and title controls beside Rebuild`

## MODIFIED Requirements

### Requirement: The series page carries picture and title controls beside Rebuild
The series page SHALL carry a **Choose picture** control and a **Choose title** control for the series it is showing.

Both SHALL sit in the **Series stats** heading row, alongside the **Rebuild** control, and SHALL be presented as that row's controls are rather than as links. They SHALL NOT sit in the page's header block among the external links to MyAnimeList, AniList and SeriesGraph — a control that changes what the series is SHALL be grouped with the other such control rather than with links that navigate away. The header block SHALL therefore hold the picture, title, status, year span and external links only, and nothing SHALL be left in its place where the two controls were.

**Rebuild** SHALL keep its position at the end of that group, adjacent to the partial- and truncated-series notices that qualify it, so those notices still read as belonging to it.

**Choose picture** SHALL be rendered only when the series has more than one picture to choose between, and SHALL open the series picture picker the `artwork-selection` capability defines. A series with a single picture available SHALL show no control at all, rather than a control that opens onto one image. Its absence SHALL NOT disturb the position of the remaining controls in the row.

**Choose title** SHALL always be rendered, since a title can always be trimmed even when only one is offered. It SHALL open a picker listing every title offered by the `series-identity` capability, together with a text field for a trimmed title, and SHALL refuse to submit a title that capability's rule rejects.

Both controls SHALL affect the series only. Neither SHALL change any member anime's own picture or title, and neither SHALL cause anything to be written to MyAnimeList.

Choosing a picture or a title SHALL take effect on the page without a reload, and SHALL be reflected on every other surface that shows this series the next time it is read.

#### Scenario: A multi-picture series offers the control
- **WHEN** I open a series whose main-line members between them offer six distinct pictures
- **THEN** the Series stats row shows a "Choose picture" control, and clicking it opens a picker of those six pictures

#### Scenario: A single-picture series shows no picture control
- **WHEN** I open a series whose main-line members offer exactly one distinct picture between them
- **THEN** no "Choose picture" control is shown, and "Choose title" and "Rebuild" still sit together in the Series stats row

#### Scenario: The title control is always available
- **WHEN** I open any series
- **THEN** the Series stats row shows a "Choose title" control

#### Scenario: The controls sit with Rebuild, not with the links
- **WHEN** I open any series
- **THEN** "Choose picture" and "Choose title" appear in the Series stats heading row next to "Rebuild", and the page header block below the title shows only the MyAnimeList, AniList and SeriesGraph links

#### Scenario: Rebuild keeps its notices
- **WHEN** I open a series that could not be built in full
- **THEN** the "Some entries couldn't be loaded yet" notice still reads alongside the Rebuild control in that same row

#### Scenario: A chosen picture applies immediately
- **WHEN** I pick a picture from the series picker
- **THEN** the header's picture changes to it without a page reload

#### Scenario: Choosing does not touch the members
- **WHEN** I choose a picture for a series
- **THEN** no main-line member's own displayed picture changes, and nothing is pushed to MyAnimeList

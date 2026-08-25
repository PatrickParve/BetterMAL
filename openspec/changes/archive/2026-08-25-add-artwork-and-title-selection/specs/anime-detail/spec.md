## ADDED Requirements

### Requirement: The detail page offers a picture choice for a my-list anime
The anime detail page SHALL carry a **Choose picture** control for an anime that is in my list and has more than one picture available, and SHALL NOT carry it otherwise.

The control SHALL sit in the page's action row, alongside the existing refresh action, rather than over the artwork itself — the artwork is rendered at its own proportions and an overlaid control would cover it.

The control SHALL open the picture picker the `artwork-selection` capability defines, over the anime's option set. Choosing SHALL update the page's own artwork without a reload.

The page SHALL render its artwork from the anime's displayed picture, so a chosen picture is what the detail page shows, and the existing portrait and landscape artwork rules SHALL apply to it unchanged — a chosen picture that is wider than it is tall SHALL be shown whole at landscape proportions exactly as a MAL main picture of the same shape would be.

#### Scenario: A my-list anime with several pictures
- **WHEN** I open the detail page of an anime in my list whose picture set holds four pictures
- **THEN** the action row shows a "Choose picture" control beside the refresh action

#### Scenario: An anime not in my list
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** no "Choose picture" control is shown

#### Scenario: A my-list anime with one picture
- **WHEN** I open the detail page of an anime in my list for which MAL publishes exactly one picture
- **THEN** no "Choose picture" control is shown

#### Scenario: Choosing updates the page
- **WHEN** I choose a picture from the picker
- **THEN** the page's artwork changes to it without a reload

#### Scenario: A landscape choice is shown whole
- **WHEN** I choose a picture that is wider than it is tall
- **THEN** it is shown at landscape proportions and is not cropped, exactly as a landscape main picture would be

### Requirement: The detail response carries the anime's pictures and asks for a backfill when needed
The detail read SHALL return, for the anime it describes: its displayed picture, MAL's own main picture, and its stored picture set.

When the anime is in my list and its picture set has never been fetched, the response SHALL carry a flag saying so, and the client SHALL make one follow-up request that fetches the set and returns it. The detail read itself SHALL NOT block on that fetch.

When a staleness-triggered full-detail fetch runs as part of the same read and the anime is in my list, the picture set SHALL arrive with it and the flag SHALL NOT be set.

The flag SHALL NOT be set for an anime that is not in my list, since no fetch is due for one.

#### Scenario: A pending backfill is flagged
- **WHEN** I open the detail page of a my-list anime whose full detail is fresh and whose picture set has never been fetched
- **THEN** the response is flagged as needing a picture fetch, and the client makes one follow-up request for it

#### Scenario: A fresh fetch carries the pictures
- **WHEN** opening the detail page triggers a full-detail fetch for a my-list anime
- **THEN** the picture set arrives with it and no follow-up request is made

#### Scenario: A non-list anime is never flagged
- **WHEN** I open the detail page of an anime that is not in my list
- **THEN** the response carries no picture-fetch flag

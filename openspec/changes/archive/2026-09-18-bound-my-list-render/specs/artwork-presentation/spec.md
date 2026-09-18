## ADDED Requirements

### Requirement: Row pictures are requested as they come into view

Every surface listed as a row picture slot SHALL let the browser defer a picture's request until that picture is at or near the viewport, so opening a page holding many rows does not issue a request for every picture at once. The deferral SHALL be the browser's own native lazy loading, asked for on the picture element itself; the system SHALL NOT track scroll position or manage a request queue of its own to achieve it.

Deferral SHALL change only *when* a picture is requested, never which picture is drawn or how. A deferred picture SHALL take the whole-picture treatment on load exactly as an immediately requested one does, so "Orientation is read from the picture, not fetched" holds unchanged: the slot shows the portrait box until the picture arrives, whenever it arrives, and a picture the browser already holds is still drawn wide immediately.

#### Scenario: A long list does not request every picture at once
- **WHEN** I open a page holding far more rows than fit on screen
- **THEN** the pictures near the viewport are requested and those far below it are not requested yet

#### Scenario: Scrolling requests the rest
- **WHEN** I scroll a row further down that list into view
- **THEN** its picture is requested and drawn

#### Scenario: A deferred picture still takes the wide treatment
- **WHEN** a row scrolled into view holds landscape artwork
- **THEN** its slot adopts the whole-picture treatment once that picture loads, exactly as a row visible from the start does

#### Scenario: Held pictures are unaffected
- **WHEN** I return to a page whose pictures the browser already holds
- **THEN** those pictures are drawn immediately, wide artwork included, with no wait introduced by the deferral

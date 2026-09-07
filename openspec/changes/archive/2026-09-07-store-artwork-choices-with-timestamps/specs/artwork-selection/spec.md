## ADDED Requirements

### Requirement: Every presentation choice carries the time it was made
The system SHALL store, beside each of the three presentation choices — an anime's chosen picture, a series' chosen title, and a series' chosen picture — the time that choice last changed.

The time SHALL be recorded when a choice is set and when it is cleared alike. A clearing is a change, and it SHALL be possible to recognise it as the later of two conflicting versions of the same choice.

The time SHALL be the moment the change was made, taken from the act that makes it. Nothing that later reads, copies, exports, or rebuilds a choice SHALL restamp it: a choice made three weeks ago SHALL still report a three-week-old time. No MyAnimeList sync of any kind SHALL write one of these times.

A recorded time SHALL NOT be the marker of whether a choice exists. A time stored beside no chosen value SHALL mean a choice that was cleared at that time; no time at all SHALL mean that no choice has ever been made or cleared for that anime or series.

Setting a choice to the value it already holds SHALL record the time of that write, since it is a write.

These times exist so that the same choice made on two devices can be ordered against each other. No surface of the app SHALL be required to display them.

#### Scenario: Setting records the time
- **WHEN** I choose a picture for an anime
- **THEN** the anime's chosen picture and the time I chose it are both stored

#### Scenario: Clearing records the time
- **WHEN** I clear a series' chosen picture
- **THEN** no chosen picture is stored for it and the time I cleared it is recorded

#### Scenario: A choice keeps its own age
- **WHEN** a picture chosen three weeks ago is read, rendered, or carried through a series rebuild today
- **THEN** its recorded time is still the three-week-old one

#### Scenario: A MAL sync does not stamp anything
- **WHEN** an anime with a chosen picture is refreshed from MAL
- **THEN** its chosen picture's recorded time is unchanged

#### Scenario: Never chosen has no time
- **WHEN** an anime has never had a picture chosen or cleared
- **THEN** no time is recorded for its picture choice

#### Scenario: Choosing the same picture again
- **WHEN** I choose the picture an anime already has chosen
- **THEN** the recorded time becomes the time of that write

### Requirement: Picking MAL's own picture pins it, and clearing is an act of its own
The picker SHALL present MAL's main picture among the options. Choosing it SHALL store it as the chosen picture exactly as choosing any other option does, so that anime stops following MAL's main picture from then on: a later MAL change of main picture SHALL NOT move that anime's displayed picture.

Clearing SHALL be a separate act from choosing, and the only way to remove a choice. It SHALL remove the chosen picture so that the anime follows MAL's main picture again, and SHALL record the time.

A choice SHALL NOT be re-validated away by this rule any more than by any other: a chosen picture that happens to be MAL's own current main picture SHALL remain stored as a choice until it is cleared.

The same SHALL hold for a series' chosen picture, whose default is its root member's MAL picture: choosing the picture the series already shows by default SHALL store it as a choice, and only clearing SHALL return the series to following its root's MAL picture — including when the root anime itself carries its own chosen picture, which the series default SHALL NOT follow.

#### Scenario: Picking the default stores it
- **WHEN** I pick the picture MAL calls an anime's main picture
- **THEN** that URL is stored as the anime's chosen picture, and the anime is recorded as having a chosen picture

#### Scenario: A pinned picture survives a MAL picture change
- **WHEN** MAL changes the main picture of an anime whose chosen picture is the main picture MAL published before
- **THEN** the anime still displays the picture I chose, and MAL's new main picture is recorded behind it

#### Scenario: Clearing returns the anime to MAL
- **WHEN** I clear an anime's chosen picture
- **THEN** no chosen picture is stored for it, its displayed picture becomes MAL's main picture, and it follows MAL from then on

#### Scenario: A series choice that matches its root
- **WHEN** a series shows its root member's MAL picture by default and I pick that same picture in the series picker
- **THEN** it is stored as the series' own chosen picture, and choosing a different picture for the root anime no longer changes the series' picture

#### Scenario: A series default ignores the root anime's own choice
- **WHEN** a series has no chosen picture of its own, and its root anime has a chosen picture that differs from MAL's main picture for that anime
- **THEN** the series displays the root anime's MAL picture, not the root anime's chosen picture

## MODIFIED Requirements

### Requirement: An anime's picture set is stored, not just its main picture
The system SHALL store, per anime, the full set of picture URLs MyAnimeList publishes for it, alongside MAL's own main picture, the picture chosen for it, and the picture the app displays.

Four distinct values SHALL be kept per anime:

- **MAL's main picture** — whatever MAL reports as `main_picture`, always overwritten by any MAL sync.
- **The chosen picture** — the picture I have chosen for that anime. Its default SHALL be nothing at all, which SHALL mean "no choice; follow MAL".
- **The displayed picture** — the picture every surface in the app renders for that anime. It SHALL be derived from the two values above: the chosen picture where there is one, MAL's main picture otherwise. Every write of either value it derives from SHALL re-derive it in the same write, so the three can never disagree.
- **The picture set** — every URL MAL returns under `pictures` for that anime, in MAL's order.

The system SHALL also record **when the picture set was last fetched**, as a value distinct from "when full detail was last fetched". An anime whose picture set has never been fetched SHALL be distinguishable from one that has been fetched and has exactly one picture, so the latter is never re-fetched on every visit.

An anime SHALL be treated as having a chosen picture exactly when a chosen picture is stored for it. Chosen-ness SHALL NOT be inferred by comparing the displayed picture with MAL's main picture: those two hold the same URL both when MAL's own picture was chosen deliberately and when nothing was chosen at all, and those are different states. Nor is such a comparison meaningful beyond the one database that made both writes, since another device may have cached MAL's picture at another time.

#### Scenario: Storing a picture set
- **WHEN** the system fetches an anime whose MAL record lists six pictures
- **THEN** all six URLs are stored in MAL's order, and the fetch timestamp is recorded

#### Scenario: One picture is not the same as none
- **WHEN** an anime's picture set has been fetched and MAL publishes exactly one picture for it
- **THEN** the anime is recorded as fetched, and it is not fetched again until its next full-detail refresh

#### Scenario: Nothing is chosen by default
- **WHEN** an anime's picture set has been fetched and no picture has been chosen
- **THEN** no chosen picture is stored for it and its displayed picture equals MAL's main picture

#### Scenario: A chosen picture that is MAL's own
- **WHEN** I choose the picture MAL calls an anime's main picture
- **THEN** that URL is stored as the chosen picture and the anime is recorded as having one, even though its displayed picture equals MAL's

### Requirement: The picture picker
The system SHALL offer a picture picker as an overlay showing every option as an image, with the current selection marked. Clicking an option SHALL set it and close the overlay.

Every option SHALL be shown **whole, at its own proportions**. No part of any option SHALL be cropped away, and no option SHALL be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider than a portrait option beside it, and a square picture SHALL be drawn square. Two options that differ only near their edges SHALL therefore be distinguishable in the picker, which is the whole point of showing them side by side.

Options SHALL be laid out to a **common height**, each option's width following from its own proportions at that height, so that a set mixing orientations still forms tidy rows rather than a ragged field. That height SHALL be smaller than the picture the page behind the overlay displays for the same anime or series, so that a large option set shows more of itself at once and a picker of a franchise's whole pool is not mostly below the fold.

An option whose proportions are so wide that it cannot fit the width available to a row SHALL be reduced to fit whole within that width rather than being cropped or forced to overflow the overlay.

The whole of an option — its full drawn area, whatever its shape — SHALL be the control that chooses it, and the current selection's marking SHALL be legible on options of every shape.

The overlay SHALL behave as the app's other overlays do — dismissable with Escape and by clicking outside it, locking the page behind it from scrolling, and closing when the page it was opened over is navigated away from.

The control that opens the picker SHALL be rendered **only when there is more than one option**, so a picker never opens onto a single image and an anime with several pictures never lacks the control.

The picker SHALL offer a control that **clears the choice**, rendered only when a choice is stored, so that the presence of the control is itself the sign that one is. Its wording SHALL name the default that clearing returns to, which differs by surface: MAL's own main picture for an anime, and the root member's MAL picture for a series. Using it SHALL clear the choice and close the overlay, as choosing an option sets and closes.

These presentation rules SHALL hold for the anime picker and the series picker alike.

#### Scenario: Opening and choosing
- **WHEN** I open the picker and click a picture
- **THEN** that picture becomes the anime's picture and the overlay closes

#### Scenario: The current selection is marked
- **WHEN** I open the picker for an anime that has a chosen picture
- **THEN** that picture is marked as the current selection

#### Scenario: One option, no control
- **WHEN** an anime's option set holds exactly one picture
- **THEN** no control to open the picker is rendered

#### Scenario: Dismissing without choosing
- **WHEN** I press Escape or click outside the overlay
- **THEN** it closes and the anime's picture is unchanged

#### Scenario: Clearing from the picker
- **WHEN** I open the picker for an anime with a chosen picture and use the control that clears it
- **THEN** the choice is removed, the overlay closes, and the anime displays MAL's main picture again

#### Scenario: No choice, no clear control
- **WHEN** I open the picker for an anime that has no chosen picture
- **THEN** no clear control is rendered

#### Scenario: The series picker names its own default
- **WHEN** I open the picker for a series with a chosen picture
- **THEN** its clear control names the root anime's MAL picture as what clearing returns to, and using it makes the series follow its root member's MAL picture again

#### Scenario: A landscape option is shown landscape
- **WHEN** I open the picker on an option set containing a picture wider than it is tall
- **THEN** that option is drawn as a wide, short image at its own proportions — wider than the portrait options beside it — rather than as a centre-cropped portrait slice of it

#### Scenario: Nothing is cut off
- **WHEN** I open the picker on an option set containing two pictures that differ only near their left and right edges
- **THEN** both are shown whole and the difference between them is visible in the picker

#### Scenario: Mixed orientations line up
- **WHEN** an option set holds both portrait and landscape pictures
- **THEN** every option is drawn to the same height, differing only in width, and the options sit in even rows

#### Scenario: Options are smaller than the picture on the page
- **WHEN** I open the picker from a page showing that anime's or series' picture
- **THEN** each option is drawn shorter than that picture, so more options fit in the overlay at once

#### Scenario: An extremely wide option still fits
- **WHEN** an option is so wide that at the common height it would be wider than the overlay's row
- **THEN** it is reduced to fit whole within that width, and the overlay does not scroll sideways

#### Scenario: The series picker looks the same
- **WHEN** I open the series picture picker on a pool drawn from several main-line members
- **THEN** its options follow the same rules — shown whole, at their own proportions, to a common height

## REMOVED Requirements

### Requirement: Choosing MAL's own picture clears the choice
**Reason**: The rule existed because chosen-ness was inferred from the displayed picture differing from MAL's main picture, which made "pick the default" and "reset" the same state by construction. With the choice stored outright, that identity is gone and the rule is actively harmful: it discards the fact that I deliberately chose the picture MAL happens to publish, which is exactly the fact another device cannot reconstruct.

**Migration**: Replaced by "Picking MAL's own picture pins it, and clearing is an act of its own". Picking MAL's main picture now stores it as a choice; clearing is reached through the picker's own clear control or the existing reset endpoint. No stored data is reinterpreted: every anime whose displayed picture differs from MAL's keeps that URL as its chosen picture, and every other anime keeps no choice at all.

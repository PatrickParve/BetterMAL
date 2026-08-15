## ADDED Requirements

### Requirement: Filter and sort controls in one cluster share one height
The system SHALL render every control that sits side by side in a page's filter/sort cluster at the same height, so the cluster reads as one row of controls rather than a ragged line. This SHALL hold regardless of the control's underlying element — a native `<select>`, a popover trigger button, a checkbox label, a text input, and a toggle button in the same cluster SHALL all be the same height — and SHALL hold at every font size the app renders, since the root font size is fluid.

The shared height SHALL come from one shared definition rather than from per-control padding values that each happen to land near the same number, so a control added later inherits the cluster's height instead of re-introducing the mismatch. Equal padding is not sufficient on its own: a `<select>`'s inner line box is normalized by the browser while a button or label inherits the root's computed line-height, so identical padding yields different rendered heights.

This SHALL cover the Seasonal Anime page's season, year, and sort selects together with its Type filter and its "In my list" checkbox; the Search results page's Type filter and sort select; and My List's filter bar, whose Type and Airing filters use the same shared control as the other two pages. Round, icon-only navigation arrows (the season and week steppers) are their own control family and are not part of this requirement.

Aligning heights SHALL NOT change any control's behaviour, its accessible name, its keyboard handling, or where its popover panel is anchored.

#### Scenario: Season page filter row is flush
- **WHEN** I open the Seasonal Anime page
- **THEN** the season, year, and sort selects, the Type filter button, and the "In my list" checkbox are all exactly the same height, with their tops and bottoms aligned

#### Scenario: Search page filter row is flush
- **WHEN** I open the Search results page with results showing
- **THEN** its Type filter button and sort select are exactly the same height

#### Scenario: My List filter bar stays consistent
- **WHEN** I open My List
- **THEN** the find-in-list input, the Type and Airing filter buttons, and the sort/score selects beside them are all the same height as one another

#### Scenario: Height holds as the font scales
- **WHEN** the viewport width changes enough to move the app's fluid root font size
- **THEN** the controls in each cluster still match one another's height

#### Scenario: Filters keep working
- **WHEN** I open the Type filter popover after the heights are aligned
- **THEN** it opens anchored beneath its button as before, and selecting, clearing, and closing it behave exactly as they did

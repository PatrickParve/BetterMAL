## ADDED Requirements

### Requirement: The updates control is marked while its panel is open

The navbar's updates control SHALL carry the same treatment a navbar page control carries while its page is being viewed — a tinted background together with a border stronger than the one hovering produces — for as long as what it opened is on screen. This SHALL hold while its dropdown is open, and SHALL continue to hold while the updates history overlay opened from that dropdown is open, so the control that produced what is on screen stays visibly the source of it.

Hovering the control while it is marked SHALL NOT replace or weaken the mark, exactly as hovering the current page's navbar link does not.

Closing the dropdown, or closing the history overlay, SHALL clear the mark.

This mark SHALL be independent of the unseen-news indicator: the control SHALL be markable with or without the indicator showing, and the indicator's own rules SHALL be unaffected. Because opening the dropdown clears the indicator, the ordinary case is a marked control with no indicator.

#### Scenario: Opening the dropdown marks the control
- **WHEN** I open the updates dropdown
- **THEN** the updates control shows the same tinted background and border a navbar link shows for the page I am on

#### Scenario: The mark survives following History
- **WHEN** I open the dropdown and follow **History** to the updates history overlay
- **THEN** the updates control stays marked for as long as that overlay is open

#### Scenario: Closing clears the mark
- **WHEN** I close the dropdown, or close the history overlay
- **THEN** the updates control returns to its unmarked appearance

#### Scenario: Hovering the marked control keeps it marked
- **WHEN** I move the pointer over the updates control while its dropdown is open
- **THEN** it keeps its stronger mark rather than falling back to the hover border

#### Scenario: The mark and the indicator are independent
- **WHEN** I open the dropdown while the unseen indicator is showing
- **THEN** the control becomes marked and the indicator clears on its own existing rules, neither state being derived from the other

## MODIFIED Requirements

### Requirement: View-control selections are restored with the page
A page's view controls — filter tabs, media-type filters, sort selections, section filters, per-section expand/collapse states, and the like — SHALL be captured as part of that page's restorable state and SHALL be restored to their previously selected values when the page is restored.

A control that holds several selections at once, or a selection per section of the page, SHALL be restored whole rather than reduced to a single value.

On a fresh visit those same controls SHALL open on their documented defaults. A page SHALL NOT persist view-control selections across visits by any other mechanism.

A restored selection that names something the restored page no longer renders — a group that has since disappeared, an option no longer offered — SHALL be ignored rather than treated as an error.

#### Scenario: Filter survives a back navigation
- **WHEN** I select a non-default filter on a page, open an anime from it, and then go back
- **THEN** that filter is still selected and the page shows the filtered contents

#### Scenario: Two independent controls both restore
- **WHEN** a page has two independent view controls and I change both before navigating away
- **THEN** going back restores both to the values I selected

#### Scenario: A multi-selection restores whole
- **WHEN** I select several values in one multi-select control, navigate away, and go back
- **THEN** every one of those values is still selected

#### Scenario: A per-section state restores
- **WHEN** I collapse one section of a page and expand another, navigate away, and go back
- **THEN** each section is in the state I left it in

#### Scenario: A fresh visit opens on defaults
- **WHEN** I select a non-default filter, then reach that page again by clicking its navbar link
- **THEN** the filter is back on its default

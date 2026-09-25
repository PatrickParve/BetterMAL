## MODIFIED Requirements

### Requirement: The Series page states which empty situation it is in
The system SHALL distinguish, on the Series page, between having no series to list and still loading, and SHALL NOT present either as an error.

When no series is stored at all, or none has a member in my list, the page SHALL say that series are still being discovered from my list and SHALL link to the Settings page's "Build all series from my list" action — the same situation and the same remedy the profile page's Top series section already names.

While the list is loading the page SHALL show the `page-load-states` capability's loading presentation rather than the empty message: nothing for that capability's delay, then a loading indicator.

When the list cannot be loaded, the page SHALL say so rather than showing the empty message, since "nothing here" and "this did not load" are different facts. It SHALL do so with the `page-load-states` capability's failure state, including Try again and the automatic retry when the server is reachable again.

The page SHALL choose between these states and the grid from its read and from the whole list after the current filters, never from the cards its progressive reveal has drawn so far. It SHALL say that no series match the selected filters only when its read has settled, the list holds series, and the current filters exclude every one of them.

#### Scenario: Nothing built yet
- **WHEN** I open the Series page before any series has been built
- **THEN** it says series are still being discovered from my list and links to the Settings page's build action

#### Scenario: Loading is not emptiness
- **WHEN** the Series page's list is still loading
- **THEN** no empty message is shown, and a loading indicator is shown if the load outlasts the delay

#### Scenario: A failed load is not emptiness
- **WHEN** the Series page's list fails to load
- **THEN** the page says it could not be loaded and offers Try again, rather than saying there are no series

#### Scenario: Opening the page with no filters never claims a filter mismatch
- **WHEN** I open the Series page with no filter set and it holds series
- **THEN** "No series match the selected filters" is never shown, not even for a moment before the cards appear

#### Scenario: Filters that exclude everything
- **WHEN** the list has loaded and the filters I selected exclude every series in it
- **THEN** the page says no series match the selected filters

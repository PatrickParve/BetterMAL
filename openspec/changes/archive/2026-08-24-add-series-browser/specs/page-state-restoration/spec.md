## MODIFIED Requirements

### Requirement: Restoration applies to every routed page
Every routed page in the application SHALL participate in restoration — Home, Season, Year, Top, Airing, My list, Series, Profile, Settings, Search, and anime detail — so that navigating back from any page to any other page behaves the same way.

A page whose content depends on a route parameter or query string SHALL key its restorable state by that parameter, so returning to one anime's detail page does not restore another anime's data.

A page that renders only part of a loaded list, revealing more as it is scrolled, SHALL restore how much of that list had been revealed as well as the list itself — otherwise its recorded scroll position is unreachable on restore and is clamped back to the top.

#### Scenario: Any pair of pages
- **WHEN** I navigate from any page to any other page and then go back
- **THEN** the page I return to is restored, with no page behaving differently from the rest

#### Scenario: Parameterised pages do not cross-contaminate
- **WHEN** I open one anime's detail page, then another's, and then go back
- **THEN** the first anime's detail page is restored with that anime's data, not the second's

#### Scenario: The year browser restores the year it was left on
- **WHEN** I browse one year, open an anime, then go back, and later repeat this on a different year
- **THEN** each return restores the year it was left on with that year's results, rather than the current year or the other year's data

#### Scenario: Search results restore with their query
- **WHEN** I run a search, open a result, and go back
- **THEN** the query text and its results are both restored

#### Scenario: The Series page restores how far it had been revealed
- **WHEN** I scroll the Series page well past its first screen of cards, open a series, and go back
- **THEN** the same number of cards is rendered again and the page is returned to the scroll position I left it at, rather than being clamped back to the top

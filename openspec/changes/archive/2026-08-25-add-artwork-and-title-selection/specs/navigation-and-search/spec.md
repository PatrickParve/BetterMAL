## MODIFIED Requirements

### Requirement: Series appear in search results
The system SHALL match stored series against a search query alongside individual anime, in both the type-ahead dropdown and the full search results page.

A series SHALL match when ANY of its members' title or English title matches the query under the same rules applied to anime titles — starts-with, contains, or (for a double-quoted query) exact equality. A series that has been given a chosen title SHALL additionally match on that title under the same rules, so a trimmed title no member title begins with is still findable.

A matched series SHALL be presented using the identity the `series-identity` capability resolves — its chosen title and chosen picture where it has them, and its ROOT member's title and displayed picture otherwise, the same identity the series page itself uses — regardless of which member matched. A chosen title SHALL NOT narrow what the series matches on: every member title still matches.

Each matched series SHALL take the strongest match quality of any of its members (exact ahead of prefix, prefix ahead of contains) and, as a tie-break, the best popularity rank among its matching members. Matched series SHALL be listed AHEAD of anime results.

A series SHALL be matched only from stored series — searching SHALL NOT trigger a live MAL fetch or a synchronous series build.

#### Scenario: Searching a franchise name surfaces its series
- **WHEN** I search for "attack on titan" and that series is stored
- **THEN** the Attack on Titan series appears in the results, above the individual anime entries, showing the series' resolved title and picture

#### Scenario: Matching on a later entry's title
- **WHEN** I search for text that appears only in a non-root member's title (e.g. "final season")
- **THEN** the series still matches, and is shown under its resolved identity rather than the matching member's title and picture

#### Scenario: A renamed series still matches its members
- **WHEN** a series titled "Beyblade" has a member titled "Beyblade: Metal Fusion" and I search for "Metal Fusion"
- **THEN** the series matches and is listed as "Beyblade"

#### Scenario: A renamed series matches its own title
- **WHEN** a series has been given a chosen title and I search for it
- **THEN** the series matches on that title

#### Scenario: Exact-match query and series
- **WHEN** I wrap a query in double quotes
- **THEN** a series matches only when one of its members' title or English title, or its chosen title, exactly equals the quoted text

#### Scenario: Search never blocks on building a series
- **WHEN** I search for a franchise whose series has never been built
- **THEN** the results return at the usual speed with anime matches only, rather than waiting for a series to be built

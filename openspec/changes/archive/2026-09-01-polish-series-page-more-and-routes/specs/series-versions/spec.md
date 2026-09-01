## MODIFIED Requirements

### Requirement: Alternative versions on a main line share one watch-order position
Where two or more **main-line** entries of a series are alternative versions of one another, they SHALL form a **version slot**: one position in the watch order holding all of them, with a control that picks which is shown.

A version slot SHALL be a connected group, of at least two members, over the version relations among that series' main-line entries. Each member of a slot is one of its **alternatives**.

For each alternative, the system SHALL derive its **branch**: the main-line entries whose shortest path to that alternative, over `sequel`/`prequel` relations with every other alternative of every slot removed from the graph, is strictly shorter than to any other alternative. A main-line entry that is not an alternative and is not strictly nearest to one — because it ties between two or more, or reaches none — SHALL be **trunk**.

The watch order SHALL show every trunk entry, the picked alternative of each slot, and the entries of that alternative's branch. Entries of an unpicked branch SHALL NOT be shown while it is unpicked. Exactly the entries shown SHALL form one continuous sequence, with no gap left where an unpicked branch's entries would have been.

The watch order SHALL be a topological ordering as the `series-page` capability defines, computed with each slot contracted to a single position, so a branch entry that precedes its alternative in story order is ordered before the slot rather than after it.

**The picker SHALL be rendered above the first card of the route it selects** — the earliest card in the displayed watch order that is either the picked alternative itself or one of that alternative's branch — rather than inside a card. Where a branch holds an entry that precedes its alternative in story order, the picker SHALL therefore sit above *that* entry's card, since that card is where the route begins; where the branch begins at the alternative, the picker SHALL sit above the alternative's own card.

The picker SHALL NOT change any card's size or push one card out of line with its neighbours: the cards of a series that has a slot SHALL stay aligned with one another exactly as the cards of a series without one are, so the picker's strip SHALL take reserved space above the row rather than displacing the card beneath it. A series with no slot SHALL reserve no such space.

Each of the picker's buttons SHALL name its alternative and SHALL state which one is currently picked.

Picking an alternative SHALL change only what the page shows and the figures the `series-page` capability scopes to the picked route. It SHALL NOT rebuild the series, SHALL NOT change any anime's membership, and SHALL NOT change the series' root, title or picture.

A series whose main line contains no version slot SHALL render exactly as it does today, with no picker.

#### Scenario: One position, two ways to watch it
- **WHEN** I open Demon Slayer, whose Mugen Ressha movie and Mugen Ressha TV arc are alternative versions and both main line
- **THEN** they share one position with a picker, and the rest of the franchise reads as a single continuous watch order around it

#### Scenario: The picker sits above the alternative when the route starts there
- **WHEN** I open Demon Slayer, whose Mugen Ressha alternatives have no branch entry before them
- **THEN** the movie/TV buttons are rendered above the Mugen Ressha card, not inside it

#### Scenario: The picker sits above the route's first card
- **WHEN** I open Fate/stay night, where the Unlimited Blade Works prologue is in the UBW branch and is ordered before the slot
- **THEN** the route buttons are rendered above the prologue's card — the first card of that route — rather than above the Unlimited Blade Works card

#### Scenario: The picker does not knock the cards out of line
- **WHEN** I open a series whose main line holds a version slot
- **THEN** every main-line card's top edge is aligned with every other card's, the picker occupying reserved space above the row

#### Scenario: The picker follows the route it selects
- **WHEN** I switch from a route whose first card is a prologue to one whose first card is the alternative itself
- **THEN** the buttons move to sit above the newly picked route's first card

#### Scenario: A route is shown through to its last sequel
- **WHEN** I open Fate/stay night and pick the Unlimited Blade Works television route
- **THEN** the watch order shows Fate/Zero, Fate/Zero 2nd Season, the Unlimited Blade Works prologue, Unlimited Blade Works and its second season, and shows no Heaven's Feel film

#### Scenario: Switching routes switches the tail
- **WHEN** I then pick Heaven's Feel
- **THEN** the three Heaven's Feel films are shown in order and the Unlimited Blade Works entries are not

#### Scenario: A prologue is ordered before the slot it belongs to
- **WHEN** the Unlimited Blade Works prologue is a prequel of Unlimited Blade Works and is in its branch
- **THEN** it is shown before the slot's position, not after it

#### Scenario: Shared continuations stay visible under every pick
- **WHEN** Demon Slayer's later arcs are equidistant from both Mugen Ressha entries
- **THEN** they are trunk and are shown whichever alternative is picked

#### Scenario: The shown entries form one continuous sequence
- **WHEN** a route with two entries is picked over a route with four
- **THEN** exactly the visible entries are shown in watch order, with no gap where the unpicked route's entries would have been

#### Scenario: A folded-in alternative version makes no slot
- **WHEN** Clannad Movie is a version-neighbour extra rather than a main-line entry
- **THEN** the Clannad series' main line has no version slot and no picker

#### Scenario: A series without alternatives is unaffected
- **WHEN** no two main-line entries of a series are alternative versions of one another
- **THEN** the watch order is a plain list of cards with no picker and no reserved picker space above the row

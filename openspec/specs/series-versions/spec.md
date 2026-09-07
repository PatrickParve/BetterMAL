# series-versions Specification

## Purpose
TBD - created by archiving change split-series-by-version. Update Purpose after archive.

## Requirements

### Requirement: A series is a story component
The system SHALL derive a series as the connected component of the stored related-anime graph over **story relations alone** — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, and the companion-media `other` case the `series-page` capability defines. Two anime joined by any chain of those relations SHALL be members of the same series, however many version relations their entries also carry.

`alternative_version` and `alternative_setting` — wherever this capability says *version relation*, it means either of them — SHALL NOT split a component and SHALL NOT merge two components. They SHALL determine only which further anime a series shows in its More section, where a More tile links, and which of a main line's entries share a watch-order position.

Two anime joined by nothing but a version relation SHALL therefore belong to two different series, each with its own root, main line, watch order, title, picture, page and browser card.

Whether an anime carries a version relation SHALL have no bearing on which series it belongs to. In particular a franchise SHALL NOT be split because two of its entries each carry a version relation of their own.

#### Scenario: A season and its sequel stay in one series
- **WHEN** Clannad and Clannad: After Story are joined by `sequel`/`prequel`, and each additionally carries an `alternative_setting` relation to a special of its own
- **THEN** both are members of one series, with After Story a main-line entry rather than an extra

#### Scenario: Two retellings with no story path are two series
- **WHEN** Fullmetal Alchemist (2003) and Fullmetal Alchemist: Brotherhood are joined only by `alternative_version`, each with its own films and specials
- **THEN** two series are stored, one rooted on each

#### Scenario: A retelling on the same chain does not fork the franchise
- **WHEN** Demon Slayer's Mugen Ressha movie and Mugen Ressha TV arc are `alternative_version` of each other and both sit on the franchise's `sequel` chain
- **THEN** one series is stored holding the whole franchise, not two near-identical ones

#### Scenario: Routes that share a prequel are one series
- **WHEN** Fate/Zero 2nd Season declares `sequel` relations to Fate/stay night, the Unlimited Blade Works film, the Unlimited Blade Works prologue and Heaven's Feel I, and those four are `alternative_version` of one another
- **THEN** all of them, with Fate/Zero and each route's own sequels, are members of one series

#### Scenario: An alternative setting with its own story is its own series
- **WHEN** a franchise carries an `alternative_setting` relation to a spin-off franchise that has its own sequels and specials
- **THEN** the spin-off is a separate series and is not merged into the first

#### Scenario: An ordinary franchise is unchanged
- **WHEN** a component carries no version relation at all
- **THEN** one series is stored, with the root, main line and watch order it has today

### Requirement: A version neighbour folds in as an extra or opens its own series
A **version neighbour** SHALL be an anime linked to a member of a series by a version relation, in either direction, that is not itself a member of that series' story component.

Each version neighbour SHALL be classified once, at build time, and recorded on its membership:

- A neighbour with a cached metadata row and **no story relation of its own** SHALL be a member of this series, an extra, whose tile opens that anime's **detail page**. Its story component is itself alone, so it can never be a series.
- A neighbour with a cached metadata row that **does carry story relations of its own** SHALL be a member of this series, an extra, whose tile opens **its own series page**.
- A neighbour with **no cached metadata row at all** SHALL NOT be a member — a member requires a metadata row — and SHALL be shown as a related entry rendered from the stored relation row, whose tile opens that anime's detail page. It SHALL become a member on a later build once its row is cached.

The classification SHALL be decided from cached relation rows only and SHALL cost no fetch.

A version neighbour SHALL NOT be traversed through: nothing reachable only from a version neighbour SHALL be admitted to this series. A version neighbour SHALL NEVER be a main-line entry of the series that holds it.

A version neighbour that is a member SHALL be counted exactly as any other extra is — in the all-member averages, the member count, the extras figures and the browser card's figures.

#### Scenario: A lone alternative version is an ordinary extra
- **WHEN** Clannad Movie's only relations are `alternative_version` to Clannad and to Clannad: After Story
- **THEN** it is an extra of the Clannad series, its tile opens its detail page, and no separate series is stored for it

#### Scenario: A retelling with its own content links out
- **WHEN** Brotherhood is a version neighbour of the Fullmetal Alchemist (2003) series and carries `side_story` and `spin_off` relations of its own
- **THEN** it is an extra of the 2003 series whose tile opens Brotherhood's own series page

#### Scenario: A neighbour's own entries stay off this page
- **WHEN** the 2003 series holds Brotherhood as a version neighbour
- **THEN** Brotherhood's specials, its 4-koma shorts and The Sacred Star of Milos are not members of the 2003 series

#### Scenario: A neighbour extra counts like any other
- **WHEN** the 2003 series holds Brotherhood as a version-neighbour extra
- **THEN** Brotherhood is included in that series' all-member averages and its member count

#### Scenario: An uncached neighbour is shown without being stored
- **WHEN** a member carries an `alternative_setting` relation to an anime with no cached metadata row
- **THEN** that anime is shown as a related entry from the stored relation row, is not a member, and costs no fetch

#### Scenario: The classification costs nothing
- **WHEN** a series has eight version neighbours, all with cached rows
- **THEN** all eight are classified with no MyAnimeList request

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

### Requirement: The alternative a version slot opens on
The system SHALL choose one alternative of each version slot as its **default**, applying these rules in order and stopping at the first that separates the candidates:

1. The branch across which I have watched the most episodes; where two branches tie, the one in which I have more entries marked Completed or Rewatching. This rule SHALL be skipped when I have watched nothing in any branch.
2. The alternative with the higher MAL score. Where the two best alternatives' MAL scores differ by **0.25 or less**, the more popular of the two — the lower popularity rank — SHALL win. An alternative with no MAL score SHALL sort after every alternative that has one.
3. The earliest aired-from date, then the lower MAL id.

The default SHALL be reported with the slot, so a page rendered without a stored preference opens on it.

My own pick SHALL override the default and SHALL be remembered per series as part of the series page's restorable view state, as the `series-page` capability defines.

#### Scenario: The route I have watched opens
- **WHEN** I have completed Demon Slayer's Mugen Train movie and none of the TV arc
- **THEN** the slot opens on the movie

#### Scenario: The other route opens when that is the one I watched
- **WHEN** I have completed the Mugen Ressha TV arc and not the movie
- **THEN** the slot opens on the TV arc

#### Scenario: Popularity settles a close score
- **WHEN** nothing in any branch is in my list and the two best alternatives score 8.19 and 8.11
- **THEN** the slot opens on whichever of those two has the lower popularity rank

#### Scenario: A clearly better route wins outright
- **WHEN** nothing is in my list and one alternative scores 8.60 against another's 8.30
- **THEN** the slot opens on the 8.60 alternative, whatever their popularity ranks

#### Scenario: Unscored alternatives sort last
- **WHEN** one alternative has no MAL score and another does, and neither is in my list
- **THEN** the slot opens on the scored one

#### Scenario: My pick is remembered
- **WHEN** I pick a route, open one of its entries, and navigate back
- **THEN** the same route is still picked

### Requirement: A build derives and stores only the seed's series
A build SHALL traverse and persist the story component of the anime it was seeded from, and SHALL NOT persist any other series.

Traversal SHALL run in two phases. The first SHALL exhaust the seed's story component over story relations, spending the build's fetch and probe budgets there. The second SHALL follow each member's version relations one hop to find that component's version neighbours, classifying them from cached rows without spending any budget, and SHALL NOT expand beyond them.

A build whose first phase yields a single anime that has version neighbours SHALL instead build the story component of the version neighbour with the largest story component, folding the seeded anime into it as a version neighbour, so opening a lone alternative version reaches the series it belongs to rather than reporting no series. Where such a seed has no version neighbour either, the build SHALL report that the anime belongs to no series, as it does today.

A neighbouring series SHALL be stored when it is itself visited, bulk-built, or enqueued by a relation discovery. A tile that opens a neighbouring series SHALL therefore always resolve: the series read endpoint builds a series for an anime that has none.

Series identity across a rebuild SHALL be resolved by matching the derived component to the stored series it overlaps most; that series SHALL keep its members, its chosen title and its chosen picture, and SHALL be stored under the identifier the derived root gives it — unchanged where the root did not move. Every other stored series overlapping the component SHALL be deleted, and SHALL surrender its chosen title and picture to the survivor where the survivor has none, exactly as an absorbed series does today.

Where the surviving series' identifier changes, the change SHALL take effect together with the deletion of the stored series holding that identifier, so no build leaves a franchise unstored or two series claiming one identifier.

Overlap and deletion SHALL be computed over **story-component members only**, never over version neighbours, so a series that holds a neighbouring series' root as an extra can never delete that neighbour's stored series.

#### Scenario: A build stores one series
- **WHEN** a build is seeded from an entry of Fate/stay night
- **THEN** that franchise's series is stored, and Prisma☆Illya's, Fate/Grand Order's and Fate/Apocrypha's are not

#### Scenario: A neighbouring series is built on demand
- **WHEN** I activate a tile that opens a version neighbour's series and no series is stored for it
- **THEN** its series is built by that visit and its page renders

#### Scenario: A lone alternative version resolves to its franchise
- **WHEN** I open the series page for Clannad Movie, whose only relations are version relations
- **THEN** the Clannad series is built and shown, with the Movie among its extras

#### Scenario: Fragments left by the previous rules are absorbed
- **WHEN** Clannad, Clannad: After Story and Clannad Movie are stored as three separate series and any of them is next read
- **THEN** one series remains, keeping the members and choices of whichever of the three overlapped the component most, stored under the merged component's root, and the other two are deleted

#### Scenario: A neighbour's series is not deleted
- **WHEN** the Fullmetal Alchemist (2003) series is rebuilt while holding Brotherhood as a version-neighbour extra
- **THEN** Brotherhood's own stored series is left intact

#### Scenario: A rebuild does not shuffle identity
- **WHEN** a series is rebuilt with no membership change
- **THEN** it keeps the identifier it had, since its root is unchanged

#### Scenario: A survivor takes an absorbed series' identifier
- **WHEN** a rebuild's component is rooted at an entry belonging to a series it absorbs rather than to the survivor
- **THEN** the survivor is stored under that root's identifier, keeping its own chosen title and picture, and the absorbed series is gone

#### Scenario: Favourite ranks survive a re-derivation
- **WHEN** a series holding ordered favourites is re-derived under these rules
- **THEN** each member keeps its rank in the series it remains a member of

### Requirement: An anime may belong to more than one series, with one of them primary
The system SHALL permit an anime to be a member of more than one series, recording its membership per series — whether it is main line there, its position there, its relation group there, its version slot and branch there, and its favourite rank there.

Story components are disjoint, so an anime SHALL hold at most one **story-component** membership. It MAY additionally hold **version-neighbour** memberships, since a single anime can be an alternative version of one franchise and an alternative setting of another.

An anime SHALL appear at most once on any single series page.

Exactly one of an anime's memberships SHALL be its **primary** series, resolved by rule rather than by distance:

- A story-component membership SHALL always be primary.
- A membership as a version neighbour that opens its own series SHALL never be primary; that anime's own series is its home, built on demand.
- A membership as a folded-in version neighbour SHALL be primary when the anime holds no story-component membership anywhere, resolved between two such memberships in favour of the larger component and then the lower root MAL id.

Every surface that asks for *the* series an anime belongs to — the detail page's series link, search's series lookup, the artwork and picture services, and the relation-confidence fallback — SHALL resolve to the primary series. A question about whether two anime belong to the same series SHALL be answered by whether they share **any** series.

#### Scenario: A folded-in neighbour shared by two franchises
- **WHEN** Fate/Prototype is an `alternative_setting` of Fate/stay night and an `alternative_version` of Fate/strange Fake, with no story relations of its own
- **THEN** it is an extra of both series, and exactly one of the two memberships is primary

#### Scenario: The detail page links to the primary
- **WHEN** I open the detail page of an anime that is a member of two series
- **THEN** its series link opens its primary series

#### Scenario: A story-component membership wins
- **WHEN** an anime is a member of its own franchise's component and also a version-neighbour extra of another series
- **THEN** its own franchise's series is primary

#### Scenario: A neighbour that opens its own series is not primary here
- **WHEN** Brotherhood is a version-neighbour extra of the Fullmetal Alchemist (2003) series
- **THEN** that membership is not primary, and Brotherhood's detail page does not link to the 2003 series

#### Scenario: Same-series questions accept any shared series
- **WHEN** two anime are both members of one series and one of them is also a member of another
- **THEN** they are treated as being in the same series

#### Scenario: No duplicate tiles
- **WHEN** a series page renders an anime that is a member of it and also related to its main line by a further relation
- **THEN** that anime is rendered exactly once

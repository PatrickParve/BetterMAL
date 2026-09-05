## MODIFIED Requirements

### Requirement: Main line and extras
Within a series the system SHALL identify a main line: the connected chain over `sequel`/`prequel` relations among the members holding the most main-line-eligible members — ties broken in favour of the chain containing the earliest-aired eligible member — reduced to just its eligible members. Every other member of the series SHALL be an extra.

Main-line classification SHALL run over the series' own **story-component** members. A version neighbour SHALL never be main line, and SHALL never bridge two chains, since it is not part of the component the chains are formed over.

A member SHALL be main-line-eligible unless it is any of:
- a member whose media type is `special`, `music`, or `pv`;
- a recap of another member — a `summary`/`full_story` relation to it;
- side content of another member — an outgoing `parent_story` relation to another member, or an incoming `side_story` relation from another member.

A promotional video is never a chapter of a story, so a `pv` member SHALL never be main line however MAL relates it — including in a franchise whose real entries carry no `sequel`/`prequel` relations at all, where a chain of promos could otherwise out-rank the show.

Chains SHALL be formed over every member, eligible or not, and ranked afterwards by their eligible members only. Excluding an ineligible member from the ranking SHALL NOT split the chain it sits in, so a recap or side entry that bridges two seasons still keeps those seasons in one chain while never being main line itself.

When any chain holds an eligible member whose media type is `tv`, only such chains SHALL be ranked. A long-running show with no separately-listed seasons is a one-node chain, and without this restriction a handful of side movies that chain to each other could out-count it; when no chain holds an eligible `tv` member — a movie-only or ONA-only franchise — every chain SHALL be ranked.

Because eligibility, not raw chain size, decides the ranking, a franchise whose members carry no `sequel`/`prequel` relations at all — every member its own one-node chain — SHALL still resolve to its actual show rather than to whichever promotional short happens to have aired first.

A recap or side-content tag overrides a sequel/prequel edge on the same member: MAL routinely gives a recap special both a `summary`/`full_story` relation to the season it recaps and a `sequel`/`prequel` relation bridging it to the next season, and often types it `tv_special` rather than `special` — the media-type filter alone would not catch it, so the explicit tags are checked independently.

Where no member of the series is eligible at all, the system SHALL fall back to the unreduced chain, so a specials-only or side-story-only franchise still has a main line to render.

Extras SHALL be grouped by their **relation to the main line** rather than by media type, in the fixed display order Alternative version, Alternative setting, Prequel, Sequel, Parent story, Side story, Full story, Summary, Spin-off, Character, Adaptation, Other, and ordered by aired-from date within each group.

An extra's group SHALL be resolved so that it belongs to exactly one:

1. Where the extra carries a relation, in either direction, to any main-line member of this series, its group SHALL be the highest-precedence such relation in the display order above. Version relations rank first precisely so a version neighbour that also carries a sequel edge still reads as an alternative version.
2. Where it carries no relation to the main line at all, but does carry a version relation, in either direction, to any other member of this series, its group SHALL be that version relation's group — Alternative version ahead of Alternative setting where it carries both. An alternative version of a side story is an alternative version, and SHALL NOT fall to Other merely because the entry it retells is itself an extra.
3. Where it carries neither — an extra reached only through another extra — it SHALL inherit the group of the extra that reaches it, resolved breadth-first outward from the main line, ties broken by the lower MAL id. A special of a side story therefore reads as Side story. The walk SHALL travel relations other than the version relations, and SHALL be seeded from extras grouped by rule 1 alone: a **version neighbour** SHALL NOT pass its group on by inheritance and SHALL NOT be reached by it, and neither SHALL an extra grouped by rule 2 — so a story extra is never labelled an alternative version merely for sitting next to one.
4. Failing all three, its group SHALL be Other.

A version relation SHALL count for grouping under rules 1 and 2 even though it is never traversed. Both ends of such a relation are frequently members of one series — a recap movie trilogy retelling the TV run it sits beside, a theatrical cut of a side story — and the relation is what names the entry, whether or not the series traversal follows it. Grouping SHALL NOT depend on whether the entry it names was reached as a story-component member or as a version neighbour.

The relation SHALL be read **directionally, from the extra's side**: `M --summary--> X` makes X a Summary, while `X --summary--> M` makes X the Full story; `M --side_story--> X` makes X a Side story, while `X --side_story--> M` makes X the Parent story. The same holds for `sequel`/`prequel`.

#### Scenario: Sequels and story movies are main line
- **WHEN** a series contains three TV seasons and a movie, all linked by sequel relations
- **THEN** all four are main-line entries

#### Scenario: Specials are extras even when MAL calls them sequels
- **WHEN** a member whose media type is `special` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: A promotional video is never main line
- **WHEN** a member whose media type is `pv` is linked into the sequel chain
- **THEN** it is an extra, not a main-line entry

#### Scenario: Extras are grouped by relation, not media type
- **WHEN** a series has two specials that recap its first season and one OVA that is a side story of its second
- **THEN** the two specials appear under a "Summary" group and the OVA under a "Side story" group, rather than under "Special" and "OVA"

#### Scenario: Direction decides the group
- **WHEN** an extra declares `full_story` to a main-line member, and another extra is the target of a main-line member's `summary`
- **THEN** the first is grouped under "Full story" and the second under "Summary"

#### Scenario: A version relation outranks a sequel edge
- **WHEN** a version neighbour carries both an `alternative_version` relation and a `sequel` relation to main-line members
- **THEN** it appears under "Alternative version"

#### Scenario: An extra of an extra inherits its group
- **WHEN** a special's only relation is to an extra that is grouped under "Side story"
- **THEN** that special is grouped under "Side story" too

#### Scenario: A version neighbour does not pass its group on
- **WHEN** an extra's only relation is to a version neighbour grouped under "Alternative setting"
- **THEN** that extra is grouped under "Other" rather than under "Alternative setting"

#### Scenario: A retelling inside its own series reads as an alternative version
- **WHEN** a movie that is a member of the series declares an `alternative_version` relation to a main-line member, a `sequel` relation to another extra, and a `side_story` relation to a third
- **THEN** it is grouped under "Alternative version", the version relation counting even though the traversal never followed it

#### Scenario: An alternative version of an extra is still an alternative version
- **WHEN** an entry's only relation to this series is an `alternative_version` to a member that is an extra rather than main line
- **THEN** it is grouped under "Alternative version" rather than under "Other"

#### Scenario: A relation to the main line outranks a version relation to an extra
- **WHEN** an extra declares a `side_story` relation to a main-line member and an `alternative_version` relation to another extra
- **THEN** it stays grouped under "Side story", and the extra it versions is grouped under "Alternative version"

#### Scenario: An alternative version of an extra does not pass its group on either
- **WHEN** a special's only relation is to an extra that was grouped under "Alternative version" by its version relation to another extra
- **THEN** that special is grouped under "Other" rather than under "Alternative version"

#### Scenario: Side stories and music videos are extras
- **WHEN** a series contains a side story, an OVA run, and a music video
- **THEN** none of them are main-line entries, and they appear grouped by their relation to the main line

#### Scenario: A recap special stays an extra even when it bridges two seasons
- **WHEN** a `tv_special` recaps one season (`summary`/`full_story`) and also carries a `sequel`/`prequel` edge into the next season
- **THEN** it is an extra, not a main-line entry, and the two real seasons it bridges are still main line

#### Scenario: A member tagged as another member's side story is an extra
- **WHEN** a member declares a `parent_story` relation to another member, or another member declares a `side_story` relation to it
- **THEN** it is an extra, not a main-line entry, even when its media type would otherwise allow it

#### Scenario: A side entry that bridges two seasons does not split the main line
- **WHEN** a side-story member carries `sequel`/`prequel` edges linking two seasons that have no direct edge to each other
- **THEN** both seasons remain in one main-line chain and the side entry itself is an extra

#### Scenario: A lone TV show outranks a chain of side movies
- **WHEN** a franchise's only `tv` member has no `sequel`/`prequel` relations of its own while several of its movies chain to each other
- **THEN** the `tv` member is the main line and the movies are extras

#### Scenario: A franchise with no sequel relations is led by its show, not its promo
- **WHEN** a franchise's members are linked only by `parent_story` relations — a show plus concept and character shorts that each name it as their parent story, with no `sequel`/`prequel` relation anywhere
- **THEN** the show is the sole main-line entry and the shorts are extras, regardless of the shorts having aired years earlier

#### Scenario: Arriving from a spin-off does not make it the main line
- **WHEN** a series is built starting from the second season of a spin-off whose sequel chain is shorter than the parent series' chain
- **THEN** the parent series' chain is the main line and the spin-off's entries are extras

#### Scenario: A separate telling's chain cannot take the main line
- **WHEN** Brotherhood is a version neighbour of the Fullmetal Alchemist (2003) series and its own chain is longer
- **THEN** the 2003 series' main line is still its own chain

#### Scenario: A franchise of only side entries still renders a main line
- **WHEN** every member of a series is ineligible — all specials, recaps, or side content of one another
- **THEN** the largest chain is used unreduced rather than leaving the series with no main line


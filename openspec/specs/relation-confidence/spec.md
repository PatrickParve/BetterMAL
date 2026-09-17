# relation-confidence Specification

## Purpose
The relation-confidence capability reads an anime's related-anime edges in both directions and grades each edge's confidence by whether the two ends agree on it, lets AniList adjudicate an edge they contest, and resolves one canonical prequel and one canonical sequel by rank rather than leaving every relation equally weighted. Fetching and storing the edges themselves belong to mal-api-integration and data-persistence; series-page and anime-detail each consume the confidence this capability assigns.

## Requirements

### Requirement: An anime's relations are read in both directions
The system SHALL treat the stored related-anime graph as undirected when presenting one anime's relations: an anime's relation set SHALL be the union of the edges it stores itself (outgoing) and the edges other anime store pointing at it (incoming), each incoming edge inverted to the relation it means from this anime's side.

The inverse map SHALL be:

| Stored relation | Means, from the other end |
| --- | --- |
| `sequel` | `prequel` |
| `prequel` | `sequel` |
| `parent_story` | `side_story` |
| `side_story` | `parent_story` |
| `summary` | `full_story` |
| `full_story` | `summary` |
| `alternative_version` | `alternative_version` |
| `alternative_setting` | `alternative_setting` |
| `character` | `character` |
| `other` | `other` |

A relation with no confident inverse — `spin_off`, `adaptation`, and any unrecognized relation string — SHALL be carried across with its raw value unchanged, SHALL be eligible only for the More overlay, and SHALL NOT be given a dedicated relation button, since the app cannot state what it means from the far end.

Where an outgoing and an inverted incoming edge name the same other anime, they SHALL be de-duplicated into one entry, keeping the outgoing edge's relation type and MAL ordering. Entries derived only from an incoming edge SHALL sort after every outgoing edge.

MAL's `related_anime` is not reliably symmetric, so this is the only way an existing prequel/sequel reaches the page it belongs on.

#### Scenario: A prequel MAL stored only on the other side
- **WHEN** anime B stores no `prequel` relation of its own, and anime A stores `sequel` → B
- **THEN** B's relation set includes A as a `prequel`

#### Scenario: A symmetric edge is not duplicated
- **WHEN** A stores `sequel` → B and B stores `prequel` → A
- **THEN** B's relation set lists A exactly once, as a `prequel`

#### Scenario: A relation with no inverse keeps its raw value
- **WHEN** A stores `spin_off` → B and B stores nothing back
- **THEN** B's relation set includes A with the relation `spin_off`, reachable in the More overlay and never as a dedicated button

#### Scenario: Reverse-derived entries sort last
- **WHEN** an anime has two outgoing relations and one relation derived from an incoming edge
- **THEN** the two outgoing entries keep MAL's ordering and the derived entry follows them

#### Scenario: Self-inverse relations carry across unchanged
- **WHEN** A stores `alternative_version` → B and B stores nothing back
- **THEN** B's relation set includes A as an `alternative_version`

### Requirement: Every relation edge carries a confidence
The system SHALL classify each relation edge by whether its two ends agree:

- **Confirmed** — the far end stores the inverse relation back.
- **Unconfirmed** — the far end has been full-fetched (so its own relations are known) and stores no matching relation back.
- **Unknown** — the far end has never been full-fetched, so its silence carries no information.

A relation type with no confident inverse SHALL never be classified Confirmed by symmetry, since there is no counterpart edge to look for; it SHALL be Unknown unless an external source (see the AniList adjudication requirement) settles it.

Confidence SHALL be derived at read time from the stored graph and the far end's fetch state, not stored as a column, so it corrects itself as soon as either end is refreshed.

A one-sided edge is simultaneously the signal that a relation is missing from one end and the signal that MAL may have the relation wrong, so confidence SHALL be carried through to every consumer rather than collapsed into a boolean at the point of storage.

#### Scenario: Both ends agree
- **WHEN** A stores `sequel` → B and B stores `prequel` → A
- **THEN** the edge is Confirmed from both sides

#### Scenario: The far end was fetched and disagrees
- **WHEN** A stores `sequel` → B, and B has been full-fetched and stores no `prequel` back
- **THEN** the edge is Unconfirmed

#### Scenario: The far end was never fetched
- **WHEN** A stores `sequel` → B and B has never had a full-detail fetch
- **THEN** the edge is Unknown, not Unconfirmed

#### Scenario: Confidence follows a refresh
- **WHEN** an Unknown edge's far end is full-fetched and turns out to store the inverse relation
- **THEN** the edge reads as Confirmed on the next read, with no backfill step

### Requirement: AniList adjudicates contested relation edges
The system SHALL use AniList as a verification signal for relation edges that MAL's own two ends do not agree on. AniList SHALL NOT replace MAL as the source of relation data; it SHALL only settle whether a contested MAL edge is trusted.

Adjudication SHALL apply to Unconfirmed edges. Confirmed edges need no adjudication and SHALL NOT be looked up on their account. Unknown edges SHALL NOT be adjudicated, since the disagreement they would be tested against does not exist.

The relation data SHALL be obtained by extending the existing AniList media lookup with the media's relations, so an anime that already receives an airing lookup costs no additional request. Anime needing adjudication and nothing else SHALL be batched into paged multi-media queries under the existing AniList request pacing.

AniList's relation types are an uppercase enum and SHALL be mapped to MAL's values: `PREQUEL`→`prequel`, `SEQUEL`→`sequel`, `PARENT`→`parent_story`, `SIDE_STORY`→`side_story`, `SUMMARY`→ MAL's `summary`/`full_story` pair (AniList expresses the pair from one side only, so a `SUMMARY` edge in either direction satisfies either MAL value), `SPIN_OFF`→`spin_off`, `CHARACTER`→`character`, `OTHER`→`other`. `ALTERNATIVE` SHALL satisfy either `alternative_version` or `alternative_setting`, since MAL splits what AniList merges. `ADAPTATION` is manga-side and SHALL be ignored. Any AniList relation type with no mapping SHALL be treated as an edge of unmatched type rather than as no edge.

Each adjudicated edge SHALL take one verdict:

- **Confirmed** — AniList reports an edge between the same two anime whose mapped type matches the MAL edge.
- **Corroborated** — AniList relates the two anime, but under a different relation type. They belong to the same franchise, so the edge SHALL still be traversed for series membership, but it SHALL NOT earn a dedicated prequel/sequel button.
- **Contradicted** — AniList resolves both MAL ids to AniList media and reports no edge whatsoever between them. The edge SHALL remain stored and SHALL remain visible in the More overlay, but SHALL NOT be traversed for series membership and SHALL NOT earn a dedicated relation button.
- **Unknown** — AniList does not know one or both ends, or the lookup fails. The edge SHALL behave exactly as it does today, so an AniList outage regresses nothing.

#### Scenario: AniList contradicts a bad MAL edge
- **WHEN** MAL reports `17965 --sequel--> 39360`, 39360 stores nothing back, and AniList's media for 17965 relates it only to 3044
- **THEN** the edge is Contradicted: 39360 is not pulled into 17965's series, and no sequel button is rendered for it

#### Scenario: AniList confirms a one-sided MAL edge
- **WHEN** MAL reports `A --sequel--> B`, B stores nothing back, and AniList reports the same sequel relation between them
- **THEN** the edge is Confirmed and B's page shows A as a prequel

#### Scenario: AniList relates the pair under another type
- **WHEN** MAL reports `A --sequel--> B` unreciprocated, and AniList relates A and B as `SIDE_STORY`
- **THEN** the edge is Corroborated: B stays in A's series, and A does not get a dedicated sequel button for B

#### Scenario: AniList has never heard of the far end
- **WHEN** an Unconfirmed edge's far MAL id resolves to no AniList media
- **THEN** the verdict is Unknown and the edge is traversed and rendered exactly as before this change

#### Scenario: Adjudication rides along with an airing lookup
- **WHEN** an anime that needs relation adjudication is also due an AniList airing lookup
- **THEN** its relations arrive in that same lookup and no extra AniList request is made for adjudication

#### Scenario: AniList failure never blocks a page
- **WHEN** the AniList lookup for an adjudication errors or times out
- **THEN** every affected edge falls back to Unknown and the page renders with today's behaviour

### Requirement: One canonical prequel and sequel are resolved by rank
When an anime has more than one candidate for a relation that gets a dedicated button, the system SHALL resolve which one the button targets by the following rules, applied in order, rather than by MyAnimeList's array position:

1. Discard candidates that are a recap (`summary`/`full_story`) or side content (`parent_story`/`side_story`) of another anime in the candidate set or of the anime being viewed. Where this empties the set, the undiscarded set SHALL be used instead, so an anime whose only relations are side entries still resolves one.
2. Discard Contradicted edges.
3. Prefer the highest confidence: Confirmed above Corroborated, Unconfirmed, and Unknown.
4. At equal confidence, prefer an edge this anime stores itself over one derived from an incoming edge, so a reverse-derived candidate can never displace a correct outgoing pick.
5. Prefer a candidate whose media type matches the anime being viewed, then one whose media type is `tv`.
6. Prefer the nearest preceding aired-from date for a prequel, and the nearest following aired-from date for a sequel. Candidates with no aired date sort last.
7. Break any remaining tie on the lowest MAL id, so the result is deterministic.

The exclusion in rule 1 SHALL precede any air-date rule: air-date proximity alone picks a recap movie over the TV series it recaps whenever the recap aired closer.

Candidates not selected SHALL continue to appear in the More overlay exactly as they do today; ranking decides which one gets the button, never which ones are shown.

Where an anime has no candidate edge in a direction at all, the system MAY fall back to its immediate neighbour in the stored series' main line, in that series' story order. Where the anime is a member of more than one series, the fallback SHALL use its **primary** series, as the `series-versions` capability defines, so the button never crosses into another telling of the franchise. A direct edge SHALL always outrank the series-neighbour fallback, so a page that resolves correctly today cannot regress.

MAL's array position is ascending anime id, so "the first MAL reports" is an arbitrary pick that happens to be right only when the correct answer also holds the lowest id.

#### Scenario: A recap loses to the series it recaps
- **WHEN** anime 36862 has two prequel candidates — 34599 (`tv`) and 37515, which declares `full_story` → 34599
- **THEN** 37515 is discarded as a recap and the prequel button targets 34599, by the recap rule rather than by id order

#### Scenario: Air-date proximity does not override the recap rule
- **WHEN** the recap candidate aired closer to the anime being viewed than the TV series does
- **THEN** the recap is still discarded first and the TV series is still selected

#### Scenario: Two incoming sequel edges, one from a recap
- **WHEN** an anime is the target of two `sequel` edges, one from a recap special and one from a real season
- **THEN** its prequel button targets the real season

#### Scenario: A confirmed edge beats an unconfirmed one
- **WHEN** an anime has two sequel candidates, one Confirmed and one Unconfirmed
- **THEN** the sequel button targets the Confirmed candidate

#### Scenario: Reverse-derived candidates never displace an outgoing pick
- **WHEN** an anime stores its own `prequel` edge and another anime's incoming `sequel` edge yields a second prequel candidate at the same confidence
- **THEN** the button targets the anime's own stored prequel

#### Scenario: Non-selected candidates stay reachable
- **WHEN** ranking selects one of three prequel candidates
- **THEN** the other two are listed in the More overlay

#### Scenario: Ranking is deterministic
- **WHEN** two candidates are equal on every preceding rule
- **THEN** the one with the lower MAL id is selected, on every read

#### Scenario: Series neighbour fills a missing direction
- **WHEN** an anime has no prequel candidate of any kind but sits second in its stored series' main line
- **THEN** the prequel button MAY target the main-line entry before it

#### Scenario: The fallback uses the primary series
- **WHEN** an anime with no prequel candidate is a member of two tellings' series
- **THEN** the fallback walks its primary series' main line, not the other telling's

#### Scenario: A direct edge outranks the series neighbour
- **WHEN** an anime has both a direct prequel edge and a different main-line predecessor
- **THEN** the prequel button targets the direct edge

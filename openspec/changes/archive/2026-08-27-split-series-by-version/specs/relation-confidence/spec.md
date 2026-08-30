## MODIFIED Requirements

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

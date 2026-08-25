## MODIFIED Requirements

### Requirement: Prequel/sequel links when they exist
The system SHALL show prequel and sequel link buttons in the top-right corner of the detail page, only when such related anime exist for that anime, each linking to the related anime's detail page.

The relations these buttons are drawn from SHALL be the anime's full two-directional relation set — the edges it stores itself plus the inverted edges other anime store pointing at it — so a prequel or sequel that MyAnimeList recorded only on the other side still gets a button here. An anime that is the target of another anime's `sequel` relation SHALL therefore show a prequel button for it, whether or not it stores a `prequel` relation of its own.

When more than one candidate exists for a button, the target SHALL be the one the ranked resolution rules select, **not** the first that MyAnimeList reports; the remainder SHALL be reachable through the More overlay. An edge an external source contradicts SHALL NOT be given a button.

#### Scenario: Related anime exist
- **WHEN** an anime has a prequel and/or a sequel
- **THEN** the corresponding link button(s) are shown and navigate to the related anime's detail page

#### Scenario: No related anime
- **WHEN** an anime has neither a prequel nor a sequel, in either direction
- **THEN** no prequel/sequel buttons are shown

#### Scenario: A prequel MAL only recorded on the other side
- **WHEN** I open the detail page of an anime that stores no prequel relation, but which another anime names as its sequel
- **THEN** a prequel button is shown linking to that other anime

#### Scenario: Multiple prequels
- **WHEN** an anime has two prequels
- **THEN** the prequel button links to the one the ranked resolution selects and the other is listed in the More overlay

#### Scenario: A contradicted edge gets no button
- **WHEN** an anime's only sequel candidate is an edge an external source contradicts
- **THEN** no sequel button is shown, and the entry remains listed in the More overlay

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel, and SHALL present each anime's relations as the union of its own stored edges and the inverted edges other anime store pointing at it.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

An entry derived from an incoming edge SHALL be grouped under the relation that edge means from this anime's side, except where the relation has no confident inverse (`spin_off`, `adaptation`, or an unrecognized string), in which case the raw relation SHALL be used. Where an outgoing and an incoming edge name the same anime, one entry SHALL be shown, not two.

Every stored edge SHALL remain listed in this overlay, including one an external source contradicts. The overlay is where a relation the app declines to act on is still visible; withholding a button is a statement about confidence, not a reason to hide data.

MyAnimeList's `related_anime` data itself carries a media type per related node when requested via nested field selection (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request and store it directly — no separate fetch of the related anime is needed to learn its media type, and no backfill mechanism SHALL exist for this purpose. A relation row for which MAL reported no media type MAY fall back to a lookup in our own cached metadata for that related anime; if neither source has it, the entry SHALL show "Unknown", matching how an unknown media type is labelled elsewhere in the app.

When the anime being viewed is itself a side entry — that is, it has a `parent_story` relation in either direction, whether stored on itself or derived from another anime's `side_story` edge — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported from either side, no such button SHALL be shown.

Related-anime data (including each relation's media type) SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

#### Scenario: A relation MAL stored only on the other side appears in the overlay
- **WHEN** I open the detail page of an anime that stores no relations of its own, but which another anime names with a `side_story` relation
- **THEN** the More overlay lists that other anime under "Parent story"

#### Scenario: A contradicted relation is still listed
- **WHEN** an anime holds a relation an external source contradicts
- **THEN** the entry appears in the More overlay under its relation heading, even though it earns no dedicated button and is not traversed into a series

#### Scenario: Overlay rows show media type with no extra request
- **WHEN** I open the detail page of an anime with related entries never separately cached before, and then press More
- **THEN** every row already shows its media type (from the same full-detail fetch that loaded the page), with no additional request made for any of them

#### Scenario: Loading the detail page fetches nothing extra for relations
- **WHEN** I open the detail page of an anime with many related entries we have never cached
- **THEN** the single full-detail fetch for that page also resolves every relation's media type, and no separate request is made for any related anime

#### Scenario: A relation MAL cannot resolve shows Unknown
- **WHEN** MAL's related_anime data reports no media type for a relation, and our own cache has no metadata for that related anime either
- **THEN** that row shows "Unknown" beneath its title

#### Scenario: Navigating from the overlay
- **WHEN** I click a related anime in the More overlay
- **THEN** the overlay closes and the app navigates to that anime's detail page

#### Scenario: Anime with only a prequel and sequel
- **WHEN** I open the detail page of an anime whose only related entries, in either direction, are a prequel and a sequel
- **THEN** no "More" button is shown

#### Scenario: Side entry links back to its main series
- **WHEN** I open the detail page of a movie that MAL reports as having a parent story
- **THEN** a "Main series" button is shown and navigates to that parent anime's detail page

#### Scenario: Main-series button from the parent's own side_story edge
- **WHEN** I open the detail page of a movie that stores no `parent_story` relation, but whose parent story stores a `side_story` relation naming it
- **THEN** a "Main series" button is shown linking to that parent

#### Scenario: Main-series button absent without a parent story
- **WHEN** I open the detail page of an anime for which no parent story is reported from either side
- **THEN** no "Main series" button is shown

#### Scenario: Lean refresh preserves relations
- **WHEN** an anime with stored related-anime entries is refreshed as part of a season or top-anime listing fetch
- **THEN** its stored related-anime entries (including their media types) are left intact

#### Scenario: Closing the overlay
- **WHEN** the More overlay is open and I press Escape or click outside it
- **THEN** the overlay closes and the detail page is unchanged

## ADDED Requirements

### Requirement: A failed data refresh is shown as a failure, not as an empty page
When the detail page's read reports that fetching fresh data was attempted and failed, the page SHALL say so and SHALL offer a retry, rather than rendering the relations row as though the anime simply has no relations.

The rest of the page SHALL still render from whatever cached data was returned. The retry SHALL re-read the anime, taking the same path a first visit does.

A page served after a failed fetch of a never-cached anime is otherwise indistinguishable from a correct render of an anime with no relations — same absent prequel, sequel, More, and Series controls — which is the one case where showing nothing is actively misleading.

#### Scenario: Failed fetch offers a retry
- **WHEN** I open an anime's detail page and the server reports that its refresh failed
- **THEN** the page tells me the data could not be refreshed and offers a retry control

#### Scenario: Retry re-reads the anime
- **WHEN** I press that retry control
- **THEN** the anime is read again and the page re-renders from the result

#### Scenario: A genuinely relation-less anime is not mislabelled
- **WHEN** I open the detail page of an anime whose data refreshed successfully and which has no related anime
- **THEN** no failure message is shown and the relations row is simply absent, as today

#### Scenario: Cached content still renders behind the notice
- **WHEN** a refresh fails for an anime that already had cached data
- **THEN** the page renders that cached data with the failure notice alongside it, rather than replacing the page with an error

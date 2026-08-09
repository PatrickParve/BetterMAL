## MODIFIED Requirements

### Requirement: More-relations overlay
The system SHALL store every related-anime edge MyAnimeList reports for an anime, not only the first prequel and first sequel.

The detail page SHALL show a **More** button immediately to the left of the prequel and sequel buttons whenever the anime has at least one related entry whose relation is neither prequel nor sequel. When it has no such entries, no More button SHALL be shown.

Pressing **More** SHALL open an overlay listing those related entries, grouped by relation type with the relation shown as a group heading (e.g. "Side story", "Alternative version", "Summary", "Spin-off", "Character", "Other"), each entry linking to that anime's own detail page and closing the overlay on navigation. Each entry SHALL also show that related anime's media type (e.g. TV, Movie, OVA) beneath its title. The overlay SHALL close on Escape and on a click outside it, matching every other overlay in the app.

MyAnimeList's `related_anime` data itself carries a media type per related node when requested via nested field selection (`related_anime{node{media_type}}`), so a full-detail fetch SHALL request and store it directly — no separate fetch of the related anime is needed to learn its media type, and no backfill mechanism SHALL exist for this purpose. A relation row written before this was stored, or for which MAL reported no media type, MAY fall back to a lookup in our own cached metadata for that related anime; if neither source has it, the entry SHALL show "Unknown", matching how an unknown media type is labelled elsewhere in the app.

When the anime being viewed is itself a side entry — that is, MyAnimeList reports a `parent_story` relation for it — the detail page SHALL additionally show a **Main series** button linking to that parent anime's detail page. When no parent story is reported, no such button SHALL be shown.

Related-anime data (including each relation's media type) SHALL be written only by a full-detail fetch; a lean listing refresh (season or top-anime browsing) SHALL NOT clear or overwrite it.

#### Scenario: Anime with side entries
- **WHEN** I open the detail page of an anime that MAL lists with a prequel, a sequel, a side-story movie, and a summary special
- **THEN** a "More" button appears to the left of the prequel and sequel buttons, and opening it lists the movie under "Side story" and the special under "Summary"

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
- **WHEN** I open the detail page of an anime whose only related entries are a prequel and a sequel
- **THEN** no "More" button is shown

#### Scenario: Side entry links back to its main series
- **WHEN** I open the detail page of a movie that MAL reports as having a parent story
- **THEN** a "Main series" button is shown and navigates to that parent anime's detail page

#### Scenario: Main-series button absent without a parent story
- **WHEN** I open the detail page of an anime for which MAL reports no parent story
- **THEN** no "Main series" button is shown

#### Scenario: Lean refresh preserves relations
- **WHEN** an anime with stored related-anime entries is refreshed as part of a season or top-anime listing fetch
- **THEN** its stored related-anime entries (including their media types) are left intact

#### Scenario: Closing the overlay
- **WHEN** the More overlay is open and I press Escape or click outside it
- **THEN** the overlay closes and the detail page is unchanged

## ADDED Requirements

### Requirement: Detail page reads itself once per visit
Opening an anime's detail page SHALL issue exactly one read of that anime, however many times the page's load effect runs, and SHALL NOT re-read it to observe a change that a mutation response already reported.

The page SHALL re-read the anime only for the manual **Refresh data** action, which is an explicit user request for fresh data.

#### Scenario: One read per visit
- **WHEN** I navigate to an anime's detail page
- **THEN** exactly one `GET /api/anime/{id}` is issued for that visit

#### Scenario: Completing an anime from the detail page
- **WHEN** I increment the last episode from the detail page and the completion-score prompt saves a score and closes
- **THEN** the page's displayed entry, progress bar, and score box update from the saved entry, and the anime is not re-read

#### Scenario: Refresh data still re-reads
- **WHEN** I press "Refresh data"
- **THEN** the anime is refreshed against MAL and AniList and the page re-reads it, as it does today

## ADDED Requirements

### Requirement: Hide-NSFW setting excludes hentai from the season browser
The system SHALL provide a persisted user setting, presented on the Settings page as a "Hide NSFW" checkbox and **unchecked by default**, that excludes hentai from the season browser's results.

The filter SHALL exclude hentai and nothing else: an anime SHALL be treated as hentai when, and only when, MAL's own `rating` field for it is `rx`. Every other rating — including `r` and `r+` — SHALL remain visible with the setting enabled, so mature and ecchi titles are not swept up by it. An anime whose rating MAL has not yet reported (not yet cached) SHALL be treated as not hentai, so the filter never hides a title it cannot positively identify.

The filter SHALL be applied server-side, before the season result count is computed, so the displayed count and infinite scroll stay correct. It SHALL apply to the season browser only: search results, my list, top anime, the airing schedule, the home dashboard, and anime detail pages SHALL be unaffected by it. The setting SHALL persist across reloads and new tabs, and SHALL take effect on the season page without requiring a MAL refetch of the season.

#### Scenario: Default is off
- **WHEN** I open the Settings page without ever having changed this setting
- **THEN** the "Hide NSFW" checkbox is unchecked, and the season page shows hentai alongside everything else

#### Scenario: Enabling the setting hides hentai from the season page
- **WHEN** I enable "Hide NSFW" and open a season containing hentai
- **THEN** every anime MAL rates `rx` is absent from the results, and the result count reflects the smaller set

#### Scenario: R and R+ titles stay visible
- **WHEN** "Hide NSFW" is enabled and a season contains anime rated `r` or `r+`
- **THEN** those anime are still shown — only `rx` titles are removed

#### Scenario: Unknown rating is not hidden
- **WHEN** "Hide NSFW" is enabled and a cached season anime has no rating recorded yet
- **THEN** it is still shown, rather than being hidden on suspicion

#### Scenario: Filtering holds across infinite scroll
- **WHEN** "Hide NSFW" is enabled and I scroll far enough to load additional pages of a season
- **THEN** no hentai appears in any loaded page, because the exclusion is applied server-side rather than to each loaded page on the client

#### Scenario: Toggling re-reads from cache only
- **WHEN** I change the "Hide NSFW" setting while viewing a season
- **THEN** the season results are re-read from the cache with the new filter applied and no MAL fetch is started

#### Scenario: Setting persists
- **WHEN** I enable "Hide NSFW" and later reload the app or open it in a new tab
- **THEN** the setting is still enabled

#### Scenario: Other pages are unaffected
- **WHEN** "Hide NSFW" is enabled and I use search, my list, top anime, the airing schedule, or the home dashboard
- **THEN** those pages show exactly what they showed before, including any hentai already in my list

### Requirement: Cached anime records carry MAL's content rating
The system SHALL store MAL's `rating` value (`g`, `pg`, `pg_13`, `r`, `r+`, `rx`) on each cached anime record, populated both by the lean listing refresh that backs season browsing and by full detail fetches, so the season browser can filter on it without an extra MAL request. A missing or unrecognized rating SHALL be stored as absent rather than rejected.

#### Scenario: Season refresh records ratings
- **WHEN** a season is refreshed from MAL
- **THEN** each returned anime's rating is stored on its cached record

#### Scenario: Anime cached before ratings were stored
- **WHEN** an anime was cached before the rating field was recorded and its season has not been refreshed since
- **THEN** its rating is absent and it is treated as not hentai, until the next refresh of that season fills it in

#### Scenario: Rating absent from MAL
- **WHEN** MAL returns an anime with no rating value
- **THEN** the cached record stores no rating and the ingest succeeds

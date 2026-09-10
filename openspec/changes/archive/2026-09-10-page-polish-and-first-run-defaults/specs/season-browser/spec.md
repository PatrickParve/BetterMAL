## MODIFIED Requirements

### Requirement: Hide-NSFW setting excludes hentai from the season browser
The system SHALL provide a persisted user setting, presented on the Settings page as a "Hide NSFW" checkbox and **checked by default** whenever no choice has been stored — as on the first run of the app in a browser — that excludes hentai from the season browser's results and from the year browser's results — the two pages read the same cached season listings, at a season's grain and at a year's, so the exclusion follows the data rather than the page. Once a choice is stored, the stored choice SHALL govern from then on, whether checked or unchecked; the default SHALL NOT override it.

The filter SHALL exclude hentai and nothing else: an anime SHALL be treated as hentai when, and only when, MAL's own `rating` field for it is `rx`. Every other rating — including `r` and `r+` — SHALL remain visible with the setting enabled, so mature and ecchi titles are not swept up by it. An anime whose rating MAL has not yet reported (not yet cached) SHALL be treated as not hentai, so the filter never hides a title it cannot positively identify.

The filter SHALL be applied server-side, before the result count is computed, so the displayed count and infinite scroll stay correct on both pages. It SHALL apply to the season and year browsers only: search results, my list, top anime, the airing schedule, the home dashboard, and anime detail pages SHALL be unaffected by it. The setting SHALL persist across reloads and new tabs, and SHALL take effect on either page without requiring a MAL refetch.

#### Scenario: Default is on for a first run
- **WHEN** I open the Settings page in a browser that has no stored choice for this setting
- **THEN** the "Hide NSFW" checkbox is checked, and the season page omits every anime MAL rates `rx`

#### Scenario: Unchecking is remembered
- **WHEN** I uncheck "Hide NSFW" and then reload the page or open the app in a new tab
- **THEN** the checkbox is still unchecked and the season page shows hentai alongside everything else, rather than the first-run default re-enabling the filter

#### Scenario: A stored choice is not overridden by the default
- **WHEN** a browser already has a stored "Hide NSFW" choice of unchecked from before this default was introduced
- **THEN** the checkbox remains unchecked there

#### Scenario: Enabling the setting hides hentai from the season page
- **WHEN** I enable "Hide NSFW" and open a season containing hentai
- **THEN** every anime MAL rates `rx` is absent from the results, and the result count reflects the smaller set

#### Scenario: Enabling the setting hides hentai from the year page
- **WHEN** I enable "Hide NSFW" and open a year whose seasons contain hentai
- **THEN** every anime MAL rates `rx` is absent from the year's combined results, and the result count reflects the smaller set

#### Scenario: R and R+ titles stay visible
- **WHEN** "Hide NSFW" is enabled and a season contains anime rated `r` or `r+`
- **THEN** those anime are still shown — only `rx` titles are removed

#### Scenario: Unknown rating is not hidden
- **WHEN** "Hide NSFW" is enabled and a cached season anime has no rating recorded yet
- **THEN** it is still shown, rather than being hidden on suspicion

#### Scenario: Filtering holds across infinite scroll
- **WHEN** "Hide NSFW" is enabled and I scroll far enough to load additional pages of a season or a year
- **THEN** no hentai appears in any loaded page, because the exclusion is applied server-side rather than to each loaded page on the client

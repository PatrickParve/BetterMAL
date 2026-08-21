## Why

The colour families introduced in `section-colour-language` currently land only on a section's title *text*, as a gradient painted through the letters. On a page of boxed lists that reads as a slightly off-colour heading rather than as a label for the block beneath it: the colour stops at the end of the words, the section it names is not marked, and the rows inside a coloured section still highlight in the app's generic purple accent, so a family names a heading rather than a section. The same colour language should carry the whole section — a title band across the block's full width and the rows inside it lighting up in that block's own colour.

Two unrelated rough edges ride along with it: a main-line timeline card is narrow enough that a multi-digit episode count (One Piece's `Watching · 1141/1141`) pushes the rewatch indicator out of the card's footer entirely, contradicting the `series-page` requirement that a card show its rewatch count; and the settings page is nine visually identical boxes stacked in one 640px column with no grouping, so a preference toggle looks exactly like a several-minute background job.

## What Changes

- **BREAKING (visual)**: a section title drawn in a colour family is no longer gradient-painted text. It becomes a **filled band** spanning the full width of the list or table it heads, filled with the family's gradient, with the title centred on it in contrasting ink. The gradient-through-the-letters treatment is removed.
- Rows ("cards") inside a family-carrying section highlight on hover and keyboard focus in **that family's colour** instead of the app's purple accent — same tinted background, coloured border, and elevation as today, in the section's own hue.
- In my-score-vs-MAL comparisons the two score roles keep their existing blue/purple: the profile's **They liked it, I didn't** band and rows are blue, **I liked it, they didn't** purple, and a **hot-take row** highlights blue or purple according to the direction pill it already carries — while the hot-takes band itself stays the hot-take family's red.
- Banded titles: profile's two divergence lists, **Favourite years**, **Favourite seasons**; recap's **Biggest Hot takes**, **Season ranking**, **Seasons by time watched**, **Year ranking**, **Years by time watched**. **Top N**, **Stats**, and **Rating distribution** stay untinted and unbanded, as they are today.
- Series page: main-line timeline cards get wider, and a card's rewatch indicator is guaranteed visible — the status text truncates before the badge does, so a four-digit episode count can no longer push it out of the card.
- Settings page: the nine flat boxes are reorganised into a handful of labelled groups (preferences, sync and its status, the long-running data tools, account connection), restyled with consistent action rows and a real progress readout for background jobs, in a wider single column. No setting, action, or endpoint is added or removed.

## Capabilities

### New Capabilities
- `settings-page`: how the settings page is organised and presented — grouping of its controls into named groups, the visual distinction between an instant preference and a long-running background job, the shape of an action row, and how a running job reports progress.

### Modified Capabilities
- `section-colour-language`: the "Tinted section titles" requirement is replaced — a family is carried by a full-width filled band with centred, legible label text rather than by tinting the title's own letters; a new requirement extends a family from a section's title to the rows inside that section's list.
- `profile-stats`: the profile's four family-carrying section titles are banded rather than tinted, and their rows highlight in the section's family (blue for the MAL divergence list, purple for mine).
- `list-recaps`: the recap's five family-carrying section titles are banded rather than tinted; ranking rows and "See all" overlay rows highlight in their section's family; a hot-take row highlights in the score role of its own direction rather than in the page accent.
- `series-page`: the main-line timeline card's fixed size is widened, and the card's rewatch indicator is required to stay visible whatever the entry's episode count.

## Impact

- `frontend/src/index.css` — family tokens and the shared title treatment (`.tinted-title` → band), plus the `--fam-*` aliases rows will read.
- `frontend/src/components/RankingSection.tsx` / `.css` — family moves from the `<h2>` to the section element; ranking rows read `--fam-*`.
- `frontend/src/components/RankingOverlay.tsx` (+ CSS) — "See all" overlay rows inherit the family of the ranking that opened them.
- `frontend/src/pages/ProfilePage.tsx` / `.css` — divergence and favourites sections carry the family; `.profile-list-row` hover reads `--fam-*`.
- `frontend/src/pages/RecapPage.tsx` / `.css` — hot-takes and ranking sections carry families; `.recap-hot-take` hover follows its own direction.
- `frontend/src/components/SeriesTimeline.css` — `--card-w` (and the landscape width derived from it), footer/badge flex rules.
- `frontend/src/pages/SettingsPage.tsx` / `.css` — regrouped markup and restyled controls; no API-client change.
- No backend, API, or data changes. No new dependencies.

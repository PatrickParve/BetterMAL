## Why

Top series is meant to rank my *franchises*, but a "series" with a single main-line entry — a standalone movie, or a show that only ever got one season — is ranked alongside genuine multi-season franchises. Because those one-entry series average a single score, they dominate the top of the strip while saying nothing about a franchise holding up across seasons, and they pad a strip that is already uncapped.

## What Changes

- Add a control to the **Top series** section header that hides series whose **main line** is a single entry, and shows them again when pressed a second time.
- The control defaults to **showing** single-entry series, so the section looks exactly as it does today for anyone who never touches the button. No existing behaviour changes until it is pressed.
- The count that decides it is the main line only: a series with one main-line season plus OVAs, specials, or other extras still counts as single-entry and is hidden while the filter is on. A main-line season that's announced but hasn't aired a single episode doesn't count toward that total either — a franchise isn't multi-entry until its second entry has actually started.
- The filter composes with the existing **my score / MAL score** ranking basis — both narrow the strip, and neither resets the other. Ordering within the strip is unchanged.
- When the filter empties the strip, the section says so rather than rendering an empty strip, matching how the existing unrankable-under-this-basis empty state reads.
- The control's state behaves like the ranking basis: reset to its default on a fresh visit to the profile page, restored on back-navigation.

## Capabilities

### New Capabilities

None — this extends the existing Top series section rather than introducing a new capability.

### Modified Capabilities

- `profile-stats`: adds a requirement for the single-entry filter control and its default, extends the Top series empty-state requirement with the "filter hid everything" case, and extends the restore-on-back-navigation requirement to cover the new control alongside the ranking basis.

## Impact

- `frontend/src/pages/ProfilePage.tsx` — the toggle's state (`useRestorableState`), the filter applied ahead of `rankTopSeries`, the header-row button, and the new empty-state branch.
- `frontend/src/pages/ProfilePage.css` — a pressed/active style for the toggle, reusing the existing `profile-box__control` button.
- `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs` and `Services/Profile/ProfileDto.cs` — a new `MainLineAiredCount` on `SeriesRankingResult`/`TopSeriesItemDto` (main-line members excluding ones that haven't started airing), computed from airing status already loaded by `SeriesRankingLookup`. No database or endpoint shape change beyond that one added field; `malMain`/`mineMain` and their `totalCount`s are untouched (design.md decision 2).
- No change to the series page, series search, or any other surface — the filter is local to the profile strip.

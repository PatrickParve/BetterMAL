## Why

Several controls tell the reader nothing when they fail or when there is nothing to show. The detail page's Prequel/Sequel buttons vanish when the relation is absent, so the row's controls move under the pointer between anime; its More overlay lists relations under MyAnimeList's romaji titles while every other surface in the app shows English ones. The entry editor can set a date but never clear one, its date fields are the browser's own — cramped and near-unusable on Safari/macOS — and a save the server rejected for a real, stated reason ("Finish date cannot be earlier than start date") is reported as "Could not save changes. Please try again.", because the client throws away the `{ "error": … }` body every rejection carries. Worst of all, a "+" click the server refuses is swallowed in silence: the count simply does not move and nothing says why. Alongside those, three smaller misreadings: the series page's "in my list" filter shows nothing when pressed (every group is collapsed, and the filter is defined not to open any), the profile's "See all" overlay is sized off the viewport so a sliver of a further row always peeks under the last one, and the top-3 showcase cards print my score and MAL's on visibly different lines.

## What Changes

- **Prequel and Sequel buttons are always present on the detail page.** When no prequel (or sequel) exists, its button renders dimmed and non-interactive rather than being omitted, so the related-links row keeps one stable shape across anime. **BREAKING** for the `anime-detail` spec's "only when such related anime exist" claim; nothing about which anime a button targets changes.
- **The More overlay shows English titles.** Related-anime rows use the English title where one is known, falling back to the stored title — the same `pickDisplayTitle` rule every other surface uses. Needs the English title carried on the related-anime DTO, which today has only MAL's romaji `title`.
- **The entry editor's date fields become dropdown triples.** Start and finish date are each a Year / Month / Day set of selects rendered identically in every browser, replacing `<input type="date">` and Safari/macOS's cramped native field. Selecting the blank option in any part clears that date; a "Clear" control clears it in one action, and a "Today" control sets it in one action. The day list follows the selected month and year, including leap years. Neither field can be set to a date later than today — the dropdowns never offer the choice, and the server independently rejects one that reaches it by another path.
- **A date can exist on its own.** A finish date with no start date, and a start date with no finish date, are both explicitly valid; only the pair, both present, is ordered, and same-day is allowed. This is what the backend already enforces, now stated and matched by the editor's own check.
- **A rejected save says why.** The client reads the `{ "error": … }` body every 4xx carries and shows that sentence in the editor, so a finish-before-start rejection reads as such instead of "Could not save changes. Please try again." Server-side messages are reworded to be reader-facing: no `Anime 31964` id prefix and no `(Parameter 'request')` suffix.
- **A failed action is reported, wherever it was taken.** A new app-wide failure notice reports an action that did not take effect — the "+" button above all, whose failure is currently swallowed with the count left where it was — naming the anime and the server's own reason where there is one. Its mechanism sits alongside the existing connection-status notice rather than inside any one page.
- **The series page's "in my list" control shows my list.** Activating it turns the filter on, drops per-group exemptions, and opens every group holding at least one entry of mine that the media-type filter admits — mirroring what selecting a media type already does — so what is on screen afterwards is exactly my own entries, narrowed to the selected types when any are selected. Activating it again collapses every group, returning the section to headings alone. **BREAKING** for the `series-page` spec's "SHALL NOT change any group's collapsed state".
- **The "See all" rankings overlay is sized by its rows and carries no close control.** It shows eight whole rows on open with no part of a ninth visible, derived from row height rather than from the viewport, and keeps scrolling for the rest. The Close button beneath the list is removed outright — no ✕ replaces it — leaving Esc and a click outside, which every overlay in the app already offers. This applies wherever the overlay opens: the profile page's favourites rankings and the recap page's rankings alike.
- **A score chip's value aligns with its neighbour's.** The two chips on a top-3 showcase card put my score and MAL's on the same line; today MAL's sits lower because its value is a `ScoreValue` box whose `vertical-align: middle` shifts it against the plain number beside it. Fixed in the shared chip so every paired chip in the app agrees.

## Capabilities

### New Capabilities

- `action-failure-notices`: an app-wide mechanism for reporting an action that failed — what did not happen, and the server's own reason where it gave one — so a rejected or dropped edit is never silent, whatever page or control it was taken from.

### Modified Capabilities

- `anime-detail`: Prequel/Sequel buttons are always rendered, dimmed and inert when the relation is absent; the More overlay's rows show English titles.
- `list-editing`: the editor's date fields are browser-independent dropdown triples that can be cleared or set to today in one action, and can never be set later than today; either date may exist without the other; a rejected save reports the server's stated reason rather than a generic message.
- `series-page`: the More section's "in my list" control opens the groups holding my entries and collapses them again, instead of leaving collapsed state alone.
- `profile-stats`: the "See all" rankings overlay shows eight whole rows sized from row height and offers no close control of its own, leaving Esc and click-outside.
- `list-recaps`: the same "See all" overlay, opened from the recap page's rankings, behaves identically.
- `score-presentation`: a chip's value sits on the same line whether it is a plain number or a hide/reveal-capable MAL score.

## Impact

- `frontend/src/pages/AnimeDetailPage.tsx`, `AnimeDetailPage.css` — always-rendered Prequel/Sequel controls and their disabled styling.
- `frontend/src/components/RelatedAnimeOverlay.tsx` — English titles via `pickDisplayTitle`.
- `frontend/src/components/EntryEditorOverlay.tsx`, `EntryEditorOverlay.css`, plus a new `DateField` component — dropdown date fields, clearing, and the server's error message shown in place.
- `frontend/src/api/client.ts` — a typed API error carrying the `{ error }` body's message, replacing today's `Error("<url> responded with 400")`; every existing `catch` keeps working unchanged.
- `frontend/src/context/CompletionPromptContext.tsx` — the increment/set-watched path reports its failure instead of returning silently.
- New `frontend/src/context/ActionFailureContext.tsx` + `ActionFailureNotice` component, mounted in `frontend/src/AppShell.tsx` beside `ConnectionStatusNotice`.
- `frontend/src/pages/SeriesPage.tsx` — the "in my list" control's new open/collapse behaviour and its pressed-state derivation.
- `frontend/src/components/RankingOverlay.tsx`, `RankingOverlay.css` — close control removed, eight-row sizing. Shared by the profile and recap pages, so both change together.
- `frontend/src/components/ScoreChip.css` — value-line alignment.
- `backend/AnimeTracker.Api/Services/Detail/RelatedAnimeDto.cs`, `AnimeDetailDto.cs`, `AnimeDetailService.cs` — `EnglishTitle` on the related-anime DTO, filled from the metadata cache the media-type lookup already reads.
- `backend/AnimeTracker.Api/Services/Entries/EntryEditExceptions.cs`, `UserAnimeEntryEditService.cs`, `Controllers/EntriesController.cs` — reader-facing rejection messages; the date-order rejection moves off `ArgumentOutOfRangeException` so no `(Parameter 'request')` suffix reaches the client; `ApplyDates` also rejects a submitted date later than today.
- `backend/AnimeTracker.Api.Tests/Services/Entries/UserAnimeEntryEditServiceDateTests.cs` — new tests for the future-date rejection.
- No database migration: no stored data changes shape. `UserAnimeEntryEditRequest` and the PATCH contract are unchanged apart from message wording.

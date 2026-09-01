## 1. Backend: reader-facing rejection messages

- [x] 1.1 In `backend/AnimeTracker.Api/Services/Entries/EntryEditExceptions.cs`, add `public class EntryEditRejectedException(string message) : Exception(message)` and make `CannotCompleteUnknownEpisodeCountException`, `CannotCompleteUnknownAiredCountException`, `RewatchingNotEligibleException`, `EpisodesWatchedRequiresAiredEpisodeException`, `StatusRequiresAiredEpisodeException`, `ScoreRequiresAiredEpisodeException` and `RewatchCountRequiresAiredEpisodeException` derive from it (design D8). Keep each type's `animeId` parameter — only the base class and the message text change.
- [x] 1.2 Reword those messages for a reader: drop the `Anime {animeId}` prefix in favour of "This anime …" — e.g. `"No episode of this anime has aired yet, so episodes watched can't be set above 0."`, `"This anime's total episode count is unknown, so it can't be marked Completed."`, `"This anime is currently airing and how many episodes have aired isn't known, so it can't be marked Completed."`, `"This anime can't be set to Rewatching: {reason}."`. `EntryNotFoundException` becomes `"This anime isn't in my list."`. No message may name an id, a parameter, or a type.
- [x] 1.3 In `UserAnimeEntryEditService.cs`, replace all five `throw new ArgumentOutOfRangeException(nameof(request), "…")` with `throw new EntryEditRejectedException("…")` — lines around 165, 180, 347 (`ApplyDates`), 358 (`ApplyScore`), 377 (`ApplyRewatchCount`). This is what removes the `(Parameter 'request')` suffix from the finish-before-start message.
- [x] 1.4 In `Controllers/EntriesController.cs`, collapse the seven typed `catch` blocks into one `catch (EntryEditRejectedException ex) { return BadRequest(new { error = ex.Message }); }`, keeping `catch (AnimeNotFoundException) → NotFound()` and leaving `catch (ArgumentOutOfRangeException ex)` in place as a backstop for anything not converted. Apply the same to the DELETE action's catches where the same types appear.
- [x] 1.5 Confirm `ApplyDates`' rule is already what the spec states and leave it alone: it compares post-edit values, allows either date without the other, and allows same-day. No behaviour change here — only the exception type it throws.
- [x] 1.6 Run the backend test suite; the tests assert exception *types*, not messages, so they must pass unchanged. Build via the sdk:10.0 Docker image (`rsync` the `backend/` tree to `/private/tmp/bm-build` first — Docker cannot bind-mount `~/Documents`).

## 2. Backend: English title on related-anime rows

- [x] 2.1 In `Services/Detail/AnimeDetailService.cs`, rename `GetMediaTypesAsync` to `GetRelatedMetadataAsync` and project to a `record RelatedMetadata(string? MediaType, string? EnglishTitle)` keyed by anime id, reading `m.MediaType` and `m.EnglishTitle` from the same `AnimeMetadata` query (design D2). No new query.
- [x] 2.2 Add `string? EnglishTitle` to `Services/Detail/RelatedAnimeDto.cs` and fill it in `FromEntity` from the lookup, leaving `Title` (MAL's stored relation title) exactly as it is. Update the record's doc comment to say where the English title comes from and that a related anime with no cached metadata simply has none.
- [x] 2.3 Update `AnimeDetailDto.FromEntity`'s parameter type for the lookup dictionary and its call site in `AnimeDetailService`.
- [x] 2.4 Add `englishTitle: string | null` to `RelatedAnimeDto` in `frontend/src/api/types.ts`, beside `title`.

## 3. Client: keep the reason the server gave

- [x] 3.1 In `frontend/src/api/client.ts`, add and export `class ApiError extends Error { status: number; reason: string | null }` (design D5). Its `message` is `reason ?? \`${input} responded with ${status}\``, so anything logging `err.message` improves rather than regresses.
- [x] 3.2 Have `fetchJson` and `fetchVoid` throw it on `!res.ok`: read the body as text, `JSON.parse` inside a `try`, take `.error` when it is a non-empty string, otherwise `null`. A non-JSON or empty body must yield `reason: null`, never an exception of its own.
- [x] 3.3 Confirm the in-flight GET de-duplication is unaffected: each caller already reads its own `res.clone()`, so consuming an error body in one caller cannot starve another. Confirm `performFetch`'s reachability reporting is untouched.
- [x] 3.4 Search every existing `catch` around an API call and confirm none inspects the thrown value's shape — the change must be additive for all of them.

## 4. Client: app-wide failure notices

- [x] 4.1 Create `frontend/src/context/ActionFailureContext.tsx`: `ActionFailureProvider` holding a queue of `{ id, title, reason }`, exposing `reportFailure({ title, reason })` via `useActionFailure()`. Cap the queue at 3 (newest kept), auto-dismiss each after ~8s, and clear the queue on `pathname` change (design D6).
- [x] 4.2 Create `frontend/src/components/ActionFailureNotice.tsx` + `.css`: fixed bottom-right, `z-index: 95` (under the modal backdrop's 100, above the page), stacked newest-first, each notice carrying its title line, its reason line when there is one, and a dismiss control. `role="status"`, not `alert`. Themed from the app's existing custom properties so it reads in light and dark.
- [x] 4.3 Mount `ActionFailureProvider` in `AppShell.tsx` **above** `CompletionPromptProvider` (so the increment path can reach it) and render `<ActionFailureNotice />` beside `<ConnectionStatusNotice />`, outside `<Routes>`. Verify the two notices do not overlap — the connection bar is bottom-centre at `z-index: 90`.
- [x] 4.4 In `context/CompletionPromptContext.tsx`, replace `setEpisodesWatched`'s silent `catch { return }` with a `reportFailure` call naming the anime (`target.animeTitle`) and the reason (`err instanceof ApiError ? err.reason : null`), then return as before. The episode count must still not move — the notice is what accounts for it. Update the comment, which currently says the failure is deliberately left silent.
- [x] 4.5 In `pages/AnimeDetailPage.tsx`, give `handleAddToWatching` and `handleAddToList` a `catch` that reports through the same hook — today they have `try/finally` with no `catch`, so a rejection escapes as an unhandled promise rejection.
- [x] 4.6 Exercise it: with the backend stopped, press "+" on the dashboard carousel, on my list, and on a detail page, and confirm one notice per press naming the anime, the count unmoved, and the connection bar still doing its own job.

## 5. Client: the entry editor reports the reason

- [x] 5.1 In `components/EntryEditorOverlay.tsx`, change `handleSubmit`'s, `handleRank`'s and `handleConfirmDelete`'s `catch` to `setError(err instanceof ApiError && err.reason ? err.reason : '<existing generic message>')`, keeping each action's own generic fallback text.
- [x] 5.2 Confirm the form keeps every entered value after a rejection (it does — only `saving` and `error` change) and that the editor does not also raise an app-wide notice for the same failure.
- [x] 5.3 Loosen the editor's own pre-check to exactly the spec's rule: reject only when both dates are set and `completedAt < startedAt`; a finish date with no start date, and same-day dates, must both pass through to the server.

## 6. The date fields

- [x] 6.1 Create `frontend/src/components/DateField.tsx` + `.css` (design D3): props `{ value: string; onChange: (next: string) => void; label?: string; idPrefix: string }`, `value` in `yyyy-MM-dd` or `''`. Hold `{ year, month, day }` in local state seeded from `value`, re-seeding when `value` changes from outside.
- [x] 6.2 Emit rule: all three parts set → `onChange('yyyy-MM-dd')`; any part blank → `onChange('')`. A partly filled field is empty, not an error.
- [x] 6.3 Day list from `new Date(year, month, 0).getDate()` (31 while no year is chosen); changing month or year clamps a now-invalid day down to that month's last day rather than emitting an invalid date. Verify February 2024 offers 29 and February 2023 offers 28.
- [x] 6.4 Year list: current year down to 1970 (superseded by D12 — no future year is offered at all — plus the incoming value's own year when it falls outside that range, so a stored date is always representable). Month labels `Jan`–`Dec`. Every select carries a blank option (shown as `-`).
- [x] 6.5 Selecting the blank option in any part clears the whole field. Add a trailing `Clear` control (text, not a `✕` glyph — D15) with an accessible label naming its field ("Clear start date"), disabled while the field is already empty.
- [x] 6.6 In `EntryEditorOverlay.tsx`, replace both `<input type="date">` with `DateField`, keeping `startedAt`/`completedAt` state and `buildRequest`'s "only send what changed" comparison exactly as they are — `DateField`'s value contract is the same `yyyy-MM-dd`-or-empty string those already hold.
- [x] 6.7 In `EntryEditorOverlay.css` (and `DateField.css`), lay the field out as one row inside the editor's shared control width: each select sized to its own content (4 digits / 3 letters / 2 digits — not `flex: 1 1 0` even sharing, which overflowed), the `Today`/`Clear` controls at `flex: 0 0 auto`, the row's height matching every other control (design D4, `list-editing` "Entry editor controls share one size"). Row centered (`justify-content: center`) rather than left-packed.
- [x] 6.8 Check in Safari on macOS and in one other browser that both fields render identically, that clearing works from a dropdown and from `Clear`, and that the Dates disclosure adds no new control size to the form.
- [x] 6.9 Neither field may be set into the future (design D12): the year list stops at the current year, the month list stops at the current month once the current year is picked, and the day list stops at today's day once the current year and month are both picked; changing an earlier part re-clamps a later one that would now be future. `UserAnimeEntryEditService.ApplyDates` rejects a submitted future `StartedAt`/`CompletedAt` with `EntryEditRejectedException`, checked only against a field the request actually touches. Covered by `UserAnimeEntryEditServiceDateTests.cs`.
- [x] 6.10 Add a `Today` control per field (design D13) that sets all three parts to today's date in one action, styled from the recap page's `--family-year-*` palette at tint/outline weight (not its solid gradient-plus-glow, which read as oversized here).
- [x] 6.11 Fix vertical centering of each select's displayed value (design D14): a `display: flex` attempt on the select itself centered correctly in Chromium but broke in Safari/WebKit, which doesn't support flex/grid layout on a native select's own box. Replaced with `text-align`/`text-align-last: center` plus a `line-height` equal to the control's content-box height, zeroing vertical padding — verified in both Chromium and WebKit via Playwright.
- [x] 6.12 Add a hover state to every previously-bare control in the editor (design D15): the status/score dropdowns, episodes/rewatch-count inputs, Cancel, and the date-field parts all pick up the app's standard accent tint; Rank and Delete each get a light fill of their own colour; Save and confirmed-delete (already solid) are left unchanged on hover, matching how the recap page's own filled tabs behave.
- [x] 6.13 Change the Dates disclosure's two field wrappers from `<label>` to `<div>` (design D16): a `<label>` wrapping `DateField`'s five controls implicitly associated with only the first of them (the year select), so clicking the field's heading text or a gap between the boxes silently moved focus there, which then rendered as if permanently hovered. Each control already carries its own `aria-label`, so nothing accessible is lost. Confirmed via a before/after focus check.

## 7. Detail page: always-present prequel/sequel controls

- [x] 7.1 In `pages/AnimeDetailPage.tsx`, render the prequel and sequel controls unconditionally: a `<Link>` when the relation exists, otherwise `<button type="button" disabled>` carrying the same class plus an `--disabled` modifier (design D1). Keep the arrows and label text identical between the two forms.
- [x] 7.2 Drop `prequel`/`sequel` from the related-row wrapper's render condition — the row now always has at least those two controls.
- [x] 7.3 In `AnimeDetailPage.css`, add the dimmed treatment for `.anime-detail-page__related-link--disabled` (reduced opacity, `cursor: default`, no hover/focus treatment), sized identically to the enabled form so nothing in the row shifts. Confirm `Series`, `Main series` and `More` keep their existing come-and-go behaviour.
- [x] 7.4 Check an anime with both relations, one with only a sequel, and one with neither, and confirm the enabled controls sit in the same place in all three.

## 8. More overlay: English titles

- [x] 8.1 In `components/RelatedAnimeOverlay.tsx`, title each row with `pickDisplayTitle(relation.title, relation.englishTitle)`, importing it from `utils/anime.ts`. Nothing else about the row changes.
- [x] 8.2 Confirm the grouping, ordering, media-type line and click-to-navigate behaviour are untouched, and that a relation whose anime we hold no metadata for still shows its stored title.

## 9. Series page: "in my list" shows my list

- [x] 9.1 In `pages/SeriesPage.tsx`, extend the control's pressed state: `mineOnly && unfilteredGroups.size === 0 && at least one group open` (design D9). A freshly opened page — every group collapsed — must read as off.
- [x] 9.2 Replace `toggleMineOnly` with an apply/put-away pair over the same three state pieces: when not pressed, set `mineOnly = true`, clear `unfilteredGroups`, and write `collapsedGroups[key] = false` for every group holding at least one `entry != null` extra that `typeAdmits`, `true` for every other group; when pressed, collapse every group and leave `mineOnly` on. Move it below the early returns beside `toggleMediaType` if it needs `extrasGroups`.
- [x] 9.3 Update the comments above the `mineOnly`/`collapsedGroups`/`unfilteredGroups` declarations and above the old `toggleMineOnly`, which currently state that the control never changes a group's collapsed state.
- [x] 9.4 Verify the interactions: with "Movie" selected, pressing the control opens only groups holding a movie of mine and shows only those movies; with nothing of mine anywhere, every group stays collapsed; pressing again collapses everything; `Expand all` still turns the filter off; a group heading still exempts that one group; and back/forward restoration still works (no new state was added).

## 10. See-all overlay: eight rows, no close control

- [x] 10.1 In `components/RankingOverlay.tsx`, delete the `.ranking-overlay__buttons` footer and its Close button, adding nothing in its place — no ✕ corner control (design D10). The list becomes the last element in the overlay. Dismissal is `Modal`'s existing Esc / click-outside / route-change behaviour, which needs no change.
- [x] 10.2 In `RankingOverlay.css`, replace the list's `max-height: 60vh` with a row-derived height: eight outer row heights plus seven gaps, expressed from the existing `--ranking-poster-h` and the row's own padding/border so the two cannot drift. Keep `overflow-y: auto`. Remove the `.ranking-overlay__buttons` rules and re-check the container's bottom spacing now that nothing follows the list.
- [x] 10.3 If a padded frame is introduced around the list, put the padding on a non-scrolling wrapper — a padded scroll container shows a sliver of the next row (the trap `EditHistoryOverlay.css` documents).
- [x] 10.4 Check on the profile page (Favourite seasons and Favourite years) and on the recap page (its season and year rankings, and the two by-time-watched rankings) that exactly eight whole rows show with no ninth peeking, that the rest scrolls, that a ranking of six shows six without reserving eight, that no close control is rendered, and that Esc and a click outside both still close it.
- [x] 10.5 Leave `EditHistoryOverlay`'s own ✕ alone — its contents are interactive (search field, date bounds), so it keeps the close control the `profile-stats` spec requires of it.

## 11. Score chips align

- [x] 11.1 In `components/ScoreChip.css`, make `.score-chip__value` a flex line (`display: flex; align-items: center;`) so `.score-value` becomes a flex item and its `vertical-align: middle` stops applying (design D11). Do **not** touch `ScoreValue.css` — that property is load-bearing wherever a score sits inline in text.
- [x] 11.2 Confirm the top-anime showcase's overrides (`.top-anime-showcase__scores .score-chip__value`, `… .score-value { justify-content: flex-start }`) still do what they say under a flex parent, and adjust only if they do not.
- [x] 11.3 Check the rank 1–3 cards with scores shown, with scores hidden (reveal control), and after revealing one, plus the series page's average chips and a timeline card's compact pair — every pair's two values on one line, nothing else about the chips changed.

## 12. Verification

- [x] 12.1 Build the frontend with Node 22: `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` in `frontend/` (the default `node` is v16 and Vite fails on it).
- [x] 12.2 Build the backend against the sdk:10.0 image and run its tests (see 1.6).
- [x] 12.3 Walk each spec delta's scenarios against the running app: dimmed prequel/sequel, English titles in More, dropdown dates and clearing, a finish-before-start save reporting its real reason, a failed "+" raising a notice, the series page's "in my list" press, eight rows in See all, and the top-3 chips' alignment.
- [x] 12.4 Run `openspec validate polish-detail-dates-and-error-messages` and confirm the change is still valid before archiving.

## 1. Premiere-move wording

- [x] 1.1 In `frontend/src/components/Updates/updateText.ts`, replace the `StartDateChanged` branch's `'Moved up'` verb with `'Moved earlier'`, keeping `'Delayed'` for a later date, and update the comment to say why the up/down wording was dropped (design D1).
- [x] 1.2 Add the equal-dates guard to the same branch: where `airedFrom` and `previousStartDate` are the same date, render `Premiere moved to <date>` with no direction word and no `· was` clause.
- [x] 1.3 Check the update in the live database that prompted this (anime 53913, `PreviousStartDate = 2026-10-08`, `AiredFrom = 2026-10-01`) now reads `Moved earlier to 1 Oct 2026 · was 8 Oct 2026` in both the dropdown and the history.

## 2. Pointer cursor as a rule of the app

- [x] 2.1 Add the base rule to `frontend/src/index.css`: `button:not(:disabled)` and `[role="button"]:not([aria-disabled="true"])` take `cursor: pointer`, with a comment recording that anchors already take it natively and that every `cursor: default` / `not-allowed` override in the app sits on a `:disabled` selector (design D2).
- [x] 2.2 Add `input[type="date"] { cursor: pointer }` in the same place, with a comment that a date field is picked from rather than typed into.
- [x] 2.3 Verify the three reported controls — the navbar bell, the dropdown's History button, both history date fields — now show the pointer, and that a disabled control (a dimmed Prequel/Sequel button on a detail page, a disabled pagination arrow, the Settings buttons while a job runs) still does not.

## 3. Updates history overlay — one scroll region

- [x] 3.1 Add a shared `.modal--column` modifier to `frontend/src/components/Modal.css` (`display: flex; flex-direction: column; overflow: hidden`), commented as the "only the content scrolls" treatment, and leave `.modal--rank` untouched (design D3).
- [x] 3.2 Have `UpdatesHistoryOverlay` pass `modal--wide modal--column`, and make `.updates-history` fill the box as a flex column with `min-height: 0`.
- [x] 3.3 Make `.updates-history__list-frame` the flexible child (`flex: 1 1 auto; min-height: 0; display: flex`) and the list inside it `flex: 1 1 auto; min-height: 0`, dropping the inline `max-height`.
- [x] 3.4 Keep `.updates-history__list`'s hidden-scrollbar rules (`scrollbar-width: none`, `::-webkit-scrollbar { display: none }`) exactly as they are, and update their comment so it explains the hiding in terms of the panel-filling list rather than the three-row cap it currently refers to.
- [x] 3.5 Remove `useCappedCardHeight` from `UpdatesHistoryOverlay` and give it its own node-in-state callback ref for the `<ul>`, so `useSeenTracking` still observes the list; keep the hook itself for the navbar dropdown and update its doc comment, `LIST_GAP_PX` note and `VISIBLE_CARD_CAP` comment to name only that one caller.
- [x] 3.6 Update the comments that describe the old geometry: `useSeenTracking`'s `computeVisibleArea` note (`.modal` now clips at 85vh rather than scrolling) and its `TOLERANCE_PX` note (no longer a fractional cap on this surface).
- [x] 3.7 Re-lay the filter row so search flexes and the date range and Clear stay at their natural size on one line at the panel's width, wrapping only where the window is too narrow.
- [x] 3.8 Verify at several window heights (tall, ~800px, ~600px) and with 1, 2, 3 and many recorded updates: the panel itself never scrolls, the list is the only scroll position, a short history makes a short panel, a long one fills the height with its bottom row cut partly off as the only scroll affordance and no scrollbar drawn, and the filters stay fixed above it.

## 4. English titles for a resolved relation

- [x] 4.1 Add `string? EnglishTitle` to `backend/AnimeTracker.Api/Services/Relations/ResolvedRelationEdge.cs`, beside `Title`, documented as the far end's cached English title where one is known (design D4).
- [x] 4.2 Fill it at all three construction sites in `Services/Relations/RelationResolver.cs` — `farAnime?.EnglishTitle`, `info.EnglishTitle`, `neighbour.Anime.EnglishTitle` — with no new query, and note in the outgoing-edge branch that an uncached far end keeps its denormalised title and no English title.
- [x] 4.3 In `Services/Updates/AnimeUpdateService.cs`, compose the affiliate reason from the displayed title (English where known, MAL title otherwise) through one small helper rather than inline at the call site.
- [x] 4.4 Switch `FindAffiliateAsync`'s `.ThenBy(e => e.Title, …)` tie-break in `Services/Updates/AnimeUpdateRelevance.cs` to that same displayed title, and record in the comment that the named affiliate follows the title shown.
- [x] 4.5 Carry `EnglishTitle` through `Services/Detail/ResolvedRelationDto.cs` and mirror it on `ResolvedRelationDto` in `frontend/src/api/types.ts`.
- [x] 4.6 Use `pickDisplayTitle` for the Prequel, Sequel and parent-story tooltips in `frontend/src/pages/AnimeDetailPage.tsx`.
- [x] 4.7 Update `backend/AnimeTracker.Api.Tests/Services/Updates/AnimeUpdateServiceTests.cs` so its affiliate fixtures carry distinct MAL and English titles, and assert the reason names the English one; add a case for an affiliate with no English title falling back to the MAL title.
- [x] 4.8 Add a test that the affiliate tie-break between two candidates sharing the most specific relation follows the displayed title.

## 5. Verification

- [x] 5.1 `dotnet build` and `dotnet test` in `backend/` pass.
- [x] 5.2 `npm run lint` and `npm run build` in `frontend/` pass (Node 22 via nvm, not the default v16).
- [x] 5.3 Rebuild the running containers and confirm in the app: update 76 reads `Moved earlier`, its reason reads `Sequel to Reincarnated as a Sword`, the bell/History/date fields show the pointer, and the history overlay scrolls in one place only, with no scrollbar drawn.
- [x] 5.4 Run `openspec validate --change polish-updates-panel` and confirm the three delta specs still validate.

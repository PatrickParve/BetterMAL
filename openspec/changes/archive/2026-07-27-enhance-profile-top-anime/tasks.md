## 1. Divergence score visibility fix

- [x] 1.1 Add `IsCompleted` to `OpinionDivergenceItemDto` in `backend/AnimeTracker.Api/Services/Profile/ProfileDto.cs` and populate it from `e.Status == WatchStatus.Completed` in `ProfileService.ToDivergenceItem`
- [x] 1.2 Add `isCompleted: boolean` to `OpinionDivergenceItemDto` in `frontend/src/api/types.ts`
- [x] 1.3 Pass `completed={item.isCompleted}` to `<ScoreValue>` in `DivergenceList` (`frontend/src/pages/ProfilePage.tsx`) and verify a completed show's MAL score renders unblurred with hide-scores on and the always-show-completed setting on

## 2. Ordering persistence

- [x] 2.1 Add `Position` (int) to `TopAnimeSelection` (`backend/AnimeTracker.Api/Models/TopAnimeSelection.cs`) and update its XML doc to describe the widened meaning: an ordered preference list, not a set
- [x] 2.2 Configure the property in `AnimeTrackerDbContext` if needed and generate the EF migration adding `Position` NOT NULL default 0 (build via the `sdk:10.0` Docker image; migrations auto-apply on startup)
- [x] 2.3 Replace `ITopAnimeSelectionRepository.GetSelectedAnimeIdsAsync`/`ReplaceSelectionAsync` with ordered equivalents: read sorted by `(Position, AnimeId)`; replace writes `Position` sequentially from the given ordered list; keep the unknown-anime-id validation

## 3. Top-anime computation

- [x] 3.1 Add `TopAnimeTierDto(int Score, List<TopAnimeEntryDto> Members, int IncludedCount)` and reshape `TopAnimeSectionDto` to `(Items, Tiers, MediaType)`, removing `TieBreakSlots`/`Candidates`/`SelectedAnimeIds`
- [x] 3.2 Add a media-type scope helper (`all|tv|movie|ova|ona|special`, case-insensitive; `special` also matches `tv_special`) with parsing that rejects unknown values
- [x] 3.3 Rewrite `ProfileService.BuildTopAnimeSection` to take a scope and the stored order: filter by scope, group into descending score tiers, order each tier by `(storedPosition ?? MaxValue, Title)`, emit the 10-tier uncapped then lower tiers until 10 items, truncating the overflowing tier, and record `IncludedCount` per contributing tier
- [x] 3.4 Expose `IProfileService.GetTopAnimeSectionAsync(scope, ct)` and keep `GetProfileAsync` embedding the `all` scope section
- [x] 3.5 Implement the slot-preserving merge used on save: per tier recompute the full effective order, refill the indices held by the incoming visible ids with the incoming order, leave hidden members in place, then persist edited tiers (descending score) followed by any other previously stored ids in their old relative order

## 4. API endpoints

- [x] 4.1 Add `GET /api/profile/top-anime?mediaType=<scope>` to `ProfileController` returning a `TopAnimeSectionDto`; unknown `mediaType` → 400
- [x] 4.2 Replace `TopAnimeSelectionController`'s GET/PUT `/api/top-anime/selection` with `PUT /api/top-anime/order` taking `{ mediaType, tiers: [{ score, animeIds }] }`, running the merge from 3.5
- [x] 4.3 Validate the order request: unknown anime ids → 400 (`UnknownAnimeIdsException`), and any anime id whose current score differs from its stated tier score → 400
- [x] 4.4 Manually exercise both endpoints (all scope + one filtered scope, a reorder and a cross-cut-line swap) and confirm the order survives a restart

## 5. Frontend data layer

- [x] 5.1 Update `frontend/src/api/types.ts`: `TopAnimeTierDto`, reshaped `TopAnimeSectionDto`, and a `TopAnimeMediaType` union for the six filter values
- [x] 5.2 Replace `putTopAnimeSelection` in `frontend/src/api/client.ts` with `getTopAnimeSection(mediaType)` and `putTopAnimeOrder(mediaType, tiers)`

## 6. Profile page UI

- [x] 6.1 Hold the top-anime section in `ProfilePage` state (seeded from `profile.topAnime`) with a `mediaType` state defaulting to `'all'`, refetching the section when the filter changes and after a save
- [x] 6.2 Add the filter tabs (All / TV / Movie / OVA / ONA / Specials) to the "My top anime" header, styled after `MyListPage`'s status tabs, with matching CSS in `ProfilePage.css`
- [x] 6.3 Show the edit control whenever any tier has more than one member, and render the "No scored <type> yet." empty state for filters with no entries
- [x] 6.4 Confirm the strip's existing drag-to-scroll and click-through behaviour still works after the state change

## 7. Tier order overlay

- [x] 7.1 Rewrite `TopAnimeSelectionOverlay` as a tier order editor: one section per tier listing all members in order, with a cut line after `includedCount` when the tier is truncated, and a hint naming the active filter
- [x] 7.2 Implement reordering within a tier via native HTML5 drag-and-drop plus ↑/↓ buttons (no new dependency), keeping moves confined to their own tier
- [x] 7.3 Save all tiers for the active scope via `putTopAnimeOrder`, keeping the existing error/saving states, and refresh the section on success; update `TopAnimeSelectionOverlay.css` for the tier sections and cut line

## 8. Verification

- [x] 8.1 Build the backend (`sdk:10.0` Docker image) and the frontend (`nvm` Node v22) with no errors
- [x] 8.2 Walk the spec scenarios in the running app: ten 10s reorder and persist across reload; 10s stay above 9s; a cross-cut-line swap in a truncated tier; a movie-filtered top list; ordering set under a filter visible in All; hidden tier members keep their relative positions; completed MAL scores visible in both divergence lists

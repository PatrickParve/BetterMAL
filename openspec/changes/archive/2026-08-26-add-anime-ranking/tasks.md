## 1. The ranking rule (backend core)

- [x] 1.1 Add `Services/Ranking/RankBand.cs`: the four bands of design D2 (`HandOrdered`, `ShortForm`, `Dropped`, `Unranked`) and a `Resolve(UserAnimeEntry)` that derives one, checking Dropped before short form and using `AiredEpisodeGate.HasAired` for the aired test.
- [x] 1.2 Add `Services/Ranking/AnimeRankingKey.cs` holding the ordering rule of design D1/D3 in both forms side by side: an in-memory `Compare`/`OrderEntries` over `UserAnimeEntry` plus a stored-position map, and an `OrderByRanking` extension for `IQueryable` that expresses the same key in columns (score, band, left-joined position nulls-last, title). Document that the two must move together.
- [x] 1.3 Add `Services/Ranking/AnimeRankingSnapshot.cs`: the ranked set in order plus a `RankByAnimeId` lookup, built by a pure `Build(entries, storedOrder)` so any caller already holding the entry list can build one without touching the database.
- [x] 1.4 Add `Services/Ranking/IAnimeRankingService.cs` + `AnimeRankingService.cs`: the DB-loading wrapper (`GetSnapshotAsync`) over 1.3, plus tier projections for the editor — the per-score hand-orderable counts and one score's full hand-ordered membership, optionally narrowed by a `TopAnimeMediaTypeScope`.
- [x] 1.5 Register the ranking service in `Program.cs`.
- [x] 1.6 Tests: band resolution including the dropped-music-video case; score dominance; hand-ordered/short-form/dropped banding within one score; never-placed members falling after placed ones alphabetically; unranked entries excluded from the ranked set.
- [x] 1.7 Test that the in-memory comparator and the `IQueryable` ordering produce identical order over one shared fixture (design D3's drift guard).

## 2. The ranking store (writes)

- [x] 2.1 Rewrite `TopAnimeSelectionRepository.ReplaceOrderAsync`'s caller-facing contract into a slot-preserving single pass (design D5): walk the existing flat list, refill the slots held by edited ids with each edited tier's new sequence, leave every untouched id in its own slot, append ids that had no slot. Keep the unknown-anime-id validation.
- [x] 2.2 Move `ApplyTopAnimeOrderAsync` off `ProfileService` into the ranking service as `ApplyTierOrderAsync`, keeping the tier-score mismatch validation and the hidden-member merge, but computing each tier's effective order over hand-orderable members only.
- [x] 2.3 Add `PlaceLastInTierAsync(animeId, score)` (design D6): materialise the target score's full hand-ordered order, move the anime to its end, write it back through 2.1.
- [x] 2.4 Call 2.3 from `UserAnimeEntryEditService` whenever a save sets a score where there was none or changes it to a different value, inside the same `SaveChangesAsync`; re-saving an unchanged score must not move anything.
- [x] 2.5 Tests: a first score lands last in its tier even when every other member was never placed; a re-score moves the anime to the end of the new tier and leaves the old tier's order intact; an unchanged score moves nothing; a dropped anime keeps its stored slot through an edit of its tier and returns to it when un-dropped.
- [x] 2.6 Tests for 2.1's pass: untouched ids keep their slots, edited tiers refill exactly the slots they held, and members a scoped view hid keep their relative positions.

## 3. Reading the ranking everywhere (backend)

- [x] 3.1 Retire `Services/Profile/TopAnimeOrdering.cs`, pointing `ProfileService.BuildTopAnimeSection` at `AnimeRankingKey`, so the top-anime tiers band dropped and short-form members to the bottom of their tier.
- [x] 3.2 Add `MyRank` to `TopAnimeEntryDto` and populate it from the snapshot.
- [x] 3.3 Recompute `TopAnimeTierDto.IncludedCount` against listed (hand-orderable) members per design D8, so the cut line lands correctly and is absent when the cut falls among pinned members.
- [x] 3.4 Add `MyRank` to `MyListItemDto` and populate it in `MyListService`.
- [x] 3.5 Add `MyRank` to `RecapRowDto` and populate it in `RecapService`.
- [x] 3.6 Switch `SeasonRepository`'s `SeasonSortKey.MyScore` ordering onto `OrderByRanking` (design D3), keeping the scored/unwatched split, the `Unwatched` divider grouping, and the popularity-then-title fallback for anime the ranking does not cover; server-side paging must stay in SQL.
- [x] 3.7 Tests: profile tiers band correctly and carry ranks; my-list and recap rows carry ranks; the season/year my-score page orders by ranking and holds that order across page boundaries.

## 4. The ranking API

- [x] 4.1 Add `Controllers/RankingController.cs` with `GET /api/rankings?mediaType&score` returning `{ scores: [{ score, count }], tier: { score, members } }` per design D7, defaulting to the highest non-empty tier when `score` is omitted, and validating `mediaType` through `TopAnimeMediaTypeScope`.
- [x] 4.2 Add `PUT /api/rankings/order` on the same controller, with the payload and error handling `TopAnimeSelectionController` had.
- [x] 4.3 Delete `Controllers/TopAnimeSelectionController.cs` and `PUT /api/top-anime/order`.
- [x] 4.4 Controller tests: unknown media type and unknown score rejected; an omitted score returns the highest non-empty tier; a tier/score mismatch on save is rejected.

## 5. The ranking overlay (frontend)

- [x] 5.1 Add the new API types (`AnimeRankingTierDto`, `AnimeRankingScoreDto`, the response shape, `myRank` on the top-anime/my-list/recap row types) to `api/types.ts` and the calls to `api/client.ts`, retargeting `putTopAnimeOrder` at `/api/rankings/order`.
- [x] 5.2 Rename `components/TopAnimeSelectionOverlay.tsx`/`.css` to `components/AnimeRankOverlay.tsx`/`.css` and introduce the `mode: 'top' | 'all'` split of design D9, leaving the drag machinery, arrows, promote, and save untouched.
- [x] 5.3 Implement `all` mode: fetch its own tier, render the score selector over the non-empty scores, show each row's overall rank, draw no cut line, and fix the promote boundary at 10.
- [x] 5.4 Implement focusing (`animeId`): open on that anime's score, scroll its row into view, and mark it.
- [x] 5.5 Add `context/AnimeRankContext.tsx` exposing `openRanking({ animeId?, mediaType? })`, mount one overlay instance at the app root in `AppShell.tsx`, and verify that opening it does not re-render the page beneath (the `list-editing` overlay-independence requirement).
- [x] 5.6 Save from `all` mode through `PUT /api/rankings/order`, and refresh the profile's top-anime section when the ranking changes beneath it.
- [x] 5.7 Post-review polish: in `all` mode, replace the top-10 promote control with a jump-to-top/jump-to-bottom pair on every row (design D13) — first row jump-to-bottom only, last row jump-to-top only; `top` mode's single-step-plus-promote-at-10 controls are unchanged.
- [x] 5.8 Post-review polish: widen the editor (`.modal--rank`, `min(920px, 100%)`) so the score selector fits on one line without wrapping being the normal case; move Close (and, in `top` mode, "Rank my whole library") to the header; move the how-to-reorder hint into a "?" tooltip next to the title, with the scope label folded into that same tooltip; make only the anime list scroll, not the whole box.
- [x] 5.9 Post-review polish (design D14): move the header inside the same scrollable box as the list, as its first item; while a row is dragged, scroll it out of view by exactly its own height (revealing the top-ranked rows beneath it) instead of resizing/reflowing it, and scroll back once the drag ends — a resize was found to visibly jump the dragged row out from under the pointer.
- [x] 5.10 Post-review polish (design D12): remove the Save/Cancel pair in favor of auto-save — every reorder schedules a debounced, serialized save; closing the editor (Close, Escape, or backdrop click) flushes a not-yet-saved reorder first and blocks the close if that flush fails, showing the error with a Retry.

## 6. Ways in (frontend)

- [x] 6.1 `ProfilePage.tsx`: rename the "Edit order" control to **Rank**.
- [x] 6.2 Add the whole-library action to the `top`-mode overlay, calling `openRanking()`.
- [x] 6.3 `EntryEditorOverlay.tsx`: add the Rank action, shown only for a hand-orderable entry, opening on the saved score.
- [x] 6.4 `CompletionScoreOverlay.tsx`: add **Save and rank** beside Skip and Save, offered only while the chosen score leaves the entry hand-orderable; on success save by the existing path, close, and call `openRanking({ animeId })`; on failure do not open.
- [x] 6.5 `CompletionPromptContext.tsx`: carry the entry's status and media type into the prompt so 6.4 can decide whether to offer the action, and keep the caller's view refresh firing when the prompt closes rather than when the ranking editor does.

## 7. Reading the ranking everywhere (frontend)

- [x] 7.1 `utils/anime.ts`: add `myRank` to `SortableListItem` and insert rank into the comparator chain wherever My score is the primary or the tiebreaker key — after the chosen tiebreaker, before the alphabetical fallback, always in its natural direction, unranked last.
- [x] 7.2 `RecapPage.tsx`: break top-10 ties by rank when the basis is my score, leaving the MAL basis on title.
- [x] 7.3 `ScoreBoardOverlay.tsx`: order each slot by rank, unranked last by title.
- [x] 7.4 Confirm `MyListPage.tsx` needs no change beyond 7.1, and that its `#` numbers stay positional rather than becoming overall ranks.

## 8. Verification

- [x] 8.1 Run the backend test suite and the frontend build (Node 22 via nvm, per the project's build note).
- [x] 8.2 Walk the flow end to end in the app: complete an anime, score it, save and rank, place it, then confirm the same order in my list sorted by my score, in the profile top strip, in a recap top 10, and on the score board.
- [x] 8.3 Check the two edge cases by hand: a dropped anime sinking to the bottom of its tier and returning to its slot when un-dropped, and a tier whose cut line falls among pinned members drawing no cut line.

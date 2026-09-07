## 1. The model (design D1, D2, D3)

- [x] 1.1 In `Models/AnimeMetadata.cs`, add `public string? SelectedPictureUrl { get; set; }` above `PictureUrl`, and `public DateTimeOffset? SelectedPictureModifiedAt { get; set; }` beside it. Comment the pair: the chosen picture, null meaning "no choice; follow MAL", and the time that choice was last set **or cleared**, null meaning it has never been either (spec `artwork-selection` "Every presentation choice carries the time it was made").
- [x] 1.2 In the same file, add `public void ResolvePictureUrl() => PictureUrl = SelectedPictureUrl ?? MalPictureUrl;` next to `ResolveTotalEpisodes()`, with the same doc-comment shape: the only writer of `PictureUrl`, called by whichever writer just touched `SelectedPictureUrl` or `MalPictureUrl`; the choice wins when there is one.
- [x] 1.3 Rewrite the `PictureUrl`/`MalPictureUrl` comment block (`:19-21`). It currently says "the two differ exactly when a picture has been chosen (Services/Artwork/AnimePicture)" — that inference is what this change retires. It now reads as the effective value derived from the chosen picture and MAL's, in the same shape the `TotalEpisodes` trio below it already documents.
- [x] 1.4 Update the class doc comment's "PictureUrl and TotalEpisodes are each an effective value beside the source value(s) they derive from — MAL's picture until one is chosen…" to name the actual source pair for the picture, so the two derived columns read alike.
- [x] 1.5 In `Models/Series.cs`, add `SelectedTitleModifiedAt` and `SelectedPictureModifiedAt` beside the two existing choice fields, and extend the "Survive a rebuild" comment: a choice and its time move together and a rebuild never restamps either (spec `series-identity`, design D5).
- [x] 1.6 Confirm no `AnimeTrackerDbContext` configuration is needed for the four new columns — nullable `string`/`DateTimeOffset` map by convention, and neither entity block (`:33`, `:194`) configures per-column facets today. Record the check rather than skipping it.

## 2. The migration (design D8)

- [x] 2.1 Scaffold an EF migration `StoreArtworkChoicesWithTimestamps` in the `sdk:10.0` Docker image (the local SDK is 9.0; mind the `~/Documents` bind-mount caveat). The four `AddColumn` calls are all the scaffold should produce.
- [x] 2.2 Hand-write into `Up`, after the columns: `UPDATE "AnimeMetadata" SET "SelectedPictureUrl" = "PictureUrl", "SelectedPictureModifiedAt" = now() WHERE "PictureUrl" IS DISTINCT FROM "MalPictureUrl";`. Comment that `IS DISTINCT FROM` is the exact SQL reading of the C# `!=` being retired — it leaves the two rows where both columns are null alone, as `!=` did — and that copying the value the row already holds re-derives the same displayed picture, so no `PictureUrl` write is needed. Expected: 190 rows.
- [x] 2.3 Then `UPDATE "Series" SET "SelectedTitleModifiedAt" = now() WHERE "SelectedTitle" IS NOT NULL;` and the same for `"SelectedPictureUrl"`/`"SelectedPictureModifiedAt"`. Expected: 22 and 96 rows. Comment why every existing choice is stamped rather than left null: it keeps null meaning exactly one thing — never chosen, never cleared — and puts every pre-existing choice before everything chosen after the migration on that database (spec `data-persistence` "The chosen-picture migration preserves every displayed picture and stamps every existing choice").
- [x] 2.4 `Down`: drop the four columns only. Comment that this is lossless for artwork — `PictureUrl` still holds the displayed picture and `MalPictureUrl` MAL's, so the retired comparison reads exactly as it does today; only the recorded times are gone.
- [x] 2.5 Regenerate the model snapshot and confirm the diff is the four new properties and nothing else.

## 3. Writing a choice (design D4, D6)

- [x] 3.1 In `Services/Artwork/ArtworkSelectionService.cs`, `SetAnimePictureAsync`: keep both rejections (not in my list; URL outside `AnimePicture.Options`), then write `anime.SelectedPictureUrl = pictureUrl`, `anime.SelectedPictureModifiedAt = DateTimeOffset.UtcNow`, and `anime.ResolvePictureUrl()`. Delete the "Setting MAL's own main picture *is* the clear" comment and the behaviour it describes — picking MAL's own picture now pins it (design D6). Return `anime.PictureUrl` as today.
- [x] 3.2 `ResetAnimePictureAsync`: write `SelectedPictureUrl = null`, stamp `SelectedPictureModifiedAt`, and `ResolvePictureUrl()` instead of copying `MalPictureUrl` into `PictureUrl`. Comment that clearing is now the only way to remove a choice, and that it stamps because a clear must be able to outrank an earlier set on another device.
- [x] 3.3 `SetSeriesTitleAsync` / `ResetSeriesTitleAsync` — stamp `SelectedTitleModifiedAt` on both paths. `SetSeriesPictureAsync` / `ResetSeriesPictureAsync` — stamp `SelectedPictureModifiedAt` on both. Take `DateTimeOffset.UtcNow` at the point of the write, matching the convention everywhere else in the codebase (design D4: no clock abstraction exists and this change does not introduce one).
- [x] 3.4 Add a class-level comment recording the invariant these six methods now carry: they are the **only** writers of the three timestamps, which is what makes "stamped when the change is made, never when it is later read, exported or rebuilt" true by construction (spec `artwork-selection`).
- [x] 3.5 Return the chosen value alongside the displayed one: `SetAnimePictureAsync`/`ResetAnimePictureAsync` and the four series methods keep their current return types where they already return the stored choice; where a controller needs both (task 6.2) read them off the tracked entity after the save rather than re-querying.

## 4. Reading a choice (design D9)

- [x] 4.1 In `Services/Artwork/AnimePicture.cs`, `IsOverridden` becomes `anime.SelectedPictureUrl is not null`. Rewrite its doc comment: chosen-ness is stored, not derived, because the old comparison was only meaningful inside the one database that made both writes.
- [x] 4.2 In the same file, `Options` takes its second identity-authoritative upsert from `anime.SelectedPictureUrl` rather than `anime.PictureUrl` — set-equivalent, since the displayed value is always one of the two already upserted, and it now names what it means. Leave the `PictureIdentity` de-duplication and MAL's ordering untouched; update the doc comment's reference to `IsOverridden` accordingly.
- [x] 4.3 In `Services/Mal/MalMappingExtensions.cs` `ApplyTo` (`:100-102`), delete the `wasOverridden` read and the conditional write, leaving `target.MalPictureUrl = node.MainPicture?.Large ?? node.MainPicture?.Medium; target.ResolvePictureUrl();`. Replace the "single guard for the whole app" comment with what is now true: there is no ordering hazard and no guard, only the same re-derive obligation `MalTotalEpisodes` already carries.
- [x] 4.4 Same treatment in `ApplyLeanTo` (`:186-188`), keeping its note that lean nodes never carry `pictures`, so `PictureUrls`/`PicturesSyncedAt` stay untouched.
- [x] 4.5 Leave `ApplyPictureSetTo` alone — the picture *set* has never had anything to do with chosen-ness.
- [x] 4.6 Confirm nothing else writes `MalPictureUrl` or `PictureUrl` on a persisted row: the only remaining hits are `Services/Series/SeriesService.cs:420` (a transient, never-saved `AnimeMetadata` built from a relation snapshot for display) and `Services/Search/AnimeSearchService.cs:330` (a `SearchCandidate` record, not the entity). Record the check.

## 5. Carrying a choice through a rebuild (design D5)

- [x] 5.1 In `Services/Series/SeriesGraphBuilder.cs` (`:1116-1122`), extend the absorption fill-in so each `??=` moves the choice **and** its timestamp from the same source series. Take both from one `FirstOrDefault(...)` result rather than two independent scans, so a title and its time can never come from different series.
- [x] 5.2 In `PersistReRootedAsync` (`:1220-1236`), copy `SelectedTitleModifiedAt`/`SelectedPictureModifiedAt` off the matched row beside the two choices, and set them on the re-inserted `SeriesEntity`. Extend the method's doc comment — the thing a crash between the two saves must not lose is the chosen title and picture **and the times they were chosen**.
- [x] 5.3 Comment the rule once, where the absorption happens: adopting or carrying an existing choice is not making one, so `UtcNow` never appears on either path (spec `series-identity` "A rebuild does not restamp a choice").

## 6. The API surface (design D7)

- [x] 6.1 In `Services/Detail/AnimeDetailDto.cs`, add `string? SelectedPictureUrl` after `MalPictureUrl`, map it from `anime.SelectedPictureUrl`, and extend the "Feeds the picture picker" comment: the client needs the choice itself, not just the displayed URL, to know whether there is anything to clear. Do **not** expose any timestamp — nothing in the UI reads one (design non-goal).
- [x] 6.2 `Controllers/AnimeDetailController.cs` — `SetPicture` and `ResetPicture` return `{ pictureUrl, selectedPictureUrl }`; `RefreshPictures` (`:77`) returns `selectedPictureUrl` alongside its existing `pictureUrl`/`pictureUrls` so a backfill response updates the same shape.
- [x] 6.3 `Controllers/SeriesController.cs` — `SetPicture`/`ResetPicture` return `{ pictureUrl, selectedPictureUrl }`; `SetTitle`/`ResetTitle` keep returning the title, which already **is** the stored choice.
- [x] 6.4 Leave `SeriesDto` as it is — it already carries `SelectedTitle` and `SelectedPictureUrl`.

## 7. Backend tests

- [x] 7.1 `Services/Artwork/ArtworkSelectionServiceTests.cs` — rewrite `SetAnimePictureAsync_ChoosingMalMainPictureLeavesAnimeUnchosen` as `…PinsIt`: picking MAL's main picture stores it as the choice and the anime is recorded as chosen. Seed overrides via `SelectedPictureUrl` rather than by making the two picture columns differ.
- [x] 7.2 Add to the same file: a set stamps `SelectedPictureModifiedAt` within the window the test spans; a reset clears the choice, re-derives the displayed picture to MAL's, and stamps a time no earlier than the set before it; choosing the same picture twice re-stamps; and the three series methods each stamp their own timestamp on set and on clear.
- [x] 7.3 `Services/Mal/MalMappingExtensionsTests.cs` — `OverriddenRowSurvivesApplyTo`/`ApplyLeanTo` seed `SelectedPictureUrl` instead of a differing `PictureUrl`, and additionally assert the displayed picture is still the chosen one and `SelectedPictureModifiedAt` was not written. `UnoverriddenRowFollowsMalMainPictureThroughApplyTo`/`ApplyLeanTo` keep their behaviour and gain an assertion that `SelectedPictureUrl` is still null.
- [x] 7.4 `Services/Artwork/AnimePictureTests.cs` and `Services/Artwork/SeriesPicturePoolTests.cs` — update the seed helpers to set `SelectedPictureUrl` (and `PictureUrl` to what the resolver would produce) where they express a chosen picture today; confirm the "a choice MAL no longer lists stays in the options" case still passes unchanged.
- [x] 7.5 `Services/Detail/AnimeDetailServiceTests.cs` (`:300-308`) — assert the DTO carries `SelectedPictureUrl`, null for an anime with no choice and the chosen URL for one with.
- [x] 7.6 Add a mapping test for the invariant itself: after a full and a lean upsert, with and without a stored choice, `PictureUrl == SelectedPictureUrl ?? MalPictureUrl`.
- [x] 7.7 Add a `SeriesGraphBuilder` test that an absorbed series' chosen title arrives with the absorbed series' own timestamp, not the rebuild's; and one that a re-root carries both choices and both timestamps unchanged.
- [x] 7.8 Run the full suite in the `sdk:10.0` container and fix any fixture that seeded an override by making the two picture columns differ.

## 8. Frontend (design D7)

- [x] 8.1 `api/types.ts` — add `selectedPictureUrl: string | null` to the anime detail shape beside `malPictureUrl` (`:881`).
- [x] 8.2 `api/client.ts` — `setAnimePicture`, `resetAnimePicture`, `setSeriesPicture`, `resetSeriesPicture` return `{ pictureUrl, selectedPictureUrl }`; `refreshAnimePictures` gains `selectedPictureUrl`. Delete the comment at `:320-322` saying MAL's own picture "*is* the clear" and replace it with the new rule: picking MAL's picture pins it, and only the reset clears.
- [x] 8.3 `components/PicturePickerOverlay.tsx` — add optional `onClear` and `clearLabel` props. Render the clear button in the button row beside Close, only when `onClear` is supplied; clicking it clears and closes, mirroring `handlePick`. Comment that its presence is the client's only signal that a choice is stored (spec `artwork-selection` "The picture picker").
- [x] 8.4 `components/PicturePickerOverlay.css` — style the clear button as a secondary action in the existing button row; no new layout.
- [x] 8.5 `pages/AnimeDetailPage.tsx` — `animePictureOptions` (`:140`) takes `[detail.malPictureUrl, detail.selectedPictureUrl]` as its canonical pair. `handlePickAnimePicture` (`:262`) patches `selectedPictureUrl` alongside `pictureUrl`. Add `handleClearAnimePicture` calling `resetAnimePicture`, patching both fields from the response optimistically as the pick handler does. Pass `onClear`/`clearLabel="Use MAL's picture"` only when `detail.selectedPictureUrl != null`.
- [x] 8.6 `pages/SeriesPage.tsx` — add `handleClearSeriesPicture` calling `resetSeriesPicture` and patching `pictureUrl`/`selectedPictureUrl` (the existing `handlePickSeriesPicture` at `:610` already patches both). Pass `onClear`/`clearLabel="Follow the root anime"` only when `series.selectedPictureUrl != null`, keeping the existing `note`.
- [x] 8.7 Leave `SeriesTitlePickerOverlay` untouched — no clear control for the title (proposal non-goal).
- [x] 8.8 Build the frontend with nvm's Node v22 (the default v16 cannot run Vite).

## 9. Apply and verify (design D8, migration plan)

- [x] 9.1 Apply the migration against the live database and confirm the three counts: 190 anime with a non-null `SelectedPictureUrl`, 96 series with a stamped picture choice, 22 with a stamped title choice.
- [x] 9.2 Verify the invariant in SQL: zero rows where `"PictureUrl" IS DISTINCT FROM COALESCE("SelectedPictureUrl", "MalPictureUrl")`, and zero rows carrying a chosen value with no timestamp.
- [x] 9.3 In the running app: open an anime with a chosen picture, confirm the clear control appears, clear it, confirm the picture reverts to MAL's and the control disappears; re-pick a picture and confirm the control returns.
- [x] 9.4 Pick MAL's own main picture for an anime that had no choice, and confirm it is now recorded as chosen (the clear control appears) rather than treated as a reset — the behaviour change this whole change turns on.
- [x] 9.5 On a series with a chosen picture, confirm the picker's clear control reads "Follow the root anime" and that using it returns the series to its root member's picture.
- [x] 9.6 Trigger a series rebuild on a series with a chosen title and picture and confirm both, and both timestamps, are unchanged afterwards.

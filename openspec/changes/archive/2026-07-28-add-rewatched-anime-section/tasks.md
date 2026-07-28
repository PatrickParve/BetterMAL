## 1. Backend: rewatched section

- [x] 1.1 Add `RewatchedEntryDto(AnimeId, Title, EnglishTitle, PictureUrl, RewatchCount, MyScore)` with nullable `MyScore`, and `RewatchedSectionDto(Items, MediaType)` to `Services/Profile/ProfileDto.cs`
- [x] 1.2 Add a `Rewatched` property to `ProfileDto` for the default "all" scope, documenting that the section is ordered by rewatch count and carries no tiers
- [x] 1.3 Add `Task<RewatchedSectionDto> GetRewatchedSectionAsync(string mediaType, CancellationToken ct = default)` to `IProfileService`
- [x] 1.4 Implement `BuildRewatchedSection(entries, mediaType)` in `ProfileService`: keep entries with `RewatchCount > 0` matching the scope via `TopAnimeMediaTypeScope.Matches`, order by `RewatchCount` desc, then `MyScore` desc with unscored last, then `Title` (ordinal-ignore-case) asc, with no cap
- [x] 1.5 Wire `BuildRewatchedSection(entries, TopAnimeMediaTypeScope.All)` into `GetProfileAsync`, and implement `GetRewatchedSectionAsync` to load entries and build the requested scope
- [x] 1.6 Add `GET /api/profile/rewatched?mediaType=` to `ProfileController`, validating with `TopAnimeMediaTypeScope.IsValid` and returning the same `BadRequest(new { error = ... })` shape as the top-anime endpoint, with an XML doc comment matching the file's style
- [x] 1.7 Build the backend (dotnet sdk:10.0 Docker image) and confirm it compiles clean

## 2. Frontend: API surface

- [x] 2.1 Add `RewatchedEntryDto` and `RewatchedSectionDto` types to `api/types.ts`, reusing `TopAnimeMediaType` for the scope, with `myScore: number | null`
- [x] 2.2 Add `rewatched: RewatchedSectionDto` to the `ProfileDto` type
- [x] 2.3 Add `getRewatchedSection(mediaType: TopAnimeMediaType)` to `api/client.ts`, mirroring `getTopAnimeSection`

## 3. Frontend: drag-scroll extraction

- [x] 3.1 Extract the top strip's drag handlers and refs from `ProfilePage.tsx` into a local `useDragScroll` hook returning the scroll ref, the mouse handlers, and the click-suppression handler
- [x] 3.2 Switch the "My top anime" strip onto the hook and verify drag-to-scroll, click-after-drag suppression, and plain navigation clicks all still work

## 4. Frontend: Most rewatched section

- [x] 4.1 Add `rewatched` and `rewatchedMediaType` state to `ProfilePage`, seeding `rewatched` from the profile payload and defaulting the filter to `all`
- [x] 4.2 Add a `loadRewatchedSection(type)` fetch + `selectRewatchedMediaType(type)` handler following the existing top-anime pattern (swallow errors, leave the box as-is)
- [x] 4.3 Add a `REWATCHED_EMPTY_MESSAGES` map keyed by media type with the exact per-scope copy ("No shows have been rewatched", "No TV shows have been rewatched", "No movies have been rewatched", "No OVAs have been rewatched", "No ONAs have been rewatched", "No specials have been rewatched")
- [x] 4.4 Render the "Most rewatched" `profile-box` directly below the "My top anime" box: heading with no edit control, `profile-media-tabs` reusing `MEDIA_TYPE_TABS`, and the strip or the scope's empty message
- [x] 4.5 Render each tile as a `Link` to `/anime/:id` with the poster (or placeholder), non-draggable image, alt text from `pickDisplayTitle`, and the rewatch-count badge in the top strip's badge position

## 5. Styling

- [x] 5.1 Add `.rewatched-strip` styles to `ProfilePage.css` matching the top strip (gap, `overflow-x: auto`, `cursor: grab`/`grabbing`, `user-select: none`, thin scrollbar)
- [x] 5.2 Give `.rewatched-strip__item` `flex: 0 0 calc((100% - 9 * <gap>) / 10)` so ten tiles span the box at the top strip's size and further tiles overflow into horizontal scroll
- [x] 5.3 Add `.rewatched-strip__picture`, its `--placeholder` variant, and `.rewatched-strip__count` badge styles mirroring the top strip's tile and score badge

## 6. Verification

- [x] 6.1 Build the frontend with node v22 (nvm) and confirm the Vite build and typecheck pass
- [x] 6.2 Run the app and check each spec scenario: ordering by rewatch count, zero-count entries excluded, a non-completed rewatched entry still listed, tie-break order, scrolling to the last tile past ten, per-scope empty messages, filter independence from "My top anime", filter reset after navigating away and back, and tile navigation
- [x] 6.3 Run `openspec validate add-rewatched-anime-section --strict` and fix anything it reports

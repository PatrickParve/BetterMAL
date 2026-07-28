## Context

The profile page renders read-only boxes computed server-side from cached Postgres data (`ProfileService` → `ProfileDto`). "My top anime" already does almost everything the new section needs: it has a media-type filter backed by `GET /api/profile/top-anime?mediaType=`, an embedded default ("all") section in the profile payload, and a horizontal poster strip with drag-to-scroll in `ProfilePage.tsx`.

The rewatch data already exists: `UserAnimeEntry.RewatchCount` is persisted, synced from MAL, and editable in the entry editor. It surfaces today only as a single count in the stats box (`Rewatched: entries.Count(e => e.RewatchCount > 0)`).

The difference from top anime is that "Most rewatched" has no tiers, no persisted ordering, no cut line and no cap — it is a straight sort over a filtered set.

## Goals / Non-Goals

**Goals:**
- Surface every rewatched entry, most-rewatched first, under "My top anime" in a visually matching strip.
- Reuse the existing media-type filter vocabulary and matching rules exactly.
- Make the strip genuinely scroll to the end of an arbitrarily long list.
- Keep the section server-computed and cache-only — no MAL calls, no schema change.

**Non-Goals:**
- Reordering, tie-break selection, or any persisted preference for this section.
- Changing "My top anime" behavior, its DTOs, or its ordering.
- Sharing filter state between the two boxes, or persisting either across visits.
- A dedicated full-page view of rewatched anime.

## Decisions

### Mirror the top-anime endpoint shape rather than generalizing it

New `GET /api/profile/rewatched?mediaType=` alongside the existing top-anime endpoint, plus a `Rewatched` section on `ProfileDto` holding the "all" scope so the first paint needs one request. `IProfileService` gains `GetRewatchedSectionAsync(string mediaType, ...)`, and `ProfileController` validates `mediaType` with the existing `TopAnimeMediaTypeScope.IsValid`, returning the same `400 { error }` shape on an unknown scope.

*Alternative considered:* one parameterized `/api/profile/section?kind=` endpoint. Rejected — the two sections return different payloads (tiers vs. a flat list) and the union type would leak into the frontend for no gain.

### New DTOs, not reuse of `TopAnimeEntryDto`

`RewatchedEntryDto(AnimeId, Title, EnglishTitle, PictureUrl, RewatchCount, MyScore)` and `RewatchedSectionDto(Items, MediaType)`. `TopAnimeEntryDto.MyScore` is non-nullable because that list is scored-only; rewatched entries may be unscored, so `MyScore` here is `int?`. `RewatchCount` rides along because the tile badge shows it.

### Ordering and tie-break

`RewatchCount` descending, then `MyScore` descending with unscored entries last, then `Title` ordinal-ignore-case ascending. Sorting happens in `ProfileService` over the entries already loaded by `entryRepository.GetAllAsync` — same access pattern as every other section, so no new query. Titles are compared with the raw `Title` (matching how `TopAnimeOrdering` sorts tier members) rather than the display title, so the order does not shift with title-preference display logic.

*Alternative considered:* tie-break by most recently updated. Rejected — no per-entry "last rewatched" timestamp exists, and `LastSyncedAt` reflects sync activity, not my behavior.

### Uncapped strip needs fixed-width tiles

`.top-anime-strip__item` uses `flex: 1 1 0`, so its tiles divide the available width and the strip effectively never overflows. That is wrong for a list meant to run past the edge. The rewatched tiles get `flex: 0 0 calc((100% - 9 * <gap>) / 10)` instead: exactly ten tiles span the box at the same size as the top strip's default, and the eleventh onward overflow into horizontal scroll. Everything else (gap, aspect ratio, radius, badge placement, `cursor: grab`) is shared with the top strip's look.

*Alternative considered:* a hard pixel width per tile. Rejected — it would not track the top strip's size across the page-width changes the browse grid already handles.

### Drag-to-scroll is extracted, not duplicated

`ProfilePage.tsx` currently holds the drag handlers for the top strip in component-local refs. The rewatched strip needs the same behavior over its own scroll container, so the four handlers plus their ref state move into a small local hook (`useDragScroll`) returning `{ ref, handlers, onItemClick }`, and both strips consume it. This is a refactor of existing code with no behavior change to the top strip.

### Independent filter state, both defaulting to All

A second `useState<TopAnimeMediaType>('all')` in `ProfilePage`, with its own fetch on tab change following the existing `loadTopAnimeSection` pattern (set state optimistically, swap contents on resolve, swallow errors so the box just stays as-is). The `TopAnimeMediaType` union is reused as-is rather than aliased.

### Empty-state copy is a per-scope lookup

The wording is irregular ("No shows…", "No TV shows…", "No movies…", "No OVAs…"), so it is a literal map keyed by media type rather than a template built from the tab label. The map lives next to `MEDIA_TYPE_TABS` in `ProfilePage.tsx`. The empty state renders whenever the resolved section has zero items — the same condition the top-anime box uses.

## Risks / Trade-offs

- **A user with hundreds of rewatched entries makes one long strip** → Acceptable: the payload is small (five scalar fields per entry) and tiles are plain images; the section is explicitly specified as uncapped, and the browser only decodes posters as they scroll into view.
- **Extracting the drag handlers touches working top-anime code** → The extraction is mechanical and the top strip's behavior is covered by the existing spec scenarios; verify drag, click-after-drag suppression, and plain click still work on both strips.
- **Two sections now fetch on every filter tab click** → Independent state means a click on one box issues one request, unchanged from today; both endpoints read the same already-cached entry set.
- **`RewatchCount` can be edited without a MAL round-trip** → The section reflects local state immediately, which may briefly disagree with MAL until sync runs. This matches every other profile box, so no special handling.

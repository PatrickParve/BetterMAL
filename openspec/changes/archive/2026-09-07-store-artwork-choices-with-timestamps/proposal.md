## Why

Whether I have chosen a picture for an anime is not stored anywhere. It is inferred, every time it is needed, from two columns disagreeing: `Services/Artwork/AnimePicture.cs` reads `IsOverridden(anime) => anime.PictureUrl != anime.MalPictureUrl`. That comparison is only meaningful inside one database, because both columns there were written by the same machine at the same moment. On another machine MAL's main picture may have been cached at a different time, so the same pair of values can read as "chosen" on one device and "not chosen" on the other, and a cleared choice can arrive on the other device looking like a deliberate one.

Nor does any choice carry a time. Two devices that each hold a version of the same choice have nothing to order them by, so there is no way to say which one is the later — the thing any comparison between devices has to decide first.

**Verified against the code and the live database** (2026-09-06):

- `Services/Artwork/AnimePicture.cs:10` — `IsOverridden` is the whole of the stored-choice notion for anime. There is no flag.
- `Services/Mal/MalMappingExtensions.cs:100-102` (`ApplyTo`) and `:186-188` (`ApplyLeanTo`) each read `wasOverridden` **before** writing either picture column and only let `PictureUrl` follow MAL when the row was not overridden. Its own comment names it "the single guard for the whole app… A second writer of `PictureUrl` outside this guard would defeat it."
- `Services/Artwork/ArtworkSelectionService.cs:25` writes `anime.PictureUrl`; `:35` clears a choice by writing `PictureUrl = MalPictureUrl`. Those two lines and the two guards above are the only writers of `PictureUrl` on a persisted row.
- `Models/Series.cs` — `SelectedTitle` and `SelectedPictureUrl` are already stored choices, null by default, cleared by writing null. They carry no timestamp either.
- Live data: 14,089 anime rows, of which **190** have `PictureUrl` distinct from `MalPictureUrl`. All 190 are in my list and all 190 already have a stored picture set; every one of those chosen URLs is still a member of its own stored set, and none of the 190 differ from MAL's main picture by file extension alone. 256 series rows, of which **96** carry a chosen picture and **22** a chosen title — 105 carrying one or the other.
- No export or cross-device sync exists yet. This change stores what such a sync would need to compare; it does not build the comparison.

## What Changes

**A chosen picture becomes a column of its own**

- `AnimeMetadata` gains `SelectedPictureUrl`, null meaning "no choice, follow MAL". `PictureUrl` stays exactly what it is — the displayed picture every surface reads — and becomes a derived value in the shape `TotalEpisodes` already uses: a single `ResolvePictureUrl()` re-derives it as `SelectedPictureUrl ?? MalPictureUrl`, and every writer of either source column calls it in the same write.
- Because the displayed column keeps its name and its meaning, none of the ~40 read sites, projections, or DTOs that render `PictureUrl` change at all.
- `AnimePicture.IsOverridden` becomes `anime.SelectedPictureUrl is not null`. The two `MalMappingExtensions` guards stop being guards: each writes `MalPictureUrl` and then calls `ResolvePictureUrl()`, so the read-before-write ordering hazard that comment warns about disappears rather than being restated.

**Every choice carries the time it was made**

- Three timestamp columns: `AnimeMetadata.SelectedPictureModifiedAt`, `Series.SelectedTitleModifiedAt`, `Series.SelectedPictureModifiedAt`. Each is set whenever its choice is set **or cleared**, so "cleared at 10:05" can outrank "chosen at 10:04" when the two devices are eventually compared.
- The stamp is taken at the moment the choice is made, from the request that makes it. Nothing later — no export, no sync, no rebuild — restamps it. A choice made three weeks ago keeps its three-week-old time.
- A series rebuild that carries choices across a re-root, or that adopts an absorbed series' choice, carries each choice's timestamp with it unchanged. Adopting an existing choice is not making one.

**Picking MAL's own picture is a choice, not a clear** — **BREAKING**

- Today, choosing the picture MAL calls its main picture *is* the reset: it makes the two columns equal again, which is what "not overridden" means. That stops being true. Picking MAL's current main picture stores it as the chosen picture, pinning it, so a later MAL change of main picture no longer moves that anime's displayed picture.
- Clearing becomes an explicit act, and the only one: it writes null and stamps the time.
- A stored choice is still never re-validated away — a chosen picture MAL has since dropped is kept, displayed, and offered in the picker as the current selection.

**The picker gains the control that clears**

- The shared picture picker gains a clear button, rendered only when a choice is actually stored, so it doubles as the sign that one is. On an anime it reads "Use MAL's picture"; on a series, whose default is its root member's displayed picture rather than anything of MAL's, it reads "Follow the root anime". Both clear the stored choice to null.
- `AnimeDetailDto` gains `selectedPictureUrl` so the anime picker can tell a choice from a coincidence; `SeriesDto` already carries it. The four set/reset endpoints return the chosen value alongside the displayed one.

**The stored choices are backfilled, not re-picked**

- One migration copies each of the 190 differing anime's current `PictureUrl` into `SelectedPictureUrl` and leaves the rest null, which re-derives exactly the displayed picture each of those rows already has.
- Every choice that exists at migration time — those 190, plus the 96 series pictures and 22 series titles — is stamped with the migration's own run time. A backfilled choice is therefore always older than anything chosen afterwards on that device, and a null timestamp comes to mean exactly one thing: no choice has ever been made or cleared here.

**Not in scope**

- No export, import, or cross-device comparison. This change makes the data comparable; deciding which side wins is a later change.
- No timestamp is exposed by any endpoint. Nothing in the UI reads one yet.
- The series **title** picker gets no clear control. Clearing a chosen title is supported by the API today and stays that way; only the picture picker was asked for.

## Capabilities

### New Capabilities

None. This changes how existing choices are stored and what one of them means.

### Modified Capabilities

- `artwork-selection`: "An anime SHALL be treated as having a chosen picture exactly when its displayed picture differs from MAL's main picture" is replaced by a stored chosen picture with a null default. The requirement "Choosing MAL's own picture clears the choice" is **replaced** by its opposite — picking MAL's picture pins it, and clearing is an explicit act with its own control in the picker. A new requirement says every choice carries the time it was made or cleared.
- `data-persistence`: the AnimeMetadata requirement gains the chosen-picture column and drops "No separate flag SHALL be stored for it"; the series-overrides requirement gains the two timestamps; a new requirement covers this migration's backfill and its stamp.
- `series-identity`: the rebuild rules gain that a carried or adopted choice keeps its own timestamp rather than being restamped by the rebuild.

## Impact

**Backend**

- `Models/AnimeMetadata.cs` — `SelectedPictureUrl` and `SelectedPictureModifiedAt` added; `PictureUrl`'s comment rewritten as a derived value; `ResolvePictureUrl()` added beside `ResolveTotalEpisodes()`.
- `Models/Series.cs` — `SelectedTitleModifiedAt` and `SelectedPictureModifiedAt` added.
- `Services/Artwork/AnimePicture.cs` — `IsOverridden` reads the column; `Options` swaps `PictureUrl` for the chosen value where it needs the identity-authoritative literal.
- `Services/Mal/MalMappingExtensions.cs` — both `wasOverridden` guards replaced by `ResolvePictureUrl()`.
- `Services/Artwork/ArtworkSelectionService.cs` — the four setters and resetters write the chosen column and stamp its timestamp; the anime setter no longer treats MAL's own picture specially.
- `Services/Series/SeriesGraphBuilder.cs:1116-1122` (absorb) and `:1220-1236` (re-root) — carry the timestamps alongside the choices they already carry.
- `Services/Detail/AnimeDetailDto.cs`, `Controllers/AnimeDetailController.cs`, `Controllers/SeriesController.cs` — expose the chosen value; the set/reset responses return it.
- One EF migration: three columns, the 190-row backfill, and the run-time stamp for all 311 existing choices.
- 5 backend test files seed or assert on the two picture columns (`AnimePictureTests`, `ArtworkSelectionServiceTests`, `SeriesPicturePoolTests`, `MalMappingExtensionsTests`, `AnimeDetailServiceTests`); each needs the chosen column seeded where it seeds an override today.

**Frontend**

- `components/PicturePickerOverlay.tsx` — an optional clear action and its label.
- `pages/AnimeDetailPage.tsx`, `pages/SeriesPage.tsx` — pass it, wire it to the existing reset calls, update local state from the responses.
- `api/types.ts`, `api/client.ts` — `selectedPictureUrl` on the anime detail shape and on the four set/reset responses; the comment at `client.ts:320` saying MAL's own picture "*is* the clear" is removed.

**Unaffected**

- Every surface that renders `pictureUrl`: the dashboard, my list, Season, Year, Top, Airing, Search, Recap, profile, series page and timeline, series browser, related tiles. They read the displayed column, which keeps its name, its meaning, and its value.
- The picture-set fetch paths (`PictureRefreshService`, the visit backfill, the bounded series pool fill), the series picture pool, the title rule, and MAL write-sync — none of them touch chosen-ness.

## Why

A series' identifier is an EF identity column — a counter local to one database. The same franchise built on two machines gets two different numbers, and the numbers say nothing about the franchise, so nothing keyed on a series id can be carried anywhere or rebuilt from scratch and still mean the same thing. The chosen titles and chosen pictures already stored per series (105 of the 255 stored series carry one) are exactly the kind of stored choice that needs an identity it can be reunited with.

The value that *would* travel is already on the row and already unique. `Models/Series.cs` documents `RootAnimeId` as "the earliest main-line member; unique" and `Data/AnimeTrackerDbContext.cs:196` gives it a unique index. Every device that walks the same MAL relation data derives the same root, so the root's MAL id is the same identifier everywhere, with no coordination. The database's own counter is the redundant half.

**Verified against the code and the live database** (2026-09-06):

- `Series.Id` is `ValueGeneratedOnAdd` with `UseIdentityByDefaultColumn` (model snapshot), and Postgres reports `is_identity = YES, BY DEFAULT`. Its doc comment does claim rebuild stability, and that claim holds only within one database — `SeriesGraphBuilder.MatchToStoredSeries` keeps the overlapping series' row, so its counter value survives; a fresh build elsewhere mints a different one.
- `SeriesMember` is keyed on `(SeriesId, AnimeId)` (`PK_SeriesMembers`), with `FK_SeriesMembers_Series_SeriesId ... ON DELETE CASCADE`.
- 255 series, ids 1–308; roots 1–63375; 105 series carry a chosen title or a chosen picture; 1600 memberships over 1567 distinct anime; no orphaned membership; every root exists in `AnimeMetadata`; every series holds a membership for its own root.
- **12 series' `RootAnimeId` equals a *different* series' current `Id`** (roots run as low as 1 — Cowboy Bebop). A naive in-place `UPDATE "Series" SET "Id" = "RootAnimeId"` therefore collides mid-statement. No `(RootAnimeId, AnimeId)` pair collides, so the remapped membership rows are unique once sequenced correctly.
- The frontend already navigates by root anime id: the route is `/series/:animeId` and the read is `/api/series/by-anime/{animeId}`. A series id appears in a URL only on the four selection endpoints (`/api/series/{seriesId}/title|picture`).

## What Changes

**The series id *is* the root anime's MAL id**

- `Series.Id` stops being generated and is set by the builder to the MAL id of the earliest main-line member. The `RootAnimeId` column is **removed**: the two hold the same value by definition, and one of them is the identity. **BREAKING** for anything holding a stored series id, which is why the migration rewrites the stored ones rather than leaving them.
- `SeriesMember.SeriesId` — and so the `(SeriesId, AnimeId)` key — carries the root anime id too. The composite key gains a meaning it did not have: a membership row now says which franchise-root it hangs off.
- Two machines that build the same franchise from the same relation data arrive at the same id without coordinating, and a rebuild from an empty database reproduces the ids it had.

**When the root moves, the id moves with it**

- If MAL later reveals an older main-line entry, the series' root changes and its identifier changes with it. This is accepted, not prevented: every device rebuilding from the same relation data reaches the same new id, which is the property being bought. The series keeps its chosen title and chosen picture across the move — the choices follow the series, not the number.
- **BREAKING** relative to the current specs, which say a rebuilt series "keeps the identifier it had". It keeps its identity — its members, its choices, its page — but the identifier is now derived, so it tracks the root.

**One id at the API boundary too**

- `SeriesDto`, `SeriesListItemDto`, `SeriesSearchResultDto`, `TopSeriesItemDto` and `RewatchedSeriesItemDto` currently carry `SeriesId` **and** `RootAnimeId`. They become one field, `seriesId`, holding the root anime's MAL id. The surfaces that meant "the root anime" — the card link, the poster's anime id, the MAL link on the series page — read that same field.
- `/api/series/{seriesId}/title` and `/picture` keep their shape; the id in the path is now a MAL anime id, the same number `/series/:animeId` already uses.

**The stored data is rewritten, not rebuilt**

- One schema migration rekeys the 255 stored series and their 1600 memberships in place, so the 105 stored choices survive with no re-picking. It sequences around the 12 colliding ids by moving every id into negative space first, then onto its root. The identity property is dropped, the `RootAnimeId` column and its unique index go, and the cascade FK is rebuilt.
- No series is rebuilt by the migration: the id each series takes is the root already stored on its row.

**Not in scope**

- No constraint is added that an anime may be main-line in only one series. The relation data and the builder already produce that, and the user has asked for no such constraint.
- `Series.Id` does **not** become a foreign key to `AnimeMetadata`. Nothing needs it (the root's membership row already carries that FK), and it would make deleting a cached anime cascade away a whole franchise.

## Capabilities

### New Capabilities

None. This changes what an existing identifier *is*.

### Modified Capabilities

- `series-page`: the persistence requirement gains what the identifier is — the root entry's MAL id, derived rather than assigned — and its "keeps the identifier it had" rule is restated as "keeps its identity and its choices; the identifier tracks the root".
- `series-versions`: the rebuild-identity rule (the most-overlapping stored series keeps its identifier, chosen title and chosen picture) is restated the same way — the survivor keeps its choices, and takes the identifier its root implies.
- `series-identity`: the re-merge scenario ("the one with the largest overlap keeps its identifier, its chosen title and its chosen picture") is restated, and a new rule says a chosen title and picture survive the root moving.
- `data-persistence`: the series-overrides requirement currently justifies itself with "a series' identity is independent of which member happens to be its root". That is no longer true and is replaced: the choices live on the series row so that they survive a rebuild, including one that re-roots the series and so changes its identifier.
- `series-browser`: the list endpoint's field list drops the duplicated pair ("root anime id, series id") for the single series id, which is the root anime id.

## Impact

**Backend**

- `Models/Series.cs` — `RootAnimeId` removed; `Id`'s doc comment rewritten to say what the id is and that it moves with the root.
- `Data/AnimeTrackerDbContext.cs` — `Series.Id` mapped `ValueGeneratedNever()`; the `RootAnimeId` unique index removed.
- `Services/Series/SeriesGraphBuilder.cs` — `PersistAsync` sets the id from the derived root, and handles the one case EF cannot: a matched series whose root has moved cannot have its key updated, so it is deleted and re-inserted with its choices carried over, ordered so the id it takes is free by the time it takes it. `ResolveFoldedPrimaryAsync` loses its lookup of a competing series' `RootAnimeId` — the competing series id *is* that value.
- `Services/Series/SeriesService.cs`, `SeriesListService.cs`, `SeriesRankingLookup.cs`/`SeriesRankingIndex.cs`, `Services/Search/SeriesSearchLookup.cs`/`SeriesSearchIndex.cs`, `Services/Profile/ProfileService.cs` — every projection that reads `series.RootAnimeId` reads the series id instead; the root member is found by `m.AnimeId == m.SeriesId`.
- `Services/Series/SeriesDto.cs`, `SeriesListDto.cs`, `Services/Search/SeriesSearchResultDto.cs`/`AnimeSearchResultDto.cs`, `Services/Profile/ProfileDto.cs` — the two id fields collapse to one.
- One EF migration: rekey `Series` and `SeriesMembers`, drop the identity property, drop `RootAnimeId` and its index, rebuild the cascade FK.
- ~17 backend test files seed `new Series { Id = 1, RootAnimeId = 100 }`-shaped rows and need the two reconciled.

**Unaffected**

- `Services/Artwork/ArtworkSelectionService.cs`, `PictureRefreshService.cs`, `Services/Relations/RelationResolver.cs` and every other series-id consumer keep working unchanged: they take a series id from a lookup and pass it back, and never care what the number means.
- Series membership semantics, main-line classification, version slots, the build budgets, and the single-flight gate.

**Frontend**

- `api/types.ts` — `rootAnimeId` removed from the five series shapes.
- `components/SeriesCard.tsx`, `components/SearchBar.tsx`, `pages/SearchPage.tsx`, `pages/ProfilePage.tsx`, `pages/SeriesPage.tsx` — read `seriesId` where they read `rootAnimeId` today. Routes and endpoints are untouched: `/series/:animeId` already carries this exact number.

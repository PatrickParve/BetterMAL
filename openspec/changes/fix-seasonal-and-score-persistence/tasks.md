## 1. Score-visibility persistence

- [x] 1.1 In `context/ScoreVisibilityContext.tsx`, initialize the global `hidden` state from `localStorage` and write it back on every change so the choice survives a reload / new tab; leave per-score reveal non-persistent

## 2. Season membership from MAL's `start_season`

- [x] 2.1 Add `start_season` to `Services/Mal/MalClient.DefaultAnimeFields` and a `MalStartSeason { Year, Season }` DTO on `Services/Mal/Dto/MalAnimeNode`
- [x] 2.2 In `Services/Season/SeasonBrowseService.FetchAndCacheAsync`, decide listing membership from MAL's `start_season` (include when it matches the fetched season or is absent; exclude only when MAL classifies the anime under a different season)
- [x] 2.3 In `Data/Repositories/SeasonRepository.GetPageAsync`, remove the start-date re-derivation filter so the cached listing is the single source of truth

## 3. Unranked popularity sorts last

- [x] 3.1 In `SeasonRepository.GetPageAsync`, order the popularity sort so anime with rank 0 or null come after all ranked anime, then by ascending rank, then title
- [x] 3.2 In `components/CurrentSeasonSection.tsx`, treat popularity rank 0 or null as unranked (sort last) in the popularity sort

## 4. Verification

- [x] 4.1 `docker compose build backend frontend` passes with no new warnings/errors
- [x] 4.2 After clearing the affected current-season fetch logs and redeploying, confirm `GET /api/season/2026/summer` includes the previously-missing anime (id 62076) and that unranked anime sort last
- [x] 4.3 Confirm the rebuilt frontend bundle persists the hide-scores toggle across reload

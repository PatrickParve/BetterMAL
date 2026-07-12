## Why

Three defects surfaced after the seasonal and score-visibility work shipped:

1. **Hide-scores did not survive a reload.** The global hide toggle lived in in-memory React state only, so every refresh (or new tab) reset it and the scores reappeared.
2. **The Season page dropped valid anime.** Season membership was re-derived from each anime's start *date* (`(month-1)/3` quarter) instead of MAL's own season classification. MAL files some anime under a season that differs from their start-date quarter — e.g. *Super no Ura de Yani Suu Futari* aired `2026-06-03` (a June/spring quarter date) but MAL classifies it as **Summer 2026**. The derived-quarter filter dropped it at ingestion, so it never appeared on the Season page even though the Home "Current season" section (which keys off airing status, not date) showed it.
3. **Unranked anime sorted first under "Popularity".** MAL popularity is a rank where `1` is most popular and `0`/absent means "unranked". The sort used `rank ?? MAX`, so a rank of `0` sorted ahead of rank `1` — pushing obscure, unranked titles to the top of the Season page and the Home Current-season section.

## What Changes

- **Global hide-scores toggle persists** across reloads and new tabs (via `localStorage`). Per-score in-place reveals remain non-persistent, as before.
- **Season membership follows MAL's authoritative `start_season`** (the field MAL itself uses to build its per-season listings) instead of re-deriving the season from the start date. Anime whose premiere season differs from their start-date quarter now appear in the correct season. The redundant read-time re-derivation filter is removed — the cached listing is the single source of truth for season membership.
- **Popularity sorting treats an absent or zero rank as "unranked" and orders those last** (after all ranked anime), on the Season page and the Home Current-season section — matching the search results page.

## Capabilities

### New Capabilities
<!-- None — all changes modify behavior of existing capabilities. -->

### Modified Capabilities
- `score-visibility`: the global hide toggle persists across reloads.
- `season-browser`: season membership follows MAL's `start_season`, not the start-date quarter; unranked anime (popularity rank absent or zero) sort last.
- `main-dashboard`: the Current-season popularity sort places unranked anime last.

## Impact

- **Backend** (`backend/AnimeTracker.Api`): `Services/Mal/MalClient` (request `start_season`), `Services/Mal/Dto/MalAnimeNode` (new `MalStartSeason`), `Services/Season/SeasonBrowseService` (classify listings by `start_season`), `Data/Repositories/SeasonRepository` (remove the start-date read filter; unranked-last popularity sort).
- **Frontend** (`frontend/`): `context/ScoreVisibilityContext` (persist the toggle), `components/CurrentSeasonSection` (unranked-last popularity sort).
- **Data**: no schema change. Season membership self-heals on the next daily re-fetch of current/upcoming seasons; stale season-fetch logs for the affected current seasons were cleared so they re-ingest with the corrected classification on next visit. Already-cached *past* seasons heal the same way if their fetch log is cleared.
- **External**: adds MAL's `start_season` field to the default fields request; no new dependencies.

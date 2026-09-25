## Why

Since the TMDB change, the user sees the Season, Year, Top anime and Series pages flicker as they open. They flash "Loading…", sometimes an error ("This season couldn't be loaded"), and on the Series page "No series match the selected filters" with no filter set, before the real page appears a moment later. The user asked what causes this, whether TMDB image fetching is to blame, and for opening pages to feel smooth whatever the load time. They also asked what the app shows when the backend is offline or a load fails, and what is missing there.

Investigation found that the backend is not slow and that the TMDB change added no work to these reads. The three reads answer in 10–45 ms through nginx, measured on the running stack. The flicker comes from how the frontend presents a read in progress:

1. **Every loading message shows from the first frame.** A 30 ms load therefore reads as a flicker of "Loading…".
2. **A progressive grid's reveal count collapses to zero while its list is empty.** Season, Year, Series and Search each reveal their grid as a sentinel below it scrolls into view. While the list is still loading, the sentinel is on screen, so the reveal clamps its count to the empty list's length, which is 0. When the list arrives, the Series page computes its message from the zero revealed cards and says "No series match the selected filters". Season and Year paint an empty grid. Both last until the sentinel fires again.
3. **Season and Year read their refresh outcome before their own read has landed.** Opening a season starts the cached read and the MAL refresh together. A refresh that is skipped as already fresh costs one database lookup, so it usually answers first. The page takes "nothing loaded yet, refresh settled" to mean the first fetch failed and shows "couldn't be loaded". Then it issues a second, redundant read of the whole listing.

The part that is due to TMDB is picture weight. A chosen TMDB picture is drawn from TMDB's `original` file on every surface: grid cards, list rows and picker thumbnails. That file is 0.2–1 MB, against about 33 KB for MAL's picture. The user's data has 25 anime and 23 series with a TMDB picture, so those cards arrive late and pop in.

Failure handling is uneven across pages. A failed load leaves Home blank and the Airing page with only its header. My list says "Nothing here yet" and Search says "No anime found", as if empty. Top anime stays on "Loading…" for the rest of the session, because its failed request is kept and reused. No page reloads by itself when the server comes back, even though the connection bar clears. The first-load "Can't reach the backend" screen asks for a manual browser reload.

## What Changes

- **Loading states wait before appearing, then fade in.** A page, section or overlay shows its loading indicator only after a short delay, and fades it in. A fast load goes straight from the page's header to its content with no loading text in between. A slow load still says it is loading.
- **A page never shows an empty or failure message while its own read is in flight.** Empty and "no match" messages come from the whole loaded list, never from the revealed slice. A progressive grid's reveal count never drops below its first screenful because the list was briefly empty. This covers Season, Year, Series, Search and My list.
- **Season and Year read the refresh outcome only against a settled read.** The visit-triggered refresh is interpreted, and its follow-up re-read decided, only once the page's own cached read has landed. This removes the error flash and the redundant second read. A season with nothing cached, waiting on its first MAL fetch, says it is fetching from MyAnimeList rather than a bare "Loading…".
- **A failed read is never shown as emptiness.** Every page and profile section that loads data has a failure state. It says the data could not be loaded and offers **Try again**, and when the server cannot be reached it says so. This covers Home, Airing, My list, Search, Recap, Top anime, Settings, the Profile sections, anime detail and series pages, and the Season, Year and Series pages.
- **Failed reads retry once the server is back.** When the backend becomes reachable again after an outage, a page whose read failed during it reloads by itself, once. A retry that fails again waits for **Try again**, so a failing endpoint cannot cause a retry loop.
- **Top anime recovers from a failed first load.** The failed request is dropped rather than reused, so Try again, a later visit or a reconnect loads the list.
- **The first-load "Can't reach the backend" screen recovers by itself.** It retries on the health-poll cadence and has a Try again button, and it enters the app as soon as the backend answers, with no browser reload.
- **TMDB pictures are drawn from a rendition sized to their surface.** Each surface gets the smallest of TMDB's fixed sizes that is at least twice the widest it draws the picture, so the picture is still sharp on a high-density screen. That is `w342` for rows, `w780` for cards, tiles and picker options, and `w1280` for the detail and series headers and the widest tiles, with each surface's width confirmed by measurement. On the user's pictures, `w780` is about a third of the `original` poster's bytes and a tenth of a 4K backdrop's. The stored choice and every URL the API sends stay the `original` URL. Only the address the browser downloads from changes.
- **Stepping between periods keeps the current content until the next arrives.** On the Season, Year and Airing pages, the arrows no longer collapse the page to empty or "Loading…" between one period and the next. The previous grid or week stays until the new one replaces it, and is muted only if the new one is slow.
- **Pictures fade in as they arrive.** On cards, tiles and rows, a newly loaded picture fades in over its box instead of popping in. A picture the browser already holds, for example after going back, shows at once with no fade. Reduced-motion users get no animation.

## Capabilities

### New Capabilities

- `page-load-states`: how every page, page section and data overlay presents a read in progress, an empty result, a failed read and recovery. It covers the delayed, fading loading indicator; empty and failure messages that are never shown while their read is in flight; a failure state with Try again; the single automatic retry when the backend becomes reachable again; a progressive grid's reveal never falling below its first screenful; and a period step on the Season, Year or Airing page holding the current content until the next period arrives.

### Modified Capabilities

- `season-browser`: the terminal-state rules in "Cache-first read with visit-triggered background refresh" now require the page's own read to have settled before a refresh outcome can decide what shows. The skipped-refresh re-read happens only when that settled read found nothing cached. A never-cached season waiting on MAL says it is fetching from MyAnimeList. The failure message no longer promises a retry only on the next visit.
- `year-browser`: the same change to "The Year page states which empty situation it is in".
- `series-browser`: "The Series page states which empty situation it is in" decides between loading, empty, no-match and failure from the read and the whole filtered list, never from the revealed cards, and its failure state offers Try again.
- `library-views`: "Fast switching between ranking lists" adds that a failed first load of a list ends in a failure state with Try again and is not reused, rather than loading indefinitely.
- `connection-status`: "First-load failure keeps its full-page message" adds that the message retries by itself and offers Try again, entering the app once the backend answers.
- `tmdb-artwork`: a new requirement that a TMDB picture is displayed from a rendition sized to its surface. The `original` URL remains the picture's identity everywhere it is stored, sent or compared.
- `artwork-presentation`: a new requirement that a picture fades in as it arrives, and that a picture already held by the browser shows at once.

## Impact

**Frontend only.** No backend, API, DTO, schema, migration or dependency change.

- `frontend/src/hooks/usePageData.ts`: reports a failed read, and adds `retry` and the one automatic retry when the backend becomes reachable again, driven by `api/connectionStatus.ts`'s existing store.
- New shared pieces under `frontend/src/components/` and `frontend/src/hooks/`: a delayed, fading loading notice; a load-failure block with Try again; and a scroll-reveal hook for the sentinel observer that five pages now copy.
- Pages: `SeasonPage.tsx`, `YearPage.tsx`, `SeriesBrowserPage.tsx`, `TopAnimePage.tsx`, `SearchPage.tsx`, `MyListPage.tsx`, `HomePage.tsx`, `AiringPage.tsx`, `RecapPage.tsx`, `ProfilePage.tsx`, `SettingsPage.tsx`, `AnimeDetailPage.tsx`, `SeriesPage.tsx`; overlays with a "Loading…" line (`AnimeRankOverlay`, `EditHistoryOverlay`, `UpdatesHistoryOverlay`); `App.tsx` for the first-load screen.
- Pictures: `utils/anime.ts` gains the TMDB display-size mapping next to `isTmdbImageUrl`. It is applied in `PosterPicture.tsx`, `RowPicture.tsx`, `PicturePickerOverlay.tsx`, `SeriesEntryRow.tsx`, `Updates/UpdateCard.tsx` and the detail and series headers. `PosterPicture` and `RowPicture` gain the arrival fade.
- `openspec/specs/tmdb-artwork/spec.md`'s rule that the URL is built as `…/t/p/original…` still holds for what is stored and sent. The new requirement adds a display-only rendition on top of it.

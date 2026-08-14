## Why

Three things about how airing shows are presented still read wrong. The anime detail page shows only my watched progress, so for a show that is still broadcasting there is no way to see at a glance how much of it exists yet — the home page has shown that as a blue aired fill for a while, but the detail page never got it. On the series timeline, the year ruler floating above the cards still doesn't communicate elapsed time: labels sit at arbitrary fractional positions inside a card, and the two-year wait between two seasons is invisible. And a currently-airing card is marked with a translucent badge laid over the poster art, which disappears whenever the poster happens to be the same colour — the one card you most want to spot in the row is the easiest one to miss.

## What Changes

- The anime detail page's progress bar shows broadcast progress as a blue fill behind my purple watched fill while the anime is currently airing, matching the home page's convention. The bar keeps its inline-editable watched count and increment button.
- The series timeline drops the floating year ruler. Each card states its own air range instead, so elapsed time is stated on the card rather than implied by position on a ruler the cards don't have. (An earlier pass of this change also labelled the wait between consecutive cards on a connector between them; that was tried and then dropped at the user's request — the per-card range alone is enough.)
- The timeline's own scroll container no longer clips the airing ring, and no longer shows a visible scrollbar — a franchise with many seasons still scrolls, it just doesn't show a track under the row.
- Card size and the spacing between cards stay uniform — spacing still never varies with real elapsed time, and a year in which nothing aired is still never presented.
- A currently-airing card is marked by a ring around the whole card rather than a badge over the poster, so poster colours can never camouflage it. The "Airing" badge is removed.
- The status-coloured left border is removed from timeline cards; my list status stays in the card footer text, where it already is.

## Capabilities

### New Capabilities

None — all three changes modify existing behaviour.

### Modified Capabilities

- `series-page`: the Series timeline ribbon requirement changes how time is presented (per-card air range, no year ruler, no gap connectors), how a currently-airing card is marked (ring, no badge, never clipped by the timeline's own scroll container), and drops the status-coloured left border from cards. The timeline's horizontal scrollbar is hidden.
- `anime-detail`: the progress bar requirement gains a broadcast-progress fill while the anime is currently airing.
- `main-dashboard`: its airing-progress-bar requirement currently states the bar applies only to the home page and that the detail page keeps the plain watched/total bar; that exclusion is amended to allow the detail page.

## Impact

- `frontend/src/components/SeriesTimeline.tsx` / `.css` — remove the year-ruler axis and its label-placement math, add per-card air ranges, replace the airing badge with a card ring that renders in full without being clipped by the timeline's scroll container, hide that container's scrollbar, and drop the status left border.
- `frontend/src/components/ProgressBar.tsx` / `.css` — optional aired fill behind the watched fill.
- `frontend/src/pages/AnimeDetailPage.tsx` — pass the aired count and airing status through to the progress bar.
- No backend work: `AnimeDetailDto.episodesAired` and `SeriesEntryDto.airedFrom` / `airedTo` / `airedEpisodes` are already served.

## Why

Three surfaces still read as unfinished next to the pages beside them. The schedule shows seven identical day-columns with nothing marking which one is *now*, so finding today means reading dates. The Top anime page's medal tier was built before the recap podium existed and has since drifted from it — its own literal medal colours, a different rank badge, oversized score chips with 10px labels — and both its card tiers crop posters to `2 / 3`, so the same artwork is visibly cut differently here than on the anime pages, while a hidden MAL score's reveal control floats mid-slot instead of landing where the number it replaces would sit. And every poster box in the app assumes portrait art: the handful of anime whose MAL picture is landscape get centre-cropped down to a portrait sliver on the detail and series pages, losing most of the image.

## What Changes

- **Today is marked on the schedule.** When the displayed week contains today, that day's column header is visually distinguished from the other six — the marking lives in the header row at the top of the grid, and carries an accessible `aria-current` alongside the visual treatment. Weeks that do not contain today mark nothing.
- **The Top anime showcase adopts the recap podium's colour language.** The page drops its own local medal palette in favour of the app-wide `--medal-*` tokens the podium uses, takes the podium's card treatment (medal-tinted surface over the neutral card colour, medal border, medal band across the card's top edge) and the podium's rank badge (a medal-outlined circle carrying the bare rank number) in place of the filled `#N` pill. Colour and badge only — the podium's sizing, staggered entrance, hover lift, and gold sheen stay on the recap page.
- **The showcase's score chips shrink and their labels grow.** The two chips get tighter boxes, and "My score"/"MAL" labels rise from 10px to a legible size, reversing today's proportions where the label is the smallest text on the card. The chips stay equal-width to each other, as they already must.
- **Ranks 1–10 stop cropping posters.** Both card tiers render their poster in the same proportions the anime detail page uses, so a poster shown on Top anime is the same picture, uncropped, that the anime's own page shows.
- **A hidden MAL score's reveal control lands where its value would.** In the showcase chips the control sits at the start of the value slot, where the shown score and the unscored `—` beside it both start; in the ranks 4–10 cards it sits at the end of the slot, mirroring the `—` on the row's other side. The same rule is applied to the flat rows' right-aligned MAL column on the same page, which has the identical mismatch — leaving it would put code in this file in direct conflict with the rule this change writes. The slot's width is unchanged everywhere, so nothing reflows.
- **Landscape artwork is shown whole.** Where an anime's picture is wider than it is tall, the anime detail page and the series page's header render it at its natural proportions rather than cropping it to a portrait box, and the series page's main-line timeline cards and More tiles show it whole inside a wider card. Portrait artwork — effectively every other anime — is untouched.

## Capabilities

### New Capabilities

None — every change refines behaviour already covered by existing specs.

### Modified Capabilities

- `airing-schedule`: the day-column header for today SHALL be distinguished from the other six, and marked as the current date for assistive technology, when the displayed week contains it.
- `library-views`: the Top anime showcase tier's medal colours SHALL be the app's shared medal colours rather than its own; its rank badge SHALL take the same form as the recap podium's; its score chips SHALL be compact with labels legible against their values; and both card tiers (ranks 1–3 and 4–10) SHALL render posters in the anime pages' proportions rather than cropping them.
- `score-visibility`: a hidden score's reveal control SHALL sit at the position within its reserved slot that the value it replaces would occupy — start, centre, or end — rather than always centred.
- `anime-detail`: a landscape picture SHALL be shown at its natural proportions rather than cropped to the page's portrait poster box.
- `series-page`: a landscape picture SHALL be shown whole in the page header, on main-line timeline cards, and on More tiles; a card carrying landscape artwork MAY be wider than its portrait neighbours, and that widening SHALL carry no meaning about the entry's duration.

## Impact

- **Frontend only.** `AiringPage.tsx`/`.css`, `TopAnimePage.tsx`/`.css`, `AnimeDetailPage.tsx`/`.css`, `SeriesPage.tsx`/`.css`, `SeriesTimeline.tsx`/`.css`, `SeriesExtraTile.tsx`/`.css`, plus one new shared hook for orientation detection.
- **No backend, DTO, or API change.** `pictureUrl` is already MAL's `large` image; orientation is read from the loaded image in the browser, so nothing new is fetched, stored, or migrated.
- **Deletes the divergence rather than adding to it**: `TopAnimePage.css`'s local `--medal-gold/silver/bronze` block (which shadows the identical-purpose tokens in `index.css`) is removed, leaving one medal palette in the app.
- **Reuses existing components** — `ScoreChip` and `ScoreValue` keep rendering the Top anime scores, so hide/reveal and the completed-score setting keep working untouched; the reveal-control change is an alignment rule, not a change to what is rendered.
- **Spec-covered behaviour that must not regress**: "A hidden score occupies the same slot as a shown score" (no reflow, slot width fixed to a two-decimal average), "A showcase card's two score boxes match", "Medal colours survive a theme switch", "Ranks 4 to 10 form their own tier" and its badge-legibility scenario, the timeline's "every card the same fixed size regardless of how long that entry ran", and the schedule's existing week-navigation and empty-week behaviour.

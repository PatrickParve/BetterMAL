## Why

The recap's rating distribution says *how many* anime got each score but never *which* ones — the only way to see the 10s of a year is to follow the row out to my list and leave the recap behind. And the recap's mid-page controls throw the page away underneath you: the top 10's **My score / MAL score** toggle sits halfway down the page, but switching it pushes a history entry, which `useScrollRestoration` reads as a fresh visit and scrolls to the top — so the very section you were comparing jumps off screen the moment you compare it.

## What Changes

- **A score board opens from the rating distribution.** A button beside the **Rating distribution** heading opens a large overlay laying the period's scored anime out by score: ten slots, 10 down to 1, each holding the poster art of every anime that got that score. It answers "which ones were the 10s?" without leaving the recap.
- **Each score's slot is coloured for its rank.** 10 is a purple-and-white iridescent mix that belongs to no other slot — the mark of superiority; 9 is red, 8 blue, 7 gold, 6 and 5 silver, 4 through 1 bronze. The tier a score belongs to is readable from its colour alone, before reading the numeral.
- **Posters answer when pointed at.** Hovering (or keyboard-focusing) a poster lifts it out of its slot and shows a card naming the anime, its score, and its media type; the poster is a link to the anime's page, so the board is a way *into* the period as well as a picture of it.
- **The board covers the same set as the distribution beside it** — every included entry of the period and time filter that carries one of my scores, unnarrowed by the top 10's media-type control — so the count in a distribution row and the number of posters in the matching slot always agree.
- **Mid-page recap controls hold your scroll position.** Switching the ranking basis or the media-type filter keeps the page exactly where it is instead of scrolling to the top. Controls that genuinely change the page's subject — the recap-type tabs, the period steppers and selects, the time filter — keep today's scroll-to-top.

## Capabilities

### New Capabilities

None — the board is a new view onto the recap's existing rating distribution, so it belongs to `list-recaps` rather than to a capability of its own.

### Modified Capabilities

- `list-recaps`: the rating distribution gains a companion score board overlay — its opening control, its per-score slots and their colour tiers, the poster hover/focus card, its set-selection rule (identical to the distribution's), and its empty-slot and nothing-scored behaviour; and the recap's mid-page controls (ranking basis, media type) gain a scroll-position guarantee that the period/mode/time-filter controls deliberately do not share.

## Impact

- **Frontend only.** No backend, DTO, or request change: the board is a second rendering of `recap.items`, which the page already holds and already reduces into the distribution's ten buckets via `scoreBucketsOf`.
- New `frontend/src/components/ScoreBoardOverlay.tsx` + `.css`, built on the existing `Modal` (Esc/backdrop close for free) and opened from `RecapPage.tsx` local state exactly as `RankingOverlay` already is.
- `frontend/src/index.css` gains score-tier colour tokens beside the existing `--medal-*` set (10 and 9 and 8 are new; 7/6-5/4-1 alias the medal tokens already there), with dark-theme values.
- `frontend/src/components/Modal.css` gains one wider size class — the board needs more width than `modal--wide`'s 640px to lay a slot's posters out.
- `frontend/src/hooks/useScrollRestoration.ts` learns one opt-in flag carried in navigation state; `RecapPage.tsx`'s `updateParams` learns to pass it. Every other page's navigations are untouched, so `TopAnimePage`'s deliberate scroll-to-top on paging (and every other page's) behaves exactly as it does today.
- **Spec-covered behaviour that must not regress**: "Recap rating distribution" and "Drilling into a rating distribution row" (the board is added beside them, not in place of them), "Top 10 ranking basis control" and "Top 10 media-type control" (which anime are ranked and in what order is unchanged — only the scroll position is), and the `useScrollRestoration` back/forward restore path.

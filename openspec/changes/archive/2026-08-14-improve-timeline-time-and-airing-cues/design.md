## Context

Three presentation problems, all around "what is airing and when did it air".

The series timeline currently draws a year ruler above the card row (`SeriesTimeline.tsx`, `assignYearMarks`): each card is a fixed 168px, and a year label is placed at the fraction of that card's width where 1 January falls inside that entry's own air span. That mapping is invented — a card's width is not a duration, so a fraction of it is not a date. It also collides with itself often enough that a `MIN_LABEL_GAP_PX` rule silently drops labels.

The currently-airing card is marked with `.series-timeline__airing-badge`: a translucent pill (`--status-watching-bg`, 12–16% alpha) positioned over the top-right of the poster. Against a poster that happens to be green, or bright, or busy in that corner, it vanishes.

The anime detail page renders the plain `ProgressBar` (watched/total). The home page's followed-shows-airing section has shown broadcast progress as a blue fill behind the purple watched fill since the dashboard work, and the series page adopted the same convention, but the detail page — the one page dedicated to a single show — never did. `AnimeDetailDto.episodesAired` is already in the payload, so this is presentation-only.

Two constraints come directly from the user: card spacing stays uniform (it must never encode elapsed time), and a year in which nothing aired must never be presented. A third: the airing cue is a ring around the card, not a badge, and the status-coloured left border comes off the timeline cards entirely.

## Goals / Non-Goals

**Goals:**
- The detail page shows how much of a still-airing show exists, in the same blue-behind-purple language as the home page and series page.
- Elapsed time on the timeline is *stated* — each card's own air range — instead of being implied by a positional scale that cards do not actually have.
- A currently-airing card is unmissable in the row regardless of poster art, and its ring is never clipped by the row's own scroll container.
- Every card keeps identical size and identical spacing to its neighbours.
- The timeline scrolls when it doesn't fit, without showing a scrollbar track.

**Non-Goals:**
- Proportional/true-scale chronology on the timeline. Explicitly rejected by the constraint that spacing stays uniform, and already rejected once in `series-page-improvements`.
- Any change to the extras (More section) tiles, including their own status treatment.
- Any change to the home page's airing bar, My List, or the currently-watching carousel.
- Backend or DTO changes. Everything needed is already served.
- A next-episode countdown on timeline cards — `SeriesEntryDto` carries no next-episode ETA, and fetching one per main-line entry is out of scope.

## Decisions

### 1. Replace the year ruler with per-card air ranges

The `.series-timeline__axis` row, `assignYearMarks`, `MIN_LABEL_GAP_PX`, and the `CARD_WIDTH`/`CARD_STRIDE` constants that existed only to feed the ruler's math are deleted. In their place, **each card states its own air range** in its meta block, replacing the bare start year. The meta block becomes two fixed single lines so card heights stay identical: line 1 `TV · 12 ep`, line 2 the air range. An undated card's line 2 carries the existing no-date indicator instead.

Range formatting keeps to one line at 11px inside 168px, so months are abbreviated and a shared year is not repeated: `Apr – Jun 2013` within one year, `Oct 2013 – Mar 2014` across years, `Apr 2013` for a single-date entry such as a movie, and `Apr 2026 – ongoing` while the entry is still broadcasting.

Cards go back to plain, fixed spacing between neighbours — a `12px` `gap` on the row — rather than any element sitting between them.

*Alternatives considered.* Keeping the ruler but pinning one label per card (the entry's own start year) was rejected as strictly less informative than the range the meta line now carries, for an extra row of chrome. A proportional axis was rejected by constraint.

*Revised after initial implementation.* The first pass of this change also inserted a fixed-width connector between every pair of cards, labelled with the elapsed wait between them (`1y 9m`, `<1m`, `Overlaps`), measured via an `entryEndDay` helper. The user tried it and asked for it to be removed outright — the per-card range carries enough context on its own, and the connector added width and clutter that weren't wanted. `formatGap`, `monthsBetween`, `connectorLabel`, `entryEndDay`, `dayNumber`, `DAY_MS`, and the `Segment`/`buildSegments` plumbing that existed only to support the ruler and then the connector are deleted along with it — a card's `undated` flag is now just `entry.airedFrom === null`, computed inline rather than precomputed per segment.

### 2. The airing cue is a ring on the card, drawn as a box-shadow

`.series-timeline__airing-badge` is removed. A currently-airing card instead carries `.series-timeline__card--airing`, styled as a ring drawn with `box-shadow` (a hard 2px ring in the page's broadcast blue plus a soft outer glow) rather than by thickening the border.

Using `box-shadow` rather than a wider border matters: borders participate in layout, so a 1px→3px change on one card would shift that card's contents relative to its neighbours and break the row alignment the previous change worked to establish. A shadow ring costs no layout.

Blue (`--mal`) is chosen over the green watching colour because the series page has one colour convention — blue is the world's/broadcast side, purple is mine — and "this is broadcasting right now" is a broadcast fact, matching the blue aired fill already drawn on that same card's progress track. The ring persists on hover and focus; hover keeps its existing accent border, which reads as an outer purple edge inside the blue ring rather than fighting it.

The ring is a presence/absence difference in card chrome, not a hue difference within otherwise identical chrome, so it survives colour-blindness and any poster art. A slow pulse on the glow reinforces "live", suppressed under `prefers-reduced-motion: reduce`. Because the visible cue is now purely graphical, the card carries `Currently airing` as accessible text (visually hidden) so a screen reader is told what the ring means.

*Alternatives considered.* An opaque badge with a contrasting outline keeps the cue over the poster, where it competes with art at every size. An animated left edge was rejected because the left edge is exactly what decision 3 is clearing.

*Revised after initial implementation.* The ring was being clipped on the top and bottom always, and on whichever of the left/right edges a card sat at the end of the row — `.series-timeline__scroll`'s `overflow-y: hidden` (and the matching ink-overflow clip that `overflow-x: auto` applies on its own axis) cut off the box-shadow the moment it extended past the row's own box, which had no spare room around it. Fixed by giving `.series-timeline__scroll` 20px of padding on every side and dialling back the pulse's peak size slightly (from a 5px spread / 18px blur down to 3px / 15px) so the ring's maximum extent comfortably fits inside that padding rather than needing an even larger container. `.series-box`, the card this timeline sits inside on the series page, has `overflow: visible` and its own 16px of padding, so it does not re-introduce the clip one level up.

### 3. Timeline cards lose their status-coloured left border

`.series-timeline__card--watching` / `--completed` / `--plantowatch` / `--onhold` / `--dropped` and the 4px `border-left` are removed; every card gets a uniform 1px border on all four sides. My list status stays where it already is, spelled out in the card footer (`Watching · 5/24`).

Beyond the user asking for it, this is what makes decision 2 unambiguous: with five status colours running down the left edge of every card, a coloured ring around one card would have read as a sixth status colour rather than as a different kind of signal. Removing them leaves card chrome saying exactly one thing — airing or not — and the dashed border of an undated card remains the only other chrome variation.

Extras tiles in the More section are untouched; they are a different presentation of a different kind of entry and were not part of the request.

### 4. The detail page gets the aired fill by extending `ProgressBar`, not by swapping in `AiringProgressBar`

`ProgressBar` gains one optional prop, `aired?: number | null`. When it is a number, a blue aired fill is drawn in the same track behind the purple watched fill; when it is absent or `null`, rendering is byte-identical to today, so My List, the carousel, and every other call site are untouched by construction.

`AnimeDetailPage` passes `aired={detail.airingStatus === 'currently_airing' ? detail.episodesAired : null}` — the fill appears only while the show is actually broadcasting, which is what was asked for, and a finished show keeps the plain bar where aired and total are the same number anyway.

Swapping in `AiringProgressBar` was rejected: it has no inline-editable watched count and no increment button, both of which the detail page's progress row requires, and it draws its own `aired/total` label that would duplicate the info box's `Currently airing: 5/12 ep aired` line two rows below.

Unknown-total behaviour mirrors the home page's rule rather than inventing a third one: with a known total the blue fill is `aired/total`; with an unknown total and a known aired count it is a fixed half-track "progress so far, end unknown" marker, with my purple fill measured *within* that blue extent against the aired count so being caught up covers it exactly and never overshoots; with neither known, no blue fill is drawn. The label stays `watched/total` — the aired figure is already named in the Status field, and re-labelling the bar would leave the detail page's two readouts saying the same thing twice.

This carves an exception into an existing spec sentence in `main-dashboard` that names the anime detail page as a page that keeps the plain bar, so that requirement is amended in the same change rather than left contradicting `anime-detail`.

### 5. The timeline's own scrollbar is hidden

`.series-timeline__scroll` hides its horizontal scrollbar chrome — `scrollbar-width: none`, `-ms-overflow-style: none`, and a `::-webkit-scrollbar { display: none }` rule — while the container stays fully scrollable by drag, wheel, or trackpad. Requested by the user: a franchise with enough main-line entries to overflow the row was showing a visible scrollbar track sitting under the cards, which the row-of-cards presentation doesn't need to state explicitly (the last card's poster peeking in from the edge is the existing affordance that there's more to scroll to).

## Risks / Trade-offs

- **A two-line meta block is one line taller than today's, on every card.** → It is a fixed two lines on every card, dated or not, so the row stays aligned; the ruler row removed above the cards costs more height than the extra meta line adds back.
- **The air range can overflow 168px for an entry spanning several years.** → Abbreviated months and a shared-year form keep the common cases short; the line clips with an ellipsis as the meta line already does, and the card's title attribute carries the full range.
- **Blue is now doing three jobs on a card: the MAL score chip, the aired fill, and the airing ring.** → That is the page's stated colour convention working as intended (all three are "the world's side"), and the ring is distinguished by being chrome rather than content. The competing risk — reusing green, the watching-status colour, for airing — would have re-introduced exactly the status/airing confusion decision 3 removes.
- **Removing the status left border loses a scannable status colour down the row.** → Explicitly requested; status is still stated in words on every card, and the series page's own header progress figures carry the aggregate picture.
- **`.series-timeline__scroll`'s 20px padding makes the timeline section taller and insets the row a bit further from `.series-box`'s own edge than before.** → That padding is what gives the airing ring's box-shadow room to render without being clipped; it's a fixed, one-time cost per series page rather than one that grows with entry count.
- **Removing the wait-between-seasons connector means a long gap between two seasons is no longer stated anywhere on the timeline.** → Explicitly requested by the user after trying it; the per-card air range on each side of the gap is judged to be enough to see that time passed, without a labelled connector spelling out how much.

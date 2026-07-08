# Personal Anime Tracker — Pages & Layout Brief

This document merges the written page requirements with the 7 hand-drawn
layout sketches provided, and points out a few places where the sketches add
or conflict with the earlier written spec. It's meant as a companion to the
main `anime-tracker-brief.md` (data model, sync, APIs), specifically for the
pages/UI change once that's picked up.

**On the sketches:** these are rough wireframes. Only two things matter —
*what's present on the page* and *roughly where it sits relative to other
elements*. Box sizes, curve shapes, exact spacing, and alignment in the
sketches are not intended as design constraints.

Sketch → page mapping:

| Page | Sketch file |
|---|---|
| Main page | `sketches/Home_Page_Example.png` |
| Airing page | `sketches/Schedule_Page_Example.png` |
| Season page | `sketches/Season_Page_Example.png` |
| Top anime page | `sketches/TopAnime_Page_Example.png` |
| My list page | `sketches/MyList_Page_Example.png` |
| Profile page | `sketches/Profile_Page_Example.png` |
| Single anime page | `sketches/Anime_Page_Example.png` |

One labeling quirk worth knowing up front: the My List and Top Anime sketches
both display the header text "The season" at the top — this is a copy/reuse
artifact from the Season sketch's template, not meaningful. Go by the
filename/intent, not the literal header text, on those two.

---

## Main page — `sketches/Home_Page_Example.png`

- **Currently watching:** a horizontal row of cards, navigated with left/right
  arrows. Click a card to go to the anime page of it. Plus button next to the 
  ep count to increment episodes watched (triggers the debounced-MAL-sync 
  logic). Each card shows a countdown to the next episode — 
  *"Next ep: in x days, y h"* — this is a detail from the sketch that wasn't 
  in the original written spec; it implies pulling from the cached broadcast 
  schedule per anime.
- **Airing today:** a list column (not a grid) — each row is a small image +
  "time: Title". My-list-only, filtered by broadcast day converted to local
  time. Clickable and will take you to its page. If nothing is airing today, 
  small text mentioning that.
- **Current season:** a card grid with a header bar and filter controls
  above it. My-list-only. Filterable by popularity, MAL score, alphabetical. 
  Each card has a progress bar along the bottom. Clicking card takes you to 
  its page.

## Airing page — `sketches/Schedule_Page_Example.png`

- Title bar "Schedule" with a `< current >` control to move between weeks.
- Seven day-columns laid out left to right, each with a day-label header.
- Each column stacks time slots underneath (time + small image + title & that 
  ep number). The sketch shows 2 slots per day — that's just the mockup's
  placeholder count, not a cap; a day with more or fewer airing shows just
  has more or fewer slots. If no slots on a day, keep it empty. If nothing
  is airing that week mention it in the middle with text.
- Only the anime in my list are shown here, no anime that I dont follow.

## Season page — `sketches/Season_Page_Example.png`

- Title bar "The season" (the one sketch where that text is actually
  correct) plus a "sort" control near the top.
- A **grid of cards** (not a list). Each card shows **Title, picture, 
  episode count, and Type** — this is more detail than the original written 
  spec. Card is clickable again.
- Infinite scroll, as originally specified — the sketch's fixed grid is just
  the mockup canvas, not a page limit. 
- All anime for the season, not just yours.

## Top anime page — `sketches/TopAnime_Page_Example.png`

- Row-based list: rank number, picture, title, my score, MAL score
  (right-aligned) — matches the original spec closely.
- The sketch adds an action button on the right of each row: **"add to list
  btn."** Since this page ranks anime overall (not just yours), a given row
  might not be on your list yet. That suggests the button should be
  **conditional** — "Add" if it isn't on your list, "Edit" (as shown
  on the My List sketch).

## My list page — `sketches/MyList_Page_Example.png`

- Row-based list: rank number (`#1`, shown when sorted by score), picture,
  title, my score, MAL score, and an "edit button" on the right. Edit button
  opens view/card on top of the page (this should happen everywhere where edit
  or add to list is, same way like in MAL). 
- Control buttons where there are All, watched, watching, plan to watch, plan to watch
  so that only these are shown, also include a filter button that has quick filters like
  by mal score, by my score, alphabetical and whatever else possible.
- The sketch includes a MAL score column here, which the original written
  spec didn't call for on this page. I've kept both scores plus type and progress 
  from the original text.
- Grouped and ordered as originally specified: Currently watching → On hold
  → Plan to watch → Completed → Dropped.

## Profile page — `sketches/Profile_Page_Example.png`

- Top row, three boxes side by side: **Anime stats** (left) | **Rating
  distribution** (center, wider) | **Latest updates** (right).
- Below that, three full-width stacked bars, in this order: **My top
  anime**, then **"I liked it, they didn't,"** then **"They liked it, I
  didn't."**
- **Latest updates:** scrollable within its box, with a button in the
  top-right corner that opens a full history of every edit as an overlay on
  top of the page — closes on Esc or click-outside. New detail, not in the
  earlier brief.
- **Top anime — logic has changed from the earlier version.** Anime scored
  10 are still always shown in full, no cap, same as before. But filling the
  remainder up to 10 now works differently: it **auto-fills by
  next-highest-score as a default**, and an edit button in the box's
  top-right corner opens a page listing the tied next-highest-scored anime
  so you can **manually choose** which ones occupy the remaining slots. This
  replaces the earlier automatic alphabetical tiebreak. It means the data
  model needs a small addition — somewhere to store which anime you've
  manually selected for the top-10 — that wasn't in the original plan.
- Rating distribution and opinion-divergence lists ("They liked it, I
  didn't" / "I liked it, they didn't") are unchanged from the earlier brief.

## Single anime page — `sketches/Anime_Page_Example.png`

- Top area: title, with a large picture on the left.
- Near the top right of the title: two separate boxes side by side — **"Rank
  and MAL score"** and **"My score + rewatch count"** — not merged into one
  block.
- Top-right corner: prequel/sequel link buttons, shown only if they exist
  for that anime.
- Below those: an info box (type, studio, aired-from/to, genre, and any
  other short-form fields available) and a synopsis/background box beneath
  it.
- Below the picture: a progress bar (`watched/total` or `watched/?`, plus
  current status) with an **edit button** next to it. Clicking it opens an
  overlay on top of the page for updating episodes watched, rewatch count,
  and score. **No start/finish date fields in this editor** — those are set
  automatically by the app's date logic, not edited directly.
- MAL score here still respects the global hide/unhide toggle, same as
  everywhere else it appears.

---

## Navbar

- **Center:** search, debounced, local-cache-first with live-API fallback
  for uncached titles, dropdown of up to 5 matches (picture + title).
- **Left:** Home, Seasonal anime, Top anime, Airing.
- **Right:** Profile, hide/unhide MAL score toggle, Settings (gear icon).

No sketch shows navbar button-level detail — every mockup just labels it
generically "Navbar" — so this section carries forward unchanged from the
earlier decision, including the settings icon we added after noticing it
had no entry point in the original spec.

## Settings page

No sketch was provided for this one. Carried forward unchanged from the
earlier plan:

- Sync status (pending entries, last successful sync time, failed/retrying
  entries)
- Manual "resync now" and full-reconciliation triggers
- Re-authorize with MAL (recover from an invalid refresh token)
- Force-refresh a specific anime's cached metadata on demand

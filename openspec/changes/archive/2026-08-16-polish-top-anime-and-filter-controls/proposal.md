## Why

The Top Anime page's top tiers don't yet read the way the ranking deserves: ranks 1–3 sit on literal coloured podium blocks (a stand under each card, with #1 shifted to the middle by CSS `order`, so the visual order is 2‑1‑3), and ranks 4–10 are small posters with a translucent dark pill in the corner — busy where it should be clean, and hard to tell at a glance which card is which rank. Neither tier has a hover highlight, unlike every other anime card in the app.

Separately, the Season and Search pages' filter rows are visibly ragged: the `Type` popover button and the `In my list` checkbox render taller than the neighbouring `Sorting`, `Year`, and `Season` selects, because a `<select>`'s inner line box is normalized by the browser while a `<button>`/`<label>` inherits the root's computed 23px line-height — same padding, different rendered height. And the native clear ("×") control in the search field keeps the default arrow cursor, so it doesn't read as clickable.

## What Changes

- **Redesign the Top Anime top-3 tier**: replace the podium-with-stands treatment with three showcase cards laid out left-to-right in rank order (1, 2, 3 — no `order` reshuffle, so DOM order and reading order finally agree). Each card is a self-contained bordered surface carrying a large gold/silver/bronze rank badge, the poster, title, both scores, and its Add/Edit action. Rank 1 keeps extra emphasis through its medal accent, not through a taller block underneath it.
- **Redesign the Top Anime ranks 4–10 tier**: keep the single 7-up row, but give each card a rank badge in the same visual family as the top-3 badge (a solid, high-contrast chip rather than today's translucent dark pill) so `#4`…`#10` is legible over any poster art.
- Give both card tiers the app's standard card hover/focus highlight, which they lack today while every other anime card has it.
- Show the top-3 cards' two scores as the app's labelled score chips (the standalone-figure form) instead of bare numbers, so which score is MAL's and which is mine is readable without decoding colour alone; ranks 4–10 and the flat rows keep the compact coloured-value form.
- **Normalize header/filter control heights**: introduce one shared control-height token and apply it so the `Type` filter, the `In my list` checkbox, and the sort/year/season selects render at exactly the same height on the Season page, and the `Type` filter and sort select match on the Search page. Because the `Type` control is the shared `FilterMultiSelect`, My List's filter bar is normalized to the same token in the same pass — otherwise fixing Season and Search would introduce a new mismatch there.
- **Give the search field's clear control a pointer cursor**, applied to every search-type input the app renders (the navbar search bar and the Settings refresh picker).

## Capabilities

### New Capabilities
<!-- None: every change lands on an existing capability. -->

### Modified Capabilities
- `library-views`: the Top Anime page's rank 1–3 and 4–10 presentation — currently specified as "larger rows with a gold/silver/bronze badge" — becomes an explicit three-tier presentation (showcase cards, top-10 card row, flat rows) with a per-tier requirement that the rank is unambiguous.
- `navigation-and-search`: the card hover-highlight requirement extends to the Top Anime page's showcase and top-10 cards; a new requirement covers the search field's clear control reading as clickable.
- `score-presentation`: the chip-vs-value density rule names the Top Anime showcase cards as a chip surface, while the top-10 cards and flat rows stay coloured values.
- `page-header-design`: a new requirement that the controls in a page's header/filter cluster share a single height.

## Impact

- Frontend only. No API, DTO, or backend change; no data-shape change to `TopAnimeItemDto`.
- Pages: `TopAnimePage.tsx` / `.css` (both card tiers rewritten; the flat rows for rank 11+ are untouched), `SeasonPage.css`, `SearchPage.css`, `MyListPage.css`.
- Components: `FilterMultiSelect.css` (button height), `ScoreChip` (reused as-is, not modified), `ScoreValue` (reused as-is — the MAL chip wraps it, so the hide/reveal behaviour is unchanged).
- Global: `index.css` gains a shared control-height custom property and a `::-webkit-search-cancel-button` cursor rule. The cursor rule is WebKit/Blink-only by nature — Firefox renders no native clear control, so it is a no-op there.
- Behaviour preserved throughout: pagination, the conditional Add/Edit action and its pending state, score hide/reveal, and the rule that podium styling only ever appears on page 1.

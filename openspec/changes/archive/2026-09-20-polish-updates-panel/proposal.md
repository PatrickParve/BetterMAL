## Why

Four things are wrong with the navbar's Updates panel, all of them visible on the same screen.

**The premiere-move wording reads backwards.** Update 76 (Reincarnated as a Sword Season 2) recorded a move from 8 Oct 2026 to 1 Oct 2026 — the premiere came *earlier* — and the card reads `Moved up to 1 Oct 2026 · was 8 Oct 2026`. "Moved up" is the English idiom for *sooner*, so the code is idiomatically right and reads wrong anyway: up/down is ambiguous about whether it means the date or the calendar. The word has to stop relying on that metaphor.

**Three controls never change the pointer.** The bell, the dropdown's History button and the history's two date fields keep the arrow (or an I-beam) on hover. The cause is structural: the app has no base cursor rule at all — 41 stylesheets set `cursor: pointer` by hand on their own controls, and these four were simply missed. Fixing only these four leaves the next one to be missed too.

**The history overlay scrolls in two places at once.** The list inside it is capped to its three newest rows and scrolls; the modal box around it is `overflow-y: auto` with `max-height: 85vh` and scrolls as well, because the list's cap is measured against the *window* rather than against the room the modal has left. On a shorter window both scroll, and the panel has two independent scroll positions for one list.

**The reason line is in romaji.** It reads `Sequel to Tensei shitara Ken deshita`, built server-side from the relation row's denormalised MyAnimeList title, while the card's own title directly above it reads "Reincarnated as a Sword Season 2". `navigation-and-search` already requires the English title wherever an anime is named; this line predates that being applied to composed text.

## What Changes

**Premiere-move wording**

- A premiere that moves earlier reads `Moved earlier to <date> · was <date>`; a premiere that moves later keeps `Delayed to <date> · was <date>`. The up/down metaphor is dropped entirely, in favour of two words that cannot be read in reverse.
- Where the two dates are equal — possible today, because the card's "to" date is the anime's live premiere date while the "was" date is the one recorded, so a date that moved and moved back lands on itself — the line states the move without claiming a direction rather than printing `Moved earlier to 1 Oct · was 1 Oct`.

**Pointer cursor**

- `index.css` gains one base rule: every enabled `<button>` and every element carrying `role="button"` shows the pointer cursor, and `input[type="date"]` does too. This fixes the four reported controls and every control anywhere in the app that has the same gap, and makes it impossible for a new one to be added without it. The 41 per-component rules stay where they are — they become redundant, not wrong, and removing them is not worth the diff.

**Updates history overlay — one scroll**

- The modal box stops scrolling. It becomes a flex column that never overflows (the treatment `.modal--rank` already uses), with its title row and its filter row fixed in place and the list the single scrolling region inside it.
- The list stops being capped to three rendered rows and instead fills whatever height the panel has left, up to the panel's 85vh ceiling. Short histories still make a short panel; long ones fill the screen and scroll once. **This drops the "exactly three newest rows" opening height for the history only** — the navbar dropdown keeps it, where the argument for it (a compact panel hanging under a navbar control) still holds.
- The filter row is re-laid out so the search field, the date range and Clear fit on one line at the overlay's width without wrapping into the space the list needs.

**English titles in the reason line**

- The affiliated anime in the reason is named by its English title when the app has one cached, falling back to its MyAnimeList title otherwise — `Sequel to Reincarnated as a Sword`, not `Sequel to Tensei shitara Ken deshita`. No extra request: the far end of a qualifying relation is a non-dropped entry of my own, so its metadata is already cached, and the resolver already loads those rows.
- Where an anime is affiliated with more than one entry, the tie-break that picks which one to name follows that same displayed title, so the choice matches what is shown.
- **Knock-on, included deliberately:** the same resolved-relation record feeds the detail page's Prequel/Sequel buttons, whose hover tooltips name the related anime in romaji for the same reason. They are fixed by the same field rather than left as the one place the rule still fails.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `anime-updates`: a premiere-date change states its direction in words that do not depend on an up/down metaphor, and states no direction where it has none to state; the reason names its affiliated anime by the title the app displays for it; the history overlay is a single-scroll panel whose list fills it, replacing the three-row opening height, with the hidden scrollbar it shares with the dropdown kept as it is.
- `navigation-and-search`: every enabled control the app renders as clickable shows the pointer cursor, as a rule of the app rather than of each component; "English title preferred for display" is extended to cover an anime named inside a composed line, not only a title shown on its own.
- `anime-detail`: the Prequel and Sequel controls name their target in the hover tooltip by the same displayed title every other surface uses.

## Impact

- **Frontend** — `components/Updates/updateText.ts` (the premiere-change line), `components/Updates/UpdatesHistoryOverlay.tsx` and `.css` (single-scroll layout, filter row, dropping `useCappedCardHeight` for this surface while keeping the list node the seen-tracker observes), `components/Modal.css` (a column variant for a non-scrolling modal box), `index.css` (the base cursor rule), `components/Updates/UpdatesMenu.css` (unchanged behaviour, redundant cursor rules not added), `pages/AnimeDetailPage.tsx` (tooltip title), `api/types.ts` (`ResolvedRelationDto.englishTitle`).
- **Backend** — `Services/Relations/ResolvedRelationEdge.cs` gains `EnglishTitle`, filled at its three construction sites in `Services/Relations/RelationResolver.cs` from the far end's cached metadata; `Services/Updates/AnimeUpdateService.cs` builds the reason from it; `Services/Updates/AnimeUpdateRelevance.cs` tie-breaks on it; `Services/Detail/ResolvedRelationDto.cs` carries it to the detail page.
- **No migration, no new API call, no DTO removal.** `AnimeUpdateDto` is unchanged: the reason it carries is still one composed string.
- **Not changing** — the hidden scrollbar on both lists; the dropdown's three-card opening height and its overscroll containment; what gets recorded as an update and when; seen-tracking, which keeps observing the same list node; the 41 existing per-component cursor rules.
- **Known gap left open (not this change):** `anime-updates` says the values a schedule change moved between are reported *as recorded*, but no new premiere date is stored on the update row — the card reads the anime's current premiere date as the "to" value. A second move therefore rewrites an older card's "to" date, and can in principle invert the direction word this change introduces. Closing it needs a `NewStartDate` column, a migration and a backfill decision for existing rows, which is a change of its own; the equal-dates guard above is the part of it that costs nothing here.

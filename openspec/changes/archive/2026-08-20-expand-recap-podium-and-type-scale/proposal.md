## Why

The recap's podium celebrates three anime and then stops: the row of cards is capped at 640px inside a lead column that is often half again as wide, so the section's right-hand third is empty while ranks 4 and 5 — the rest of a period's genuinely good shows — are demoted to the same grey row as rank 10. Around it, the page's supporting text was tuned when the podium was small: 12–13px labels, meta, and distribution rows now read as fine print beside the cards, the top-5 score chips are content-sized so no two line up, and #1's decorative motion is a sheen that crosses only the card's top 44px band and then vanishes for most of its cycle. Two smaller defects sit alongside: a followable stat whose count is zero still wears the accent rail and accent hover of a tile that leads somewhere, and the score board's posters are drawn at roughly half the resolution their source art carries, so they read soft.

## What Changes

- **The podium goes from three cards to five.** Ranks 1–5 are cards; ranks 6–10 continue as rows in the list design they use today, numbered six through ten. Ranks 1–3 get *larger* than they are now, and 4 and 5 are smaller than 3, so the row still descends in size left to right. The 640px cap comes off and the five cards fill the full width of the top-10 section.
- **Podium score chips become one size.** Every card's score sits in an identically sized box — the same on rank 5's small card as on rank 1's large one — wide enough for a two-digit score (`10`) and for MAL's two-decimal figure, so the five scores read as a column rather than as five differently-sized blobs.
- **A zero-count stat tile reads as an aggregate tile.** A followable stat with nothing to count (Dropped on a period with no drops) currently keeps the accent rail and the accent hover of a tile that leads somewhere, promising a destination it does not have. It takes the aggregate tile's quieter resting marker and quieter hover instead. **BREAKING** for the existing spec scenario "A zero-count stat is highlighted but not followable", which asserted the opposite.
- **#1's decorative motion becomes a slow, continuous wave across the whole card.** In place of the 5s band sweep confined to the card's top edge — which spends much of its cycle off-card — a soft highlight drifts across the entire card at a markedly slower pace, seamlessly and without ever disappearing. The poster is excluded (it is opaque art, not a surface to light); the title and the score chip are not — the wave passes behind the score exactly as it passes behind the card, so the score lights up with the surface rather than sitting inert on top of it.
- **The score board's posters are drawn at their source resolution.** Board tiles are enlarged and their box is re-cut to the poster art's true 425×600 proportion, so a tile on a high-density display asks for about as many pixels as the source actually has instead of roughly half again fewer, and `object-fit: cover` stops cropping to a taller box than the art.
- **The recap's supporting text steps up one size.** The top-10 section (podium card titles, rank/score cells, and the rows beneath), the stat tiles, the rating distribution, all four season/year ranking lists, and the hot takes move up a step from the 12–13–14px scale they use today, so the page reads at the same weight as the artwork on it.

## Capabilities

### New Capabilities

None — every change refines behaviour already covered by `list-recaps`.

### Modified Capabilities

- `list-recaps`:
  - "The top three are presented as a podium" becomes a **top five** podium — five cards, descending in size left to right, filling the section's width, with ranks 4 and 5 visually subordinate to the three medalled ranks and every card's score box a single shared size.
  - "The podium is animated" — the first card's continuous decorative motion is respecified: it covers the whole card except its poster, is continuous rather than intermittent, is slow, and carries the score chip with it.
  - "Top 10 of the period" — the split is now five cards plus ranks 6–10 as rows; the "three or fewer entries" case becomes "five or fewer".
  - "Drilling into a recap stat" and "The stat block is headed and responds to hover" — a zero-count followable tile now takes the aggregate treatment at rest and on hover, rather than the accent one.
  - Two new requirements sit alongside the existing ones, changing nothing they say: **"Board posters are drawn at their source resolution"** (tile size and proportion matched to the poster art, on standard and high-density displays alike) and **"The recap's supporting text sits on one legible scale"** (the top 10, stats, distribution, and ranking lists set above their present fine-print sizes).

## Impact

- **Frontend only, presentation only.** `RecapPage.tsx` (podium slice 3→5, rank-4/5 card variant, chip sizing, zero-count tile branch), `RecapPage.css` (podium grid, wave keyframes, type scale, stat tile rail/hover), `RankingSection.css`, `ScoreDistribution.css` (compact size), and `ScoreBoardOverlay.css` (tile size and aspect).
- **No backend, DTO, API, or data change.** The podium already receives all ten ranked entries; showing five as cards is a slice boundary, not a new request. `TOP_TEN_SIZE` stays at 10.
- **No new colour tokens.** Ranks 4 and 5 alias the existing neutral `--border`/`--text`/`--code-bg` set through the card's existing `--medal*` local aliases, so nothing new lands in `index.css`.
- **Reuses existing components** — `ScoreChip` and `ScoreValue` still carry the podium scores, so hidden MAL scores and the reveal control keep working untouched.
- **Spec-covered behaviour that must not regress**: the podium's rank-order reading/keyboard order, the "no layout reflow" guarantee on every animation, the reduced-motion switch-off, "Recap rows highlight on hover" for ranks 6–10, hidden MAL scores under the MAL basis, and the board/distribution count agreement.
- **Assumption stated for review**: "make the score box of the top 5 same size to fit number 10 in" is read as *one shared chip size across the five cards, sized for the widest score either basis can print*, not as matching the row list's score cell.

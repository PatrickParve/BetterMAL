## Why

The airing cue on a series-timeline card is currently a blue ring plus a soft outer glow that pulses. Blue is the page's MAL/broadcast colour, but a glowing ring around one card in a row reads as *selection* — "this is the card you picked" — not as "this season is on air right now". The cue is also purely graphical: nothing on the card says the word, so the meaning has to be inferred from chrome.

## What Changes

- The airing cue becomes a small green **Airing** pill in the card's air-range line, where the "No air date" tag already lives for undated cards, so a currently-airing card states in words what it is. The pill takes the place of the range's `– ongoing` wording, which said the same thing more quietly.
- The card-level cue becomes the card's existing 1px border recoloured green — no ring, no glow, no pulse — so a still-airing season is still findable when scanning a long franchise row without any card looking selected.
- The blue ring, the outer glow, and the pulse animation are removed, along with the reduced-motion suppression that existed only for that animation and the visually-hidden "Currently airing" text that existed only because the ring was graphics-only (the pill is now real text).
- Green is introduced to the series page as a third page-scoped colour meaning "on air now", alongside the existing blue-is-MAL / purple-is-mine convention. Broadcast progress fills stay blue — green marks the *state*, blue still measures the *broadcast*.
- Card geometry does not change: no new rows, no size change, no change to the poster, chips, fills, or footer, and nothing is drawn over the poster art.

## Capabilities

### New Capabilities

None — this changes how an existing indicator is presented.

### Modified Capabilities

- `series-page`: the *Series timeline ribbon* requirement currently mandates that the airing indicator be a ring in the page's broadcast colour, accompanied by accessible text, animated-but-reduced-motion-aware, and never clipped by the scroll container. That is replaced by a green textual pill in the card's air-range line plus a green card border. The *Score and progress colour language* requirement gains green as the page's "currently airing" state colour, distinct from blue-for-MAL/broadcast and purple-for-mine.

## Impact

- `frontend/src/components/SeriesTimeline.tsx` — render the green pill in the air-range line for a currently-airing entry; stop appending `– ongoing` to that entry's range; drop the visually-hidden "Currently airing" span.
- `frontend/src/components/SeriesTimeline.css` — replace the `--airing` card ring/glow/pulse rules with a green border colour; add the pill; delete the keyframes, the reduced-motion block, and the now-unused `sr-only` class.
- `frontend/src/pages/SeriesPage.css` — add page-scoped `--airing*` colour aliases next to the existing `--mal*` / `--mine*` aliases.
- No backend work and no API change: `SeriesEntryDto.airingStatus` already drives this cue.

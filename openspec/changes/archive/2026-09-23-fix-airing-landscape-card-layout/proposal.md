## Why

The Airing page's slots are row picture slots: the picture sits beside the title, and a wide picture is drawn whole at up to 16/9 of the slot's 72px height, which is 128px. A day column is only a seventh of the page. At 1280px it is about 165px wide, and at 1440px about 188px. Inside the slot's padding, a 16:9 key visual leaves the title between roughly 10px and 30px, so the title breaks a few letters per line and the two-line clamp shows fragments. The episode pill is `content-box` with `max-width: 100%` and 14px of horizontal padding, so in a column that narrow it runs past the slot's right edge. The row rule assumes a row wide enough to give the picture extra width. A seventh of a page isn't. Even a plain poster leaves the title only about 20px at the 801px breakpoint.

## What Changes

**Each slot becomes a small media card: picture band on top, title beneath.** The picture no longer sits beside the text. It fills a **band** across the full width of the slot, and the title gets the whole card width below it on its two reserved lines. The picture's shape can no longer take space from the text, because the text doesn't share a row with it.

**The band draws every picture whole, posters included.** The band is landscape, so the shape that used to cause the problem now fits it best. Its height follows the card's width, at roughly 16:9, clamped between 64px and 96px. At common laptop widths, a 16:9 key visual fills the band edge to edge. A poster is drawn whole and centred at the band's height, and a wider or narrower picture is drawn whole as large as the band allows. Any leftover space simply shows the band's own flat background: no blurred copy of the picture behind it, and nothing drawn over it. The band's size never depends on the picture, so nothing moves as pictures load, and every slot in the week stays the same height.

**Time and episode move into a header strip above the band.** The air time sits at the strip's left and the episode label (`Ep 12`, `Ep 1-8`, `Ep —`) at its right, as the plain pill it has always been. Nothing is drawn over the picture, so a poster is never covered. The strip is bounded by the card, and anything too long is cut short with an ellipsis, so an episode label can no longer run past the slot's edge. On the narrowest cards the strip tightens so an ordinary label still fits. Reading order stays time, then title, then episode.

**The narrow layout tiles the cards.** At 800px and below, where the days already stack into one column, each day's slots wrap into a grid of equal cards instead of one card stretched across the whole width. The band keeps sensible proportions on a phone or tablet.

**Unchanged:** everything else on the page, including the header, week navigation, jump selectors, today mark, grouping, merged ranges, empty-week message and the week bounds. Every other row slot keeps its behaviour, the dashboard's "Airing today" list included.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `airing-schedule`: the "Seven day-column layout" requirement changes what a slot looks like: a header strip with the time and episode number, a picture band beneath it, and the title beneath that at the slot's full width. Nothing is drawn over the picture. It adds that the title and episode number always stay inside the slot box whatever the picture's shape, and it adds the narrow tiled layout. The same-height, two-line title and reserved episode row rules are kept.
- `artwork-presentation`: the airing page's slots move out of the row picture slots and into a new fourth card family, the **banner box**. A banner box is wider than the picture it holds, so every shape is drawn whole inside it, a poster included, and any leftover space shows the box's own flat background, with no blurred fill. The three-shapes and fill requirements get an exception for a banner box. The banner keeps native lazy loading.

## Impact

**Frontend only.** No backend, API, DTO, schema or dependency change.

- `frontend/src/pages/AiringPage.tsx`: new slot markup (header strip, band through `PosterPicture`, title). No longer imports `RowPicture`.
- `frontend/src/pages/AiringPage.css`: slot card, header strip, band sizing, and the narrow tiled grid. Removes the `__body`, `__thumb` and `__info` rules.
- `frontend/src/components/PosterPicture.tsx` / `.css`: an opt-in "whole" mode for banner boxes. Every shape, poster included, is drawn with `contain`, and no blurred fill is mounted. Existing hosts are unaffected because they don't opt in.

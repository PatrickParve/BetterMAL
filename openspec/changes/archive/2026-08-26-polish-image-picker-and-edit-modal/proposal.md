## Why

Three rough edges left over from the artwork-and-title work, all of them things the app does slightly wrong every time they are used.

The **picture picker** crops. Every option is forced into a fixed 2:3 poster tile and `object-fit: cover` cuts away whatever does not fit, which is the one thing a picker must never do: it is asking me to choose between pictures while hiding parts of them. Landscape artwork — key visuals, banners, the very pictures worth choosing over MAL's default — is reduced to a centre slice with its sides gone, so two wide pictures that differ only outside the crop look identical in the grid. The pages themselves already know better: both the detail page and the series page render landscape artwork whole, at its own proportions. Only the picker that chooses it does not.

The **series page's Choose picture and Choose title controls** sit in the header block, wedged under the MyAnimeList/AniList/SeriesGraph links. They read as more external links, they push the score chips and progress bar further down the page, and they sit nowhere near the Rebuild control — the other button on the page that changes what the series *is* rather than navigating away from it.

The **entry editor survives navigation.** It is mounted at the app root so any page can open it, and nothing tells it a page change has happened. Opening the editor on an anime, then pressing back or forward, leaves the overlay hanging over an unrelated page — still holding the page behind it from scrolling, still editing the anime I have just navigated away from — until it is dismissed by hand. The same is true of every overlay that outlives the view it was opened from: the ranking editor and the completion prompt at the app root, and the detail page's own overlays when moving from one anime to the next, since that route keeps the page mounted and only swaps its id.

## What Changes

**The picture picker shows every option whole**
- Each option is rendered at its own proportions rather than cropped to a poster tile: a portrait picture appears portrait, a landscape picture appears landscape and wider than its portrait neighbours, and no part of any option is cut off.
- Options are laid out to a common height rather than a common width, so a mixed set stays in tidy rows, and that height is smaller than today's tile so more options fit on screen at once.
- This is one component shared by the anime detail page and the series page, so both pickers change together.

**The series page's artwork controls move next to Rebuild**
- **Choose picture** and **Choose title** move out of the header block and into the Series stats row alongside **Rebuild**, presented as that row's controls are.
- The rules governing them are unchanged: Choose picture appears only when there is more than one picture to choose between, Choose title always appears, and neither touches a member anime or MyAnimeList.

**An overlay closes when its page goes away**
- Any overlay is dismissed when the app navigates to a different page than the one it was opened over — back, forward, a link, or the navbar — instead of hanging over the new page.
- It closes through its own close path, so an overlay with work to flush on dismissal (the ranking editor saves a pending arrangement) still does it, and closing on navigation is not treated as a save.
- This is a property of the overlay mechanism, not of any one overlay, so it holds for the entry editor, the ranking editor, the completion prompt, the picture and title pickers, and every overlay added later.

## Capabilities

### New Capabilities

None.

### Modified Capabilities
- `artwork-selection`: the picture picker's presentation — every option shown whole at its own proportions, laid out to a common height, sized so more fit on screen.
- `series-page`: the **Choose picture** and **Choose title** controls sit in the Series stats row beside **Rebuild** rather than in the page header block.
- `overlay-behaviour`: an open overlay closes when the app navigates away from the page it was opened over.

## Impact

**Frontend only.** No backend, API, database, or MAL-facing change; nothing here alters what is stored or fetched.

- `components/PicturePickerOverlay.css` — the option tile stops being an aspect-ratio-locked box with a cropping image, and becomes a common-height row item whose width follows the artwork.
- `components/Modal.tsx` — the shared overlay wrapper learns the route it was opened on and calls `onClose` when the path changes, alongside its existing Escape and click-outside handling. Every overlay in the app already goes through it, so no overlay needs its own version of this.
- `pages/SeriesPage.tsx` / `pages/SeriesPage.css` — the two artwork controls move from `series-page__artwork-controls` in the header into the Rebuild row; the now-empty controls block goes.

**Non-goals** (flagged, deliberately not in scope)
- Changing what the pickers offer, how a choice is stored, validated, or applied. Only how the options are drawn.
- Any change to how the detail page or the series page renders the picture it has already chosen — both already show landscape artwork whole, and this change brings the picker in line with them, not the other way round.
- Confirming before an overlay closes on navigation, or restoring it on return. A navigation dismisses it, exactly as Escape does.
- Preserving an overlay across a change of query parameters on the same page — filters, sort, and pagination are not a page change.
- Moving the anime detail page's own Choose picture control, which stays where it is beside Refresh data.

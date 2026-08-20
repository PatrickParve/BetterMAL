## MODIFIED Requirements

### Requirement: Top anime ranked list
The system SHALL present a Top anime page as a ranked list covering up to rank 500, where every entry — whatever form it takes — shows its rank, picture, title, my score, and MAL score. The page shows one selected ranking list at a time (see "Top anime ranking list selector"); everything below applies to whichever list is selected. Because this page ranks anime overall, an entry's anime may not be in my list; the flat-row tier (rank 11 and beyond) SHALL additionally carry a list-action button, conditional — "Add" when the anime is not yet in my list, and "Edit" when it is — the showcase and top-ten tiers (ranks 1–10) SHALL NOT carry one, keeping those tiers focused on the ranking and scores alone. The list SHALL be paginated at 50 entries per page, with page-number controls plus left/right arrows at the bottom, and left/right arrow controls at the top-right.

The page-number controls SHALL show the first page, the last page, and the current page with at most one page on each side of it, collapsing any gap between those groups into an ellipsis. On page 6 of 10 this yields `1 … 5 6 7 … 10`.

Changing the page from the bottom controls (page-number buttons or arrows) SHALL scroll the page back to the top, so the newly-shown entries are visible without the reader having to scroll up manually. The top-right arrows need no such scroll, since they already sit at the top of the page.

Changing the page, by any control, SHALL add a step to the browser's own navigation history rather than only updating local view state. Going back (the browser's back button, a keyboard shortcut, or a trackpad swipe gesture) SHALL therefore step to the previously-viewed page of the ranking, and going forward again SHALL return to the page just left — the same way back/forward already move between any other two pages in the app. Only once that page-by-page history is exhausted SHALL going back leave the Top anime page entirely, for whichever page preceded it in the browsing session.

The ranking SHALL be presented in three tiers, each visually distinct from the next, so the shape of the page itself communicates where an anime sits in the ranking:

- **Ranks 1–3 — showcase cards.** Three cards, each a self-contained bordered surface carrying its rank, poster, title, and both scores. They SHALL be laid out in ascending rank order following the reading direction, so the first card is rank 1; the presentation SHALL NOT reorder them visually away from their document order. Each card SHALL carry a prominent rank badge in a gold, silver, and bronze family for ranks 1, 2, and 3 respectively, and the card SHALL pick up its own medal colour beyond the badge (for example in its border and surface tint) so the three are distinguishable from one another at a glance and from every other tier. Rank 1 SHALL read as the most prominent of the three. The medal colours SHALL render the same in light and dark mode, since a medal's colour is its meaning. The card's two score chips SHALL be the same size as each other regardless of their label or value text differing in length, so the pair reads as one deliberate row rather than two mismatched boxes.
- **Ranks 4–10 — top-ten card row.** A single row of poster cards, smaller than the showcase cards, each carrying its own rank badge, title, and both scores, inside a bordered, coloured box of its own so the tier reads as a defined group rather than loose posters. The badge SHALL remain fully legible over any poster artwork, bright or dark, rather than relying on a translucent overlay whose contrast depends on the art beneath it.
- **Ranks 11 and beyond — flat rows.** The existing full-width rows, each with plain `#N` rank text, poster, title, my score, right-aligned MAL score, and its action button.

The showcase tier's medal identity SHALL be drawn from the application-wide medal colours — the same gold, silver, and bronze the recap podium uses — rather than from a palette local to this page, so the two surfaces that rank a top three in the app cannot drift apart in colour. Its rank badge SHALL take the same form as the recap podium's: a medal-coloured outline around a neutral fill, carrying the rank as a bare number. The medal treatment carried over SHALL be colour and badge form only; the podium's own sizing, entrance animation, hover response, and rank-1 sheen SHALL NOT follow it onto this page.

A showcase card's score chips SHALL be compact — sized for the narrow column beside the poster rather than to the app's default chip width — and within them the role label ("My score", "MAL") SHALL be large enough to read as a label rather than as fine print, at a size closer to its own value's than today's, while staying subordinate to that value.

Both card tiers (ranks 1–3 and 4–10) SHALL render an entry's poster in the same proportions the anime pages give that poster, so the artwork shown here is the artwork the anime's own detail page shows, not a differently-cropped portion of it. Neither tier SHALL crop a poster to proportions narrower than that box in order to fit its layout. The flat-row tier's small thumbnail is unaffected by this rule.

The rank of any entry SHALL be readable from the entry itself, in every tier, without relying on its position in the layout.

Because a page holds 50 entries, the showcase and top-ten tiers SHALL appear only on page 1; every entry on page 2 and beyond SHALL use the flat row form.

#### Scenario: Rendering the top-anime ranking
- **WHEN** the top-anime page loads
- **THEN** it shows the first page of 50 entries of the selected list, each with its rank, picture, title, my score, and MAL score, plus a list-action button for entries in the flat-row tier

#### Scenario: Paginating the ranking
- **WHEN** I use the page-number controls or the left/right arrows (at the bottom or top-right)
- **THEN** the list shows the corresponding 50-entry page, up to rank 500

#### Scenario: Changing pages from the bottom scrolls back to the top
- **WHEN** I use the bottom page-number controls or arrows to change the page, from anywhere on the page
- **THEN** the page scrolls back to the top so the newly-shown entries — the showcase tier on page 1, or the first flat rows on later pages — are visible immediately

#### Scenario: Going back steps to the previous page of the ranking
- **WHEN** I change from page 1 to page 2, then to page 3, and then go back — via the browser's back button, a keyboard shortcut, or a trackpad swipe gesture
- **THEN** I return to page 2 of the ranking, not to whatever page I was on before I opened Top anime

#### Scenario: Going forward returns to the page just left
- **WHEN** I go back from page 3 to page 2 as above, then go forward
- **THEN** I return to page 3

#### Scenario: Going back past the first page leaves Top anime
- **WHEN** I am on page 1 of the ranking, having reached it without changing pages or lists since arriving, and go back
- **THEN** I leave the Top anime page for whatever page I was on immediately before it

#### Scenario: Page numbers in the middle of the range
- **WHEN** I am on page 6 of 10
- **THEN** the page-number controls show 1, an ellipsis, 5, 6, 7, an ellipsis, and 10 — and no other page numbers

#### Scenario: Page numbers near an edge of the range
- **WHEN** I am on page 2 of 10
- **THEN** the page-number controls show 1, 2, 3, an ellipsis, and 10, with no ellipsis between adjacent pages

#### Scenario: Flat-row entry not in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is not in my list
- **THEN** its button reads "Add"; using it adds the anime with status Plan to watch and the button changes in place to "Edit"

#### Scenario: Flat-row entry already in my list
- **WHEN** a flat-row (rank 11+) top-anime entry's anime is already in my list
- **THEN** its button reads "Edit" and opens the editor overlay in place when used

#### Scenario: Top 3 stand out from the rest of the ranking
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 1, 2, and 3 render as three showcase cards, each carrying a gold, silver, or bronze rank badge and matching card accent, visibly different from both the top-ten card row and the flat rows below

#### Scenario: The top 3 read in rank order
- **WHEN** I look at the three showcase cards
- **THEN** they run 1, 2, 3 in reading order, and each card states its own rank

#### Scenario: The showcase and the recap podium agree on gold
- **WHEN** I compare a Top anime showcase card with the recap page's podium card of the same rank
- **THEN** both carry the same medal colour and the same badge form, in light mode and in dark mode alike

#### Scenario: The podium's motion does not follow its colours
- **WHEN** the top-anime page's first page renders
- **THEN** the showcase cards appear without an entrance animation, rank 1 carries no sweeping sheen, and hovering a card does not lift it

#### Scenario: A showcase card's two score boxes match
- **WHEN** I look at a showcase card's "My score" and "MAL" chips
- **THEN** the two boxes are the same width and height as each other, even though "My score" is longer text than "MAL"

#### Scenario: A showcase chip's label is readable
- **WHEN** I look at a showcase card's score chips
- **THEN** each chip's label is legible beside its value rather than reading as fine print, and the chip itself takes no more room beside the poster than its two short lines need

#### Scenario: A top-ten poster matches the anime's own page
- **WHEN** I open the detail page of an anime shown in the showcase or top-ten tier
- **THEN** its poster there is the same picture, showing the same extent of the artwork, as the Top anime card showed — neither is a narrower crop of the other

#### Scenario: Ranks 4 to 10 form their own tier
- **WHEN** the top-anime page's first page renders
- **THEN** ranks 4 through 10 appear as one row of cards, each stating its rank, sized and styled distinctly from both the showcase cards above and the flat rows below

#### Scenario: A rank badge stays legible over bright poster art
- **WHEN** a top-ten card's poster art is bright, pale, or visually busy
- **THEN** its rank badge is still fully legible, because the badge does not depend on contrast against the artwork behind it

#### Scenario: Medal colours survive a theme switch
- **WHEN** I switch between light and dark mode
- **THEN** the gold, silver, and bronze accents on ranks 1, 2, and 3 still read as gold, silver, and bronze

#### Scenario: Card tiers do not appear on later pages
- **WHEN** I view page 2 or later of the top-anime ranking
- **THEN** every entry on that page uses the flat row form, since ranks 1–10 only appear on page 1

#### Scenario: A filtered list is ranked in its own right
- **WHEN** I view the Movie list
- **THEN** its entries are numbered from rank 1, with the top three in the showcase tier, rather than carrying their positions in the overall ranking

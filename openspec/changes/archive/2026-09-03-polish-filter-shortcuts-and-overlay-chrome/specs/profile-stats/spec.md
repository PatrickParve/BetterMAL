## ADDED Requirements

### Requirement: Profile lists and the rankings overlay show no scrollbar
The scrollable vertical lists on the profile page and in its overlays SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the rows are the content, and a bar drawn beside them inside an already-small box reads as chrome.

This SHALL apply to the "Latest updates" feed, both opinion-divergence lists ("They liked it, I didn't" and "I liked it, they didn't"), the full edit-history overlay's list, and the list in the rankings "See all" overlay — which, being the same overlay the recap page's rankings open, SHALL therefore show no scrollbar there either, for Season ranking, Year ranking, Seasons by time watched, and Years by time watched alike.

Hiding the scrollbar SHALL cost none of these lists any scrolling: a list holding more rows than fit SHALL still scroll by wheel, trackpad, keyboard, and drag exactly as it does today, and every row it holds SHALL remain reachable.

No gutter SHALL be reserved where the scrollbar was: the width it occupied SHALL be given back to the rows.

#### Scenario: No bar beside the feed
- **WHEN** the "Latest updates" feed holds more rows than fit
- **THEN** no scrollbar is drawn beside or over its rows, at rest or while scrolling

#### Scenario: No bar in the divergence lists
- **WHEN** an opinion-divergence list holds more than ten anime
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: No bar in the edit-history overlay
- **WHEN** the full edit-history overlay holds more rows than fit
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: No bar in a "See all" overlay
- **WHEN** I open the "See all" overlay on a ranking holding more than eight rows, from the profile page or from the recap page
- **THEN** no scrollbar is drawn beside its rows

#### Scenario: Scrolling still works
- **WHEN** I make a wheel gesture over any of these lists, or drag inside it
- **THEN** it scrolls exactly as it did when it had a scrollbar, and its last row is reachable

#### Scenario: The rows take the gutter back
- **WHEN** one of these lists is drawn without its scrollbar
- **THEN** its rows extend to the edge the scrollbar's gutter used to hold, with no empty strip beside them

## MODIFIED Requirements

### Requirement: Poster strips show no scrollbar
The "My top anime", "Top series", and "Most rewatched" strips SHALL NOT render a visible scrollbar, in any browser, whether or not they overflow — the posters are the content and a bar under them is noise.

Hiding the scrollbar SHALL NOT cost the strips any scrolling: a strip that holds more entries than fit SHALL still scroll by wheel gesture and by dragging it, exactly as it does today.

The profile page's vertical lists SHALL be drawn without a scrollbar for the same reason, under "Profile lists and the rankings overlay show no scrollbar".

#### Scenario: No bar under the posters
- **WHEN** a strip holds more entries than fit across it
- **THEN** no scrollbar is drawn under or over the tiles, at rest or while scrolling

#### Scenario: Scrolling still works
- **WHEN** I drag an overflowing strip, or make a horizontal wheel gesture over it
- **THEN** it scrolls exactly as it did when it had a scrollbar

#### Scenario: Vertical lists are drawn the same way
- **WHEN** the "Latest updates" feed has more rows than fit
- **THEN** it too is drawn without a scrollbar

## REMOVED Requirements

### Requirement: Scrollbars sit beside scrollable content
**Reason**: Reversed. The requirement existed to stop a scrollbar being drawn over a row's right-hand edge; the answer adopted here is to draw no scrollbar on these lists at all, which settles the overlap it guarded against and removes the chrome as well. Its subject — the "Latest updates" feed, both opinion-divergence lists, and the full edit-history list — is exactly the set the replacement covers.

**Migration**: Replaced by "Profile lists and the rankings overlay show no scrollbar", which covers the same three lists plus the rankings "See all" list. No row can be covered by a scrollbar that is not drawn, so every scenario this requirement asserted is satisfied by the replacement.

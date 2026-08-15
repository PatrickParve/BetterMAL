## MODIFIED Requirements

### Requirement: Pages do not scroll or drift horizontally
The system SHALL keep every page's content fixed horizontally. A sideways trackpad gesture, a horizontal wheel, or a swipe SHALL NOT slide the page, rubber-band it, or reveal anything beside it — including when the gesture starts inside a horizontally scrolling region (the currently-watching carousel, the series timeline, the profile page's poster strips) and that region is already at either end of its scroll.

This constrains the page's own content only. The browser's built-in back/forward gesture — a two-finger sideways swipe on a trackpad over ordinary page content — SHALL keep working on every page, and SHALL move through history exactly as the back and forward buttons do. Suppressing that gesture is NOT a way to satisfy this requirement.

The gesture's own native visual feedback — the browser's live slide or rubber-band preview while the two fingers are still down, before the gesture resolves — is the browser's gesture chrome, not the page's content, and is an accepted consequence of keeping the gesture itself. The web platform gives no way to keep the gesture recognized while hiding that preview; suppressing it would mean not satisfying the requirement above instead.

Horizontal scrolling SHALL remain available inside those regions themselves: a sideways gesture over a strip SHALL still scroll the strip, and SHALL simply stop at the strip's ends rather than passing the remaining movement on to the page. A gesture that starts over such a region SHALL NOT navigate through history either, at any point in that region's scroll range including both ends — so browsing a strip sideways can never carry the user off the page by accident.

No page SHALL present a document-level horizontal scrollbar at any window width the app supports.

#### Scenario: Sideways gesture on an ordinary page
- **WHEN** I swipe left or right with two fingers on a page with no horizontal region under the pointer
- **THEN** the page's own content does not drift independently of the gesture, and the browser's back/forward gesture is what responds — including whatever native slide or rubber-band preview the browser itself shows while the gesture is in progress

#### Scenario: Swipe back and forward
- **WHEN** I open an anime's detail page and then swipe right with two fingers over ordinary page content
- **THEN** the browser navigates back to the page I came from, and swiping left from there returns me to the detail page

#### Scenario: Sideways gesture at the end of a strip
- **WHEN** I keep swiping sideways over a poster strip that has already reached its last tile
- **THEN** the strip stays at its end, the page behind it does not move, and no back or forward navigation happens

#### Scenario: Strips still scroll
- **WHEN** I swipe sideways over a poster strip, the carousel, or the series timeline that has more content
- **THEN** that region scrolls as it does today, including its drag-to-scroll behaviour

#### Scenario: No horizontal scrollbar
- **WHEN** any page is loaded at any supported window width
- **THEN** the document shows no horizontal scrollbar

## ADDED Requirements

### Requirement: Horizontal regions scroll on one axis only
Every horizontally scrolling region in the app — the main dashboard's currently-watching carousel, the series timeline, and the profile page's "My top anime", "Most rewatched", and "Top series" strips — SHALL move on the horizontal axis only. A two-finger gesture over such a region SHALL NOT shift its contents up or down by any amount, however small, and the region SHALL NOT present a vertical scrollbar.

The vertical component of a gesture made over one of these regions SHALL scroll the page as it would anywhere else, rather than being absorbed by the region.

The hover treatments on the cards and tiles inside these regions SHALL continue to render in full, unclipped at every edge, exactly as the existing hover and edge-reserve requirements demand — constraining the axis SHALL NOT come at the cost of cutting off a card's highlight.

#### Scenario: Carousel does not drift vertically
- **WHEN** I make a two-finger gesture over the currently-watching row with any vertical component
- **THEN** the row's cards do not shift up or down at all, and only their horizontal position changes

#### Scenario: Vertical gesture over a strip scrolls the page
- **WHEN** I scroll straight up or down with the pointer over the currently-watching row or a profile poster strip
- **THEN** the page scrolls normally

#### Scenario: Hover treatment still fits
- **WHEN** I hover the first or last card of the currently-watching row, at either end of its scroll
- **THEN** the full hover treatment is drawn with no part of it clipped at the top or bottom of the row

### Requirement: Pages do not scroll or bounce past their vertical bounds
The system SHALL keep every page bounded vertically at both ends. Scrolling up when the page is already at the top SHALL NOT pull the page down: nothing SHALL ever be revealed above the navbar, which SHALL stay flush with the top of the window at scroll position zero. Scrolling down when the page is already at the bottom SHALL likewise leave the page where it is rather than bouncing it.

The page's ordinary vertical scrolling SHALL be unaffected — the navbar scrolls out of view with the rest of the content when the page is scrolled down, and scroll restoration on back/forward navigation continues to work as specified in the `page-state-restoration` capability.

#### Scenario: No empty space above the navbar
- **WHEN** I am at the top of any page and keep scrolling up with two fingers
- **THEN** the page does not move and no space appears above the navbar

#### Scenario: No bounce at the bottom
- **WHEN** I am at the bottom of a page long enough to scroll and keep scrolling down
- **THEN** the page stays where it is rather than bouncing past its last content

#### Scenario: Normal scrolling is unchanged
- **WHEN** I scroll down a page that is taller than the window and back up again
- **THEN** the content scrolls normally in both directions and comes to rest with the navbar flush at the top

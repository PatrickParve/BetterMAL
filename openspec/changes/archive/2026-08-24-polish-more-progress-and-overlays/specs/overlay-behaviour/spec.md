## ADDED Requirements

### Requirement: An open overlay holds the page behind it still

While any overlay is open, the system SHALL prevent the page behind it from scrolling — by wheel, trackpad, keyboard, or scrollbar — so the only thing that moves is the overlay the user is looking at.

This SHALL be a property of the app's overlay mechanism rather than of any one overlay, so it SHALL hold for every overlay the app opens on top of a page — the entry editor, the completion-score prompt, the ranking overlay, the recap picker, the edit-history overlay, the score board, the top-anime selection overlay, the unresolved-episodes overlay, and the related-anime overlay — and for any overlay added later, without that overlay having to ask for it.

The page behind SHALL keep the scroll position it had when the overlay opened, and SHALL be scrollable again from that same position once the overlay closes. Holding the page still SHALL NOT disturb the scroll position the app remembers for back/forward restoration: opening and closing an overlay SHALL leave a later return to that page landing exactly where it would have landed without the overlay.

The page's content SHALL NOT shift sideways when an overlay opens or closes: the width a hidden scrollbar would give back SHALL be held in reserve while the page is held still.

An overlay's own contents SHALL keep scrolling as they do today, so a tall overlay remains fully reachable.

Where one overlay opens on top of another, the page SHALL stay held until the last of them has closed.

#### Scenario: The page does not scroll under an overlay

- **WHEN** an overlay is open and I scroll with a wheel or trackpad over the page behind it
- **THEN** the page behind does not move

#### Scenario: Keys do not scroll the page either

- **WHEN** an overlay is open and I press Page Down, an arrow key, or the space bar
- **THEN** the page behind does not scroll

#### Scenario: The page keeps its position

- **WHEN** I open an overlay partway down a long page and then close it
- **THEN** the page is scrollable again and sits exactly where it was when the overlay opened

#### Scenario: Nothing shifts sideways

- **WHEN** an overlay opens over a page long enough to have a scrollbar
- **THEN** the page's content stays where it is horizontally, with no jump as the scrollbar disappears

#### Scenario: A tall overlay still scrolls

- **WHEN** an overlay's own content is taller than the space it has
- **THEN** that content scrolls within the overlay as it does today

#### Scenario: Stacked overlays

- **WHEN** a second overlay opens over the first and is then closed
- **THEN** the page behind is still held still until the remaining overlay is also closed

#### Scenario: Restoration is unaffected

- **WHEN** I open and close an overlay on a page scrolled partway down, navigate away, and come back
- **THEN** the page is restored to the position it held, exactly as it would have been had no overlay been opened

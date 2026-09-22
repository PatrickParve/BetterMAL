## ADDED Requirements

### Requirement: The navbar stays within reach while I scroll
The navbar SHALL stay attached to the top of the window as a page scrolls, rather than scrolling away with the page, and SHALL show or hide with the direction I am scrolling:

- Scrolling down SHALL slide it up out of view, once I am further from the top of the page than the navbar's own height. Scrolling up SHALL slide it back into view, from wherever I am on the page. A movement SHALL have to travel a short, deliberate distance in one direction before it changes the navbar, so a small reversal of direction does not make it flicker.
- Within the navbar's own height of the top of the page it SHALL always be shown.
- Showing and hiding SHALL be a smooth slide rather than an instant change. Where I have asked the system for reduced motion, it SHALL show and hide without animating.
- Hiding it SHALL NOT move the page's content. The space it takes at the top of the page SHALL be kept whether or not it is in view.
- While I am using it, it SHALL stay shown whatever the page does: while focus is inside it, and while its search dropdown or its updates dropdown is open. Moving keyboard focus into it while it is hidden SHALL show it.
- While shown over the page it SHALL be opaque, so the page's content does not show through it. It SHALL paint above the page's content, and every overlay the app opens over a page SHALL cover it as it covers the page.

Arriving at a page, by a link, a typed address or Back/Forward, SHALL show the navbar.

Scrolling the client performs itself SHALL NOT be read as my scrolling. Returning a restored page to its recorded position SHALL leave the navbar shown. A scroll the client performs to bring a piece of content to the top of the window SHALL hide the navbar, so the navbar never covers what was just brought there. Keyboard focus moving to an element, and the browser scrolling an element into view, SHALL NOT leave that element under the navbar.

#### Scenario: Scrolling down puts the navbar away
- **WHEN** I scroll down a long page past the navbar's own height
- **THEN** the navbar slides up out of view and the page's content does not shift

#### Scenario: Scrolling up brings it back from anywhere
- **WHEN** I am far down a long page with the navbar hidden and I scroll up a little
- **THEN** the navbar slides back into view at the top of the window, without my having to reach the top of the page

#### Scenario: Always there at the top
- **WHEN** I am at, or within the navbar's height of, the top of a page
- **THEN** the navbar is shown whichever direction I last scrolled

#### Scenario: A small wobble does not flicker it
- **WHEN** I scroll down and my scrolling reverses by only a pixel or two as it comes to rest
- **THEN** the navbar stays hidden rather than flashing back into view

#### Scenario: It stays while I am searching
- **WHEN** I have typed into the navbar's search field and the results dropdown is open, and I scroll the page down
- **THEN** the navbar and its dropdown stay in view

#### Scenario: It stays while the updates dropdown is open
- **WHEN** the updates dropdown is open and I scroll the page down
- **THEN** the navbar stays in view with the dropdown still anchored beneath its control

#### Scenario: Tabbing into a hidden navbar shows it
- **WHEN** the navbar is hidden and I move keyboard focus into it
- **THEN** it slides into view with the focused control visible

#### Scenario: Reduced motion
- **WHEN** my system asks for reduced motion and I scroll down and then up
- **THEN** the navbar disappears and reappears without sliding

#### Scenario: Arriving by Back shows it
- **WHEN** I scroll far down a list, open an anime, and go back
- **THEN** the list is restored to where I left it and the navbar is shown

#### Scenario: Content the page pins to the top is not covered
- **WHEN** a page scrolls one of its own sections to the top of the window, as a series page does when I open a group in its More section
- **THEN** that section's heading sits at the top of the window rather than under the navbar

#### Scenario: Focus is not hidden under it
- **WHEN** I move keyboard focus backwards to an element above the window's top edge and the page scrolls it into view
- **THEN** the focused element is visible below the navbar rather than under it

#### Scenario: An overlay covers it
- **WHEN** the navbar is shown and I open an overlay over the page
- **THEN** the overlay's backdrop covers the navbar just as it covers the page

### Requirement: The current page's navbar link returns me to the top
Clicking the navbar control for the page I am already on (the control carrying the current-page marking) while that page is scrolled down SHALL scroll the page smoothly back to its top and do nothing else. It SHALL NOT navigate, reload, show a loading state, add or replace a history entry, or change the address. It SHALL NOT reset any view control, page number, selected period or year, revealed amount of a list, or strip offset. Where I have asked the system for reduced motion, it SHALL go to the top without animating. Scrolling myself while the page is on its way to the top SHALL stop it where I am.

This SHALL apply to every navbar control that leads to a page: the eight left links, Profile and the Settings gear.

Clicking that same control when the page is already at its top SHALL take me to the page's default view, the same view clicking that control from any other page opens. Where the default view's address differs from the one I am on, this SHALL add a history entry, so Back returns me to the view I left. Where the address is the same, it SHALL replace the current entry rather than add a duplicate. Data the page already holds for the default view SHALL be shown at once, without a loading state. Data it does not hold SHALL load as it does on any fresh visit.

A click that arrives while the page is still on its way to the top, or a moment after it arrives there, because of an earlier click on that control SHALL only scroll, never reset. A double-click while scrolled down therefore never resets the page.

A click that asks the browser to open the link elsewhere (a new tab or window) SHALL keep its usual behaviour: the default view opens there, and the page I am on is left exactly as it was, neither scrolled nor reset.

The navbar's Season link SHALL address the current season directly, as the Year link addresses the current year, so opening Season from the navbar never passes through the page's replacement of an address that names no season.

#### Scenario: Back to the top of the year I am browsing
- **WHEN** I am browsing Year 2020 with a non-default sort, scrolled well down, and I click the navbar's Year link
- **THEN** the page scrolls smoothly to its top and still shows 2020 with the same sort, with no loading state and the address unchanged

#### Scenario: Nothing on the page is reset
- **WHEN** I have filtered My List and scrolled far enough that more rows were revealed, and I click the navbar's My List link
- **THEN** the page returns to its top with the same filters, search text and revealed rows it had

#### Scenario: A click at the top opens the default view
- **WHEN** I am at the top of Year 2020 and click the navbar's Year link
- **THEN** the year browser opens on the current year, and pressing Back returns me to Year 2020

#### Scenario: Already on the default view at the top
- **WHEN** I am at the top of the Season page showing the current season with its default controls, and I click the navbar's Season link
- **THEN** nothing visibly changes: no loading state, no reload, and Back does not step through a duplicate entry for the same view

#### Scenario: A double-click only scrolls
- **WHEN** I am scrolled down on Top anime page 3 and double-click the navbar's Top link
- **THEN** the page scrolls to its top and still shows page 3

#### Scenario: Profile and the Settings gear behave the same
- **WHEN** I am scrolled down on the Profile page or the Settings page and click Profile or the Settings gear respectively
- **THEN** that page scrolls to its top and nothing on it is reset

#### Scenario: Scrolling myself interrupts the trip to the top
- **WHEN** I click the current page's link while scrolled far down and scroll the page myself before it reaches the top
- **THEN** the scroll to the top stops and leaves me where I scrolled to

#### Scenario: Opening in a new tab leaves this page alone
- **WHEN** I am scrolled down on a page and open its own navbar link in a new tab
- **THEN** the new tab opens that page's default view, and this page neither scrolls nor changes

#### Scenario: Another page's link is unaffected
- **WHEN** I am scrolled down on Year 2020 and click the navbar's Season link
- **THEN** the Season page opens as a fresh visit at its top, on the current season

#### Scenario: Season opens without a reload
- **WHEN** I click the navbar's Season link from another page
- **THEN** the Season page opens directly on the current season, rather than appearing, disappearing and loading a second time

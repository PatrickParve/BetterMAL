# navigation-and-search Specification

## Purpose
The navigation-and-search capability governs the navbar, both search surfaces — the type-ahead dropdown and the full results page — and the page-wide rules every page follows: no horizontal scroll or drift, one-axis scrolling within a region, no vertical bounce, hover and focus treatment, and preferring an anime's English title everywhere it's shown. Search results include matching series alongside anime, fall back to locally stored anime when the live search fails, and can schedule a series build for a franchise not yet known to the app.
## Requirements
### Requirement: Navbar layout
The system SHALL provide a navbar with two groups of controls: a left group of page links and a right group holding the search field and the account/preference controls. There SHALL be no third, centred group — the search field belongs to the right group.

The left group's links SHALL be, in order: **Home, My List, Series, Recap, Top, Season, Year, Airing**. Series SHALL sit directly to the right of My List and Recap directly to the right of Series, since both are views over the same list — Series grouping it into franchises and Recap slicing it by period. Year SHALL sit directly to the right of Season, since the two browse the same listings at different grains.

The right group's controls SHALL be, in order from left to right: **the search field, the hide/unhide MAL-score toggle, the Updates control, Profile, and the Settings (gear icon) button** — so Settings sits at the navbar's far right edge, Profile immediately to its left, then Updates, then the score toggle, then the search field. Read right-to-left from the edge, the order is Settings, Profile, Updates, score toggle, search field.

The Updates control belongs to the right group rather than the left because it opens a menu over the current page instead of navigating to one, and because it carries per-user state as the other right-group controls do. It SHALL be an icon button of the same size **and resting appearance** as the Settings gear — plain, with no persistent background of its own — rather than reading as a separately-boxed control; it SHALL open and close the updates dropdown the `anime-updates` capability specifies, and its open state SHALL be exposed to assistive technology. Opening it SHALL NOT navigate anywhere or disturb the page behind it.

The Settings control SHALL report, without being opened, that something on the Settings page needs me and that long-running work is in flight: it carries the same indicator the Updates control carries, and a thin progress bar along its bottom edge, both on the terms `navbar-settings-status` sets out. Neither SHALL change its size, its position in the group, its hover treatment, or the fact that clicking it opens the Settings page; and both SHALL be reflected in its accessible name, which is otherwise "Settings" as before.

Every other control SHALL keep the behaviour, hover treatment, and accessible labelling it has today; only the ordering, the new Updates control, the Settings control's own status, and the search field's group membership change. The search field SHALL keep its own width within the right group rather than being squeezed to the width of a button, and its type-ahead dropdown SHALL stay anchored beneath the field in its new position.

At window widths too narrow for one row, the navbar MAY wrap the search field onto its own row, and SHALL keep the two groups' internal orderings when it does. The updates dropdown SHALL stay anchored beneath its own control and within the window at every width the app supports, rather than overflowing the window's right edge.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Series, Recap, Top, Season, Year, Airing, Profile, or Settings)

#### Scenario: Left group order
- **WHEN** the navbar renders
- **THEN** its left links read Home, My List, Series, Recap, Top, Season, Year, Airing from left to right, with Series directly right of My List, Recap directly right of Series, and Year directly right of Season

#### Scenario: Opening the series browser from the navbar
- **WHEN** I click the navbar's Series link
- **THEN** the Series page opens, listing the series built from my list

#### Scenario: The Series link marks itself current
- **WHEN** I am on the Series page
- **THEN** the navbar's Series link carries the current-page marking, and opening an individual series' page from it does not leave that marking on

#### Scenario: Opening the year browser from the navbar
- **WHEN** I click the navbar's Year link
- **THEN** the year browser opens on the current year

#### Scenario: Right group order
- **WHEN** the navbar renders
- **THEN** its right controls read search field, score toggle, Updates, Profile, Settings from left to right, with Settings at the far right edge

#### Scenario: Opening the updates menu
- **WHEN** I click the navbar's Updates control on any page
- **THEN** its dropdown opens beneath it over the current page, and I stay on that page

#### Scenario: The Updates control reads like its neighbours
- **WHEN** the Updates control is at rest, neither hovered nor open
- **THEN** it shows no background of its own, matching Profile and the Settings gear rather than standing out as a boxed control

#### Scenario: The updates dropdown stays within the window
- **WHEN** I open the updates dropdown at a narrow window width
- **THEN** it is anchored beneath its control and fully within the window rather than clipped at the window's right edge

#### Scenario: The search field is not centred
- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the search field sits in the right-hand group rather than centred between the two groups

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

#### Scenario: The gear reports without being opened
- **WHEN** changes are held for review and a job is running
- **THEN** the Settings gear carries the Updates control's indicator and a progress bar along its bottom edge, unchanged in size and position, and clicking it still opens the settings page

#### Scenario: The dropdown follows the field
- **WHEN** I type into the relocated search field
- **THEN** the type-ahead dropdown appears anchored beneath the field, fully within the window rather than clipped at its right edge

### Requirement: Clickable anime cards everywhere
The system SHALL make anime cards clickable everywhere they appear, linking to that anime's detail page. The clickable region SHALL cover the card's picture, title, and passive metadata. Interactive progress controls — the watched/total bar, the episode count, and the plus control — SHALL sit outside the card's link, so clicking or dragging within that region never navigates.

#### Scenario: Clicking any card
- **WHEN** I click an anime card's picture or title on any page
- **THEN** I am taken to that anime's detail page

#### Scenario: Clicking a card's progress controls
- **WHEN** I click the progress bar, episode count, or plus control on an anime card
- **THEN** I stay on the current page and only that control reacts

### Requirement: Hover highlight on anime cards and rows
The system SHALL give every anime card a clearly visible hover state — a change in surface and border, not colour alone — so the card under the pointer is unmistakable. This SHALL apply wherever anime cards appear (the main dashboard's currently-watching carousel and current-season section, the season browser, search results, and the Top anime page's rank 1–3 showcase cards and rank 4–10 card row) and to the equivalent full-width anime rows on my list, top anime, the series page's main-series and More sections, and the profile page's latest-updates feed and opinion-divergence lists. The highlight SHALL appear on the whole card or row as one unit, SHALL be reachable by keyboard focus as well as pointer hover, and SHALL NOT shift surrounding layout or clip against a scroll container's edge. The highlight SHALL remain clearly visible regardless of the card's own poster artwork — it SHALL NOT rely on a thin ring drawn over the picture itself, which can wash out against bright or visually busy cover art. This requirement does not apply to the poster-tile strips in the profile page's "My top anime" and "Most rewatched" boxes, which use their own distinct hover treatment (see the `profile-stats` capability).

#### Scenario: Hovering a card
- **WHEN** I move the pointer over an anime card on any page
- **THEN** the whole card is clearly highlighted and the highlight clears when the pointer leaves

#### Scenario: Hovering a top-anime card
- **WHEN** I move the pointer over one of the Top anime page's rank 1–3 showcase cards or rank 4–10 cards
- **THEN** the whole card is highlighted the same way anime cards are highlighted everywhere else

#### Scenario: Hovering a list row
- **WHEN** I move the pointer over a my-list, top-anime, series-page, or profile-page row
- **THEN** the whole row is clearly highlighted in the same way cards are

#### Scenario: Highlight does not move the layout
- **WHEN** a card or row is highlighted on hover
- **THEN** neighbouring cards and page content stay exactly where they were

#### Scenario: Highlight stays visible against busy poster art
- **WHEN** I hover an anime card whose poster art is bright or visually busy
- **THEN** the highlight is still clearly visible, since it does not depend on contrast against the poster image itself

### Requirement: Clearly visible hover state on navigation controls
The system SHALL give navigation controls a clearly visible hover state, consistent across the app: the navbar links, the score-visibility toggle, the settings link, the currently-watching carousel arrows, pagination controls, and the season and airing period arrows. The hover state SHALL be visible as more than a text-colour change, and SHALL leave the active/current state of a control still distinguishable while hovered.

#### Scenario: Hovering a navbar link
- **WHEN** I move the pointer over a navbar link
- **THEN** it is clearly highlighted rather than only changing text colour

#### Scenario: Hovering an arrow control
- **WHEN** I move the pointer over a carousel, pagination, season, or airing arrow
- **THEN** it is clearly highlighted in the same style as other navigation controls

#### Scenario: Active page stays distinguishable
- **WHEN** I hover the navbar link for the page I am already on
- **THEN** it shows the hover state while still reading as the active page

### Requirement: Every interactive control highlights on hover
The system SHALL give every enabled control it renders a visible hover state, on every page and inside every overlay. This covers buttons, links drawn as buttons, dropdowns, text and search fields, checkbox chips, and each option of a segmented switch. A control that does not visibly change when the pointer is over it SHALL be treated as a defect, whatever page it is on.

The same kind of control SHALL highlight the same way wherever it appears, so a control's hover tells me what kind of control it is and not which page I'm on:

- **An outlined control** is a control with a neutral fill and its own border: a button, a dropdown, a text field or a checkbox chip. On hover its border SHALL take the accent colour and its label SHALL take the heading colour. Its size and position SHALL stay the same.
- **A control already drawn as on** is one whose resting look already carries a tint and a coloured border: a filter that is narrowing the list, or a pressed toggle such as **Multi-entry only** or a status filter. Pressing it does something, so it SHALL still visibly respond under the pointer. Its outline SHALL thicken in its own colour (the accent colour, or the status colour it is drawn in) without changing its size. It SHALL NOT look identical hovered and at rest.
- **A segmented switch** SHALL take the accent border around the whole switch while the pointer is over it. The option under the pointer SHALL be picked out with a neutral tint, different from the accent tint that marks the chosen option, so hovering one option never makes it look chosen.
- **A filled action button**, one drawn in a solid colour as a call to action, SHALL darken its fill on hover. Its label SHALL stay legible in both themes.
- **The chosen option of a set where exactly one is chosen**, such as a tab row, a segmented switch or the current page number, MAY keep its selected look unchanged under the pointer, because choosing it again does nothing. It SHALL still read as chosen while hovered. This exemption does not cover a pressed toggle, since pressing a toggle again turns it off.

Navigation steppers and pagination keep the tinted highlight "Clearly visible hover state on navigation controls" already gives them, and the navbar search field keeps its own highlight from "The search field highlights on hover". Both are consistent within their own kind.

The dropdown rule SHALL be a rule of the app rather than of each component. It SHALL be written once, where it applies to every dropdown the app renders, so a dropdown added later carries it without anything being remembered about it. This is the same way the pointer cursor is expressed.

A hover state SHALL NOT change a control's size, position, or the layout around it.

#### Scenario: A dropdown on the season page
- **WHEN** I move the pointer over the Season page's season, year, or sort dropdown
- **THEN** its border takes the accent colour and its label the heading colour, the same as the Type filter beside it

#### Scenario: The same control on another page
- **WHEN** I hover the sort dropdown on the Series browser, the Home page's "Followed shows airing" sort, and My list's sort dropdown in turn
- **THEN** all three highlight in the same way

#### Scenario: The recap's period and type dropdowns
- **WHEN** I hover the Recap page's year or season dropdown, or its **All types** dropdown
- **THEN** each one highlights as an outlined control

#### Scenario: A checkbox chip
- **WHEN** I hover the **In my list** chip on the Season or Year page
- **THEN** its border takes the accent colour and its label the heading colour

#### Scenario: My list's find field
- **WHEN** I hover My list's find field
- **THEN** its border takes the accent colour, whether or not it holds text

#### Scenario: An active filter still responds
- **WHEN** My list's score filter is narrowing the list and I hover it
- **THEN** its outline thickens in the accent colour, so it visibly responds even though it was already accent-tinted

#### Scenario: A pressed toggle still responds
- **WHEN** **Multi-entry only** is pressed on the profile's "Top series" box or on the Series browser, and I hover it
- **THEN** its outline thickens, and it still reads as pressed

#### Scenario: A pressed status filter responds in its own colour
- **WHEN** a status filter on My list or the Series browser is pressed and I hover it
- **THEN** its outline thickens in that status's colour rather than switching to the accent colour

#### Scenario: The chosen tab
- **WHEN** I hover the media-type tab that is already chosen in "Most rewatched"
- **THEN** it still reads as the chosen tab

#### Scenario: The grouping switch
- **WHEN** **By status** is chosen and I hover **Single list**
- **THEN** the switch takes the accent border, **Single list** is picked out with a neutral tint, and **By status** still reads as the chosen option

#### Scenario: A filled action button
- **WHEN** I hover **Reset filters & sort**
- **THEN** its accent fill darkens and its label stays legible

#### Scenario: A Settings page action
- **WHEN** I hover **Resync now**, **Run full reconciliation**, or any other action button on the Settings page
- **THEN** it highlights in the same way as the accept and decline buttons on the same page

#### Scenario: A control inside an overlay
- **WHEN** I hover a Cancel or Close button in the rank editor, the completion-score overlay, or the related-anime overlay
- **THEN** it highlights as an outlined control

#### Scenario: Hover moves nothing
- **WHEN** any control is highlighted on hover
- **THEN** neither the control nor anything around it changes size or position

### Requirement: A disabled control shows no hover highlight
A control that is disabled SHALL NOT show any hover treatment. Moving the pointer over it SHALL leave its colour, background, border and size exactly as they are at rest, so it never offers itself as clickable when it isn't. This SHALL hold for every control the app can disable: the Airing page's week buttons, pagination arrows, period and season steppers, and the action buttons that disable themselves while a request is in flight.

A disabled control SHALL remain distinguishable from an enabled one without being hovered.

#### Scenario: The current week on the Airing page
- **WHEN** the Airing page is showing the current week and I hover its current-week button
- **THEN** the button stays dimmed and shows no highlight

#### Scenario: The first page of Top anime
- **WHEN** the Top anime page is on its first page and I hover the previous-page arrow
- **THEN** the arrow stays dimmed and shows no highlight

#### Scenario: The last page of Top anime
- **WHEN** the Top anime page is on its last page and I hover the next-page arrow
- **THEN** the arrow stays dimmed and shows no highlight

#### Scenario: A control that re-enables
- **WHEN** I step the Airing page back one week and hover the current-week button
- **THEN** it highlights like any other enabled control

### Requirement: A clickable control shows the pointer cursor
The system SHALL show the pointer cursor over **every** control it renders as clickable, wherever that control is: in the navbar, in a dropdown, inside an overlay, or on a page. This SHALL hold for a control built as a button as much as for one built as a link, and for an icon-only control as much as for one with a label — the bell opening the updates dropdown, the History control inside it, and the date fields filtering the updates history are all controls of this kind and SHALL show it.

This SHALL be a rule of the app rather than of each component: it SHALL be expressed once, where it applies to every control the app renders, so a control added later carries it without anything being remembered about it. A control that is **disabled** SHALL NOT show the pointer cursor, since it cannot be clicked — the dimmed prequel and sequel controls on the detail page are the case this excludes.

A field that both accepts typing and opens a picker — a date field — SHALL show the pointer cursor, since selecting a date, not typing one, is how it is used.

This requirement constrains the cursor alone. It SHALL NOT change any control's behaviour, position, size, hover treatment, focus ring, or accessible name.

#### Scenario: The updates bell
- **WHEN** I move the pointer over the navbar's updates bell
- **THEN** the cursor changes to the pointer, marking it as clickable

#### Scenario: The History control in the dropdown
- **WHEN** I move the pointer over the History control at the top of the updates dropdown
- **THEN** the cursor changes to the pointer

#### Scenario: The history's date fields
- **WHEN** I move the pointer over either date field in the updates history
- **THEN** the cursor changes to the pointer rather than staying a text cursor

#### Scenario: A disabled control does not offer itself
- **WHEN** I move the pointer over a disabled control, such as a dimmed prequel control on the detail page
- **THEN** the cursor does not change to the pointer

#### Scenario: A control added anywhere in the app
- **WHEN** any clickable control the app renders is hovered, whatever page or panel it sits in
- **THEN** it shows the pointer cursor, without that control's own styling having to ask for it

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

### Requirement: A truncated title reveals itself on hover
Wherever the system truncates an anime title for display — cut off at one line, at a fixed number of lines, or with an ellipsis — the full title SHALL be recoverable by pointing at it. Hovering the truncated title SHALL show the full title in a small box positioned below the pointer, which SHALL follow the pointer while it remains over the title and SHALL disappear when the pointer leaves.

The box SHALL NOT overlap the truncated title's own box, even when the title spans multiple lines and the pointer is near its top edge — the tooltip is meant to reveal text the title is hiding, not sit on top of text the title is already showing.

The tooltip SHALL appear only for titles that are actually truncated: a title that fits in full SHALL show no tooltip. Where this tooltip is used, the browser's own native title tooltip SHALL NOT also be shown, so the same text never appears twice.

The tooltip SHALL be presentation only: it SHALL NOT intercept pointer events, so it never blocks a click on the row or tile beneath it.

#### Scenario: Hovering a cut-off title
- **WHEN** I point at a title that has been cut off
- **THEN** a small box appears below the pointer showing the full title

#### Scenario: The tooltip follows the pointer
- **WHEN** I move the pointer across a truncated title
- **THEN** the box tracks the pointer, staying below it

#### Scenario: Tooltip clears a multi-line title
- **WHEN** I hover near the top of a title that is truncated across two lines
- **THEN** the tooltip appears below the whole title, not overlapping either of its lines

#### Scenario: Leaving the title
- **WHEN** I move the pointer off the title
- **THEN** the box disappears

#### Scenario: A title that fits
- **WHEN** I point at a title that is displayed in full
- **THEN** no tooltip appears

#### Scenario: No duplicate native tooltip
- **WHEN** I rest the pointer on a truncated title long enough for the browser's own tooltip to appear
- **THEN** only the app's tooltip is shown, not a second native one

#### Scenario: The tooltip does not block clicking
- **WHEN** the tooltip is visible beneath the pointer and I click
- **THEN** the click reaches the row or tile under the pointer

### Requirement: English title preferred for display
The system SHALL, wherever an anime title is displayed (Home, Season, Top, anime detail, Profile, My List, Airing, search results, and the updates dropdown and history), show the anime's English title when one is available, falling back to the default title otherwise.

This SHALL hold wherever an anime is **named**, not only where a title stands on its own: an anime named inside a composed line of text — such as an update's reason naming the entry it is affiliated with, or a control's hover tooltip naming the anime it leads to — SHALL name it by that same English-preferred title. An anime SHALL therefore never be named one way on a card and another way in a sentence, a tooltip, or a label beside it.

#### Scenario: English title available
- **WHEN** an anime has a stored English title and its title is displayed anywhere
- **THEN** the English title is shown

#### Scenario: No English title
- **WHEN** an anime has no English title
- **THEN** the default title is shown

#### Scenario: An anime named inside a line of text
- **WHEN** a line of text names an anime alongside something else — an update's reason naming the entry it is affiliated with, or a tooltip naming a control's target
- **THEN** that anime is named by its English title where one is known, exactly as its own card names it

### Requirement: Series appear in search results
The system SHALL match stored series against a search query alongside individual anime, in both the type-ahead dropdown and the full search results page.

A series SHALL match when ANY of its members' title or English title matches the query under the same rules applied to anime titles — starts-with, contains, or (for a double-quoted query) exact equality. A series that has been given a chosen title SHALL additionally match on that title under the same rules, so a trimmed title no member title begins with is still findable.

A matched series SHALL be presented using the identity the `series-identity` capability resolves — its chosen title and chosen picture where it has them, and its ROOT member's title and displayed picture otherwise, the same identity the series page itself uses — regardless of which member matched. A chosen title SHALL NOT narrow what the series matches on: every member title still matches.

Each matched series SHALL take the strongest match quality of any of its members (exact ahead of prefix, prefix ahead of contains) and, as a tie-break, the best popularity rank among its matching members. Matched series SHALL be listed AHEAD of anime results.

A series SHALL be matched only from stored series — searching SHALL NOT trigger a live MAL fetch or a synchronous series build.

#### Scenario: Searching a franchise name surfaces its series
- **WHEN** I search for "attack on titan" and that series is stored
- **THEN** the Attack on Titan series appears in the results, above the individual anime entries, showing the series' resolved title and picture

#### Scenario: Matching on a later entry's title
- **WHEN** I search for text that appears only in a non-root member's title (e.g. "final season")
- **THEN** the series still matches, and is shown under its resolved identity rather than the matching member's title and picture

#### Scenario: A renamed series still matches its members
- **WHEN** a series titled "Beyblade" has a member titled "Beyblade: Metal Fusion" and I search for "Metal Fusion"
- **THEN** the series matches and is listed as "Beyblade"

#### Scenario: A renamed series matches its own title
- **WHEN** a series has been given a chosen title and I search for it
- **THEN** the series matches on that title

#### Scenario: Exact-match query and series
- **WHEN** I wrap a query in double quotes
- **THEN** a series matches only when one of its members' title or English title, or its chosen title, exactly equals the quoted text

#### Scenario: Search never blocks on building a series
- **WHEN** I search for a franchise whose series has never been built
- **THEN** the results return at the usual speed with anime matches only, rather than waiting for a series to be built

### Requirement: A series result is marked as a series
The system SHALL mark every series result so it is never mistaken for a single anime. On the line BELOW the title, a series result SHALL show a "Series" badge together with the number of entries in the series, counting every member — main line and extras alike.

On the full results page this badge line SHALL occupy the same slot an anime card uses for its media-type and episode-count line, so series cards and anime cards keep identical geometry within the grid. The badge SHALL be scaled so that this line takes the same vertical space as an anime card's `TYPE · N ep` line: it SHALL NOT make a series card taller than the anime cards beside it, and SHALL NOT push the row of cards it sits in taller than a row of anime cards alone. The same scaled badge SHALL be used in the type-ahead dropdown row.

In the type-ahead dropdown, every row SHALL be the same height whether it is a series row or an anime row. A series row's title and badge line together SHALL fit within that shared height rather than making the row taller, so the dropdown's overall height depends only on how many rows it holds — a dropdown of five rows SHALL be the same height whether none, some, or two of those rows are series, and rows SHALL NOT shift as the matches change while typing.

The badge SHALL stay legible and keep reading as a badge at that size — a tinted, bordered pill naming "Series", with the entry count beside it — rather than being reduced to plain text.

#### Scenario: Badge on a dropdown result
- **WHEN** a series appears in the type-ahead dropdown
- **THEN** its row shows the series title with a "Series" badge and the entry count on the line beneath it

#### Scenario: A series row does not make the dropdown taller
- **WHEN** the dropdown lists both series rows and anime rows
- **THEN** every row is the same height, and the dropdown is no taller than one holding the same number of anime rows alone

#### Scenario: The dropdown does not jump while typing
- **WHEN** typing another character changes which of the five dropdown rows are series
- **THEN** the dropdown keeps the same height and its rows stay where they are

#### Scenario: Badge on a results-page card
- **WHEN** a series appears on the full search results page
- **THEN** its card shows the "Series" badge and entry count where an anime card shows its type and episode count, and the card is the same size and shape as the anime cards around it

#### Scenario: A series card does not add height to its row
- **WHEN** the results grid renders a row containing both a series card and anime cards
- **THEN** the series card's badge line is the same height as the anime cards' meta line, and the row is no taller than a row of anime cards alone

#### Scenario: The badge is still a badge
- **WHEN** I look at a series result at its reduced size, in the dropdown or on the results page
- **THEN** the "Series" pill is still legible and still visually distinct from surrounding text

#### Scenario: Entry count covers extras too
- **WHEN** a series has four main-line entries and three extras
- **THEN** its badge line reports seven entries

### Requirement: Search result text is not clipped by its own line box
No line of text in a search result SHALL be clipped by the line box it is laid out in. This covers every row of the type-ahead dropdown — a result's title and, on a series row, the "Series" badge line beneath it — and every card on the results page, including a series card's badge line.

Where a line's height is pinned to a fixed value (as the dropdown's rows are, to keep every row the same height), that value SHALL be at least the height of the glyphs the line renders, across the whole range of the app's fluid root font size, so that no ascender, descender, or bracket is shaved off along the line's top or bottom edge. A fixed row height SHALL likewise leave room for the lines it contains at that largest size, rather than squeezing them.

Sizing a line to its glyphs SHALL NOT be achieved by letting the dropdown's rows differ in height from one another, which the "A series result is marked as a series" requirement forbids: the row's own height SHALL absorb the difference so that a dropdown of five rows stays one height whichever of those rows are series.

#### Scenario: A dropdown title is drawn in full
- **WHEN** the type-ahead dropdown shows a result whose title contains tall or bracketed characters, at any window width
- **THEN** every character is drawn whole, with nothing cut off along the top or bottom of the line

#### Scenario: A series badge line is drawn in full
- **WHEN** a series result is shown, in the dropdown or as a results-page card
- **THEN** the "Series" pill's text and the entry-count text beside it are drawn whole, with nothing cut off along the top of the line

#### Scenario: Rows stay uniform after the fix
- **WHEN** the dropdown lists both series rows and anime rows
- **THEN** every row is still the same height and the dropdown does not change height as the matches change while typing

### Requirement: A series result opens the series page
The system SHALL navigate to the series page when a series result is activated, rather than to any individual anime's detail page.

#### Scenario: Opening a series from the dropdown
- **WHEN** I click a series row in the type-ahead dropdown
- **THEN** I am taken to that series' page

#### Scenario: Opening a series from the results page
- **WHEN** I click a series card on the search results page
- **THEN** I am taken to that series' page

### Requirement: Title matching ignores case, spacing, punctuation, and accents

Every comparison the search makes between a query and a title SHALL be made over a **normalized** form of both sides, so that a query which is right about the words finds the title whatever the user did with case, spacing, or punctuation.

Normalization SHALL: lower-case the text, fold accents and other diacritical marks to their base letters, and remove every character that is not a letter or a digit — spaces, hyphens, dashes, colons, slashes, full stops, apostrophes, quotation marks, exclamation and question marks, and any other punctuation or symbol. Letters of every script SHALL be kept, so a title in kana or kanji matches exactly as it does today.

This SHALL govern all three match kinds — **exact equality**, **starts-with**, and **contains** — and SHALL apply everywhere search matches a title: the type-ahead dropdown, the full results page, the local fallback used when the live search fails, and series matching (member titles, member English titles, and a series' chosen title alike). Anime and series SHALL use the one normalization rule, so the two can never disagree about whether a query matches.

Normalization SHALL widen only what counts as a match. Match quality ordering (exact ahead of prefix ahead of contains), popularity ordering within each band, the number of series and anime rows shown, and the background series-build trigger SHALL all be unchanged.

A query that contains no letters or digits at all — punctuation or symbols only — normalizes to nothing, and the system SHALL return no matches for it rather than treating it as matching every title.

The live MyAnimeList search request SHALL keep sending the query as the user typed it; normalization governs the system's own matching, not what a third party is asked. An anime that is neither stored locally nor returned by MyAnimeList for the query is therefore still not found.

#### Scenario: Spacing does not matter
- **WHEN** I search for "full metal" and *Fullmetal Alchemist* is stored
- **THEN** it appears in the type-ahead dropdown and on the results page

#### Scenario: Punctuation does not matter
- **WHEN** I search for "re zero" and *Re:ZERO -Starting Life in Another World-* is stored
- **THEN** it matches, and so does a search for "rezero" or "Re-Zero"

#### Scenario: A hyphenated title matches without its hyphen
- **WHEN** I search for "kaguya sama" and *Kaguya-sama: Love is War* is stored
- **THEN** it matches

#### Scenario: Accents do not matter
- **WHEN** I search for "kimi ni todoke" and a stored title spells a word with an accented letter
- **THEN** the accented and unaccented spellings match one another

#### Scenario: A series matches by the same rule
- **WHEN** a stored series has a member titled "Fullmetal Alchemist: Brotherhood" and I search for "full metal alchemist"
- **THEN** the series matches and is listed ahead of the anime results, as any matched series is

#### Scenario: A quoted query is exact about the words, not the punctuation
- **WHEN** I search for `"fullmetal alchemist"` in double quotes
- **THEN** *Fullmetal Alchemist* matches, and *Fullmetal Alchemist: Brotherhood* does not, because a quoted query still means the whole title rather than part of one

#### Scenario: Ranking is unchanged
- **WHEN** several titles match a query, some by prefix and some only in the middle
- **THEN** the prefix matches are still listed first, each band still ordered by popularity with unranked titles last

#### Scenario: A punctuation-only query matches nothing
- **WHEN** I search for "!!!" or "—"
- **THEN** no results are returned, rather than every title matching

#### Scenario: A Japanese-script title is unaffected
- **WHEN** I search using kana or kanji
- **THEN** the same titles match as before the normalization rule existed

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a debounced type-ahead search, presented in the navbar's right-hand control group (see "Navbar layout"), that on every query MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EQUALS the quoted text.

Starts-with, contains, and equality SHALL all be evaluated under the normalization the "Title matching ignores case, spacing, punctuation, and accents" requirement defines, so the ranking bands and the quoted-query filter are alike insensitive to case, spacing, punctuation, and accents.

The dropdown's 5 rows are shared with matched series (see "Series appear in search results"): matched series occupy the first rows, up to a maximum of 2, and anime matches fill the rest. When no series matches, all 5 rows are anime, exactly as before.

**The dropdown SHALL NOT wait on the live search to show anything.** Suggestions SHALL be delivered in two stages for every query:

- A **cache-only stage**, answered from the app's own stored anime and stored series with no live request in its path, presented as soon as it arrives. It SHALL be ranked, capped, and shaped exactly as the merged result is — the same prefix-then-contains ordering by popularity, the same 5-row budget, the same maximum of 2 series pinned first — so it is indistinguishable from a merged result apart from which anime it could draw on.
- The **merged stage** described above, which SHALL replace what the cache-only stage put on screen once it arrives.

The cache-only stage SHALL be debounced more eagerly than the merged stage, since it costs a local read rather than a live request. When both stages have answered for the same query, what is shown SHALL be the merged result. A merged result SHALL NOT replace what is on screen once the cache-only stage has moved on to a newer query.

The live search behind the merged stage SHALL request the same candidate set the full search results page requests for the same query, so that the two agree about which anime exist for a query and one live response can serve both (see `mal-api-integration`, "Live search responses are briefly cached and shared").

The cache-only stage's view of the app's stored anime MAY be served from an index held in memory rather than read from storage for each query, provided that index stays consistent with what is stored. That index SHALL reflect every saved change to a stored anime's title, English title, picture or popularity — an anime newly stored, or one of those fields rewritten on an anime already stored — from the next query after the change is saved. It SHALL NOT be allowed to go stale on a clock: only a saved change SHALL be able to make it out of date, never the passage of time, and a query SHALL NOT be answered from a partially built index. Nothing about the two stages, their two debounce rates, their ranking, their 5-row budget or their series rows SHALL differ according to whether a query was answered from such an index or from storage directly.

#### Scenario: Prefix matches ranked by popularity
- **WHEN** I type a query that is the start of several anime titles
- **THEN** the dropdown shows up to 5 matches, listing titles that start with the query first, ordered by popularity (e.g. typing "attack" surfaces the popular "Attack on Titan" entries, not a single incidental cached title)

#### Scenario: Contains-matches fill remaining slots
- **WHEN** fewer than 5 anime titles start with the query
- **THEN** anime whose title contains the query (but does not start with it) fill the remaining slots, also ordered by popularity

#### Scenario: A newly stored anime is matchable on the next query
- **WHEN** an anime the app had not stored before is stored by browsing a season, and I then type a query its title matches
- **THEN** the cache-only stage lists it, rather than leaving it out until some later change or restart

#### Scenario: A renamed or re-pictured anime is shown as stored
- **WHEN** a stored anime's title, English title, picture or popularity is rewritten by a refresh, an import or my own picture choice, and I then type a query that matches it
- **THEN** the cache-only stage shows the rewritten values and ranks it by the rewritten popularity, not by what it held before

#### Scenario: Uncached titles found via live search
- **WHEN** a matching anime is not in the local cache
- **THEN** it still appears because the live MAL search results are merged in (e.g. searching "paradise" surfaces "Hell's Paradise")

#### Scenario: Exact match with quotes
- **WHEN** I wrap the query in double quotes
- **THEN** only anime whose title or English title equals the quoted text — compared under the app's normalization rule — are shown

#### Scenario: Live-search failure is non-fatal
- **WHEN** the live MAL search fails (network blip or transient error)
- **THEN** the dropdown still shows any local-cache matches instead of erroring

#### Scenario: Series take the first rows of the dropdown
- **WHEN** my query matches one stored series and several anime
- **THEN** the dropdown shows the series first and fills the remaining rows with the top-ranked anime matches, 5 rows in total

#### Scenario: At most two series in the dropdown
- **WHEN** my query matches more than two stored series
- **THEN** only the two strongest-matching series are shown, leaving at least three rows for anime matches

#### Scenario: Stored matches appear before the live search returns
- **WHEN** I stop typing a query that several stored anime match
- **THEN** those matches are on screen well before the live MAL search for that query has returned, rather than the dropdown staying empty until it does

#### Scenario: Stored series appear in the first stage too
- **WHEN** my query matches a stored series
- **THEN** that series is pinned at the top of the first stage's rows, not added a second later when the live results arrive

#### Scenario: The merged ranking replaces the stored-only rows
- **WHEN** the live search for the query I have stopped typing returns
- **THEN** the dropdown's rows become the merged local + live ranking, which is what it settles on

#### Scenario: Nothing stored matches
- **WHEN** I stop typing a query no stored anime or series matches
- **THEN** the dropdown shows nothing until the merged stage arrives, and then shows the live matches

#### Scenario: A superseded live response is discarded
- **WHEN** a live response arrives for a query I have already typed past, and the first stage has already shown rows for the newer query
- **THEN** the newer rows stay on screen and the superseded response is discarded

### Requirement: Search returns NSFW-rated titles
The system SHALL include NSFW-rated anime (MAL ratings `r+` and `rx`) in every search result set — both the navbar type-ahead dropdown and the full search results page — by opting the live MAL search request into NSFW results, exactly as the season listing and user-animelist requests already do. A title that appears on the season page SHALL be findable by searching for it. No user setting SHALL suppress NSFW titles from search results.

#### Scenario: Searching for an NSFW-rated title
- **WHEN** I search for an anime MAL rates `r+` or `rx`
- **THEN** it appears in the type-ahead dropdown and on the search results page, rather than the search returning no match

#### Scenario: Season and search agree
- **WHEN** an NSFW-rated anime is listed on the season page
- **THEN** searching for that anime's title finds it

#### Scenario: Hide-NSFW setting does not apply to search
- **WHEN** the "Hide NSFW" setting is enabled and I search for a hentai title
- **THEN** it still appears in the search results, because the setting scopes to the season browser only

### Requirement: Full search results page
The system SHALL provide a dedicated search results page that lists the anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, episode count, and MAL score per card, laid out exactly as a season card lays them out, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). The MAL score SHALL follow the season card's rules in full, including showing nothing at all — no value, no placeholder, no reveal control — while the global hide-scores toggle is on. Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

The page SHALL work over a **bounded candidate set: the 60 highest-relevance anime the search finds for the query**. A query matching more than 60 anime SHALL show the 60 most relevant rather than all of them. The same 60 SHALL be what every other behaviour on the page is defined over — the sorts reorder those 60, the Type filter offers only the media types present among them and narrows within them, the count line counts them, and continuous scroll reveals them and then stops. The system SHALL NOT fetch more candidates than the page can display: the number requested from the live search, the number the endpoint will return, and the number the page asks for SHALL be one and the same figure.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

A series card SHALL NOT show a MAL score in that slot: its badge line already occupies the slot an anime card uses for its meta line, and a series has no single MAL score of its own.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

The system SHALL provide a multi-select Type filter on the search results page, using the same control and display labels as My List's type filter, offering only the media types actually present among the currently loaded candidate results. Selecting one or more types SHALL immediately narrow the displayed anime cards to matching types, applied over the already-loaded candidate set without issuing a new search request. The filter SHALL distinguish **All** from **None** as the `page-header-design` capability defines: on **All** — its state on a fresh visit — no type restriction applies; on **None**, no anime card passes the filter. **All**, **None**, and a partial selection SHALL each be distinctly representable in the page's URL state, with a URL naming no type filter meaning **All**. The result count line SHALL reflect the type-filtered anime count rather than the full unfiltered candidate count. Series cards SHALL be unaffected by the type filter and SHALL continue to be shown under the default relevance order regardless of which types are selected, since a series is not itself a single media-typed row.

When the type filter leaves no anime card to show but the query itself matched anime, the page SHALL say that no anime match the current filters rather than showing an empty grid in silence or reporting that nothing was found for the query. Any series cards the query matched SHALL still be listed alongside that message, since the type filter does not apply to them.

#### Scenario: Viewing full results for a query
- **WHEN** I submit a search
- **THEN** the search page shows all matching anime as cards with picture, title, type, episode count, and MAL score, in the API's relevance order by default

#### Scenario: A search card's score sits where a season card's does
- **WHEN** I compare a search results card with a season card for the same anime
- **THEN** both show the type and episode count at the start of the meta line and the MAL score at its end, laid out identically

#### Scenario: Hiding scores removes the search card score entirely
- **WHEN** the global hide-scores toggle is on and I view search results
- **THEN** no card shows a MAL score, a placeholder, or a reveal control

#### Scenario: A series card has no score slot
- **WHEN** a series card is shown among the results
- **THEN** its badge line occupies the meta line and no MAL score is shown on it

#### Scenario: Search grid matches the season grid's layout
- **WHEN** search results are displayed at any window width
- **THEN** the cards in each full row together span the content width the same way the season page's grid does, with no large empty space to the right of the grid

#### Scenario: Sorting results
- **WHEN** I choose a sort option on the search page
- **THEN** the results reorder by Relevance, Popularity, MAL score, Alphabetical, or My score as selected, starting again from the first chunk

#### Scenario: Scrolling loads more results
- **WHEN** I scroll to the end of the loaded search results and more matches exist
- **THEN** the next chunk is appended automatically below the ones already shown, without any page controls and without replacing what is already on screen

#### Scenario: No pagination controls
- **WHEN** a query returns more matches than fit in one chunk
- **THEN** no page numbers, next/previous buttons, or other pagination controls are shown

#### Scenario: Sort order holds across loaded chunks
- **WHEN** I sort by any option and scroll far enough to load several chunks
- **THEN** the results remain in one continuous sorted order across the chunk boundaries, rather than each chunk being sorted on its own

#### Scenario: End of results
- **WHEN** every match for the query has been loaded
- **THEN** scrolling further loads nothing more and no error or empty-state message replaces the results

#### Scenario: Exact match on the results page
- **WHEN** I submit a double-quoted query
- **THEN** the results page lists only anime whose title or English title exactly equals the quoted text

#### Scenario: View restored on back-navigation
- **WHEN** I open an anime from the results and navigate back
- **THEN** the same query and sort are restored from the URL

#### Scenario: Series lead the relevance ordering
- **WHEN** I submit a query matching a stored series and view the results in the default order
- **THEN** the series cards appear first, before the anime cards, with at most three shown

#### Scenario: Series are omitted under other sorts
- **WHEN** I switch the results page to Popularity, MAL score, Alphabetical, or My score
- **THEN** only anime cards are listed, with no series among them

#### Scenario: The result count reports anime only
- **WHEN** a query matches one series and twenty anime
- **THEN** the count line reads twenty results, and the series card is shown in addition to those twenty

#### Scenario: Filtering results by type
- **WHEN** I select Movie in the search page's type filter
- **THEN** only movie cards remain visible among the loaded results, and the result count line reflects only the movie count

#### Scenario: Only present types are offered
- **WHEN** the loaded search results contain no music videos
- **THEN** the type filter does not offer Music as an option

#### Scenario: All applies no type restriction
- **WHEN** the search page's type filter is on All
- **THEN** anime cards of every loaded type are shown

#### Scenario: None empties the grid and says so
- **WHEN** I press **None** in the search page's type filter on a query that matched anime
- **THEN** no anime cards are shown and the page says no anime match the current filters, rather than saying no anime were found

#### Scenario: Series survive None
- **WHEN** the type filter is on **None** and the query matched a stored series
- **THEN** that series card is still listed

#### Scenario: A query with more matches than the page holds
- **WHEN** I submit a query that matches more anime than the page's candidate set holds
- **THEN** the 60 most relevant are listed, the count line reads 60, and scrolling past them loads nothing further

#### Scenario: Nothing is fetched that cannot be shown
- **WHEN** the page runs a search for a query
- **THEN** the live search is asked for exactly the number of candidates the page can display, rather than a larger set that is ranked and then partly discarded

#### Scenario: The type filter works within the candidate set
- **WHEN** I filter by a media type on a query whose candidate set is full
- **THEN** the types offered and the cards shown are drawn from those candidates, and the count line reflects the filtered count among them

### Requirement: Searching schedules a series build for an unknown franchise
When a search's top-ranked anime match belongs to no stored series, the system SHALL schedule that anime's series to be built in the background, so the franchise becomes searchable on a later search without the user having to open its series page.

Scheduling SHALL NOT affect the response: it SHALL NOT delay the search, SHALL NOT add a MAL request to the search request path, and a scheduling or build failure SHALL leave the search results unchanged.

The system SHALL schedule at most one build per search — the top-ranked anime match only — SHALL skip scheduling for queries shorter than three characters, and SHALL NOT re-schedule an anime it has already scheduled since the app started, so a debounced type-ahead does not queue a build per keystroke.

"Per search" counts a query, not a request. Where a query is answered in two stages (see "Type-ahead search, merged local + live ranked by prefix and popularity"), only the merged stage SHALL schedule: the cache-only stage SHALL schedule nothing, since its top-ranked match is drawn from stored anime alone and is not necessarily the query's top-ranked match.

#### Scenario: An unknown franchise becomes searchable later
- **WHEN** I search for a franchise whose series has never been built, and later search for it again
- **THEN** the series has been built in the background in the meantime and now appears in the results

#### Scenario: Scheduling never slows the search
- **WHEN** a search schedules a background build
- **THEN** the results return exactly as fast as they would have otherwise, and contain the same anime matches

#### Scenario: A failed background build is invisible
- **WHEN** a scheduled build fails
- **THEN** the search that scheduled it is unaffected and no error is shown

#### Scenario: Typing does not queue a build per keystroke
- **WHEN** I type a franchise name one character at a time into the type-ahead
- **THEN** each candidate anime is scheduled at most once, not once per keystroke

#### Scenario: Very short queries schedule nothing
- **WHEN** my query is one or two characters long
- **THEN** no background build is scheduled

#### Scenario: The cache-only stage schedules nothing
- **WHEN** the type-ahead's cache-only stage answers a query
- **THEN** it schedules no build, and the query still schedules at most one — from its merged stage

### Requirement: Search submission keeps the query text
The system SHALL let the user submit the current search either by pressing Enter in the search box or by clicking a magnifier button beside it, navigating to the search results page for that query. The submitted text SHALL remain in the search box (and SHALL be restored from the URL when the search page is loaded directly or reloaded) rather than being cleared.

Submitting SHALL dismiss the type-ahead dropdown and leave the search field unfocused, so the results page is shown with nothing over it and no cursor left in the field.

**Only a user interaction with the search field SHALL open the dropdown** — typing in it, or focusing it. Nothing else SHALL: not a search response arriving, not a navigation, and not the search page restoring the submitted query into the field from the URL. This SHALL hold whatever was in flight and whatever the type-ahead's debounce was holding at the moment of submission, so pressing Enter before suggestions for the typed query have been requested at all is no different from pressing it after they have been shown.

While the dropdown is dismissed, the type-ahead SHALL issue no search requests, and SHALL abandon any it has outstanding — so a submitted search does not compete with the results page's own search for the same query.

Dismissal SHALL NOT be sticky: typing anything after a submission SHALL show suggestions again as it does today, and returning focus to the field SHALL show suggestions for whatever the field currently contains — searching for them if they are not already in hand, and never showing suggestions fetched for a different query than the one now in the field.

#### Scenario: Submit via Enter
- **WHEN** I press Enter with text in the search box
- **THEN** I am taken to the search results page for that query and the text stays in the box

#### Scenario: Submit via the magnifier button
- **WHEN** I click the magnifier button beside the search box
- **THEN** I am taken to the search results page for the current query

#### Scenario: The dropdown does not follow me to the results
- **WHEN** I press Enter while the type-ahead dropdown is showing suggestions
- **THEN** the results page is shown with no dropdown over it

#### Scenario: The field gives up focus
- **WHEN** I submit a search
- **THEN** the search field is no longer focused and no cursor is left in it

#### Scenario: A late response does not reopen the dropdown
- **WHEN** I press Enter before the suggestions for that query have arrived, and the response arrives once the results page is showing
- **THEN** no dropdown opens over the results

#### Scenario: Submitting before the debounce has even fired
- **WHEN** I type a query and press Enter faster than the type-ahead's debounce, so suggestions for the typed query are requested only after I have left for the results page
- **THEN** no dropdown opens over the results, and none appears when the search page writes the query back into the field

#### Scenario: A dismissed dropdown stops searching
- **WHEN** I submit a search while a type-ahead request for that query is in flight
- **THEN** that request is abandoned rather than left to compete with the results page's own search

#### Scenario: Typing again brings suggestions back
- **WHEN** I submit a search and then type another character in the field
- **THEN** the dropdown opens again with suggestions for the new query

#### Scenario: Refocusing brings suggestions back
- **WHEN** I submit a search and then click back into the search field without typing
- **THEN** the dropdown opens with suggestions for the query in the field

#### Scenario: Refocusing never shows another query's suggestions
- **WHEN** the text in the field changed while the dropdown was dismissed and I then focus the field
- **THEN** the dropdown shows suggestions for the text now in the field, never the ones fetched for what it held before

#### Scenario: Query restored on the search page
- **WHEN** I load or reload the search page for a query (e.g. via a direct `/search?q=…` link)
- **THEN** the search box shows that query

### Requirement: The search field is one control with a self-contained focus ring
The system SHALL present the navbar search input and its magnifier button as a single control: one bordered field, with the magnifier sitting inside that field's right edge rather than as a separate box joined to it by a seam.

Focus SHALL be indicated on the field as a whole, and the indication SHALL stay within the field's own bounds — it SHALL NOT be drawn outside the input and SHALL NOT overlap, cross, or sit on top of the magnifier. This SHALL hold whichever part of the field has focus: typing in the input and tabbing to the magnifier SHALL each show focus without any ring landing on the other part.

The field SHALL keep its current behaviour and affordances: the placeholder, the type-ahead dropdown anchored beneath it, submission by Enter or by clicking the magnifier, and a visible hover state on the magnifier. At every font size the app renders (the root size is fluid), the ring SHALL remain inside the field.

#### Scenario: Focusing the input
- **WHEN** I click or tab into the search input
- **THEN** the whole field shows it has focus and no part of the focus indication touches or covers the magnifier

#### Scenario: Focusing the magnifier
- **WHEN** I tab from the input to the magnifier button
- **THEN** the magnifier shows it has focus without a ring spilling over the input beside it

#### Scenario: The field reads as one control
- **WHEN** the navbar renders
- **THEN** the input and the magnifier appear as a single bordered field rather than two boxes with a seam between them

#### Scenario: Submitting still works from the field
- **WHEN** I press Enter in the input, or click the magnifier
- **THEN** I am taken to the search results page for the current query, exactly as before

### Requirement: The search field highlights on hover
The system SHALL highlight the navbar search field while the pointer is over it, using the same highlight the field shows when it has focus, so the field responds to the pointer as every other navigation control in the app does.

The highlight SHALL stay within the field's own bounds, exactly as the focus indication does — it SHALL NOT be drawn outside the field, SHALL NOT overlap or sit on top of the magnifier, and SHALL NOT change the field's size or position, so hovering the navbar never reflows it. Hovering a field that already has focus SHALL look the same as focusing it, rather than compounding the two into a heavier treatment. The magnifier SHALL keep its own hover state within the highlighted field.

#### Scenario: Hovering the field
- **WHEN** I move the pointer over the navbar search field
- **THEN** the field shows the same highlight it shows when focused

#### Scenario: The highlight stays inside the field
- **WHEN** the field is highlighted by hover
- **THEN** no part of the highlight touches or covers the magnifier, and the field neither grows nor moves

#### Scenario: Hovering a focused field
- **WHEN** I move the pointer over the field while typing in it
- **THEN** it looks as it does when focused, without a second, heavier ring

#### Scenario: The magnifier still responds
- **WHEN** I move the pointer from the field onto the magnifier inside it
- **THEN** the magnifier shows its own hover state as before, within the highlighted field

### Requirement: The search field's clear control reads as clickable
The system SHALL present the clear ("×") control inside a search field as an interactive control: hovering it SHALL show the pointer cursor, the same cursor every other clickable control in the app shows, rather than the default text or arrow cursor. This SHALL hold for every search field the app renders — the navbar search bar and the Settings page's refresh picker — and SHALL NOT change the control's behaviour, position, or visibility, nor the field's own focus indication.

Where the browser renders no clear control of its own, this requirement is satisfied vacuously; it constrains the cursor over the control, not whether the control exists.

#### Scenario: Hovering the clear control
- **WHEN** I have typed into the navbar search field and move the pointer over its clear ("×") control
- **THEN** the cursor changes to the pointer, marking it as clickable

#### Scenario: Clearing still works
- **WHEN** I click that clear control
- **THEN** the field is cleared exactly as before, with no change to the dropdown, the focus ring, or the magnifier beside it

#### Scenario: Every search field behaves the same
- **WHEN** I use the Settings page's anime-refresh search field
- **THEN** its clear control shows the same pointer cursor as the navbar's

### Requirement: Navbar page controls and the search field share one height

The navbar's page controls — the seven left links (Home, My List, Recap, Top, Season, Year, Airing), the Profile link, and the Settings gear — together with the navbar's search field SHALL all render at exactly the same height, with their tops and bottoms aligned.

The shared height SHALL come from one shared definition rather than from per-control padding values that each happen to land near the same number, so a control added to the navbar later inherits the row's height instead of re-introducing a mismatch. The height SHALL hold across the full range of the app's fluid root font size, rather than agreeing only at one window width.

The hide/unhide score toggle is explicitly NOT part of this requirement. It is its own control family and SHALL keep its current shape, height, and behaviour unchanged.

The shared height SHALL NOT clip any control's contents at any supported window width, and SHALL NOT change any control's behaviour, its accessible name, its keyboard handling, its hover treatment, its position in the navbar's ordering, or where the search field's type-ahead dropdown is anchored.

The search field the Settings page renders shares its component with the navbar's field but is not in the navbar; its size SHALL be unaffected.

#### Scenario: The navbar row is flush

- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the seven left links, Profile, the Settings gear, and the search field are all exactly the same height, with their tops and bottoms aligned

#### Scenario: The gear is no longer a small square

- **WHEN** I compare the Settings gear with the Profile link beside it
- **THEN** the two boxes are the same height

#### Scenario: Height holds as the font scales

- **WHEN** the window width changes enough to move the app's fluid root font size
- **THEN** those controls still match one another's height

#### Scenario: Nothing is clipped

- **WHEN** the navbar renders at the widest window width the app supports, where its root font size is largest
- **THEN** every link's text and the search field's text and placeholder are drawn whole, with nothing cut off along the top or bottom

#### Scenario: The score toggle is untouched

- **WHEN** the navbar renders
- **THEN** the hide/unhide score toggle keeps the pill shape, height, knob travel, and eye animation it had before, whether or not that height matches its neighbours

#### Scenario: The Settings page's search field is unaffected

- **WHEN** I open the Settings page and look at its anime-refresh search field
- **THEN** it is the size it was before, unchanged by the navbar's shared height

#### Scenario: Everything still works

- **WHEN** I click a navbar link, click the gear, type in the search field, or open its type-ahead dropdown
- **THEN** each behaves exactly as it did, with the dropdown anchored beneath the field as before

### Requirement: The navbar marks the current page with a border

The navbar control for the page currently being viewed SHALL carry a visible border in addition to its tinted background, so that being on a page reads at least as strongly as hovering a link does. This SHALL apply to every navbar control that leads to a page: the seven left links, Profile, and the Settings gear.

The active border SHALL be visually stronger than the border a control shows on hover, so that an active control under the pointer still reads as the current page rather than as just another hovered link. Hovering the active control SHALL NOT replace or weaken its active border.

The Settings gear SHALL be marked this way when the settings page is being viewed, like every other navbar page control.

The updates control opens a panel rather than a page, and SHALL be marked with this same treatment while what it opened is on screen, on the terms the `anime-updates` capability sets out. It is therefore not an exception to the rule that a navbar control shows where you are; it answers to its panel rather than to the route.

The score toggle is not a page control, opens nothing, and SHALL remain the one navbar control that is never marked.

#### Scenario: The current page's link is bordered

- **WHEN** I am on My List and look at the navbar
- **THEN** the My List link shows a border as well as its tinted background

#### Scenario: The gear is marked on the settings page

- **WHEN** I am on the settings page
- **THEN** the Settings gear shows the same active treatment the other navbar page controls show for their pages

#### Scenario: Active outranks hover

- **WHEN** I hover a navbar link for a page I am not on, and then hover the link for the page I am on
- **THEN** the active link's border is visibly stronger than the hovered one's, so which link is the current page is still clear

#### Scenario: Hovering the active control keeps it marked

- **WHEN** I move the pointer over the navbar control for the page I am already on
- **THEN** it shows the hover state while keeping its stronger active border

#### Scenario: The updates control is marked by its panel

- **WHEN** the updates dropdown is open
- **THEN** the updates control carries the same marking a navbar page control carries for its page, without any page having changed

#### Scenario: The score toggle gains nothing

- **WHEN** the navbar renders on any page
- **THEN** the hide/unhide score toggle shows no active-page treatment, whatever else in the navbar is marked

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

### Requirement: Search results fall back to locally stored anime when the live search fails
When the live MAL search cannot be reached or fails for a query on the full search results page, the system SHALL search the anime it has stored locally instead of returning nothing, and SHALL present those matches as the page's results.

The locally stored set SHALL be every anime the app holds in its own storage, however it came to be there — anime in my list, anime cached from a season or a top-anime listing, and anime cached from any earlier search or detail view alike. Membership of my list SHALL NOT be a condition.

Matching SHALL follow the same rules the search already uses: a query SHALL match an anime whose title or English title contains it, and a double-quoted query SHALL match only an anime whose title or English title equals the quoted text — both evaluated under the normalization the "Title matching ignores case, spacing, punctuation, and accents" requirement defines, so the fallback is insensitive to case, spacing, punctuation, and accents exactly as the live path is.

Fallback results SHALL be presented exactly as live results are: the same cards, with picture, title, media type, episode count, and MAL score; the same continuous scroll; the same Type filter, offering only the media types present among the fallback results; and the same result count line.

Under the default relevance order, fallback results SHALL be ranked with exact title matches first, then anime whose title or English title begins with the query, then the remaining matches, each of those groups ordered by MAL popularity with unranked anime last and then by title, case-insensitively. The Popularity, MAL score, Alphabetical, and My score sorts SHALL each order the fallback results by the same rule they order live results by.

Matched series SHALL continue to be shown under the default relevance order, to the same maximum of three, since they are drawn from the app's own stored series rather than from the live search.

The page SHALL state that the fallback is in effect: it SHALL show a message saying the MAL search could not be reached and that the results shown are the app's locally stored matches. The message SHALL be shown whenever the fallback answered the query, including when the fallback itself found nothing, so an empty result reads as "nothing stored here matches" rather than as "this does not exist".

When the live MAL search succeeds, nothing SHALL change: the results, their ordering, the series shown, the count, and the absence of any such message SHALL all be exactly as they are today. A live search that succeeds and legitimately returns no matches SHALL show the ordinary empty state, with no fallback message.

#### Scenario: The live search is unreachable
- **WHEN** I submit a query and the live MAL search fails
- **THEN** the page lists the anime stored locally whose title or English title contains the query, presented as ordinary result cards

#### Scenario: The fallback matches loosely too
- **WHEN** the live search fails and I submit "full metal"
- **THEN** a locally stored *Fullmetal Alchemist* is among the results

#### Scenario: The fallback says why
- **WHEN** the fallback has answered my query
- **THEN** the page shows a message saying the MAL search could not be reached and that these are the locally stored matches

#### Scenario: An empty fallback still explains itself
- **WHEN** the live search fails and nothing stored locally matches my query
- **THEN** the page still shows the message saying the MAL search could not be reached, rather than only an ordinary "no results" state

#### Scenario: Anime outside my list are included
- **WHEN** the fallback answers a query that matches an anime the app cached from a season listing but which is not in my list
- **THEN** that anime is among the results

#### Scenario: Fallback relevance ordering
- **WHEN** the fallback answers a query that one stored anime matches exactly, several match by prefix, and several more match only in the middle of their titles
- **THEN** the exact match is listed first, the prefix matches next, and the remaining matches after them, each group ordered by popularity

#### Scenario: Sorting fallback results
- **WHEN** the fallback has answered my query and I choose the MAL score sort
- **THEN** the local matches are reordered by MAL score, exactly as live results would be

#### Scenario: Filtering fallback results by type
- **WHEN** the fallback has answered my query and I select Movie in the type filter
- **THEN** only movie cards remain, and the count line reflects only the movie count

#### Scenario: An exact-match query against local storage
- **WHEN** the live search fails and I submit a double-quoted query
- **THEN** only stored anime whose title or English title equals the quoted text under the app's normalization rule are listed

#### Scenario: Series still lead the fallback
- **WHEN** the fallback answers a query that matches a stored series, under the default order
- **THEN** the matched series cards are shown first, before the anime cards, as they are for a live search

#### Scenario: A working search is untouched
- **WHEN** I submit a query and the live MAL search succeeds
- **THEN** the results are exactly the live search's results in their usual order, and no fallback message is shown

#### Scenario: A working search with no matches
- **WHEN** the live MAL search succeeds and genuinely returns no matches for my query
- **THEN** the page shows its ordinary empty state and does not claim the MAL search failed


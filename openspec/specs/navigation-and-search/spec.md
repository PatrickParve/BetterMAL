# navigation-and-search Specification

## Purpose
TBD - created by archiving change bootstrap-anime-tracker. Update Purpose after archive.
## Requirements
### Requirement: Navbar layout
The system SHALL provide a navbar with two groups of controls: a left group of page links and a right group holding the search field and the account/preference controls. There SHALL be no third, centred group — the search field belongs to the right group.

The left group's links SHALL be, in order: **Home, My List, Recap, Top, Season, Airing**. Recap SHALL sit directly to the right of My List, since a recap is a view of the same list.

The right group's controls SHALL be, in order from left to right: **the search field, the hide/unhide MAL-score toggle, Profile, and the Settings (gear icon) button** — so Settings sits at the navbar's far right edge, Profile immediately to its left, then the score toggle, then the search field. Read right-to-left from the edge, the order is Settings, Profile, score toggle, search field.

Every control SHALL keep the behaviour, hover treatment, and accessible labelling it has today; only the ordering and the search field's group membership change. The search field SHALL keep its own width within the right group rather than being squeezed to the width of a button, and its type-ahead dropdown SHALL stay anchored beneath the field in its new position.

At window widths too narrow for one row, the navbar MAY wrap the search field onto its own row, and SHALL keep the two groups' internal orderings when it does.

#### Scenario: Navigating via the navbar
- **WHEN** I click a navbar button
- **THEN** I am taken to the corresponding page (Home, My List, Recap, Top, Season, Airing, Profile, or Settings)

#### Scenario: Left group order
- **WHEN** the navbar renders
- **THEN** its left links read Home, My List, Recap, Top, Season, Airing from left to right, with Recap directly right of My List

#### Scenario: Right group order
- **WHEN** the navbar renders
- **THEN** its right controls read search field, score toggle, Profile, Settings from left to right, with Settings at the far right edge

#### Scenario: The search field is not centred
- **WHEN** the navbar renders at a width wide enough for one row
- **THEN** the search field sits in the right-hand group rather than centred between the two groups

#### Scenario: My List is reachable from the navbar
- **WHEN** the navbar renders
- **THEN** a "My List" button appears in the left button group and navigates to the my-list page

#### Scenario: Opening settings from the gear
- **WHEN** I click the Settings gear icon
- **THEN** I am taken to the settings page

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
The system SHALL, wherever an anime title is displayed (Home, Season, Top, anime detail, Profile, My List, Airing, and search results), show the anime's English title when one is available, falling back to the default title otherwise.

#### Scenario: English title available
- **WHEN** an anime has a stored English title and its title is displayed anywhere
- **THEN** the English title is shown

#### Scenario: No English title
- **WHEN** an anime has no English title
- **THEN** the default title is shown

### Requirement: Series appear in search results
The system SHALL match stored series against a search query alongside individual anime, in both the type-ahead dropdown and the full search results page.

A series SHALL match when ANY of its members' title or English title matches the query under the same rules applied to anime titles — starts-with, contains, or (for a double-quoted query) exact equality. A matched series SHALL be presented using its ROOT member's title and picture, the same identity the series page itself uses, regardless of which member matched.

Each matched series SHALL take the strongest match quality of any of its members (exact ahead of prefix, prefix ahead of contains) and, as a tie-break, the best popularity rank among its matching members. Matched series SHALL be listed AHEAD of anime results.

A series SHALL be matched only from stored series — searching SHALL NOT trigger a live MAL fetch or a synchronous series build.

#### Scenario: Searching a franchise name surfaces its series
- **WHEN** I search for "attack on titan" and that series is stored
- **THEN** the Attack on Titan series appears in the results, above the individual anime entries, showing the root entry's title and picture

#### Scenario: Matching on a later entry's title
- **WHEN** I search for text that appears only in a non-root member's title (e.g. "final season")
- **THEN** the series still matches, and is shown under the root entry's title and picture rather than the matching member's

#### Scenario: Exact-match query and series
- **WHEN** I wrap a query in double quotes
- **THEN** a series matches only when one of its members' title or English title exactly equals the quoted text

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

### Requirement: Type-ahead search, merged local + live ranked by prefix and popularity
The system SHALL provide a debounced type-ahead search, presented in the navbar's right-hand control group (see "Navbar layout"), that on every query MERGES matches from the local cache with a live MAL search (deduplicated by anime id) rather than short-circuiting on cached matches. It SHALL rank titles whose title or English title STARTS WITH the query ahead of titles that merely CONTAIN the query, order each group by popularity — most popular first, with unranked titles (MAL popularity rank absent or zero) last — and show a dropdown of up to 5 matches with picture and title. A live-search failure SHALL degrade to local matches only rather than erroring. When the query is wrapped in double quotes (`"…"`), the system SHALL instead return only anime whose title or English title EXACTLY equals the quoted text.

The dropdown's 5 rows are shared with matched series (see "Series appear in search results"): matched series occupy the first rows, up to a maximum of 2, and anime matches fill the rest. When no series matches, all 5 rows are anime, exactly as before.

#### Scenario: Prefix matches ranked by popularity
- **WHEN** I type a query that is the start of several anime titles
- **THEN** the dropdown shows up to 5 matches, listing titles that start with the query first, ordered by popularity (e.g. typing "attack" surfaces the popular "Attack on Titan" entries, not a single incidental cached title)

#### Scenario: Contains-matches fill remaining slots
- **WHEN** fewer than 5 anime titles start with the query
- **THEN** anime whose title contains the query (but does not start with it) fill the remaining slots, also ordered by popularity

#### Scenario: Uncached titles found via live search
- **WHEN** a matching anime is not in the local cache
- **THEN** it still appears because the live MAL search results are merged in (e.g. searching "paradise" surfaces "Hell's Paradise")

#### Scenario: Exact match with quotes
- **WHEN** I wrap the query in double quotes
- **THEN** only anime whose title or English title exactly equals the quoted text are shown

#### Scenario: Live-search failure is non-fatal
- **WHEN** the live MAL search fails (network blip or transient error)
- **THEN** the dropdown still shows any local-cache matches instead of erroring

#### Scenario: Series take the first rows of the dropdown
- **WHEN** my query matches one stored series and several anime
- **THEN** the dropdown shows the series first and fills the remaining rows with the top-ranked anime matches, 5 rows in total

#### Scenario: At most two series in the dropdown
- **WHEN** my query matches more than two stored series
- **THEN** only the two strongest-matching series are shown, leaving at least three rows for anime matches

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
The system SHALL provide a dedicated search results page that lists every anime matching a submitted query, presented the same way as the seasonal page (picture, title, media type, episode count, and MAL score per card, laid out exactly as a season card lays them out, using the same content-width-filling grid layout and the same fixed per-row card count at ordinary desktop widths). The MAL score SHALL follow the season card's rules in full, including showing nothing at all — no value, no placeholder, no reveal control — while the global hide-scores toggle is on. Results SHALL default to the order returned by the search API (closest match first) and SHALL additionally be sortable by Popularity, MAL score, Alphabetical, and My score; the Popularity sort SHALL place unranked anime (MAL popularity rank absent or zero) last. A double-quoted query SHALL constrain results to exact title matches. The query and sort SHALL be held in the URL so back-navigation restores the same view.

Under the default relevance order, matched series (see "Series appear in search results") SHALL be shown FIRST, ahead of the anime cards, to a maximum of 3. Under any other sort — Popularity, MAL score, Alphabetical, or My score — series SHALL be omitted, since those orderings are defined over per-anime figures a series does not have. The result count shown on the page SHALL continue to count anime only.

A series card SHALL NOT show a MAL score in that slot: its badge line already occupies the slot an anime card uses for its meta line, and a series has no single MAL score of its own.

Results SHALL be loaded with continuous (infinite) scroll rather than numbered pages: an initial chunk renders immediately and further chunks are appended automatically as the user scrolls toward the end of the loaded results, with no pagination controls anywhere on the page. Sorting SHALL be applied server-side across the whole candidate result set before it is chunked, so the order is global rather than per-chunk and an item never moves between chunks as more load. Changing the sort SHALL reset the accumulated results to the first chunk. Series cards SHALL all be shown up front rather than participating in chunked reveal.

The system SHALL provide a multi-select Type filter on the search results page, using the same control and display labels as My List's type filter, offering only the media types actually present among the currently loaded candidate results. Selecting one or more types SHALL immediately narrow the displayed anime cards to matching types, applied over the already-loaded candidate set without issuing a new search request. With no type selected, no type restriction applies. The result count line SHALL reflect the type-filtered anime count rather than the full unfiltered candidate count. Series cards SHALL be unaffected by the type filter and SHALL continue to be shown under the default relevance order regardless of which types are selected, since a series is not itself a single media-typed row.

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

#### Scenario: Clearing the type filter
- **WHEN** no type is selected in the search page's type filter
- **THEN** anime cards of every loaded type are shown

### Requirement: Searching schedules a series build for an unknown franchise
When a search's top-ranked anime match belongs to no stored series, the system SHALL schedule that anime's series to be built in the background, so the franchise becomes searchable on a later search without the user having to open its series page.

Scheduling SHALL NOT affect the response: it SHALL NOT delay the search, SHALL NOT add a MAL request to the search request path, and a scheduling or build failure SHALL leave the search results unchanged.

The system SHALL schedule at most one build per search — the top-ranked anime match only — SHALL skip scheduling for queries shorter than three characters, and SHALL NOT re-schedule an anime it has already scheduled since the app started, so a debounced type-ahead does not queue a build per keystroke.

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

### Requirement: Search submission keeps the query text
The system SHALL let the user submit the current search either by pressing Enter in the search box or by clicking a magnifier button beside it, navigating to the search results page for that query. The submitted text SHALL remain in the search box (and SHALL be restored from the URL when the search page is loaded directly or reloaded) rather than being cleared.

#### Scenario: Submit via Enter
- **WHEN** I press Enter with text in the search box
- **THEN** I am taken to the search results page for that query and the text stays in the box

#### Scenario: Submit via the magnifier button
- **WHEN** I click the magnifier button beside the search box
- **THEN** I am taken to the search results page for the current query

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


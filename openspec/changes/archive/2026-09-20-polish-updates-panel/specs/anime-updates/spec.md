## MODIFIED Requirements

### Requirement: An update names the anime, what happened, and why it concerns me

Each shown update SHALL report the anime's picture and title; what happened, naming which kinds it covers; the anime's **current** episode count and premiere date, each shown only where known and omitted entirely where not; and why it concerns me — the entry it is affiliated with together with the relation it holds to it, or, where the anime is my own entry and has no such affiliation, that entry's own status.

**Layout.** An update SHALL be presented as a card whose picture sits on the **left**, with the title beside it at the top, the news beneath the title, and the reason beneath the news. Cards SHALL share a common width wherever they are listed, and their height SHALL follow their content, so a card with a tall picture or more to say is taller than one without.

**The picture SHALL be drawn whole, at its own proportions.** No part of it SHALL be cropped away, and it SHALL NOT be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider and shorter, and a square picture SHALL be drawn square — the same rule the `artwork-selection` picker applies to its options. The drawn picture SHALL be bounded in **both** directions, so that a portrait picture cannot make a card towering and a wide picture cannot leave the text beside it unreadably narrow; a picture too wide to fit that bound SHALL be reduced whole rather than cropped. An anime with no picture SHALL show a placeholder of the app's usual poster proportions rather than a card with no picture area at all.

**One fact SHALL read as one line.** The news SHALL be one line per kind the update covers, and a kind whose news *is* a value SHALL state that value in its own line rather than announcing the kind and leaving the value to a separate line — `Total episodes: 12`, not `Episode count revealed` above `12 episodes`; a premiere date released, a premiere moved, a broadcast slot moved and an episode moved SHALL each read the same way, as one self-contained statement. A schedule change SHALL name both the value it moved to and the value it moved from, including for a moved episode.

**A premiere that moved SHALL say which way it moved, in words that cannot be read in reverse.** A premiere that moved to an **earlier** date SHALL be described as having moved *earlier*; a premiere that moved to a **later** date SHALL be described as *delayed*. Neither wording SHALL rest on an up/down metaphor, since "up" is read both as *sooner* and as *later in the calendar* and so cannot state the direction unambiguously. Where the date the premiere moved to and the date it moved from are the same date, the line SHALL report the move **without asserting a direction**, rather than claiming one of the two directions between a date and itself.

The current-value lines SHALL then be shown for each fact **no news line already stated**: an episode-count release suppresses the episode-count line, and a premiere date released or changed suppresses the premiere-date line, while an announcement — which states neither — still reports both where known.

Episode count and premiere date SHALL be read from the anime's current cached record rather than from anything captured when the update was recorded, so a later correction is reflected on the update rather than leaving it stating a superseded value. The values a schedule change moved between are the exception, and SHALL be reported as recorded.

A date SHALL be shown with its year, so news about a premiere or an episode a year out is unambiguous.

**Nothing SHALL be clipped except a title in the dropdown.** In the navbar dropdown, a title too long for its card MAY be cut off, and SHALL then be recoverable in full the way the app's other truncated titles are. Every other line, on every surface, SHALL be shown in full, wrapping onto as many lines as it needs.

Where an update's anime is affiliated with more than one non-dropped entry, the system SHALL name one of them deterministically, preferring the most specific relation. Where the anime is both my own entry and affiliated with another, the affiliation SHALL be named, as it says more than the entry's own status does.

**The affiliated entry SHALL be named by the title the app displays for it** — its English title where one is known, its MyAnimeList title otherwise — so the reason names that anime exactly as its own card, row and detail page name it, rather than in a title the app shows nowhere else. This holds although the name sits inside a composed line rather than standing on its own as a title, and requires no request the app does not already make: the affiliated entry is a list entry of my own, whose metadata the app already holds. Where two candidate entries hold the same most-specific relation, that same displayed title SHALL settle which is named, so the choice matches what is shown.

Selecting an update SHALL open that anime's detail page.

#### Scenario: An episode count reveal reads as one line

- **WHEN** an update covering an episode-count release is shown for an anime whose total is 12
- **THEN** its news reads `Total episodes: 12` on one line, and no separate episode-count line is shown beneath it

#### Scenario: A premiere reveal reads as one line

- **WHEN** an update covering a premiere-date release is shown
- **THEN** its news states the premiere date itself on one line, and no separate premiere-date line is shown beneath it

#### Scenario: An announcement still reports the facts it arrived with

- **WHEN** an announcement update is shown for an anime whose episode count and premiere date are both known
- **THEN** the card reads `Announced` and then reports the episode count and the premiere date

#### Scenario: A premiere brought forward

- **WHEN** a premiere-date-changed update reports a move from 8 October 2026 to 1 October 2026
- **THEN** its line states that the premiere moved **earlier**, naming both dates, and states it in no wording that could be read as the premiere having been pushed later

#### Scenario: A premiere pushed back

- **WHEN** a premiere-date-changed update reports a move from 5 October 2026 to 12 October 2026
- **THEN** its line states that the premiere was **delayed**, naming both dates

#### Scenario: A premiere that reads as moving to the date it came from

- **WHEN** a premiere-date-changed update would state a move whose two dates are the same date
- **THEN** its line reports the move and the date without claiming that the premiere moved earlier or was delayed

#### Scenario: A moved episode reports both dates

- **WHEN** an episodes-moved update is shown
- **THEN** its line names the episode, the date it moved to, and the date it moved from

#### Scenario: A portrait picture

- **WHEN** a shown update's anime has a portrait poster
- **THEN** the card draws it portrait, whole, and the card is correspondingly taller

#### Scenario: A landscape picture

- **WHEN** a shown update's anime has a picture wider than it is tall
- **THEN** the card draws it wide and short at its own proportions, whole, without cropping it to a portrait slice, and the text beside it still has room to read

#### Scenario: An extremely wide picture

- **WHEN** a shown update's anime has a picture too wide to fit the space a card gives it
- **THEN** it is reduced until it fits whole, rather than being cropped or pushing the card wider than its neighbours

#### Scenario: Cards differ in height but not in width

- **WHEN** a list holds one card with a tall picture and one with a short picture
- **THEN** the two cards are the same width and differ in height

#### Scenario: A long title in the dropdown

- **WHEN** a card in the navbar dropdown has a title too long for its width
- **THEN** the title is cut off, and the full title is available on hover as elsewhere in the app

#### Scenario: A card with a lot to say

- **WHEN** an update covers a broadcast-slot change, naming both the new slot and the previous one
- **THEN** the whole of that text is shown, wrapping as needed, rather than being cut off at one line

#### Scenario: An anime with no episode count yet

- **WHEN** a shown update's anime has no known episode count
- **THEN** no episode count is displayed for it, and its premiere date is displayed if known

#### Scenario: An anime with neither count nor date

- **WHEN** a shown update's anime has neither a known episode count nor a known premiere date
- **THEN** neither is displayed, and the update still shows its picture, title, news and reason

#### Scenario: A corrected count is reflected

- **WHEN** an anime's episode count is corrected from 12 to 13 after its episode-count update was recorded
- **THEN** that update displays 13

#### Scenario: A delay keeps the dates it was recorded with

- **WHEN** a premiere-date-changed update recorded a move from 5 October to 12 October, and the date later moves again
- **THEN** that update still reports the move from 5 October to 12 October, while a newer update reports the newer move

#### Scenario: The affiliation is named

- **WHEN** an update concerns the sequel of an entry I am watching
- **THEN** it names that entry and states that the update's anime is its sequel

#### Scenario: The affiliation is named in English

- **WHEN** an update concerns the sequel of an entry of mine whose MyAnimeList title is "Tensei shitara Ken deshita" and whose English title is "Reincarnated as a Sword"
- **THEN** the reason names it "Reincarnated as a Sword", the same title its own card carries, rather than the romaji one

#### Scenario: An affiliated entry with no English title

- **WHEN** an update concerns the sequel of an entry of mine for which no English title is known
- **THEN** the reason names it by its MyAnimeList title

#### Scenario: My own standalone entry names its status

- **WHEN** an update concerns an anime that is my own Plan to watch entry with no affiliation in my list
- **THEN** it reports that entry's own status rather than an affiliation

#### Scenario: Opening an update

- **WHEN** I select a shown update
- **THEN** the detail page for the anime it concerns opens

### Requirement: The updates history is searchable and date-filterable

The system SHALL offer, from the **top of the updates dropdown**, a control opening a history of **every** shown update ever recorded, listed chronologically with the most recent first — regardless of age. This control SHALL read as one of the app's ordinary secondary buttons — plain text, bordered — rather than in the site's accent colour, since it is not the primary action the dropdown exists for.

The history SHALL offer a search over the anime's titles and a from/to date range, both filtering the list as they are set, and a control clearing them that appears only while a filter is active. It SHALL distinguish "nothing has been recorded" from "nothing matches these filters".

The control opening the history SHALL remain available while the dropdown has nothing recent to show, so older updates stay reachable.

Every row of the history SHALL show **everything it carries**: its picture whole and at its own proportions, its title in full, and every line of its news and its reason in full, wrapping rather than being cut off. A row SHALL be as tall as its own content requires, so rows differ in height; the history SHALL NOT impose a common row height that clips what a row holds.

**The history SHALL hold exactly one scrolling region.** The overlay panel itself SHALL NOT scroll: its title row and its filter row SHALL stay fixed, and the list of rows SHALL be the only part of the overlay that moves, with one scroll position for the whole history. Two scrollable regions for one list — the list inside a scrolling panel — SHALL NOT occur at any window size.

The list SHALL fill whatever height the panel has left beneath the filter row, up to the panel's own ceiling, so a history holding few rows makes a short panel and one holding many fills the height available and scrolls within it. The list SHALL NOT be sized to a fixed number of rows, and a row at its bottom edge MAY therefore be partly visible — which marks that there is more to reach. The panel SHALL never be taller than the window can hold. Its filter row SHALL hold to a single row at the panel's usual width, so the search field, the date range and the clear control do not take height from the list by wrapping; on a window too narrow for them to fit one row, the row MAY wrap, and the list SHALL still fill whatever height is left rather than introducing a second scroll.

Scrolling the list SHALL remain fully usable by wheel, trackpad, touch, and keyboard, while the scrollbar itself SHALL NOT be rendered — the same rule the navbar dropdown's own list follows, so the two surfaces scroll alike. That there is more to reach SHALL be marked by the row at the list's bottom edge being partly visible, rather than by a scrollbar.

#### Scenario: The history holds everything

- **WHEN** I open the history
- **THEN** it lists every shown update ever recorded, most recent first, including those older than 30 days

#### Scenario: Reached from the top of the dropdown

- **WHEN** I open the updates dropdown
- **THEN** the control opening the full history is at the top of it, above the cards

#### Scenario: The History control reads as a secondary button

- **WHEN** the updates dropdown is open
- **THEN** its History control shows plain, bordered styling rather than the site's accent colour

#### Scenario: Searching by title

- **WHEN** I type part of an anime's title into the history's search
- **THEN** only updates whose anime matches that text remain listed

#### Scenario: Filtering by date range

- **WHEN** I set a from date and a to date
- **THEN** only updates detected within that range, inclusive of both days, remain listed

#### Scenario: No match under a filter

- **WHEN** a filter matches none of the recorded updates
- **THEN** the history says nothing matches the filters, distinctly from saying nothing has been recorded

#### Scenario: History reachable from an empty dropdown

- **WHEN** the dropdown has no updates within the last 30 days
- **THEN** the control opening the history is still available and the history still lists the older updates

#### Scenario: A long row is shown in full

- **WHEN** the history lists an update whose news and reason are longer than one line each
- **THEN** the row grows to show all of that text rather than cutting it off

#### Scenario: A history row's picture is whole

- **WHEN** the history lists an update whose anime has a landscape or square picture
- **THEN** that picture is drawn whole at its own proportions, not cropped into a portrait thumbnail

#### Scenario: Rows differ in height

- **WHEN** the history lists one update with a short line of news and one with several
- **THEN** the two rows differ in height, each as tall as its own content

#### Scenario: Only the list scrolls

- **WHEN** the history holds far more rows than the panel can show, on a window short enough that the panel reaches its ceiling
- **THEN** the list scrolls and the panel around it does not, so there is one scroll position and not two

#### Scenario: A short history makes a short panel

- **WHEN** the history holds two rows
- **THEN** the panel is only as tall as its title row, its filters and those two rows, with no empty space beneath them and nothing to scroll

#### Scenario: A long history fills the panel

- **WHEN** the history holds more rows than fit
- **THEN** the list occupies the whole height left beneath the filters and every row is reachable by scrolling it

#### Scenario: Scrolling the history with no visible scrollbar

- **WHEN** the history holds more rows than fit and I scroll it
- **THEN** the list scrolls to reveal the rest, and no scrollbar track or thumb is drawn over or beside it

#### Scenario: Filters stay in view

- **WHEN** I scroll a long history
- **THEN** the search field and date range stay visible above the list, fixed in place rather than scrolling with it

#### Scenario: The filter row fits on one line

- **WHEN** the history is open at the overlay's usual width
- **THEN** the search field, the date range and the clear control sit on one row, leaving the rest of the panel's height to the list

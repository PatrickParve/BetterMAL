## RENAMED Requirements

- FROM: `### Requirement: The Updates section shows the last 30 days, newest first`
- TO: `### Requirement: The updates menu shows the last 30 days, newest first`

## MODIFIED Requirements

### Requirement: The updates menu shows the last 30 days, newest first

The system SHALL surface the last-30-days window from a control in the **navbar**, available on every page, rather than from a section on any one page. Selecting that control SHALL open a dropdown holding every shown update detected within the last 30 days, and no update older than that.

The dropdown SHALL lay them out as a single column of cards running **top to bottom**, most recently detected at the top. Where the column holds more cards than the dropdown's height, it SHALL scroll vertically rather than drop cards, and reaching either end of that scroll SHALL NOT pass the remaining movement on to the page behind.

On opening, the dropdown SHALL be exactly as tall as its **three newest cards** together with the spacing between them: no fourth card partly visible, and no card cut short. Because card heights vary with their content, that height SHALL be taken from the three cards **as actually rendered** rather than from any assumed per-card height, and SHALL be re-taken whenever those heights change — including as pictures finish loading. Where fewer than three updates are shown, the dropdown SHALL be as tall as they need and no taller. Where three cards would not fit in the window, the dropdown SHALL be reduced to what fits and the rest reached by scrolling.

An update passing 30 days old SHALL leave the dropdown without being deleted, and SHALL remain in the history.

Where the window holds nothing to show, the dropdown SHALL say so in place of the list.

The dropdown SHALL close when Escape is pressed, when a click lands outside it, and when a card in it is followed to an anime's detail page.

#### Scenario: Reachable from every page

- **WHEN** I am on any page of the app
- **THEN** the navbar's updates control is present, and opening it shows the last 30 days of updates without navigating anywhere

#### Scenario: Newest at the top

- **WHEN** the dropdown shows three updates detected on different days
- **THEN** the most recently detected is at the top and the oldest at the bottom

#### Scenario: Opening height fits exactly three cards

- **WHEN** I open the dropdown on a window holding more than three updates, whose three newest cards are of differing heights
- **THEN** the dropdown is as tall as exactly those three cards and the spacing between them — the third card is whole and the fourth is not visible until I scroll

#### Scenario: The opening height follows the cards as they render

- **WHEN** a picture in one of the three newest cards finishes loading and changes that card's height
- **THEN** the dropdown's height is re-taken from the three cards as they now stand, still ending exactly at the third

#### Scenario: Fewer than three updates

- **WHEN** the window holds two updates
- **THEN** the dropdown is as tall as those two cards need and shows no empty space beneath them

#### Scenario: Three cards taller than the window

- **WHEN** the three newest cards together are taller than the space beneath the control
- **THEN** the dropdown is reduced to the space available and the remaining cards are reached by scrolling within it

#### Scenario: More cards than fit

- **WHEN** the window holds more cards than the dropdown's height
- **THEN** the dropdown scrolls vertically and every card remains reachable

#### Scenario: Scrolling the list does not scroll the page

- **WHEN** I scroll to the bottom of the dropdown's list and keep scrolling
- **THEN** the page behind the dropdown does not move

#### Scenario: An update ages out

- **WHEN** an update detected 31 days ago would otherwise be shown
- **THEN** it does not appear in the dropdown, and it still appears in the history

#### Scenario: Nothing to show

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the dropdown displays text saying there are no recent updates, in place of the list

#### Scenario: Dismissing the dropdown

- **WHEN** I press Escape, click outside the dropdown, or follow one of its cards to an anime
- **THEN** the dropdown closes

### Requirement: An update names the anime, what happened, and why it concerns me

Each shown update SHALL report the anime's picture and title; what happened, naming which kinds it covers; the anime's **current** episode count and premiere date, each shown only where known and omitted entirely where not; and why it concerns me — the entry it is affiliated with together with the relation it holds to it, or, where the anime is my own entry and has no such affiliation, that entry's own status.

**Layout.** An update SHALL be presented as a card whose picture sits on the **left**, with the title beside it at the top, the news beneath the title, and the reason beneath the news. Cards SHALL share a common width wherever they are listed, and their height SHALL follow their content, so a card with a tall picture or more to say is taller than one without.

**The picture SHALL be drawn whole, at its own proportions.** No part of it SHALL be cropped away, and it SHALL NOT be letterboxed inside a box of a different shape: a portrait picture SHALL be drawn portrait, a landscape picture SHALL be drawn landscape and correspondingly wider and shorter, and a square picture SHALL be drawn square — the same rule the `artwork-selection` picker applies to its options. The drawn picture SHALL be bounded in **both** directions, so that a portrait picture cannot make a card towering and a wide picture cannot leave the text beside it unreadably narrow; a picture too wide to fit that bound SHALL be reduced whole rather than cropped. An anime with no picture SHALL show a placeholder of the app's usual poster proportions rather than a card with no picture area at all.

**One fact SHALL read as one line.** The news SHALL be one line per kind the update covers, and a kind whose news *is* a value SHALL state that value in its own line rather than announcing the kind and leaving the value to a separate line — `Total episodes: 12`, not `Episode count revealed` above `12 episodes`; a premiere date released, a premiere moved, a broadcast slot moved and an episode moved SHALL each read the same way, as one self-contained statement. A schedule change SHALL name both the value it moved to and the value it moved from, including for a moved episode.

The current-value lines SHALL then be shown for each fact **no news line already stated**: an episode-count release suppresses the episode-count line, and a premiere date released or changed suppresses the premiere-date line, while an announcement — which states neither — still reports both where known.

Episode count and premiere date SHALL be read from the anime's current cached record rather than from anything captured when the update was recorded, so a later correction is reflected on the update rather than leaving it stating a superseded value. The values a schedule change moved between are the exception, and SHALL be reported as recorded.

A date SHALL be shown with its year, so news about a premiere or an episode a year out is unambiguous.

**Nothing SHALL be clipped except a title in the dropdown.** In the navbar dropdown, a title too long for its card MAY be cut off, and SHALL then be recoverable in full the way the app's other truncated titles are. Every other line, on every surface, SHALL be shown in full, wrapping onto as many lines as it needs.

Where an update's anime is affiliated with more than one non-dropped entry, the system SHALL name one of them deterministically, preferring the most specific relation. Where the anime is both my own entry and affiliated with another, the affiliation SHALL be named, as it says more than the entry's own status does.

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

#### Scenario: My own standalone entry names its status

- **WHEN** an update concerns an anime that is my own Plan to watch entry with no affiliation in my list
- **THEN** it reports that entry's own status rather than an affiliation

#### Scenario: Opening an update

- **WHEN** I select a shown update
- **THEN** the detail page for the anime it concerns opens

### Requirement: The updates history is searchable and date-filterable

The system SHALL offer, from the **top of the updates dropdown**, a control opening a history of **every** shown update ever recorded, listed chronologically with the most recent first — regardless of age.

The history SHALL offer a search over the anime's titles and a from/to date range, both filtering the list as they are set, and a control clearing them that appears only while a filter is active. It SHALL distinguish "nothing has been recorded" from "nothing matches these filters".

The control opening the history SHALL remain available while the dropdown has nothing recent to show, so older updates stay reachable.

Every row of the history SHALL show **everything it carries**: its picture whole and at its own proportions, its title in full, and every line of its news and its reason in full, wrapping rather than being cut off. A row SHALL be as tall as its own content requires, so rows differ in height; the history SHALL NOT impose a common row height that clips what a row holds. The history's own list SHALL remain scrollable with its search and date controls staying in view above it.

#### Scenario: The history holds everything

- **WHEN** I open the history
- **THEN** it lists every shown update ever recorded, most recent first, including those older than 30 days

#### Scenario: Reached from the top of the dropdown

- **WHEN** I open the updates dropdown
- **THEN** the control opening the full history is at the top of it, above the cards

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

#### Scenario: Filters stay in view

- **WHEN** I scroll a long history
- **THEN** the search field and date range stay visible above the list

## ADDED Requirements

### Requirement: The updates control signals unseen news

The navbar's updates control SHALL carry an indicator whenever at least one update in the last-30-days window is **newer than the newest update already shown to me**, and SHALL carry no indicator otherwise. The indicator SHALL be reflected in the control's accessible name as well as visually, so it is not carried by appearance alone.

Opening the dropdown SHALL mark everything then in the window as shown, clearing the indicator. The indicator SHALL stay clear across reloads until an update newer than that is detected, at which point it SHALL appear again.

Where the system holds no record of anything having been shown, every update in the window SHALL count as unseen. The record SHALL be kept locally, alongside the app's other per-browser viewing preferences, and losing it SHALL do no more than raise the indicator once.

An update older than 30 days SHALL never raise the indicator, whether or not it was ever shown: the indicator covers the window the dropdown covers.

#### Scenario: A newly detected update raises the indicator

- **WHEN** an update is detected after the last time I opened the dropdown
- **THEN** the navbar's updates control shows its indicator

#### Scenario: Opening clears it

- **WHEN** I open the dropdown while the indicator is showing
- **THEN** the indicator clears, and stays clear when I close and reopen the dropdown

#### Scenario: It stays clear across a reload

- **WHEN** I open the dropdown, then reload the app with no new update detected in between
- **THEN** the control shows no indicator

#### Scenario: Only news newer than what I have seen counts

- **WHEN** I have opened the dropdown, and one further update is detected afterwards
- **THEN** the indicator appears for that one update and clears when I next open the dropdown

#### Scenario: First use

- **WHEN** the app has no record of my having been shown any update, and the window holds updates
- **THEN** the control shows its indicator until I open the dropdown

#### Scenario: An empty window shows no indicator

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the control shows no indicator, and the control itself is still present

#### Scenario: Ageing out does not raise it

- **WHEN** the newest update in the window is one I have already been shown, and older updates exist in the history
- **THEN** the control shows no indicator

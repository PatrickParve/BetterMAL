## MODIFIED Requirements

### Requirement: The updates control signals unseen news

The navbar's updates control SHALL carry an indicator whenever at least one shown update in the last-30-days window has **not yet been seen**, and SHALL carry no indicator otherwise. The indicator SHALL be reflected in the control's accessible name as well as visually, so it is not carried by appearance alone.

**Seen is recorded per update, on this device.** Whether an update has been seen SHALL be recorded on the update itself, as a plain seen or not-seen flag, in this device's own database. When it was seen SHALL NOT be recorded. An update SHALL become seen only on the terms "An update becomes seen once I have looked at it" sets out, and opening the dropdown SHALL NOT by itself mark anything seen. Once seen, an update SHALL stay seen.

Every browser on this device SHALL read the same flags; seen state SHALL NOT be kept per browser. Seen state SHALL NOT travel between devices either. It is part of the anime-updates feed, which the device-transfer file already leaves out, so exporting SHALL NOT carry it and importing a file SHALL leave this device's flags exactly as they were.

**The indicator follows the flags.** The indicator SHALL clear as soon as the last unseen update in the window has been seen, including while the dropdown or the history is still open. It SHALL appear again when an unseen update enters the window.

**Other browsers catch up when I switch to them.** The control SHALL re-check the window whenever the app's window gains focus or its tab becomes visible, in addition to its regular poll. Updates seen in one browser SHALL therefore clear the indicator in another as soon as I switch to it.

**Only shown updates count.** An update that "Updates are shown for my own entries and for their franchises" hides SHALL NOT raise the indicator. Hiding and showing an update SHALL NOT change its flag. When a dropped entry is restored, its updates SHALL return as seen or unseen as they were before: an unseen one inside the window SHALL raise the indicator again, and one already seen SHALL NOT.

**The window.** An update older than 30 days SHALL never raise the indicator, whether or not it has been seen: the indicator covers the window the dropdown covers. An unseen update older than that SHALL still be marked New in the history.

**Starting point.** Every update recorded before this rule took effect SHALL count as seen. Every update recorded afterwards SHALL begin unseen.

#### Scenario: A newly detected update raises the indicator

- **WHEN** an update is detected and shown
- **THEN** the navbar's updates control shows its indicator, and its accessible name says there is something new

#### Scenario: Opening alone does not clear it

- **WHEN** I open the dropdown while the indicator is showing, and close it again before any card has been looked at
- **THEN** the indicator is still showing, and the same cards are marked New when I next open it

#### Scenario: Looking at every unseen update clears it

- **WHEN** every unseen update in the window has been looked at in the dropdown
- **THEN** the indicator clears, without waiting for the dropdown to close

#### Scenario: Looking in the history counts as well

- **WHEN** I look at the window's unseen updates in the history rather than in the dropdown
- **THEN** they become seen and the indicator clears

#### Scenario: It stays clear across a reload

- **WHEN** every update in the window has been seen, and I reload the app with no new update detected in between
- **THEN** the control shows no indicator

#### Scenario: Another browser catches up on focus

- **WHEN** I look at every unseen update in one browser, then switch to another browser on the same device that was showing the indicator
- **THEN** the indicator clears there as soon as its window gains focus, without waiting for the regular poll

#### Scenario: Seen state stays on this device

- **WHEN** I export this device's data, and later import a file exported from my other device
- **THEN** the exported file carries no seen state, and no update on this device changes between seen and unseen because of the import

#### Scenario: An empty window shows no indicator

- **WHEN** no shown update was detected within the last 30 days
- **THEN** the control shows no indicator, and the control itself is still present

#### Scenario: Ageing out does not raise it

- **WHEN** the only unseen update is older than 30 days
- **THEN** the control shows no indicator, and that update is marked New in the history

#### Scenario: A hidden update does not count

- **WHEN** the only unseen update in the window concerns an entry I have since set to Dropped, and nothing else connects it to my list
- **THEN** the control shows no indicator

#### Scenario: Restoring an entry brings back its unseen news

- **WHEN** I restore that entry from Dropped, and it has one unseen update and one seen update inside the window
- **THEN** the indicator shows again, and only the unseen one is marked New

#### Scenario: Updates already recorded start seen

- **WHEN** this rule first takes effect on a device that already holds updates
- **THEN** every one of those updates counts as seen, and none of them raises the indicator

### Requirement: The updates control is marked while its panel is open

The navbar's updates control SHALL carry the same treatment a navbar page control carries while its page is being viewed — a tinted background together with a border stronger than the one hovering produces — for as long as what it opened is on screen. This SHALL hold while its dropdown is open, and SHALL continue to hold while the updates history overlay opened from that dropdown is open, so the control that produced what is on screen stays visibly the source of it.

Hovering the control while it is marked SHALL NOT replace or weaken the mark, exactly as hovering the current page's navbar link does not.

Closing the dropdown, or closing the history overlay, SHALL clear the mark.

This mark SHALL be independent of the unseen-news indicator: the control SHALL be markable with or without the indicator showing, and the indicator's own rules SHALL be unaffected. Opening the dropdown does not itself clear the indicator, so the control can be marked while the indicator is still showing, until the unseen updates have been looked at.

#### Scenario: Opening the dropdown marks the control
- **WHEN** I open the updates dropdown
- **THEN** the updates control shows the same tinted background and border a navbar link shows for the page I am on

#### Scenario: The mark survives following History
- **WHEN** I open the dropdown and follow **History** to the updates history overlay
- **THEN** the updates control stays marked for as long as that overlay is open

#### Scenario: Closing clears the mark
- **WHEN** I close the dropdown, or close the history overlay
- **THEN** the updates control returns to its unmarked appearance

#### Scenario: Hovering the marked control keeps it marked
- **WHEN** I move the pointer over the updates control while its dropdown is open
- **THEN** it keeps its stronger mark rather than falling back to the hover border

#### Scenario: The mark and the indicator are independent
- **WHEN** I open the dropdown while the unseen indicator is showing
- **THEN** the control becomes marked, and the indicator stays until the unseen updates have been looked at, neither state being derived from the other

## ADDED Requirements

### Requirement: An update becomes seen once I have looked at it

An update SHALL become seen once I have looked at its card, in the navbar dropdown or in the updates history. Either surface SHALL count, under the same rules.

A card SHALL count as looked at when any of these holds:

- **It has been on screen, whole, for about a second.** The whole card SHALL have been inside the list's visible area, continuously, for about one second while the tab is visible. The visible area is the part of the list not scrolled out of view, not clipped by the surface holding the list, and inside the window. Scrolling the card partly out of that area, or hiding the tab, SHALL restart the second.
- **I have moved the mouse over it.** Moving the mouse pointer over any visible part of a card SHALL count at once. A card carried under a still pointer by scrolling SHALL NOT count until the pointer moves. Touch SHALL NOT count as hovering.
- **I have followed it to its anime.** Following a card to its anime's detail page SHALL count at once, whether by mouse, touch or keyboard, and however briefly the card was on screen.

A card taller than the list's visible area can never be whole on screen. It SHALL count once it has filled the whole of that area for the same second.

Nothing else SHALL mark an update seen. Opening the dropdown or the history SHALL NOT, and neither SHALL a card that was only partly visible, was scrolled past, or was on screen for less than the second, unless it was hovered or followed.

These rules SHALL hold with the list's scrollbar hidden and the list at its three-card opening height. A card below the third is not in the visible area until it has been scrolled to, and SHALL NOT count until then.

**Reporting.** Cards that become seen close together SHALL be reported to the server together, as one report, rather than one request per card. A report SHALL be sent without waiting for the dropdown or history to close, so another browser can catch up while I am still looking. Anything still waiting SHALL also be sent when the dropdown or history closes and when the page is left. A report that fails SHALL leave its updates unseen. They SHALL be reported again the next time they are looked at, and the failure SHALL NOT be announced.

#### Scenario: A card whole on screen for a second

- **WHEN** a card has been fully inside the dropdown list's visible area for one second while the tab is visible
- **THEN** its update becomes seen

#### Scenario: A glance does not count

- **WHEN** I scroll a card fully into view and out again within half a second
- **THEN** its update stays unseen

#### Scenario: A card cut off at the edge does not count

- **WHEN** I leave the list scrolled so that a card is cut off at its bottom edge
- **THEN** that card's update stays unseen until the card has been scrolled fully into view for a second

#### Scenario: Cards below the opening height

- **WHEN** I open the dropdown on a window of five unseen updates and do not scroll
- **THEN** after a second the three cards in view become seen, and the fourth and fifth stay unseen

#### Scenario: A hidden tab does not count

- **WHEN** I open the dropdown and switch to another tab before a second has passed
- **THEN** its cards stay unseen until I return and they have been on screen for a second while the tab is visible

#### Scenario: Hovering counts at once

- **WHEN** I move the mouse over a card, even one only partly visible
- **THEN** its update becomes seen immediately

#### Scenario: Scrolling under a still pointer does not count

- **WHEN** I scroll the list by wheel or trackpad while the pointer rests over it
- **THEN** the cards that pass under the pointer do not become seen by hovering

#### Scenario: Following a card counts at once

- **WHEN** I tap a card, or press Enter on it, before it has been on screen for a second
- **THEN** its update becomes seen, and the report reaches the server even though the dropdown closes as the anime's page opens

#### Scenario: A card taller than the list

- **WHEN** a card is taller than the list's visible area, and fills that area for a second
- **THEN** its update becomes seen

#### Scenario: Opening marks nothing

- **WHEN** I open the dropdown and close it at once
- **THEN** no update becomes seen

#### Scenario: The history counts

- **WHEN** a card in the history is looked at on these same terms
- **THEN** its update becomes seen, and is not marked New the next time the dropdown opens

#### Scenario: Cards seen together are reported together

- **WHEN** three cards become seen within the same moment
- **THEN** they reach the server in one report

#### Scenario: A failed report leaves updates unseen

- **WHEN** the report carrying an update fails
- **THEN** that update stays unseen, no failure notice is shown, and it becomes seen the next time it is looked at and the report succeeds

### Requirement: Unseen updates are marked New

Wherever an update's card is listed — in the navbar dropdown and in the history — an unseen update SHALL be marked **New**. The marking SHALL be carried by three things together, so that it does not rest on colour alone:

- the same dot the updates control carries for unseen news, at the same size and in the same colour;
- an accent edge along the card;
- the word "New", which SHALL be part of the card's accessible name.

A card whose update has been seen SHALL carry none of the three. The list SHALL keep its newest-first order and SHALL NOT separate new cards from older ones with a divider, heading or gap.

**The marking is fixed for one look.** A look SHALL run from the moment the dropdown or the history opens until it closes; moving from the dropdown to the history ends one look and begins another.

- A card SHALL be marked New for the whole of a look if its update was unseen when the card was first shown in that look.
- A card whose update becomes seen during the look SHALL keep its marking until the look ends, and SHALL NOT be marked New when the dropdown or history next opens.
- Refreshing the list during a look, by the regular poll or a focus re-check, SHALL NOT remove the marking from a card shown as New, nor add it to a card shown without it.
- An update that first appears during a look SHALL be marked New if it is unseen, and SHALL follow the same rule from then on.

An unseen update older than 30 days SHALL be marked New in the history, although it never raises the indicator.

**The marking changes no layout.** It SHALL NOT change a card's width, padding or wrapping. The dot and the word SHALL sit at the start of the title's line rather than on a line of their own. In the dropdown:

- where a title is too long for its line, the title SHALL still be cut off to one line, with the dot and the word shown in full beside it;
- the full title SHALL still be recoverable on hover;
- the dropdown and the history SHALL still open exactly three cards tall.

#### Scenario: An unseen card is marked

- **WHEN** I open the dropdown and one of its cards is for an unseen update
- **THEN** that card shows the dot, the accent edge and the word "New"

#### Scenario: A seen card is not marked

- **WHEN** the dropdown lists an update that has already been seen
- **THEN** its card shows none of the dot, the accent edge or the word "New"

#### Scenario: New is not carried by colour alone

- **WHEN** a screen reader reads a card marked New
- **THEN** the card's accessible name includes the word "New"

#### Scenario: No divider

- **WHEN** the dropdown lists two unseen and two seen updates
- **THEN** all four appear as one newest-first list, with no divider or heading between the new ones and the rest

#### Scenario: The marking stays while I am looking

- **WHEN** a card marked New becomes seen while the dropdown is open
- **THEN** it stays marked New until I close the dropdown

#### Scenario: The marking is gone next time

- **WHEN** I close the dropdown after a card marked New has become seen, and open the dropdown or the history again
- **THEN** that card is not marked New

#### Scenario: A refresh does not change the look

- **WHEN** the list refreshes while the dropdown is open, after one of its New cards has been seen in another browser
- **THEN** that card stays marked New until I close the dropdown

#### Scenario: An update arriving during a look

- **WHEN** a new update arrives by the poll while the dropdown is open
- **THEN** it appears marked New, and keeps that marking until I close the dropdown even if I look at it

#### Scenario: An old unseen update in the history

- **WHEN** the history lists an unseen update older than 30 days
- **THEN** it is marked New, and the updates control shows no indicator on its account

#### Scenario: A long title with the marking

- **WHEN** a card marked New in the dropdown has a title too long for its line
- **THEN** the dot and the word "New" are shown in full, the title is cut off beside them, and the full title appears on hover

#### Scenario: The opening height still fits three cards

- **WHEN** I open the dropdown on a window of more than three updates, some of them marked New
- **THEN** the dropdown is exactly as tall as its three newest cards and the spacing between them

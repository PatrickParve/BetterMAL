## ADDED Requirements

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

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: A profile box title keeps one gap whether or not it carries a control
Every profile box whose title is followed by a control row or content SHALL leave the same vertical gap between the bottom of its title and the top of what follows. This SHALL hold whether or not the title has a control beside it. "My top anime" with its **Rank** control and "Top series" with its **Multi-entry only** control SHALL leave exactly the gap "Most rewatched" leaves under its bare title.

A control beside a title SHALL be vertically centred on the title's line. It SHALL NOT make the title row taller than the title itself, SHALL NOT push the title down from the top of its box, and SHALL NOT overlap the title or the row beneath it.

This SHALL hold at every window width the app supports, since the title's size scales with the window.

#### Scenario: Three boxes, one gap
- **WHEN** the profile page shows "My top anime" with its **Rank** control, "Top series", and "Most rewatched"
- **THEN** the gap between each title and the button row beneath it is the same in all three boxes

#### Scenario: The Rank control comes and goes
- **WHEN** "My top anime" gains or loses its **Rank** control because a tier gains or loses a second member
- **THEN** the title and the row beneath it do not move

#### Scenario: The control sits on the title's line
- **WHEN** a box shows a control beside its title
- **THEN** the control is vertically centred on the title and clear of the row beneath it

#### Scenario: Latest updates lines up with its neighbours
- **WHEN** the profile's top row shows "Latest updates" with its **Full history** control next to boxes with bare titles
- **THEN** the gap between its title and its feed matches the gap under their titles, and its feed still shows exactly five whole rows reaching the bottom of the box

#### Scenario: A narrow window
- **WHEN** the window is narrowed until the titles are at their smallest size
- **THEN** the three gaps are still equal

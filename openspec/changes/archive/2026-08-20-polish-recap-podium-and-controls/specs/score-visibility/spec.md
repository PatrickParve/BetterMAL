## MODIFIED Requirements

### Requirement: The global hide toggle is presented as a switch
The global hide/unhide control in the navigation bar SHALL be presented as a two-state switch rather than as a text button, so that it reads as a control the app is currently on one side of, and so which side that is can be told at a glance.

The switch SHALL consist of a track carrying a fixed label naming what it governs, and a knob that sits over that label and slides from one side of the track to the other as the state changes. The label SHALL NOT change with the state — the state is carried by the knob's position and its icon — so the control's width is identical in both states and toggling it SHALL NOT reflow the navigation bar around it.

The knob SHALL sit wholly within the track, separated from the track's border by a visible gap on every side, in both states and at every point of its travel. Neither the knob nor any shadow it casts SHALL overlap, obscure, or darken the track's border. The gap at the knob's resting end and at its travelled end SHALL be equal, so the switch is symmetric in both states.

The knob SHALL carry an eye icon: **open** while scores are shown, and **closed or struck through** while scores are hidden. The icon alone SHALL be sufficient to tell the current state, without reference to the knob's position or the track's colouring. The icon SHALL be centred within the knob, and the knob SHALL be centred on the track's vertical axis.

Moving between the two states SHALL be animated — the knob travelling across the track and the eye opening or closing — rather than snapping between two static images. Where the user has asked for reduced motion, the control SHALL change state instantly instead, remaining fully usable and still showing the correct icon.

The switch SHALL expose its two-state nature to assistive technology, reporting whether scores are currently shown, and SHALL carry an accessible name describing the action taking it will perform. It SHALL be operable by keyboard, SHALL show a visible focus indicator when focused by keyboard, and SHALL give the same hover feedback the app's other navigation controls give.

The switch SHALL be the same height as the navigation bar's other controls, so adjusting the knob within it SHALL NOT change the height of the navigation bar or the alignment of the controls beside it.

Only the control's presentation changes: it SHALL drive the same global hide state as before, with the same persistence, so a user who had scores hidden finds the switch already in the hidden position when the app next loads.

#### Scenario: Scores shown
- **WHEN** scores are not hidden
- **THEN** the switch's knob rests on one side of its track showing an open eye

#### Scenario: Scores hidden
- **WHEN** scores are hidden
- **THEN** the knob rests on the other side of the track showing a closed or struck-through eye

#### Scenario: The knob does not cover the track's border
- **WHEN** the switch is shown in either state
- **THEN** the track's border is visible unbroken all the way around, with a clear gap between it and the knob, and no shadow from the knob falling across it

#### Scenario: The knob is centred and symmetric
- **WHEN** I compare the switch in its two states
- **THEN** the knob is vertically centred on the track in both, and the gap between the knob and the nearer end of the track is the same in both

#### Scenario: Toggling animates
- **WHEN** I use the switch
- **THEN** the knob slides across the track and the eye opens or closes as it travels, rather than the control redrawing instantly

#### Scenario: The navbar does not reflow
- **WHEN** I toggle the switch
- **THEN** the control occupies exactly the same width as before and nothing beside it in the navigation bar moves

#### Scenario: The switch matches the navbar's control height
- **WHEN** the navigation bar is shown
- **THEN** the switch is the same height as the controls beside it and their centre lines agree

#### Scenario: Reduced motion
- **WHEN** I have asked my system for reduced motion and I use the switch
- **THEN** it changes state without the sliding or opening animation, and still shows the icon for the new state

#### Scenario: Announced as a switch
- **WHEN** I reach the control with a screen reader
- **THEN** it is announced as a two-state control reporting whether scores are currently shown, with a name describing what using it will do

#### Scenario: Keyboard operation
- **WHEN** I focus the control with the keyboard and activate it
- **THEN** scores are hidden or shown as they would be on a click, and the control shows a visible focus indicator while focused

#### Scenario: Hover feedback
- **WHEN** I move the pointer over the switch
- **THEN** it is clearly highlighted in the same style as the app's other navigation controls

#### Scenario: The hidden state still survives a reload
- **WHEN** I hide scores with the switch and then reload the page
- **THEN** scores are still hidden and the switch is already in its hidden position

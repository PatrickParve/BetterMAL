## ADDED Requirements

### Requirement: A score-role control carries its role's colour
Where a control's options select between the two score roles — which score a list is ranked by, which score a view shows — each option SHALL carry the colour of the role it selects: the option choosing MAL's score in the MAL role's blue, the option choosing my score in the mine role's purple. A control that switches to somebody else's opinion SHALL NOT light up in the colour this app uses for my own.

This SHALL apply to the recap page's top-10 ranking-basis toggle and the profile page's Top series ranking-basis control, and to any later control offering the same choice.

The colour SHALL be carried through the control's states as the surface's own control styling defines them — at minimum its selected state and its hover state — rather than appearing in one state and reverting to the page accent in another. Carrying a role's colour SHALL change the control's colour only: its size, shape, and the set of states it distinguishes SHALL be unchanged, so a cluster it sits in SHALL NOT reflow.

Controls that select something other than a score role — a media type, a time filter, a period — SHALL NOT take a score role's colour, so the blue and the purple keep meaning "MAL's opinion" and "mine" rather than merely "selected".

#### Scenario: Choosing MAL's score is blue
- **WHEN** I select the **MAL score** option on the recap's top 10 or on the profile's Top series
- **THEN** that option is drawn in the MAL role's blue, matching the scores the list then ranks on

#### Scenario: Choosing my score is purple
- **WHEN** I select the **My score** option on either control
- **THEN** that option is drawn in the mine role's purple

#### Scenario: The colour holds across the control's states
- **WHEN** I hover the **MAL score** option and then select it
- **THEN** both states are drawn in the MAL role's blue rather than one of them reverting to the page's accent

#### Scenario: A non-score control keeps the accent
- **WHEN** I select a media type on the profile page or a time filter on the recap page
- **THEN** the selected option is drawn in the page's accent, not in either score role's colour

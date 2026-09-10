## MODIFIED Requirements

### Requirement: Next-episode countdown on currently-watching cards
The system SHALL show, on each currently-watching card, a countdown to the next episode in the form "Next ep: in X days, Y h", computed from the earliest stored per-episode air instant still in the future, converted to local time.

The time remaining SHALL be rounded **up** to the next whole hour before being split into days and hours, so the countdown never reads fewer hours than actually remain: any remainder under an hour reads "in 0 days, 1 h", and a remainder of 23 hours and 30 minutes reads "in 1 days, 0 h". A remainder that is already a whole number of hours SHALL be shown unchanged. The countdown SHALL never read "0 days, 0 h" while the next episode is still in the future. The countdown SHALL be produced by the same computation the anime detail page's Status-field countdown uses, so the card and the detail page always agree for the same episode.

The countdown SHALL NOT be projected from the anime's broadcast day and time when no future stored episode row exists.

#### Scenario: Showing the countdown
- **WHEN** a currently-watching card is rendered for an anime with a stored episode row whose air instant is in the future
- **THEN** it shows the time remaining until that instant, rounded up to the next whole hour, as "Next ep: in X days, Y h"

#### Scenario: Less than an hour left
- **WHEN** a currently-watching anime's next stored episode airs in 59 minutes
- **THEN** the card reads "Next ep: in 0 days, 1 h" rather than "0 days, 0 h"

#### Scenario: A partial hour rounds up
- **WHEN** a currently-watching anime's next stored episode airs in 3 days, 4 hours and 5 minutes
- **THEN** the card reads "Next ep: in 3 days, 5 h"

#### Scenario: Card and detail page agree
- **WHEN** a currently-watching anime's next stored episode is 40 minutes away and I compare its card with its detail page
- **THEN** both report one hour remaining — "in 0 days, 1 h" on the card and "next in 0d 1h" on the detail page

#### Scenario: No known next episode
- **WHEN** a currently-watching anime has no stored episode row with a future air instant
- **THEN** the card omits the next-episode countdown rather than showing a stale or projected value

#### Scenario: Countdown skips a break
- **WHEN** a currently-watching anime is on a one-week break and its next stored episode is two weeks out
- **THEN** the countdown counts to that instant rather than to the next weekly broadcast slot

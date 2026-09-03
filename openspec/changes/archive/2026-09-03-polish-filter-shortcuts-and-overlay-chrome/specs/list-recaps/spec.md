## ADDED Requirements

### Requirement: The score board is restored with the recap page
The score board's open state SHALL be part of the recap page's restorable state. When a recap page that was left with the board open is returned to by back/forward navigation, the board SHALL be shown again over that page, holding the same period's scores it held before.

The board's own scroll position SHALL be restored with it, so a board that was scrolled a long way down its slots comes back at that position rather than at the top. Restoration SHALL happen once the board's slots have been laid out, so the recorded position is reachable rather than clamped to a shorter board.

Opening the board SHALL still add nothing to the browser's history and SHALL still put nothing in the address, so the board is neither a destination the back gesture stops at nor part of a shared link. A recap page left with the board closed SHALL be restored with it closed, and a fresh visit to the recap page SHALL open with the board closed as it does today.

Restoring the board SHALL leave the page behind it where it was: closing a restored board SHALL reveal the recap at the scroll position it was left at, exactly as closing the board does when it was never navigated away from.

#### Scenario: The board comes back
- **WHEN** I open the score board, follow one of its posters to an anime, and then go back
- **THEN** the score board is open again over the recap page, showing the same period

#### Scenario: The board comes back where I was in it
- **WHEN** I scroll the score board down to a low score, follow a poster, and then go back
- **THEN** the board is scrolled to the same place rather than back at the top slot

#### Scenario: A closed board stays closed
- **WHEN** I open the score board, close it, open an anime from the page behind it, and then go back
- **THEN** the recap page is restored with no board over it

#### Scenario: A fresh visit has no board
- **WHEN** I reach the recap page by clicking its navbar link
- **THEN** the score board is closed

#### Scenario: Opening the board is still not a history entry
- **WHEN** I open the score board and then press the browser's back gesture
- **THEN** I leave the recap page altogether rather than merely closing the board

#### Scenario: The page behind a restored board is where I left it
- **WHEN** I return to a recap page whose board reopens, and I then close the board
- **THEN** the recap page beneath is at the scroll position I left it at

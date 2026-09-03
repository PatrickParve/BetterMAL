## MODIFIED Requirements

### Requirement: An overlay closes when the page it was opened over is left

An overlay SHALL close as soon as the app moves to a page other than the one the overlay was opened over, so an overlay can never hang above a page it has nothing to do with.

This SHALL hold however the move is made — the browser's back or forward gesture, a link inside the overlay itself, a link on the page behind it, or the navbar — and SHALL be a property of the app's overlay mechanism rather than of any one overlay, so it holds for the entry editor, the ranking editor, the completion-score prompt, the picture and title pickers, the edit-history overlay, the score board, the recap picker, the unresolved-episodes overlay, the related-anime overlay, and for any overlay added later, without that overlay having to ask for it.

A **different page** SHALL mean a different address for the content being shown, including the same kind of page showing a different subject: moving from one anime's detail page to another anime's, or from one series to another, SHALL close an overlay opened on the first, even though the same kind of page remains on screen.

A change of a page's own view state carried in the address — a filter, a sort, a scope, a page number, a search term — SHALL NOT count as leaving the page, and SHALL NOT close an open overlay.

Closing on navigation SHALL be a **dismissal**, identical to pressing Escape: it SHALL NOT save, submit, or confirm anything the overlay was holding, and it SHALL NOT ask for confirmation first. Unsaved edits in the overlay are discarded exactly as they are on Escape. An overlay that does real work as part of its own dismissal — the ranking editor, which flushes a pending arrangement when dismissed — SHALL still do that work, because the overlay SHALL be closed through its own close path rather than by being torn off the screen.

Where more than one overlay is open, leaving the page SHALL close all of them.

As the last overlay closes, the hold on the page behind it SHALL be released as it is on any other close, so the page being navigated to scrolls normally and is not left frozen.

A page whose restorable state records that one of its overlays was open (see `page-state-restoration`) SHALL NOT be an exception to any of the above. Such an overlay is closed on leaving the page like every other; what is restored is the page, which opens a **new** overlay of its own as part of being rebuilt as it was left. The overlay on screen after a restore therefore belongs to the page beneath it, no overlay outlives the navigation, and the dismissal that closing on navigation performs — including any work an overlay does on its own dismissal — happens exactly as it does for an overlay whose page records nothing.

#### Scenario: Going back closes the editor

- **WHEN** I open the entry editor on an anime and then press the browser's back gesture
- **THEN** the editor closes, and the page I have gone back to is shown with nothing over it

#### Scenario: Going forward closes it too

- **WHEN** I open the entry editor, press back, and then press forward
- **THEN** no editor is on screen at any point after the first navigation

#### Scenario: Moving between two anime closes an overlay

- **WHEN** I open an overlay on one anime's detail page and follow a related-anime link to a different anime
- **THEN** the overlay closes and the new anime's page is shown with nothing over it

#### Scenario: Filtering is not leaving the page

- **WHEN** an overlay is open and the page's own filter, sort, or page number changes in the address
- **THEN** the overlay stays open

#### Scenario: Navigating away does not save

- **WHEN** I change values in the entry editor and then navigate away without pressing Save
- **THEN** the editor closes without saving those values and without asking me to confirm

#### Scenario: Dismissal work still runs

- **WHEN** the ranking editor is open with an arrangement it would flush on dismissal, and I navigate away
- **THEN** it is dismissed through its own close path, so that arrangement is flushed exactly as it would be on Escape

#### Scenario: Stacked overlays all close

- **WHEN** two overlays are open, one over the other, and I navigate away
- **THEN** both close

#### Scenario: The new page is not left frozen

- **WHEN** an overlay closes because I navigated away
- **THEN** the page I have arrived at scrolls normally, with no hold left over from the overlay

#### Scenario: A recorded overlay still closes on the way out

- **WHEN** I follow a link out of an overlay whose page records that it was open
- **THEN** that overlay closes and the page I arrive at is shown with nothing over it

#### Scenario: A restored overlay belongs to the page beneath it

- **WHEN** I go back to a page whose recorded overlay is opened again as part of its restore
- **THEN** the overlay is over the page it was opened on, and going forward again leaves nothing over the page I arrive at

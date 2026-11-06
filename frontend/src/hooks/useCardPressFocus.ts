import { useEffect } from 'react'

// Every card and row that is lit while hovered or while keyboard focus is
// inside it: one selector per kind, matching the CSS rules written as
// `:has(:focus-visible:not(select))`, plus the two ranking rows that are links
// themselves and use `:focus-visible`. Top anime's rank-1 card and the recap
// podium's reduced-motion rule share a surface with their base rule, so each
// appears once. SeriesEntryRow is not rendered anywhere today, but its rule
// is changed with the rest and it stays listed so it can't bring the problem
// back.
const HIGHLIGHTED_SURFACES = [
  '.anime-card',
  '.my-list-row',
  '.top-anime-row',
  '.top-anime-showcase__item',
  '.top-anime-card',
  '.series-timeline__card',
  '.series-extra-tile',
  '.series-entry-row',
  '.profile-list-row',
  '.recap-podium__card',
  '.recap-top-ten-row',
  '.recap-hot-take',
  '.recap-ranking-row__link',
  '.ranking-overlay__link',
  '.edit-history__row',
  '.unresolved-episodes__row',
  '.import-report-overlay__row',
].join(', ')

// Gives back the focus a mouse press put on a link or button inside a card or
// row. Chrome, Edge and Firefox focus a link or button when the mouse presses
// it; Safari does not. The highlight follows :focus-visible, so that focus no
// longer lights anything by itself, but a key pressed afterwards (Esc, Shift,
// Space, an arrow, Page Down) makes it keyboard focus and lights the card the
// mouse last pressed, and Space or Enter presses the button a second time.
// Blurring it makes those browsers behave like Safari. The next Tab still goes
// to the control after the pressed one.
//
// The blur comes after the press, from a timer, rather than preventing the
// focus with preventDefault() on mousedown: that would also stop native link
// drags, text selection and the blur an open episode count field gets when
// another card is pressed (design D1 of stop-currently-watching-selection).
// The timer runs after the browser's own focus step, which is the mousedown's
// default action after every listener, and before a held press is released;
// the click still follows. A microtask would run too early.
//
// Only links and buttons: a dropdown needs focus while its list is open, and
// a text field needs it to be typed in. A card or row missing from the list
// above keeps the CSS fix and loses only this guard.
export function useCardPressFocus(): void {
  useEffect(() => {
    function handleMouseDown(event: MouseEvent) {
      const target = event.target
      if (!(target instanceof Node)) return
      setTimeout(() => {
        const focused = document.activeElement
        if (
          focused instanceof HTMLElement &&
          focused.matches('a[href], button') &&
          focused.contains(target) &&
          focused.closest(HIGHLIGHTED_SURFACES) !== null &&
          !focused.matches(':focus-visible')
        ) {
          focused.blur()
        }
      }, 0)
    }
    window.addEventListener('mousedown', handleMouseDown, true)
    return () => window.removeEventListener('mousedown', handleMouseDown, true)
  }, [])
}

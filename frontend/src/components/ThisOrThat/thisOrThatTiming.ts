// How long the two steps of a This or That pick take (add-this-or-that-ranking
// design.md D11). The server has already stored the pick by the time either
// runs, so they only pace what is shown.

// The picked card stays marked this long before the next pair can show. Under
// reduced motion there is no hold: the next pair shows as soon as the answer is in.
export const PICK_MARK_MS = 180

// The card that changes fades in over this long. The stylesheet's 0.15s is the
// same figure.
export const CARD_FADE_MS = 150

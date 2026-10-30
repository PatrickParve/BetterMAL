// How long each step of a Higher or Lower round takes (add-games-higher-or-lower
// design.md D10). The server has already judged the pick and moved on by the time
// any of these runs, so they only pace what's shown: leaving mid-reveal loses
// nothing. A game that has ended has no hold: its last verdict stays up until I
// leave it.

// The hidden scores count up to their values.
export const REVEAL_MS = 450
// The verdict stays up after a correct pick, then the next round swaps in. A
// correct round moves on about 1.65 s after the click.
export const CORRECT_HOLD_MS = 1200
// The same after a pick that reaches a streak milestone, long enough to enjoy.
export const MILESTONE_HOLD_MS = 2400
// The cards change places.
export const SWAP_MS = 320

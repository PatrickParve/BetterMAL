## Context

The personal badge exists in two implementations that must agree by construction (add-series-browser design.md D5): `SeriesRankingIndex.ListedSeries`'s `ProgressBadge` helper (server, over a lightweight projection with no navigation properties) and `SeriesPage.tsx`'s client-side `completionBadge` (over the full `SeriesDto`, computed client-side so an in-place row edit updates it without a refetch). Both currently implement:

1. `Completed` — whole series finished, every finished-airing main-line entry Completed.
2. `Caught up` — every currently-airing main-line entry's watched count meets its broadcast count.
3. `N behind` — same as (2) except some currently-airing main-line entry's watched count falls short.
4. No badge — a finished-airing entry isn't Completed, or nothing has aired/is airing at all.

This change widens that precedence to six states and generalizes the "behind" figure. Both implementations move together; the two are tested against the same case set (as the prior change's tasks did).

## Goals / Non-Goals

**Goals:**
- Two new terminal states — `Dropped`, `Unwatched` — replacing what today is silence for a franchise I gave up on or never started.
- `N behind`/`Caught up` computed over the *whole* aired main line (finished and currently-airing together), not just currently-airing entries, so a partially-watched finished season is no longer invisible to the badge.
- A uniform rendered size for the status pill and the personal badge, and updated colours (`Completed` → the app's Completed-status blue; `Dropped` → the Dropped-status red; `Unwatched` → the Plan-to-watch purple).
- Filter buttons on the Series page over both the progress badge and the status pill, multi-select, applied client-side to the already-loaded list.

**Non-Goals:**
- Changing the *status pill's* own four-value precedence (Airing/Ongoing/Upcoming/Finished) — untouched.
- Changing sort behaviour or the list endpoint's response shape beyond the badge field's two new possible values.
- A combined "Behind or Dropped" filter shorthand, or saved filter presets — the four progress buttons and four status buttons are it.

## Decisions

### D1 — The full precedence, in order

Evaluated over a series' main-line entries, ordered by their `Order` (release/watch order — already projected on both the backend's `SeriesRankingMemberProjection` and the frontend's `SeriesEntryDto`):

1. **`Completed`** — every member of the whole series has finished airing (`status == "Finished"`), at least one main-line entry has finished airing, and every main-line entry that has finished airing is marked Completed. Unchanged from today.
2. **`Dropped`** — among main-line entries that have aired (finished or currently airing), at least one is marked Dropped, **and no aired main-line entry released after the most recent such drop has ever been watched** (watched-episode count of every later aired entry is zero). "Most recent" matters, not "any": if I dropped season 1 but later watched season 2, the drop no longer describes where I stand — see D2.
3. **`Caught up`** — I have watched at least one main-line episode, and total watched across the main line meets total broadcast across the aired main line.
4. **`N behind`** — I have watched at least one main-line episode, but fewer than have broadcast. `N` = total broadcast main-line episodes − total watched main-line episodes, summed across every aired main-line entry (not just currently-airing ones — this is the generalization the request asked for).
5. **`Unwatched`** — at least one main-line entry has aired, I have watched none of the main line, and (2) didn't already claim the case.
6. **No badge** — nothing in the main line has aired yet, or a currently-airing entry's broadcast count is unknown and (3)/(4) can't be resolved (see D4). (2) and (5) don't need broadcast-count data at all, so they're decided before this can block them.

An entry not in my list counts as zero episodes watched, same as today. An entry that hasn't aired at all never counts toward any figure here, same as today.

### D2 — Why "most recent drop, nothing watched after it" rather than "any drop" or "only the frontier"

Two simpler rules were on the table and rejected:

- **Any aired entry marked Dropped, full stop.** Rejected: dropping season 1 and then watching and completing seasons 2–4 would still show "Dropped", which is actively wrong — I'm caught up, not dropped.
- **Only the single latest-aired entry's own status matters.** Rejected: dropping season 2 (with season 3 not yet aired) and never touching anything since is exactly "dropped and didn't continue" even though season 2 isn't literally the newest thing that exists in the world — it's the newest thing that's *aired*, which is what "haven't continued" has to mean.

The adopted rule — the most recently *aired* drop, checked against whether anything aired after it has any watched progress at all — covers both: a drop with nothing after it (including the drop being the last aired entry) reads as Dropped; a drop followed by real continuation reads as whatever (3)/(4)/(5) say about the *whole* main line's watched-vs-aired figures, because continuing past a drop is a decision to keep going, and the badge should describe today's standing, not history.

A dropped entry itself may have partial watched episodes (I dropped it five episodes in) — that doesn't change the outcome. Dropping mid-entry and not continuing is still "dropped, didn't continue"; the badge doesn't second-guess by falling through to a behind-count instead.

### D3 — Where "Unwatched" sits relative to "Dropped" when both technically apply

A franchise dropped with literally zero episodes watched satisfies both "a drop with nothing after it" (D2) and "total watched is zero" (state 5). `Dropped` wins — it's checked first and is strictly more informative: it tells me not just that I haven't watched, but that I *decided* not to. `Unwatched` is reserved for a franchise I've simply never started.

### D4 — The unknown-broadcast-count guard now only blocks (3)/(4)

Today's single guard ("an unknown currently-airing broadcast count means no badge, not a guess") gated the *entire* computation, because the entire computation depended on that count. Now, (2) Dropped and (5) Unwatched depend only on watch status and watched-episode counts — never on how many episodes have actually broadcast — so they're resolved first and are never blocked by missing schedule data. The guard now only applies once the computation reaches (3)/(4): if any currently-airing main-line entry's broadcast count is unknown at that point, the result is no badge, exactly as today, just reached later in the sequence.

### D5 — Colours and sizing

`Completed` moves from the accent purple (`--accent`) to the app's existing Completed-status blue (`--status-completed`) — the same colour a Completed row already carries on My List and everywhere else "Completed" is drawn; the accent purple was a leftover from before the personal badge had a dedicated colour language. `Dropped` takes the existing Dropped-status red (`--status-dropped`); `Unwatched` takes the existing Plan-to-watch purple (`--status-plantowatch`) — both already-established tokens, not new colours. `Caught up` and `N behind` keep their current colours (the on-air green alias and the on-hold amber) unchanged.

The status pill and the personal badge both get a shared `min-width` plus centred text, so "Airing" and "Ongoing" (or "3 behind" and "Caught up") don't visibly differ in width purely because one label is shorter — applied identically on the series detail page's header-scale badges and the Series page card's smaller badges (each scale keeps its own min-width constant).

### D6 — Filters: two independent multi-select groups, client-side, no re-fetch

The whole eligible set is already loaded in one response (add-series-browser D1); filtering, like sorting, is a pure client-side operation over that array — no new endpoint, no new query parameters server-side.

Two groups, AND'd together; buttons within one group OR together:

- **Progress**: `Watched` (badge is `Completed` or `Caught up` — "everything from the main series that has aired has been watched", the request's own phrasing), `Behind`, `Dropped`, `Unwatched`.
- **Status**: `Airing`, `Ongoing`, `Upcoming`, `Finished` — the status pill's own four values.

No button selected in a group applies no filter for that group (matches the Type filter's existing convention on Season/Search/My List). Selections live in the URL (`?progress=watched,dropped&status=airing`, mirroring the existing `?sort=`), so a link carries them and back-navigation restores them, and switching a filter re-slices the already-sorted array — no new request, matching how sort already works. A "no badge" series matches none of the four progress buttons (rather than being force-fit into one), so with every progress button unselected it's shown, but selecting any progress filter hides it — same as any other value that doesn't match a selected filter.

Filtering runs before the revealed-count slice, same as sort does today: changing a filter re-applies from the top of the (now-shorter) filtered-and-sorted array, consistent with how changing sort already re-orders "cards not yet revealed" per the existing spec.

### D7 — `SeriesProgressBadge`'s wire values

Two new enum members, `Dropped` and `Unwatched`, added to the existing `"Completed" | "CaughtUp" | "Behind" | "None"` wire contract — `"Dropped" | "Unwatched"` joining it, serialized the same way (`JsonStringEnumConverter`, matching member name exactly). No `BehindEpisodes`-style payload field is needed for either new state.

## Risks / Trade-offs

- **Two hand-written implementations of one six-state precedence** — the same cost the original design accepted for four states, now for six. Mitigated the same way: one stated precedence in the spec, tests over the same case set on both sides.
- **A finished-airing entry with an unknown `TotalEpisodes`** (very rare — MAL almost always publishes a total once a show finishes) can't contribute a known aired count to (3)/(4)'s sum. Treated as blocking the computation the same way an unknown currently-airing count does (D4), rather than silently under-counting — accepted as an edge case that will essentially never fire in practice.
- **A dropped-then-resumed franchise's badge depends on the *whole* main line's watched-vs-aired figures**, not just "did I finish what came after the drop" — e.g. dropping season 1, then watching half of season 2, reads as "N behind" over the combined shortfall rather than distinguishing "resumed but not finished season 2" from "never touched season 1 either". Accepted: the badge is a single figure, not a per-entry breakdown; the series page's own entry rows already show the per-entry detail.

## Migration Plan

No data or schema change. Purely a computation and presentation change over existing stored fields (`Order`, `EntryStatus`, `EpisodesWatched`, airing status, per-member aired counts) already available to both implementations. Rollback is reverting the precedence function and CSS on both sides; nothing external depends on the badge's specific values beyond the two UIs that render it.

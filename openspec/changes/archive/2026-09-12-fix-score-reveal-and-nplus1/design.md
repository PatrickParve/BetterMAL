## Context

Three independent faults, sharing one theme: **state that outlives the thing it belongs
to**. A per-score reveal outlives its page; a filter's reported state outlives the
condition that made it honest; a page's data read is re-issued for a history entry that
changed nothing it depends on. The backend N+1 loops are the exception — plain
oversights, with the batch reads they need already written.

Current state, verified 2026-09-12:

- `ScoreValue` (`frontend/src/components/ScoreValue.tsx:21`) holds `revealed` in plain
  `useState` and documents the assumption that navigation unmounts it. React Router
  mounts one `AnimeDetailPage` for `/anime/:id`, so anime → anime keeps the instance —
  a problem `usePageData` and `useRestorableState` both already solve for their own
  state, by comparing an identity during render. `ScoreValue` is the **only** holder of
  reveal state in the app, so one fix covers all 13 call sites.
- `SeriesPage`'s More section starts with `mineOnly = true`
  (`pages/SeriesPage.tsx:475`) and computes
  `filterActive = mineOnly && unfilteredGroups.size === 0 && some group expanded`
  (`:820`). The third clause exists only to make a freshly opened page read "off" while
  the filter is in fact on — and it is what a type button trips, because
  `toggleMediaType` opens every group holding the selected type (`:831`).
- `MyListService:30` and `MainDashboardService:65,76,106` issue one query per entry.
  `MainDashboardService:25` already calls the bulk aired-count read; `:106` re-queries
  anyway. `AiringScheduleService` is the in-repo exemplar of the right shape: one range
  query over every my-list anime for a whole week.
- `usePageData`'s load effect depends on `[key, isRestore, snapshot]`, and `snapshot`
  changes identity on every history entry — so any `setSearchParams` re-runs the load
  even though `key`, which by the hook's own contract carries every parameter the data
  depends on, is unchanged.

Constraints: no API, DTO, or schema changes; every bulk read must answer exactly what
the per-anime read answers; 28 test doubles implement `IEpisodeScheduleService`; the
frontend has no test harness, so frontend correctness rests on the spec scenarios and
manual verification.

## Goals / Non-Goals

**Goals:**
- A revealed score is never visible on a page the user did not reveal it on.
- The series page's "in my list" control is off on arrival, and its lit state always
  means the filter is actually in force.
- `/api/my-list` and `/api/dashboard` issue a bounded number of database reads,
  independent of list size, with byte-identical responses.
- A page stops re-downloading its payload for a URL change its data does not depend on.

**Non-Goals:**
- No change to what a score is, when the always-show-completed setting applies, or how
  the navbar switch behaves. Only the lifetime of a manual reveal changes.
- No change to the More section's grouping, collapse model, per-group exemptions,
  heading behaviour, or expand/collapse-all control beyond what the filter default
  forces.
- `SeriesService.AiredEpisodesByAnimeIdAsync` loops per member, but over one franchise's
  currently-airing members — bounded and small. Left as is.
- No other `ISSUES.md` entry is addressed here.
- No caching layer, no response cache, no query-count assertions in production code.

## Decisions

### D1 — Reveal state is keyed to the pathname, not to component lifetime

`ScoreValue` compares `useLocation().pathname` against a rendered-identity state during
render and clears `revealed` when it differs — the same pattern `useRestorableState`
(`hooks/useRestorableState.ts:22`) and `usePageData` (`hooks/usePageData.ts:56`) already
use for exactly this hazard. Adjusting during render rather than in an effect means a
hidden score never paints revealed for one frame on the new page.

**Pathname, not `location.key`.** `location.key` changes on every history entry,
including a `setSearchParams` filter toggle — which would re-hide scores while the user
is still on the same page, reading as a glitch. Pathname changes precisely when the user
leaves a page, which is the wording the capability already uses. Consequence, accepted:
on `/season`, `/year`, `/top` and `/search`, whose subject lives in the query string, a
reveal survives a change of season or tab. Those are the same page by every other
measure (the browse grids do not even render a reveal control), and "I revealed this and
it stayed revealed while I filtered" is the behaviour a user expects.

**Rejected:** keying the routes by pathname in `AppShell` to force a remount. That would
remount `AnimeDetailPage` on every anime, discarding the restore-aware data seeding
`usePageData` exists to provide — a large regression to fix a small leak.

**Rejected:** a reveal-scope context provider keyed by pathname. It centralises the
reset, but no second component holds reveal state, so it buys nothing over the local
identity comparison and adds a provider to the tree.

### D2 — Turning the global toggle back on also drops every reveal

Folded into D1 by making the compared identity `${pathname}|${hidden}`. Without it,
revealing a score, switching scores on from the navbar, then switching them off again
leaves that one score visible — which contradicts the capability's "when hidden,
replaces **every** MAL score everywhere". Flipping `hidden` in either direction clears
the reveal; the off direction is invisible (everything is shown anyway), the on
direction is the fix. The always-show-completed setting needs no such handling: it
reveals without any `revealed` state, so turning it off re-hides on its own.

### D3 — The More section's filter starts off, and the control reports the truth

Two coupled edits, because neither works alone:

- `mineOnly` defaults to `false`.
- `filterActive` drops its third clause: `mineOnly && unfilteredGroups.size === 0`.

Flipping the default alone would leave the lie in place and the bug one step deeper:
press "in my list" (on), press again (today: collapse all, `mineOnly` still true), press
a type button — groups open, the third clause is satisfied again, and the control lights
up untouched. Removing the clause alone would make a freshly opened page read "on" with
the filter genuinely on and nothing visible. Together they give the state the user
described: everything off, everything collapsed, and a type button that shows all
entries of that type.

### D4 — The control becomes a two-way toggle

Once the control reports the truth, "press again to collapse every group with the filter
still on" (today's `toggleMineOnly` active branch) would leave it reading **on** over an
empty section, and a third press would do nothing at all — a dead control. So the active
branch now turns the filter **off**: `mineOnly = false`, no collapse state touched, so
every expanded group widens to all its extras. Pressing it while off is unchanged — turn
the filter on, drop exemptions, open every group holding an admitted extra of mine.

This restores the model the heading requirement already describes ("Activating it while
it reads as on SHALL show every extra of every expanded group") and retires the
"show it / put it away" pairing, which only made sense while off was unreachable.
"Collapse all" remains the way to put the section away.

### D5 — While the filter is on, a type nothing of mine carries is unavailable

From the user's rule: *select Music, then "in my list", with no music of mine → drop the
music filter and show my entries*. Implemented as one invariant rather than a one-shot
fallback, so it holds in both orders:

- `myMediaTypes` = the types carried by extras that are in my list.
- A type button is rendered `disabled` while `mineOnly` is on and the type is not in
  `myMediaTypes`. It cannot be selected, and it states why through its accessible name.
- Turning the filter on narrows `selectedMediaTypes` to `myMediaTypes`. If that empties
  the selection, no type restriction applies and the section shows my extras across
  every group — exactly "only show entries in my list".

**Rejected:** letting the empty combination render, as today, which gives a column of
headings with zero tiles and no explanation. **Rejected:** auto-turning the filter off
instead of dropping the type — the user pressed "in my list" last, so that is the
intent that should survive. A dropped selection stays dropped when the filter is later
turned off; re-pressing the type is one click and guessing would be worse.

### D6 — Bulk reads live on `IEpisodeScheduleService`, as default interface members

Two reads are added beside the three bulk overloads already there:

```
Task<Dictionary<int, ResolvedEpisode>> ResolveOnLocalDateAsync(IReadOnlyCollection<AnimeMetadata>, DateOnly, ct)
Task<Dictionary<int, DateTimeOffset>> NextAiringInstantAsync(IReadOnlyCollection<AnimeMetadata>, DateTimeOffset, ct)
```

`EpisodeScheduleService` implements the first with one `GetRowsInRangeAsync` over every
id for the local day — already multi-id, already ordered by `AirsAtUtc`, so the earliest
row per anime is the same row the per-anime read takes — and the second with a new
`GetNextAiringInstantsAsync` on the repository (`GroupBy(AnimeId).Min(AirsAtUtc)`),
mirroring `GetMaxAiredEpisodesAsync` exactly, empty-input guard included.

Both are declared as **default interface members** delegating to the per-anime member in
a loop. 28 test doubles implement this interface; the point is not saving 28 edits but
correctness — a hand-written stub returning an empty dictionary for the bulk read while
its per-anime member returns a value would silently change what dashboard tests assert.
Delegating by default makes every double consistent with itself for free. The real
implementation overrides both, and the doc comments say that any implementation reading
a database must.

**Rejected:** injecting `IEpisodeAiringRepository` into `MainDashboardService`. It would
bypass the service that owns local-time conversion and the "no estimation" rule, and put
schedule reading in two places.

### D7 — `usePageData` reloads on a history change only when it must

The effect gains a guard: if the resource `key` is unchanged since the last completed
load, data is already in hand, and this is not a restore, then write that data into the
new history entry's snapshot and return without loading. A `loadedKeyRef` records which
key the current data belongs to.

The hook's contract already makes this safe: "`key` … must include any route/query
parameter the data depends on". An unchanged key therefore *means* the data does not
depend on what changed, so the reload was redundant by definition. Restores are left
alone — their silent background refresh is documented behaviour and costs no loading
flash. Writing the data into the new snapshot keeps a later back/forward restore of that
entry seeded.

**Rejected:** a silent background refresh instead of skipping. It removes the loading
flash but keeps the 305 KB and its round trips, which is the cost being removed. Pages
already call `reload()` explicitly after a mutation that needs fresh server state.

## Risks / Trade-offs

- **[A reveal survives a query-string change on `/season`, `/year`, `/top`, `/search`]**
  → Accepted, per D1: the user has not left the page. The two browse grids render no
  reveal control at all, so this reduces to the Top anime tabs.
- **[Series-page users accustomed to arriving at a filtered More section]** → This is
  the requested change; the section now opens as headings alone and the filter is one
  press away. The change is visible immediately, not silent.
- **[Retiring "press again to collapse everything"]** → "Collapse all" already does
  exactly that and is right beside it. D4 explains why keeping it would strand the
  control.
- **[A future `IEpisodeScheduleService` implementation inherits the looping default]** →
  The only production implementation overrides both members; the doc comments state the
  requirement; the spec pins "one database read" on the capability, so a regression is a
  spec violation, not a style question.
- **[A 613-id `IN` clause]** → Npgsql sends it as a single array parameter, and
  `GetMaxAiredEpisodesAsync` and `AiringScheduleService.GetWeekAsync` already pass the
  whole list this way today.
- **[Skipping a reload hides a server-side change that happened between two URL
  states]** → Only for data whose key did not change, i.e. data the URL change does not
  describe. Mutations already patch or `reload()` explicitly, and a real revisit — new
  key, remount, or reload — still fetches.
- **[No frontend test harness]** → Three of the four fixes are frontend. Each spec
  scenario is written to be checkable by hand, and the tasks name the exact walk-through
  for each.

## Migration Plan

None. No schema, API, or stored-state change. The client-side changes take effect on the
next page load, and nothing persisted needs clearing: `pageStateStore` is an in-memory
`Map` that dies with the page load, so no pre-change series-page snapshot can outlive
the deploy, and the only `localStorage` keys involved belong to the global score toggles,
which this change does not touch. Rollback is a revert; the bulk reads are additive, so
reverting the call sites alone is also safe.

## Open Questions

None.

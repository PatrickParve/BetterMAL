## Context

Five independent behaviours, four of them small. What they share is that each already has a mechanism in place that stops one step short.

**The More section** (`pages/SeriesPage.tsx`) already holds all three pieces of state the new behaviour needs: `mineOnly` (the "in my list" filter, on at mount), `collapsedGroups` (per-group collapse, all expanded at mount), and `unfilteredGroups` (the set of groups currently exempt from the filter, populated only by the `+N more` control). What is missing is that the group heading only writes `collapsedGroups`, so on a group whose tiles the filter is hiding it toggles between "no tiles" and "no tiles".

**Resume-to-Watching.** `UserAnimeEntryEditService.ApplyEpisodesWatched` already reacts to an episode-count edit with two automatic status moves: reaching everything available marks the entry Completed, and a Completed entry falling below it returns to Rewatching or Watching. Nothing happens in between — the case where the count rises but does not reach the end.

**Overlay scroll.** Every overlay in the app — entry editor, completion-score prompt, ranking, recap picker, edit history, score board, top-anime selection, unresolved episodes, related anime — renders through one component, `components/Modal.tsx`, which owns Escape and click-outside and nothing else. The page behind it is the document itself: `#root` is a normal flow container, `window` is the scroller, and `hooks/useScrollRestoration.ts` snapshots `window.scrollY` on every scroll event to restore it on a back navigation.

**The two score boxes** (`pages/AnimeDetailPage.css`) sit in a plain flex row, each `flex: 0 0 auto` and therefore each as wide as its own text. My box carries one to three lines depending on whether a rewatch count and a finish date exist, so how badly the pair mismatches varies per anime.

**My-list rows.** `ProgressBar` gained an optional `aired` prop (the blue broadcast fill drawn behind the purple watched fill) for the anime detail page and the dashboard's currently-watching cards. `MyListRow` renders the same component and already reads `item.airingStatus` and `item.episodesAired` — for the aired-episode gate and for the increment ceiling — but passes neither to the bar.

## Goals / Non-Goals

**Goals:**

- The More section's group heading is the control that opens a group, and the section's controls always report the state they are actually in.
- One rule for resume-to-Watching, enforced by the API, and never applied behind the user's back where a status control is on screen.
- The scroll lock is written once, in `Modal`, and every overlay in the app gets it without knowing about it.
- The scroll lock leaves the scroll-restoration machinery alone: the page keeps its scroll position and its snapshot, and no page-level scroll event is produced by locking or unlocking.
- The two score boxes are equalised in CSS, from content, with no measurement in JavaScript.

**Non-Goals:**

- Changing what the "in my list" filter *filters* (membership, not status — unchanged), or the initial state of the More section (filter on, groups expanded — unchanged).
- Changing the completion rules, the completion-score prompt, or the aired-episode gate. The resume rule fires only where an edit does *not* reach everything available; everything that already fires at the boundary keeps firing there.
- Reordering, regrouping, or re-fetching anything when an entry resumes. It is a status change like any other: logged, synced, applied in place.
- Broadcast progress on any surface that does not have it yet beyond my-list rows — the series page's per-entry rows and More tiles are out of scope.
- Making the overlay's own content behave differently. Overlays already scroll internally at `max-height: 85vh`; that is untouched.

## Decisions

### D1: A More group's heading toggles between "showing everything" and "collapsed"

The heading is given one job — *open this group* — with collapse as its off state:

```
showsAll(group) = !collapsed(group) && (filterOff || unfiltered(group))

heading click:
  showsAll  ->  collapse the group
  otherwise ->  expand it AND exempt it from the filter
```

Three consequences worth naming:

- A group with nothing of mine in it opens on the first click, which is the case that reads as broken today.
- A group *with* something of mine (filtered, showing some tiles) also opens in full on a heading click rather than collapsing. That is the price of a two-state toggle, and it is the right price: such a group already has a `+N more` link, so the granular action is still one click away, while the heading keeps meaning the same thing on every group.
- With the filter off, every group is already unfiltered, so the heading is a plain expand/collapse toggle exactly as it is today. The exemption is only recorded while the filter is on, so the set never fills with entries that mean nothing.

*Alternative considered:* a three-state cycle on the heading — filtered → full → collapsed. Rejected: a control whose meaning changes with each press is worse than one that sometimes reveals more than asked, and it would leave the heading's `aria-expanded` with no honest value to report.

*Alternative considered:* leave the heading alone and make the `+N more` link the opener on every group, including empty ones (today's behaviour, with the link kept). Rejected — this is what the user asked to change, and a hidden-count link on a group showing nothing is a strange primary control.

### D2: The filter control reports the state the section is actually in

With per-group exemptions, "is the filter on?" has two possible answers: the stored intent, and whether it is in force everywhere. The control shows the second:

```
filterActive = mineOnly && no group is exempt
```

Pressing it while it reads active turns the filter off (show everything) — today's behaviour. Pressing it while it reads inactive turns the filter *on*, clears every exemption, and expands every group, so one press always returns the section to the state a freshly opened series page is in. That is the same reset the control already performs in both directions today; only the direction is now derived from `filterActive` rather than from `mineOnly` alone.

This is what makes D1 legible: opening one group in full visibly switches the filter control off, so the section never claims to be filtered while showing a group in full.

*Alternative considered:* a third, indeterminate visual state on the control. Rejected as more vocabulary than a two-state button needs, when "not fully filtered" is honestly reported by "off".

### D3: The hidden-count control belongs to a group that is showing something

`+N more` appears only where a group is expanded, shows at least one tile, and is hiding the rest behind the filter. A group showing no tiles — nothing of mine, or collapsed — offers none: its heading opens it (D1) and its entry count is already in the heading, so nothing is lost but the link.

This drops the control's collapsed-group branch entirely. It exists today only because a collapsed group also "shows fewer tiles than its entry count"; with the heading owning collapse, the control's whole job is "see past the filter in this one group", which is `unfilteredGroups.add(key)` and nothing else.

### D4: Resume-to-Watching is a third arm of the same automatic-status block

It goes where the other two automatic status moves already are, in `ApplyEpisodesWatched`, and fires when:

- the new count is **higher** than the stored one (a correction downward on a Dropped entry is not a resumption), and
- the new count does **not** reach the completion target — or there is no known target at all, in which case nothing can be reached and the raise is a resumption by default, and
- the stored status is **not** Watching, Rewatching, or Completed.

Completed is excluded because it is already owned by the existing arm: `list-editing`'s strict Completed rule sends a Completed entry whose progress no longer covers everything available to Rewatching or Watching by its own reasoning, whichever direction the count moved. Adding Completed here would contradict it. Watching and Rewatching are excluded because there is nothing to resume — a rewatch in progress stays a rewatch in progress.

The unknown-target case is why this arm cannot live inside the existing `completionTarget is { } target` guard: an entry raised from Plan to watch on a show with no published total should still land in Watching, and today that guard would skip it.

*Alternative considered:* also resuming on a count *drop* (any edit at all files the entry as Watching). Rejected: lowering a Dropped entry's count from 5 to 3 is a correction to a record of something abandoned, not a statement about watching it now.

### D5: An explicit status in the request suppresses the resume rule, and the editor states one

The existing arms are skipped when the request carries a *different* status, which is enough for them — an explicit change wins over an automatic one. The resume rule needs a finer distinction, because the status a user might want to defend against it is the one already stored: raising a Dropped entry's count while deliberately leaving it Dropped.

So `UserAnimeEntryEditRequest` gains a `HasStatus` flag, set by the `Status` property's setter exactly as `HasStartedAt`/`HasCompletedAt` already are — `System.Text.Json` calls a setter only for keys actually present in the body, so the flag distinguishes "sent, and equal to the stored value" from "not sent at all". The resume rule is skipped whenever `HasStatus` is true. The two existing arms keep their current guard untouched, so nothing about auto-completion changes.

The client side of that contract is the entry editor:

- Its status dropdown **follows the count as it is typed**: raising the count past the entry's stored value, under exactly the conditions in D4, moves the dropdown to Watching. The user sees the change before saving, and can pick something else — including the original status — instead.
- It **sends `status` explicitly whenever the count is raised**, whatever the dropdown ends up showing. That is the only way re-picking the original status can survive the rule, and it costs one condition on a request field the editor already builds.

Which means: from the editor, the *dropdown* is what decides the saved status, and the server rule never fires there. From the "+" control and the in-place count field — neither of which shows a status control at all — the server rule is what decides, and it fires. Both paths agree on the outcome; they differ only in which side states it.

*Alternative considered:* fire the rule from the editor too and forbid the same-status override. Rejected: a dropdown reading Dropped that saves as Watching is precisely the silent behaviour this decision exists to avoid.

*Alternative considered:* keep the server rule keyed on "status differs from stored" and let the editor mirror the rule locally without sending anything. Rejected — the two would drift the moment the editor's idea of the completion target differed from the server's, and the override would still be unexpressible.

### D6: The scroll lock is a counted `overflow: hidden` on the body, not a fixed-position body

`Modal` acquires a lock on mount and releases it on unmount, through a shared counter so that stacked overlays (an editor opening over a score board, a completion prompt following an increment) do not let the first unmount unlock the page. The first acquisition records `document.body.style.overflow` and sets it to `hidden`; the last release restores what was recorded.

`overflow: hidden` on the body rather than `position: fixed` is the load-bearing part. The body's overflow propagates to the viewport (the root element sets none), so the document stops scrolling by wheel, trackpad, and keyboard alike — but `window.scrollY` is preserved, no scroll event fires, and `useScrollRestoration`'s snapshot is untouched. A fixed-position body would scroll the document to 0, fire a scroll event, and write that 0 into the current history entry's snapshot, so navigating back to the page after closing an overlay would land at the top.

Hiding the scrollbar reclaims its width, which would shift the page sideways under the overlay. The lock compensates by adding `window.innerWidth - document.documentElement.clientWidth` as body padding-right while held — zero on macOS's overlay scrollbars, a real number on Windows and Linux — and removing it on release. The alternative, `scrollbar-gutter: stable` on the root, would reserve that gutter permanently on every page for the benefit of a transient state.

*Alternative considered:* a wheel/touch/key event listener that calls `preventDefault`. Rejected: it has to enumerate every way a page can scroll, it fights the overlay's own internal scrolling, and CSS already expresses this exactly.

### D7: The two score boxes are equalised by a fit-content grid, not by measurement

The row becomes a grid: `grid-auto-flow: column`, `grid-auto-columns: 1fr`, `width: fit-content`. Sizing a grid under a max-content constraint gives every `1fr` track the largest track's max-content contribution, so both boxes come out at the width of whichever box needs more — which is exactly "the same size when all are at their max width" — while `fit-content` keeps the pair content-sized rather than stretching across the column, as `anime-detail`'s existing layout requirement demands. Heights already match: both boxes stretch to the row's height today and continue to.

With only my box absent (no score set), the grid has one track and renders exactly as it does now. The existing narrow-viewport rule that stacks the boxes becomes `grid-auto-flow: row`, which stacks them at one shared width.

*Alternative considered:* `flex: 1 1 0` on both boxes. It reaches the same equal widths through flexbox's intrinsic sizing, but reads as "share the row", which is the opposite of what the boxes must do here; the grid states the intent.

*Alternative considered:* measuring both boxes and setting a width in JavaScript. Rejected outright — a resize/font-load dependency for a two-box row.

### D8: Broadcast progress on a my-list row is the existing prop, keyed the same way

`MyListRow` passes `aired={item.airingStatus === 'currently_airing' ? item.episodesAired : null}` to the `ProgressBar` it already renders — the same expression the anime detail page uses, and the same "only while currently airing" rule `main-dashboard` states for the currently-watching cards. Nothing else about the row changes: same single bar, same `watched/total` label, same in-place count and "+" control, same row height. No new payload field is needed; both values are already on `MyListItemDto`.

## Risks / Trade-offs

**A heading click on a group that has tiles of mine opens more than the user wanted.** → The `+N more` link still does the narrow thing, the filter control re-filters everything in one press (D2), and a heading that behaves identically on every group is easier to learn than one whose effect depends on what is in the group.

**The editor's completion target can differ from the server's** — a stale aired count would have the dropdown flip to Watching where the server would have auto-completed, and since the editor now sends its status explicitly, Watching is what gets saved. → The same staleness already governs whether the editor offers Completed at all, the entry is one edit away from correct, and the next read re-resolves the aired count.

**Resume-to-Watching moves an entry the user may have deliberately filed elsewhere** (an On hold show they were sampling). → Explicitly requested behaviour, visible in the editor before saving (D5), logged as activity like every other status change, and reversible from the same dropdown.

**The scroll lock leaks if a `Modal` unmounts abnormally.** → The counter is decremented from the effect's cleanup, which React runs on unmount including when a parent throws; the last release restores the exact value recorded at acquisition rather than clearing the property blind, so a page that set its own body overflow is left as it was.

**Equal-width score boxes leave whitespace in the narrower box.** → Already true vertically (the boxes stretch to a shared height today), and the pair reading as one matched figure block is the point.

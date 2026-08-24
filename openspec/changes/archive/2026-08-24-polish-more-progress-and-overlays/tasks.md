## 1. Backend: resume-to-Watching

- [x] 1.1 Add a `HasStatus` flag to `Services/Entries/UserAnimeEntryEditRequest.cs`, set from the `Status` property's setter and `[JsonIgnore]`d, exactly mirroring the existing `HasStartedAt`/`HasCompletedAt` pattern — so "sent, and equal to the stored status" is distinguishable from "not sent" (design D5). Extend the class comment to say the flag exists for the resume rule.
- [x] 1.2 Add the resume arm to `ApplyEpisodesWatched` in `Services/Entries/UserAnimeEntryEditService.cs`, alongside the existing auto-complete and count-drop arms but **outside** the `completionTarget is { } target` guard, so an unknown target still resumes (design D4). It fires when: `newEpisodes > previousEpisodesWatched`, the new count does not equal the completion target (or there is no known target), `originalStatus` is not Watching, Rewatching, or Completed, and `request.HasStatus` is false. Set `entry.Status = WatchStatus.Watching` and add a `StatusChanged` activity entry in the same `$"{originalStatus} -> {entry.Status}"` form the neighbouring arm uses.
- [x] 1.3 Confirm by reading that the two existing arms keep their current guard (`request.Status is null || request.Status == originalStatus`) untouched, so auto-completion still fires for an editor save that resends an unchanged status, and that a newly created entry raised straight past 0 ends up logged as `Added as Watching` rather than as Plan to watch.

## 2. Backend tests

- [x] 2.1 Add `Services/Entries/UserAnimeEntryEditServiceResumeTests.cs` covering the resumptions: Plan to watch, On hold, and Dropped entries each raised short of the completion target become Watching, with the raised count kept and a status-change activity row written.
- [x] 2.2 Cover the exclusions: a raise that reaches the completion target completes instead of resuming; a lowered or unchanged count leaves the status alone; a Watching entry stays Watching; a Rewatching entry stays Rewatching; a Completed entry follows the existing strict-Completed rule rather than this one.
- [x] 2.3 Cover the unknown-target case: a Dropped entry on an anime with no known total and no known aired-so-far count is resumed by a raise.
- [x] 2.4 Cover `HasStatus`: a request carrying `status` equal to the stored status alongside a raised count leaves the status alone; a request carrying a different status saves that status; a request with no `status` key resumes.
- [x] 2.5 Confirm the existing entry-edit test suites still pass — in particular `UserAnimeEntryEditServiceRewatchingTests` and `UserAnimeEntryEditServiceAiringGateTests`, whose Plan-to-watch and Dropped fixtures may now also cross the resume arm.

## 3. Frontend: the entry editor's half of the resume rule

- [x] 3.1 `components/EntryEditorOverlay.tsx`: compute the same completion target the backend uses — `episodesAired` when `airingStatus === 'currently_airing'`, otherwise `totalEpisodes` — and, while the episode-count field holds a value above `initialEpisodesWatched` that does not reach that target and `initialStatus` is not Watching, Rewatching, or Completed, move the status select to Watching as the count is typed (design D5). A later manual selection by the user SHALL stand rather than being overwritten on the next keystroke.
- [x] 3.2 Send `status` in the edit request whenever the status differs from `initialStatus` **or** the episode count is being raised, so a user who sets the select back to the original status suppresses the server rule.
- [x] 3.3 Check the count-lowering and count-unchanged paths still send no `status` when nothing else changed, so an ordinary date- or score-only edit is unchanged.

## 4. Frontend: the overlay scroll lock

- [x] 4.1 Add a `useScrollLock()` hook (`hooks/useScrollLock.ts`) implementing design D6: a module-level counter; on the first acquisition record `document.body.style.overflow` and `paddingRight`, set `overflow: hidden` and a `padding-right` of `window.innerWidth - document.documentElement.clientWidth`; on the last release restore exactly what was recorded. Comment why it is `overflow: hidden` and not `position: fixed` (it preserves `window.scrollY` and fires no scroll event, leaving `useScrollRestoration`'s snapshot alone).
- [x] 4.2 Call it from `components/Modal.tsx`, so every overlay in the app inherits the lock without changes of its own.
- [x] 4.3 Verify the stacked case by reading the mount paths: `CompletionScoreOverlay` opening while another overlay is mounted must leave the page locked until the last unmount.

## 5. Frontend: the series page's More section

- [x] 5.1 `pages/SeriesPage.tsx`: derive `showsAll` per group (`!isCollapsed && (!mineOnly || unfilteredGroups.has(key))`) and rewrite the heading handler per design D1 — `showsAll` collapses the group; anything else expands it and, while the filter is on, adds it to `unfilteredGroups`.
- [x] 5.2 Derive `filterActive = mineOnly && unfilteredGroups.size === 0` and drive the "In my list" button's active class and `aria-pressed` from it (design D2).
- [x] 5.3 Rework `toggleMineOnly` to key off `filterActive` rather than `mineOnly`: active → filter off; inactive → filter on, exemptions cleared, every group expanded. Keep the existing reset of `collapsedGroups`/`unfilteredGroups` in both directions.
- [x] 5.4 Show the `+N more` control only when a group is expanded, shows at least one tile, and is hiding the rest (design D3); simplify `revealGroupTiles` to the unfilter case alone, since a collapsed group no longer offers the control.
- [x] 5.5 Re-check `nothingHidden` and the Expand/Collapse-all control against the new states: a section with one group open in full and others filtered must read "Expand all", and activating it must still show everything with the filter off.
- [x] 5.6 Check the group heading's `aria-expanded` still reports collapse state honestly under the new toggle, and that a group opened from its heading and then collapsed re-opens in full rather than filtered.

## 6. Frontend: the two score boxes

- [x] 6.1 `pages/AnimeDetailPage.css`: turn `.anime-detail-page__score-boxes` into a fit-content grid — `display: grid; grid-auto-flow: column; grid-auto-columns: 1fr; width: fit-content` — keeping the existing gap, so both boxes take the wider box's content width (design D7). Comment why `1fr` under `fit-content` equalises rather than stretches.
- [x] 6.2 Update the narrow-viewport rule that currently sets `flex-direction: column` to `grid-auto-flow: row`, so the stacked pair keeps one shared width.
- [x] 6.3 Confirm the single-box case (no score of mine) and the existing per-box padding override (`.anime-detail-page__score-boxes .detail-box`) still render as they do today.

## 7. Frontend: broadcast progress on my-list rows

- [x] 7.1 `components/MyListRow.tsx`: pass `aired={item.airingStatus === 'currently_airing' ? item.episodesAired : null}` to the row's `ProgressBar`, mirroring the anime detail page (design D8). No CSS or payload change should be needed — confirm that by reading `ProgressBar.css`'s `--aired` fill rule and `MyListItemDto`.
- [x] 7.2 Check the row's height and column alignment are unchanged with the aired fill present, since the fill sits inside the existing track.

## 8. Verification

- [x] 8.1 Build the frontend (`nvm use 22 && npm run build` in `frontend/`) and run `npm run lint`.
- [x] 8.2 Build and test the backend via the `sdk:10.0` Docker image (the local SDK is 9.0 — see the project's build note).
- [x] 8.3 Walk the More section in the running app: on a series whose OVA group holds nothing of mine, its heading opens all of it, the "In my list" button flips to off, other groups stay filtered, no `+N more` shows on the OVA group, and pressing "In my list" restores the opening state.
- [x] 8.4 Walk the resume rule: "+" on a Plan to watch entry and an in-place count edit on a Dropped entry both land in Watching; raising the count in the editor moves its status select to Watching; setting the select back to Dropped and saving keeps it Dropped; reaching the last available episode still completes.
- [x] 8.5 Walk the scroll lock: open the editor, the ranking overlay, and the recap score board in turn and confirm the page behind does not move by wheel or keyboard, does not shift sideways, and is back where it was after closing — including after navigating away and back.
- [x] 8.6 Walk the detail page's score boxes on an anime with a finish date and rewatch count, one with my score alone, and one with no score of mine; and check a my-list row for a currently airing show draws the blue aired fill and still increments in place.
- [x] 8.7 Run `openspec validate polish-more-progress-and-overlays --strict`.

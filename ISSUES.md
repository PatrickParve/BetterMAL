# Code & Logic Issues — full-project review (2026-07-19)

Findings from reading the entire backend, frontend, and config. Graded
**Bug** (wrong behavior today), **Risk** (correct today, breaks under a
realistic condition), and **Note** (rough edge / debt). Security-specific
findings live in [SECURITY_REVIEW.md](SECURITY_REVIEW.md); spec mismatches in
[SPEC_CONFORMANCE.md](SPEC_CONFORMANCE.md).

Context: the 2026-07-12 `REVIEW-NOTES.md` review's items were all verified as
**fixed** in the current code (episode upper bound, accept-diff PendingSync
re-check, cleared dates propagating, auto-fill episodes on completion,
partial-update editor, preserved finish dates, import entry backfill,
loopback port binding, callback XSS encoding, dead `DeleteMyListStatusAsync`
removed, all listed DRY extractions done). Nothing below repeats those.

---

## Bugs

### 1. Clearing a score from the My List inline dropdown silently does nothing
`frontend/src/pages/MyListPage.tsx` (`changeScore`) sends
`{ myScore: null }` when "—" is selected. The backend treats a `null`
`MyScore` as "field not provided" (`ApplyScore`: `request.MyScore is not { }
newScore → return`), so the score is never cleared — and because the UI
re-renders from the (unchanged) response, the dropdown snaps back to the old
score. The convention everywhere else is **send `0` to clear** (MAL's own
convention; `EntryEditorOverlay` does this correctly).
Fix: `updateEntry(item.animeId, { myScore: score })` (send the raw 0).

### 2. `POST /api/anime/{id}/refresh` returns 500, not 404, for unknown ids
`MetadataRefreshController.RefreshOne` catches
`AnimeMetadataNotFoundException`, but `MetadataRefreshService.RefreshOneAsync`
can never throw it — it fetches from MAL *first*, so an invalid id surfaces
as `HttpRequestException` (MAL 404) → unhandled → 500. The catch block is
dead code. (`AnimeDetailService` is the only thrower of that exception.)
Fix: catch `HttpRequestException` with `StatusCode == NotFound` → 404, or
translate in the service.

### 3. AiringPage date math mixes UTC and local — wrong week around midnight and broken Monday navigation
`frontend/src/pages/AiringPage.tsx`:
- `todayIso()` = `new Date().toISOString().slice(0,10)` — the **UTC** date.
  In Helsinki (UTC+2/+3) between 00:00 and 02:00/03:00 local, this is
  *yesterday*, so opening the page early Monday shows **last week** (the
  backend would have defaulted correctly; the frontend always passes an
  explicit date).
- `addDaysIso()` builds a **local**-midnight `Date`, then converts back via
  `toISOString()` — which lands on the *previous* UTC day for any UTC+
  timezone. Net effect: week navigation moves 6 days, not 7. From a
  Monday-dated reference, "next week" yields the Sunday of the *same*
  displayed week — the button appears to do nothing on the first click.
Fix: do all of it in local calendar terms (format with
`getFullYear/getMonth/getDate`, never `toISOString`) — or let the backend
own "today" by omitting the param until the user navigates.

### 4. Debounce/push race can clear `PendingSync` without pushing the newest edit
`DebouncedEntrySyncScheduler` + `EntryPushService`: if an edit lands while
the 8 s timer's callback is between "read entry" and "SaveChanges", the
sequence *push reads state A → user saves edit B (PendingSync=true) → push
saves PendingSync=false* loses B's pending flag while B's values were never
sent. Related: `ScheduleSync` can `Change()` a timer the fire callback is
about to dispose, discarding the new edit's debounce window. The 2-minute
retry sweep does **not** self-heal this (the flag is false); the edit reaches
MAL only on the next edit to that anime or via reconciliation review.
Low probability (needs an edit in a ~sub-second window) but it's the one
place the app can silently drop a change — the exact thing the design says
must never happen.
Fix direction: make the push conditional (e.g. compare a row version /
`xmin`, or re-check-and-keep `PendingSync` if the row changed since read).

### 5. Concurrent token refreshes can race and strand the stored token
`MalTokenProvider.GetValidAccessTokenAsync` has no lock: several bearer
requests arriving with <10 min of token life each trigger their own
`RefreshAsync`. MAL rotates refresh tokens, so the second exchange uses an
already-consumed refresh token → fails → returns null (surfaces as
authorization-required), and whichever exchange persists *last* may store the
stale pair. The 6-hour background refresher narrows the window but doesn't
close it (it races too).
Fix: a `SemaphoreSlim(1,1)` around the refresh path in the provider,
double-checking expiry after acquiring.

### 6. Weekly reconciliation may never run
`ReconciliationBackgroundService` does `Task.Delay(7 days)` **before** the
first run and re-arms from process start. Any container restart inside the
window resets the clock — on a machine that reboots (or rebuilds images) more
often than weekly, the scheduled reconciliation never fires. Nothing persists
"last ran at".
Fix: persist a last-run timestamp (a one-row table like the fetch logs) and
run on startup when overdue.

## Risks

### 7. Every MAL 403 is treated as throttling
`MalAuthPacingHandler` retries *any* 403 five times with exponential backoff
(~62 s total). MAL also returns 403 for genuinely forbidden requests
(invalid/revoked client id, restricted content). A real auth failure turns
into a slow, misdiagnosed retry storm per request, serialized behind the
1 req/s pacer.
Fix: inspect the body/`www-authenticate` where possible, or cap retries when
the token/client-id hasn't changed between attempts.

### 8. Unknown MAL list-status string kills an entire import/reconcile run
`MalMappingExtensions.ToWatchStatus` throws on an unrecognized status. The
model docs explicitly keep *other* MAL vocabulary as raw strings so "an
unrecognized upstream value doesn't break ingestion" — but this one enum
mapping is a hard throw inside loops that process the whole list
(`ReconciliationService.RunAsync` has no per-item catch; one bad edge aborts
the run before the diff is saved).
Fix: map unknown → `PlanToWatch` + warning log, or per-item try/catch in the
reconcile loop (ResyncService already does this).

### 9. Fixed-offset paging assumption in `GetAllPagesAsync`
Offset advances by a constant 100 regardless of how many items the page
actually returned. If MAL ever returns short pages with a `next` link
(observed on other endpoints of theirs), entries would be silently skipped —
in the import that means permanently missing list entries.
Fix: `offset += page.Data.Count` (equivalent today, robust tomorrow).

### 10. AniList schedule query caps at 50 episodes
`AniListClient.ScheduleQuery` requests `Page(perPage: 50)` and never pages.
A show with more than 50 entries in the 150-day lookback+future window (e.g.
a daily-airing long-runner) gets a truncated schedule whose coverage window
then *ends early*; dates beyond it silently fall back to the weekly estimate,
which for a non-weekly show is wrong.
Fix: iterate `Page(page: n)` until short page, or lower LookbackDays for
high-frequency shows.

### 11. `POST /api/sync/reconcile` and `/api/sync/now` are synchronous long requests
Reconcile pulls the entire MAL list (N/100 paced requests ≥ ~6 s for 600
entries — fine) but "sync now" pushes each pending entry at 1 req/s: 100
pending edits = a ~100 s HTTP request, at the mercy of proxy/browser
timeouts, with double-execution risk on retry. The corrective re-sync already
solved this pattern (trigger + background loop + polled progress).
Fix: reuse the trigger/progress pattern, or cap and report partial progress.

### 12. Detail-page "rich row" marker misfires for genre-less anime
`AnimeDetailService` decides a row needs its one-time full fetch via
`Genres is not { Count: > 0 }`. An anime MAL legitimately reports zero genres
for (some specials/music entries) re-triggers a live MAL fetch on **every**
detail-page visit — a permanent, quiet exception to "no live calls on
render". (Also: `CODE_GUIDE.md` still describes the old
`LastSyncedAt == default` marker.)
Fix: a dedicated `HasFullDetail`/`DetailSyncedAt` column set only by rich
fetches.

## Notes

13. **No way to remove an anime from the list — in either direction.** The
    app has no delete endpoint (deliberate, for MAL safety), but
    reconciliation also only iterates *remote* edges: an entry deleted on
    MAL's site is never flagged (no `Removed` diff type), so it lives on
    locally forever; conversely nothing in-app can remove an entry. Fine as
    a stance, but it's an undocumented gap — a "deleted on MAL" diff type
    that proposes local deletion for review would complete the loop.
14. **Import progress UI never shipped.** `GET /api/import/status` has zero
    frontend callers; during first import the app just looks sparse with no
    "142/380 synced" indicator (spec requires one — see SPEC_CONFORMANCE).
15. **Dev-port config disagrees with itself.** `launchSettings.json` says
    `dotnet run` listens on **5273**; the README says native dev listens on
    **5000** (and the Vite proxy targets `BACKEND_PORT`, default 5000);
    `.env.example` ships `BACKEND_PORT=5050` while the README documents 5000.
    Out-of-the-box native dev therefore proxies to a port nothing listens
    on. Pick one port story (add `"applicationUrl": http://localhost:5000`
    or document `--urls`).
16. **`ResyncProgressTracker` counts failures as "synced".** The loop
    increments `synced` in the finally-ish path even when the per-item fetch
    failed, so the Settings progress can read "595/595 processed" after a
    partially failed run with no visible signal (only logs).
17. **Dashboard "Days" stat ignores real episode durations.** The comment in
    `ProfileService` claims MAL's duration field "isn't fetched anywhere" —
    stale: `AverageEpisodeDurationSeconds` *is* cached by every full-detail
    fetch. The 24 min/ep assumption remains, and the comment misleads.
18. **`SeasonAnimeListing` rows are never pruned.** Once cached, an anime
    stays in a season's listing even if MAL later removes/reclassifies it
    (only *additions* are reconciled on refresh). Same for a bogus year
    (e.g. `/api/season/9999/winter`) creating a permanent empty fetch-log
    row and re-fetch attempt per visit until "cached".
19. **Type-ahead loads the full metadata table per keystroke.**
    `GetSearchIndexAsync` projects every `AnimeMetadata` row on each
    (debounced) request. Harmless at a personal-library scale (a few
    thousand rows after season browsing); an in-memory cached index or a DB
    `ILIKE` query is the upgrade path if it grows.
20. **Inline +1 / Add errors are invisible.** All the quick-action callers
    of `updateEntry` swallow errors with a bare `catch {}` — a 400 (e.g.
    over-total, cannot-complete) just makes the button do nothing. Only the
    editor overlay shows an error message. A tiny toast would fix the
    silent-failure UX.
21. **Carousel state can go stale after auto-completion.** Incrementing to
    the final episode auto-completes the entry server-side, but
    `HomePage.handleEpisodesWatchedChange` only patches the episode count —
    the card stays in "Currently watching" (at n/n with a disabled +) until
    the next dashboard fetch. Same class of staleness: `App.tsx` checks MAL
    auth once and never re-polls after the OAuth tab completes; the user
    must manually reload.
22. **`EpisodeScheduleCache` never evicts.** Shows that leave the tracked
    set keep their last schedule in memory for the process lifetime.
    Bounded by library size; worth a note, not a fix.
23. **`MalListStatus.IsRewatching` is fetched but never used** (requested in
    `UserAnimeListFields`, present on the update DTO, never set or stored).
    Dead weight — either drop it from the field list or model it.
24. **`ScoreValue` in the profile divergence lists doesn't pass
    `completed`,** so the "always show completed scores" setting doesn't
    apply there even though every rated anime in those lists is typically
    completed. (Also inherently: divergence-list *membership* leaks the
    relative MAL score even while hidden — logic quirk of combining that
    feature with score-hiding.)
25. **`MetadataRefreshBackgroundService` counts only successes against the
    daily cap** — a day of persistent MAL failures means unlimited retry
    calls (each pass retries up to 20, 144 passes/day ≈ 2 880 failed calls
    worst-case, all paced at 1 req/s). The cap should count *attempts*.

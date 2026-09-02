## Why

Three ways the app currently loses or distorts information it already has:

1. **A corrective re-sync destroys Rewatching.** Rewatching is a local-only status pushed to MAL as `watching`. Reconciliation was taught not to *diff* on that basis, but nothing stops it from *writing* Watching back: the diff it holds for review records MAL's `watching` verbatim, so accepting a diff raised by any other field (an episode count edited on MAL's own site, say) silently demotes the rewatch. The corrective re-sync has no carve-out at all and overwrites every rewatch on every run.

2. **"Caught up" is being recorded as "Completed", and completion is gated on a field that lies.** Watching the last aired episode of a currently-airing show marks the entry Completed — a workaround for keeping caught-up shows out of the home page's Currently watching carousel. It states something false on MAL, leaves the entry with no finish date, and needs a re-opening rule to undo itself every time an episode airs. Worse, every rule around Completed branches on MyAnimeList's `airing_status`, which routinely still reads `currently_airing` weeks after a show has ended: a run watched to its genuine final episode then cannot complete, cannot be completed by hand, cannot be rewatched, and never asks for a score. The app already holds a fact that does not lie — episodes watched is hard-capped at the aired-so-far count, so an entry that has reached its total has necessarily watched only aired episodes.

3. **An unknown episode count stays unknown when AniList knows it.** MAL publishes no total for some airing shows (*Tensei shitara Ken deshita II*), while AniList reports 12. The app already fetches that anime's AniList media record for airing data on the same request, and simply discards the figure — so the show reads `watched/?` forever, its progress bar has no end, and no episode-count-released update is ever raised.

## What Changes

**Rewatching survives every MAL read path**

- The rewatch carve-out moves from a comparison-only rule to a shared resolution rule applied wherever a MAL list status is read onto an existing local entry: a local **Rewatching** entry meeting a remote `watching` stays Rewatching.
- Applied at three points: the reconciliation diff is *recorded* as Rewatching (so the review screen shows the truth), accepting that diff re-checks the live entry, and the corrective re-sync applies the same rule instead of overwriting blindly.
- Every other remote status, and every other field, diffs and applies exactly as today.

**Completed means every episode has aired and been watched**

- **BREAKING** (behaviour, not API): the completion target is always the anime's **total episode count**, never its aired-so-far count. Reaching the last aired episode of a show with more to come no longer auto-completes, and explicitly choosing Completed is refused unless the whole run is out.
- Completion is *not* gated on MyAnimeList's `airing_status`, which is routinely stale. It is gated on a fact the app can check: every episode has aired. Since episodes watched is already hard-capped at the aired-so-far count, reaching the total is itself proof of that — so an entry completes on its final episode even while MyAnimeList still calls the show airing, and gets its finish date, score prompt and ranking offer as normal.
- Being caught up on a show that is *not* over is instead a *display* rule: a Watching entry whose episodes watched has reached the aired-so-far count is omitted from the home page's Currently watching carousel. It stays Watching everywhere else — My List, the detail page, MAL — and returns the moment a further episode airs.
- New mirror rule: a **Watching** entry that has already watched what turns out to be the full run is completed on the next read — the case where the total arrives *after* the viewing, which is exactly what the AniList fallback below now makes common.
- The re-opening rule stops keying on the aired count and becomes "a Completed entry re-opens while its anime is airing unless it has watched the published total", so a stale `currently_airing` can no longer un-complete a finished show, and the entries the old auto-completion wrote at a partial count self-correct on the next read. No migration pass.
- Rewatching eligibility moves onto the same "every episode has aired" test, so a stale airing status no longer blocks a rewatch of a show that is actually over.
- Removed as a consequence: the aired-so-far fill on an explicit completion, the no-finish-date completion branch, and the "cannot complete: aired count unknown" rejection.

**AniList fills a total episode count MAL does not have**

- The AniList media lookup and schedule fetch already made for every my-list anime additionally read AniList's own episode total. It costs no extra request.
- MAL remains authoritative: the effective total is MAL's figure when MAL has one, and AniList's only when MAL's is unknown. A later MAL figure supersedes the AniList one; a MAL refresh that still reports nothing no longer blanks the filled total.
- An episode count moving from unknown to known this way records an **episode-count-released** update on the home page, exactly as a MAL-sourced reveal does. AniList SHALL feed no other update kind — not premiere dates, not broadcast slots, not relations-as-news.

## Capabilities

### New Capabilities

None. Every change refines an existing capability.

### Modified Capabilities

- `mal-write-sync`: "Reconciliation does not demote a rewatch" widens from the comparison to every write path — the recorded diff, its acceptance, and the corrective re-sync.
- `list-editing`: the completion target becomes the total episode count in every case; completion is gated on every episode having aired rather than on MyAnimeList's airing status; a Watching entry that already covers the full run is completed on read; the re-opening rule is restated so a stale airing status cannot un-complete a finished show; rewatching eligibility uses the same every-episode-aired test; the airing branches of "Unknown total episodes cannot be completed" and of the Completed-fallback rule are withdrawn.
- `main-dashboard`: the Currently watching carousel omits an entry that has watched everything aired so far.
- `episode-airing-data`: AniList becomes the fallback source for an anime's total episode count when MyAnimeList reports none, amending "MyAnimeList SHALL remain the source for the anime's total episode count".
- `anime-updates`: an episode count that becomes known from AniList records an episode-count-released update; no other update kind may originate from AniList.
- `data-persistence`: the cached anime record stores MyAnimeList's own reported total and AniList's reported total alongside the effective total it already holds.

## Impact

**Backend**

- `Services/Mal/MalMappingExtensions.cs` — status resolution on apply; MAL total written to its own column and the effective total resolved from the pair.
- `Services/Sync/ReconciliationService.cs`, `Services/Sync/ResyncService.cs` — shared rewatch-preserving status resolution.
- `Services/Entries/AiredEpisodeGate.cs` — gains the shared "every episode has aired" predicate the completion and rewatching gates both read.
- `Services/Entries/UserAnimeEntryEditService.cs` — one completion target (the total) and one completion gate shared by the automatic and explicit paths; `CannotCompleteUnknownAiredCountException` retired, a new rejection added.
- `Services/Entries/RewatchingEligibility.cs` — the finished-airing test broadens to the shared predicate.
- `Services/Entries/CompletedEntryReopenService.cs` — restated condition, no longer reading aired counts; new complete-on-read counterpart alongside it, called from the same three read paths (`MainDashboardService`, `MyListService`, `AnimeDetailService`) and the airing schedule pass.
- `Services/Dashboard/MainDashboardService.cs` — carousel filter, using the aired counts it already resolves.
- `Services/Airing/AniList/AniListClient.cs` and `IAniListClient.cs` — `episodes` added to the existing lookup and schedule queries and to their result records.
- `Services/Airing/EpisodeScheduleRefreshService.cs` — persists AniList's total, resolves the effective total, records the episode-count-released update.
- `Models/AnimeMetadata.cs` + one EF migration — two nullable int columns, with the existing total backfilled as MyAnimeList's.

**Frontend**

- No new payload fields. Entries the old rule completed at a partial count now read as Watching in every status display. `EntryEditorOverlay` needs the completion target and the Completed/Rewatching availability tests brought in line with the backend's; the completion score prompt now fires for a final episode watched while MyAnimeList still reports the show as airing, which is the point.

**Not covered**

- No provenance is displayed: an AniList-sourced total renders as any other total.
- The AniList total is only ever fetched for my-list anime, since that is the only set the airing sync visits. A browsed season row with no MAL total keeps reading `?`.

## Why

The app has no way to say "I am watching this again". Rewatching is currently represented by leaving an entry Completed and resetting its episode count — a state the specs already describe (`profile-stats`: "a twelve-episode series with a rewatch count of two whose episodes watched currently reads one") and that `list-editing` already accommodates ("Rewatch increment on a completed entry ... no score prompt opens").

That representation is a compromise, and it blocks a rule we want: **an entry should be Completed only while its episodes watched covers everything available**. Today a Completed entry can sit at 10/24 indefinitely, because the app cannot tell a rewatch-in-progress from a stale count. Giving a rewatch its own status removes the ambiguity, and lets Completed mean exactly what it says.

## What Changes

- Add **Rewatching** as a watch status alongside Watching, Completed, On hold, Plan to watch, and Dropped.
- **Entering it resets progress.** Setting an entry to Rewatching sets its episodes watched to 0, so the run is tracked from the start.
- **It is reachable only for an anime that has finished airing and that the user has finished at least once** — evidenced by a finish date, a rewatch count above zero, or a current status of Completed. Because that tests history rather than present status, a rewatch abandoned and parked under Watching, On hold, or Dropped can be resumed without first returning the entry to Completed.
- **Watching it to the end re-completes and counts it.** When a Rewatching entry's episodes watched reaches the anime's total, its status returns to Completed and its rewatch count increases by one, silently.
- **Ending it early asks whether it counts.** Choosing Completed in the editor while still partway through a rewatch puts the question to the user rather than deciding — the app cannot tell an abandoned pass from one being recorded by hand. It defaults to not counting, since the abandoned case is what produces this transition.
- **It behaves like Watching in the app.** Rewatching entries appear in the home page's Currently watching carousel, get the editable progress row and "+" control, and take their own group in My List and their own status filter tab.
- **Completed becomes strict.** An entry may only be Completed while its episodes watched covers everything available. Lowering the count below that on a Completed entry moves it to **Rewatching** — which is what a count drop on a finished show means now that there is somewhere for it to go.
- **A darker blue than Completed** identifies it, in the status stripe, the filter tab, and everywhere else a status carries a colour.
- **MAL sees it as Watching.** Rewatching is pushed as MyAnimeList's `watching` with the same episodes watched, so progress through the rewatch is visible there and increments in step. Reconciliation is taught not to demote a local Rewatching entry back to Watching just because MyAnimeList reports `watching`.
- **The MAL score stays revealed**, as it does for Completed — a rewatch cannot be spoiled by a community average.

## Capabilities

### New Capabilities

None. Rewatching is a new value within capabilities that already own watch status.

### Modified Capabilities

- `list-editing`: adds the Rewatching status, the reset-on-entry and re-complete-on-finish rules, and the strict Completed rule with a count drop landing in Rewatching.
- `library-views`: adds Rewatching to My List's group order, its status filter tabs, and the status colour set.
- `main-dashboard`: the Currently watching carousel covers Rewatching entries as well as Watching ones.
- `mal-write-sync`: defines how Rewatching maps onto MyAnimeList's status values in both directions.
- `profile-stats`: adds Rewatching to the status breakdown and confirms the episode/time formulas are unaffected.
- `score-visibility`: Rewatching joins Completed and Dropped as a status whose MAL score is revealed under "always show completed scores".

## Impact

**Backend**

- `Models/UserAnimeEntry.cs` — the `WatchStatus` enum gains `Rewatching`. The column is persisted with `HasConversion<string>()` (`Data/AnimeTrackerDbContext.cs:42`), so the new value needs no data migration and no ordering care.
- `Services/Entries/UserAnimeEntryEditService.cs` — reset-on-entry, re-complete-and-increment, and the strict Completed rule.
- `Services/Mal/MalMappingExtensions.cs` — `ToMalStatusString` gains the Rewatching arm; it currently throws on an unmapped value, so this is required, not optional.
- `Services/Sync/ReconciliationService.cs` — treat a local Rewatching entry as matching a remote `watching` rather than as a difference to be overwritten.
- `Services/Library/MyListService.cs`, `Services/Dashboard/MainDashboardService.cs`, `Services/Profile/ProfileService.cs` — status handling where the enum is switched over or filtered on.

**Frontend**

- `utils/anime.ts` — `STATUS_LABELS`, `STATUS_CLASS`, and `isScoreRevealableStatus`.
- `index.css` — a `--status-rewatching` colour trio in both the light and dark palettes.
- `components/EntryEditorOverlay.tsx` — the new status option.
- `pages/MyListPage.tsx` — group order and filter tabs.
- `api/types.ts` — the `WatchStatus` union.

**Cross-change**

- `gate-editing-on-aired-episodes` overlaps this change and is intended to be applied **after** it. Its artifacts need revisiting for Rewatching — see the dedicated task group.

**Out of scope**

MyAnimeList's `is_rewatching` flag. The app already declares it in both directions (`Services/Mal/Dto/MalListStatusUpdate.cs:15`, `MalClient.cs:39`) and never sets it. Using it instead of the `watching` mapping is recorded as an open question in `design.md`, not adopted here.

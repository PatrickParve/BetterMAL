## Why

The To Be Hero X series page names a 3-minute promo short as the series — root `61633` "Tu Bian Yingxiong X Concept Movie" — and files the actual show (`53447`, "Tu Bian Yingxiong X") under Extras. This is not stale data: the stored series was rebuilt today and came out the same way, because main-line classification only ranks chains over `sequel`/`prequel` edges, and this franchise has **no** `sequel`/`prequel` edges at all. Its members link by `parent_story` only, so every member is its own one-node chain, the size comparison ties four ways, and the "earliest-aired member" tie-break hands the main line to the oldest entry — the 2022 concept video — over the 2025 show. The Rebuild button cannot fix a deterministic misclassification, which is why pressing it (and re-fetching each anime) changes nothing.

The same shape hits four other stored series today: Youjo Senki's pasta-comedy ONA, Gundam 0080, two DanMachi side entries, and Koyomimonogatari all currently sit on the main line despite MAL explicitly tagging them as side stories of another member.

Separately, the Settings page's "Build all series from my list" button appears to do nothing on the first press. The `POST /api/series/build-all` handler signals the background loop and then immediately returns the *current* progress snapshot — which is still `NotStarted`, because the background service has not yet resolved its targets and called `Start`. The page stores that snapshot, sees a non-`Running` phase, and therefore never starts polling and never enables its progress line. A second press, by which time the run really is in flight, returns `Running` and the UI finally comes alive.

## What Changes

- **Side content is excluded from the main line.** A member MAL tags as a side story of another member — an outgoing `parent_story` edge, or an incoming `side_story` edge from another member — is treated the way recaps (`summary`/`full_story`) already are: it can never be a main-line entry while any non-side member is available. This is what demotes the concept/character movies and promotes the real show.
- **Chains are ranked by eligible members, not raw members.** Candidate `sequel`/`prequel` chains compete on how many *main-line-eligible* members they contain (excluding specials, music, recaps, and the new side content), with the earliest-aired eligible member breaking ties. A chain made only of promo shorts now scores zero and loses to a one-entry chain holding the actual show. The graph itself is still built over every member, so a recap that bridges two seasons keeps them in one chain exactly as it does today.
- **Stored series heal themselves once.** `SeriesService` gains a classifier-revision timestamp; a series built before it is treated as needing a rebuild, so all 192 stored series recompute on their next read instead of the user having to guess which ones are wrong. No new fetches beyond the existing per-build budget.
- **"Build all series" also covers out-of-date series,** not only anime with no series at all, so one press of the Settings button heals the whole store.
- **The bulk-build trigger reports itself as running immediately.** The POST marks the run pending before returning, so the first press shows progress and disables the button, as the spec already requires.
- **A bulk run that fails whole-hog no longer sticks the UI on "Building…".** A new `Failed` phase is reported and the button becomes usable again.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `series-page`: three requirements change — "Main line and extras" (side-story exclusion and eligible-member chain ranking), "Series read endpoint and freshness" (a stored series built under superseded classification rules is rebuilt on next read), and "Build all series from my list" (the trigger reports the run as in flight immediately, targets include out-of-date series, and a failed run is reported rather than left appearing to run forever).

## Impact

- **Backend** — `Services/Series/SeriesGraphBuilder.cs` (classification), `Services/Series/SeriesService.cs` (freshness rule), `Services/Series/SeriesBulkBuildBackgroundService.cs` (target selection, failure reporting), `Services/Series/ISeriesBulkBuildProgressTracker.cs` + `SeriesBulkBuildProgressTracker.cs` (pending/failed states), `Controllers/SeriesController.cs` (mark pending on trigger).
- **Frontend** — `src/api/types.ts` (`Failed` phase), `src/pages/SettingsPage.tsx` (render a failed run).
- **Data** — no schema change and no migration. Existing `Series` rows are rewritten in place by the normal rebuild path; series identity, `FavouriteRank`, and member sets are untouched by this change. Five stored series change which member is main line, one of them (To Be Hero X) also changing its root, title, and picture.
- **MAL API** — the one-time re-classification pass reuses the existing per-build fetch budgets (8 on a visit, 20 on an explicit rebuild); it spends fetches only on members with missing or lean cached rows, exactly as the 30-day staleness rebuild already does.
- **Docs** — `CODE_GUIDE.md`'s `Services/Series/` section.

## 1. Baseline the current classification

- [x] 1.1 Dump every stored series' root and main-line membership to a scratch file (`SELECT s."Id", s."RootAnimeId", sm."AnimeId", sm."IsMainLine", sm."Order" FROM "Series" s JOIN "SeriesMembers" sm ON sm."SeriesId" = s."Id" ORDER BY s."Id", sm."AnimeId"`), so task 4 can diff against it.
- [x] 1.2 Re-run the side-content detection query from design.md's Context and save its six-row result, so the post-change delta can be checked against exactly the members expected to move.

## 2. Main-line classification (`SeriesGraphBuilder`)

- [x] 2.1 Add `FindSideContentIds(members, memberById)` alongside `FindRecapIds`: a member with an outgoing `parent_story` edge to another member is side content, and a member another member points at with `side_story` is side content. Only edges between two members count. Document the converse pair in the summary comment the way `FindRecapIds` documents `summary`/`full_story`.
- [x] 2.2 In `ClassifyMainLineChain`, compute one ineligible set = recaps ∪ side content ∪ members whose `MediaType` is `special` or `music`, and derive `IsEligible(id)` from it.
- [x] 2.3 Change candidate-chain selection to: prefer chains holding an eligible `tv` member (all chains when none does) → order by eligible-member count descending → tie-break on the earliest-aired eligible member, falling back to the earliest member of any kind when a chain has no eligible members. Keep chain formation over every member so a bridging recap or side entry still joins the seasons either side of it.
- [x] 2.4 Reduce the winning chain to its eligible members, keeping the existing "fall back to the whole chain when that empties it" behaviour.
- [x] 2.5 Update the XML doc comments on `ClassifyMainLineChain` to describe eligibility-based ranking and the no-`sequel`/`prequel`-edges case, replacing the current "largest chain" wording.
- [x] 2.6 Add `public static readonly DateTimeOffset ClassificationRevisedAt` with the ship date and a comment stating the bump-on-rule-change rule.

## 3. Classification-triggered rebuilds

- [x] 3.1 Add the `series.BuiltAt < SeriesGraphBuilder.ClassificationRevisedAt` clause to `SeriesService.NeedsBuild`, and extend its comment (which currently explains why `IsTruncated` is deliberately excluded) to cover the new clause.
- [x] 3.2 In `SeriesBulkBuildBackgroundService.GetTargetsAsync`, add my-list anime whose series predates `ClassificationRevisedAt` to the targets, alongside those with no `SeriesMembers` row at all.
- [x] 3.3 Change the in-run `alreadyCovered` re-check to "has a member row **and** its series was built at or after `ClassificationRevisedAt`", so a franchise rebuilt earlier in the same run still short-circuits its other members.

## 4. Verify the classification change against real data

- [x] 4.1 Build and run the backend, open the To Be Hero X series page (anime 53447), and confirm the header shows the show — not `Tu Bian Yingxiong X Concept Movie` — with the three shorts under Extras as ONA.
- [x] 4.2 Trigger the Settings "Build all series from my list" action and let it finish, so every stored series is re-classified.
- [x] 4.3 Re-run task 1.1's dump and diff it against the baseline. **Result differed from the prediction here — investigated, both root causes below are correct, not bugs:**
  - Series 73 (To Be Hero X) and series 147 (Sakasama no Patema) both changed root. 147 wasn't in task 1.2's six-row audit (that query only found MAL-tagged side content) — it's a second, independent case the eligibility fix also corrects: a `special` (`13429`) and a `movie` (`12477`) with no `sequel`/`prequel` edge between them used to tie as size-1 chains and lose to the special's earlier air date; ranking by eligible count now scores the special 0 and the movie 1, so the movie wins. Confirmed via the DB: no edge between them, `13429.MediaType = special`.
  - 11 members flipped `IsMainLine` total: the six from task 1.2, `53447`'s consequential promotion (To Be Hero X), and the Patema pair. A third pair (series 170, Tetsuwan Atom members `971`/`3044`) also flipped — traced to `33371` ("Atom: The Beginning") joining the series for the first time with real `sequel`/`prequel` edges to all three older adaptations, unifying what had been separate singleton chains. Confirmed via `AnimeRelatedAnime`: `33371` declares `sequel` to `971`/`2747`/`3044`.
  - 30 further members were added (zero removed) across 11 series (16, 27, 42, 105, 126, 129, 132, 150, 170, 173, 188) — all traced to previously-lean/unfetched rows finally being resolved now that every stored series was force-rebuilt at once (a scale of self-healing the 30-day staleness path never exercises in one pass). This is `TraverseAsync`'s pre-existing lean-member expansion, untouched by this change, not new classification behaviour. Titles checked: all are genuine sequels/specials/parts of ongoing franchises (JoJo Stone Ocean parts, Fairy Tail seasons, Nanatsu no Taizai seasons, Konosuba 4, Baki-dou Part 2, etc.).
  - One pre-existing, out-of-scope quirk noted for awareness, not fixed here: series 27 (Gundam) already fused multiple separate Gundam continuities under one series before this change (visible in the *baseline* dump too), via the pre-existing `alternative_version` traversal relation. This rebuild pulled in several more Gundam TV entries the same way. Untouched by this change (`SeriesRelations.TraversalSet` is out of scope per design.md's Non-Goals) and worth its own follow-up.

## 5. Bulk-build trigger reports itself immediately

- [x] 5.1 Add `MarkPending()` to `ISeriesBulkBuildProgressTracker` and implement it in `SeriesBulkBuildProgressTracker` as `Phase = Running, Built = 0, Total = 0` under the existing lock.
- [x] 5.2 Call `bulkBuildProgress.MarkPending()` in `SeriesController.TriggerBulkBuild` before returning, so the `Accepted` body already reports the run in flight.
- [x] 5.3 Add `Failed` to `SeriesBulkBuildPhase` and `Fail()` to the tracker (keeps the counts reached), and call it from `SeriesBulkBuildBackgroundService`'s existing whole-run `catch` so a run that dies during target resolution cannot strand the page on "Building…".
- [x] 5.4 Add `'Failed'` to `SeriesBulkBuildPhase` in `frontend/src/api/types.ts`.
- [x] 5.5 In `SettingsPage.tsx`, render a failed run as `Last run failed after {built}/{total} processed — see backend logs.` and keep the button enabled for it (the existing `phase === 'Running'` guards already do the right thing; confirm the "Last run complete" branch no longer catches `Failed`).

## 6. Tests

- [x] 6.1 Add `SeriesGraphBuilderTests` using the in-memory `AnimeTrackerDbContext` helper the existing series tests use.
- [x] 6.2 Test the To Be Hero X shape: four members, no `sequel`/`prequel` edges, three with `parent_story` to the fourth → the fourth is the sole main-line member and the series root, the other three are extras.
- [x] 6.3 Test that a member another member tags with `side_story` is an extra even when its media type would allow the main line.
- [x] 6.4 Test that a side entry carrying `sequel`/`prequel` edges between two seasons keeps those seasons in one main line while staying an extra itself (the bridging case that forbids deleting ineligible nodes from the graph).
- [x] 6.5 Test that a lone `tv` member with no `sequel`/`prequel` edges still outranks a longer chain of side movies, and that a franchise with no `tv` member at all falls back to ranking every chain.
- [x] 6.6 Test the all-ineligible fallback: every member a special or side entry → the largest chain is used unreduced rather than leaving no main line.
- [x] 6.7 Add a `SeriesService` test that a series with `BuiltAt` before `ClassificationRevisedAt` is rebuilt on read while one built after it is served from storage.
- [x] 6.8 Extend `SeriesBulkBuildBackgroundServiceTests` with: an out-of-date series' members are targets, a series rebuilt earlier in the run short-circuits its other members, and a run that throws during target resolution reports `Failed`.
- [x] 6.9 Add a `SeriesController` (or tracker-level) test that the trigger response reports `Running` before the background service has started, and run the full backend suite.

## 7. Documentation

- [x] 7.1 Update `CODE_GUIDE.md`'s `Services/Series/` section: eligibility-based main-line ranking, the side-content relation pair, `ClassificationRevisedAt` and what bumping it does, and the bulk-build pending/failed phases.
- [x] 7.2 Note in `CODE_GUIDE.md` (or wherever the trigger pattern is described) that `AiringController` and `SyncController` still return a pre-signal snapshot, so the follow-up is recorded rather than rediscovered.

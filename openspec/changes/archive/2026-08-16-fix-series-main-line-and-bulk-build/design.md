## Context

Two independent defects, both in `Services/Series/`, both reproduced against the live store.

**1 — Main-line classification picks a promo short.** `SeriesGraphBuilder.ClassifyMainLineChain` builds an adjacency map over `sequel`/`prequel` relations only, splits it into connected components, keeps the component with the most nodes (ties → the component holding the earliest-aired node), and only then drops `special`/`music`/recap members from the winner. Selection happens before eligibility is considered, so an ineligible member is still counted when chains compete.

The To Be Hero X component has no `sequel`/`prequel` edges at all — verified in the store:

```
53447 Tu Bian Yingxiong X                          --other-->        61633, 35044
53447                                              --character-->    61719, 62576
61633 Tu Bian Yingxiong X Concept Movie            --parent_story--> 53447
61719 Tu Bian Yingxiong X Character Story Movie    --parent_story--> 53447
62576 Tu Bian Yingxiong X Character Concept Movie  --parent_story--> 53447
```

All four members are ONA, so the existing `tv`-chain preference does not apply. Four one-node chains tie at size 1, the earliest-aired tie-break picks the 2022 concept video, and the stored series ends up `RootAnimeId = 61633` with the actual 2025 show demoted to Extras. `BuiltAt` is today's date: the Rebuild button ran and reproduced the same answer, because nothing about the input is stale — the rule is wrong.

The same query run over all 192 stored series finds five more members currently main line while MAL explicitly tags them as another member's side story: Youjo Senki's `Sabaku no Pasta Daisakusen`, `Gundam 0080`, DanMachi's `Orion no Ya` and its OVA, and `Koyomimonogatari`. Only To Be Hero X has a wrong *root*; the rest are wrong main-line membership.

**2 — "Build all series" needs two presses.** `SeriesController.TriggerBulkBuild` calls `bulkBuildTrigger.Signal()` and returns `bulkBuildProgress.Snapshot` in the same breath. The background service is still parked in `WaitAsync` and has not run `GetTargetsAsync`, so the snapshot returned is whatever the last run left — `NotStarted` on a cold backend. `SettingsPage` stores that, and both its progress line (`phase !== 'NotStarted'`) and its poll effect (`phase !== 'Running'`) stay switched off. A second press lands after the run has really started, reads `Running`, and everything works. The same trigger/snapshot shape exists in `AiringController` and `SyncController`.

Constraints: `db.Database.Migrate()` runs at startup and there is no manual migration step, so a schema change means authoring a migration under the .NET 10 SDK — worth avoiding for a fix of this size. MAL fetches are budgeted per build (8 on a visit, 20 on a rebuild) and that budget is the only thing keeping a re-classification pass from being a bulk fetch job.

## Goals / Non-Goals

**Goals:**
- The entry a franchise is actually named after leads its series page, including franchises with no `sequel`/`prequel` edges anywhere.
- MAL's explicit side-story tags are honoured the way its recap tags already are.
- Already-stored series pick up corrected classification without the user knowing which ones are wrong.
- One press of "Build all series from my list" shows progress and disables the button.
- A bulk run that dies never leaves the page claiming a build is in progress.

**Non-Goals:**
- No change to which relations are traversed (`SeriesRelations.TraversalSet` stays as is) — this is about classifying members, not finding them.
- No user-facing control for choosing a series' root or main line by hand.
- No schema change, no migration, no change to series identity or `FavouriteRank` semantics.
- The identical trigger/snapshot race in `AiringController` and `SyncController` is left alone; it is the same latent bug but neither was reported and both deserve their own change.

## Decisions

### Decision 1: Side content is an eligibility rule, not a new traversal rule

MAL states side-story membership with a converse pair, exactly like the recap pair the builder already understands:

| Relation stored on A | Meaning | Ineligible member |
|---|---|---|
| `A --summary--> B` | B condenses A | B *(existing)* |
| `A --full_story--> B` | A condenses B | A *(existing)* |
| `A --side_story--> B` | B is a side story of A | B *(new)* |
| `A --parent_story--> B` | A's parent story is B | A *(new)* |

So `FindSideContentIds` mirrors `FindRecapIds` one-for-one, and both feed a single ineligibility set. Only edges between two *members* count — an entry whose parent story was never traversed into the series is not demoted by it.

**Alternatives considered.** *Stop traversing `parent_story`/`side_story`* — wrong: those edges are the only thing holding the To Be Hero X franchise together, and dropping them would leave four unrelated one-member components and no series page at all. *Special-case the media type of promo shorts* — MAL types the concept movie `ona`, the same type as the show itself; there is no signal there. *Prefer the highest-`MalScore` or most-`NumListUsers` member as root* — popularity is not story structure, and it would reshuffle roots across the whole store for reasons no rule can state cleanly.

### Decision 2: Rank chains by eligible members, keep the chain graph whole

Chain formation stays over every member; only the ranking key changes, from `chain.Count` to "how many of this chain's members are eligible". Removing ineligible nodes from the *graph* was the obvious alternative and is wrong for a reason the existing code already documents: a recap special routinely carries the `sequel`/`prequel` edge that bridges season 1 to season 2, so deleting it splits one franchise into two chains. Ranking after the fact keeps the bridge and still refuses the bridge a place on the main line.

Full ordering of candidate chains, in order of application:

1. If any chain holds an eligible `tv` member, only those chains are candidates; otherwise all chains are (unchanged rationale — a One Piece-shaped singleton must not lose to a chain of side movies — but now stated over eligible members so a chain of ineligible `tv` recaps cannot qualify).
2. Rank by eligible-member count, descending.
3. Break ties by the earliest-aired eligible member (`OrderKey`), falling back to the earliest member of any kind when a chain has no eligible members.
4. Reduce the winner to its eligible members; if that empties it, keep it whole (the existing specials-only fallback, now also covering a hypothetical all-side-content cycle).

For To Be Hero X: no eligible `tv` member anywhere → all four singletons are candidates; `{53447}` has one eligible member and the three promo shorts have zero; `{53447}` wins; main line = the show; root = the show. Extras become the three shorts, grouped as ONA in aired order.

### Decision 3: A classification-revision timestamp, not a schema column

`SeriesService.NeedsBuild` gains one clause: `series.BuiltAt < SeriesGraphBuilder.ClassificationRevisedAt`, a `static readonly DateTimeOffset` set to the date this change ships and bumped whenever classification rules change again. Every stored series then rebuilds exactly once, on its next read, through the path that already exists.

*A `Series.BuildVersion` column* was the alternative and is cleaner in the abstract, but it costs a migration for a fix that otherwise needs none, and the codebase already uses precisely this "row predates X, heal it on first read" pattern (`AnimeDetailService` refetches rows whose `LastSyncedAt` predates the `AddAnimeRelatedAnime` migration). *Wiping the `Series` table on deploy* would rebuild everything from scratch but discards series ids and every `FavouriteRank` with them.

Cost: 192 one-time rebuilds spread over reads. Each is a graph traversal over cached rows; fetches are spent only on members with a missing or lean row, capped by the existing per-build budget. This is the same work the 30-day staleness rule already triggers, brought forward once.

### Decision 4: The bulk run also targets out-of-date series

`GetTargetsAsync` currently returns my-list anime with no `SeriesMembers` row. It gains a second source: my-list anime whose series' `BuiltAt` predates `ClassificationRevisedAt`. The in-run `alreadyCovered` re-check becomes "covered **and** built at or after the revision", which needs no extra bookkeeping — a series rebuilt earlier in the same run is stamped `BuiltAt = UtcNow`, so its other members fall out of the target set naturally, and the "one build covers a whole franchise" behaviour is preserved by construction.

This makes the Settings button the one deliberate way to heal the whole store, rather than waiting for 192 page visits.

### Decision 5: Mark the run pending inside the trigger request

`ISeriesBulkBuildProgressTracker` gains `MarkPending()`, called by the controller before it returns, publishing `Phase = Running, Built = 0, Total = 0`. The response therefore reports a run in flight, the page starts polling on the first press, and the button disables. `Start(total)` still overwrites with the real total once the background service resolves targets, so "Building… 0/0" is replaced within a poll or two.

*Polling unconditionally for a few seconds after the POST* was the frontend-only alternative: it papers over a backend that reports something untrue, and leaves the "control disabled while a run is in flight" requirement satisfied by a timer rather than by state.

`MarkPending` publishes `Running` rather than a distinct `Pending` phase because every consumer — button disabled, progress line shown, polling active — wants the same behaviour for both, and a fourth phase would have to be handled identically everywhere it appears.

### Decision 6: A `Failed` phase, because pending states can now strand

Before this change, a run that threw during target resolution left the phase at `NotStarted` — invisible, but not stuck. With `MarkPending`, that same throw would strand the page on "Building…" forever with the button disabled. So the background service's existing `catch` also calls `progress.Fail()`, publishing `Phase = Failed` with the counts reached, and `SeriesBulkBuildPhase`/`SeriesBulkBuildStatusDto` gain the case. The Settings page renders "Last run failed after N/M processed — see backend logs." and re-enables the button.

Reusing `Complete()` on failure was the smaller diff and was rejected: reporting a failed run as complete is the kind of quiet lie that makes the next bug report harder to read.

## Risks / Trade-offs

- **[The side-content rule demotes an entry the user considers main line]** → Blast radius was measured before writing the rule, not after: exactly six members across five series change status, and each was inspected (a comedy ONA short, an OVA side story, two DanMachi side entries, a Monogatari side entry, and the To Be Hero X promos). Only To Be Hero X changes root. Implementation re-runs the same query after the change to confirm the delta matches.
- **[Eligible-count ranking changes a main line somewhere unmeasured]** → The ranking only differs from today when the raw-largest chain and the eligible-largest chain disagree, which requires a chain carrying more ineligible members than another chain carries eligible ones. Task 4 captures every series' root and main-line count before and after the re-classification pass and diffs them, so any surprise is seen rather than assumed.
- **[192 rebuilds spend MAL fetch budget in a burst]** → Fetches only happen for members with a missing or lean cached row, bounded per build by the unchanged budget and paced by the existing MAL client handler. Reads are spread across page visits; the Settings button concentrates them, which is the point of a manual, background, cancellable-by-restart action.
- **[Bumping `ClassificationRevisedAt` is a manual step future changes can forget]** → It sits next to the classification code with a comment stating the rule, and every task that changes classification lists it. A forgotten bump degrades to today's behaviour (corrections reach a series on its 30-day staleness rebuild) rather than breaking anything.
- **[`MarkPending` fires even when the background service is not running]** → The service is registered as a hosted service for the process lifetime; if it were absent, the phase would sit at `Running` with no progress, which is the same symptom as a hung run and is visible rather than silent.

## Migration Plan

No schema change and no data migration. Deploy order is irrelevant — the frontend's `Failed` phase is additive and an older frontend renders an unknown phase as the "last run" line.

Rollback: reverting the commit restores the previous classification. Series stored under the new rules stay as they are until their next rebuild, at which point the reverted rules re-apply. Nothing is lost either way — series ids, member sets, and favourite ranks are never keyed on classification.

## Open Questions

- Should the same trigger-response race be fixed in `AiringController` and `SyncController` now? They are the same three-line shape. Deliberately deferred here to keep this change reviewable, and worth a follow-up.
- Should `spin_off` join the ineligibility set? Its stored direction is less certain than `parent_story`/`side_story`, and no stored series currently shows a wrong main line because of it, so it is left out until a case appears.

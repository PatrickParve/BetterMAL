## Context

`WatchStatus` currently holds five values — Watching, Completed, OnHold, Dropped, PlanToWatch — and is persisted with `HasConversion<string>()`, so it lands in SQLite as text. Adding a value therefore needs no migration and no care about ordinal position, which removes the usual risk from extending a persisted enum.

Rewatching is not currently modelled anywhere, but is already *described* in two places as a Completed entry with a reduced count:

- `profile-stats`: "a twelve-episode series with a rewatch count of two whose episodes watched currently reads one"
- `list-editing`: "Rewatch increment on a completed entry — pressing + on an already-Completed entry opens no score prompt"

`RewatchCount` exists on the entry and syncs to MyAnimeList as `num_times_rewatched`.

MyAnimeList has no `rewatching` value in its status enum — its five are `watching`, `completed`, `on_hold`, `dropped`, `plan_to_watch`. It does carry a separate `is_rewatching` boolean on the list status. This app already declares that field in both directions — `MalClient.cs:39` requests it on reads, `MalListStatusUpdate.cs:15` serialises it on writes — and never sets or reads it. `MalMappingExtensions.ToMalStatusString` throws on an unmapped status, so a new enum value **must** be given an arm there or every sync of a Rewatching entry will fail.

## Goals / Non-Goals

**Goals:**

- Rewatching is a first-class status: selectable, filterable, colour-coded, and visible where in-progress watching is visible.
- Completed becomes an honest claim — it holds only while the count covers everything available.
- A rewatch survives a round trip through MyAnimeList without being silently demoted.
- The existing rewatch-count semantics, and every stat built on them, keep working unchanged.

**Non-Goals:**

- Tracking per-rewatch history (when each rewatch happened, a score per rewatch). `RewatchCount` stays a single integer.
- Adopting `is_rewatching` — see D4 and the open question.
- Re-classifying existing entries. Nothing already stored is rewritten into Rewatching on deploy; the status is reached by an edit.
- Any of the aired-episode gating from `gate-editing-on-aired-episodes`. That is its own change, applied after this one.

## Decisions

### D0: Rewatching requires a finished anime the user has finished at least once

A rewatch presupposes two things, and the status is refused unless both hold:

- **The anime has finished airing.** Airing status is `finished_airing`, or is not recorded at all — the latter included for consistency with how such an anime reaches Completed in the first place, which treats an unrecorded status as the finished case. `currently_airing` and `not_yet_aired` are both refused: there is no complete run to go through again.
- **The user has finished it at least once.** This is a fact about the entry's history, not about its present status, so it is tested against durable evidence: the entry has a **finish date**, or a **rewatch count above zero**, or its **current status is Completed**.

Any one of the three suffices. The disjunction is not redundancy — each covers a case the others miss:

| Evidence | Case it covers |
| --- | --- |
| Finish date set | The ordinary one. Entering Completed fills it and nothing clears it automatically, so it survives the entry moving to Watching, On hold, or anywhere else. |
| Rewatch count above zero | A count can only be accrued by finishing, so it is proof even where the date was cleared by hand. |
| Current status is Completed | An entry imported from MyAnimeList as completed with a blank finish date, which neither of the others would catch. |

Testing history rather than present status is what lets an abandoned rewatch be parked anywhere — Watching, On hold, Dropped — and picked up again later, without a detour through Completed. An entry that is already Rewatching passes too (its finish date or rewatch count is what carried it there), so the editor can show the status it currently holds as its selected value rather than as a disabled option.

One corner remains: an entry whose finish date has been cleared by hand, whose rewatch count is zero, and whose status has since moved off Completed carries none of the three signals, and so cannot be set to Rewatching. Restoring the finish date in the editor restores eligibility.

The app is not *entirely* without a record in that case. `ActivityLog` writes a `Completed` row on every completion and is never pruned — no code path deletes activity rows. It is deliberately **not** used as a fourth signal here, for two reasons:

- **It is not a complete record.** Activity rows are written only by `UserAnimeEntryEditService`. The two bulk paths bypass it entirely — `InitialImportService` adds entries through `ToUserAnimeEntry`, and `ReconciliationService.ApplyPendingDiff` assigns fields directly — so an imported library has Completed entries with no `Completed` activity row at all. It could only ever grant eligibility on top of the three signals, never stand in for them.
- **It cannot be read client-side.** The three signals all ride on `UserAnimeEntryDto`, so the editor decides locally with no extra data. An activity-log check would have to become a server-computed boolean on every DTO that opens the editor, plus a bulk lookup on My List.

If that flag is wanted later it is a clean addition rather than a rework, and it would carry a second benefit: a server-computed eligibility boolean would also remove the need for the editor to reimplement this rule at all.

This also settles what was an open question about starting a rewatch on a still-airing show: it cannot happen, so "everything available" is always the anime's total episode count for a Rewatching entry, never a moving aired-so-far figure.

### D1: Entering Rewatching resets episodes watched to 0

Setting an entry to Rewatching sets its episodes watched to 0 in the same edit, so the run is tracked from the beginning. This mirrors how entering Completed already fills the count to the total — the status change carries the count with it rather than leaving the user to fix it up by hand afterwards.

`StartedAt` and `CompletedAt` are untouched. The existing rule that a finish date is never cleared automatically continues to hold, and MyAnimeList likewise keeps the original finish date across rewatches.

### D2: A rewatch has two exits, and only one of them counts automatically

A Rewatching entry can return to Completed two ways, and they mean different things:

**Reaching the last episode** — episodes watched arrives at the anime's total, by increment or by an in-place count edit. The rewatch was actually completed, so the status returns to Completed and `RewatchCount` increases by one, silently. By D0 the anime has always finished airing, so the target is the total; there is no aired-so-far case here.

**Selecting Completed in the editor while still partway through** — the user stopped halfway and is tidying up. The app cannot tell this from someone who finished and is recording it by hand, so it **asks whether to count the rewatch**, and does not decide on its own. Answering no leaves `RewatchCount` untouched; answering yes increases it by one.

The asked case defaults to **not** counting, because that is the situation that produced it: an abandoned rewatch is the reason this exit exists, and a silent increment there would quietly inflate a number the user reads as "times I have watched this".

By construction the question only ever arises below the total — a Rewatching entry that reaches the total has already taken the first exit and is no longer Rewatching. So the prompt never appears in a situation where the answer is obvious.

Selecting Completed still fills episodes watched to the total, as it does from any status. That is correct even for an abandoned rewatch: the user has watched every episode, on the first viewing. Whether *this* pass counts as another one is the separate question being asked.

The completion-score prompt does **not** fire on either exit. `list-editing` is already explicit that the prompt is for entering Completed from an unfinished state; a rewatch ends with a score the user has had since the first viewing, and re-prompting for it every time would be noise. The score stays editable in the editor.

*Mechanism:* the question is a control inside the entry editor, revealed when Completed is chosen on a Rewatching entry, rather than a second overlay stacked on the editor after saving. It keeps the transition to one save and one activity record, where a follow-up modal would need two of each. The alternative — reusing the app-root overlay pattern that the completion-score prompt already uses — is available if the inline control turns out to read as too easy to miss.

### D3: A count drop on a Completed entry lands in Rewatching where a rewatch is possible, Watching otherwise

This is the strict Completed rule finally becoming safe to enforce. Three cases, distinguished by *cause* and by whether D0's preconditions hold:

| What happened | Where it lands | Why |
| --- | --- | --- |
| More episodes became available than you have watched | **Watching** | New content exists; you are behind on a first viewing |
| Your count dropped, and the anime has finished airing | **Rewatching** | You have already finished it once; you are going through it again |
| Your count dropped, but the anime is still airing | **Watching** | Same meaning, but D0 forbids Rewatching here, so the entry falls back to the nearest true status |

The third row is the one that only exists because of D0, and it is easy to miss. A Completed entry on a *currently airing* anime is reachable — `gate-editing-on-aired-episodes` allows Completed once every aired episode is watched — so lowering its count is a real case that must land somewhere. Rewatching is unavailable, so it becomes Watching. Some information is lost there (that the entry had been caught up), but inventing a fourth state for it would be worse than the fallback.

*Alternative considered:* send every count drop to Watching. Rejected for the finished case — it discards the information that the entry had been completed, which is exactly what the new status exists to preserve.

### D4: Rewatching is pushed to MyAnimeList as `watching`

`ToMalStatusString` maps Rewatching to `watching`, and the entry's episodes watched are sent as they are. On MyAnimeList the rewatch therefore reads as a normal in-progress watch, starting at 0 and climbing in step with the local count, which is what the user asked for and what makes the episode figures on both sides agree.

This costs one guard on the way back in. Reconciliation compares the local entry against MyAnimeList's copy and overwrites on a difference; without help it would see local `Rewatching` against remote `watching`, call that a difference, and demote the entry — destroying the rewatch on the first reconciliation after the push. So **reconciliation treats a local Rewatching entry as matching a remote `watching`**, and never rewrites Rewatching to Watching on that basis alone. A remote status that is anything *other* than `watching` still wins normally, so a change genuinely made on MyAnimeList still lands.

*Alternative considered — and left open:* use MyAnimeList's `is_rewatching` boolean, pushing `status=completed, is_rewatching=true`. It round-trips losslessly, needs no reconciliation guard, and the wire plumbing already exists unused in both directions. It was not adopted because it shows the entry on MyAnimeList as Completed rather than as in progress, and MyAnimeList's handling of `num_watched_episodes` while `is_rewatching` is set is not something this codebase has ever exercised. See Open Questions.

### D5: Rewatching reads as "in progress" everywhere in-progress-ness matters, and as "finished" where finishedness matters

The status sits between the two, so each surface takes the side that matches what it is for:

- **In-progress side.** The dashboard's Currently watching carousel includes Rewatching entries; My List gives Rewatching its own group directly after Currently watching; the editable progress row and "+" control behave exactly as they do for Watching.
- **Finished side.** The MAL score stays revealed under "always show completed scores" (`score-visibility`). The reasoning in that capability is that the user has settled their relationship with the anime and a community average can no longer bias or spoil a viewing still ahead of them — which is at least as true on a rewatch as on a completed entry.

### D6: A darker blue than Completed

The status palette is one colour per status, each a trio of `--status-<name>`, `-bg`, and `-border` in both the light and dark blocks of `index.css`. Rewatching gets a darker blue than Completed's (`#2563eb` light / `#60a5fa` dark), so the two read as related — both are states of a show you have finished — while staying distinguishable in a list where they sit in adjacent groups.

The pair must stay distinguishable in both palettes, which is the part worth checking by eye rather than by hex arithmetic: "darker" in the light theme and "darker" in the dark theme move in opposite perceptual directions.

### D7: The stat formulas do not change, only the breakdown

`profile-stats` computes Episodes as "episodes watched plus one full run per recorded rewatch", and Days from that same rewatch-inclusive count. A rewatch in progress under this change is `RewatchCount` completed runs plus the current `EpisodesWatched` — arithmetically identical to what the existing scenario already describes, because entering Rewatching resets the count to 0 and D2 only increments `RewatchCount` once the run finishes. No formula moves.

What does change is the status breakdown, which lists a count per status. Rewatching gets its own entry there rather than being folded into Watching or Completed, so the numbers still sum to Total Entries.

### D8: `gate-editing-on-aired-episodes` is revisited, not merged

The two changes overlap in `UserAnimeEntryEditService` and in the `list-editing` capability, and this one is applied first. Rather than trying to write either change to anticipate the other, this change carries an explicit task group that re-reads the other change's artifacts and edits them for Rewatching. The known touch points:

- Its "Nothing may be tracked against an anime that has aired no episode" requirement limits status to Watching and Plan to watch — Rewatching must be excluded too, since an unaired show cannot be rewatched.
- Its re-open rule sends a Completed airing entry to **Watching**, which stays correct under D3 and should say so explicitly rather than by omission.
- Its Completed-eligibility rule and D4 fill behaviour need to state where Rewatching sits.
- Its editor gating lists which status options are disabled; Rewatching joins that list.

## Risks / Trade-offs

**Reconciliation could still demote a rewatch if the guard is missed or regresses.** → The guard is a named requirement in `mal-write-sync` with its own test, not an implementation detail. It is the single highest-consequence line in the change: getting it wrong silently destroys user state on a background pass.

**`ToMalStatusString` throws on an unmapped status.** → Turned from a risk into a certainty by the compiler only if the switch is exhaustive; it is not, it has a `_ => throw`. The task list calls this out explicitly, and a sync test for a Rewatching entry covers it.

**Two statuses now mean "I have finished this at least once".** → Anything filtering on Completed alone will silently exclude rewatches. Every such site is enumerated in the task list; `isScoreRevealableStatus` is the one that already exists and is handled in D5.

**Users who relied on the old convention have entries that look wrong.** → Existing Completed entries with a reduced count keep their status until edited (Non-Goals). Under the strict rule, the next edit that touches the count will move them to Rewatching, which is the correct classification anyway.

## Open Questions

1. **`is_rewatching` instead of the `watching` mapping (D4).** Using it would round-trip losslessly and remove the reconciliation guard entirely — the plumbing is already there and unused. The cost is that MyAnimeList shows the entry as Completed rather than in progress, and that its handling of `num_watched_episodes` during a rewatch is unverified. Worth a single manual experiment against a real account before committing to the `watching` mapping permanently.
2. **Where should Rewatching sit in My List's group order?** This change places it directly after Currently watching, on the grounds that both are in progress. Placing it next to Completed instead would group by "finished at least once". Cheap to change later; the order is one array.
3. **Should the "count this rewatch?" question be an inline control or an overlay?** D2 makes it a control inside the editor, revealed when Completed is chosen on a Rewatching entry, to keep the transition to a single save. If it reads as too easy to skip past, the app-root overlay pattern used by the completion-score prompt is the alternative — at the cost of a second write and a second activity record.

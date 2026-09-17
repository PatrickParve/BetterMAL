Every task in sections 2–8 replaces **line 4** of `openspec/specs/<spec>/spec.md` (the `TBD - created by …` placeholder) with one Purpose paragraph, and changes nothing else in the file (design Decision 2). Each Purpose follows design Decision 3:

- one line, two to four sentences, opening "The <spec> capability …"
- a first sentence that stands alone as a summary
- the neighbouring specs it's kept apart from
- no figures or enumerations copied from requirements

Where a task names a draft, start from that line of `docs/SPEC_CONFORMANCE.md` and correct it per its row in design Decision 4. Where it says **no draft**, write from the spec's own `## Requirements`, anchored on its row in design Decision 5. Tasks are grouped so sibling specs are written together.

## 1. Baseline

- [x] 1.1 Confirm the starting state:
  - `grep -rl "TBD" openspec/specs/*/spec.md | wc -l` prints `31`
  - `grep -n "^TBD" openspec/specs/*/spec.md` shows every hit on line 4
  - `openspec validate --specs --strict --no-interactive` reports 41 passed
  - `git diff --quiet -- openspec/specs` succeeds, so there are no uncommitted spec edits to confuse the diff checks in 9.2
- [x] 1.2 Read the 10 existing Purposes (`activity-recording`, `anime-updates`, `background-jobs`, `device-transfer`, `list-backup`, `list-recaps`, `overlay-behaviour`, `section-colour-language`, `settings-page`, `year-browser`) for voice and length before writing any.

## 2. Lists, editing and sync

- [x] 2.1 `list-editing`: draft `SPEC_CONFORMANCE.md:15`, **incomplete**. Add Rewatching, the aired guards, automatic completion and reopening, the completion prompt, adding and removing, and ranking placement. Leave logging detail to activity-recording and overlay mechanics to overlay-behaviour.
- [x] 2.2 `anime-ranking`: no draft.
- [x] 2.3 `library-views`: draft `:106`. "Ranked mode" now reads anime-ranking. Grouping is an explicit choice, Top Anime has several lists refreshed daily, and my list can be scoped to a recap period.
- [x] 2.4 `mal-write-sync`: draft `:35`. Add removals, changes held for review at startup, and Rewatching pushed as watching.
- [x] 2.5 `initial-import`: draft `:52`, **stale**. Drop the corrective re-sync entirely, since it was removed by `remove-corrective-resync`.

## 3. MAL connection and failure reporting

- [x] 3.1 `mal-api-integration`: draft `:120`. Add paging, live-search caching, relation edges persisted on full fetches, and the lost-connection lifecycle.
- [x] 3.2 `connection-status`: no draft.
- [x] 3.3 `action-failure-notices`: no draft.
- [x] 3.4 `navbar-settings-status`: no draft.

## 4. Pages and navigation

- [x] 4.1 `main-dashboard`: draft `:73`. Name the three sections.
- [x] 4.2 `anime-detail`: draft `:63`. Add relations, the series link and external links, on-page progress editing, picture choice, and a failed refresh shown as a failure.
- [x] 4.3 `airing-schedule`: draft `:90`. Episode timing comes from episode-airing-data.
- [x] 4.4 `season-browser`: draft `:142`. Add MAL's forward horizon and refusals outside it, the hide-NSFW setting, and year-browser deriving from it.
- [x] 4.5 `profile-stats`: draft `:151`, **stale by omission**. Add most rewatched, Top series, episode progress, favourite seasons and years, and banded section titles. Top-anime order reads anime-ranking.
- [x] 4.6 `navigation-and-search`: draft `:132`. Add series in search, the stored-anime fallback, and page-wide scroll and hover rules.
- [x] 4.7 `page-header-design`: no draft.

## 5. Series

- [x] 5.1 `series-page`: no draft.
- [x] 5.2 `series-versions`: no draft. Its Purpose and 5.1's must agree on which spec owns what (design Decision 5).
- [x] 5.3 `series-browser`: no draft.
- [x] 5.4 `series-identity`: no draft.
- [x] 5.5 `relation-confidence`: no draft.

## 6. Artwork and scores

- [x] 6.1 `artwork-selection`: no draft.
- [x] 6.2 `artwork-presentation`: no draft.
- [x] 6.3 `score-visibility`: draft `:162`. Add that a hidden score keeps its slot and that a rewatch keeps completed visibility. Colour and form are score-presentation's.
- [x] 6.4 `score-presentation`: no draft.

## 7. Data, refresh and deployment

- [x] 7.1 `data-persistence`: draft `:183`. Add the migration guarantees, the storage of relation, AniList and airing data, and the device identifier.
- [x] 7.2 `metadata-refresh`: draft `:171`. Add the no-live-calls rule and its named exceptions, unaired list-adjacent anime, backing off, picture backfill, and series-build enqueueing. AniList timing is episode-airing-data's.
- [x] 7.3 `episode-airing-data`: no draft.
- [x] 7.4 `deployment`: draft `:191`, **stale**. Add the native Development run reading the same `.env`, and the one `BACKEND_PORT`.

## 8. Frontend mechanics

- [x] 8.1 `frontend-data-loading`: no draft.
- [x] 8.2 `page-state-restoration`: no draft.

## 9. Verify

- [x] 9.1 No placeholder is left:
  - `grep -rl "TBD" openspec/specs/*/spec.md` prints nothing
  - `grep -rn "Update Purpose after archive\|created by archiving change\|created by change\|created by applying change" openspec/specs` prints nothing
- [x] 9.2 Nothing below `## Purpose` moved:
  - `git diff --numstat -- openspec/specs` lists exactly 31 files, each `1	1`
  - every hunk header in `git diff -U0 -- openspec/specs | grep '^@@'` is `@@ -4 +4 @@`
- [x] 9.3 `openspec validate --specs --strict --no-interactive` reports 41 passed. Don't use `openspec validate fill-in-spec-purposes` as the check: its "at least one delta" error is expected (design Decision 1).
- [x] 9.4 Print all 41 Purposes, for example `for f in openspec/specs/*/spec.md; do echo "== $f"; sed -n '/^## Purpose/,/^## Requirements/p' "$f"; done`. Read them in one sitting and confirm:
  - each of the 31 is one line of two to four sentences, not a clause or a list of topics
  - each reads like the 10 existing Purposes
- [x] 9.5 Stale drafts are corrected:
  - `initial-import`'s Purpose has no "re-sync"
  - `deployment`'s mentions the native Development run
  - `profile-stats`' covers the sections added since July
- [x] 9.6 Read each sibling group from design Decision 5 side by side. No two Purposes claim the same ground, and no area both disclaim.
- [x] 9.7 Spot-check at least five of the 16 undrafted Purposes against their own `## Requirements`: `series-versions`, `navbar-settings-status`, `frontend-data-loading`, `relation-confidence` and `page-header-design`. Every claim in each Purpose traces to a requirement, and none is just the spec's name reworded.

## 10. Record

- [x] 10.1 In `docs/ISSUE_TRIAGE.md` (local, gitignored), follow the pattern SP1–SP4 used:
  - replace the SP5 entry (`:124-129`) with a short blockquote pointing to the resolved file
  - update the Summary table's counts, and add SP5 to a "Resolved (spec/docs text only)" row for the date it lands
  - mark Phase 7 item 15 done, which closes Phase 7
  - update the SPEC_CONFORMANCE "Housekeeping: TBD Purpose sections / action 9" row (`:406`)
- [x] 10.2 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## SP5.` section under a spec/docs-text heading for that date, adding the heading if it's missing. Name this change. Record:
  - all 31 Purposes were filled in: 15 from the July drafts, 16 written fresh
  - the three drafts that had gone stale (`initial-import`, `deployment`, `profile-stats`) and what replaced their wording
  - that the change carries no delta specs and is archived with `--skip-specs`
- [x] 10.3 When reporting completion, list every finding below `## Purpose` for a separate change. Start with the `series-page` / `series-versions` duplicated series definition from design "Open Questions", and add anything new found while reading requirements in sections 2–8. Don't edit any of them here.

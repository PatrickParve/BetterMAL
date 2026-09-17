## Why

This change fixes triage item **SP5** from `docs/ISSUE_TRIAGE.md`, the remaining item in Phase 7 ("Dev-setup & docs hygiene") of its fix order. 31 of the 41 specs under `openspec/specs/` still open with the placeholder that `openspec` writes when an archive creates a spec file, so their `## Purpose` says nothing about what the capability covers. A reader has to infer each spec's scope from its requirement titles. `openspec`'s spec index also takes a spec's summary from the first line of its Purpose, so for these 31 the summary is the placeholder too.

**Verified against the repo** (2026-09-17, at `7ff588d`):

- `grep -rl "TBD" openspec/specs/*/spec.md` lists exactly 31 files, and every hit is the Purpose placeholder. The wording varies with how the file was created: "TBD - created by archiving change X", "created by change X", or "created by applying change X". All three end with "Update Purpose after archive."
- The other 10 already have a real Purpose: `activity-recording`, `anime-updates`, `background-jobs`, `device-transfer`, `list-backup`, `list-recaps`, `overlay-behaviour`, `section-colour-language`, `settings-page` and `year-browser`.
- `docs/SPEC_CONFORMANCE.md` has a one-line drafted purpose for 15 of the 31, each in a section heading (`:15`–`:191`). The other 16 were created after that review and have no draft anywhere.
- Several drafts no longer match their spec's current requirements. The ones that matter most:
  - `initial-import`'s draft still names "the corrective re-sync repair path". That path was removed by `remove-corrective-resync` (archived 2026-09-13).
  - `deployment`'s draft says only "the Docker Compose stack contract". Since `fix-native-dev-setup` (archived 2026-09-17), the spec also covers a native Development run that reads the same `.env`, and the single `BACKEND_PORT`.
  - `profile-stats`' draft predates most of the profile page's current sections: most rewatched, top series, all-list episode progress, favourite seasons and years, and family-banded section titles.

  The full review, one row per draft, is in `design.md`.

## What Changes

- **All 31 placeholder `## Purpose` sections get a real one**, two to four sentences long. Each says what the capability governs and why it's a spec of its own rather than part of a neighbouring one. The 10 existing Purposes set the style and length; `activity-recording` is the reference.
- **The 15 drafted Purposes start from their `SPEC_CONFORMANCE.md` draft**, expanded into a full paragraph. Each draft is corrected wherever it no longer matches the spec's current `## Requirements`.
- **The 16 undrafted Purposes are written from their own `## Requirements` section**: `action-failure-notices`, `anime-ranking`, `artwork-presentation`, `artwork-selection`, `connection-status`, `episode-airing-data`, `frontend-data-loading`, `navbar-settings-status`, `page-header-design`, `page-state-restoration`, `relation-confidence`, `score-presentation`, `series-browser`, `series-identity`, `series-page` and `series-versions`.
- **The placeholder wording goes entirely.** No replacement says "TBD", names the change that created the file, or keeps "Update Purpose after archive". None of the 10 existing Purposes records where its file came from.
- **Nothing below `## Purpose` changes** in any of the 31 files: no requirement text, scenario text or ordering. The `# … Specification` title line above it stays as it is.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. A Purpose isn't a requirement, and no requirement or scenario changes, so there are no delta specs. The 31 edits go directly into `openspec/specs/<capability>/spec.md`. OpenSpec's delta format (`ADDED`/`MODIFIED`/`REMOVED`/`RENAMED Requirements`) can't express a Purpose edit. The change is archived with `--skip-specs` so the archive doesn't try to apply deltas that don't exist.

## Impact

- **Specs:** the `## Purpose` section of 31 files under `openspec/specs/*/spec.md`, and nothing else in them.
- **No app code, tests, dependencies, API or behaviour changes.** No backend, frontend or dev-setup file is touched.
- **Tooling:** once the placeholders are gone, `openspec`'s spec index shows a real one-line summary for every spec. Each Purpose stays a single paragraph so that line carries the whole Purpose.
- **Out of scope:**
  - Wording below `## Purpose` in any spec. The staleness check may turn up requirement text that looks out of date. Any such finding is recorded for a separate change, not fixed here.
  - `docs/SPEC_CONFORMANCE.md`. It's a dated review document, and its drafts stay as they were written.
  - The 10 specs that already have a Purpose.
  - B4 (done in `fix-native-dev-setup`), S1–S4, N1, PF6, and every item already marked resolved in `docs/ISSUE_TRIAGE.md`.

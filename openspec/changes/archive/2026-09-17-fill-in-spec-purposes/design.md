## Context

`openspec` writes a placeholder Purpose whenever an archive creates a new spec file:

```
## Purpose
TBD - created by archiving change <name>. Update Purpose after archive.
```

The placeholder was never filled in for 31 of the 41 specs. In 12 of those files a blank line separates the placeholder from `## Requirements`, and in 19 the heading follows it directly. In all 31 the placeholder is on line 4. `openspec validate --specs --strict` passes all 41 today (the placeholder is over the 50-character minimum), so validation won't catch the problem.

Two things read a Purpose:

- **A person** opening the spec, who wants to know what the capability owns before reading hundreds of lines of requirements. `list-editing` is 1,199 lines, `profile-stats` 1,689 and `series-page` 1,737.
- **`openspec`'s spec index**, which takes the first non-blank line under `## Purpose` as the spec's summary (`extractFirstPurposeLine`).

The 10 existing Purposes are the model. They run from one long sentence (`overlay-behaviour`) to four sentences (`activity-recording`). Most open with "The <name> capability governs / defines / lets me…". Each says what the capability covers and what it is kept apart from. `section-colour-language`, for example, lists what it must not disturb, and `list-backup` says how it differs from device-transfer. `docs/SPEC_CONFORMANCE.md` has one-line drafts for 15 of the 31, written 2026-07-19. The remaining 16 specs were created after that review.

## Goals / Non-Goals

**Goals:**
- Every spec's Purpose tells a reader, in two to four sentences, what the capability governs and why it's a spec of its own.
- Purposes of sibling specs agree on where the boundary between them falls.
- The diff touches only the placeholder line of each of the 31 files.

**Non-Goals:**
- Fixing anything below `## Purpose`, including requirement text the review below finds out of date or duplicated. Findings are recorded, not fixed.
- Editing `docs/SPEC_CONFORMANCE.md` or any of the 10 existing Purposes.
- Making Purposes a second copy of the requirements. A Purpose names what the spec owns, not the rules it sets.

## Decisions

### 1. Edit the main specs directly, with no delta specs, and archive with `--skip-specs`

OpenSpec's delta format only knows `ADDED`/`MODIFIED`/`REMOVED`/`RENAMED Requirements`, and archiving a `MODIFIED` delta replaces requirement blocks, never the Purpose. A Purpose edit therefore can't travel through a delta. The 31 edits are made in place under `openspec/specs/`. `openspec archive fill-in-spec-purposes --skip-specs` then moves the change to the archive without applying any deltas. Archive only runs delta validation when a delta header is present (`archive.js`), so an empty `specs/` doesn't block it.

One consequence is that `openspec validate fill-in-spec-purposes` reports "Change must have at least one delta". That error is expected here and isn't something to fix. Archive shows the same finding only as a non-blocking proposal warning. The check that means something for this change is `openspec validate --specs --strict`, over the specs that were actually edited.

*Alternative considered:* a token `MODIFIED` delta per spec, re-stating one requirement unchanged, so the specs artifact counts as done. Rejected. It would be 31 fake requirement edits, the archive would rewrite requirement blocks this change promises not to touch, and it would still not carry the Purpose.

### 2. Replace the placeholder line and nothing else

Each file's line 4 (the placeholder) is replaced by the new Purpose. The blank line after it, or the lack of one, stays as it is in all 31 files. A heading can follow a paragraph directly in Markdown, so both layouts render the same, and `git diff --numstat` then shows exactly `1 1` per file. That is the cheapest proof that nothing below the Purpose moved.

*Alternative considered:* adding the missing blank line in the 19 files. Rejected. It's cosmetic, and every file would stop showing a uniform one-line diff.

### 3. One paragraph, on one line, two to four sentences

- **One paragraph on one line.** `extractFirstPurposeLine` indexes only the first non-blank line, so a Purpose split across lines or paragraphs would be cut off in the index. This rules out `device-transfer`'s two-paragraph form for these 31.
- **The first sentence stands alone as a summary.** It says what the capability governs, because it is what the index shows first.
- **The later sentences give the reason for a separate spec.** They name the neighbouring capabilities it is distinguished from, and what it deliberately leaves to them. Naming a neighbour in plain prose ("the series-page capability") is fine; `background-jobs` and `section-colour-language` already do this.
- **Voice matches the spec body.** Open with "The <name> capability …". Refer to the user as the requirements do: specs written in the first person ("my list", "I") keep it, and specs about "the user" keep that.
- **No figures, limits or enumerations that belong in a requirement.** Examples are "8 members per visit", the list of story relations and the exact filter names. They go stale first, and they already live below.

*Alternative considered:* copying the `SPEC_CONFORMANCE.md` heading form ("the X — a, b, c"). Rejected. It's a compressed list of topics, not a statement of purpose, and several of those drafts are already stale (Decision 4).

### 4. Drafts are a starting point; the current requirements win

Each of the 15 drafts was checked against its spec's current `### Requirement:` headings and opening lines. The review:

| Spec | July draft | Status now | What the Purpose must reflect |
|---|---|---|---|
| `list-editing` | business rules for editing an entry (dates, guards, logging, sync triggering) and the single overlay editor | **Incomplete** | Also owns Rewatching as a status and its rules, the "has aired" / "fully aired" resolution that guards tracking, automatic completion and reopening, the completion score prompt, adding and removing, and placing a newly scored anime in the ranking. What exactly gets logged is activity-recording's; how overlays behave as a mechanism is overlay-behaviour's. |
| `mal-write-sync` | how local edits reach MAL (debounce, retry) and how MAL-side drift is pulled back under review | Accurate, thin | Add removals pushed to MAL, changes still pending at startup held for review (and how a held change is decided), and Rewatching pushed as watching. |
| `initial-import` | first-run full-list import **and the corrective re-sync repair path** | **Stale** | The corrective re-sync was removed (`remove-corrective-resync`, 2026-09-13). The spec now covers only the background import after authorization: progressive insertion, pacing, resuming, visible progress, retrying on its own, and leaving a pending removal alone. |
| `anime-detail` | single-anime page layout, data completeness, one-anime refresh | Accurate, thin | Add the relations controls and overlay, the series link and external links, progress and status editing on the page, picture choice, and a failed refresh shown as a failure rather than an empty page. |
| `main-dashboard` | the home page's three sections and their interaction rules | Accurate | Still three: Currently watching, Airing today and Followed shows airing. Name them. Rewatches and undoing a completion from a card now belong to the carousel. |
| `airing-schedule` | the weekly, break-aware schedule grid in local time | Accurate | The break-week scenario is still there. Note that episode timing comes from episode-airing-data. |
| `library-views` | My List and Top Anime — grouping, filtering, sorting, ranked mode, inline actions | Mostly accurate | "Ranked mode" is now rank numbers when sorted by score, drawn from anime-ranking. Grouping is an explicit choice, status tabs are multi-select, Top Anime has several ranking lists refreshed daily, and my list can be scoped to a recap period. |
| `mal-api-integration` | auth modes, PKCE, token lifecycle, pacing, field selection | Accurate, thin | Add full-list paging, live-search caching, full fetches persisting relation edges, and the lost-connection lifecycle: recorded on a refused sign-in, no sign-in while lost, restored by re-authorizing, and the state reported. |
| `navigation-and-search` | navbar, global card-click behaviour, English-title preference, both search surfaces | Accurate, thin | Add series in search results, the fallback to stored anime when live search fails, and the page-wide scroll, bounce and hover rules. Also add that a search can schedule a series build. |
| `season-browser` | whole seasons with MAL's own classification and cache-first reads | Accurate | Add MAL's forward season horizon and refusing requests outside it, and the hide-NSFW setting. Year-browser is derived from this spec's cached listings. |
| `profile-stats` | local-only stats, filtered activity feed, top-anime fill rules, distribution, divergence | **Stale by omission** | Also most rewatched, Top series, all-list episode progress, favourite seasons and years, and family-banded section titles. Top-anime order now reads anime-ranking. |
| `score-visibility` | global MAL-score hide toggle, per-score reveal, completed-shows exception | Accurate | Add that a hidden score keeps its slot and that a rewatch keeps a completed entry's visibility. Colour and form are score-presentation's. |
| `metadata-refresh` | tiered background refresh, visit-triggered lean refreshes, on-demand full refresh | Accurate, thin | Add the rule of no live calls on render beyond the named exceptions, and unaired list-adjacent anime with announcement resolution. Add backing off when MAL is down, visit-triggered picture backfill, and relation discovery enqueuing a series build. AniList episode timing is episode-airing-data's. |
| `data-persistence` | what must be durable in Postgres and why | Accurate, thin | The spec also sets what each migration must preserve (chosen pictures, portability, the series rekey, the picture column), the storage of relation, AniList and airing data, and the per-database device identifier. |
| `deployment` | the Docker Compose stack contract | **Stale** | Since `fix-native-dev-setup` (2026-09-17), it also covers a native Development run reading the same gitignored `.env`, and the one backend port set by `BACKEND_PORT`. |

### 5. The 16 undrafted Purposes are anchored on their boundaries

What distinguishes each of these specs is mostly where it ends. Each is written from its own requirements, starting from this note of what it owns and what it leaves to a neighbour:

| Spec | Owns | Kept apart from |
|---|---|---|
| `action-failure-notices` | Every action taken on my behalf that doesn't take effect is reported, through one app-wide notice, with the reason the server gave. | connection-status (the backend unreachable as a whole) |
| `anime-ranking` | One persisted total order over every scored, aired, not-plan-to-watch anime (by score, then my hand order), every by-my-score ordering reading it, and the ranking editor. | library-views, profile-stats and list-recaps, which read it; list-editing, which places a newly scored anime |
| `artwork-presentation` | How a displayed picture is drawn in fixed-height row slots and thumbnails: whole, at its own proportions, with orientation read from the loaded image. | artwork-selection (which picture); anime-detail and series-page (their own larger artwork) |
| `artwork-selection` | MAL's picture set stored for my-list anime; choosing an anime's or a series' picture; the choice shown everywhere and surviving every sync; the time stamped on each presentation choice. | artwork-presentation (how it's drawn); series-identity (resolving a series' title and picture); device-transfer (carrying choices between devices) |
| `connection-status` | One frontend reachability state for the app's own backend, the connection-lost notice, clearing itself on recovery, and the first-load full-page message. | mal-api-integration (the connection to MAL); action-failure-notices (a single failed action) |
| `episode-airing-data` | AniList as the sole source of episode timing and of totals MAL lacks; per-episode rows and their bulk reads; refresh cadence, triggers and pacing. | metadata-refresh (MAL metadata); airing-schedule, main-dashboard and list-editing, which read it |
| `frontend-data-loading` | The web client's read discipline: identical in-flight reads collapse, each distinct read is issued once per load, mutation responses are applied without a refetch, and reads are keyed by what they depend on. | page-state-restoration (back/forward restores) |
| `navbar-settings-status` | The Settings control's "needs me" indicator and job progress bar: what raises and clears each, derived from server state so every browser on the device agrees, at no outside-service cost. | background-jobs (job lifecycle); settings-page (where jobs are shown in full) |
| `page-header-design` | Each page's title header block and treatment, one height per filter/sort cluster, and the shared multi-select filter's panel, trigger, label and All/None shortcuts. | section-colour-language (section colour); series-page (its hero-block title) |
| `page-state-restoration` | Back/forward as a restore of every routed page: immediate render from held data, a background refresh, view controls, page, strip and overlay scroll offsets, and open overlays. | overlay-behaviour (overlays as a mechanism while open); frontend-data-loading (reads on a fresh load) |
| `relation-confidence` | Reading related-anime edges in both directions, grading each edge's confidence by whether its ends agree, AniList settling contested edges, and resolving one canonical prequel and sequel. | mal-api-integration and data-persistence (fetching and storing edges); series-page and anime-detail, which consume the result |
| `score-presentation` | The app-wide MAL (blue) and mine (purple) score roles, the two densities, paired chips on one line, and score-setting and role-selecting controls carrying their role's colour. | score-visibility (whether a MAL score shows); section-colour-language (which reuses these roles) |
| `series-browser` | The Series page listing every series with a member in my list: per-series card figures, sorting, filtering, its read endpoint and its empty states. | series-page (one series), whose badge and reveal rules the cards defer to |
| `series-identity` | One shared resolution of a series' displayed title and picture, which titles may be chosen, search still matching member titles, choices surviving rebuilds, and none of it sent to MAL. | artwork-selection (picture options and the picker); series-page (the page itself) |
| `series-page` | Deriving a series from the relation graph (main line, extras, watch order, root id), persisting it, bounded builds and their triggers, and the series page: header, averages, stats, timeline, More section and rebuild. | series-versions (alternative versions and multi-series membership); series-browser (the list page); relation-confidence (edge trust) |
| `series-versions` | How version relations shape a series: story components only, version neighbours folded in or split off, alternative main-line versions sharing one slot with a default, builds storing only the seed's series, and an anime in several series with one primary. | series-page, whose composition it refines for version relations |

Siblings are written side by side so their Purposes meet cleanly. The groups are: the three series specs with series-identity; artwork-selection and artwork-presentation; score-presentation and score-visibility; connection-status, action-failure-notices and navbar-settings-status; page-state-restoration and frontend-data-loading.

## Risks / Trade-offs

- **[A Purpose restates requirements and goes stale on the next change]** → Decision 3's no-figures rule. Each Purpose says what the capability owns, not how that behaves.
- **[Sibling Purposes claim the same ground or leave a gap]** → Siblings are written together (Decision 5), and the verification step reads each group's Purposes side by side.
- **[A draft is copied while stale]** → The Decision 4 table is the checklist. The three rows marked stale must not keep their draft's wording.
- **[An accidental edit below `## Purpose`]** → `git diff --numstat -- openspec/specs` must show exactly 31 files at `1 1`, with every hunk at line 4.
- **[Tempted to fix requirement text on the way past]** → Out of scope. Findings go in the change summary for a separate change (see below).

## Migration Plan

None: this is documentation only. To roll back, revert the commit.

## Open Questions

None blocking. **Found below `## Purpose` while reviewing; not fixed here, for a separate change:**

- `series-page`'s "Series composition from the relation graph" and `series-versions`' "A series is a story component" both define a series as the connected component over the same story relations. The second refers back to the first. One of them should probably own the definition.

## Context

Both features are small, mostly-frontend refinements built on machinery that already exists.

- **Score hiding** is a client-only concern owned by `ScoreVisibilityContext`. It exposes `{ hidden, toggle }`, persists `hidden` to `localStorage` under `bettermal.scoresHidden`, and is consumed by `ScoreValue`, which — while hidden and not individually revealed — deliberately keeps the numeric value out of the DOM entirely (blur placeholder + reveal button only). The global toggle button lives in the Navbar. `ScoreValue` today takes only `{ value, placeholder }` and knows nothing about the entry a score belongs to.
- **My list** rows are rendered by `MyListPage.renderRow`, which reads `MyListItemDto`. The DTO carries `mediaType` but not airing status. The airing status itself already exists end-to-end on the cached record (`AnimeMetadata.AiringStatus`, values `not_yet_aired` / `currently_airing` / `finished_airing`) and is surfaced on the detail page via `AIRING_STATUS_LABELS` in `AnimeDetailPage.tsx`; it is simply not included in the my-list payload. `MyListService` projects entries into `MyListItemDto` field-by-field, so adding one field is a one-line change on each side.

## Goals / Non-Goals

**Goals:**
- Let a user opt in, from Settings, to always see MAL scores for shows they've completed even while scores are globally hidden — without weakening the no-leak guarantee for scores that stay hidden.
- Show Not aired / Airing / Aired next to the type on Plan-to-watch rows, driven by real data flowed through the my-list API.
- Keep the score-hiding model in one place (the context) and keep `ScoreValue` the single component that decides whether a score is visible.

**Non-Goals:**
- No change to the global hide toggle, its Navbar control, or its storage key.
- No airing indicator on non-Plan-to-watch rows, and no airing indicator elsewhere in the app (detail page already shows full airing status).
- No new backend column, migration, or MAL API call — the data already exists on `AnimeMetadata`.
- No server-side persistence of the new setting; like the global toggle it is a client preference.

## Decisions

### Decision 1: Extend `ScoreVisibilityContext` with a second persisted flag, not a new context
Add `alwaysShowCompletedScores: boolean` plus a `toggleAlwaysShowCompletedScores` (or `setAlwaysShowCompletedScores`) to the existing context, persisted under a distinct key `bettermal.alwaysShowCompletedScores`. Score-visibility state is already centralized here and `ScoreValue` already consumes it; a second context would fragment that.
- *Alternative considered:* a standalone settings context. Rejected — the two flags are the same concern (how MAL scores are shown) and are read together by `ScoreValue`.

### Decision 2: `ScoreValue` gains a `completed?: boolean` prop and computes effective visibility itself
`ScoreValue` reads both flags from context and accepts `completed`. Effective visibility becomes: show the value when `!hidden || revealed || (completed && alwaysShowCompletedScores)`. When the completed override applies, the score renders as a plain value with **no reveal button** (it is intentionally fully visible), preserving the existing no-leak path for everything still hidden.
- Callers pass `completed={entry?.status === 'Completed'}`. The prop defaults to `false`, so the many `ScoreValue` call sites that have no associated entry (browse/season/search cards, top-anime rows not in the list) are unaffected.
- *Alternative considered:* compute visibility in each caller and pass a `forceVisible` boolean. Rejected — it would duplicate the setting lookup across pages and split the visibility decision out of the one component that owns it.

### Decision 3: Thread `completed` only where a MAL score sits next to a known watch status
Concretely: `MyListPage` (`item.entry.status`), `AnimeDetailPage` (`detail.entry?.status`), and `TopAnimePage` (`entry?.status`). Other `ScoreValue` usages render without the prop and keep today's behavior. This keeps the override tied precisely to "a show the user has completed."

### Decision 4: Flow airing status through the existing DTO; centralize the short-label mapping
Add `string? AiringStatus` to the `MyListItemDto` record and populate it from `e.Anime.AiringStatus` in `MyListService`; add `airingStatus: string | null` to the frontend `MyListItemDto` type. Introduce a shared short-label map in `utils/anime.ts` — `not_yet_aired → "Not aired"`, `currently_airing → "Airing"`, `finished_airing → "Aired"` — returning `null` for unknown/missing values so the row simply omits the badge. The detail page keeps its longer labels ("Not yet aired", etc.); the list uses the terse ones the user asked for, so the two mappings stay separate but both live near `utils/anime.ts` conventions.
- *Alternative considered:* derive airing status on the client from aired-from/aired-to dates. Rejected — those dates aren't in the my-list payload either and MAL's own `airingStatus` is the authoritative value already cached.

### Decision 5: Render the badge inline with the type, only for Plan-to-watch
In `renderRow`, when `item.entry.status === 'PlanToWatch'` and the mapping yields a label, render it next to the existing `my-list-row__type` element (e.g. `TV · Airing`), styled as a small badge. Keeping it in `renderRow` avoids touching the grouped/ranked layout branches.

## Risks / Trade-offs

- **Stale airing status** → the badge reflects the last cached `AiringStatus`; a show that started airing since the last metadata refresh could read "Not aired" briefly. Acceptable: the background refresh and force-refresh already keep this field current, and the same value already drives the detail page.
- **Completed-score override could surprise a user who forgot it's on** → Mitigation: default off, label it explicitly on Settings, and scope it strictly to completed entries so hidden scores elsewhere never leak.
- **Two airing-status label maps (detail vs. list)** → minor duplication, but intentional: the detail page wants full phrasing and the list wants a compact badge. Both are trivial constant maps.

## Migration Plan

Additive and backward-compatible. Backend adds one optional field to the my-list response; an older frontend ignores it. Frontend adds two client-only preferences (new `localStorage` keys, default off/unset) that don't affect existing stored state. No migration or rollback steps beyond a normal deploy; reverting is a straight code revert.

## Open Questions

- None blocking. Minor UX polish (exact badge separator/placement and whether to color-code the airing badge) is left to implementation against the existing My list styling.

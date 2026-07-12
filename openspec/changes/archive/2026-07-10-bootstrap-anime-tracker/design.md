## Context

This is a greenfield, single-user, local-only anime tracker that mirrors my MyAnimeList (MAL) account. The local Postgres database is the source of truth for reads; MAL is the canonical account the local data is imported from and pushed back to. Because there is exactly one user (me) on one machine with no public deployment, there is no authentication, session, RBAC, or admin surface — every page is the editing interface.

Key external constraint: the official MAL API v2 is the only integration. It has two auth tiers (client-id for read endpoints; OAuth2 PKCE bearer token for user endpoints), a `plain` PKCE code-challenge quirk, long-lived tokens (~31 days), and undocumented burst throttling that returns `403` rather than a fixed quota. The design therefore centers on caching aggressively in Postgres and treating live API calls as rare, small, and paced.

Stakeholders: just me. The repo is also a portfolio piece, so it must run cleanly from `docker compose up`, keep secrets out of git, and be readable.

## Goals / Non-Goals

**Goals:**
- Local Postgres as the read source of truth — no page render ever blocks on a live API call.
- One-time interactive OAuth, then a resumable, progressive, paced first-boot import.
- Debounced per-entry write-back to MAL with a durable retry queue and a reconciliation safety net.
- Tiered, scheduled metadata/score refresh plus on-demand single-anime refresh.
- Correct JST→local (Finland) timezone conversion for all airing/broadcast displays.
- One-command Docker Compose bring-up that survives reboots and keeps data across rebuilds.

**Non-Goals:**
- Authentication, multi-user, RBAC, or a separate admin backend.
- Public/cloud deployment.
- Any anime API other than MAL v2 (no Jikan/AniList data fetching; AniList is only an outbound link).
- Real-time push/websocket sync — background jobs and debounced timers are sufficient.

## Decisions

### Architecture: three containers, background jobs in the backend
Backend = ASP.NET Core Web API (C#) + EF Core; Frontend = React SPA; Store = Postgres. All background work (import, write-sync, retry, refresh, reconciliation) runs as hosted services inside the backend process rather than as separate workers — a single user on one machine does not justify a separate scheduler service, and co-locating keeps DB access and MAL client reuse simple.
- *Alternative considered:* a dedicated worker container. Rejected as overkill for single-user scale and it complicates the compose file and shared code.

### MAL client with two auth paths
A single typed `IMalClient` exposes read methods (attach `X-MAL-Client-ID`) and user methods (attach `Authorization: Bearer`). A delegating `HttpClientHandler`/middleware injects the correct header per call and centralizes 403-backoff and pacing. Token acquisition/refresh is a separate service reading/writing the `OAuthToken` row.
- *Alternative considered:* two separate clients. Rejected — one client with per-endpoint auth selection avoids duplicated retry/pacing logic.

### OAuth2 PKCE with `plain` challenge, tokens in Postgres
Implement the PKCE flow explicitly rather than via a generic OAuth library default, because MAL requires `code_challenge_method=plain` (most libraries default to `S256` and fail silently). The callback is `http://localhost:{port}/callback`. Tokens live in the `OAuthToken` table (not memory/filesystem) so they survive restarts; a background refresh renews the access token ahead of its ~31-day expiry.
- *Alternative considered:* off-the-shelf OAuth middleware. Rejected due to the `plain` quirk and the desire to persist tokens in the DB explicitly.

### Caching-first data flow: two field tiers, my-list vs. browsed anime
Every page reads from Postgres. The API is touched only by: (1) initial import, (2) nightly tiered refresh, (3) on-demand single-anime refresh, (4) visit-triggered season/Top-Anime listing refresh, (5) search live-fallback for uncached titles, (6) write-sync pushes, (7) reconciliation. `AnimeMetadata.last_synced_at` / `last_score_synced_at` drive staleness decisions.

Metadata fetches split into two tiers:
- **Rich (full detail)** — genres, synopsis, background, studio, aired dates, broadcast schedule, prequel/sequel. Only ever fetched via initial import, the nightly tiered refresh (my-list anime only), or the first time a specific anime's own detail page is opened (or manually refreshed).
- **Lean (listing fields)** — title, picture, episode count, type, MAL score, rank/popularity. Used for the Season and Top Anime listing pages, since those pages show many anime that aren't in my list and mostly don't change once an anime finishes airing (studio, genres, aired dates are permanent; only score/rank keep moving). A lean upsert never overwrites existing rich fields on a row that already has them.
- *Alternative considered:* refreshing full detail for every anime shown on Season/Top-Anime pages. Rejected — it would mean N per-anime detail calls for anime nobody has added to their list, for fields that don't change once a show has finished airing; the lean listing call already returns score/rank in the same paginated request needed to render the page at all.

### Nightly tiered refresh: my-list only, four tiers, capped and ordered
The nightly refresh job's candidate query is restricted to `AnimeMetadata` rows with a corresponding `UserAnimeEntry` — Season/Top-Anime browsing populates `AnimeMetadata` too, but those rows are refreshed only via the lean, visit-triggered path below, never by this nightly job. Candidates are selected by tier: airing (every 1 day), finished within 60 days (every 3 days), finished 60 days–1 year ago or not yet aired (weekly), finished 1+ years ago (monthly). The job caps itself at a fixed batch (e.g. 500 calls/night), taking the most-stale-first candidates across tiers; any overflow beyond the cap simply refreshes on a later night since it remains the most stale.
- *Alternative considered:* the original two-tier "1–2 days / weekly-monthly" split. Replaced with four tiers for a closer match between refresh cost and how fast each tier's data actually moves.

### Season/Top-Anime listings: visit-triggered daily refresh, not a background job
The current season, the upcoming season, and the Top Anime ranking each re-fetch their lean listing fields the first time they're visited on a new local calendar day since their last fetch; same-day revisits serve straight from Postgres. A fully past/completed season, once cached, is never re-fetched again. A season or ranking never visited is never proactively fetched by anything in the background.
- *Alternative considered:* a nightly background refresh of all season/ranking data regardless of visits. Rejected — most of that data belongs to anime outside my list and nobody would see the refresh most nights; tying the refetch to actual page visits avoids wasted calls entirely.

### Write-sync: per-entry debounce with durable fallback
An in-memory per-anime debounce timer (8 seconds, fixed) coalesces rapid edits, but durability rests on the `pending_sync` flag in Postgres, not the timer. A background retry job periodically pushes any `pending_sync = true` entries, so a crash mid-debounce or a failed push is never lost. "Sync now" flushes the pending set immediately.
- *Alternative considered:* an outbox/queue table with a single drain loop and no in-memory timers. Viable and slightly more robust; the debounce-timer + `pending_sync` hybrid was chosen for simpler "one push per burst" behavior while still being crash-safe via the flag.
- *Alternative considered for the window itself:* tuning within the 5–10s range by feel. Fixed at 8s instead — correctness never depended on the exact value (the `pending_sync` flag and retry job are the real durability mechanism), so there's nothing to gain from leaving it open or making it configurable.

### Reconciliation: compute-then-review, not auto-apply
Weekly/manual full reconciliation pulls the complete MAL list and diffs it against local data, but does **not** apply differences immediately. Entries with `pending_sync = true` at diff time are excluded from that run's diff (an edit still in flight isn't a real conflict yet — it's revisited on the next run). The computed diff is persisted and surfaced on the settings page with **Accept** (apply everything in that diff) and **Cancel** (discard it; the next run computes fresh). This is a deliberate step up from a narrower "only guard entries with a pending edit" auto-apply design: since reconciliation is a rare, exceptional safety net rather than routine traffic, requiring an explicit look before anything changes costs little in practice and gives full visibility into an otherwise-invisible background operation, catching any divergence rather than only the one narrow race a pending-flag guard would catch.
- *Alternative considered:* auto-apply every non-conflicting diff and only pause on entries with `pending_sync = true`. Rejected in favor of reviewing every diff — simpler mental model ("reconciliation never changes anything without a look"), and the cost of an occasional glance at the settings page is low given how rarely reconciliation actually finds drift.
- *Alternative considered:* per-entry accept/reject inside the review screen. Deferred — a single accept-all/cancel-all action is enough for how infrequently this runs; per-entry granularity can be added later if bulk review ever proves too coarse.

### Timezone conversion at the edge
Store broadcast day/time as MAL provides it (JST). Convert to local (Finland) time when computing "airing today", weekly grouping, and slot times. Weekly grouping keys on the converted local day so day-boundary crossings land under the correct local day. Use a proper IANA timezone (`Europe/Helsinki`) to handle DST rather than a fixed offset.

### Score hiding on the client
The global hide toggle is client-side UI state. When on, MAL scores render as a blur and the actual value is not placed in the DOM text; per-score reveal is transient component state that resets on navigation (not persisted).

### Search: local word-boundary prefix, live fallback covers the rest
The local-cache-first search stage matches if the query is a prefix of the title or a prefix of any word within the title (so "titan" matches "Attack on Titan," not just "attack"). No local fuzzy/typo matching is implemented. When nothing matches locally, the existing live-API fallback runs MAL's own (fuzzier) search, so typo/substring coverage comes for free without building it locally.
- *Alternative considered:* local fuzzy matching (trigram similarity, edit distance) over cached titles. Rejected for v1 — added complexity for marginal benefit at personal-library scale, when the live fallback already covers the cases prefix matching misses. Could be added later (e.g. `pg_trgm`) if plain prefix matching ever proves annoying in practice.

### UI interaction patterns from the pages brief
The hand-drawn layout sketches refined several page interactions, adopted here:
- **Edit/add-to-list is always a modal overlay** (MAL-style) opened on top of the current page and closed on Esc/click-outside — one reusable editor component used by the my-list, top-anime, season, detail, and dashboard surfaces. The editor exposes episodes, status, score, and rewatch count but **no start/finish date fields** (those are set by the automatic date logic).
- **Currently-watching increment moves to a dedicated plus control**; clicking the card body navigates to the detail page. This separates "advance an episode" from "open the anime," which were conflated in the original text.
- **Empty states are explicit**: "nothing airing today" on the dashboard, empty day-columns and a centered "nothing airing this week" on the airing page.
- **Settings gets a navbar entry point** (gear icon on the right), closing the gap where the settings page had no navigation to it.
- **Top anime is a global ranking**, so each row carries a conditional Add/Edit action depending on whether the anime is already in my list.

### Data-model additions from the pages brief
The sketches imply a few fields the original model did not carry:
- **Detail-page metadata** — genres, synopsis, and background text on `AnimeMetadata`, fetched during import/refresh.
- **Related-anime references** — prequel/sequel MAL ids + titles on `AnimeMetadata`, so the detail page can show relation links (shown only when they exist). No extra API integration — these come from MAL's related-anime fields.
- **Manual top-anime selection** — persisted storage of which tied next-highest-scored anime the user pinned into the remaining "My top anime" slots. This replaces the old automatic alphabetical tiebreak: the default is a deterministic next-highest auto-fill, and a persisted manual selection (when present) takes precedence.
- **Next-episode countdown** is *computed*, not stored — derived from the cached JST broadcast schedule via the existing local-time converter.
- **Add-to-list** is a create path (new `UserAnimeEntry`, default status **Plan to watch**) reusing the existing debounced-sync push; MAL's `PATCH my_list_status` creates the entry server-side. After adding, the row's Add button flips in place to **Edit**, which opens the editor overlay on the same page like every other edit action — no navigation.

### Progressive import + resumability
Import pages `@me/animelist` (~100/page), then fetches per-anime metadata at ~1 req/s, inserting each record as it completes so the UI fills in progressively. Resumability is achieved by skipping anime already present in `AnimeMetadata`; a visible "X / N synced" indicator reflects progress.

## Risks / Trade-offs

- **[Undocumented 403 burst throttling]** → Conservative pacing (~1 req/s), request only needed fields on refresh, spread nightly refresh into small batches, and treat 403 as backoff-and-retry rather than failure.
- **[PKCE `plain` quirk fails silently]** → Set `code_challenge_method=plain` explicitly and cover the token exchange with a focused test; document it in setup.
- **[Lost writes on crash/offline]** → Durability lives in `pending_sync` in Postgres, not the debounce timer; a retry job drains pending entries, and reconciliation is the final safety net.
- **[Out-of-band edits on MAL's own site cause drift]** → Weekly/manual full reconciliation diffs the whole list and holds the result for review (Accept/Cancel) on the settings page, rather than silently applying it.
- **[Timezone/DST errors]** → Use IANA `Europe/Helsinki` conversion, not a fixed offset; group weekly view by converted local day.
- **[Secrets leaking into a public repo]** → `.env` is gitignored, tokens live only in the DB volume, and the repo carries a `.env.example` with no real values.
- **[Refresh job hammering the API on a large library]** → Four staleness tiers plus a fixed nightly batch cap (most-stale-first, overflow rolls to the next night) keep normal usage far below any burst threshold; Season/Top-Anime browsing never adds to this load since it refreshes only lean fields on visit, not via the nightly job.

## Migration Plan

Greenfield, so "migration" is first-run bootstrap:
1. `docker compose up` starts Postgres (named volume), backend, frontend with `restart: unless-stopped`.
2. EF Core migrations create the schema on backend start.
3. User completes the one-time MAL authorization at the local callback.
4. Initial import runs in the background, progressively populating Postgres; UI shows progress and is usable as data fills in.
5. Ongoing debounced sync, retry, refresh, and reconciliation jobs run continuously thereafter.

Rollback: since data lives in the Postgres named volume, container rebuilds are safe; a bad schema migration is recovered by fixing the migration and re-applying. Re-authorization is available on the settings page if the refresh token ever becomes invalid.

## Open Questions

None open at this time — all items previously tracked here have been resolved (see below).

**Resolved:**
- The "My top anime" tiebreak is no longer alphabetical — the default is a deterministic next-highest auto-fill with an optional persisted manual selection (see Decisions).
- Add-to-list applies a default status of **Plan to watch** and adds immediately (no pre-filled editor); the button then flips in place to **Edit**, which opens the editor overlay on the same page (see Decisions).
- Debounce window is fixed at **8 seconds**, not left open within the 5–10s range (see *Write-sync* Decision).
- Staleness thresholds are four fixed tiers (airing: 1 day; finished <60 days: 3 days; 60 days–1 year or upcoming: weekly; 1+ years: monthly), scoped to my-list anime only, with a 500-call nightly batch cap ordered most-stale-first (see *Nightly tiered refresh* Decision). Season/Top-Anime listings use a separate visit-triggered lean refresh instead of the nightly job (see *Season/Top-Anime listings* Decision).
- Search matching strategy is local word-boundary prefix matching, with MAL's live search as fallback for everything else (see *Search* Decision).
- Reconciliation computes a diff and holds it for review rather than auto-applying; the settings page offers Accept/Cancel on the whole diff, with entries mid-debounce (`pending_sync = true`) excluded from that run (see *Reconciliation* Decision).

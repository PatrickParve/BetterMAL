## Context

`AnimeSearchService.SearchAsync` does three things per type-ahead request: read the whole anime title index out of Postgres, run a live MAL search, and load the series-member index. It then merges, ranks, and returns the top 5. The `await` on the MAL call is in the critical path of a control the user is typing into.

Measured on the running instance (14,080 rows in `AnimeMetadata`, 1,553 series members across 246 series, backend idle):

| | |
|---|---|
| `GET /api/anime/search?q=att` \| `attac` \| `attack` | 442 ms \| 899 ms \| 962 ms |
| The same endpoint on a query that returns before any I/O (`q=!!!`) | 3 ms |
| The local title-index query, in Postgres | 8.5 ms |

So essentially all of it is the MAL call, and the local half — which is what the user is usually looking for, since anything they have watched or browsed is already cached — is already sitting there in single-digit milliseconds. Two things make the live half worse than the table suggests:

- **`MalRequestPacer`** gates *every* outbound MAL call in the process to one per second through a single semaphore. A type-ahead search that arrives while the nightly metadata refresh, a series build, or a season fetch is running waits behind it. The 442–962 ms above is the uncontended case.
- **Submitting doubles the cost.** `SearchPage` fetches `/api/anime/search/page`, which runs its own `SearchAnimeAsync(term, 100)`. If the type-ahead's live request for the same term is still in flight, the results page's request is paced a full second behind it — and it is the *same query*, asked twice.

The second problem is unrelated in mechanism and connected in code. `useAnimeSearch` guards against a late response reopening a dismissed dropdown by recording the query that was dismissed:

```ts
function dismiss() {
  dismissedQueryRef.current = debouncedQuery   // ← the debounced query, not the submitted one
  setOpen(false)
}
```

Pressing Enter before the 250 ms debounce fires is exactly the case where `debouncedQuery` is a stale prefix. The dismissal is filed under `"attac"`, the response arrives for `"attack"`, `dismissedQueryRef.current !== debouncedQuery`, and `setOpen(true)` runs — over the results page. `navigation-and-search` already requires this not to happen ("A late response does not reopen the dropdown"); the guard keys on the wrong string, and no key derived from the debounced value can be the right one, because the debounce is the thing that is behind.

## Goals / Non-Goals

**Goals:**

- The dropdown's first paint stops waiting on MAL. Target: cached matches on screen within ~160 ms of the last keystroke, against 0.7–1.2 s today.
- The dropdown's final contents — which rows, in which order, with which series pinned on top — are exactly what it shows today. The staging is about *when* rows appear, not which.
- Opening the dropdown becomes a user action with no timing in it. No response, no debounce, no navigation can open it.
- Submitting a search costs the app fewer MAL calls than it does today, not more.
- No response-shape change, no schema change, no migration.

**Non-Goals:**

- A priority lane in `MalRequestPacer` for interactive requests. Every MAL call in the app shares that component, and with the local stage in place what it would rescue is only the speed of the *upgrade* from cached rows to merged rows.
- Holding the title index in memory. It is 8.5 ms for 14k rows; an in-memory copy would need invalidating on every metadata write, every import, every season fetch, for single-digit milliseconds.
- Pushing normalized matching into SQL. `SearchTextMatch` normalizes case, spacing, punctuation and accents in C#; doing it in Postgres means a stored normalized column, a migration, and an index — a real change to make if the local stage ever stops being fast, and unjustifiable while it is 8.5 ms.
- A leaner MAL field set for search requests. `mal-api-integration` fixes the listing field set (including `rating`) across season, search and ranking; trimming it for search alone is a spec change for a payload saving that is noise beside a one-second pacer wait.
- The full search results page's own behaviour, ordering, filters, fallback, or empty states.

## Decisions

### D1 — Two stages behind one endpoint, selected by `stage`

`GET /api/anime/search?q=…` keeps its meaning: the merged local + live ranking. It gains `&stage=local`, which runs the same ranking over the local index and the series index only, with no MAL call in the path. Both return the same `AnimeSearchResultDto` rows under the same 5-row budget and the same 2-series cap, so the client can render either without knowing which it got.

The split lives in `SearchAsync` rather than in a second method: the ranking, the exact-match (`"…"`) handling, the series matching and the row budget are one algorithm over a candidate set, and the only difference between the stages is whether the MAL candidates are in that set. The signature becomes `SearchAsync(query, limit, bool includeLive, ct)`, and the MAL block is skipped when `includeLive` is false.

*Alternative rejected — a separate `/api/anime/search/local` route.* Cleaner-looking, but it either duplicates the ranking or forwards to the same method with a flag, which is what the parameter does with one route to document instead of two.

*Alternative rejected — one request that streams two payloads (NDJSON or SSE).* It is the shape this problem is usually solved with, and it would save a round trip. It also means a chunked response reader on the client, a hand-rolled framing format, and giving up `fetchJson`'s error handling and the `AbortController` semantics the hook already relies on — for a round trip that costs 3 ms against a first stage that costs 40.

### D2 — The dropdown is a gate the user opens, not a state the network sets

`dismissedQueryRef` goes away. In its place the hook holds one boolean — is the dropdown wanted — and only two things set it true:

- the user typed in the field, and
- the user focused the field.

Nothing else can. A response's job is now to fill `results`; whether the dropdown is *shown* is `open && results.length > 0`, and `open` was decided by an interaction. That makes the reported bug structurally impossible rather than guarded against: Enter sets it false and blurs, and the field then survives the navigation, the search page writing the URL's query back into the input, and any response landing afterwards, without reopening — regardless of what the debounce was holding when Enter was pressed.

This also drops a subtlety the old code had to reason about ("scoped to that one query, not sticky"). Typing sets the gate true, so typing after a submission brings suggestions back on its own, with no per-query bookkeeping to get right.

`SearchBar` and `SettingsPage`'s `AnimeRefreshPicker` both change `onFocus={() => results.length > 0 && reopen()}` to `onFocus={reopen}` — with D5 below, focus is what *starts* a search, so gating focus on already having results would mean a field that can never fill.

**Stale rows on reopen.** `results` outlives a dismissal, which is what makes refocusing instant. If the query changed while the dropdown was shut — the search page writing `?q=` into the field is exactly that — those rows belong to a different query. `reopen()` therefore clears `results` when the query they were fetched for is not the current one, so a reopened dropdown either shows rows for what is in the field or shows nothing for ~160 ms. Between keystrokes, where the query changes while the dropdown is *open*, rows stay on screen as they do today rather than blanking on every character.

### D3 — Two debounces, and why "local leads" makes ordering trivial

The stages have costs that differ by two orders of magnitude, so they get their own debounces: **120 ms** for the local stage, **300 ms** for the live one. The local one can be aggressive because a miss costs ~10 ms of Postgres; the live one is longer than today's 250 ms because a miss costs a slot in a one-per-second global queue.

Both derive from the same trimmed query through `useDebouncedValue`, and 120 < 300, so for any settled query the local value commits first and the live value commits second, never the reverse. That invariant is what keeps the merge rule to one line (D4) with no sequence counters: *the local stage is never behind the live stage.*

Sequence: last keystroke → +120 ms local request → ~+160 ms local rows on screen → +300 ms live request → ~+1.2 s merged rows replace them (or immediately, on a cache hit).

### D4 — Which response wins

Each stage's effect aborts its own in-flight request when its debounced query changes, so a response for a query the client has moved past is normally never delivered. The one window the aborts do not cover is 120–300 ms after a keystroke, where the local stage has already advanced to the new query and the live stage is still in flight for the old one. So:

- A **local** response is always accepted. It is the freshest stage by construction (D3).
- A **live** response is accepted only if the query it was issued for is still the query whose rows are on screen. If the local stage has moved on, it is dropped.

The hook records the query behind the displayed rows on every accepted response, which is the same value D2 uses to decide whether reopening should clear them.

### D5 — A shut dropdown issues no requests

Both effects are gated on the dropdown being wanted. When it is dismissed — submit, Escape, click-outside, picking a row — the effects tear down and their `AbortController`s fire.

This is not just saved work. The abort reaches the server as `HttpContext.RequestAborted`, which cancels the request's wait inside `MalRequestPacer`, which releases the pacer for whatever is behind it — and immediately behind it, when the dismissal was a submit, is the results page's own search. Today the type-ahead's live request and the results page's request are two paced calls for the same query, a second apart, and the page waits for both. After this change, submitting cancels the first so the second goes straight through — or, with D6 and D7, finds the answer already in cache and makes no MAL call at all.

The cost is that focusing an empty-result field must be able to *start* a search rather than only reveal one, which is why `onFocus` becomes an unconditional `reopen` (D2).

### D6 — One candidate count, 60, for every live search

The app currently holds three different opinions about how many candidates a query has, and they disagree:

| | today | after |
|---|---|---|
| `SearchAsync` asks MAL for | 25 | **60** |
| `SearchPageAsync` asks MAL for (`MaxResults`) | 100 | **60** |
| the controller clamps the page's `limit` to | 90 | **60** |
| `SearchPage.tsx` asks the endpoint for (`CANDIDATE_LIMIT`) | 90 | **60** |

So the results page fetches 100 rows from MAL, ranks all 100, and can never show more than 90 of them — ten are fetched and discarded on every search — while the dropdown makes a *second*, differently-sized request for the same query that neither can reuse.

One figure, 60, replaces all four. Sixty is chosen because it is past the point where anyone scrolls a result grid and past the number of genuine matches almost any query has; the queries that legitimately have more (a long-running franchise, a one-word title) were already truncated at 90, and the difference between "truncated at 90" and "truncated at 60" is a tail nobody reaches. Named once and referenced from both call sites, so the three-way disagreement cannot come back.

Two things follow, and they are the reason this sits in the change rather than being a separate tidy-up:

- **The two live searches become one identical request**, which is what makes D7's cache shared rather than two entries that never help each other: **type a query, press Enter, and the results page renders from the type-ahead's cached response with no MAL call and no pacer wait.**
- **The dropdown's candidate set widens from 25 to 60.** A title MAL ranks 40th for the query can now reach the dropdown where before it could not. That is a strictly better answer under the ranking `navigation-and-search` already specifies (prefix first, then contains, each by popularity) — the dropdown was ranking a truncated candidate list — and it makes the dropdown and the results page agree about what exists.

The MAL payload shrinks for the results page (100 → 60 nodes) and grows for the dropdown (25 → 60), for a net reduction across the pair, since the pair is now one request.

*Alternative rejected — leave the type-ahead at 25 and key the cache by (query, limit).* Correct, and it never shares an entry with the results page, which is where the largest single win in this change is.

*Alternative rejected — keep 100/90 and align the dropdown upward.* It shares a cache entry just as well, but it preserves both the ten-row waste and a candidate set larger than the page can display, for a tail that is not reached.

### D7 — A 60-second in-memory cache for live search responses

`MalSearchCache`: a singleton holding `query → (expiry, edges)`, 60-second TTL, capped at 50 entries with expired entries swept on write and the oldest evicted at the cap. `SearchMalAsync` consults it before dispatching and populates it on success; failures are not cached, so a MAL blip does not stick for a minute.

Sixty seconds is chosen to cover the interaction it exists for — type, read the dropdown, press Enter — with room for a second look, and to be short enough that "MAL results are a minute stale" is not a claim worth reasoning about for a search box.

Capped at 50 because an entry is 60 lean anime nodes (D6); at roughly 1 KB each that is ~3 MB worst case, which is a fine trade for removing a paced network call from the two most common search interactions.

It is a singleton because the service is scoped per request and the whole point is sharing across requests. It sits in `Services/Mal/` beside the pacer and the auth handler rather than in `Services/Search/`, because it is a fact about the MAL client's traffic, not about ranking.

*Alternative rejected — `IMemoryCache`.* It would work and it is one line of registration, but the app has no `IMemoryCache` anywhere today; a 40-line purpose-built class with a name that says what it holds is easier to reason about than introducing a general cache abstraction for exactly one use.

*Alternative rejected — caching the ranked results instead of the MAL edges.* The two callers project the same edges differently (the dropdown takes title/picture/popularity; the page also takes episodes, media type and score) and rank them differently. Caching the raw response is the one thing both can reuse.

### D8 — A cancelled request is abandoned, not recorded as a MAL failure

`SearchMalAsync` catches `Exception`, which includes the `OperationCanceledException` thrown when the client goes away — so every abandoned keystroke logs `"Live MAL search failed for query …"` and then carries on doing local ranking and two more database round trips for a response nobody will read. With D5 aborting far more requests than today's code does, that noise would grow. The catch rethrows when the cancellation is the request's own token; every other exception keeps degrading to local-only exactly as it does now, because that is what keeps a MAL blip from taking down the search box.

### D9 — Only the merged stage schedules a series build

`navigation-and-search` requires at most one background series build per search, for the top-ranked anime match. A query now produces two requests, so the local stage schedules nothing and the merged stage keeps doing exactly what it does today. This is also the more correct half: the top-ranked match over local candidates only is not necessarily the top-ranked match, and the trigger's whole purpose is to build franchises the app does not have yet — which is the case where the local stage has the least to say.

`SeriesBuildTrigger` already de-duplicates per app run, so this is belt-and-braces rather than the only thing standing between the app and a build per keystroke — but "one build per search" should stay true by construction, not by a downstream guard.

### D10 — The local stage answers with series too

The local stage returns the same shape as the merged one: up to 2 matched series pinned first, then the top local anime, 5 rows total. The series index is stored data, and it is precisely the "anime and series that are quickly gotten from db" that the local stage exists to deliver — a first paint that omitted series only to have them appear a second later would be a worse flicker than not staging at all.

## Risks / Trade-offs

**Rows can reorder under the pointer when the merged stage lands** → The dropdown's contents change once per query, ~1 s in, and the user may be moving toward a row. The window is real but far narrower than today's (today the *entire* list appears at that moment, so there is nothing to mis-click before it); in the common case the local rows are already the merged rows' top entries, because a title the user recognises is usually one the app has cached. Freezing the list while the pointer is over it was considered and rejected: it makes the dropdown's contents depend on pointer position, which is harder to reason about than the thing it prevents.

**A query with no cached matches shows nothing for ~1 s, then rows** → Same total wait as today, no worse; the dropdown simply stays empty rather than showing rows it does not have. It is not distinguishable from "no matches" while it lasts. Accepted rather than adding a loading state to a control whose fast path is now 160 ms.

**Two HTTP requests per query instead of one** → The extra one is the ~40 ms local call; the MAL call count per query is unchanged (D6 changes its size, not its number), and D5 removes one MAL call per submitted search. Net MAL traffic goes down.

**A cached live response can be up to 60 s stale** → For a search box over a catalogue that changes on a nightly refresh cycle, a minute is not observable. The cache holds only MAL's *search* responses; nothing is persisted from them, so no stale data can reach storage.

**Widening the dropdown to 60 candidates changes which rows can appear** → It widens the candidate set the existing ranking runs over, so any row that appears now is one the specified ranking always preferred and a truncated candidate list was hiding. Called out in the spec delta so it is a decision on the record rather than a silent drift.

**The results page now shows at most 60 anime for a query, down from 90** → Real for the handful of queries that have more than sixty genuine matches — a one-word title, a long franchise — where the tail is now unreachable rather than reachable by scrolling. It was already unreachable past 90, so this moves a boundary rather than introducing one, and the far side of it is a relevance-ranked tail nobody scrolls to. Two smaller consequences ride along: the Type filter chooses from the media types present in 60 candidates rather than 90, and the count line tops out at 60. Both are stated in the spec delta.

**Focus now starts a search** → A user tabbing through the navbar with text left in the field triggers a local query (~10 ms) and, 300 ms later, a live one. Tabbing straight past costs one aborted local request; lingering costs one paced MAL call, which is what focusing a search box with a query in it should cost.

## Migration Plan

Deploy in one piece; there is no data to migrate and no persisted state involved. The endpoint change is additive — `stage` defaults to the merged behaviour, so an older frontend against a newer backend behaves exactly as it does today, and a newer frontend against an older backend would receive merged results for both stages (correct, just not fast). Rollback is a revert.

## Open Questions

None.

## Why

The navbar search is the slowest control in the app and the only one that opens itself uninvited.

**It is slow.** Every keystroke-debounced type-ahead request waits for a live MAL search before it answers anything. Measured against the running instance (14,080 cached anime, 246 stored series, no background job competing): `GET /api/anime/search` takes **440–960 ms**, of which the local half — the whole-table title projection the ranking merges in — is **~8 ms** of Postgres time. Add the 250 ms debounce and the dropdown appears roughly **0.7–1.2 s** after the user stops typing, for results the app already had in its own database within ten milliseconds. It is worse than that in practice, because every outbound MAL call passes through a single global pacer that admits one request per second across the whole process: a type-ahead search that lands behind a nightly refresh, a series build, or a season fetch waits its turn first. And submitting the search makes it worse again — pressing Enter fires the results page's own MAL search *behind* the type-ahead's, so the two are serialised a second apart.

**It opens itself.** Type a title and press Enter before the 250 ms debounce has fired, and the dropdown appears over the search results page you just landed on, and stays there until you click it away. `useAnimeSearch` tries to prevent exactly this: `dismiss()` records the query being dismissed so a response already in flight cannot reopen the dropdown. But it records the **debounced** query, and pressing Enter early is precisely the case where the debounced query is still a stale prefix — so the dismissal is filed against `"attac"` (or `""`), the response arrives for `"attack"`, the keys don't match, and the dropdown opens. The existing spec already forbids this ("A late response does not reopen the dropdown"); the implementation keys the guard on the wrong string.

## What Changes

**The dropdown opens only when the user asks for it**

- Opening the dropdown becomes something only a user interaction can do — typing in the field, or focusing it. A search *response* never opens it. This replaces the per-query dismissal bookkeeping, which was trying to infer the same rule from timing and got the timing wrong.
- Pressing Enter (or clicking the magnifier) therefore leaves the field blurred and the dropdown shut, and it stays shut through the navigation, through the search page restoring the query into the field from the URL, and through any response still in flight — whatever the debounce happened to be holding at the moment Enter was pressed. Clicking back into the field brings the suggestions back.
- While the dropdown is shut, the type-ahead issues no requests at all, and cancels any it has outstanding. Pressing Enter now *frees* the pacer for the results page's own search instead of making it queue.

**The dropdown answers from the database first, then upgrades**

- The type-ahead endpoint gains a local stage: `GET /api/anime/search?q=…&stage=local` ranks and returns matches from the app's own cache and its stored series, with no MAL call in the path. The existing (default) call is unchanged and still returns the merged local + live ranking.
- The frontend fires both, on two debounces — the local one short (120 ms), the live one longer (300 ms), since local costs a ~10 ms query and live costs a paced MAL call. The dropdown paints the local rows as soon as they land and replaces them with the merged ranking when it arrives. First paint goes from ~0.7–1.2 s to roughly **160 ms**; the final contents and ordering are exactly what the dropdown shows today.

**One live search of 60 candidates, shared by the dropdown and the results page**

- The results page's candidate set drops from **100 fetched — of which it could only ever show 90** — to **60 fetched and 60 shown**. Only a handful of queries have sixty genuine matches at all, and sixty cards is well past where anyone scrolls; today's tail is fetched from MAL, ranked, and then discarded. The three places that number lives (the MAL fetch, the endpoint's clamp, and what the page asks for) become one figure instead of three that disagree.
- The type-ahead's live stage asks for that same 60-candidate search, so the two make one identical request — which is what lets them share a cached response.
- Live MAL search responses are cached in memory for 60 seconds, keyed by the query. So typing a query and then pressing Enter renders the results page **from cache, with no MAL call and no pacer wait at all**, where today it costs a fresh paced request. Retyping, backspacing back onto a query, or returning to a search within the minute are likewise free.
- A client that gives up on a search (the user typed another character, or dismissed the dropdown) cancels the request; the server stops treating that cancellation as a MAL failure to log and recover from, and simply abandons the request — which releases the pacer for whatever is queued behind it.

**Deliberately not changed.** The pacer stays first-come-first-served — no priority lane for interactive searches over background jobs. It would help, but it is a change to a component every MAL call in the app shares, for a benefit the local stage already delivers: the dropdown no longer waits on MAL at all, so what it would rescue is only how quickly the merged rows *upgrade* the local ones. The local stage also keeps reading the title index from Postgres on every request rather than holding it in memory; at ~8 ms for 14k rows there is nothing to win, and an in-memory index would need invalidating on every metadata write.

## Capabilities

### New Capabilities

None. This changes how two already-specified behaviours perform and when the dropdown is allowed to open, not what the search can do.

### Modified Capabilities

- `navigation-and-search`:
  - "Type-ahead search, merged local + live ranked by prefix and popularity" gains the two-stage delivery — cached matches shown first, merged ranking replacing them — while its ranking, its 5-row budget, and its series rows stay exactly as specified.
  - "Search submission keeps the query text" replaces its "the dismissal holds against an in-flight response" clause with the stronger and simpler rule it was approximating: only typing or focusing opens the dropdown, so no response can open it regardless of what was in flight or how far the debounce had got.
  - "Searching schedules a series build for an unknown franchise" is clarified for two-stage delivery: the local stage schedules nothing, so "at most one build per search" still counts one build per query, not one per request.
  - "Full search results page" states the bounded candidate set — the page lists up to 60 anime for a query rather than literally "every anime matching" — so the cap is on the record rather than an implementation detail three files disagree about.

- `mal-api-integration`: a new requirement that live search responses are briefly cached and shared across the type-ahead and the results page, and that a cancelled request is abandoned rather than recorded as a MAL failure.

## Impact

**Backend** (`backend/AnimeTracker.Api/`)

- `Controllers/AnimeSearchController.cs` — the `stage` parameter on the type-ahead route, and the results page's clamp lowered to 60.
- `Services/Search/IAnimeSearchService.cs`, `Services/Search/AnimeSearchService.cs` — the local-only path, the single 60-candidate figure shared by both live callers, cancellation no longer swallowed as a MAL failure, and series-build scheduling confined to the merged stage.
- **New** `Services/Mal/MalSearchCache.cs` — the 60-second in-memory cache, registered as a singleton in `Program.cs`.
- `AnimeTracker.Api.Tests/Services/Search/AnimeSearchServiceTests.cs` — local-stage coverage; new cache tests.

**Frontend** (`frontend/src/`)

- `hooks/useAnimeSearch.ts` — the two-stage fetch, the interaction gate replacing `dismissedQueryRef`, and no requests while shut.
- `api/client.ts` — `searchAnime` takes the stage.
- `components/SearchBar.tsx` and the settings page's `AnimeRefreshPicker` (`pages/SettingsPage.tsx`) — open the dropdown on typing as well as on focus, and focus alone is now enough to start a search.
- `pages/SearchPage.tsx` — `CANDIDATE_LIMIT` from 90 to 60. `CHUNK_SIZE` stays 48, so the page still reveals in two chunks and continuous scroll still has something to do.

No schema change, no migration, no new dependency, and no change to any response shape — `stage=local` returns the same `AnimeSearchResultDto` rows the merged call returns.

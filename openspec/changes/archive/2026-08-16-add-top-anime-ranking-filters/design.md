## Context

Today the Top anime page is single-list. `TopAnimeService.GetRankingAsync()` takes no parameters, `TopAnimeRankingEntries` is keyed on `AnimeId` alone, `TopAnimeFetchLogs` holds exactly one row ("at most one row ever exists", per its own doc comment), and `RefreshGate` is locked on the literal string `"top-anime"`. Every one of those is a place where "the ranking" is assumed to be singular.

`MalClient.GetRankingAsync(rankingType, limit, ct)` already accepts a ranking type and defaults it to `"all"` — the client layer needs no change at all. Everything above it does.

On the frontend, `TopAnimePage.tsx` deliberately does *not* use `usePageData`: that hook keys its cache by location, and because the page number lives in the URL (`?page=N`), every page change would look like a new resource and re-fetch. Instead the page holds a module-scoped `cachedItems` + `inFlightLoad` pair, fetched at most once per app session. That reasoning survives this change intact — it just needs to become per-ranking-list rather than global.

Constraints carried in from the existing specs:
- `metadata-refresh`: Top Anime refreshes only on visit, at most once per local calendar day, lean listing fields only, never proactively; and at most one live fetch per subject in flight at a time.
- `page-state-restoration`: view-control selections restore with the page on back/forward, and open on their documented default on a fresh visit. This change adds a view control, so it inherits that requirement rather than restating it.
- MAL has no published rate limit and throttles bursts with a 403; `MalRequestPacer` spaces requests out.

## Goals / Non-Goals

**Goals:**
- Seven selectable ranking lists on the Top anime page, defaulting to All, with the active one visibly selected.
- The selected list survives back/forward navigation.
- Switching between lists already seen this session is instant; switching to an unseen one never blanks the page.
- Each list refreshes on its own daily clock, independently of the others.

**Non-Goals:**
- ONA and Music lists. MAL API v2 rejects them (see D1) and the request explicitly rules out a second data provider to cover the gap.
- `airing` and `upcoming` lists. MAL supports them, but they were not requested.
- Prefetching lists the user has not asked for. The "never proactively fetched" rule in `metadata-refresh` applies per list, and speculatively warming seven lists would multiply MAL traffic sevenfold for no asked-for benefit.
- Changing the three-tier (showcase / card row / flat row) presentation, the pagination model, or the flat-row Add/Edit action.

## Decisions

### D1. The seven lists are exactly what MAL API v2 supports, of what was asked for

Verified against the live API with this project's `X-MAL-CLIENT-ID`, `GET /v2/anime/ranking?ranking_type=…&limit=1`:

| Requested | `ranking_type` | Result |
| --- | --- | --- |
| All | `all` | 200 |
| TV | `tv` | 200 |
| Movie | `movie` | 200 |
| OVA | `ova` | 200 |
| Special | `special` | 200 |
| Popularity | `bypopularity` | 200 |
| Favourite | `favorite` | 200 |
| ONA | `ona` | **400 `{"message":"invalid ranking_type"}`** |
| Music | `music` | **400 `{"message":"invalid ranking_type"}`** |

So Favourite and Popularity — the two the request flagged as uncertain — are both available; ONA and Music are the two that are not, and they are dropped rather than sourced elsewhere. `limit=500` was also confirmed to return 500 rows for `favorite`, so every list carries the same 500-row depth the page already paginates.

MAL spells it `favorite`; the UI label is "Favourite", matching this codebase's existing en-GB usage (`SeriesMemberFavouriteRank`). The wire value stays MAL's spelling so no translation layer is needed.

### D2. `RankingType` becomes part of the key of both persisted tables

`TopAnimeRankingEntries` is re-keyed from `AnimeId` to `(RankingType, AnimeId)`; `TopAnimeFetchLogs` drops its surrogate `Id` in favour of `RankingType` as the key. This mirrors `SeasonFetchLog`/`SeasonAnimeListing`, which are already keyed `(Year, Season)` and `(Year, Season, AnimeId)` respectively — the same "one cached listing per selector value" shape, so the codebase gains no new pattern.

The type is stored as a short string (MAL's own `ranking_type` token), not an enum column, matching how `SeasonFetchLog.Season` stores `winter`/`spring`/…. A C# side is still typed: a `TopAnimeRankingType` static class holding the allow-list and the label mapping, so nothing in the codebase spells the tokens by hand at more than one site.

*Alternatives considered:* one table per list (multiplies migrations and repository code for no benefit); a single JSON column holding all seven rankings (loses the `AnimeMetadata` foreign key that keeps ranking rows cascading away with their anime).

### D3. Freshness, single-flight, and fetch failure are all per list

`EnsureFreshAsync` takes the ranking type; `IsFreshAsync` reads that type's own fetch-log row; the `RefreshGate` key becomes `$"top-anime:{rankingType}"`. Consequences that matter:

- Viewing Movie does not mark All as fetched for the day, and vice versa.
- A failed Movie fetch does not block or consume All's daily fetch.
- Two concurrent requests for *different* lists proceed in parallel — they are different subjects, so collapsing them would be wrong; two concurrent requests for the *same* list still collapse into one, preserving the existing `metadata-refresh` guarantee.

Worst case MAL traffic rises from one ranking fetch per day to seven — but only for a user who actually opens all seven lists, and `MalRequestPacer` already spaces them.

### D4. `type` is a query parameter on the existing endpoint, validated server-side

`GET /api/top-anime?type=movie`, with `type` omitted meaning `all`. An unrecognised value returns `400`, rather than silently falling back to All — a silent fallback would render the All list under a highlighted "Movie" button, which is worse than an error. Validation is an allow-list check against `TopAnimeRankingType`, never string interpolation of user input into the MAL URL.

*Alternative considered:* `GET /api/top-anime/{type}`. Rejected because the query form keeps the default (`/api/top-anime`) working unchanged and matches how `/api/season/...?type=` and `/api/profile/top-anime?mediaType=` already express a view selector in this API.

### D5. The selected list lives in the URL, next to `page`

`?type=movie&page=3`. This is the same mechanism the page already uses for `page`, and it is what makes "the list you were on stays when going back and forth" true without any new persistence: back/forward restores the URL, and `page-state-restoration`'s view-control requirement is satisfied structurally. Arriving via the navbar link (`/top-anime`, no params) is a fresh visit and opens on All, as that same spec requires for a fresh visit.

Selecting a list **pushes** a history entry (it does not replace one), consistent with how changing pages already behaves here, so back steps from Movie to whatever list preceded it. Selecting a list also **resets `page` to 1** — page 7 of Movie has no relationship to page 7 of All, and the tiered layout only makes sense from page 1.

Selecting the list already shown is a no-op: no history entry, no reload.

### D6. The module cache becomes a per-list `Map`, keeping the reason it exists

`cachedItems: TopAnimeItemDto[] | null` → `Map<TopAnimeRankingType, TopAnimeItemDto[]>`, and `inFlightLoad` likewise. The original justification (in the file's own comment) is unchanged: a list is identical regardless of which page of it is being viewed, so it is fetched at most once per app session and `page` slices it client-side. Keying by ranking type is the minimum extension that keeps that true per list.

This is what makes re-selecting a previously seen list instant — no network call, no loading state, no flash.

### D7. Switching to an unseen list keeps the outgoing list on screen, muted

Three distinct states, rather than one "Loading…":

| Situation | What renders |
| --- | --- |
| Nothing loaded yet (first arrival) | the existing "Loading…" line |
| Switching to a list already cached | the new list, immediately |
| Switching to a list not yet cached | the *outgoing* list, dimmed and non-interactive, until the new one arrives |

The third case is the one the request calls out. Dimming rather than blanking keeps the page's height and layout stable, so nothing jumps when the new list lands, and the muted + `pointer-events: none` treatment makes it unmistakable that what is on screen is not the list the highlighted button names. The selector row itself stays interactive throughout, so a mis-click can be corrected without waiting.

*Alternative considered:* skeleton rows. Rejected — the outgoing real content is more informative than placeholder boxes, and it costs no extra markup.

### D8. An entry edit patches every cached list, not just the visible one

`setEntry` currently maps over the one cached array. With seven, an anime added from the Movie list must also flip to "Edit" in the All list's cache, since the two lists overlap heavily. So the patch iterates every cached list, updating that anime's `entry` wherever it appears. Without this, going back to All after adding from Movie would show a stale "Add" that re-adds an anime already in the list.

### D9. The selector is a pressed-state button group

The request's own suggestion — highlight the active button — is the right one and is kept. Concretely: one horizontal group of buttons, the active one filled with the accent colour, the rest in the page's ordinary control style, each carrying `aria-pressed` so the selection is exposed to assistive tech rather than being colour-only. Heights come from the shared control-height token introduced by the earlier filter-control alignment work, so the row lines up with the pagination controls sharing its line.

The group sits on its own row below the page's `<h1>`, sharing that row with the ranking's pagination controls (`justify-content: space-between`) rather than crowding the title: seven buttons plus a title plus pagination all on one line would not survive a narrow window. The selector wraps onto further lines first as space runs out; pagination drops to its own line below the selector only once the two no longer fit together.

### D10. Ranks are list-relative, so the tier layout needs no changes

Confirmed against the live API: for `ranking_type=movie`, the first three edges carry `ranking.rank` 1, 2, 3 (while their `node.rank` — the overall rank — reads 6, 7, 22). `TopAnimeService` already stores `edge.Ranking?.Rank ?? position`, so each list is stored ranked 1..500 in its own right. The showcase (1–3), card row (4–10), and flat-row (11+) tiers therefore apply per list with no code change, and "page 1 only" stays true for each.

## Risks / Trade-offs

- **Seven lists × 500 rows multiplies `TopAnimeRankingEntries` and can pull in lean `AnimeMetadata` rows for anime not otherwise tracked** → Bounded and small: ≤3,500 ranking rows total, and the lists overlap heavily (Popularity and Favourite are largely the same titles as All), so the number of *new* metadata rows is far below 3,500. Ranking rows already cascade-delete with their anime.
- **The migration re-keys two live tables** → Both are pure caches. `RankingType` is added with a `DEFAULT 'all'` backfill, which is correct for every existing row (they were all fetched as the `all` ranking), so no data is lost and no re-fetch is forced. `TopAnimeFetchLogs` loses its surrogate `Id`; it holds at most one row, so there is nothing to reconcile.
- **A user clicking through all seven buttons triggers seven MAL fetches in quick succession, which MAL may throttle with a 403** → `MalRequestPacer` already spaces MAL requests, and each list's fetch is once-per-day. A throttled fetch is caught and logged by the existing `try/catch` in `EnsureFreshAsync`, which serves whatever is cached and does not consume the day, so the next visit retries.
- **A never-fetched list's first open is a full 500-row MAL round trip, which is slower than the instant switch between cached lists** → Unavoidable without prefetching, which is explicitly out of scope. D7's muted-outgoing-list treatment is what keeps that wait from reading as a broken page.
- **Seven buttons is a wide control row on a narrow window** → The group wraps rather than scrolling horizontally, and it sits on its own line below the header for exactly this reason (D9).

## Migration Plan

One EF migration, applied automatically by `db.Database.Migrate()` on startup (this project has no manual migration step):

1. `ALTER TABLE "TopAnimeRankingEntries" ADD COLUMN "RankingType" text NOT NULL DEFAULT 'all'` — backfills every existing row as the All ranking, which is what they are.
2. Drop `PK_TopAnimeRankingEntries`, re-add it on `("RankingType", "AnimeId")`.
3. Same shape for `TopAnimeFetchLogs`: add `RankingType` defaulting to `'all'`, drop the `Id` primary key and column, re-key on `RankingType`.

Rollback is the generated `Down` (drop the columns, restore the original keys); because both tables are caches, dropping them entirely and letting the next visit re-fetch is also a safe recovery path.

## Open Questions

None. The two genuinely uncertain points — whether MAL exposes Favourite/Popularity/ONA/Music as rankings, and whether a filtered list's ranks are list-relative or global — were both settled against the live API (D1, D10).

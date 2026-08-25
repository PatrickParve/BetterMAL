## Why

Relation data is the spine of the detail page, the More overlay, and the whole series graph — and it is currently one-directional, never re-checked after its first fetch, and ordered by release date instead of story order. Verified against the live DB and MAL API on 2026-08-24: 12 sequel edges point at an anime that links nothing back, so a prequel visible on the series page is absent from the anime's own page; MAL's bad `17965 --sequel--> 39360` edge silently drags a 1999 Shinsengumi movie into the Astro Boy franchise; all 609 my-list anime are frozen at their first full fetch because the nightly job only ever asks for `mean`; and Jujutsu Kaisen 0 sorts after season 1 despite a symmetric prequel edge saying otherwise.

These are one problem wearing four hats. A one-sided edge is simultaneously the missing-prequel signal and the bogus-member signal, so no fix that treats reverse edges as uniformly trustworthy — or uniformly ignorable — is correct. Fixing them together also lays the data foundation for a future "updates view" without standing up a second refresh system.

## What Changes

**Fetch model**
- The nightly tiered refresh performs a **full-detail** fetch instead of a score-only one, so relations, airing status, episode counts and ranks refresh on the tiers that already exist. Call count is unchanged — MAL rate-limits per request, not per field.
- **BREAKING (spec)**: the "Cheap, spread-out refresh requests" requirement's minimal-field mandate is replaced by request-count pacing and capping (the existing 1 req/sec pacer, 10-minute ticks, 500/day cap). The premise it was built on is false.
- Staleness tiers are re-cut and re-keyed onto `LastSyncedAt` (the true "last full-detail fetch"): airing or not-yet-aired → 1 day; finished within 1 year → 3 days; 1–2 years → 14 days; older → 28 days.
- **BREAKING (behaviour)**: `NeedsFullDetailFetch` becomes a TTL check — never fetched, or older than this anime's tier. The `Genres`-as-completeness marker, both dated migration cutoffs, and the `MediaType is null` clause are **deleted**. This retires the stuck 92 relation rows from the mid-morning 2026-08-09 cutoff for free.
- A detail-page live fetch that throws is no longer swallowed into a confidently empty relation bar; the page reports the failure and offers a retry.

**Edge confidence** *(the core of the change)*
- Every relation edge is classified **CONFIRMED** (both ends state it), **UNCONFIRMED** (the other end was full-fetched and does not state it), or **UNKNOWN** (the other end was never full-fetched).
- The detail page projects outgoing edges **union inverted incoming edges**, so a prequel that exists in the data appears on the anime's own page whichever side MAL stored it on.
- AniList adjudicates UNCONFIRMED edges as a verification signal only: it confirms an edge, it contradicts one (stored but not traversed and not given a button), or it is silent (current behaviour, so nothing regresses). This is what removes Shinsengumi from the Astro Boy series while recovering the genuinely missing prequels.
- The prequel/sequel button target becomes a **ranked pick** — recaps and side content excluded first, then CONFIRMED over UNCONFIRMED, then media type, then air-date proximity, then lowest id — replacing "first by MAL's array order", which is effectively "lowest MAL id" and therefore arbitrary.

**Series ordering**
- The main line is ordered by a topological sort over its prequel/sequel chain, with air date demoted to a tie-break. The main line is documented as **story order, not release order**.

**Updates-view foundation**
- Newly-appearing relation edges are recorded as events at refresh time. Data only — no UI in this change.

## Capabilities

### New Capabilities
- `relation-confidence`: how a stored relation edge is turned into a usable, directional, trustworthy fact — the inverse-relation map, the CONFIRMED/UNCONFIRMED/UNKNOWN classification, AniList adjudication of contested edges, and the ranked rules that resolve a single canonical prequel/sequel from multiple candidates. Owned here rather than in `anime-detail` because `series-page` consumes the same rules.

### Modified Capabilities
- `metadata-refresh`: the tiered nightly job becomes a full-detail refresh; pacing and capping are stated per request rather than per field; the tier ladder is re-cut and re-keyed onto last-full-detail-fetch; the on-demand detail fetch trigger becomes the same TTL check; a failed visit-triggered fetch is surfaced instead of silently serving thin cache.
- `anime-detail`: relations shown are the union of outgoing and inverted incoming edges; the prequel/sequel button targets the ranked pick rather than MAL's first; a failed live fetch renders a retry affordance rather than an empty relation bar.
- `series-page`: main-line ordering is story order via topological sort over chain edges; traversal skips edges AniList contradicts; the classification-revision timestamp is bumped so stored series re-classify on next read.
- `data-persistence`: a reverse index on the related-anime edge table; a new relation-discovery event record.
- `mal-api-integration`: the "Full detail fetches persist every related-anime edge" requirement asserts that MAL's `related_anime` carries no media type, which is false and contradicted by both the shipped code and the `anime-detail` spec — corrected here, since this change rewrites that requirement's storage contract anyway.

## Impact

**Backend**
- `Services/Metadata/MetadataRefreshService.cs` — drop `ScoreOnlyFields`; full-detail path in `RefreshStaleBatchAsync` with `.Include(a => a.RelatedAnime)` on the `due` query (the same tracked-snapshot requirement already documented in `RefreshOneAsync` and `ResyncService`); re-cut tiers; emit relation-discovery events from the pre-`ApplyTo` snapshot it already loads.
- `Services/Detail/AnimeDetailService.cs` — rewrite `NeedsFullDetailFetch`; delete both migration cutoffs; reverse-edge query; fetch-failure flag.
- `Services/Detail/AnimeDetailDto.cs`, `RelatedAnimeDto.cs` — direction, confidence, and fetch-failure fields.
- `Services/Series/SeriesGraphBuilder.cs` — topological main-line ordering; confidence filter on traversal; `ClassificationRevisedAt` bump.
- `Services/Airing/AniList/AniListClient.cs` — extend `LookupQuery` with `relations { edges { relationType node { idMal } } }` (free for anime already getting an airing lookup); batch the rest via `Page.media` under the existing 4.5s pacer.
- New: relation-confidence resolver service, AniList relation-type mapping (uppercase enum → MAL snake_case), relation-event entity.

**Data**
- Migration: `HasIndex(e => e.RelatedAnimeId)` on `AnimeRelatedAnime` — the reverse lookup is unindexed today and this change puts it on every detail read.
- Migration: relation-discovery event table.
- Optional, flagged in design: collapse `LastScoreSyncedAt` into `LastSyncedAt`, which always move together once every refresh is a full fetch.

**Frontend**
- `pages/AnimeDetailPage.tsx` — consume the server-resolved prequel/sequel instead of picking the first by array order; render the fetch-failure retry state.

**External**
- AniList request volume rises for adjudication; stays inside the existing 30 req/min budget and 4.5s pacer. AniList remains a verification signal only — MAL stays the primary metadata source.

**Risks**
- Inverted reverse edges can surface a prequel MAL's own page does not show. Intended; edge confidence is what keeps it honest.
- Dropping the `Genres` completeness marker changes which rows count as complete. 130 rows currently have genres and zero relations; 103 were synced after 2026-08-09 and spot-checks (27775 Plastic Memories, 48556 Takt op. Destiny) genuinely have no relations on MAL — the TTL check must leave them alone rather than refetching every visit.

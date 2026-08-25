## Context

Relation data enters the system exactly once — the first time an anime is full-fetched — and is then read by three consumers that disagree about what it means.

- `AnimeDetailDto.FromEntity` projects `anime.RelatedAnime`: outgoing edges only.
- `SeriesGraphBuilder.TraverseAsync` walks outgoing *and* incoming edges, and explicitly documents why ("a member whose own relations have never been fetched still connects the component").
- `SeriesGraphBuilder.Classify` builds the main line from `sequel`/`prequel` edges and then throws those edges away, ordering by `OrderKey` — aired date, MAL id.

MAL's `related_anime` is not symmetric, so the first two disagree in practice: 12 sequel edges in the live DB point at an anime that links nothing back, and 4 anime have an incoming `sequel` with no `prequel` of their own. The third produces Jujutsu Kaisen 0 sorted after season 1 despite a symmetric prequel edge stating the opposite.

The refresh side compounds it. `MetadataRefreshService.RefreshStaleBatchAsync` fetches `fields=mean` and writes `LastScoreSyncedAt`; `ApplyTo` — the only writer of `LastSyncedAt` and of relation rows — is never reached from the nightly job. `AnimeDetailService.NeedsFullDetailFetch` returns false as soon as `Genres` is non-empty. Between them, relation data for all 609 my-list anime is frozen at its first fetch, with two dated migration cutoffs bolted on to force one-time re-fetches after past schema changes.

The constraint that shapes everything below: **a one-sided edge is simultaneously the missing-prequel signal and the bad-data signal.** MAL's `17965 --sequel--> 39360` is one-sided because MAL is wrong; the four missing prequels are one-sided because MAL is incomplete. Unioning reverse edges fixes the second class and worsens the first. Distinguishing them needs a source outside MAL.

## Goals / Non-Goals

**Goals:**
- An existing prequel/sequel appears on the anime's own page regardless of which side MAL stored it on.
- A one-sided edge MAL got wrong does not silently admit an anime to a franchise.
- Relation data refreshes on the staleness tiers that already exist, at no additional request cost.
- Main-line order is story order.
- Relation-change data is recorded now so a later updates view needs no second refresh system.

**Non-Goals:**
- The updates view itself — this change writes the events and reads none of them.
- Replacing MAL as the relation source. AniList is a veto and a corroborator, never an author.
- Backfilling relation-discovery events for edges that predate this change.
- Curating MAL's relation vocabulary, normalizing relation types at write time, or repairing MAL data upstream.

## Decisions

### 1. Confidence is derived at read time, not stored

An edge's confidence is a function of (this edge, the far end's edges, the far end's fetch state, AniList's edges for the pair). Every input is already in the database. Storing a computed verdict on `AnimeRelatedAnime` would mean invalidating it whenever *either* end refreshes — and `ApplyTo` replaces an anime's whole relation collection wholesale, so a stored verdict would be destroyed on the owning side and stale on the far side.

Derived is also self-healing: an Unknown edge becomes Confirmed the moment its far end is fetched, with no backfill pass. Given the tier ladder now refreshes everything, "wait for the far end's next refresh" is a bounded wait rather than forever.

*Alternative considered:* a `Confidence` column maintained by the refresh path. Rejected — it makes correctness depend on both ends being refreshed in the right order, which is exactly the property the current write-once design already failed at.

### 2. One shared resolver, not two implementations

Detail page and series builder must agree on which edges are real, or the "Series" button will lead to a page that disagrees with the one it was pressed from. A single `RelationResolver` (working name) owns: the inverse map, the union of outgoing and inverted incoming edges, confidence classification, AniList adjudication, and the ranked prequel/sequel pick. `AnimeDetailService` and `SeriesGraphBuilder` both call it.

The ranked pick moves **server-side**. Today `AnimeDetailPage.tsx` picks the first candidate by array order. The rules that make the pick correct — recap and side-content exclusion — are already implemented in `SeriesGraphBuilder.FindRecapIds` / `FindSideContentIds` and depend on the relation graph, not on what the page loaded. Reimplementing them in TypeScript against a flat DTO would be a second, weaker copy. The client receives resolved `prequel` / `sequel` / `parentStory` references and renders them.

### 3. The union costs one extra indexed query per detail read

`GetDetailAsync` gains a query for `AnimeRelatedAnime WHERE RelatedAnimeId = @id`. That column is unindexed today; `HasIndex(e => e.RelatedAnimeId)` is a required part of this change, not an optimization. (The existing `HasIndex(e => e.AnimeId)` is redundant with the composite PK's leading column, which is why the reverse direction was never covered.)

Confidence classification then needs the far ends' own relation rows and fetch state — one further batched query over the candidate ids, not one per edge. Detail reads go from one relation query to three, all indexed and all bounded by an anime's relation count.

### 4. AniList relations are stored, never fetched on the read path

Adjudication needs AniList data, and the `metadata-refresh` spec forbids live API calls during a page render. AniList edges therefore land in their own table, populated by background work, and adjudication becomes a local join.

The three-state distinction matters more than the edges do. A contradiction verdict requires knowing that AniList *was asked* about this anime and *does* know it. That is three states, and the existing `AnimeAiringSync` already models two of them for the airing sync (`AniListId` null with a non-null `LastFetchedAt` = "AniList has no entry"). Extending that record with `RelationsFetchedAt` reuses the pattern rather than inventing a parallel one. Without it, every never-looked-up anime would silently contradict every edge pointing at it — the precise failure that would delete real franchise members.

Verdicts are four-valued rather than the three the proposal sketched:

| Verdict | AniList says | Traverse? | Button? |
| --- | --- | --- | --- |
| Confirmed | same edge, mapped type matches | yes | yes |
| Corroborated | some edge between the pair, different type | yes | no |
| Contradicted | knows both, no edge at all | **no** | no |
| Unknown | doesn't know one/both, or lookup failed | yes | yes |

Corroborated exists because AniList's enum is not a relabelling of MAL's — it carries `COMPILATION` and `CONTAINS`, which MAL has no equivalent for, and merges `alternative_version`/`alternative_setting` into one `ALTERNATIVE`. Collapsing "AniList relates these two, differently" into Contradicted would evict genuinely related members over a vocabulary mismatch; collapsing it into Confirmed would hand a sequel button to an edge AniList calls a side story. The distinction is cheap and each half is used differently.

`ADAPTATION` is manga-side and ignored. Any unmapped AniList relation type still counts as "an edge exists between the pair", so it produces Corroborated rather than Contradicted — silence about the *type* is not evidence of *no relation*.

*Alternative considered:* trusting reverse edges only when the far end has zero relations (i.e. "it's thin, not contradicting"). Rejected — 39360 has exactly zero relations and is exactly the case that must be rejected. Thinness and wrongness are indistinguishable from inside MAL.

### 5. Adjudication only runs where it changes an answer

Confirmed edges are not looked up — they need no help. Unknown edges are not looked up — the disagreement being tested does not exist yet, and the far end's own refresh will settle it for free on its tier. Only Unconfirmed edges are adjudicated, which in the live DB is a small set.

The lookup rides along with the AniList airing lookup where one is already due (`LookupQuery` gains `relations { edges { relationType node { idMal } } }` — free, same request). Anime needing only adjudication batch through `Page.media` under the existing 4.5s pacer, well inside AniList's 30 req/min.

### 6. The tier ladder moves to `LastSyncedAt`, and `ScoreOnlyFields` is deleted

`LastSyncedAt` is already exactly "last full-detail fetch": only `ApplyTo` writes it, `ApplyLeanTo` deliberately does not, and a never-fetched row carries `default` (`-infinity` in Postgres). It is the correct key and needs no new column.

This also fixes a bug the change did not set out to fix: the current query tiers on `LastScoreSyncedAt`, which `ApplyLeanTo` **does** write. Browsing a season today bumps an anime's score-sync timestamp and so postpones its nightly refresh, even though nothing about its detail data was refreshed.

`RefreshStaleBatchAsync` calls `GetAnimeDetailsAsync` with the default full field set and `ApplyTo`, and its `due` query gains `.Include(a => a.RelatedAnime)` — the same tracked-snapshot requirement already documented in `RefreshOneAsync` (without it EF has no collection to diff and re-inserts existing rows).

New ladder, keyed on last full-detail fetch:

| Tier | TTL |
| --- | --- |
| `currently_airing` or `not_yet_aired` | 1 day |
| `finished_airing`, aired within 1 year | 3 days |
| `finished_airing`, 1–2 years | 14 days |
| older / unknown | 28 days |

Steady-state cost at 609 my-list entries is dominated by the 3-day tier and stays far under `NightlyCap = 500`; the 10-minute tick and 1 req/sec pacer are untouched, because a full-detail request and a `fields=mean` request each cost exactly one request against MAL's per-request limit. The first pass after deploy is the expensive one — see Migration Plan.

### 7. `LastScoreSyncedAt` is kept, and the proposed collapse is rejected

The proposal flagged collapsing `LastScoreSyncedAt` into `LastSyncedAt` as optional. It should not be done. `ApplyLeanTo` writes `LastScoreSyncedAt` to record that a *lean listing* refresh touched the row; collapsing the two would make season browsing stamp the full-detail timestamp and thereby suppress the tiered refresh and the detail page's TTL fetch for every browsed anime — reintroducing the frozen-data bug through a different door.

After this change nothing reads `LastScoreSyncedAt`; it remains a write-only lean-refresh marker. Leave the column and the write in place. Renaming it to something honest is a separate, cosmetic change.

### 8. `NeedsFullDetailFetch` becomes one TTL comparison

```
LastSyncedAt == default  ||  now - LastSyncedAt > TierTtl(anime)
```

Deleted with it: `RelatedAnimeMigrationCutoff`, `RelatedAnimeMediaTypeMigrationCutoff`, the `Genres is not { Count: > 0 }` completeness marker, and the `RelatedAnime.Any(r => r.MediaType is null)` clause.

The `Genres` marker was always a proxy — it answers "was this row ever full-fetched", which `LastSyncedAt` answers directly and correctly. Dropping it is what protects the 130 rows that have genres and genuinely zero relations (27775 Plastic Memories, 48556 Takt op. Destiny): under a relations-based completeness test they would refetch on every single visit forever.

The dated cutoffs disappear as a category, taking problem 5 with them — the 92 relation rows stranded by a midnight-dated cutoff that shipped mid-morning are now simply rows past their TTL. Shared logic between `AnimeDetailService` and `MetadataRefreshService` means the on-demand path and the nightly path cannot drift, and "already fetched today, skip" needs no extra state.

### 9. Story order is a stable topological sort seeded by air date

Kahn's algorithm over the `sequel`/`prequel` edges among main-line members, with the ready-set held in a priority queue keyed by the existing `OrderKey` (aired-from, nulls last, MAL id). This gives all three required behaviours from one mechanism: chain edges constrain order absolutely; members unconstrained relative to each other fall back to air date; a member with no chain edge at all is placed purely by air date, interleaved rather than appended.

Cycles must not throw. MAL relation data can contain a `sequel`/`prequel` cycle, and a series build that throws is worse than one that is slightly mis-ordered. When the queue empties with nodes remaining, emit the remainder in `OrderKey` order.

`ClassificationRevisedAt` is bumped to the ship date so every stored series re-derives on next read — the mechanism exists for exactly this.

Extras keep air-date ordering within their media-type group. Story order is a claim about a narrative chain; extras have no chain, and release order is the only meaningful order they have.

### 10. Relation-discovery events are written from a snapshot the refresh already holds

`RefreshOneAsync` loads `Include(a => a.RelatedAnime)` before `ApplyTo` replaces it. Diffing the incoming set against that snapshot costs nothing extra. Edges present after and absent before become events.

An anime with no cached row at all emits none — its entire initial relation set would otherwise register as "newly discovered", flooding the table on first import with news that is not news. Removals are not events; the updates view is about things appearing.

### 11. The fetch-failure flag is a flag, not an exception

`AnimeDetailService` catches every exception from `RefreshOneAsync` and serves whatever is cached. For a lean row that renders as a page with no relations and no prequel/sequel/More/Series controls — pixel-identical to a correct render of an anime that genuinely has none. A MAL 403 burst-throttle outlasting `MalAuthPacingHandler`'s 5 backoff attempts produces exactly this on a first visit.

The catch stays (a failed refresh must not turn a viewable page into an error) but sets `RefreshFailed` on the DTO. The client renders a notice and a retry. The failure does not mark the anime fetched, so the next visit retries naturally — `LastSyncedAt` is untouched by a fetch that threw, so this falls out of decision 8 with no extra state.

## Risks / Trade-offs

**A surfaced reverse edge shows a prequel MAL's own page does not** → Intended, and the point of the change. Confidence is what keeps it honest: the edge is shown because some anime asserts it and nothing contradicts it. AniList adjudication removes the ones that are wrong.

**AniList adjudication deletes a real franchise member** → The three-state fetch record (decision 4) is the guard: contradiction requires AniList to have been asked and to know both anime. Unknown always falls back to today's behaviour. Verify explicitly that the Astro Boy series loses 39360 and keeps its other 13 members.

**The first tiered pass after deploy is much larger than steady state** → Every my-list anime is past its new TTL on day one. 609 rows against a 500/day cap is roughly a day and a half, self-limiting and ordered most-stale-first. Acceptable; see Migration Plan.

**Full-detail responses are larger than `fields=mean`** → More bytes, identical request count. MAL rate-limits per request. Pacer, tick interval, and daily cap are unchanged, which is the property to confirm rather than assume.

**Dropping the `Genres` completeness marker changes which rows count as complete** → Verify against the 130 rows with genres and zero relations (103 synced after 2026-08-09) that they are treated as fetched and do not refetch per visit.

**Three relation queries per detail read instead of one** → All indexed, all bounded by relation count. The reverse index is mandatory; without it this is a table scan on every detail page.

**Server-side pick resolution is a client/server contract change** → The DTO gains resolved references while keeping the full relation list, so the More overlay is unaffected and the client change is confined to reading a field instead of computing one.

## Migration Plan

1. **Index first.** `HasIndex(e => e.RelatedAnimeId)` ships before anything reads reverse edges on the detail path.
2. **Schema.** AniList relation table; `RelationsFetchedAt` on `AnimeAiringSync`; relation-discovery event table. All additive — no column drops, no data migration.
3. **Refresh model.** Delete `ScoreOnlyFields`, re-key tiers onto `LastSyncedAt`, rewrite `NeedsFullDetailFetch`. Deployable alone: it fixes the frozen-data problem with no dependency on the confidence work, and it is what populates the far-end relation data every confidence verdict depends on. Expect ~1.5 days of catch-up passes.
4. **Confidence, unadjudicated.** Resolver, union, classification, ranked pick, with every verdict Unknown. Behaviour: missing prequels appear; no edge is yet withdrawn. This is the "recover" half, and it ships without AniList.
5. **Adjudication.** AniList relation fetching and the four verdicts. This is the "reject" half — Shinsengumi leaves Astro Boy here, not before.
6. **Ordering.** Topological sort plus the `ClassificationRevisedAt` bump.
7. **Frontend.** Resolved references and the retry state.

Steps 4 and 5 in that order matter: shipping adjudication before the resolver would withdraw edges with nothing yet consuming confidence, and shipping ordering before step 3 would sort chains built from stale relation data.

**Rollback:** each step is independently revertable. The additive schema can stay in place through a code rollback — unread tables are inert. `ClassificationRevisedAt` is the one asymmetry: reverting the ordering code after series have re-derived leaves them stored in story order until the next revision bump, which is a cosmetic regression rather than a correctness one.

## Open Questions

- **AniList batch size for `Page.media`.** 50 per page matches `ScheduleQuery`'s existing paging, but AniList's complexity budget counts nested `relations` against it. Start at 25 and confirm against a live response rather than guessing.
- **Re-adjudication cadence.** AniList relation rows never expire under this design. A newly-announced sequel AniList adds later will not re-adjudicate an edge already judged Contradicted. Simplest fix if it bites: treat `RelationsFetchedAt` older than the anime's own MAL tier TTL as unfetched. Deferred until there is a case for it.
- **Whether `spin_off` should adopt an inverse.** MAL has no value meaning "this is the original of that spin-off", so a spin-off's parent currently shows the raw `spin_off` relation in the More overlay, which reads slightly wrong from that side. A display-only label ("Spin-off of") would fix it without inventing a stored relation type. Out of scope here.
- **Whether the series-neighbour fallback should apply to `parent_story`.** Specified for prequel/sequel only. Main-line neighbours have no bearing on parent stories, so probably not — noted so it is a decision rather than an oversight.

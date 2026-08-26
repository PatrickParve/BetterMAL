## Context

`MetadataRefreshService.RecordDiscoveries` already writes a `RelationDiscovery` row for every relation edge that newly appears in a cached anime's relation set, and the model's own doc comment names this change as its reader: *"the write side of a future 'updates' view (this change writes these; nothing reads them yet)"*. This change builds the reader.

Three things in the current system shape the design:

- **The tiered refresh is my-list-only.** `RefreshStaleBatchAsync` filters `Where(a => a.UserEntry != null)`, and `metadata-refresh` spells that out as a requirement ("Non-my-list anime excluded from the tiers"). An announced sequel is by definition not in my list, so today it is fetched once (if at all) and never again — its episode count and premiere date can never arrive.
- **`not_yet_aired` shares the 1-day tier with `currently_airing`.** A show premiering in fourteen months is polled daily for data that changes a handful of times a year, while a show premiering next week gets no more attention than one premiering next year.
- **`IRelationResolver.GetEdgesAsync`** already returns an anime's edges in both directions, inverted onto that anime's side, carrying AniList-adjudicated confidence. `SeriesGraphBuilder` uses it to keep Contradicted edges out of series membership. The updates feed needs exactly the same question answered — "which real relations connect this anime to my list?" — so it uses the same resolver rather than re-deriving affiliation.

`TotalEpisodes` is already normalised to `null` for MAL's `num_episodes: 0`, so "episode count released" is exactly a `null → value` transition on a stored row; `AiredFrom` likewise.

## Goals / Non-Goals

**Goals:**
- A Home-page feed of news about what there is to watch and when — announcements, episode counts, premiere dates, delays, slot changes, moved episodes — for my own entries and the franchises around them.
- Facts that became known at the same moment read as one card, not several.
- News keeps arriving without me opening anything: announced anime stay fresh enough for their counts and dates to be noticed.
- An unaired-anime refresh cadence that spends requests where they are worth spending.
- A permanent, searchable record of every update, with the Home row showing only the last 30 days.

**Non-Goals:**
- Not a general "what changed on MAL" feed. Score, rank, synopsis, studio, genre and picture changes are not updates — none of them changes what there is to watch or when.
- Not an episode-count correction feed. A count moving 12 → 13 stays out (D15), unlike the schedule changes, which are in.
- No notifications, no read/unread state, no dismissal. The feed is a view over recorded facts.
- No new external data source. AniList is used only through the adjudication that already exists.
- The activity log (`ActivityLog`) is untouched. That records *my list changing*; this records *the world changing*.

## Decisions

### D1. One `AnimeUpdate` row per card, with a flags column — not one row per fact

`AnimeUpdate` carries `Kinds`, a `[Flags]` enum over six values in two families — `Announced | EpisodeCountReleased | StartDateReleased` (a fact becoming known) and `StartDateChanged | BroadcastSlotChanged | EpisodesMoved` (a schedule moving). Two facts noticed in the same detection pass merge into one row; facts noticed at different times are separate rows.

*Why:* the requirement "if things are announced together have them all in one, separate only when they get announced at different times" is a statement about rows. Modelling it as one-row-per-fact would push the grouping into every reader, and readers would have to agree on what "the same time" means. A merged row makes the grouping a write-time fact that cannot be read two ways.

*Alternative rejected:* one row per fact plus grouping by `(AnimeId, DetectedAt)` at read time. It works only because a single detection pass stamps one `now`; any future path that writes two facts microseconds apart silently splits a card.

### D2. The two families get opposite duplication rules

**Becoming-known kinds are once-per-anime forever.** Before writing one, the detector skips a kind already present in any existing row for that anime. This makes them idempotent — a re-run, a manual refresh and a scheduled refresh cannot produce duplicate news — and absorbs MAL flapping, where a count momentarily reads as unknown and comes back. The cost is that a genuine second reveal after a genuine retraction is missed, which is the right trade for a fact that is only interesting the first time.

**Schedule-change kinds carry no limit at all.** A premiere delayed twice is two pieces of news, and collapsing them would hide the second delay behind the first. They need no dedupe query either: the guard is the comparison itself. Detection diffs the stored value against the incoming one, so once the new value is written, the next pass finds nothing changed. Idempotence falls out of the diff rather than being enforced on top of it.

*This asymmetry is the reason `Kinds` is a flags column rather than a single enum* — one row can hold kinds from both families with different lifetimes, and the read side never has to care.

### D3. Current values are read live; moved-from values are stored

For the becoming-known kinds, `AnimeUpdate` stores no values — only which anime, when, and which kinds. A card that says "12 episodes" should say "13 episodes" once MAL corrects it; freezing the value would make old cards lie, with nowhere for a reader to notice the drift.

For the schedule-change kinds this is impossible: the news *is* the movement, and the anime's current record holds only where it landed. So those updates store what each moved field held beforehand — `PreviousStartDate`, `PreviousBroadcastDayOfWeek` + `PreviousBroadcastTime`, and for a moved episode its number with the dates it moved from and to.

*Why store both ends for an episode move* when the others store only the "from": a premiere date's landing place is the anime's current `AiredFrom`, but an episode's landing place is a row that can move again. Storing both keeps an old card truthful about the move it was recorded for.

*Precedent:* `ActivityLog.PreviousEpisodesWatched` exists for exactly this reason — "lets a reader determine increase vs decrease without re-deriving it from surrounding rows". Same problem, same shape of answer.

`PreviousBroadcast*` mirrors `AnimeMetadata`'s own JST day/time pair rather than a rendered string, so the card converts it through `BroadcastLocalTimeConverter` like every other broadcast time in the app and never reports a slot in a different timezone from the rest of the page.

### D4. Eligibility is derived at read time, not stored

An update is shown when its anime **is** a non-`Dropped` `UserAnimeEntry`, or has at least one traversable, non-Contradicted relation edge to one. The second clause is computed on every read via `IRelationResolver.GetEdgesAsync`; the first is a single lookup.

*Why the first clause:* an anime I added to my list before it had any data is precisely the anime whose data I am waiting for — found somewhere, added, and now holding nothing but a title. Requiring it to also belong to a franchise I follow would withhold exactly the news I added it for. Franchise affiliation is a way news *reaches* me, not a condition on news being mine.

*Why derived rather than stored:* eligibility is mutable in a way a stored flag cannot track. Dropping the show a sequel was announced for should retire that announcement; adding an anime to my list should surface news already recorded for it. Deriving it means both happen with no backfill and no cleanup job. This mirrors `relation-confidence`'s own decision to derive confidence at read time "so it corrects itself as soon as either end is refreshed".

*Cost:* one resolver call per candidate anime per read. The Home row is bounded by 30 days of news (single digits, typically) and the history overlay by the whole log (tens to low hundreds), so this is a small fixed cost on two endpoints, not a hot path.

### D5. Affiliation uses every relation MAL reports, not just the story-relation traversal set

**Superseded after shipping, by user request.** Originally affiliation traversed only `SeriesRelations.TraversalSet` — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version` — reasoning that this is the same set a series is built from, and that `alternative_setting`/`character` "are how MAL links shows that merely share a universe or a cast, and traversing them would fuse entire multi-decade franchises" if used for series membership.

The concrete case that overturned it: a `Fate/stay night` refresh discovered a new `alternative_setting` edge to `Fate/strange Fake (Shin Series)`. The write side already recorded its `Announced` update correctly (resolution never checked relation type), but the read side hid it, since `alternative_setting` isn't in the traversal set — a real announcement about a franchise the user is watching, invisible because of a filter meant to protect series *membership*, not news eligibility.

*Why widening is correct here even though D5's original reasoning about series-building still holds elsewhere:* fusing loosely-related shows into one series page is a real cost (a wrong "same story" grouping is hard to undo and misleads the series/watch-order UI). Showing an extra news card is not the same kind of cost — worst case, a card names a relation that turns out to be a crossover cameo, which the reason text ("Shares characters with X") makes obvious at a glance, and the user can just ignore it. `SeriesRelations.TraversalSet` remains exactly as narrow as before for series-building; only `AnimeUpdateService`'s eligibility check widened to accept any non-Contradicted edge — the `Confidence != Contradicted` guard is what's still doing the real filtering work, not the relation type.

*Deliberately not widened alongside it:* `AdjacentAnimeSet` (which anime the nightly job proactively refreshes) stays scoped to the narrow traversal set. An anime reachable only through a widened relation type gets exactly the one card its resolving fetch produced and then goes stale — no further MAL calls are spent on it unless the user adds it to their own list. This was an explicit user choice: display-only widening, not refresh-scope widening.

### D6. An announcement is recorded only for an anime that has not finished airing

The resolution pass fetches the newly-related anime, then records an `Announced` update only when its `AiringStatus` is `not_yet_aired` or `currently_airing`.

*Why:* MAL adds missing edges to old shows constantly. A relation newly appearing between a show I am watching and a finished 2005 OVA is a data correction reaching us, not an announcement — the OVA has existed for twenty years. Gating on airing status is the only signal that separates "this show is new" from "this link is new".

*This gate is evaluated at write time, unlike D4.* An anime that was `not_yet_aired` when announced and is airing three weeks later must keep its announcement card; re-evaluating at read time would delete news the moment it came true.

### D7. `RelationDiscovery` gains `ProcessedAt`, and every pre-existing row is backfilled as processed

The migration stamps every existing discovery with a processed timestamp and emits nothing for them.

*Why:* discoveries have been accumulating since the relation-confidence change shipped, all of them inside the 30-day window. Processing them on first run would open the feature with a wall of backdated announcements — the same failure `activity-recording` already guards against with "The import that establishes the baseline records nothing", and for the same reason: a burst of history all sharing one moment is not news.

### D8. Detection sits on the two write paths, so a manual refresh produces updates too

Detection hangs off whichever function writes the data it reads, never off a schedule of its own:

- **Episode count, premiere date, broadcast slot** — extracted into `IAnimeMetadataChangeDetector`, which snapshots relations and fields before `ApplyTo` and diffs after. Every writer of cached `AnimeMetadata` calls it: `MetadataRefreshService` (`RefreshStaleBatchAsync` and `RefreshOneAsync`) and `Services/Sync/ResyncService`'s corrective full re-sync — the latter added once it became clear it was the one remaining full-detail write path that bypassed detection entirely (raised by the user after the rest of this change shipped).
- **Moved episodes** — `IEpisodeAiringRepository.ReplaceForAnimeAsync`'s caller in `EpisodeScheduleRefreshService`, the single point where per-episode rows are replaced.

*Why:* the request is explicit that "even if the data refresh is manual the new things should still show". Putting detection on the functions that write means every path — nightly batch, detail-page TTL fetch, the Refresh button, the Settings picker, the corrective re-sync, the 6-hourly AniList pass, the airing backfill — feeds the same detector, and no future path can refresh without it.

*Note:* neither path records anything for an anime with no prior row, exactly as `RecordDiscoveries` records nothing there. A first observation is not a reveal and not a move.

### D9. A staleness ladder for anime that have not aired

`RefreshTiers` gains a not-yet-aired ladder in place of the shared 1-day tier:

| `not_yet_aired` anime | Interval |
| --- | --- |
| Premiere date known, within 30 days, or already passed | 1 day |
| Premiere date known, more than 30 days out | 7 days |
| Premiere date unknown | 3 days |

*Why these numbers:* what changes for an unaired show is its count, its date, and eventually its status — events that happen a handful of times across a production, not daily. Weekly is well inside the 30-day window the feed displays, so a distant premiere misses nothing by being polled slowly. Inside 30 days the picture changes for real — delays, split-cour splits, final counts — and daily is worth it for a set this small. The "date already passed but MAL still says not_yet_aired" row is deliberate: MAL lags on the flip to `currently_airing`, and daily polling is how the flip gets noticed promptly.

**An unknown premiere date polls faster than a distant one (3 days, not 7).** The two look similar — both are far from anything happening — but a distant date is a *signal*, telling the system when to start paying attention, while a missing date is the absence of one. The missing date is also the single most valuable fact still outstanding about such an anime, and the one most likely to be what I added it to my list for.

*Alternative rejected:* a smooth function of time-to-premiere. Not worth it — the ladder is already expressed twice (in-memory and as an EF expression), and each extra band doubles the surface where the two can disagree.

### D10. The refresh scope gains one narrow carve-out: unaired, list-adjacent anime

`RefreshStaleBatchAsync`'s candidate filter becomes "has a `UserAnimeEntry` **or** (has not aired **and** is story-related to a non-dropped list entry)".

*Why:* it is the minimum needed to make D9 mean anything. Without it, an announced anime is fetched once by the resolution pass and never again, so its count and date can never be *released* — they would only ever appear on the announcement card or not at all.

*Why it stays small:* the carve-out is keyed on `AiringStatus in ('not_yet_aired', null)`, so an anime leaves it the moment MAL says it is airing. The set is therefore bounded by "unaired shows connected to my franchises" — tens, not thousands — and it drains itself as those shows premiere.

*The candidate query uses raw `AnimeRelatedAnime` in both directions rather than the resolver*, because it must translate to SQL. Over-including a handful of anime whose only edge AniList contradicts costs a request and nothing else; the read-time filter (D4) is where correctness lives.

### D11. Announcement resolution shares the metadata-refresh tick, pacer and daily cap

A new `AnnouncementResolutionService` runs at the start of each `MetadataRefreshBackgroundService` tick, before the staleness batch, and its MAL calls count against the same 500/day cap.

*Why:* resolving a discovery costs a MAL call, and the whole point of the existing cap is that MAL calls are one budget. A second background service with its own cap would let the two collectively exceed what either believes it is spending. Running first means news is never starved by a large staleness backlog; the batch simply gets a smaller slice on the rare tick where many discoveries land at once.

A failed resolution leaves `ProcessedAt` null so the next tick retries — the same "a failed fetch releases the turn without marking the subject as fetched" rule the visit-triggered fetches already follow.

### D12. Updates ride the dashboard payload; history gets its own endpoint

`GET /api/dashboard` gains `updates` (the last 30 days). `GET /api/updates/history` returns the whole log, newest first.

*Why:* the Home section is always rendered, so folding it into the one request the page already makes costs nothing and avoids a second spinner. The history overlay is opened rarely and would be dead weight in every dashboard read — the same split `ProfileController` already makes between `/api/profile` and `/api/profile/activity`.

### D13. Layout

Full-width `.dashboard-section` between `CurrentlyWatchingCarousel` and `.home-page__row` — deliberately *not* the `--carousel` variant, which is `width: fit-content` and centred.

Cards are horizontal: poster thumbnail on the left, text stacked on the right — title, then the update's headline, then episode count and premiere date as they are known. A schedule-change card's headline carries the movement itself ("Delayed to 12 Oct, was 5 Oct", "Ep 7 moved to 10 Nov"), since that pair is the news and the current-value line beneath would otherwise repeat half of it. The row is one line, ordered newest-first left to right, scrolling horizontally when it overflows rather than wrapping or paging. The whole card is a link to the anime's detail page. A "History" button in the section header opens the overlay, and stays available when the row is empty so old news is still reachable.

The overlay is `EditHistoryOverlay`'s shape reused: `Modal --wide`, title search plus a from/to date range filtering one fetch locally, a Clear button when a filter is active, and distinct empty states for "nothing recorded" and "nothing matches these filters".

### D14. Nothing is pruned

Rows older than 30 days leave the Home row by virtue of the query's window; they are never deleted.

*Why:* the request asks for a history overlay holding "all the updates recorded", which is incompatible with deletion. Volume is a few rows per franchise per year.

### D15. Schedule changes are news; episode-count corrections are not

Premiere dates, broadcast slots and episode dates all record every time they move. A total episode count moving from one known number to another records nothing.

*Why the line falls there:* the three schedule fields answer *when can I watch this*, and a change to any of them is something a viewer acts on — a delay to diarise, a slot that has moved, a break week. An episode count answers *how much is there*, which nothing depends on until the show is over. MAL also revises counts as routine data entry, splitting or merging specials and recounting cours, so counting those as news would fill the feed with bookkeeping.

*The asymmetry is deliberate and narrow*: a count going 12 → 24 is arguably real news, and if it turns out to matter it is one more kind alongside the other five, not a change to any existing rule.

### D16. A move is a change of local calendar day, not of instant

Episode-move detection compares the **local calendar date** an episode airs on, before and after the AniList rows are replaced, and only for episodes that had not yet aired.

*Why not the instant:* AniList adjusts air times by minutes routinely, and an instant comparison would report those as moves. The local day is also what a viewer actually perceives as "moved" — the same reason `EpisodeScheduleService.ResolveOnLocalDate` is the app's shared answer to "does episode N air on this local date?".

*Why only unaired episodes:* a past episode's stored date changing is AniList correcting history, not a schedule moving. Nobody is waiting for it.

*Where several episodes move at once* — the usual case, since a break week pushes the whole remaining run back — the update names the **earliest**, which is the one being waited for. Recording one card per shifted episode would turn a single break week into a dozen cards saying the same thing.

*Scope note:* per-episode airing data is only kept for my-list anime (`EpisodeScheduleRefreshService.GetTrackedAnimeIdsAsync`), so an announced anime not yet added records no moves. It records every other kind normally, and starts recording moves if I add it. Extending AniList coverage to non-list anime is deliberately not part of this change — it would add a per-anime AniList request against a 30 req/min budget for news about shows I have not committed to.

## Risks / Trade-offs

- **The carve-out reopens "no fetches for anime the user has not opened"** — a rule `metadata-refresh` states plainly today. → Narrowed to unaired anime with a real story edge to a non-dropped entry, self-draining as they premiere, and spelled out as a modified requirement rather than left as an implementation detail that contradicts the spec.
- **The candidate query grows an EXISTS over `AnimeRelatedAnime` in both directions, evaluated every 10 minutes.** → Both `AnimeId` and `RelatedAnimeId` are already indexed (added for relation-confidence's incoming-edge reads), and the predicate is anchored on `AiringStatus`, which cuts the scan to unaired rows before the join.
- **A burst of discoveries could eat a tick's whole quota.** → Resolution is capped per tick as well as per day, so a large discovery batch spreads over several ticks instead of starving the staleness refresh for a whole day.
- **Read-time eligibility means the feed can change without anything being written** — dropping a show silently removes cards. → This is the intended behaviour (D4), and the history overlay applies the same filter, so the two never disagree about what exists.
- **D6's gate can misfire when MAL's `airing_status` is wrong or absent** at resolution time. → A failed or inconclusive fetch leaves the discovery unprocessed and retries; only a successful fetch reporting `finished_airing` discards the discovery. The cost of a miss is one announcement not shown, not a wrong one shown.
- **`spin_off` is in the traversal set**, so a spin-off of a show I am watching announces itself. → Intended: that is news about my franchise.
- **D5 widened to every relation type, including `character` and `other`** — the discovery/announcement write side was never actually filtered by relation type in the first place (`RecordDiscoveries` records every new edge regardless), so this widening only changes what the read side is willing to *show*, not what gets detected. The real blast radius is bounded by how the reason text renders: "Shares characters with X" or "Related to X" makes a loose/crossover link legible at a glance rather than hidden behind a filter, and the anime still has to be connected to a non-Dropped list entry to show at all.
- **Schedule-change kinds are unbounded per anime**, so an upstream source that flaps a date back and forth would emit a card each way. → The airing-status gate keeps this to unaired and airing shows, the local-day threshold (D16) absorbs the small adjustments that actually flap, and a genuine back-and-forth is genuinely news both times.
- **Widening eligibility to my own entries means every unaired anime in my list can produce news**, including ones added years ago and forgotten. → That set is bounded by my own Plan-to-watch list and by the kinds themselves: an anime only produces news while its data is still arriving or its schedule still moving, and finished shows produce none.

## Migration Plan

1. Migration adds `AnimeUpdates` (indexed on `DetectedAt` and `AnimeId`) and `RelationDiscoveries.ProcessedAt`, and stamps every existing discovery row as processed in the same migration (D7).
2. Ship backend and frontend together — the dashboard DTO gains a field, which an older frontend ignores, so the ordering is not load-bearing.
3. On first run the feed is empty and fills as genuine news arrives. This is expected, and the empty-state text is what a user sees for the first days.
4. Rollback: dropping the two schema objects is safe; nothing else reads them, and `RecordDiscoveries` keeps working as it does today.

## Open Questions

- Whether a total episode count moving between two known values should join the schedule changes as news (D15). Held out for now as MAL bookkeeping, but 12 → 24 is the case that would justify revisiting it.
- Whether announced anime not yet in my list should get AniList per-episode coverage, so their episodes-moved updates are detectable too (D16). Costs AniList requests for shows I have not committed to; cheap to revisit once the feed shows how often such anime get added to the list anyway.

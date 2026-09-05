## Why

The updates feed decides what is relevant only when the menu is read. Every path that writes anime metadata runs detection first, with no idea whose anime it is, so the log fills with news about anime the user will never add and then throws it away at display time. On the live database: 14,088 anime cached against 615 list entries, 470 relation-discovery rows of which 456 (97%) sit on anime outside the list, and 14 update rows of which 7 are invisible in the menu today because they fail the read-time eligibility test.

Judging relevance only at read time also has a behavioural cost the user does not want: adding an anime to the list surfaces updates recorded weeks earlier, presenting history as news. And it produced a live bug — *Yani Neko*, a Watching entry since 16 Aug, was announced as new on 5 Sep because a half-cached spin-off's first full fetch stored its link to *Yani Neko* for the first time, and a first fetch's whole relation set is currently treated as fresh discovery.

## What Changes

**Relevance is decided when an update is written, not only when it is read**

- Before any update row is written, the anime must be either a list entry of the user's whose status is not Dropped, or directly linked — one step, either direction, by any relation MyAnimeList reports that AniList has not contradicted — to such an entry. Otherwise nothing is recorded.
- The gate applies to every kind and every writer, so season browsing, the Top-Anime rankings and opening a stranger's detail page record nothing. Metadata caching itself is untouched: season pages still cache everything they need.
- It is the same test the read-time filter already applies, so the two can never disagree about what news exists.

**Relation discoveries are recorded only where they can be news**

- A relation-discovery row is written only when the anime whose relation set changed is the user's own non-Dropped entry. A new anime appearing on a sequel's page is not a discovery, because the sequel is not the user's entry — two steps away is out of scope.
- An anime's **first** full-detail fetch records no discoveries at all. Its whole relation set arriving at once is "we finally looked", not "new links appeared". This applies whether the anime had no row or only a lean listing row. **BREAKING** relative to the current spec, which explicitly says the opposite ("A lean row's first full fetch still discovers its relations") and is the direct cause of the *Yani Neko* bug.
- The relation comparison itself still runs for every anime: its second consumer, the series rebuild trigger, is unaffected. Only what gets *recorded* narrows.

**An announcement means an anime nobody had ever fetched**

- An anime is announced only if it had never had a full-detail fetch, tested *before* the resolver's own fetch (which would otherwise make every anime look already-known), and only if it has no list entry of its own. The existing not-finished-airing gate stays as a second guard.

**Adding an anime no longer surfaces news recorded before it was mine**

- The read-time scenario "Adding the anime surfaces its news" is removed. Under the write-time gate no such row is ever recorded, so there is nothing to surface. Dropping still retires news with no cleanup, unchanged.

**The junk already recorded is deleted**

- A one-off data migration deletes update rows whose anime fails the relevance test (7 of 14 today, all already invisible in the menu), deletes the *Yani Neko* announcement (relevant, but announced about an anime long since fully fetched), and deletes relation-discovery rows not originating on a non-Dropped list anime (456 of 470). Six update rows survive.

## Capabilities

### New Capabilities

None. This narrows an existing capability's write-side rules.

### Modified Capabilities

- `anime-updates`: recording gains a relevance gate — nothing is recorded for an anime that is neither the user's non-Dropped entry nor directly linked to one; relation discoveries are written only on the user's own entries and never from a first full-detail fetch; announcements require an anime never fully fetched and holding no list entry; season browsing and Top-Anime are named as paths that record nothing; the read-time rule that adding an anime surfaces already-recorded news is removed; and the rows recorded before these rules are retired.

## Impact

**Backend**

- `Services/Updates/AnimeUpdateRecorder.cs` — the single choke point where `AnimeUpdate` rows are written gains the relevance gate, covering all four callers (`AnimeMetadataChangeDetector`, `AnnouncementResolutionService`, `EpisodeScheduleRefreshService`'s two call sites) at once.
- New `Services/Updates/AnimeUpdateRelevance.cs` — the eligibility test extracted from `AnimeUpdateService` so the write side and the read side share one implementation; `AnimeUpdateService` keeps its reason-building on top of it.
- `Services/Updates/AnimeMetadataChangeDetector.cs` — `RecordDiscoveries` moves behind the had-full-detail gate and a list-entry check, while still returning whether edges were newly seen so `ISeriesBuildTrigger` keeps firing.
- `Services/Updates/AnnouncementResolutionService.cs` — captures never-fully-fetched and has-no-entry before the resolving fetch, and skips the fetch entirely for a discovery it can already tell will not announce.
- One EF migration, data-only, no schema change.

**Not affected**

- `Services/Season/SeasonBrowseService.cs`, `Services/Library/TopAnimeService.cs`, `Services/Metadata/MetadataRefreshService.cs` — their detector calls stay exactly as they are; the gate below them makes the calls record nothing for a stranger. Metadata caching, the series page, the relation edges table, the nightly refresh scope, display eligibility, the card layout, the 30-day window and the history overlay are all unchanged.

**Frontend**

- None.

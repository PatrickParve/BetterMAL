## Why

The Updates section is announcing premiere dates for anime that aired and finished years ago — *Clannad: After Story - Another World, Kyou Chapter* among them. Nothing happened upstream. The feed is reporting the app catching up on data it never fetched, as though MyAnimeList had just revealed it.

Two independent holes produce that:

1. **A cached row is not the same thing as a prior observation.** Three paths cache an anime *lean* — reconciliation pulling in a list entry added on MAL's own site, Top-Anime rankings, and season browsing (`ToLeanAnimeMetadata`). A lean write deliberately skips the detail-only fields, so the row sits there with `AiredFrom` null and `LastSyncedAt` unset. When that anime finally gets its first full-detail fetch, the detector sees a row that already existed and diffs against it: null → a real date reads as **premiere date released**, and the same for an episode count MAL's listing did not carry. The spec already forbids this — "Writing an anime's metadata for the **first** time SHALL record nothing" — but the code decides "first time" by whether a row exists, and a lean row exists without ever having observed those fields.

2. **The becoming-known kinds have no finished-airing gate.** The three schedule-change kinds are dropped for an anime that has finished airing, on the stated grounds that MAL moving a long-finished show's dates is MAL correcting its own records. A premiere date *appearing* on a show that finished in 2008 is the same correction, and nothing stops it. So even once (1) is closed, any full fetch that momentarily returns no `start_date` re-arms the false reveal, permanently: the "recorded at most once" guard does not protect a reveal that was never recorded.

The mirror of (1) is also live and costs the user real news: season browsing writes `AiredFrom` outside detection entirely, so a genuine premiere-date change on a browsed anime is silently absorbed and never reported.

## What Changes

**A first full-detail fetch is a first observation, whatever else is cached**

- The detector's snapshot additionally captures whether the row had **ever** had a full-detail fetch, read from `LastSyncedAt` *before* the fetch stamps it. Where it had not, the field diff records nothing — the same silence the insert path already keeps.
- The test is the row-level "never fully fetched" flag the rest of the app already uses (`RefreshTiers`, `AnimeDetailService`, `RelationResolver`), not the presence of any particular field. `metadata-refresh` already forbids inferring detail-completeness from a field being populated, and the same reasoning applies here.
- Relation discovery is unaffected: a first full fetch still records its edges, still resolves them, and still enqueues the series build, exactly as today. Only the field diff falls silent.

**A premiere date is news only while the show has not finished airing**

- **Premiere date released** joins the three schedule-change kinds under the existing finished-airing gate, evaluated once at detection against the freshly-written airing status, exactly like the announcement gate.
- **Episode count released** deliberately stays ungated. For a finished show a premiere date is an event already in the past that the app already displays; an episode count is a fact about what there is to watch, and the AniList-fallback reveal `anime-updates` explicitly specifies exists precisely for shows MAL publishes no total for.

**Lean listing writes stop being a blind spot**

- A lean listing write to a row that **has** been fully fetched now detects the kinds its own fields can produce — the episode count every lean write touches, and the premiere date season browsing writes. This is the existing "every path that writes anime data detects the updates it can" requirement, applied to the three paths that were quietly exempt.
- A lean write to a row that has never been fully fetched still records nothing, by the same first-observation rule.
- Detection over a lean write covers fields only; it never diffs relations, which a lean write does not touch.
- Season browsing's hand-written `AiredFrom` keeps its purpose (a listing has to be classifiable to its premiere season) and gains the detection it should always have had.

**The false updates already recorded are retired**

- A one-off data migration clears the **premiere date released** kind from updates recorded for an anime that had already finished airing when the update was detected, deleting rows left covering nothing. Bounded to that pairing so a reveal genuinely recorded while a show was still airing survives its later finish.
- No re-recording follows: those rows now hold a real `AiredFrom`, so the next diff finds nothing to reveal.

## Capabilities

### New Capabilities

None. The change corrects an existing capability's rules.

### Modified Capabilities

- `anime-updates`: premiere-date-released comes under the finished-airing gate that today covers only the schedule-change kinds; "writing metadata for the first time records nothing" is restated in terms of the first **full-detail** fetch rather than the first row, so a lean listing row is not a prior observation; lean listing writes are named as detection paths for the fields they write on an already-fully-fetched row; and premiere-date reveals recorded against already-finished anime are retired.

## Impact

**Backend**

- `Services/Updates/AnimeMetadataChangeDetector.cs` — `AnimeMetadataSnapshot` gains the was-ever-fully-fetched flag; `RecordFieldUpdates` returns early when it is false; the finished-airing mask widens to include `StartDateReleased`; a listing-write entry point records field updates without touching relation discovery.
- `Services/Season/SeasonBrowseService.cs`, `Services/Library/TopAnimeService.cs`, `Services/Sync/ReconciliationService.cs` — snapshot before the lean upsert and record after it, on the update branch only.
- One EF migration, data-only — no schema change. `LastSyncedAt` already carries everything the fix reads.

**Not covered**

- No change to what the AniList airing refresh records. Its episode-count reveal is already guarded by the effective total, which a lean write populates whenever MAL publishes one.
- No widening of what a lean write stores. Teaching `ApplyLeanTo` to write airing status and dates would narrow this bug by other means, but every field a non-detecting path writes is a field whose genuine change it swallows — the reason season browsing's `AiredFrom` write is a bug in the first place.
- Updates recorded for an episode count a lean row happened not to carry are not retroactively identifiable and are left as recorded.

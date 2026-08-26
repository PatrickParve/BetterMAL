## Context

The app already stores a hand-made ordering of scored anime: `TopAnimeSelections` is a flat list of anime ids with a `Position`, written by the profile page's "Edit order" overlay and read by exactly one consumer — `ProfileService.BuildTopAnimeSection`, which uses it to break ties inside a score tier while assembling the top-10 strip. Its own model comment already describes it as "an ordered preference list over all scored anime", but nothing else in the app reads it, and there is no way to reach an anime that will never be in contention for the top ten.

Everywhere else that puts anime in order by my score falls back to something with no opinion in it: `utils/anime.ts` breaks my-score ties by title, `RecapPage` breaks its top-10 ties by title, `ScoreBoardOverlay` lays each slot out by title, and `SeasonRepository` breaks them by popularity then title.

Constraints that shaped this design:

- **Single user, small data.** A large library is a few thousand entries. Every consumer except the season/year browsers already loads the whole entry list into memory per request.
- **The season and year browsers page in SQL.** `SeasonRepository.GetPageAsync` builds an `IQueryable`, orders it, and applies `Skip`/`Take`. Their my-score ordering cannot be done in memory without abandoning server-side paging.
- **The existing drag/arrow/promote editor is well-specified and works.** This change generalises it rather than replacing it.

## Goals / Non-Goals

**Goals:**

- One ranking, one ordering rule, one place it is defined — read by every by-my-score ordering in the app.
- Reach every scored anime for arrangement, not only the ones near the top ten.
- Rank an anime at the moment its score is decided (completion prompt, entry editor) rather than in a separate trip to the profile page.
- Keep the ranking correct as scores, statuses, and media types change, with no maintenance step for me to remember.
- Stay future-proof: a new "rank my X" surface should need a filter and nothing else.

**Non-Goals:**

- Ranking across scores. Score always dominates; the ranking never lets a 9 outrank a 10.
- Hand-ordering dropped or short-form anime. They are placed by band and title, permanently.
- A ranking over series/franchises. "Top series" keeps its own average-based ranking.
- Showing an anime's overall rank on its detail page, on my-list rows, or on posters. The number exists and is available; putting it on surfaces is not part of this change beyond the ranking editor's own rows.
- Any change to `/api/top-anime`, which serves MyAnimeList's ranking lists and is unrelated.

## Decisions

### D1. Rank is derived, never stored

The store keeps only what I actually decided — my explicit order — and the rank number is computed from it. There is no `Rank` column and no ranking table.

The ordering key for an anime is, in order: **my score descending → band ascending → stored position ascending (never-placed last) → title case-insensitively ascending.** Rank is the 1-based index in that order over the ranked set.

Storing rank numbers would mean rewriting them on every score change, status change, media-type refresh, and MAL reconciliation, and any missed trigger would leave a wrong, confidently-displayed number. Deriving costs one sort over a few thousand rows on surfaces that already hold those rows in memory.

*Alternative considered:* an `AnimeRank(AnimeId, Rank)` projection table rebuilt on every write. Rejected — all of the maintenance burden, none of the correctness, and (per D3) not even needed to make SQL sort by the ranking.

### D2. Four bands, derived per entry

```
HandOrdered = 0   scored, aired, not Plan to watch, not Dropped, not music/cm/pv
ShortForm   = 1   media type music, cm, or pv — not Dropped
Dropped     = 2   status Dropped, whatever the media type
Unranked    = 3   unscored, Plan to watch, or never aired
```

Bands 0–2 are the ranked set and receive rank numbers; band 3 receives none. Dropped is checked before short form, which settles the dropped-music-video case in favour of the very bottom. Band 3 exists as a band rather than as an exclusion so that the one comparator also answers "where does this go?" for the orderings that must still show an unranked-but-scored anime — my list sorted by my score, for instance, still has to place a scored Plan-to-watch entry, and it places it after every ranked entry of that score.

### D3. The ordering key is expressible in SQL, so nothing has to be materialised

Every component of the key is already a column: score and status come from `UserAnimeEntries`, media type and airing status from `AnimeMetadata`, and stored position from a left join on `TopAnimeSelections`. So `SeasonRepository`'s my-score sort can order by the ranking directly and keep paging in SQL — the reason a materialised rank table looked necessary evaporates.

The rule therefore has two implementations, which is a drift risk (see Risks). They live side by side in one `Services/Ranking/AnimeRankingKey.cs`: a `Compare` for in-memory use and an `OrderByRanking` extension for `IQueryable`, with a test that asserts both produce the same order over one fixture.

The SQL side approximates the aired gate with `AiringStatus != "not_yet_aired"` rather than the full `AiredEpisodeGate.HasAired` (which prefers a known aired-episode count). The two disagree only for an anime that is both scored and marked not-yet-aired — which the app's own editing rules forbid — and the disagreement is a band-0-last versus band-3 placement at the very tail of a score. Accepted.

### D4. `TopAnimeSelections` stays, renamed only in code

The table already is the ranking store, and its rows already mean what the ranking needs them to mean. No migration. The C# surface moves to `Services/Ranking/` and the HTTP surface to `/api/rankings`; the table, its entity, and its migration history keep their names, so nothing has to be rewritten to rename a concept.

### D5. Writes rewrite the flat list in a single slot-preserving pass

Today `ApplyTopAnimeOrderAsync` merges each edited tier, then concatenates the edited segments ahead of everything untouched. That is fine while comparisons are within-tier only, but it moves every untouched id to the back of the list — which, once dropped anime keep a stored position they may later return to, silently destroys placements.

The write becomes one pass over the existing flat list: walk it, and whenever the id belongs to an edited tier, emit the next id from that tier's new sequence instead; emit every other id unchanged; append ids that had no slot yet. Edited ids therefore refill exactly the slots edited ids already occupied, and an untouched id — a dropped anime waiting to come back — keeps its slot outright.

### D6. Scoring an anime materialises its tier, then appends

"Last in its score" is not the same as "last in the stored list". If a tier holds three never-placed members, appending a newly scored anime to the flat list makes it *first* in that tier, because never-placed members sort after placed ones.

So placement computes the tier's full hand-ordered order (placed members, then never-placed alphabetically), moves the newly scored anime to the end of it, and writes that whole sequence back through D5's pass. The tier is materialised lazily — only the tier actually touched, only when it is touched.

*Alternative considered:* a one-off migration placing every scored anime up front. Rejected — it would freeze today's alphabetical defaults into stored order across the whole library, turning the "never placed sorts alphabetically" rule into dead code and making every tier permanently explicit for no benefit.

Placement runs inside `UserAnimeEntryEditService`'s existing save, on the same `SaveChangesAsync`, so a save that reports success has already placed the anime and no score-writing site can forget to.

### D7. One read endpoint for the editor, tier-scoped

`GET /api/rankings?mediaType=<scope>&score=<n>` returns `{ scores: [{ score, count }], tier: { score, members: [...] } }` — the selector's contents in one field and the selected tier's full hand-ordered membership in the other, each member carrying its overall rank. Omitting `score` returns the highest non-empty tier. One request on open, one per tier switch.

`PUT /api/rankings/order` replaces `PUT /api/top-anime/order` with the same payload and the same merge semantics. The profile page's top-anime editor keeps reading `GET /api/profile/top-anime`, which already returns exactly the tiers it needs; only its save target moves.

Tier-at-a-time is what makes the whole-library editor viable: a score tier can hold several hundred anime and the library several thousand, and the drag interaction hit-tests with `elementFromPoint` over live DOM. Nothing is lost, because dragging has always been confined to one tier.

### D8. Pinned anime are absent from both editors

Short-form and dropped anime are not listed in the whole-library editor and not listed in the profile's top-anime editor either — one rule, both places. They still hold their places in the top-10 strip itself.

This makes the cut line's meaning worth stating precisely. Because pinned members are always at the bottom of their tier, the cut can only fall among them once every hand-ordered member of that tier is already included — so: cut inside the hand-ordered members ⇒ the cut line sits after `includedCount` listed rows, exactly as today; cut among the pinned members ⇒ every listed row is included and no cut line is drawn.

*Alternative considered:* listing them greyed out and undraggable, which keeps the tier honest and the cut line literal. Rejected on request — the editor is for anime I arrange, and rows I can never act on are clutter.

### D9. One overlay component, two modes, mounted through a context

`TopAnimeSelectionOverlay` generalises into `components/AnimeRankOverlay.tsx` with `mode: 'top' | 'all'`. Shared: rows, drag machinery (floating preview, landing marker, edge auto-scroll, cancellable drag), and auto-save (D12). Differing: `top` takes its tiers from the `TopAnimeSectionDto` the profile page already holds, draws cut lines, and keeps the original single-step-arrows-plus-promote-at-10 controls, since that boundary means something concrete there (the top-10 strip); `all` fetches its own tier, shows the score selector, draws no cut line, and — since arranging a whole score or the whole library has no top-10 boundary to promote into — gives every row a jump-to-top/jump-to-bottom pair instead (D13).

The name `RankingOverlay` is already taken by the recap's season/year ranking list, hence `AnimeRankOverlay`.

`context/AnimeRankContext.tsx` mounts one instance at the app root and exposes `openRanking({ animeId?, score?, mediaType?, onSaved? })`, mirroring `CompletionPromptProvider` and `EntryEditorContext` — including their requirement that mounting an overlay must not re-render the page underneath. The entry editor's Rank action and the completion prompt's save-and-rank both call it, passing the score they already know rather than making the overlay guess which tier to open (there is no "which tier is this anime in" lookup on the API, only a tier-by-score one); the profile page renders its own `top`-mode instance directly, since it owns that section's data, and its whole-library action calls the context, passing its own `reloadTopAnime` as `onSaved` so the strip behind it refreshes once the whole-library editor's own edits are flushed.

### D10. "Save and rank" rather than a second prompt

The completion prompt gains a third action beside Skip and Save. It saves by the identical path, then calls `openRanking({ animeId })`; on failure it does not open. A follow-up yes/no dialog would read more literally as "asking", but it puts a second modal between finishing an anime and placing it, for a question the button already asks.

The prompt's own close — and therefore the caller's view refresh — happens when the score is saved, not when the ranking editor is later dismissed, so a list refreshes behind the editor instead of waiting on it.

### D11. Rank travels on the DTOs that need to sort by it, and nowhere else

`MyListItemDto` and `RecapRowDto` gain `MyRank: int?`, because my list and the recap sort client-side and need the value to sort by. `TopAnimeEntryDto` gains it because the editor's rows display it. `AnimeBrowseItemDto` does not, because the season and year browsers sort server-side (D3) and never show a rank.

*Alternative considered:* a separate `GET /api/rankings/map` the client caches and joins against. Rejected — a second fetch that can go stale against the payload it decorates, and every consumer would have to join it by hand.

### D12. Every reorder saves itself; nothing is left to a Save button

The editor (both modes) has no Save or Cancel — a drag, an arrow, a promote, or a jump persists on its own. `moveMember` calls a debounced `scheduleSave` (600ms of quiet after the last reorder) so a burst of clicks, or a drag immediately followed by another, collapses into one `PUT /api/rankings/order` instead of one per click. Saves are chained through one promise (`saveChainRef`) so a second is never fired while the first is still in flight — without that, an older request completing after a newer one would silently clobber the newer order back to stale.

Closing the editor (its own Close control, Escape, or a click on the backdrop) flushes: it clears any pending debounce and awaits that save (or the one already in flight) before actually closing, so an edit made a moment before closing is never dropped to a timer that never got to fire. If that flush fails, the editor stays open with the error and a Retry, rather than closing over an edit that never reached the server.

*Alternative considered:* keep the explicit Save/Cancel pair. Rejected on request — the friction is exactly what "arrange, then remember to click Save" costs on an editor meant to be used many times in a row (compare an anime's score straight into place, immediately reach for the next one), and there is nothing left to *cancel*: every action here is already the thing I wanted, not a draft of it.

### D13. The whole-library editor's rows jump instead of stepping

The top-anime editor's single-step arrows plus a promote-into-10th-place control read naturally in a top-10 list — but the whole-library and single-score editors (`mode: 'all'`) have no ten to promote into, and a tier there can run to several hundred rows, where a single step at a time is impractical. Every row instead gets a jump-to-top and a jump-to-bottom control (reusing the promote glyph and its mirror), moving that row to the very first or very last position of the score in one action. The first row has nowhere to jump up to and shows only jump-to-bottom; the last has nowhere to jump down to and shows only jump-to-top.

### D14. Dragging reveals more of the list by scrolling the header away, not resizing it

The editor's title, Close control, and (in whole-library mode) the score selector live inside the same scrollable box as the anime rows, as its first item, rather than as a fixed header above a separately-scrolling list. While a row is being dragged, the box smoothly scrolls down by exactly that header block's height — revealing the top-ranked rows it was covering — and scrolls back once the drag ends.

The first version of this instead resized the header away (a CSS grid-rows collapse) while the list below flex-grew into the freed space. That reflowed the list's own position on screen, and the row being dragged — rendered dimmed in its original place, next to the floating drag preview that does track the pointer — visibly jumped out from under the cursor. A scroll instead moves the header and every row together, which is what scrolling this box has always looked like, so nothing appears to jump. Scrolling only as far as the header's own height means real rows are never pulled out of view to make room, and a drag that starts already scrolled past the header (deep in a long tier) leaves the scroll position exactly where it was rather than yanking the view back up.

## Risks / Trade-offs

- **Two implementations of one ordering rule (C# and EF) can drift** → both live in `AnimeRankingKey.cs` side by side, with a test that runs a shared fixture through the comparator and through the queryable and asserts identical output.
- **Tie orders change across the app on first load** → intended and stated in the proposal as breaking, but it means my list, the recap top 10, the score board, and the season/year browsers will all visibly reshuffle equal scores once. Nothing is lost; the previous order was alphabetical or by popularity and carried no opinion.
- **Newly scored anime now land last in their tier instead of alphabetically among the unplaced** → an anime scored 10 no longer surfaces into the middle of the top strip on its own. That is the point, but it means the top-10 strip changes less on its own than it used to, and placing a new favourite needs the ranking editor — which is exactly what the completion prompt's save-and-rank is for.
- **Materialising a tier on every score write** turns a one-row write into a few-hundred-row write for a large tier → a small table, a single user, and a write that already runs inside an existing save. Measured against the alternative (a wrong "last in tier"), it is worth it.
- **The whole-library editor still renders one full tier** → a score tier of several hundred rows is well within what the DOM handles, and the tier-at-a-time scoping (D7) is what keeps it from being a full-library render. If a single tier ever becomes the bottleneck, windowing it is a local change to one component.
- **Left-joining `TopAnimeSelections` into the season/year page query** adds a join to a paged query → the table has at most one row per scored anime and is keyed by `AnimeId`; the join is on a primary key.
- **An anime dropped and later restored returns to its slot, not to its neighbours** → D5 preserves the slot exactly, so it returns to the same depth in its tier. If I rearranged that score while it was away, the anime around it will differ. Stated in the spec rather than papered over.
- **Auto-save (D12) fires one PUT per debounced burst of reorders instead of one at an explicit Save click** → debounced and serialized so rapid successive edits collapse into one request and never race each other; closing the editor flushes whatever hasn't reached the server yet rather than losing it, and blocks the close if that flush itself fails.

## Migration Plan

1. No database migration. `TopAnimeSelections` keeps its schema, its rows, and its meaning; existing placements are honoured as-is on first load.
2. Rows already stored for anime that are now pinned (a dropped or short-form anime someone once ordered) are ignored while pinned and honoured again if the anime becomes hand-orderable. Nothing needs cleaning.
3. `PUT /api/top-anime/order` is removed in the same commit as the frontend that stops calling it; backend and frontend ship together, so no deprecation window is needed.
4. Rollback is a revert: no data is written in a shape the previous code cannot read, since the only writes are to the same flat position list it already wrote.

## Open Questions

None outstanding. The two decisions that were genuinely open — whether pinned anime appear in the editor (D8) and whether the whole-library editor is tier-scoped (D7) — were settled with the user before this document was written.

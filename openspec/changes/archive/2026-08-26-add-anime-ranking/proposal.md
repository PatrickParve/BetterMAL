## Why

Score alone is too coarse to say what I actually think: I have dozens of 8s and no way to record that one of them is my third-favourite anime and another is barely an 8. Today the only ordering I can express lives inside the profile page's "My top anime" editor, it exists only to arrange a top-10 strip, and nothing else in the app reads it — my list sorted by my score, the recap's top 10, and the score board all fall back to alphabetical order for tied scores, which is the one ordering that carries no opinion at all.

This change turns that hidden per-tier order into a first-class, app-wide **ranking**: one persisted total order over every anime I have scored, editable across my whole library rather than just the tier that happens to reach the top 10, and read by every surface that puts my anime in order.

## What Changes

- **New: a persisted, app-wide anime ranking.** One total order over every scored anime I have actually watched, derived from my score (which always dominates) plus my explicit within-score order. Every anime in it carries an overall rank number, so any subset ranking — my movies, my 2025 anime, one season — is just that filter read top-to-bottom.
- **Ranking eligibility and banding.** An anime is in the ranking when it carries a score of mine, has aired, and is not Plan to watch. Within a score tier the order runs in three bands: anime I ranked (my explicit order, then alphabetical), then Music/CM/PV entries alphabetically, then anime I dropped alphabetically. Music/CM/PV and dropped anime therefore still receive a rank, always beneath everything else of the same score, and are never hand-orderable. Unscored and Plan-to-watch anime get no rank at all.
- **"Edit order" becomes "Rank".** The profile page's "My top anime" control is renamed, and its editor keeps working exactly as it does now over the anime in contention for the top list under the selected media-type filter.
- **New: "Rank all anime" in that editor.** A second action opens the same editor — same rows, same dragging, same score-dominates rule, arrow controls suited to arranging a whole score instead of a top-10 boundary (jump a row straight to the top or bottom of its score rather than one step at a time) — over my whole rankable library rather than just the top-10 contenders, one score tier at a time. Every reorder saves itself; there is no separate Save step.
- **Rank from the entry editor.** The entry editor gains a Rank action for an entry that is in the ranking; it opens the ranking editor already focused on that anime, showing only the score tier it belongs to.
- **Rank on completion.** The completion score prompt gains a save-and-rank action, so finishing an anime, scoring it, and placing it can happen in one pass.
- **Newly scored anime land last in their tier.** **BREAKING** (behavioural): scoring an anime now appends it to the end of its score tier's explicit order, instead of leaving it unordered and sorting it alphabetically among the other unordered members of that tier. Re-scoring moves it to the end of the new tier.
- **Every "by my score" ordering in the app reads the ranking.** My list sorted by my score, the recap's top 10 and its podium, the recap score board's slots, the profile's "My top anime", and the season and year browsers' my-score sort all break equal scores by rank instead of by title or popularity. **BREAKING** (behavioural): those tie orders change.
- **BREAKING** (API): `PUT /api/top-anime/order` moves to `PUT /api/rankings/order`, next to a new `GET /api/rankings`, so the ranking is no longer named after the one section that first used it. `/api/top-anime` (MAL's own ranking lists) is untouched.

## Capabilities

### New Capabilities
- `anime-ranking`: what the ranking is (eligibility, banding, tie-breaks, rank numbers), how it is edited (the whole-library editor, tier scoping, focusing one anime), how it is maintained as scores and statuses change, and the guarantee that every by-my-score ordering in the app reads it.

### Modified Capabilities
- `profile-stats`: the "My top anime" edit control is renamed to Rank and gains the whole-library entry point; the tier order it shows and persists now bands Music/CM/PV and dropped anime to the bottom of their tier rather than mixing them in alphabetically.
- `list-editing`: the entry editor gains a Rank action; the completion score prompt gains save-and-rank; saving a score places the anime at the end of its score tier.
- `library-views`: my list sorted by my score breaks ties by rank rather than title.
- `list-recaps`: the top 10 (and its podium) and the score board's slots order equal scores by rank rather than by title.
- `season-browser`: the my-score sort breaks equal scores by rank before falling back to popularity and title.
- `year-browser`: the same change to the year's my-score sort.

## Impact

**Backend** (`backend/AnimeTracker.Api`)
- New `Services/Ranking/`: the ordering rule, the ranking snapshot (rank per anime id), placement of a newly scored anime, and tier projections for the editor.
- `Services/Profile/TopAnimeOrdering.cs`, `ProfileService.ApplyTopAnimeOrderAsync`/`BuildTopAnimeSection` move onto the shared rule.
- `Services/Entries/UserAnimeEntryEditService` gains end-of-tier placement on a score change.
- DTOs gain a rank field: `TopAnimeEntryDto`, `MyListItemDto`, `RecapRowDto`, `AnimeBrowseItemDto`.
- Ordering changes in `Services/Recap` (top 10, score board feed), `Services/Season` (the my-score sort, shared by the season and year browsers).
- Controllers: new `RankingController` (`GET /api/rankings`, `PUT /api/rankings/order`); `TopAnimeSelectionController` removed.
- No schema migration: the existing `TopAnimeSelections` table already stores a flat ordered list of anime ids and keeps serving as the ranking store.

**Frontend** (`frontend/src`)
- New `components/AnimeRankOverlay.tsx` (generalised from `TopAnimeSelectionOverlay.tsx`) and `context/AnimeRankContext.tsx`, mounted at the app root alongside the entry-editor and completion-prompt providers.
- `pages/ProfilePage.tsx` (control label, new entry point), `components/EntryEditorOverlay.tsx` (Rank action), `components/CompletionScoreOverlay.tsx` (save and rank), `context/CompletionPromptContext.tsx`.
- `utils/anime.ts` sort comparators, `pages/MyListPage.tsx`, `pages/RecapPage.tsx`, `components/ScoreBoardOverlay.tsx`.
- `api/client.ts`, `api/types.ts`.

## Why

The profile page's "My top anime" box is only editable in one narrow case — when the auto-fill boundary happens to land mid-tier — so a list of ten 10s is frozen in alphabetical order with no way to say which of my favourites actually ranks first, and there is no way to ask "what are my top movies?" separately from TV. Separately, the two opinion-divergence boxes blur MAL scores for shows I have completed even when "Always show MAL scores for completed shows" is on, contradicting that setting everywhere else in the app.

## What Changes

- **Fix**: The MAL score in "They liked it, I didn't" and "I liked it, they didn't" honours the "Always show MAL scores for completed shows" setting, like every other MAL score in the app. The divergence DTO gains the entry's watch status so the frontend can mark those scores as completed.
- **Add**: A media-type filter on "My top anime" — All (default), TV, Movie, OVA, ONA, Specials — that recomputes the top list from only the entries of that type using the exact same ranking rules. The filter is a view control only; it does not persist across visits.
- **Change**: "My top anime" becomes reorderable within each score tier, always — not just when a tie-break boundary exists. Score still dominates (a 9 can never outrank a 10), but the order of the anime *inside* a score tier is mine to set, and the edit overlay exposes every tier, not only the boundary tier.
- **Change**: Tier membership and tier order become one concept. A tier that does not fully fit shows its full membership in the overlay with a cut line: anime above the line are in the top list, anime below are not, and moving an item across the line swaps membership. This replaces the checkbox "pick N of these tied anime" overlay.
- **BREAKING**: `TopAnimeSectionDto` drops `tieBreakSlots` / `candidates` / `selectedAnimeIds` in favour of a per-tier structure, and `PUT /api/top-anime/selection` is replaced by an ordering endpoint. Both are consumed only by this app's own frontend, which is updated in the same change.
- The persisted ordering is global (one preference per anime, tier derived from its current score); filtered views are views over it, and reordering inside a filtered view preserves the relative positions of the entries that filter hides.

## Capabilities

### New Capabilities

None — this extends existing profile behaviour.

### Modified Capabilities

- `profile-stats`: The "My top anime" requirement is replaced — tier-internal manual ordering is always available (not only at a tie-break boundary), membership at the cut is expressed as ordering, and a media-type filter (All/TV/Movie/OVA/ONA/Specials) recomputes the list over a subset using the same rules.
- `score-visibility`: The always-show-completed-scores requirement gains an explicit scenario that it applies to *every* MAL score in the app, including the profile page's opinion-divergence lists.

## Impact

- Backend: `ProfileService.BuildTopAnimeSection` (rewritten around score tiers + a global ordering), `ProfileDto` (`TopAnimeSectionDto`, `OpinionDivergenceItemDto`), `ProfileController` (new filtered top-anime endpoint), `TopAnimeSelectionController` (ordering endpoint replaces the selection endpoints), `TopAnimeSelection` model + repository (gains an explicit `Position`), one EF migration (auto-applied on startup).
- Frontend: `ProfilePage.tsx` (filter control, always-visible edit control, completed-aware divergence scores), `TopAnimeSelectionOverlay.tsx` (rewritten as a tier reorder editor), `api/types.ts`, `api/client.ts`, and the associated CSS.
- No MAL API calls are added — everything still comes from cached Postgres data.

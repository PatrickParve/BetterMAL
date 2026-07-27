## Context

"My top anime" is computed server-side in `ProfileService.BuildTopAnimeSection` from all `UserAnimeEntry` rows: every score-10 entry uncapped, then next-highest tiers alphabetically until the list reaches 10. The only user control is `TopAnimeSelections` — an unordered *set* of anime ids that fills the remaining slots when the fill boundary lands mid-tier. So ordering inside a tier is never expressible, and the edit control is hidden whenever no boundary tier exists (e.g. exactly ten 10s).

Related, `OpinionDivergenceItemDto` carries no watch status, so `ProfilePage`'s `DivergenceList` cannot pass `completed` to `<ScoreValue>` the way `MyListPage`, `TopAnimePage` and `AnimeDetailPage` all do — hence completed shows' MAL scores stay blurred there even with the always-show-completed setting on.

Constraints: single-user app, all data served from cached Postgres (no MAL calls on this path), EF migrations auto-apply on startup via `db.Database.Migrate()`, no backend test project.

## Goals / Non-Goals

**Goals:**
- One persisted ordering that expresses both tier order and truncated-tier membership.
- Tier ordering editable always, not only at a fill boundary.
- Media-type filtered views that reuse the exact same ranking code path.
- Divergence-list MAL scores respect the always-show-completed setting.

**Non-Goals:**
- Cross-tier ordering (a 9 outranking a 10) — score always dominates.
- Reordering by drag directly in the profile strip (the strip keeps its drag-to-scroll gesture); ordering happens in the overlay.
- Persisting the selected media-type filter across visits.
- Media types outside the six listed options (`music`, `cm`, `pv`, `unknown` remain reachable only under All).
- Renaming the `TopAnimeSelections` table/entity, even though its meaning widens.

## Decisions

### 1. One global ordering, filters are views over it

Store an explicit `Position` on each `TopAnimeSelection` row and treat the table as an ordered preference list of anime ids, tier-agnostic — an anime's tier is always derived from its *current* score, so re-scoring an anime moves it between tiers without touching stored order.

Resolution, for a scope (All or one media type):

1. Take scored entries, apply the media-type filter, group by score, order tiers descending.
2. Order each tier by `(storedPosition ?? int.MaxValue, Title alphabetical, case-insensitive)` — explicitly ordered members first, unordered ones alphabetically after.
3. Emit the score-10 tier in full (uncapped, unchanged rule). Then emit lower tiers in descending order while fewer than 10 items exist, truncating the tier that overflows.
4. `IncludedCount` per tier = how many of that tier's members reached the list (`Members.Count` for tiers that fully fit).

*Alternative considered*: a separate ordering per media-type scope (7 orderings). Rejected — it forces the user to re-order the same anime up to twice, and makes "what does All show?" ambiguous after editing a filtered view.

### 2. Slot-preserving writeback for edits made under a filter

A save sends the tier orders *as displayed in the current scope*, which may omit tier members the filter hides. Per tier the server:

1. Recomputes `E` = the tier's full current effective order (rule 2 above) over **all** members, unfiltered.
2. Collects the indices in `E` held by the incoming visible ids.
3. Refills exactly those indices with the incoming ids in their new order, leaving hidden members where they sat.
4. Persists `E'` — so every member of an edited tier becomes explicitly ordered.

Persistence is a wholesale replace: new rows = edited tiers' `E'` concatenated in descending score order, then any previously stored ids not in those tiers keeping their old relative order, with `Position` assigned sequentially. Last write wins (single user; matches the existing replace semantics).

### 3. API shape

`TopAnimeSectionDto` becomes tier-shaped — `tieBreakSlots`, `candidates` and `selectedAnimeIds` are removed:

```csharp
public record TopAnimeTierDto(int Score, List<TopAnimeEntryDto> Members, int IncludedCount);
public record TopAnimeSectionDto(List<TopAnimeEntryDto> Items, List<TopAnimeTierDto> Tiers, string MediaType);
```

`Tiers` carries only tiers that contribute to the list, with the truncated tier listed in full so the overlay can render members below the cut line. Tiers that can never reach the list are omitted (score dominance means they can never be promoted).

- `GET /api/profile` keeps embedding the `all` section, so first paint is unchanged.
- `GET /api/profile/top-anime?mediaType=<all|tv|movie|ova|ona|special>` returns a `TopAnimeSectionDto` for one scope; unknown values → 400.
- `PUT /api/top-anime/order` with `{ "mediaType": "...", "tiers": [{ "score": 10, "animeIds": [...] }] }` replaces `PUT|GET /api/top-anime/selection`, which are deleted along with `TopAnimeSelectionRequest`. Validation: unknown anime ids → 400 (existing `UnknownAnimeIdsException`), and any id whose current score differs from its stated tier score → 400, which keeps score dominance impossible to violate through the API.

*Alternative considered*: returning all six scopes inside `GET /api/profile`. Rejected — it multiplies the profile payload for a control most visits never touch, and the overlay needs full tier membership per scope.

### 4. Media-type mapping

Case-insensitive match on `AnimeMetadata.MediaType`: `tv`→`tv`, `movie`→`movie`, `ova`→`ova`, `ona`→`ona`, `special`→`special` **or** `tv_special` (MAL emits both). `all` applies no filter. Entries with a null or other media type appear only under All.

### 5. Overlay becomes a tier order editor

`TopAnimeSelectionOverlay` is rewritten as a tier editor: one section per tier, each listing its members in order, with a cut line rendered after `includedCount` when `includedCount < members.length` ("above this line makes the top list"). Reordering uses native HTML5 drag-and-drop on rows plus ↑/↓ buttons for keyboard/accessibility — no new dependency. Save sends every tier's current order for the active scope; the profile page then refetches the section for that scope.

The profile page's edit control becomes visible whenever any tier has more than one member, and the filter is a row of tabs styled like `MyListPage`'s status tabs for consistency.

## Risks / Trade-offs

- **Existing `TopAnimeSelections` rows have no order** → Backfill `Position = 0` for all; ordering falls back to alphabetical within the tie, and because stored ids still sort ahead of unstored ones the previously chosen *membership* is preserved. The first save from the new UI writes real positions.
- **Editing under a filter has non-obvious effects on the All list** → The slot-preserving writeback keeps hidden members adjacent to the same neighbours, and the spec states the shared-ordering behaviour explicitly; the overlay hint names the active filter.
- **Breaking DTO/endpoint change** → Backend and frontend ship together in this change; the only consumer is this app's own frontend.
- **`Position` uniqueness is not enforced** → Ordering reads sort by `(Position, AnimeId)` so a duplicate or backfilled tie is still deterministic rather than an error.
- **Entity name `TopAnimeSelection` now means "ordered preference"** → Renaming was rejected as churn; XML doc comments on the model, repository and interface are updated to describe the widened meaning.

## Migration Plan

1. Add `Position` (int, NOT NULL, default 0) to `TopAnimeSelections` via a new EF migration; it auto-applies on startup, no manual step and no data loss (all existing rows keep their membership).
2. Ship backend and frontend together — the removed `/api/top-anime/selection` endpoints have no other consumer.
3. Rollback: revert the code; the extra column is inert for the old code path, which selects only `AnimeId`.

## Open Questions

- Should Specials include MAL's `tv_special` alongside `special`? Assumed **yes** — treated as one option.
- Should a filtered view with zero scored entries show an empty state distinct from "score some anime to build your top list"? Assumed a short "No scored <type> yet." message.

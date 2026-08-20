## Context

Four independent complaints, two surfaces:

**Search / season cards.** `AnimeBrowseItemDto` already carries `malScore` (both the season repository and the search service project it) but neither page renders it — `AnimeCardMeta` prints only `{TYPE} · {N} ep`. Separately, the type-ahead dropdown clips text: measured in headless Chrome against the running app's own CSS at the app's largest fluid root font (18px, reached at any window ≥ 1280px), `.search-bar__result-title` has `line-height: 20px` and `overflow: hidden` while the glyph content area of 18px system-ui is 21px (`fontBoundingBoxAscent` 17 + `fontBoundingBoxDescent` 4) — `scrollHeight` 21 against `clientHeight` 20. Half-leading is negative, so ~0.5px is shaved off each end and lands as a visible cut along the top. The same measurement clears `.series-badge` (12px of glyph in a 16px box) and `.series-badge__pill` (12px in 14px), so the badge is not provably clipped today; it is nonetheless sized with less slack than anything else on that row.

**Series page.** Traversal follows eight story relations and deliberately not `other`; MAL links a franchise's songs with `other` and nothing else. In the current database, 439 `other` rows exist and 80 music entries hang off one by a non-music parent — `Idol` ← `[Oshi no Ko]`, `Let You Down` ← `Cyberpunk: Edgerunners`, `I-Bull`/`Rasen` ← `Bleach: Sennen Kessen-hen`, fifteen more under `One Piece`. Because they are unreachable, `Idol` and its Hololive cover (linked to each other by `alternative_version`, which *is* traversed) currently form their own two-member series #53.

The More section's visibility rules were written to protect the user from their own collapse action (`hasWatchProgress` pins progressed entries open inside a collapsed group; `allExtrasCompleted` removes the collapse controls entirely; `EXTRAS_COLLAPSE_THRESHOLD = 12` decides the initial state). The user wants those decisions handed back to them, with "only what's in my list" as the opening view.

## Goals / Non-Goals

**Goals:**
- A MAL score on every season/search card, aligned with the existing meta line, and gone entirely — not eye-iconed — while scores are hidden.
- No clipped glyphs in search results at either end of the fluid root font range (16–18px).
- A franchise's music entries appear in its More section, and building the series from the song produces the same series as building it from the show.
- More-section visibility driven by two explicit controls, defaulting to "only the extras in my list".
- `Highest MAL score` readable once the entry is settled (Completed **or** Dropped), scored or not.
- More tiles wide enough for `TV special · 2003 · 99 ep`.

**Non-Goals:**
- Traversing `other` in general, or any of `alternative_setting` / `character` / `adaptation`.
- Any API or database schema change; no new field on `AnimeBrowseItemDto`.
- Persisting the More section's filter/collapse state across navigations (it stays transient view state, like today).
- Reworking the score chips on series entry rows/tiles, the detail page, or anywhere else a reveal control is already established.

## Decisions

### 1. Music joins a series through `other`, but only when exactly one end of the relation is a cached music entry

`SeriesRelations` grows a second predicate beside `IsTraversable(relationType)`: an edge is traversable when its relation type is in `TraversalSet`, **or** when its relation type is `other` and exactly one of its two endpoints has cached `MediaType == "music"`. `SeriesGraphBuilder.TraverseAsync` applies it on both edge directions it already collects:

- **Outgoing** (`metadata.RelatedAnime`): if the current member is music, every `other` neighbour that is *not* music qualifies (this is the song → show hop); if it is not music, an `other` neighbour qualifies only when that neighbour's cached row says `music`. Neighbour media types come from one batched lookup per visited node (`AnimeMetadata.Where(a => otherNeighbourIds.Contains(a.Id)).Select(a => new { a.Id, a.MediaType })`), not a per-edge query.
- **Incoming** (`db.AnimeRelatedAnime.Where(r => r.RelatedAnimeId == animeId …)`): the existing `TraversalSet.Contains(r.RelationType)` filter gains an `|| (r.RelationType == "other" && …)` branch, resolved with the same media-type knowledge — the current node's own type is already loaded, so when it is music every incoming `other` source qualifies, and when it is not, only sources whose cached type is `music` do.

An `other` neighbour with **no cached metadata row at all** is not traversed and costs no fetch budget. The builder cannot tell a song from a commercial without fetching, and `other` is also how MAL links CMs, PVs, pilots, and crossovers (`Kamiusagi Rope x Boruto`, `Google Play … Android User Datta`); spending budget to find out would both blow the visit budget and risk admitting exactly the entries the traversal set exists to keep out.

*Alternatives considered.* **(a) Traverse `other` unconditionally** — rejected: crossover shorts would fuse unrelated franchises, which is the documented reason `other` was excluded. **(b) Admit music as non-expanding leaves** (attach a song to whatever found it, never traverse out of it) — rejected because it makes the component seed-dependent: a build seeded at `Idol (Mori Calliope Cover)` would reach only `{cover, Idol}`, and `PersistAsync`'s largest-overlap rule would then treat the stored Oshi no Ko series as the overlapping row to keep and *replace its member set with those two*, wiping the franchise until the next visit rebuilt it, and oscillating from then on. The symmetric "exactly one end is music" formulation is what guarantees both seeds compute the same component, which is the property the persistence rule depends on. **(c) Both-ends-music `other` edges** — not traversed: cover/remix links between songs already travel over `alternative_version`, and admitting them would let one shared song chain hop between franchises.

### 2. Force one rebuild of every stored series

`SeriesGraphBuilder.ClassificationRevisedAt` is bumped to this change's ship date. `SeriesService.NeedsBuild` already rebuilds any series whose `BuiltAt` predates it, so every stored series picks up its music members on its next read, and music-only series (#53 and friends) are absorbed by `PersistAsync`'s existing largest-overlap-wins/delete-the-rest path — no migration script, no manual rebuild clicking. The existing bulk-build background service covers series nobody visits.

### 3. More section: one filter flag, one collapse map, one per-group override

Replacing `collapsedGroups` + `hasWatchProgress` + `allExtrasCompleted` + `EXTRAS_COLLAPSE_THRESHOLD` with three pieces of transient state in `SeriesPage`:

| State | Default | Meaning |
| --- | --- | --- |
| `mineOnly: boolean` | `true` | the "In my list" toggle |
| `collapsedGroups: Record<key, boolean>` | all `false` | per-group collapse, as today |
| `unfilteredGroups: Set<key>` | empty | groups where "+N more" was used while filtering |

Visible tiles for a group = `collapsed ? [] : (mineOnly && !unfiltered.has(key) ? items.filter(e => e.entry != null) : items)`. Note the filter is `entry != null` — membership, not status — so Dropped and Plan-to-watch count exactly like Completed.

Control behaviour:
- **In my list** (a two-state toggle, `aria-pressed`, styled like the app's other switch-ish filter chips): sets `mineOnly`, clears `unfilteredGroups`, and expands every group, so the effect of turning it on is always visible rather than hidden behind a collapsed group.
- **Expand/collapse all**: reads `Collapse (all)` only when *everything* is currently visible (no group collapsed, and nothing hidden by the filter); otherwise `Expand (all)`. Expand sets `mineOnly = false`, clears overrides, expands every group. Collapse collapses every group and clears overrides, leaving nothing rendered — including in-list tiles, which is the explicit ask. The `all` suffix keeps its existing single-group rule.
- **Per-group caret**: unchanged toggle of that group's collapse.
- **"+N more"**: now appears whenever a group renders fewer tiles than it holds — collapsed *or* filtered. On a collapsed group it expands the group (into the current filter mode); on a filtered group it adds the group to `unfilteredGroups`, revealing the rest of that group alone.

The one-off initialisation effect (`collapseInitialised`) disappears with the threshold: every group simply starts expanded, and the filter does the quieting. State stays per-mount, resetting on navigation like every other transient view state on this page.

*Alternative considered:* a single tri-state per group (`collapsed | mine | all`) with no global flag. Rejected — the "In my list" control then has no state of its own to report to assistive technology, and would have to derive its pressed-ness from a quorum of groups, which reads wrong the moment one group is overridden.

### 4. Card score: a second span in the meta line, absent while hidden

`AnimeCardMeta` takes `malScore: number | null` and renders `<span class="anime-card__meta">` as a flex row with `justify-content: space-between`: the existing `{TYPE} · {N} ep` text at the start, and — only when a score should be drawn — `<span class="anime-card__meta-score score--mal">{malScore.toFixed(2)}</span>` at the end. `ScoreValue` is deliberately **not** used: its hidden branch always renders the reveal button, which is exactly what this card must not do. The condition is `!hidden && malScore != null`, read straight from `useScoreVisibility()`; `alwaysShowCompletedScores` is not consulted, per the decision recorded in the score-visibility delta (these two pages list arbitrary MAL anime and carry no watch status in their payload, and honouring the setting would mean adding `status` to `AnimeBrowseItemDto` and both projections behind it to put a stray figure back on scattered cards in a grid).

Both callers (`SeasonPage`, `SearchPage`) pass `item.malScore`. The series cards on the search page keep rendering `SeriesBadge` in that slot and gain nothing.

### 5. Line boxes sized from measured glyph metrics

- `.search-bar__result-title`: `line-height` 20px → **22px** (needs ≥ 21px at the 18px root font; 22 keeps a whole pixel of slack and stays under the row budget).
- `.series-badge__pill`: `line-height` 14px → **16px**, making the pill 18px tall with its borders. Not provably clipped today, but it is the tightest box in the badge and the reported symptom names it; 16px puts its 12px of glyph in the same slack every other line has. The results-page card is unaffected structurally — the badge line sits inside the card's inherited 26.1px line box either way, so the "a series card is no taller than the anime cards beside it" requirement still holds.
- `.search-bar__result`: `height` 52px → **56px**. Content budget becomes 44px against 22 + 2 gap + 18 = 42px, restoring the ~2px of slack the row had before. Fixed height is kept (not `min-height`), because uniform row height is a spec requirement of its own.

The comments in `SearchBar.css` that derive the old 52/20/16 arithmetic are updated to the new numbers and to *why* the numbers are what they are (glyph content area at the largest root font), so the next person adjusting them has the constraint rather than the result.

### 6. Extras grid track width from the measured worst case

`.series-page__extras-grid` track `minmax(140px, 1fr)` → **`minmax(172px, 1fr)`**. Measured: `TV special · 2003 · 99 ep` is 142.2px at the tile's 12px font, plus ~4.3px of the app's 0.18px letter-spacing across 24 characters, plus the tile's 20px of horizontal padding ≈ 167px. 172px leaves a little slack for a wider system font. Landscape tiles, which span two tracks, grow with it — acceptable, and the `--extras-gap` arithmetic in `SeriesExtraTile.css` is expressed in percentages so it needs no change. The one-line-with-ellipsis rule stays as the backstop for genuinely longer strings.

### 7. Highest MAL score reveals on settled status

`isCompletedAndScored` is replaced by the shared `isScoreRevealableStatus(entry.entry?.status)` (`Completed || Dropped`) already used for score reveal across the app, dropping the `myScore > 0` half. Dropped entries frequently carry no score of the user's, so requiring one would keep the stat hidden indefinitely — and the box guards against learning *which entry is best* while it is still ahead of you, a concern a dropped entry no longer raises. The same predicate feeds the `completed` prop of the `ScoreValue` inside the box, so title, link, and number reveal together.

## Risks / Trade-offs

- **A song related to two different franchises fuses them** → measured: of 80 music entries reachable this way in the current database, exactly one (`Peaceful Times (F02) Petit Film`) has two non-music parents, and both are Evangelion entries already in one series. Accepted; if a real fusion shows up, the narrower fix is to skip `other` expansion out of a music node with more than one distinct non-music parent, at the cost of the seed-symmetry property decision 1 relies on.
- **Series grow by a dozen-plus music entries (One Piece, Bleach)** → they land in the Music group of More, which is collapsible and, by default, filtered to what is in the user's list, so the page does not get longer for anyone who has not tracked songs. The 400-member cap is untouched and remains far away.
- **The forced rebuild re-traverses every series on next read** → same one-shot cost the last classification revision paid; builds are single-flighted through `RefreshGate` and bounded by the existing 8-fetch visit budget.
- **Removing `hasWatchProgress` means "Collapse all" can now hide something the user is mid-way through** → that is the requested behaviour, and the default filtered view shows exactly those entries, so the only way to hide them is to ask for it explicitly.
- **A grid of cards with scores absent while hiding is on looks different from every other surface** (where a hidden score leaves a reveal control) → recorded as an explicit exception in the score-visibility spec rather than left as an inconsistency; nothing on the card can reflow, since the score is the last thing on its line.
- **The badge's line-height change is speculative** (measurement shows it clears today) → cost is one extra pixel of pill height on a line that has 8px of room; verification in the running app is a task, not an assumption.

## Migration Plan

Deploy as one change. On the first read of each series after deploy, `NeedsBuild` sees `BuiltAt < ClassificationRevisedAt` and rebuilds it under the new traversal; music-only series disappear as they are absorbed. Rollback is reverting the code — series stored with music members are still valid under the old rules (the old traversal simply never re-adds them), and the next rebuild after a revert drops them again.

## Open Questions

None outstanding. The two decisions that were genuinely open — whether `Highest MAL score` should still require a score of the user's (no: status alone) and whether "Always show MAL scores for completed and dropped shows" should reopen scores on browse cards (no: hidden means hidden there) — were settled with the user before this document was written.

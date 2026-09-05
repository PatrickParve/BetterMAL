## Context

An extra's relation group is decided once, at build time, by `SeriesGraphBuilder.ResolveExtraGroups`, and persisted on `SeriesMember.RelationGroup`. `SeriesPage.tsx` does nothing but bucket consecutive same-group entries and label them from `RELATION_GROUP_LABELS`; a null group renders as "Other". So every wrong heading in the More section is a wrong string in the database, written by one 50-line method.

That method takes four inputs: the telling's ids, its main-line ids, its version-neighbour ids, and `edges`. Everything turns on what is in `edges`, and `edges` is assembled in two places:

```csharp
// TraverseStoryComponentAsync — per dequeued member
var storyOutgoingEdges = metadata.RelatedAnime
    .Where(r => SeriesRelations.IsTraversable(r.RelationType))   // StoryTraversalSet only
    .Select(r => new RelationEdge(animeId, r.RelatedAnimeId, r.RelationType));
…
foreach (var edge in storyOutgoingEdges.Concat(companionOutgoingEdges))
    if (!contradictedFarEndIds.Contains(edge.RelatedAnimeId))
        edges.Add(edge);
```

```csharp
// DiscoverVersionNeighboursAsync — after the component is complete
.Where(r => SeriesRelations.VersionRelations.Contains(r.RelationType) && !memberIds.Contains(r.RelatedAnimeId))
```

A version relation whose two ends are both component members satisfies neither: `IsTraversable` is `StoryTraversalSet.Contains`, which excludes the version relations by design (`TraversalSet` narrowed back to story relations in `rebuild-series-by-story-component`), and the neighbour query explicitly skips far ends that are members. It is stored in `AnimeRelatedAnime`, it is visible in the detail page's related-anime overlay, and it is invisible to the one method that decides how the More section labels it.

`ResolveExtraGroups` then compounds it. Its first pass matches only against `mainLineIds`:

```csharp
var edgesToMainLine = scopedEdges
    .Where(e => (e.OwnerId == extraId && mainLineIds.Contains(e.RelatedAnimeId)) ||
        (e.RelatedAnimeId == extraId && mainLineIds.Contains(e.OwnerId)))
```

and its BFS second pass excludes version neighbours from both ends of the walk. A version neighbour attached to a non-main-line member therefore matches nothing in pass one, is barred from pass two, and lands on `TryAdd(extraId, RelationGroup.Other)`.

Measured on the running instance (246 series, 1,553 members):

| | |
|---|---|
| Non-main-line members carrying a version relation to another member of their own series | 130 |
| …of which have no group stored at all — built before the column existed, series not read since | 37 |
| …correctly grouped Alternative version / Alternative setting | 60 |
| …grouped by a story relation to the main line instead (Prequel 11, Side story 9, Sequel 2, Summary 1) | 23 |
| …grouped Other | 10 |
| Of those last 33, the ones the tiered rule below actually regroups | **16** |
| Non-main-line members with no relation group stored at all, across the whole library | 307 |

The 23-vs-8 gap matters: most of the story-grouped ones *should* stay where they are. *Turn A Gundam* carries `sequel` into the main line and `alternative_setting` to a non-main-line member; it is a sequel of this series and an alternative setting of one entry in it, and "Sequel" is the honest heading. Only the 8 whose version relation names a **main-line** member are mislabelled by mechanism 1.

## Goals / Non-Goals

**Goals:**

- `series-page`'s existing rule 1 — *"a relation, in either direction, to any main-line member… Version relations rank first"* — becomes true of version relations too, which it currently is not.
- An entry that is an alternative version of *something in this series* reads as one, rather than as "Other", without needing that something to be main line.
- An extra with a genuine story relation to the main line keeps the group that relation gives it, whatever version relations it also carries.
- The correction reaches the 246 already-stored series without a data migration or a manual rebuild of each.
- No change to which anime a series contains, to the main line, to the version slots, to any stat, or to any average.

**Non-Goals:**

- Traversing version relations. They stayed out of `TraversalSet` for a reason — `rebuild-series-by-story-component` D1 — and this change records them for labelling only.
- Reaching further in `SeriesService.ProjectRelatedEntriesAsync`. It reads relations of main-line members only, so an uncached alternative version of an *extra* is absent from the page entirely rather than mislabelled. Fixing that changes what the page lists and needs its own bounding decision (how many hops, and at what fetch cost); it is not a labelling bug.
- Inferring version relations MAL did not state. A retelling MAL types `other` will keep reading "Other", and correctly so — the app reports its source.
- Splitting "alternative version of the main line" from "alternative version of an extra" into separate headings. See Open Questions.
- Any frontend change. `RELATION_GROUP_LABELS` already has every group and the section renders whatever the server sends.

## Decisions

### D1 — Record version edges during traversal, alongside the story edges, without following them

`TraverseStoryComponentAsync` gains a third edge source beside `storyOutgoingEdges` and `companionOutgoingEdges`:

```csharp
var versionOutgoingEdges = metadata.RelatedAnime
    .Where(r => SeriesRelations.VersionRelations.Contains(r.RelationType))
    .Select(r => new RelationEdge(animeId, r.RelatedAnimeId, r.RelationType));
```

recorded into `edges` under the same `contradictedFarEndIds` filter as the others, and **not** concatenated into `outgoingIds`. That single omission is what keeps the traversal a story-component traversal: nothing new is enqueued, no component grows, splits, or merges, and `TraverseAsync`'s member set is byte-identical to today's.

The existing tail filter does the partitioning for free:

```csharp
var finalEdges = edges.Where(e => memberIds.Contains(e.RelatedAnimeId)).ToList();
```

A version edge to a non-member is dropped here — which is exactly right, because `DiscoverVersionNeighboursAsync` records that same edge a moment later, under its `!memberIds.Contains(...)` guard. The two sources stay disjoint by construction: traversal keeps member→member version edges, discovery keeps member→neighbour ones. No de-duplication is needed and none is added.

Recording only a member's *own* declared relations is deliberate and matches the existing comment: an edge stored the other way round is captured when its owner is dequeued in its own turn. A lean member contributes nothing from its side, as with story edges today, and is covered from the far end's side.

*Alternative rejected — resolve version relations at read time in `SeriesService`, leaving the builder alone.* `SeriesService` already re-reads relations for the related-entry projection, so the data is at hand. But it would put the group of a *member* in two places — the persisted column for story relations, a read-time override for version ones — and every consumer of `SeriesMember.RelationGroup` (the ordering in `BuildDisplayExtrasAsync`, the persisted `Order` within a group, `SeriesRankingIndex`) would then be reading a value the page contradicts. The group is a build-time property; it belongs in the build.

*Alternative rejected — widen `IsTraversable` and subtract version relations at the enqueue site.* Same edges, but `IsTraversable` is called from `SeriesGraphBuilder`, `SeriesService` and `AdjacentAnimeSet`, and its meaning ("the same story, the same telling") is load-bearing in all three. A separate expression at one call site is cheaper than a redefinition at three.

### D2 — Group resolution becomes three tiers, not two

`ResolveExtraGroups` resolves each extra in order:

1. **Relations to a main-line member** — unchanged: `HighestPrecedenceGroup` over every edge, in either direction, between the extra and any main-line member. With D1's edges present, a version relation now competes here, and wins, since Alternative version and Alternative setting are orders 0 and 1 in `SeriesRelationGroupOrder`.
2. **Version relations to any other member** — new. An extra with nothing from tier 1 but a version relation to some member of the telling takes that relation's group (Alternative version over Alternative setting when it has both, by the same order).
3. **Inheritance, then Other** — today's BFS and today's fallback, unchanged in rule.

Tier 2 is confined to version relations rather than accepting any relation to any member, and the reason is that tier 3 already covers the rest, better. An extra whose only relation is a `side_story` to another extra should read as whatever that extra reads as — which is what inheritance gives it — not as "Side story" flatly. Version relations are the only ones where the relation *itself* names the group correctly regardless of what it points at: an alternative version of a side story is an alternative version.

The tier ordering is what protects the 23 story-grouped members that are grouped correctly today. *Uma Musume: Pretty Derby - Road to the Top* has `side_story` to the main line and `alternative_version` to its own movie: tier 1 fires, it stays "Side story". Its movie has neither a main-line relation nor any story relation to a member: tier 1 and tier 3 both give nothing, tier 2 gives "Alternative version". The two entries end up in different groups, which is the truth about them.

*Alternative rejected — accept version relations to any member in tier 1, ranked by the same precedence.* One pass instead of two, and it reads more simply. It also moves *Road to the Top* itself into "Alternative version" — because Alternative version outranks Side story — which is wrong: it is a side story of this series that happens to have a movie cut. A relation to the main line has to outrank a relation to an extra, and precedence within one flat set cannot express that.

### D3 — The inheritance walk still travels story edges only, and is still seeded from the main line only

Two constraints on the BFS, both preserving guarantees `series-page` already states:

- Version edges are excluded from the walk's adjacency. Otherwise D1's new edges would let a story extra inherit through an `alternative_version` link, breaking *"a story extra is never labelled an alternative version merely for sitting next to one"* — the guarantee the version-neighbour exclusion exists to give.
- The queue is seeded from tier-1-resolved extras only. A tier-2-resolved extra does not seed it, for the same reason a version neighbour does not: its group came from a version relation, and passing that outward is the thing the spec forbids. This keeps the spec's wording literal — inheritance resolves *"breadth-first outward from the main line"*.

Version neighbours remain excluded from the walk at both ends, exactly as today.

### D4 — A named helper for tier 2, beside `HighestPrecedenceGroup`

`SeriesRelations` gains a sibling to `HighestPrecedenceGroup` that takes the same `(RelationType, ExtraIsOwner)` shape, keeps only the version relations, and returns the highest-precedence group among them or null when there are none. Both tiers then state their rule in the file whose job is to hold relation semantics, and `ResolveExtraGroups` stays a three-branch decision rather than growing a second inline LINQ precedence sort. `ResolveDirectional` is unchanged — version relations already resolve identically in both directions.

### D5 — `ClassificationRevisedAt` carries the correction to stored series

The stamp exists for this: `SeriesService.NeedsBuild` returns true for `series.BuiltAt < SeriesGraphBuilder.ClassificationRevisedAt`, so moving it to this change's ship date makes every stored series rebuild on its next read. That covers both the 16 regroups and the 307 null groups in one mechanism, with no backfill job, no SQL migration, and no user action.

The comment above the constant currently says "whenever `ClassifyMainLineChain`'s rules change". It is widened to what the constant actually guards — any change to how a build classifies its members, main line or extras — since this change touches the second and not the first.

*Alternative rejected — a one-off SQL migration recomputing `RelationGroup`.* The rule needs the edge set, and the edge set is what the traversal produces; recomputing it in SQL means reimplementing the resolver against `AnimeRelatedAnime`, in a second language, permanently out of sync with the first.

*Alternative rejected — leave the stamp alone and let the 30-day staleness window carry it.* It would arrive eventually and invisibly, which is worse than arriving on the next read: a user comparing two series pages during that month would see two different rules.

## Risks / Trade-offs

**Every stored series rebuilds on next read, in a burst.** → This is the established path (the stamp was last moved on 2026-08-27) and the cost is bounded per build: `VisitFetchBudget` is 8 fetches and `VisitProbeBudget` 4 probes, spent only on members with no cached row, and `SeriesService` collapses concurrent rebuilds of one series onto a single build. A fully cached series rebuilds with no MAL call at all, which is the common case for a library that has been browsed. The burst is spread over reads rather than landing at once.

**A rebuild can change more than the relation group.** → Any rebuild re-derives the main line, the version slots and the order from current cached metadata, so a series whose relations changed on MAL since its last build will also pick that up. That is the intent of the stamp and not a side effect of this change, but it does mean the visible diff after shipping is not strictly limited to the 16 members counted above.

**A genuine side story that MAL also types `alternative_version` to a main-line member moves to "Alternative version".** → *Attack on Titan*'s `Kakusei no Houkou` and *Gundam Evolve* are in this bucket. It is exactly what `series-page`'s stated precedence asks for ("Version relations rank first"), it is what Fate/Zero already does, and the alternative is to invent a tie-break MAL gives no basis for. Recorded here because it is the change's most visible edge.

**"Alternative version" is order 0, so a series that gains one gains a new first heading in the More section.** → The section opens with every group collapsed, so the cost is one extra collapsed row, not a wall of tiles. The group is also the one the reader is most likely to be looking for when it exists.

**`edges` grows.** → It is consumed by `ResolveExtraGroups` alone (`ClassifyMainLineChain`, `SeriesVersionSlots.Resolve` and `TopologicalMainLineOrder` take members, not edges), the growth is bounded by the version relations MAL states — a handful per member — and both passes over it are linear.

## Migration Plan

1. Ship the builder change and the bumped stamp together. A build under the new code with the old stamp would regroup only newly built series, which is the two-rules-at-once state D5 rejects.
2. Stored series regroup lazily, on next read. Nothing needs running.
3. To regroup the whole library at once, the settings page's existing "build all series from my list" run does it — no new control.
4. Rollback is a revert: the stamp goes back with the code, and the next read rebuilds each series under the restored rules. No data is shaped in a way the old code cannot read — `RelationGroup` is the same column holding the same enum names.

## Open Questions

- **Should an alternative version of an extra share the heading with an alternative version of the main line?** This change says yes: one "Alternative version" group, ordered by aired date, whatever each entry is a version *of*. A reader looking at *Road to the Top (Movie)* under that heading is not told which entry it retells. The alternative — a qualified heading, or ordering versions-of-the-main-line first — is a real option, but it is a presentation decision worth taking on its own evidence rather than folding into a correctness fix.
- **The related-entry projection's main-line-only reach** (Non-Goals) leaves uncached alternative versions of extras missing from the page. Worth its own change; the fetch-cost bound is the open part.

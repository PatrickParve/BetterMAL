## Why

The More section groups extras by their relation to the main line, and "Alternative version" / "Alternative setting" are supposed to be groups of their own, ranked first. On Fate/Zero they are: Fate/Prototype and Sunny Day sit under "Alternative version", Prisma☆Illya, Apocrypha, Extra and strange Fake under "Alternative setting". On other franchises the same kind of entry lands under "Side story", "Prequel", or "Other" instead — Code Geass' three recap movies read as "Side story", Uma Musume's *Road to the Top (Movie)* as "Other", Gundam's *The Origin* as "Other".

There is nothing arbitrary about which series get it right. Two concrete mechanisms decide it, and Fate/Zero happens to clear both.

**1. A version relation between two members of the same series is never recorded, so the grouping rule cannot see it.**

`series-page` already requires the right answer here: *"Where the extra carries a relation, in either direction, to any main-line member of this series, its group SHALL be the highest-precedence such relation… Version relations rank first."* The builder cannot honour it, because `SeriesGraphBuilder` records only two kinds of edge as it traverses — story relations (`SeriesRelations.StoryTraversalSet`) and companion-media `other` edges — and `DiscoverVersionNeighboursAsync` records version edges **only when the far end is not a member**. A version relation whose two ends are both in the component falls through both: it is not traversable, and it is not a neighbour edge. It is never in the `edges` list `ResolveExtraGroups` reads, so the rule resolves the extra from its story edges alone.

*Code Geass: Hangyaku no Lelouch I - Koudou* (34438) declares exactly three relations: `alternative_version` → *Code Geass: Hangyaku no Lelouch* (**main line**), `sequel` → movie II, `side_story` → a picture drama. The one relation that names what it is — a retelling of the TV series — is the one the resolver never sees. It falls to inheritance and reads "Side story". Fate/Zero is unaffected because every one of its version entries is a *neighbour* — outside the story component — so its version edges travel the one path that does record them.

**2. When a version edge does survive, the resolver only accepts it if it lands on a main-line member.**

`ResolveExtraGroups` matches edges against `mainLineIds`. A version neighbour whose version relation names a non-main-line member matches nothing; it is then also excluded from the inheritance walk (correctly — a version neighbour must not pass its group on or take one), so it falls all the way through to `Other`. *Uma Musume: Pretty Derby - Road to the Top (Movie)* is an `alternative_version` of *Road to the Top*, which is itself a "Side story" extra: no main-line edge, no inheritance, "Other". Same for *[Oshi no Ko]*'s *Idol (Mori Calliope Cover)* (a version of the *Idol* music entry) and for five of Gundam's, including *The Origin*, whose version edge names the original *Kidou Senshi Gundam* — not main line in that series.

Measured against the running instance (246 stored series, 1,553 members): **16 members are grouped wrongly today** — 8 by mechanism 1, 8 by mechanism 2 — and a further **307 extras carry no relation group at all**, because they were built before the group was computed and their series has not been read since; the frontend renders a null group as "Other".

The third reason, which no code change can fix, is that MAL sometimes types a retelling `other` rather than `alternative_version`. Those are correctly reported as "Other": the app is reflecting its source.

## What Changes

**Version relations inside a series become visible to the grouping rule**

- `SeriesGraphBuilder`'s traversal records a member's own `alternative_version` / `alternative_setting` relations as edges, alongside the story and companion-media edges it already records — subject to the same AniList contradiction filter and the same "far end actually became a member" filter. They are recorded for grouping only; the traversal still refuses to *follow* them, so what a series contains is exactly what it contains today.
- With those edges present, the existing precedence rule does the rest: a version relation to a main-line member outranks a story relation to one, so Code Geass' movies, Attack on Titan's *Kakusei no Houkou*, *Gundam Evolve* and the two *Tetsuwan Atom* retellings move to "Alternative version" — the answer the spec already asks for.

**A version relation to a non-main-line member groups the extra as a version**

- New rule, between today's rule 1 and rule 2: an extra with no relation at all to the main line, but with a version relation to *some* member of the series, is grouped by that version relation. *Road to the Top (Movie)* reads "Alternative version" because it is one — of a side story rather than of the main line, which is a distinction the reader can see from the tiles and does not need a separate heading for.
- The tier matters. A relation to the main line still wins outright, so *Turn A Gundam* (a `sequel` into the main line, `alternative_setting` of a non-main-line member) stays "Sequel", and *Road to the Top* itself stays "Side story" while its movie moves.

**Inheritance still walks story edges only**

- The breadth-first inheritance walk keeps using story and companion edges, ignoring the newly recorded version edges, so `series-page`'s existing guarantee — *"a story extra is never labelled an alternative version merely for sitting next to one"* — holds by construction rather than by luck.

**Stored series regroup on their next read**

- `SeriesGraphBuilder.ClassificationRevisedAt` moves to this change's ship date, the mechanism already in place for exactly this. Every stored series is treated as needing a rebuild on next read, which both applies the new grouping and fills in the 307 null groups. No migration, no schema change, no backfill job.

**Deliberately not changed.** The related-entry projection still reaches only main-line members' relations, so an alternative version that MAL links to an *extra* and that has no cached row of its own is still absent from the page rather than merely mislabelled. It is a real gap and a different one — it changes what the page lists, not how it is labelled, and it needs its own decision about how far a read-time projection should reach. `alternative_version` and `alternative_setting` remain separate groups; they mean different things and MAL distinguishes them. Nothing infers a version relation MAL did not state.

## Capabilities

### New Capabilities

None. This corrects how an already-specified rule is implemented and adds one tier to it.

### Modified Capabilities

- `series-page`: the "Main line and extras" requirement's extras-grouping rules gain the non-main-line version tier as rule 2 (today's rules 2 and 3 shift down), and state explicitly that a version relation between two members of the same series counts for grouping even though it is never traversed, and that the inheritance walk does not travel version edges. New scenarios cover the recap-movie case, the version-of-an-extra case, and the tier ordering that keeps a sequel-to-main-line extra out of the version groups.

## Impact

**Backend** (`backend/AnimeTracker.Api/`)

- `Services/Series/SeriesGraphBuilder.cs` — record version-relation edges in `TraverseStoryComponentAsync`; add the non-main-line version tier to `ResolveExtraGroups` and confine its inheritance walk to non-version edges; bump `ClassificationRevisedAt`.
- `Services/Series/SeriesRelations.cs` — a helper naming the tier-2 rule beside `HighestPrecedenceGroup`, so the grouping precedence stays stated in one file.
- `AnimeTracker.Api.Tests/Services/Series/SeriesGraphBuilderExtraGroupResolutionTests.cs` — the tier cases; `SeriesGraphBuilderTraverseAsyncTests.cs` — version edges recorded but not followed.

**Frontend** — none. `RELATION_GROUP_LABELS` already carries every group, and the More section already renders whatever groups the server sends.

No schema change, no migration, no API shape change, and no change to which anime a series contains.

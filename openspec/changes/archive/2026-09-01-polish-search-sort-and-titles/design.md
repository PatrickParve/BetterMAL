## Context

Five independent polish items across four surfaces. Three are one-file presentation fixes; two need a figure the system does not currently compute.

Current state worth naming:

- **All search matching already funnels through one place.** `Services/Search/SearchTextMatch.cs` holds `EqualsIgnoreCase`/`ContainsIgnoreCase`/`StartsWithIgnoreCase`, and every match in the app calls them: `AnimeSearchService`'s type-ahead ranking, its exact-query filter, its local fallback bands, and `SeriesSearchIndex.MatchQualityOf` (which matches member titles, member English titles, and the series' chosen title). Widening what counts as a match is therefore a change to three methods, not to four call sites' worth of logic.
- **The live MAL search is not ours to change.** `SearchAsync` merges a live MAL result set with the local index; `SearchPageAsync` uses MAL's own relevance order. Normalizing our own predicates widens which *returned* candidates survive our filter; it cannot make MAL return a candidate it did not return.
- **`SeriesListItemDto` has neither figure the tie-break needs.** It carries `MineMain` (the average being tied on) and `MainLineAiredEpisodes` (the third tie-break, already present), but no ranking positions and no main-line entry count. `TopSeriesItemDto` already carries `MainLineAiredCount` — computed in `SeriesRankingIndex.EligibleSeries` as main-line members whose `AiringStatus != "not_yet_aired"` — so the multi-entry figure exists in the index and simply is not projected onto the list item.
- **My rankings are derived, never stored.** `AnimeRankingSnapshot.Build` orders every ranked entry (bands 0–2: scored, not plan-to-watch, has aired) and hands back `RankOf(animeId)`. It is pure over "all entries + the stored hand-order", both of which `AnimeRankingService.GetSnapshotAsync` loads.
- **The Series page's filters live in the URL** (`?sort=&progress=&status=`), read on render and toggled through `setSearchParams`. The profile's Top series filter, by contrast, lives in `useRestorableState` — the two pages hold their controls differently and each is consistent with itself.
- **The detail page's `<h1>` sets no wrapping rules at all**, so it takes the browser's default set of soft-wrap opportunities, which includes after a hyphen, after a slash, and around a dash.

## Goals / Non-Goals

**Goals:**

- A query that is right about the words finds the title, whatever the user did with case, spaces, punctuation, or accents.
- One normalization rule, written once, used by anime and series matching alike.
- The Series page's default sort is total and meaningful, not "then alphabetical" for every group of tied averages.
- The Series page can be narrowed to multi-entry franchises by the same rule the profile page uses, so the two controls can never disagree about what "multi-entry" means.
- A detail-page title never breaks a word.

**Non-Goals:**

- No fuzzy matching, no edit distance, no transliteration, no synonym or abbreviation handling ("fma" will still not find *Fullmetal Alchemist*). Normalization only removes characters that carry no meaning for a title match.
- No change to what MyAnimeList is asked for, or to the merge/rank/row-budget rules once candidates are in hand.
- No change to how a rank is computed, to what the ranking covers, or to any other page that reads it.
- No new sort option, and no tie-break change to the MAL average, Alphabetical, Status, Newest, Oldest, or My progress sorts.
- No change to the profile page's Top series control.
- No wrapping change to any title other than the anime detail page's own `<h1>`.

## Decisions

### D1. Normalization lives in `SearchTextMatch`, applied to both sides of every comparison

`SearchTextMatch` gains:

```
public static string Normalize(string value)
    // NFD-decompose, drop non-spacing marks, keep letters and digits only, lower-case invariant
```

and its three predicates become `EqualsNormalized` / `ContainsNormalized` / `StartsWithNormalized`, each normalizing both the candidate title and the term before comparing with `StringComparison.Ordinal`. The rename is deliberate: after this change the methods are no longer "ignore case" and a name that says so would mislead the next reader.

`char.IsLetterOrDigit` keeps kana, kanji, and Cyrillic intact while dropping `:`, `-`, `–`, `/`, `.`, `!`, `?`, `'`, `"`, `~`, `★`, and whitespace. So a Japanese title matches exactly as it does today, and an English one loses only the characters that were never the point of the query.

*Alternatives:* a `CompareOptions.IgnoreSymbols | IgnoreNonSpace` culture-aware comparison (does not ignore whitespace, which is the actual complaint, and drags culture-sensitivity into a comparison that must be stable); normalizing only the query (asymmetric — "fullmetal" would then still not match a stored "Full Metal Panic"); doing it per call site (four places, guaranteed to drift).

**Cost.** Both search entry points already issue a database round trip per keystroke to load the whole title index, and `SeriesSearchLookup` loads every series member. Normalizing a few thousand short strings per request is small beside that, and is not worth pre-computing a normalized column for until measurement says otherwise. The term is normalized once per request, not once per candidate.

### D2. A query that normalizes to nothing matches nothing

`"!!!"`, `"—"`, or `"  ?  "` survive `ParseQuery` (they are non-empty after trimming) but normalize to the empty string — against which `Contains` and `StartsWith` are true of every title. Both entry points therefore return an empty result set when the normalized term is empty, exactly as they already do for an empty raw term. Matching everything for a query of punctuation would be worse than matching nothing.

### D3. The exact-match operator normalizes too

A double-quoted query keeps meaning "the whole title, not a substring of it" — it is what separates *Naruto* from *Naruto: Shippuden* — but it stops meaning "these exact bytes". `"fullmetal alchemist"` matches *Fullmetal Alchemist*. Leaving the quoted path byte-exact would make it the one place in the app where the punctuation of a title must be guessed correctly, which is precisely the complaint this change answers.

### D4. The MAL request keeps the raw term

`SearchMalAsync` still sends what the user typed. MAL's own search is already tolerant, and sending it a de-spaced term ("fullmetal" for "full metal") would be a guess about a matcher we do not control. The consequence is stated plainly: an anime that is not in our local cache and that MAL itself does not return for the query is still not found. Normalization strictly widens what survives our own filtering of whatever MAL does return.

### D5. The Settings picker uses `pickDisplayTitle`, and only for display

`AnimeRefreshPicker` renders `result.title` in three places — the dropdown row, the value it writes into the input on pick, and the two result messages. All become `pickDisplayTitle(result.title, result.englishTitle)`; `AnimeSearchResult` already carries `englishTitle` (the navbar's `SearchBar` uses it on the same rows). `refreshAnime(selected.id)` is untouched — the id is what identifies the anime, and the title was never more than a label.

### D6. Space-only wrapping is a component, not a CSS property

There is no CSS property that says "break at spaces only". `word-break: keep-all` is defined for CJK and leaves non-CJK text at `normal`; `hyphens: none` governs *automatic* hyphenation, not the break opportunity a literal `-` character creates; `white-space: nowrap` on the `<h1>` would stop the title wrapping at all.

So: a small `SpaceWrappedTitle` component splits the text on runs of whitespace and renders each token inside `<span class="space-wrapped-title__word">` (which is `white-space: nowrap`), joined by real spaces. Break opportunities then exist only between tokens.

*Alternatives:* inserting U+2060 word joiners between the characters of each token (works, but puts invisible characters into text the user can select and copy); replacing `-` with U+2011 and `/` with a glued variant (per-character allow-list, fails on the next punctuation mark someone hits).

**The long-token guard.** A token wider than the line has nowhere to break once it is `nowrap`, and would run outside the page. Tokens longer than 40 characters are therefore rendered as plain text, keeping the browser's default breaking for them. No real anime title has a 40-character unbroken token, so this never fires in practice — it exists so that the pathological case degrades to today's behaviour instead of to a broken layout.

The component is written to be reusable, but this change wires it to exactly one place: `AnimeDetailPage`'s `<h1>`.

### D7. Both tie-break figures are computed server-side, on the list item

The client sorts the series list itself, so both figures have to arrive with the data:

- `MainLineAverageRank: double?` — the mean of `AnimeRankingSnapshot.RankOf` over the series' main-line members that have a rank at all; `null` when none does.
- `MainLineAiredCount: int` — main-line members whose `AiringStatus != "not_yet_aired"`, the same expression `EligibleSeries` already uses for the profile's filter.

`SeriesRankingIndex.ListedSeries` takes the ranking snapshot as a second parameter, exactly as it already takes `airedEpisodesByAnimeId`, and `SeriesListService` gains `IAnimeRankingService` alongside its lookup and schedule service. That is one extra pair of queries (all user entries, plus the stored hand-order) per Series-page read; it builds nothing and calls no MAL endpoint, so the endpoint's "cost is bounded by what is stored" property is preserved.

*Alternatives:* computing the rank average on the client (it has no ranking data on that page and would need a second full read); storing a rank on the series member (ranks are derived on purpose — `AnimeRankingSnapshot` exists so a score edit changes the ranking with no rebuild, and a stored copy would be wrong the moment a score changed).

### D8. The rank average covers the whole main line, unscoped

`ListedSeries` scopes its *episode* figures to the default combination of alternatives (a card carries no version picker) but computes both score averages over the whole, unfiltered main line. The rank average breaks a tie in `MineMain`, so it must describe the same member set that average describes: the whole main line.

Members with no rank are excluded from the mean **entirely** — they contribute no rank *and* do not count toward the divisor — rather than being counted as a worst-case rank or as a zero. A franchise with one ranked entry and three unranked ones is described by the rank it actually has, not pushed to the bottom for the entries it lacks. In practice the unranked member is almost always one I have not scored, so the rule reads: an unscored entry can neither raise nor lower its series' average position.

Concretely, `mainLineAverageRank` is `mainLine.Select(m => ranking.RankOf(m.AnimeId)).Where(r => r is not null).Average()` over a non-empty set, `null` otherwise — never `Sum() / mainLine.Count`.

This does mean the rank average and the score average can cover slightly different member sets: the ranking's coverage is bands 0–2 (scored, not plan-to-watch, has aired), so an entry scored but plan-to-watch, or scored but not yet aired, counts toward the my-average while holding no rank. That is the ranking's own coverage rule and this figure defers to it rather than inventing a second one.

### D9. The tie-break chain applies to the My average sort alone

In `sortSeries`, `myScore` stops being a bare `seriesNullsLast` and becomes a chain:

1. main-line my-average, descending, nulls last (unchanged);
2. main-line average rank, **ascending** — nearer the top of my rankings first — nulls last;
3. main-line episodes aired, descending;
4. display title, ascending (the existing final tie-break every sort already gets in `sortSeries`).

Two series that both have *no* my-average are equal under (1) and so take the same chain — the rule is "the averages compare equal", not "the averages exist and are equal". Every other sort keeps exactly the tie-break it has today.

### D10. The multi-entry filter is a URL filter, in its own labelled group

The Series page keeps every filter in the URL and its spec requires it ("The selected filters SHALL be kept in the URL"), so this one joins them as `?multi=1` rather than following the profile page's `useRestorableState`. Back-navigation restores it either way; the URL also makes it shareable, consistently with the two filters beside it.

It renders as a single toggle button inside a third filter group labelled `Entries`, matching the existing `Status` and `Progress` groups' label-plus-buttons shape and taking the same `--active` treatment and `aria-pressed` wiring. `filterSeries` gains a `multiOnly` parameter and ANDs it with the other two groups, so it narrows the whole loaded list before sorting, like everything else in that cluster.

The rule itself is `mainLineAiredCount > 1` — the identical expression the profile page's `filterMultiEntry` uses on `TopSeriesItemDto.mainLineAiredCount`. The two controls therefore share a definition rather than a re-derivation: extras never count, and an announced-but-unaired sequel does not make a series multi-entry.

## Risks / Trade-offs

- **Normalized matching widens what a short query matches** (removing spaces means "onep" now matches "One Piece") → The ranking is unchanged: prefix matches still lead, popularity still orders each band, and the dropdown still shows five rows. A wider candidate set reorders nothing that was already matching.
- **A punctuation-only query would match everything** → D2's explicit empty-normalized-term guard, covered by a test on both entry points.
- **The quoted-exact operator gets looser** → Deliberate (D3), and it still constrains to the whole title rather than a substring, which is what the operator is for.
- **The series-list read gains a query pair** → It is two indexed reads on a page that already loads every series member and is only opened by hand. If it ever matters, the snapshot is cacheable per request; nothing here forecloses that.
- **A tie-break on ranks is invisible in the UI** — the card shows no rank, so two adjacent tied cards give the reader nothing to check the order against → Accepted: it is a stable, explainable order replacing an arbitrary one, and the spec states the rule.
- **A single token longer than the line would overflow once it cannot break** → D6's 40-character guard leaves such a token on the browser's default behaviour.
- **`SeriesListService`'s constructor changes** → Every construction site is in this repo (one DI registration, one test file group); the compiler finds them all.

## Migration Plan

None required. No stored data changes shape, so there is no migration and no backfill: `MainLineAverageRank` and `MainLineAiredCount` are computed at read time from members and entries that already exist. The series-list response gains two fields and removes none, so an older client would keep working against a newer server. Rollback is a revert.

## Open Questions

None.

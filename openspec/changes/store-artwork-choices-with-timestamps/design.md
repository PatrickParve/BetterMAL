## Context

Three presentation choices exist in the app: an anime's picture, a series' title, and a series' picture. Two of them are already stored as choices — `Series.SelectedTitle` and `Series.SelectedPictureUrl`, null by default, cleared by writing null. The third is not stored at all. `AnimeMetadata` holds a displayed picture and MAL's own main picture, and `Services/Artwork/AnimePicture.cs:10` infers the choice from them:

```csharp
public static bool IsOverridden(AnimeMetadata anime) => anime.PictureUrl != anime.MalPictureUrl;
```

That inference is load-bearing in three places, all of which the current comments defend as a single co-ordinated mechanism:

- `MalMappingExtensions.ApplyTo` (`:100-102`) and `ApplyLeanTo` (`:186-188`) each read `wasOverridden` **before** writing `MalPictureUrl`, then write `PictureUrl` only when it was false. `ApplyTo`'s comment: *"The single guard for the whole app… A second writer of `PictureUrl` outside this guard would defeat it."*
- `ArtworkSelectionService.ResetAnimePictureAsync` clears by writing `PictureUrl = MalPictureUrl`, and `SetAnimePictureAsync` therefore treats picking MAL's own picture as the clear.
- `AnimePicture.Options` keeps `MalPictureUrl`/`PictureUrl` as the "identity-authoritative literals" so the picker's "Current" badge and `IsOverridden` can both match by exact string.

Constraints this design works under:

- **The inference is only valid within one database.** Both columns were written there by the same machine in the same moment. Elsewhere, MAL's main picture may have been cached at another time, so the same pair of values does not mean the same thing.
- **Nothing carries a time.** No ordering exists between two devices' versions of one choice, which is the first thing a comparison needs.
- **The displayed value is read from SQL, not just from objects.** `PictureUrl` appears in EF projections, repository queries and DTO mappings across ~40 sites (`AnimeMetadataRepository`, `SeasonRepository`, the dashboard, recap, profile, series, search and ranking services).
- **There is no clock abstraction.** The codebase takes `DateTimeOffset.UtcNow` directly at the point of the write, or accepts a `now` parameter threaded from a caller; there is no `IClock`, no injected `TimeProvider`, and 60+ direct call sites.
- **Backend is .NET 10 / EF Core 10 / Npgsql 10 on PostgreSQL**, tests on EF InMemory. The local SDK is 9.0, so builds and `dotnet ef` go through the `sdk:10.0` Docker image.
- **Live data has to survive**: 190 anime carry a chosen picture today (all in my list, all with a stored picture set, every chosen URL still in its own set, none differing from MAL's by file extension alone); 96 series carry a chosen picture and 22 a chosen title.

## Goals / Non-Goals

**Goals:**

- Chosen-ness is stored, not inferred: one column per choice, null meaning "no choice, follow the default".
- Each of the three choices carries the time it was last set or cleared, stamped at the moment of the change and never restamped afterwards.
- The displayed picture keeps its name, its meaning and its value, so no read site changes.
- Picking MAL's own picture pins it as a choice; clearing is an explicit act with a control of its own in the picker.
- The 311 existing choices survive with no re-picking, and every one of them carries a time.

**Non-Goals:**

- Any export, import, or cross-device comparison. This makes the data comparable; it does not compare it, and it does not decide which side wins.
- Exposing a timestamp through any endpoint or rendering one in the UI.
- A clear control in the series *title* picker.
- Timestamps on anything else that could carry one (list entries, rankings, top-anime selections).
- Introducing a clock abstraction.

## Decisions

### D1. `PictureUrl` stays, as a derived column, in the `TotalEpisodes` shape

`AnimeMetadata` gains `SelectedPictureUrl` (nullable). `PictureUrl` remains a stored column holding the effective value, re-derived by a single method beside its sibling:

```csharp
public void ResolvePictureUrl() => PictureUrl = SelectedPictureUrl ?? MalPictureUrl;
```

Every writer of either source column calls it in the same write, exactly as `ResolveTotalEpisodes()` is already used and as `Models/AnimeMetadata.cs` already documents for the episode-count trio. The model comment for `PictureUrl` is rewritten from "the two differ exactly when a picture has been chosen" to what it now is: the effective value, derived, with `ResolvePictureUrl()` as its only writer.

*Alternative rejected:* remove `PictureUrl` and compute the display value at read time. It is the purer model, and it is the one the proposal's phrase "derived from the chosen value falling back to MAL's" could also describe — but the derivation would then have to appear in every EF projection that currently selects `a.PictureUrl`, including ones translated to SQL and ones that project into DTOs inside `Select`. That is ~40 edits with no behavioural gain, and it loses the ability to read or order on the displayed value in SQL. The user's own framing — "the same shape `TotalEpisodes` already uses" — is this one: a stored effective column with a resolver.

*Consequence:* three picture-ish columns become four, and the invariant "`PictureUrl == SelectedPictureUrl ?? MalPictureUrl`" is the thing the resolver exists to keep true. It is checkable in SQL at any time, which the old two-column inference never was.

### D2. Names mirror the series columns

- `AnimeMetadata.SelectedPictureUrl` — the same name the series row already uses for the same idea, so "the chosen one, null means follow the default" reads identically on both entities.
- `AnimeMetadata.SelectedPictureModifiedAt`, `Series.SelectedPictureModifiedAt`, `Series.SelectedTitleModifiedAt` — each named for the choice it stamps.

*Alternative rejected:* renaming the series columns to `Chosen*` for a fresher vocabulary. It would churn `SeriesDto`, `SeriesGraphBuilder`, `SeriesIdentity`, `SeriesService`, the frontend types and 3 test files to rename fields whose meaning is not changing.

### D3. A timestamp means "when this choice last changed", and null means "never"

Each timestamp is `DateTimeOffset?` with these rules:

- Set the choice → stamp it. Clear the choice → stamp it too. Clearing is a change, and "cleared at 10:05" must be able to outrank "chosen at 10:04" when two devices are compared.
- Setting the same value again re-stamps. It is a write; there is no interesting distinction between "chose X" and "chose X again", and detecting no-ops would only make the stamp lie about when the row was last touched.
- Null means no choice has ever been made or cleared on this row. After the backfill (D8) that is the only thing it can mean.
- A non-null timestamp beside a null choice is normal and meaningful: it is a cleared choice.

*Consequence:* the pair (chosen value, timestamp) is what a future comparison reads, and it can represent all three states a device can be in about one choice — never touched, chosen at T, cleared at T.

### D4. The stamp is taken with `DateTimeOffset.UtcNow` in `ArtworkSelectionService`, and nowhere else

The four setters and two resetters (six methods) are the only writers of the three timestamps. Each takes `DateTimeOffset.UtcNow` at the point of the write, following the convention every other service in the codebase uses.

No MAL sync path, refresh path, DTO projection, export, or rebuild writes a timestamp. That is what makes "stamped when I make the change, never when it is later exported" true by construction rather than by discipline: the only code that can stamp is the code that answers a request from me.

*Alternative rejected:* injecting `TimeProvider` for testability. Nothing else in the codebase takes one, so it would be a lone exception, and the tests can assert what actually matters — that a stamp lands inside the window the test spans, that it is null before the first write, and that a clear stamps a later time than the set it follows.

### D5. A rebuild carries a choice and its timestamp as one indivisible pair

`SeriesGraphBuilder` moves choices in two places, and both take the timestamp along unchanged:

- **Absorb** (`:1116-1122`): the survivor adopts an absorbed series' chosen title or picture where it has none. It adopts that choice's timestamp in the same statement — never `UtcNow`. Adopting an existing choice is not making one, so the choice keeps the time it was actually made.
- **Re-root** (`:1220-1236`): the row is deleted and re-inserted at the new id, carrying `SelectedTitle`/`SelectedPictureUrl` across. The two timestamps ride along with them.

The pairing is the rule: a choice is never moved without its timestamp, and a timestamp is never moved without its choice. `??=` on the value alone would silently leave the survivor's null timestamp beside an adopted choice.

### D6. Picking MAL's own picture stores it — clearing is only ever explicit

`SetAnimePictureAsync` loses its special case entirely: whatever URL is picked, from the option set, is written to `SelectedPictureUrl` and stamped. Picking the URL MAL currently calls its main picture therefore **pins** that picture: a later MAL change of main picture will not move that anime's displayed picture, because the choice, not the fallback, is in force.

`ResetAnimePictureAsync` writes null and stamps. It is now the only way to clear, which is why the picker needs the control in D7.

*Why the user asked for this:* under the old rule, a device receiving "chosen = MAL's picture as I saw it" cannot distinguish it from "no choice", and a genuine choice would be read as a clear on the far side. A pin is a fact about intent; the old rule threw it away.

*Consequence, accepted:* an anime with a pinned picture stops following MAL. That is the definition of a choice, and the picker's clear control is the way back.

### D7. The picker's clear control is rendered from the stored choice, and labelled per surface

`PicturePickerOverlay` gains two optional props — a clear handler and its label — and renders the button only when the handler is supplied. The pages supply it only when a choice is actually stored, so the button's presence *is* the indicator that one is:

- `AnimeDetailPage` — "Use MAL's picture", shown when `detail.selectedPictureUrl != null`, calling the existing `resetAnimePicture`.
- `SeriesPage` — "Use the root anime's MAL picture", shown when `series.selectedPictureUrl != null`, calling the existing `resetSeriesPicture`. A series' default is its root member's *MAL* picture (`SeriesIdentity.Resolve`'s fallback, `root.MalPictureUrl`) — deliberately bypassing any pin the root anime itself carries, so the label names what clearing actually returns to rather than what the root anime happens to display elsewhere.

`AnimeDetailDto` gains `selectedPictureUrl`; `SeriesDto` already carries it. The four set/reset endpoints return `{ pictureUrl, selectedPictureUrl }` so a page can update both halves of its state from one response without a refetch.

*Revision, on instruction:* an earlier version of this design had the series default follow the root member's *displayed* picture — i.e. the root anime's own pin, when it has one. That is reversed: the series default is the root anime's **MAL** picture specifically (`root.MalPictureUrl`), bypassing the root's own choice entirely. A pin is a fact about one anime; a series silently inheriting it as its own "no choice" default would make that anime's individual pin double as an unintended series-wide one. `SeriesIdentity.Resolve` therefore takes `rootMalPictureUrl`, not `rootPictureUrl`, and every caller (`SeriesService.ProjectAsync`, `SeriesRankingIndex`'s three call sites, `SeriesSearchIndex`) sources it from `MalPictureUrl`, which means the two read-side projections that feed those callers (`SeriesRankingMemberProjection`, `SeriesMemberProjection`) need `MalPictureUrl` added alongside the `PictureUrl` they already carry.

The trigger is **whether a choice is stored**, not whether the chosen picture differs from MAL's main picture. Those two come apart in exactly the case D6 exists for: a pinned MAL main picture is a stored choice whose URL equals `MalPictureUrl`, and a differs-from-MAL trigger would leave it with no way to be cleared.

*Consequence, accepted:* the existing rule "the control that opens the picker is rendered only when there is more than one option" stays as it is, and it gates the clear control along with the rest of the picker. A single-option anime cannot acquire a choice through the interface in the first place — pinning requires the picker, which requires two options — so the reachable case is narrower than it looks: pin a picture while there are two or more, then have MAL drop pictures until only the pinned one is left. The option set then holds one URL and the picker button goes away with a choice still stored. It reopens as soon as MAL adds or changes a picture again. The alternative — rendering the picker button whenever a choice is stored, even for a single option — closes the gap for the cost of a picker that can open onto one image; it is one condition in `AnimeDetailPage.tsx:372` and its series equivalent if the case is ever hit.

### D8. One migration: three columns, a 190-row copy, and one stamp for every existing choice

Scaffolded for the column additions, then hand-written SQL for the data, in this order:

1. Add `AnimeMetadata."SelectedPictureUrl"` (text, null), `AnimeMetadata."SelectedPictureModifiedAt"`, `Series."SelectedTitleModifiedAt"`, `Series."SelectedPictureModifiedAt"` (all `timestamptz`, null).
2. `UPDATE "AnimeMetadata" SET "SelectedPictureUrl" = "PictureUrl", "SelectedPictureModifiedAt" = now() WHERE "PictureUrl" IS DISTINCT FROM "MalPictureUrl";` — `IS DISTINCT FROM` is the exact SQL reading of the C# inequality being retired, and it leaves the two rows where both columns are null untouched, as `!=` did. Expected: 190 rows.
3. `UPDATE "Series" SET "SelectedTitleModifiedAt" = now() WHERE "SelectedTitle" IS NOT NULL;` (22 rows) and the same for `"SelectedPictureUrl"`/`"SelectedPictureModifiedAt"` (96 rows).

Step 2 re-derives exactly the `PictureUrl` each of those rows already holds, so no displayed picture changes and no `PictureUrl` write is needed.

The stamp is `now()` — the migration's transaction time — for every choice that exists, on the user's instruction. It makes every pre-existing choice older than every choice made afterwards on that device, and it keeps null unambiguous (D3). It is honest about what it records: the moment the app learned to record times, not the moment the choice was made, which nothing knows.

*Down:* drop the four columns. `PictureUrl` still holds the displayed value and `MalPictureUrl` still holds MAL's, so the old inference reads exactly as it does today — the rollback is lossless except for the timestamps themselves.

### D9. The guards become resolvers, and the hazard they guarded against goes away

Both mapping paths lose their read-before-write dance:

```csharp
target.MalPictureUrl = node.MainPicture?.Large ?? node.MainPicture?.Medium;
target.ResolvePictureUrl();
```

There is no ordering requirement left, no "single guard for the whole app", and no way for a second writer of `MalPictureUrl` elsewhere to defeat anything — the worst a future writer can do is forget to re-derive, which is the same, well-established obligation `MalTotalEpisodes` already carries and which the model comment states. `ApplyPictureSetTo` is untouched: the picture *set* has never had anything to do with chosen-ness.

`AnimePicture.IsOverridden` becomes `anime.SelectedPictureUrl is not null`. `AnimePicture.Options` keeps its identity-authoritative upserts but takes the second one from `SelectedPictureUrl` rather than `PictureUrl` — set-equivalent, since the displayed value is always one of the two it already upserts, and it now says what it means. The frontend's `animePictureOptions` mirrors that, taking `[malPictureUrl, selectedPictureUrl]`.

### D10. Validation and the never-re-validate rule are unchanged

`SetAnimePictureAsync` keeps rejecting a URL outside `AnimePicture.Options(anime)` and keeps rejecting an anime not in my list. `SetSeriesPictureAsync` keeps adding the current choice to the pool before validating, so a choice MAL has since dropped stays pickable. A stored choice is still never re-validated away by any read or refresh — now more plainly than before, because nothing reads the choice's *validity* to decide whether it exists.

## Risks / Trade-offs

- **A future writer of `MalPictureUrl` forgets `ResolvePictureUrl()`** → The obligation is the one `ResolveTotalEpisodes()` already establishes, stated in the model comment; the two mapping paths are the only writers today, and both are covered by mapping tests asserting that a full and a lean upsert each leave the displayed picture correct with and without a choice.
- **Pinning changes behaviour for pictures picked from now on** → Intended, and asked for. The 190 backfilled choices were already genuine overrides and behave as they always did; only a *newly* picked MAL main picture behaves differently from before.
- **Backfilled timestamps do not describe when the choice was made, and differ per device** → Accepted on instruction. Every backfilled choice is at least correctly ordered against everything chosen after it on the same device, and null stays unambiguous. A future comparison can still see that two devices' backfill stamps are near their own migration times if it ever needs to.
- **The migration's SQL is not exercised by the test suite** (EF InMemory never runs it) → Verified directly against the live database after apply: 190 rows with a non-null chosen picture, zero rows where `PictureUrl` differs from `SelectedPictureUrl ?? MalPictureUrl`, and 96/22 stamped series.
- **A page's local state after a set or clear** → The endpoints return both the displayed and the chosen value, so both halves update from the one response; nothing is inferred client-side from comparing URLs.
- **Test fixtures that seed an override by making the two columns differ** (5 files) → Each must seed `SelectedPictureUrl` instead; a fixture that sets only `PictureUrl` now describes an anime following MAL with a stale displayed value, which is a state the resolver will correct on the next mapping call. The fix is mechanical and the assertions become stronger, since they can assert on the choice itself.

## Migration Plan

1. Model and DbContext changes, then scaffold the migration in the `sdk:10.0` container and hand-write the three data statements into its `Up`.
2. Backend code: resolver, mapping paths, `AnimePicture`, `ArtworkSelectionService`, `SeriesGraphBuilder`, DTO and controller responses.
3. Backend tests updated and extended, full suite green in the container.
4. Frontend: types, client, picker component, both pages.
5. Apply against the live database; verify the three counts and the invariant with SQL; open an anime with a chosen picture, clear it, re-pick it, and confirm the series picker's clear path on a series with a chosen picture.

Rollback is the migration's `Down` (D8): the four columns drop and the old inference reads unchanged.

## Open Questions

None blocking. Deliberately left open for a later change: which side wins when two devices hold different times for the same choice, and what happens to a choice whose URL the other device has never seen.

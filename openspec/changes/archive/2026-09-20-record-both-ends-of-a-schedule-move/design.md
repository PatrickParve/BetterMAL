## Context

`AnimeUpdate` holds `PreviousStartDate`, `PreviousBroadcastDayOfWeek` and `PreviousBroadcastTime` for the two schedule kinds, and `PreviousEpisodeDate` **and** `NewEpisodeDate` for the third. `AnimeUpdateService.ToDto` fills the missing halves from the anime itself: `anime.AiredFrom` at `AnimeUpdateService.cs:180`, and the live JST slot converted at `AnimeUpdateService.cs:160-166`. `updateText.ts:33-35` then compares the live value against the recorded one to choose its verb.

The class comment on `AnimeUpdate` already states the intended rule — "Only the schedule-change kinds store anything, because for those the news *is* the movement and the anime's current record holds only where it landed" — so this is a rule the model describes and two of its three kinds do not implement.

Live state, verified against the running database before writing this: 6 `StartDateChanged` rows across 6 distinct anime, 3 `EpisodesMoved`, 0 `BroadcastSlotChanged`. No anime holds more than one schedule-change row of any kind, so no card is visibly wrong yet and every existing row's destination is still recoverable from its anime.

`AnimeMetadataChangeDetector.RecordFieldUpdates` is the only writer of `StartDateChanged` and `BroadcastSlotChanged` (`AnimeMetadataChangeDetector.cs:238,247`); `EpisodeScheduleRefreshService` writes only `EpisodeCountReleased` and `EpisodesMoved`, and `AnnouncementResolutionService` only `Announced`. One call site has to learn anything new.

This change lands **after** `polish-updates-panel` and inherits two things from it: the `Moved earlier` / `Delayed` wording, and the equal-dates guard, which stays permanently because rows predating the backfill still fall back to the live value.

## Goals / Non-Goals

**Goals:**

- Both ends of every schedule move are stored on the update that reports them.
- A movement line reads only recorded values; a fact line reads only live ones. One sentence, no exceptions beyond the named legacy fallback.
- Existing rows keep their true destination, captured while it is still provably their destination.
- The scenario `anime-updates` already specifies — "A later correction does not rewrite an earlier update" — becomes testable.

**Non-Goals:**

- **Re-detecting history.** Nothing goes back to MyAnimeList to reconstruct what a row's dates were. The backfill reads only what the app already holds.
- **Changing what is recorded, or when.** No detector predicate, relevance gate, airing-status gate or dedupe rule moves.
- **Touching the fact lines.** `Total episodes:` and `Premiere:` keep reading the anime's current record; that behaviour is correct and is the reason the live-read rule exists.
- **Episode moves.** They already store both ends; only their spec wording is folded into the widened requirement.
- **A general "snapshot the anime at detection time" mechanism.** See D1.

## Decisions

### D1 — Three columns mirroring the ones already there, not a snapshot

`AnimeUpdate` gains `NewStartDate` (`DateOnly?`), `NewBroadcastDayOfWeek` (`string?`, JST, e.g. `"mondays"`) and `NewBroadcastTime` (`TimeOnly?`, JST) — named, typed and stored exactly like the `Previous*` fields beside them, and read through the same `ParseMalDayOfWeek` + `ConvertBroadcastSlot` path so both ends of a slot move are converted identically.

*Alternative rejected:* storing a serialised snapshot of the anime's schedule fields at detection time. It would cover any field added later without a migration, but it makes every read parse a blob, puts values outside the type system where no query can reach them, and answers a question nobody is asking — the three kinds are a closed set defined by `AnimeUpdateKinds`, and two of them are already half-recorded in plain columns.

*Why JST for the slot:* the column stores what the anime stores, and conversion to local time happens once on the way out. Storing local time would freeze a conversion done under one offset into a value read under another.

### D2 — `ScheduleMoveDetails` carries both ends; the detector fills them

`ScheduleMoveDetails` gains `NewStartDate`, `NewBroadcastDayOfWeek` and `NewBroadcastTime`, and its summary stops calling itself "the moved-from values" — it already carries `NewEpisodeDate`, so the name was already half-wrong.

`RecordFieldUpdates` sets them in the two branches that already set the `Previous*` values, from the `anime` entity, which at that point holds the values just written — the same object the `before` snapshot is being compared against. No new read, no new query, and both ends provably come from the same comparison that decided there was a move at all.

`AnimeUpdateRecorder` writes them under the same kind-bit guards its existing lines use (`AnimeUpdateRecorder.cs:50-52`), so a value can never be stored for a kind the row does not carry.

### D3 — The fallback lives in `ToDto`, not in the client

`ToDto` reports `update.NewStartDate ?? anime.AiredFrom`, and the slot equivalently. The DTO field therefore means "the value it moved to, as recorded — or the anime's current value where the row predates that being stored", documented in those terms on `AnimeUpdateDto`.

*Alternative rejected:* sending the recorded value or null and letting `updateText.ts` fall back to `airedFrom`. The server is the only side that knows whether a row recorded anything; putting the rule on the client makes the wire format ambiguous (`null` meaning both "no move recorded" and "unknown") for no gain, since the client would apply it identically for both surfaces anyway.

The direction verb then follows the pair the DTO reports, which is what makes it stable: after this change, a card's verb cannot change because the anime moved again.

### D4 — A backfill correct by construction, not by today's snapshot

The migration's data statement stamps `NewStartDate` from the anime's current premiere date, but only on a row that is **the only** `StartDateChanged` row for its anime:

```sql
UPDATE "AnimeUpdates" u
SET "NewStartDate" = a."AiredFrom"
FROM "AnimeMetadata" a
WHERE a."Id" = u."AnimeId"
  AND (u."Kinds" & 8) <> 0
  AND u."NewStartDate" IS NULL
  AND a."AiredFrom" IS NOT NULL
  AND a."AiredFrom" <> u."PreviousStartDate"
  AND NOT EXISTS (
      SELECT 1 FROM "AnimeUpdates" o
      WHERE o."AnimeId" = u."AnimeId" AND (o."Kinds" & 8) <> 0 AND o."Id" <> u."Id"
  );
```

The `NOT EXISTS` clause is what makes it sound rather than lucky: where an anime has exactly one recorded premiere move, the anime's current date *is* that move's destination, by definition of there having been no later recorded move. Where a second row exists, the chain is ambiguous and the statement leaves both rows alone to fall back as before. So the statement stays correct however the table has grown between now and the deploy, which a bare "stamp every row" would not.

`a."AiredFrom" <> u."PreviousStartDate"` skips a row whose anime has since returned to the date it moved from: stamping there would record a move to where it came from, which is not what the row recorded. Those rows keep the fallback, and `polish-updates-panel`'s equal-dates guard renders them without a direction.

Today that statement matches all 6 rows. `BroadcastSlotChanged` has no rows to backfill, and no equivalent statement is written for it — an empty backfill is better left unwritten than written and unverifiable.

*Down migration:* drops the three columns. The recorded destinations go with them, which is inherent to reverting a change whose point is to record them; rows return to the live-value behaviour they have now.

### D5 — Migration tooling

The globally installed `dotnet-ef` is **9.0.9** while the project runs EF Core **10.0.9**, and the CLI refuses to scaffold against a newer runtime. Either update the tool (`dotnet tool update --global dotnet-ef --version 10.*`) or hand-write the migration, its `.Designer.cs` and the snapshot edit, following the files already in `Migrations/` — the repo contains both scaffolded and hand-written examples, and `RetireUpdatesOutsideMyList.cs` is the house pattern for a `migrationBuilder.Sql` data statement with its reasoning written into the file. Migrations apply at startup (`Program.cs:235`), so there is no manual deploy step either way.

## Risks / Trade-offs

- **[The backfill is one-way and time-limited]** → Its correctness rests on each anime having exactly one recorded premiere move, which the `NOT EXISTS` guard enforces per row rather than assuming globally. What it cannot do is recover a destination already overwritten by a second move before the migration runs — so the sooner this ships, the more of the six rows it captures. Rows it skips are no worse off than today.
- **[`Down` loses recorded destinations]** → Unavoidable, and symmetric with every other column-adding migration here. Noted in the migration's own comment so a future reader does not read it as an oversight.
- **[Two comments in the code currently teach the wrong rule]** → `AnimeUpdate`'s class summary and `AnimeUpdateDto`'s summary both describe the moved-from values as all that is stored. Both are corrected in this change; leaving them is how the next person rebuilds the same bug.
- **[A slot's two ends could drift apart in formatting]** → Both are stored as MAL's own JST day string and parsed by the same `ParseMalDayOfWeek`; a test asserts an unparsable stored day yields no slot line rather than a half-rendered one, which is today's behaviour for the previous slot.
- **[Ordering against `polish-updates-panel`]** → Both edit the `StartDateChanged` branch of `updateText.ts`. This change assumes that one has landed; applied first, it would collide over the same lines and lose the equal-dates guard it depends on.

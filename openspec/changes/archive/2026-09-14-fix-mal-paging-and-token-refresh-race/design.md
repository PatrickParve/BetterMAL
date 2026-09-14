## Context

This change fixes triage items R3, N3 and PF1. The proposal has the evidence with line references. This section covers only what shapes the design. Paths without a prefix are under `backend/AnimeTracker.Api/`.

**The two paging loops today.** Both live in `Services/Mal/MalClient.cs` and share `FullListPageSize = 100`.
- `GetAllPagesAsync<T>(fetchPage, onPageRead)` is private and static, and backs only `GetFullUserAnimeListAsync`. It starts at offset 0, adds each page, calls `onPageRead` with the running count, stops when `Paging?.Next is null || Data.Count == 0`, and otherwise adds 100. Its doc comment still names the season listing, which no longer uses it.
- `GetFullSeasonAsync` fetches page 0 through `GetAsyncOrNotFound`, which returns null on a 404 ("MAL has no listing for this season yet"). It then loops `while (next is not null && lastPageCount > 0)` over `GetSeasonAsync`, adding 100 each time.
- Both build their URLs themselves: `BuildSeasonUrl` and `GetUserAnimeListAsync`.

**What a `paging.next` link looks like.** MAL returns an absolute URL carrying the request's parameters as MAL writes them, with `offset` moved on. For example, `https://api.myanimelist.net/v2/users/@me/animelist?offset=100&fields=…&limit=100&nsfw=1`. The app only needs the `offset` value from it.

**Token refresh today.**
- `MalTokenProvider` is a singleton (`Program.cs:57`). It opens a DI scope per call to reach the scoped `IMalTokenStore` and `IMalOAuthService`.
- Its two public methods take `_refreshLock` and re-read the stored token inside it. `GetValidAccessTokenAsync` first checks outside the lock, and only takes the lock when the token is within its 10-minute buffer.
- `MalTokenRefreshBackgroundService` (a hosted service, `Program.cs:72`) resolves the store and OAuth service from its own scope. It reads the token and calls `RefreshAsync` itself, outside the lock.
- `MalRefreshResult` has three cases: `Refreshed(Token)`, `Refused(StatusCode, Error)` and `Unavailable(Detail)`. It's returned by `IMalOAuthService.RefreshAsync` and `IMalTokenProvider.RefreshAfterRejectionAsync`. `MalAuthPacingHandler.TryRefreshAfterRejectionAsync` switches on it, and its last arm is `default: // Unavailable`.

**Constraints.**
- The backend builds and tests only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp`. The local SDK is 9.0, and `~/Documents` can't be bind-mounted.
- `AnimeTracker.Api.csproj` already grants `InternalsVisibleTo` to the test project.
- The project uses `Microsoft.NET.Sdk.Web`, so `Microsoft.AspNetCore.WebUtilities.QueryHelpers` is available without a new package.

## Goals / Non-Goals

**Goals:**
- Both full-list reads start each next page where MAL says it starts, and can't loop forever on a bad link.
- `is_rewatching` is gone from the request, the models and the form fields, and reads still deserialize.
- No two token refreshes in this process can send the same refresh token, whichever path starts them.
- Tests for each, including the first `MalClient` tests over a stubbed `HttpClient`.

**Non-Goals:**
- Changing the page size, or requesting `paging.next` URLs directly.
- Guarding against a `next` offset that jumps more than a page ahead. See Risks.
- Removing or restructuring the request-path refresh in `GetValidAccessTokenAsync` and `RefreshAfterRejectionAsync`.
- Refresh races with anything outside this process, such as a second instance or another client using the same login.
- 403 retry behaviour in `MalAuthPacingHandler` (R1, dropped).
- Deciding the archived `add-rewatching-status` open question about pushing rewatches with `is_rewatching`.

## Decisions

### D1. One helper works out the next offset, and both loops call it

Add `private static int NextPageOffset(string? nextLink, int currentOffset)` to `MalClient`:
1. If `nextLink` is null or contains no `?`, return `currentOffset + FullListPageSize`.
2. Parse the part from the `?` onward with `QueryHelpers.ParseQuery`.
3. If `offset` has exactly one value, `int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var next)` succeeds, and `next > currentOffset`, return `next`.
4. Otherwise return `currentOffset + FullListPageSize`.

Taking the query from the first `?` works whether MAL sends an absolute or a relative link, so there's no `Uri` parsing to fail. `NumberStyles.None` rejects signs and whitespace. The `> currentOffset` rule means the offset always grows, so a link that repeats or rewinds the offset can't make the loop request the same page forever. The loop still ends the same way it does today: MAL eventually returns a page with no `next` link or no entries.

The loops change as follows:
- **`GetAllPagesAsync`:** after the existing stop check, `offset = NextPageOffset(page.Paging.Next, offset)`.
- **`GetFullSeasonAsync`:** keep `offset` as the offset of the page just read, starting at 0. At the top of each loop pass, set `offset = NextPageOffset(next, offset)` before calling `GetSeasonAsync`. The page-0 request and its 404 tolerance stay as they are.

*Alternatives considered:*
- **Request the `next` link as-is.** Rejected: the link's parameters are whatever MAL writes into it, and the client has to keep control of its own field selection and `nsfw=true`. It would also bypass `BuildSeasonUrl` and the relative base address.
- **`offset += page.Data.Count`.** Rejected. It's right only when MAL caps the page size. If MAL picks positions 0–99 and then hides two entries, a page of 98 still covers 0–99, so starting the next request at 98 reads two entries twice. The import would then try to add them twice. MAL's own offset is right in both cases.
- **Fold `GetFullSeasonAsync` into `GetAllPagesAsync`.** Rejected: the season's null-on-404 first page would need a nullable first fetch threaded through the generic helper. That's more churn for no change in behaviour, and the shared helper already gives both loops the same offset rule.

### D2. `MalClient` is tested through a real `HttpClient` over a stub handler

New `Services/Mal/MalClientTests.cs` builds `new MalClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.myanimelist.net/v2/") })`, matching `Program.cs:63-66`. The pacing handler isn't in the pipeline, so the auth-mode request option is set and ignored.

The stub answers **by the requested `offset`**, from a dictionary of scripted JSON bodies. A request for an offset that has no scripted body fails the test with a message naming the offset. It records every request URI in order. A wrong offset therefore fails loudly, instead of quietly returning another page.

Response bodies are minimal snake_case JSON:
- `{"data":[{"node":{"id":1,"title":"A1"}}, …],"paging":{"next":"…"}}`
- the user list adds `"list_status":{"status":"watching"}` to each item

Tests assert the requested offsets, the ids returned, and parameters parsed from the recorded URIs. A small helper builds a page of N ids from a starting id.

*Alternative considered:* make `NextPageOffset` `internal` and unit-test it alone. Rejected as the main approach: the bug being fixed is in how the loops use the offset, not only in how it's parsed.

### D3. `is_rewatching` is deleted, not ignored

Remove it from `UserAnimeListFields`, `MalListStatus.IsRewatching`, `MalListStatusUpdate.IsRewatching` and its `ToFormFields` line.

Reads stay safe. `JsonOptions` sets only `PropertyNamingPolicy`, and System.Text.Json's default `UnmappedMemberHandling` is `Skip`. One test in `MalClientTests` pins both sides. It calls `UpdateMyListStatusAsync` with a fully populated `MalListStatusUpdate`, over a stub that records the request body and answers with a `my_list_status` that includes `"is_rewatching":false`:
- the form sent has exactly the keys `status`, `num_watched_episodes`, `score`, `num_times_rewatched`, `start_date` and `finish_date`. That's stronger than a bare "no `is_rewatching`" check, and guards the other keys too.
- the response still deserializes, with its other fields read.

### D4. The provider gains `RefreshIfExpiringWithinAsync`, returning `MalRefreshResult?`

```csharp
/// null means no refresh was attempted: no login stored, the connection is
/// lost, or the token has more than `window` left.
Task<MalRefreshResult?> RefreshIfExpiringWithinAsync(TimeSpan window, CancellationToken ct = default);
```

The implementation:
1. Open a scope and resolve the store and OAuth service, as the other two methods do.
2. `await _refreshLock.WaitAsync(ct)`, then in `try`:
   - read the token
   - return null if it's null or `ConnectionLostAt` is set
   - return null if `token.ExpiresAt - window > DateTimeOffset.UtcNow`, the same comparison both existing checks use
   - otherwise `return await oauth.RefreshAsync(token.RefreshToken, ct)`
3. `finally` release the lock.

There's no unlocked pre-check. It runs every 6 hours, so taking an uncontended lock costs nothing, and reading only inside the lock is the point of the fix.

*Alternatives considered:*
- **Add a `NotNeeded` case to `MalRefreshResult`.** Rejected. `MalAuthPacingHandler`'s switch ends in `default: // Unavailable`, so the new case would silently be read as an outage there. `IMalOAuthService.RefreshAsync`, which shares the type, would never return it either.
- **A separate outcome enum plus a result.** Rejected: that's another type for what `null` already says. The background service only needs "nothing happened" apart from the three outcomes, and the three existing cases already carry everything its logs use.

### D5. `GetValidAccessTokenAsync` and `RefreshAfterRejectionAsync` stay as they are

The new method's locked block repeats about eight lines of `GetValidAccessTokenAsync`'s: read, lost check, freshness check, refresh. They could share a private helper. They're left separate so the request path's code, not only its behaviour, stays untouched. The two differ in what they return (an access token versus a result), so a shared helper would need a tuple return that reads worse than the repetition.

With all three methods on one lock, the startup race from the proposal plays out as follows. The stored token is expired, so both paths want a refresh.

| Who gets the lock first | What the other sees after waiting | Exchanges |
|---|---|---|
| Background, and its refresh succeeds | `GetValidAccessTokenAsync` re-reads a token with ~31 days left and returns it | 1 |
| Request path, and its refresh succeeds | `RefreshIfExpiringWithinAsync` re-reads, has more than 1 day left, returns null | 1 |
| Either, and it's refused | the connection is now lost, so the other returns null without a refresh | 1 |
| Either, and it's an outage | the token is still expired, so the other tries once itself | 2, allowed by the spec's outage rule |

### D6. The background service injects the provider and logs from the result

- `MalTokenRefreshBackgroundService(IMalTokenProvider tokenProvider, ILogger<…> logger)`. `IServiceScopeFactory` is no longer needed. Both are singletons, so there's no captive dependency.
- `RefreshIfNeededAsync` becomes `var result = await tokenProvider.RefreshIfExpiringWithinAsync(RefreshBuffer, ct);`, then a switch:
  - `null`: return. This covers the three skips that had their own `return` comments before. Keep one comment listing them.
  - `Refreshed refreshed`: log an information line, `"Refreshed the MAL access token ahead of expiry; it now expires at {ExpiresAt}."`, with `refreshed.Token.ExpiresAt`.
  - `Refused refused` and `Unavailable unavailable`: the two existing warning messages, word for word.
- `CheckInterval` (6 h), `RefreshBuffer` (1 day), the loop and its catches don't change.
- The class doc comment is reworded: the background check is the normal refresh path, and it shares the provider's lock with the request path.

**The one log message that changes.** Today's `"MAL access token expires at {ExpiresAt}; refreshing ahead of expiry."` is logged before the refresh, with the old expiry. The service no longer reads the token itself, so it logs after a successful refresh instead, with the new expiry.
- *Rejected: read the token outside the lock just to log.* It would log "refreshing" in exactly the race this change fixes, when the refresh turns out not to be needed.
- *Rejected: give the provider a logger and log inside the lock.* That adds a dependency to a class that doesn't log today, for one line.

## Risks / Trade-offs

- **MAL gives an offset more than a page ahead** → the read would skip entries, trusting the link. MAL's `next` is expected to be the current offset plus the page size, so this would be a MAL bug. The spec asks for MAL's offset. A logged warning would need a logger `MalClient` doesn't have. Accepted.
- **A `next` link whose offset never advances, and a MAL that always returns a full page with a `next` link** → the fallback keeps stepping 100 at a time. Each step is a real, paced request, and MAL returns an empty page past the end of the list, which stops the loop. This is the same end condition as today.
- **An outage on the first refresh lets the waiting caller try again** → if MAL consumed the refresh token but still answered 5xx, the second try is refused and marks the connection lost. This is the existing request-path behaviour, and the spec's outage rule allows it. Unchanged.
- **The background check waits behind a slow request-path refresh** → it blocks for at most one token-endpoint call, bounded by the `HttpClient` timeout. On shutdown, `WaitAsync(ct)` throws `OperationCanceledException`, which the loop already lets through.
- **Removing `IsRewatching` from the DTOs** → nothing reads or sets it, and no test uses it. The build catches any missed reference, and task 2.4 greps for leftovers.
- **Timing-based concurrency tests** → the new tests follow `TwoConcurrentCallsRefreshOnce`: a gate holds the first refresh, and short delays let the second caller queue. If the second caller hasn't queued before the gate opens, it still sees the refreshed token and doesn't refresh. The assertion holds either way; only that run's coverage of the waiting path is weaker.

## Migration Plan

No schema, configuration or API change. Deploy as usual. Rolling back is reverting the two commits.

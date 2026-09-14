Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`.

**Two commits.**
- Groups 1–3 (R3 and N3) are the first commit.
- Groups 4–7 (PF1) are the second.
- The spec deltas in this change get written into `openspec/specs/mal-api-integration/spec.md` when the change is archived, after group 7. That covers the added paging requirement as well as the reworded refresh requirement. Archive in the same commit as PF1, as earlier changes did.

## 1. R3: take the next page's offset from `paging.next` (design D1)

- [x] 1.1 In `Services/Mal/MalClient.cs`, add `using System.Globalization;` and `using Microsoft.AspNetCore.WebUtilities;`. Next to `FullListPageSize`, add `private static int NextPageOffset(string? nextLink, int currentOffset)` as design D1 describes:
  - take the query from the first `?`, and parse it with `QueryHelpers.ParseQuery`
  - use `offset` when it has exactly one value, parses with `NumberStyles.None` and `CultureInfo.InvariantCulture`, and is greater than `currentOffset`
  - otherwise return `currentOffset + FullListPageSize`

  Its doc comment should say:
  - MAL's offset is used because a page can hold fewer entries than asked for and still cover the full range
  - why `Data.Count` would read entries twice, in one line
  - the fallback cases, and that the `>` rule keeps the offset growing, so a bad link can't repeat a page
- [x] 1.2 In `GetAllPagesAsync`, replace `offset += FullListPageSize;` with `offset = NextPageOffset(page.Paging.Next, offset);`. `Paging` is non-null past the stop check. Fix its doc comment: it pages the full my-list only (not the season listing), and each next page starts at MAL's `next` offset.
- [x] 1.3 In `GetFullSeasonAsync`:
  - declare `var offset = 0;` for the page just read, replacing `var offset = FullListPageSize;`
  - make `offset = NextPageOffset(next, offset);` the first statement in the loop, before `GetSeasonAsync`
  - remove `offset += FullListPageSize;`

  The page-0 request keeps its literal `0` and its 404 tolerance. Add a clause to the doc comment saying later pages start at MAL's `next` offset.
- [x] 1.4 Add `Services/Mal/MalClientTests.cs` (design D2):
  - build the client as `new MalClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.myanimelist.net/v2/") })`
  - the stub `HttpMessageHandler` reads `offset` from each request's query and answers from a `Dictionary<int, (HttpStatusCode, string json)>`
  - an offset with no scripted response throws `InvalidOperationException` naming the offset
  - it records every request URI, in order. Tests read parameters back with `QueryHelpers.ParseQuery(uri.Query)`, which unescapes them, since the recorded query may have `{` and `}` escaped
  - add helpers that build a page body of N sequential ids from a starting id, with an optional `next` link, for anime-list edges and for user-list edges (the latter with `"list_status":{"status":"watching"}`)
  - a small helper can run the same script against either loop
- [x] 1.5 User-list tests, through `GetFullUserAnimeListAsync`:
  - **Short page.** Offset 0 returns ids 1–98 with `next` `…/users/@me/animelist?offset=100&limit=100`. Offset 100 returns ids 101–150 with no `next`. Requested offsets are `[0, 100]`, the result is 148 distinct ids in order, and `onPageRead` saw `[98, 148]`.
  - **`next` without an offset.** Offset 0 returns 100 ids with `next` `…/users/@me/animelist?limit=100`. Offset 100 returns 10 ids with no `next`. Requested offsets are `[0, 100]`.
  - **`next` offset that doesn't advance**, a `[Theory]` over `offset=0` and `offset=abc`. Offsets 0 and 100 each return 100 ids with that link, and offset 200 returns 5 ids with no `next`. Requested offsets are `[0, 100, 200]`.
  - **No `next`.** One page of 30 ids makes exactly one request.
  - **Empty page.** Offset 0 returns 100 ids with `next` at offset 100. Offset 100 returns no entries, with `next` at offset 200. Requested offsets are `[0, 100]`, and the result has 100 ids.
  - **The app's own parameters.** Offset 0's `next` link says `offset=100&fields=id&nsfw=1`. The second request's `fields` equals the first request's, and its `nsfw` is `true`.
- [x] 1.6 Season tests, through `GetFullSeasonAsync(2024, "spring")`, with request paths under `anime/season/2024/spring`:
  - the same short page, no-offset, non-advancing, no-`next`, empty-page and parameter cases as 1.5 (no `onPageRead` there)
  - **First page 404.** Offset 0 answers 404. The result is `null`, after exactly one request.

## 2. N3: drop `is_rewatching` (design D3)

- [x] 2.1 In `Services/Mal/MalClient.cs` `UserAnimeListFields`, remove `,is_rewatching` from the `list_status{…}` selection.
- [x] 2.2 In `Services/Mal/Dto/MalListStatus.cs`, remove `IsRewatching`.
- [x] 2.3 In `Services/Mal/Dto/MalListStatusUpdate.cs`, remove `IsRewatching` and its `ToFormFields` line.
- [x] 2.4 Grep `backend/` (excluding `bin/` and `obj/`) for `is_rewatching` and `IsRewatching`, and expect no hits. Mentions in archived OpenSpec changes are history and stay.
- [x] 2.5 In `MalClientTests`, add to the user-list parameter test from 1.5: the first request's `fields` contains `list_status{status,score,num_episodes_watched,start_date,finish_date,num_times_rewatched}` and doesn't contain `is_rewatching`.
- [x] 2.6 In `MalClientTests`, add one `UpdateMyListStatusAsync` test. Use a one-response stub that records the request body. Answer `{"status":"watching","score":8,"num_episodes_watched":3,"is_rewatching":false,"num_times_rewatched":1,"updated_at":"2026-09-14T00:00:00+00:00"}`, and send an update with every property set. Assert:
  - the request is a `PATCH` to `anime/{id}/my_list_status`
  - the form keys are exactly `status`, `num_watched_episodes`, `score`, `num_times_rewatched`, `start_date` and `finish_date`
  - the returned status has `Status == "watching"`, `Score == 8` and `NumEpisodesWatched == 3`

## 3. Build, test and docs for R3 + N3

- [x] 3.1 Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass.
- [x] 3.2 In `CODE_GUIDE.md`, below the MyAnimeList endpoints table, add one sentence: the two `GetFull…` methods read 100 entries per page, and build each next request themselves, starting at the offset in MAL's `paging.next` (or one page on, when the link has no usable offset).
- [x] 3.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern R4 used:
  - replace the R3 and N3 entries with short blockquotes pointing to the resolved file
  - update the Summary table
  - mark item 5 of Phase 3 in the Fix order done
  - update the appendix rows for ISSUES #9 and #23
- [x] 3.4 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add `## R3.` and `## N3.` sections under the code-fixes heading. Name this change in each. R3 records the fallback rule. N3 records that `mal-api-integration`'s field list already left the field out, and that the `add-rewatching-status` open question is still open.

## 4. PF1: the provider's locked refresh (design D4, D5)

- [x] 4.1 In `Services/Mal/Auth/IMalTokenProvider.cs`, add `Task<MalRefreshResult?> RefreshIfExpiringWithinAsync(TimeSpan window, CancellationToken ct = default);`. Its doc comment should say:
  - it refreshes only when a login is stored, the connection isn't lost, and no more than `window` is left
  - it holds the same lock as the other two methods, and reads the stored login inside it
  - `null` means no refresh was attempted; otherwise it returns `RefreshAsync`'s result
  - the background refresh uses it

  Reword the interface summary so it no longer says the token is only refreshed lazily.
- [x] 4.2 In `Services/Mal/Auth/MalTokenProvider.cs`, implement it as design D4 describes: a scope, `WaitAsync`, read inside the lock, the null, lost and `ExpiresAt - window > UtcNow` returns, `RefreshAsync`, and release in `finally`. Leave `GetValidAccessTokenAsync` and `RefreshAfterRejectionAsync` unchanged. Update the `_refreshLock` comment so it names all three methods, the background refresh included.
- [x] 4.3 In `Services/Mal/MalAuthPacingHandlerTests.cs`, add `RefreshIfExpiringWithinAsync` to `FakeMalTokenProvider`, throwing `NotImplementedException`.

## 5. PF1: the background service (design D6)

- [x] 5.1 In `Services/Mal/Auth/MalTokenRefreshBackgroundService.cs`:
  - change the constructor to `(IMalTokenProvider tokenProvider, ILogger<MalTokenRefreshBackgroundService> logger)`
  - in `RefreshIfNeededAsync`, call `tokenProvider.RefreshIfExpiringWithinAsync(RefreshBuffer, ct)` and switch on the result:
    - `null`: return, with one comment listing the three skips (no login, connection lost, still comfortably fresh)
    - `Refreshed refreshed`: `LogInformation("Refreshed the MAL access token ahead of expiry; it now expires at {ExpiresAt}.", refreshed.Token.ExpiresAt)`
    - `Refused` and `Unavailable`: the existing two warnings, word for word
  - keep `CheckInterval`, `RefreshBuffer`, the loop and its catches
  - reword the class doc comment: the background check is the normal refresh path, it shares the provider's lock with the request path, and that lock is what stops the startup race
- [x] 5.2 Confirm `Program.cs` needs no change: the hosted service's constructor resolves the singleton `IMalTokenProvider`. Grep for any other code constructing `MalTokenRefreshBackgroundService`.

## 6. PF1: tests (design D5)

- [x] 6.1 In `Services/Mal/Auth/MalTokenProviderTests.cs`:
  - give `Token(...)` an optional `DateTimeOffset? expiresAt` (default: now + 1 day, so the existing tests don't change)
  - give `FakeMalOAuthService` a `LastRefreshToken` it records
  - extend the class comment to cover `RefreshIfExpiringWithinAsync`
- [x] 6.2 **No login stored:** `RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1))` returns `null`, and `RefreshCallCount == 0`.
- [x] 6.3 **Lost connection** with an already-expired token: returns `null`, and `RefreshCallCount == 0`.
- [x] 6.4 **Fresh token** that expires in 30 days, with a 1-day window: returns `null`, and `RefreshCallCount == 0`.
- [x] 6.5 **Inside the window**, a token that expires in 12 hours. A `[Theory]` over the three result kinds (`Refreshed`, `Refused`, `Unavailable`), built from a string or a `MemberData`, sets `oauth.Result`. Assert:
  - the returned result is `Assert.Same` as `oauth.Result`
  - `RefreshCallCount == 1`
  - `LastRefreshToken == "refresh"`
- [x] 6.6 **Background first, then a request, on an expired token.** Set up as in `TwoConcurrentCallsRefreshOnce`: a gate, and `OnRefreshed` setting `store.Current` to `Token("refreshed", expiresAt: now + 31 days)`. That's outside both the 10-minute and the 1-day window.
  - Start `RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1))`, delay, start `GetValidAccessTokenAsync()`, delay, then open the gate.
  - Assert `RefreshCallCount == 1`, the first call's result is `Refreshed`, and the second returns `"refreshed"`.
  - The fake store's `MarkConnectionLostAsync` throws, so a lost mark would fail the test. Say so in a comment.
- [x] 6.7 **A request first, then the background check**, with the same setup. Start `GetValidAccessTokenAsync()` first, then `RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1))`. Assert `RefreshCallCount == 1`, the first call returns `"refreshed"`, and the second returns `null`.

## 7. Build, test, check and docs for PF1

- [x] 7.1 Copy `backend/` to `/private/tmp/bm-build/` again, then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass, including `MalAuthPacingHandlerTests` with its updated fake.
- [x] 7.2 Grep `backend/AnimeTracker.Api/` for `RefreshAsync(`, and confirm only `MalTokenProvider` calls `IMalOAuthService.RefreshAsync`.
- [x] 7.3 In `CODE_GUIDE.md` `Services/Mal/Auth` notes:
  - **`MalTokenProvider` bullet:** add `RefreshIfExpiringWithinAsync(window, ct)`. It takes the same `_refreshLock`, re-reads the row, and returns `null` when there's no login, the connection is lost, or the token is outside the window. Otherwise it returns `RefreshAsync`'s result.
  - **`MalTokenRefreshBackgroundService` bullet:** it refreshes through that method with a 1-day window, so it can't race a request-path refresh. Replace "so requests never pay for a refresh" with wording that says requests normally don't, and refresh themselves only when the token is already at or near expiry, for example after the app was off.
- [x] 7.4 Run `openspec validate fix-mal-paging-and-token-refresh-race --strict`, and fix anything it reports.
- [x] 7.5 In `docs/ISSUE_TRIAGE.md` (local, gitignored):
  - replace the PF1 entry with a short blockquote pointing to the resolved file
  - update the Summary table's "Partly fixed" count and the resolved row
  - mark Phase 3 of the Fix order done
  - update the appendix row for ISSUES #5
- [x] 7.6 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## PF1.` section: what was fixed, the spec rewording (the request path is allowed, and every refresh is serialized), this change's name, and the limit that remains (races outside this process).

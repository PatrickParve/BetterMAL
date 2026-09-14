## Why

This change fixes three items from `docs/ISSUE_TRIAGE.md`, Phase 3 of its fix order. All three are in `Services/Mal/`.

- **R3.** Full-list reads assume every MAL page is full. A short page that still has a `next` link would skip or repeat entries without any warning.
- **N3.** `is_rewatching` is requested and modelled, but nothing uses it.
- **PF1.** The background token refresh doesn't take the provider's refresh lock. After the app was off through the token's expiry, it can race a startup job. Valid new tokens then get stored but flagged as lost, and sync stops until you reconnect for no reason.

None of these is known to cause harm today. R3 and N3 cost a few lines each. PF1 is a real race with a confusing outcome, and the fix is small.

**Verified against the code** (2026-09-14, at `1dd4042`). Paths without a prefix are under `backend/AnimeTracker.Api/`.

- **R3: fixed-step paging.**
  - `Services/Mal/MalClient.cs:205`: `GetAllPagesAsync` does `offset += FullListPageSize` after every page. It backs `GetFullUserAnimeListAsync` (`:122-123`), which `InitialImportService.cs:45` and `ReconciliationService.cs:38` call.
  - `MalClient.cs:79,87`: `GetFullSeasonAsync` does the same. `SeasonBrowseService.cs:166` calls it.
  - Both loops stop on a missing `next` link or an empty page (`:81`, `:202`). Neither one reads the link's `offset`.
  - A short page isn't expected today. 100 is under MAL's documented page-size limits (1000 for a user list, 500 for a season), and both requests send `nsfw=true` (`:95`, `:114`), which turns off the R+/Rx filter that would hide entries after a page is picked.
- **N3: `is_rewatching` is unused.**
  - Requested in `UserAnimeListFields` (`MalClient.cs:47`).
  - Modelled in `Services/Mal/Dto/MalListStatus.cs:10` and `Services/Mal/Dto/MalListStatusUpdate.cs:15`, which also sends it from `ToFormFields` (`:25`).
  - Never read: `MalMappingExtensions.ApplyTo(MalListStatus?, …)` (`:236-246`) doesn't touch it. Never set: `EntryPushService.cs:23-31` doesn't either. No test mentions it.
  - `mal-write-sync/spec.md:515` pushes a Rewatching entry as `watching`, not through this flag.
  - `mal-api-integration/spec.md:98` ("Requests select the fields the app persists") already lists the `list_status` sub-fields without `is_rewatching`. **No spec change is needed for N3.**
  - Removing the model property is safe for reads. `MalClient.JsonOptions` (`:12-15`) sets only a naming policy, so System.Text.Json keeps its default of skipping unknown members, and no DTO has a `[JsonUnmappedMemberHandling]` attribute. The PATCH `my_list_status` response (`:135`) and `GetMyListStatusAsync` (`:141`) still contain `is_rewatching`, and they keep deserializing.
  - The archived `add-rewatching-status` design left open whether to push rewatches through `is_rewatching` instead. This change doesn't decide that. If that route is ever taken, putting the field back is three lines.
- **PF1: the background refresh skips the lock.**
  - `Services/Mal/Auth/MalTokenProvider.cs:15` holds `_refreshLock`. `GetValidAccessTokenAsync` (`:17-49`, refreshing within 10 minutes of expiry, `:8`) and `RefreshAfterRejectionAsync` (`:51-74`) both take the lock and re-read the stored token after getting it.
  - `Services/Mal/Auth/MalTokenRefreshBackgroundService.cs:43-54` reads the token and calls `IMalOAuthService.RefreshAsync` directly, without the lock. It checks every 6 hours (`:10`), starting at launch, and refreshes once less than a day is left (`:11`).
  - A refusal marks the connection lost (`MalOAuthService.cs:91-98`), and a successful save clears the mark (`MalTokenStore.cs:37`). MAL rotates refresh tokens, so when two refreshes send the same refresh token, one succeeds and the other is refused. If the refusal writes last, good tokens are stored as lost. `GetValidAccessTokenAsync` then returns null (`:24-25`), and the background check skips too (`:47-48`).
  - `mal-api-integration/spec.md:46-51` says refreshing happens "as a background concern … not on the per-request path". The code deliberately refreshes on the request path as well, and that's what covers a token that expired while the app was off.

## What Changes

- **R3: full-list reads take the next offset from MAL.** Both paging loops in `MalClient` (my list, season listing) start each next request at the `offset` in MAL's `paging.next` link.
  - If the link has no usable offset (missing, not a whole number, or not past the current offset), the next request falls back to the current offset plus one page.
  - Requests are still built by the client, with its own `fields` and `nsfw=true`. The `next` link is never requested as-is.
  - Unchanged: the stop conditions (no `next` link, or an empty page), the page size of 100, the `onPageRead` progress callback, and the season listing's 404-tolerant first page.
- **N3: `is_rewatching` is removed.** It comes out of the animelist `list_status` field selection, out of `MalListStatus` and `MalListStatusUpdate`, and out of `ToFormFields`.
- **PF1: every token refresh goes through the provider's lock.**
  - `IMalTokenProvider` gets `RefreshIfExpiringWithinAsync(TimeSpan window, CancellationToken ct)`. It takes `_refreshLock` and reads the stored token inside it. When there's no token, the connection is lost, or more than `window` is left, it returns without refreshing. Otherwise it calls `RefreshAsync` and returns that result.
  - `MalTokenRefreshBackgroundService` calls it with its 1-day window and no longer calls `IMalOAuthService.RefreshAsync` itself. Kept: the 6-hour interval, the 1-day window, the error handling and the Refused and Unavailable warnings.
  - Unchanged: the request-path refresh in `GetValidAccessTokenAsync` and `RefreshAfterRejectionAsync`, "no refresh while the connection is lost", and "concurrent rejections share one refresh".
- **Internal API shapes only.** No HTTP, database or client-visible change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `mal-api-integration`:
  - **Added:** "Full-list reads follow MAL's paging". A full read of my list or a season listing gets every page, and starts each next request at the offset MAL's `next` link gives, with a one-page fallback.
  - **Rewritten:** "Background token refresh". The token is normally refreshed in the background, well ahead of expiry. A request that finds it expired, or about to expire, may refresh it itself. Every refresh, from any path, is serialized, so a refresh token that has already been exchanged is never sent again. Adds a scenario for the app starting after the token expired.

## Impact

- **Backend** (under `backend/AnimeTracker.Api/`):
  - `Services/Mal/MalClient.cs`: a next-offset helper, the two paging loops, and the `UserAnimeListFields` string
  - `Services/Mal/Dto/MalListStatus.cs` and `Services/Mal/Dto/MalListStatusUpdate.cs`
  - `Services/Mal/Auth/IMalTokenProvider.cs` and `Services/Mal/Auth/MalTokenProvider.cs`: the new method
  - `Services/Mal/Auth/MalTokenRefreshBackgroundService.cs`: injects `IMalTokenProvider` instead of `IServiceScopeFactory`
- **Backend tests** (under `backend/AnimeTracker.Api.Tests/`):
  - **New:** `Services/Mal/MalClientTests.cs`, through a stub `HttpMessageHandler`: both paging loops, the animelist field selection, and the `my_list_status` PATCH (the form fields it sends, and a response that still contains `is_rewatching`)
  - **Extended:** `Services/Mal/Auth/MalTokenProviderTests.cs`
  - **Updated:** the `FakeMalTokenProvider` in `Services/Mal/MalAuthPacingHandlerTests.cs`, for the new interface method
- **Docs:**
  - `CODE_GUIDE.md`: the MAL endpoints section, and the `MalTokenProvider` and `MalTokenRefreshBackgroundService` notes
  - `docs/ISSUE_TRIAGE.md` and `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md` (local, gitignored): R3, N3 and PF1 move to resolved
- **Commits:** two.
  1. R3 and N3 together, since both edit `MalClient.cs`.
  2. PF1, then archiving this change, which writes both spec deltas into `openspec/specs/mal-api-integration/spec.md`.
- **Out of scope:**
  - R1: how `MalAuthPacingHandler` retries a 403. Retries are not touched.
  - changing the page size, or requesting `paging.next` links directly
  - removing the request-path token refresh
  - refresh races with anything outside this process, such as a second app instance
